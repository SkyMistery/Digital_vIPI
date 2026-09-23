using System.Globalization;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Core.Disco;

/// <summary>
/// Che cosa dice il controllo prima di scrivere (carta F3 §2.4, passi 1 e 2, e §9.6): i file fuori dai confini, quelli
/// cambiati sul disco dall'apertura, e i problemi del validatore che le modifiche <b>aggiungono</b>. Non tocca niente.
/// </summary>
/// <param name="DaSalvare">I file con modifiche in sospeso, in ordine.</param>
/// <param name="FuoriDaiConfini">File che il Lab non può scrivere: non dovrebbero mai esserci, ma la guardia è qui.</param>
/// <param name="Conflitti">File che sul disco non sono più quelli dell'apertura (o non ci sono più).</param>
/// <param name="ProblemiNuovi">Errori e avvisi che ci sarebbero DOPO e non c'erano PRIMA, file per file.</param>
/// <param name="RecordCheSiFondono">File che, riletti dai byte nuovi, avrebbero meno record di quelli in memoria.</param>
public sealed record ControlloDelSalvataggio(
    IReadOnlyList<string> DaSalvare,
    IReadOnlyList<string> FuoriDaiConfini,
    IReadOnlyList<string> Conflitti,
    IReadOnlyList<ProblemaDelSector> ProblemiNuovi,
    IReadOnlyList<RecordCheSiFondono> RecordCheSiFondono)
{
    /// <summary>Vero se non si può scrivere affatto: un file fuori dai confini, o uno cambiato sotto i piedi.</summary>
    public bool Fermo => FuoriDaiConfini.Count > 0 || Conflitti.Count > 0;

    /// <summary>Gli ERRORI nuovi: sono loro a chiedere conferma (§9.6). Gli avvisi nuovi si dicono e basta.</summary>
    public IReadOnlyList<ProblemaDelSector> ErroriNuovi => [.. ProblemiNuovi.Where(p => p.Gravita == Gravita.Errore)];

    /// <summary>Si chiede conferma per gli errori nuovi (§9.6) e per i record che, riletti, non tornerebbero.</summary>
    public bool ChiedeConferma => ErroriNuovi.Count > 0 || RecordCheSiFondono.Count > 0;
}

/// <summary>
/// Un file che riletto dal disco avrebbe <paramref name="Riletti"/> record invece dei <paramref name="InMemoria"/>
/// dell'app. Succede nei formati dove un record è «le righe consecutive con lo stesso nome» (<c>.artcc</c>,
/// <c>.mva</c>, <c>.vrt</c>, <c>.lairway</c>…): un record aggiunto copiando il vicino ha il suo nome, e finché non lo si
/// rinomina, per Aurora è un pezzo del vicino (misurato sull'albero vero nella slice 9: 63 file su 695).
/// </summary>
public sealed record RecordCheSiFondono(string File, int InMemoria, int Riletti);

public enum StatoDelSalvataggio
{
    /// <summary>Nessuna modifica in sospeso: niente da fare.</summary>
    NienteDaSalvare,

    /// <summary>Tutti i file scritti, riletti e tornati puliti.</summary>
    Salvato,

    /// <summary>Non si è scritto niente: confini o conflitto (il controllo dice quali file).</summary>
    Fermo,

    /// <summary>Non si è scritto niente: ci sono errori nuovi, e l'AOD non ha ancora confermato.</summary>
    DaConfermare,

    /// <summary>Un file non si è potuto scrivere (o non è tornato quello atteso): i precedenti sono salvati, gli altri no.</summary>
    Interrotto,
}

/// <summary>Com'è andato un salvataggio: che cosa è stato scritto, dove sta il backup, e se qualcosa si è fermato, perché.</summary>
/// <param name="Salvati">I file scritti e riletti: le loro modifiche non sono più in sospeso.</param>
/// <param name="Invariati">I file le cui modifiche davano gli stessi byte del disco: non si scrivono, e tornano puliti.</param>
/// <param name="NonSalvati">I file rimasti con le modifiche in sospeso (dopo un'interruzione).</param>
/// <param name="CartellaDelBackup">Dove sono i byte di prima dei file salvati. Nulla se non si è scritto niente.</param>
/// <param name="Errore">Che cosa ha interrotto il salvataggio, col nome del file.</param>
/// <param name="ProblemiDopo">I problemi del validatore sui file salvati, riletti dal disco (passo 5).</param>
public sealed record EsitoDelSalvataggio(
    StatoDelSalvataggio Stato,
    ControlloDelSalvataggio Controllo,
    IReadOnlyList<string> Salvati,
    IReadOnlyList<string> Invariati,
    IReadOnlyList<string> NonSalvati,
    string? CartellaDelBackup,
    string? Errore,
    int ProblemiDopo);

