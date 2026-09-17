using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Vipi.Host;

/// <summary>
/// Lo scrittore dei file «del giorno» in <c>diagnostica/</c>: <c>&lt;prefisso&gt;-AAAA-MM-GG.&lt;estensione&gt;</c>, uno per
/// giorno UTC, tenuti <see cref="GiorniTenuti"/> giorni, con un tetto di byte per file. Lo usano
/// <see cref="RegistroRichieste"/> e <see cref="RegistroInformativo"/> (§A59, carta
/// <c>docs/feature/2026-09-17-registro-del-giorno.md</c>).
///
/// <para>⚠️ <b>Il tetto si decide sulla lunghezza VERA del file</b>, letta dal flusso aperto in coda, e non su un
/// contatore del processo: su <c>atc.it.ivao.aero</c> due processi Passenger vivi insieme sono già successi (§A55), e due
/// contatori vorrebbero dire il doppio del tetto. Per la stessa ragione la riga di troncamento si riconosce in coda al
/// file, non in memoria.</para>
///
/// <para>⚠️ <b>Una scrittura per riga, senza buffer.</b> Passenger uccide il processo una decina di secondi dopo l'ultima
/// richiesta: un buffer da svuotare «dopo» perderebbe proprio le righe che raccontano come è finita. Misurato il
/// 17 settembre 2026: poche migliaia di righe al giorno, un I/O che non si vede.</para>
///
/// <para>⚠️ Non solleva mai: un file che non si scrive non deve fermare una richiesta né un log.</para>
/// </summary>
public sealed class RegistroGiornaliero
{
    /// <summary>Oggi più i sei giorni prima.</summary>
    internal const int GiorniTenuti = 7;

    /// <summary>Dieci volte il volume misurato: ferma un guasto che scrivesse a raffica, non il traffico vero.</summary>
    internal const long TettoByte = 5 * 1024 * 1024;

    internal const string Troncato = "# troncato:";

    private readonly string _prefisso;
    private readonly string _estensione;
    private readonly Func<string> _intestazione;
    private readonly Func<string?> _cartella;
    private readonly long _tetto;
    private readonly object _serratura = new();

    /// <summary>Il giorno per cui questo processo ha già fatto la pulizia.</summary>
    private DateTime _pulito;

    /// <summary>Il giorno il cui file questo processo ha già visto troncato: da lì in poi non si apre nemmeno.</summary>
    private DateTime _troncato;

    /// <param name="cartella">La cartella dei file; <c>null</c> se non ce n'è una scrivibile (allora si tace).</param>
    internal RegistroGiornaliero(string prefisso, string estensione, Func<string> intestazione, Func<string?> cartella,
        long tetto = TettoByte)
    {
        _prefisso = prefisso;
        _estensione = estensione;
        _intestazione = intestazione;
        _cartella = cartella;
        _tetto = tetto;
    }

    /// <summary>La cartella <c>diagnostica/</c> vera, la stessa degli altri file.</summary>
    internal static string? CartellaVera() =>
        StartupDiagnostics.Percorso(RegistroAvvii.FileName) is { } file ? Path.GetDirectoryName(file) : null;

    internal string NomeFile(DateTime giorno) =>
        $"{_prefisso}-{giorno.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.{_estensione}";

    /// <summary>Aggiunge una riga (l'a capo lo mette qui) al file del giorno di <paramref name="ora"/>.</summary>
    public void Scrivi(DateTime ora, string riga)
    {
        try
        {
            if (_cartella() is not { } cartella) return;
            var giorno = ora.Date;

            lock (_serratura)
            {
                if (_pulito != giorno)
                {
                    _pulito = giorno;
                    Pulisci(cartella, giorno);
                }

                if (_troncato == giorno) return;

                // ⚠️ In coda e con la condivisione aperta: l'FTP legge mentre si scrive, e un secondo processo scrive
                // anche lui. Due righe nello stesso istante da due processi possono ancora pestarsi (la coda si cerca
                // all'apertura): a poche migliaia di righe al giorno è un rischio che si accetta.
                using var flusso = new FileStream(Path.Combine(cartella, NomeFile(giorno)), FileMode.Append, FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete);

                if (flusso.Length >= _tetto)
                {
                    if (!FiniscePerTroncato(Path.Combine(cartella, NomeFile(giorno))))
                        Aggiungi(flusso, $"{Troncato} superati {_tetto / 1024} kB alle {ora:HH:mm:ss} UTC, il resto del giorno non si scrive.");
                    _troncato = giorno;
                    return;
                }

                if (flusso.Length == 0)
                {
                    var preambolo = StartupDiagnostics.Codifica.GetPreamble();
                    flusso.Write(preambolo, 0, preambolo.Length);
                    var testa = StartupDiagnostics.Codifica.GetBytes(_intestazione());
                    flusso.Write(testa, 0, testa.Length);
                }

                Aggiungi(flusso, riga);
            }
        }
        catch { /* raccontare non deve mai diventare un guasto */ }
    }

    private static void Aggiungi(FileStream flusso, string riga)
    {
        var dati = StartupDiagnostics.Codifica.GetBytes(riga + "\n");
        flusso.Write(dati, 0, dati.Length);
    }

    /// <summary>Se l'ultima riga è già quella di troncamento: un altro processo, o questo, l'ha scritta.</summary>
    private static bool FiniscePerTroncato(string file)
    {
        using var lettura = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var n = (int)Math.Min(512, lettura.Length);
        lettura.Seek(-n, SeekOrigin.End);
        var coda = new byte[n];
        lettura.ReadExactly(coda);
        var testo = Encoding.UTF8.GetString(coda).TrimEnd('\n');
        var ultima = testo[(testo.LastIndexOf('\n') + 1)..];
        return ultima.StartsWith(Troncato, StringComparison.Ordinal);
    }

    /// <summary>Cancella i file di QUESTO prefisso più vecchi di <see cref="GiorniTenuti"/> giorni. Gli altri non si toccano.</summary>
    private void Pulisci(string cartella, DateTime oggi)
    {
        var limite = oggi.AddDays(-(GiorniTenuti - 1));
        var forma = new Regex($"^{Regex.Escape(_prefisso)}-(\\d{{4}}-\\d{{2}}-\\d{{2}})\\.{Regex.Escape(_estensione)}$");
        foreach (var file in Directory.EnumerateFiles(cartella, $"{_prefisso}-*.{_estensione}"))
        {
            var m = forma.Match(Path.GetFileName(file));
            if (!m.Success) continue;
            if (!DateTime.TryParseExact(m.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var giorno)) continue;
            if (giorno >= limite) continue;
            try { File.Delete(file); } catch { /* lo riprova il prossimo processo */ }
        }
    }
}
