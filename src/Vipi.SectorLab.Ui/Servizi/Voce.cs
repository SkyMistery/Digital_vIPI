using System.Runtime.InteropServices;

namespace Vipi.SectorLab.Ui.Servizi;

/// <summary>
/// La voce che legge l'anteprima di un modello ATIS (lotto «Subito» slice 18c, «file per file» W2): i <c>.atis</c> sono
/// scritti per essere pronunciati («aitis», «mlpainsa», «lee,NAH,teh»), e una pronuncia si giudica a orecchio.
/// </summary>
public interface IVoce
{
    /// <summary>Le voci installate, col nome che mostra Windows; vuoto se non ce ne sono.</summary>
    IReadOnlyList<string> Voci { get; }

    /// <summary>La voce che si propone: la prima inglese (i modelli sono in inglese), se no la prima.</summary>
    string? DiBase { get; }

    /// <summary>Perché non si può ascoltare niente, o null.</summary>
    string? PercheNo { get; }

    /// <summary>Comincia a leggere (e smette quel che stava leggendo). Falso se non si può: lo dice <see cref="PercheNo"/>.</summary>
    bool Leggi(string testo, string? voce = null);

    void Ferma();
}

/// <summary>Nessuna voce: i test, e i sistemi che non sono Windows.</summary>
public sealed class VoceMuta : IVoce
{
    public IReadOnlyList<string> Voci => [];

    public string? DiBase => null;

    public string? PercheNo => "Qui non c'è una voce: «Ascolta» funziona nel Lab su Windows.";

    public bool Leggi(string testo, string? voce = null) => false;

    public void Ferma()
    {
    }
}

/// <summary>
/// Le voci di Windows (SAPI 5, <c>SAPI.SpVoice</c>), chiamate per nome senza un pacchetto in più: sono quelle che ogni
/// programma di Windows trova installate. 🟡 Che Aurora legga l'ATIS con queste, e con quale, non sta scritto da
/// nessuna parte: la voce si sceglie nell'editor.
/// </summary>
public sealed class VoceDiWindows : IVoce, IDisposable
{
    // SpeechVoiceSpeakFlags: SVSFlagsAsync = 1, SVSFPurgeBeforeSpeak = 2. SpeechStreamFileMode: SSFMCreateForWrite = 3.
    private const int InSottofondo = 1, SmettiPrima = 2, CreaPerScrivere = 3;

    private readonly object _uno = new();
    private readonly dynamic _voce;
    private readonly List<(string Nome, object Gettone, string Lingua)> _voci = [];

    private VoceDiWindows(dynamic voce)
    {
        _voce = voce;
        dynamic gettoni = voce.GetVoices();
        for (int i = 0; i < (int)gettoni.Count; i++)
        {
            dynamic gettone = gettoni.Item(i);
            string lingua;
            try
            {
                lingua = (string)gettone.GetAttribute("Language");
            }
            catch (COMException)
            {
                lingua = "";
            }

            _voci.Add(((string)gettone.GetDescription(), (object)gettone, lingua));
        }
    }

    /// <summary>La voce di Windows, o null se non c'è (un altro sistema, SAPI assente).</summary>
    public static VoceDiWindows? Crea()
    {
        if (!OperatingSystem.IsWindows() || Type.GetTypeFromProgID("SAPI.SpVoice") is not { } tipo)
            return null;
        try
        {
            return Activator.CreateInstance(tipo) is { } voce ? new VoceDiWindows(voce) : null;
        }
        catch (Exception e) when (e is COMException or InvalidCastException or MissingMethodException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            return null;
        }
    }

    public IReadOnlyList<string> Voci => [.. _voci.Select(v => v.Nome)];

    // Le lingue di SAPI sono LCID in esadecimale, separati da «;»: 409 inglese (USA), 809 inglese (Regno Unito)…
    public string? DiBase
        => _voci.Where(v => v.Lingua.Split(';').Any(l => l.Trim().EndsWith("09", StringComparison.Ordinal))).Select(v => v.Nome).FirstOrDefault()
           ?? _voci.Select(v => v.Nome).FirstOrDefault();

    public string? PercheNo => _voci.Count == 0 ? "Windows non ha voci installate (Impostazioni → Data/ora e lingua → Voce)." : null;

    public bool Leggi(string testo, string? voce = null)
    {
        if (_voci.Count == 0 || string.IsNullOrWhiteSpace(testo))
            return false;
        lock (_uno)
        {
            try
            {
                Scegli(_voce, voce);
                _voce.Speak(testo, InSottofondo | SmettiPrima);
                return true;
            }
            catch (COMException)
            {
                return false;
            }
        }
    }

    public void Ferma()
    {
        lock (_uno)
        {
            try
            {
                _voce.Speak("", InSottofondo | SmettiPrima);
            }
            catch (COMException)
            {
                // Non stava parlando.
            }
        }
    }

    /// <summary>
    /// Scrive la lettura in un file <c>.wav</c> invece di mandarla alle casse: i test la provano così, senza far
    /// rumore sul PC di chi li lancia.
    /// </summary>
    public bool ScriviSuFile(string testo, string percorso, string? voce = null)
    {
        if (!OperatingSystem.IsWindows() || _voci.Count == 0 || Type.GetTypeFromProgID("SAPI.SpVoice") is not { } tipoDellaVoce
            || Type.GetTypeFromProgID("SAPI.SpFileStream") is not { } tipoDelFlusso)
        {
            return false;
        }

        // Una voce a parte: quella di «Ascolta» resta legata alle casse.
        dynamic lettore = Activator.CreateInstance(tipoDellaVoce)!;
        dynamic flusso = Activator.CreateInstance(tipoDelFlusso)!;
        try
        {
            flusso.Open(percorso, CreaPerScrivere, false);
            lettore.AudioOutputStream = flusso;
            Scegli(lettore, voce);
            lettore.Speak(testo, 0);
            return true;
        }
        finally
        {
            flusso.Close();
            Marshal.FinalReleaseComObject((object)flusso);
            Marshal.FinalReleaseComObject((object)lettore);
        }
    }

    private void Scegli(dynamic lettore, string? voce)
    {
        string? nome = voce ?? DiBase;
        if (_voci.FirstOrDefault(v => v.Nome == nome) is { Gettone: not null } scelta)
            lettore.Voice = (dynamic)scelta.Gettone;
    }

    public void Dispose()
    {
        Ferma();
        if (OperatingSystem.IsWindows())
            Marshal.FinalReleaseComObject((object)_voce);
    }
}