/// <summary>
/// Il salvataggio (carta F3 §2.4, slice 9): la prima cosa del Lab che scrive nel clone del sector. Per ogni file con
/// modifiche, in quest'ordine, e al primo intoppo ci si ferma <b>prima di scrivere</b>:
/// <list type="number">
/// <item><b>confini</b> — solo <c>Include/IT/**</c>, <c>update.ini</c>, <c>changelog.md</c> (<see cref="Confini"/>);</item>
/// <item><b>conflitto</b> — l'impronta sul disco dev'essere ancora quella dell'apertura;</item>
/// <item><b>backup</b> — i byte di prima in <c>%LOCALAPPDATA%\VipiSectorLab\backup\&lt;aaaammgg-hhmmss&gt;\&lt;percorso&gt;</c>,
/// fuori dal clone, tenuti 30 giorni;</item>
/// <item><b>scrittura atomica</b> — <c>.tmp</c> + <c>File.Replace</c>, coi byte dello scrittore vero, e qualche tentativo
/// se Aurora o un antivirus tengono il file (carta §7);</item>
/// <item><b>rilettura</b> — i byte sul disco devono essere quelli attesi; il file si rilegge, e la sua impronta nuova
/// diventa quella di riferimento.</item>
/// </list>
/// <para>I passi 1 e 2 si fanno su <b>tutti</b> i file prima di scriverne uno: un conflitto nel quinto file non deve
/// lasciare i primi quattro salvati e l'ultimo no. Un guasto durante la scrittura invece sì (il disco non si
/// prenota): i file già scritti restano salvati, e l'esito dice quale file si è fermato.</para>
/// </summary>
public sealed class Salvataggio
{
    /// <summary>Quanto si tengono i backup (carta §2.4, §9.5).</summary>
    public static readonly TimeSpan TenutaDeiBackup = TimeSpan.FromDays(30);

    private const string FormaDellaCartella = "yyyyMMdd-HHmmss";

    private readonly SessioneAperta _sessione;
    private readonly ModificheInSospeso _modifiche;
    private readonly string _cartellaDeiBackup;
    private readonly Action<string, byte[]> _scrivi;
    private readonly int _tentativi;
    private readonly TimeSpan _pausa;

    /// <param name="cartellaDeiBackup">La cartella dei backup (di solito <c>%LOCALAPPDATA%\VipiSectorLab\backup</c>).</param>
    /// <param name="scrivi">La scrittura atomica; di base quella del motore. Si cambia solo nei test.</param>
    public Salvataggio(SessioneAperta sessione, ModificheInSospeso modifiche, string cartellaDeiBackup,
                       Action<string, byte[]>? scrivi = null, int tentativi = 3, TimeSpan? pausa = null)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        ArgumentNullException.ThrowIfNull(modifiche);
        ArgumentException.ThrowIfNullOrEmpty(cartellaDeiBackup);
        ArgumentOutOfRangeException.ThrowIfLessThan(tentativi, 1);

        _sessione = sessione;
        _modifiche = modifiche;
        _cartellaDeiBackup = Path.GetFullPath(cartellaDeiBackup);
        _scrivi = scrivi ?? FileSaverOrchestrator.ScriviAtomico;
        _tentativi = tentativi;
        _pausa = pausa ?? TimeSpan.FromMilliseconds(250);
    }

    /// <summary>
    /// I passi 1 e 2 e i problemi nuovi, senza scrivere niente: è quello che la finestra mostra prima di salvare.
    /// </summary>
    public ControlloDelSalvataggio Controlla() => Controlla(out _);

    private ControlloDelSalvataggio Controlla(out Dictionary<string, (byte[] Prima, byte[] Dopo)> byteDeiFile)
    {
        byteDeiFile = new Dictionary<string, (byte[] Prima, byte[] Dopo)>(StringComparer.Ordinal);
        var daSalvare = _modifiche.FileToccati;
        var fuori = new List<string>();
        var conflitti = new List<string>();
        var nuovi = new List<ProblemaDelSector>();
        var fusi = new List<RecordCheSiFondono>();

        foreach (string relativo in daSalvare)
        {
            // 1. Confini: il controllo sta nel Core, qualunque cosa chieda la pagina.
            if (!Confini.Scrivibile(_sessione.Cartella, relativo) || !_sessione.File.TryGetValue(relativo, out var file)
                || file is not IFileConRecord conRecord)
            {
                fuori.Add(relativo);
                continue;
            }

            // 2. Conflitto. I byte letti qui sono gli stessi che finiscono nel backup: fra il controllo e la copia non
            // c'è una seconda lettura che potrebbe vedere un file diverso.
            string percorso = _sessione.Cartella.Assoluto(relativo);
            byte[]? prima = LeggiSeC(percorso);
            if (prima is null || Impronta.Di(prima) != file.Impronta)
            {
                conflitti.Add(relativo);
                continue;
            }

            byte[] dopo = conRecord.ByteDelFile(_modifiche.SporchiDi(relativo));
            byteDeiFile[relativo] = (prima, dopo);
            var (problemi, riletti) = RiletturaDiProva(relativo, percorso, dopo);
            nuovi.AddRange(problemi);
            if (riletti is { } quanti && quanti != file.Record)
                fusi.Add(new RecordCheSiFondono(relativo, file.Record, quanti));
        }

        return new ControlloDelSalvataggio(daSalvare, fuori, conflitti, nuovi, fusi);
    }

    /// <summary>
    /// Salva. Con errori nuovi e senza <paramref name="confermato"/> non scrive niente e torna
    /// <see cref="StatoDelSalvataggio.DaConfermare"/>: la conferma è dell'AOD, non del Lab (§9.6).
    /// </summary>
    /// <param name="adesso">L'ora del backup (nome della cartella) e della pulizia dei vecchi.</param>
    public EsitoDelSalvataggio Salva(bool confermato, DateTime adesso)
    {
        // Il controllo si RIFA qui, non si prende quello mostrato: fra il clic su «Salva» e la conferma un git pull
        // può aver cambiato un file.
        var controllo = Controlla(out var byteDeiFile);
        if (controllo.DaSalvare.Count == 0)
            return Esito(StatoDelSalvataggio.NienteDaSalvare, controllo);
        if (controllo.Fermo)
            return Esito(StatoDelSalvataggio.Fermo, controllo, nonSalvati: controllo.DaSalvare);
        if (controllo.ChiedeConferma && !confermato)
            return Esito(StatoDelSalvataggio.DaConfermare, controllo, nonSalvati: controllo.DaSalvare);

        PulisciIVecchi(adesso);

        var salvati = new List<string>();
        var invariati = new List<string>();
        string? backup = null;
        int problemiDopo = 0;

        foreach (string relativo in controllo.DaSalvare)
        {
            var (prima, dopo) = byteDeiFile[relativo];
            string percorso = _sessione.Cartella.Assoluto(relativo);

            // Modifiche che si annullano a vicenda (un vertice spostato e rimesso a mano): niente da scrivere.
            if (prima.AsSpan().SequenceEqual(dopo))
            {
                _modifiche.Dimentica(relativo);
                invariati.Add(relativo);
                continue;
            }

            try
            {
                // 3. Backup, fuori dal clone. Una cartella per salvataggio: quel che c'era prima di QUESTO clic.
                backup ??= NuovaCartellaDiBackup(adesso);
                string copia = Path.Combine(backup, relativo.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(copia)!);
                File.WriteAllBytes(copia, prima);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return Interrotto($"Il backup di «{relativo}» non si è potuto fare ({e.Message}): il file non è stato toccato.");
            }

            // 4. Scrittura atomica, con qualche tentativo.
            if (Scrivi(percorso, dopo) is { } guasto)
            {
                return Interrotto($"«{relativo}» non si è potuto scrivere dopo {_tentativi} tentativi ({guasto}). " +
                                  "Aurora o un antivirus lo tengono aperto? Sul disco è rimasto quello di prima.");
            }

            // 5. Rilettura: sul disco dev'esserci esattamente quel che si è scritto.
            byte[]? scritti = LeggiSeC(percorso);
            if (scritti is null || !scritti.AsSpan().SequenceEqual(dopo))
            {
                return Interrotto($"«{relativo}» riletto dal disco non è quello scritto: qualcuno lo ha cambiato subito " +
                                  $"dopo. I byte di prima sono in «{backup}».");
            }

            _sessione.Rileggi(relativo);
            _modifiche.Dimentica(relativo);
            problemiDopo += Validatore.ValidaIlFile(percorso, relativo).Count;
            salvati.Add(relativo);
        }

        return new EsitoDelSalvataggio(StatoDelSalvataggio.Salvato, controllo, salvati, invariati, [], backup, null, problemiDopo);

        EsitoDelSalvataggio Interrotto(string perche)
            => new(StatoDelSalvataggio.Interrotto, controllo, salvati, invariati,
                   [.. controllo.DaSalvare.Except(salvati).Except(invariati)], backup, perche, problemiDopo);
    }

    private static EsitoDelSalvataggio Esito(StatoDelSalvataggio stato, ControlloDelSalvataggio controllo,
                                             IReadOnlyList<string>? nonSalvati = null)
        => new(stato, controllo, [], [], nonSalvati ?? [], null, null, 0);

    /// <summary>
    /// Il file coi byte nuovi, letto e validato PRIMA di scrivere (<see cref="RiletturaDiProva"/>: il clone non si
    /// tocca finché non si salva davvero). Torna i problemi che il file avrebbe DOPO e non ha PRIMA, e quanti record
    /// ne rilegge il motore.
    /// <para>I problemi si confrontano per regola e testo della riga, non per numero di riga: un record aggiunto sopra
    /// fa scorrere i numeri di tutti gli errori vecchi, che non sono nuovi. Si contano, però: un record copiato da un
    /// vicino che ha un errore porta un errore in più, e quello è nuovo.</para>
    /// </summary>
    private static (List<ProblemaDelSector> Nuovi, int? Riletti) RiletturaDiProva(string relativo, string percorso, byte[] dopo)
    {
        var prima = Validatore.ValidaIlFile(percorso, relativo);
        var (poi, riletti) = Sessione.RiletturaDiProva.Con(relativo, dopo, copia =>
            (Validatore.ValidaIlFile(copia, relativo), Sessione.RiletturaDiProva.QuantiRecord(copia)));

        var restano = prima.GroupBy(Chiave).ToDictionary(g => g.Key, g => g.Count());
        var nuovi = new List<ProblemaDelSector>();
        foreach (var problema in poi)
        {
            var chiave = Chiave(problema);
            if (restano.TryGetValue(chiave, out int quanti) && quanti > 0)
                restano[chiave] = quanti - 1;
            else
                nuovi.Add(problema);
        }

        return (nuovi, riletti);

        static (Regola, string) Chiave(ProblemaDelSector p) => (p.Regola, p.Testo.Trim());
    }

    /// <summary>La scrittura atomica, ritentata: torna nullo se è andata, se no il messaggio dell'ultimo guasto.</summary>
    private string? Scrivi(string percorso, byte[] dopo)
    {
        for (int giro = 1; ; giro++)
        {
            try
            {
                _scrivi(percorso, dopo);
                return null;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                if (giro == _tentativi)
                    return e.Message;
                if (_pausa > TimeSpan.Zero)
                    Thread.Sleep(_pausa);
            }
        }
    }

    /// <summary>Una cartella nuova per questo salvataggio; se nello stesso secondo ce n'è già una, un suffisso.</summary>
    private string NuovaCartellaDiBackup(DateTime adesso)
    {
        string nome = adesso.ToString(FormaDellaCartella, CultureInfo.InvariantCulture);
        string cartella = Path.Combine(_cartellaDeiBackup, nome);
        for (int n = 2; Directory.Exists(cartella); n++)
            cartella = Path.Combine(_cartellaDeiBackup, $"{nome}-{n}");
        Directory.CreateDirectory(cartella);
        return cartella;
    }

    /// <summary>
    /// Toglie i backup più vecchi di 30 giorni. Si guarda il NOME della cartella (è l'ora del salvataggio), non la data
    /// del disco, che una copia o un antivirus cambiano; una cartella dal nome che non è di un backup non si tocca.
    /// </summary>
    private void PulisciIVecchi(DateTime adesso)
    {
        if (!Directory.Exists(_cartellaDeiBackup))
            return;

        foreach (string cartella in Directory.EnumerateDirectories(_cartellaDeiBackup))
        {
            string nome = Path.GetFileName(cartella);
            string data = nome.Length >= FormaDellaCartella.Length ? nome[..FormaDellaCartella.Length] : nome;
            if (!DateTime.TryParseExact(data, FormaDellaCartella, CultureInfo.InvariantCulture, DateTimeStyles.None, out var quando))
                continue;
            if (adesso - quando <= TenutaDeiBackup)
                continue;

            try
            {
                Directory.Delete(cartella, recursive: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Un backup vecchio che non si toglie non ferma il salvataggio: ci si riprova la volta dopo.
            }
        }
    }

    private static byte[]? LeggiSeC(string percorso)
    {
        try
        {
            return File.ReadAllBytes(percorso);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }
}
