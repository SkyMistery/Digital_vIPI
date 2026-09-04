using System.Globalization;
using System.Text;

namespace Vipi.Host;

/// <summary>
/// Una riga per ogni avvio e per ogni arresto del processo, in coda a <c>diagnostica/avvii.txt</c>.
///
/// <para><b>Perché esiste.</b> Su <c>atc.it.ivao.aero</c> capita che il browser mostri
/// «Attempting to reconnect to the server…»: è il circuito Blazor che è morto, e la causa più probabile è
/// che sia morto <b>il processo</b> — Passenger lo spegne per inattività e lo rigenera alla richiesta
/// successiva. Ma «più probabile» non è «misurato», e le altre cause danno lo stesso identico sintomo:
/// un crash, un esaurimento di memoria, un caricamento FTP sopra una dll viva.</para>
///
/// <para><b>Il file di prima non poteva rispondere.</b> <c>avvio-diagnostica.txt</c> viene
/// <b>riscritto</b> a ogni avvio: dice quando è ripartito l'ultimo, mai quanti ce ne sono stati. Tre
/// riavvii al giorno (inattività notturna, fisiologico) e quaranta (qualcosa che si rompe) producono lì
/// esattamente lo stesso file.</para>
///
/// <para><b>Come si legge l'esito.</b> Uno spegnimento per inattività è <i>ordinato</i>: Passenger manda
/// il segnale, l'host chiude, e questa classe fa in tempo a scrivere la riga <c>ARRESTO</c>. Un crash o
/// un'uccisione secca no. Quindi <b>un AVVIO preceduto da un altro AVVIO — senza ARRESTO in mezzo — è un
/// processo morto male</b>, e la riga lo dice a parole. È l'unica distinzione che conta per decidere se
/// c'è un difetto da cercare o se il sito si sta solo riposando.</para>
///
/// <para>⚠️ Non solleva mai e non fa attendere l'avvio: se il file non è scrivibile, la diagnostica tace.
/// Vale la stessa regola di <see cref="StartupDiagnostics"/> — un problema nel raccontare l'avvio non deve
/// diventare un avvio fallito.</para>
/// </summary>
public static class RegistroAvvii
{
    /// <summary>Nome del file, nella cartella <see cref="StartupDiagnostics.CartellaDiagnostica"/>.</summary>
    public const string FileName = "avvii.txt";

    /// <summary>
    /// Oltre questo numero di righe il file viene potato a <see cref="RigheTenute"/>. Cresce di due righe
    /// per riavvio: 2 000 righe sono mesi di storia su un host che ne fa qualcuno al giorno, e ~180 KB —
    /// che è quanto si scarica volentieri via FTP.
    /// </summary>
    private const int RigheMassime = 2_000;

    private const int RigheTenute = 1_000;

    /// <summary>Quando è partito QUESTO processo: serve a <see cref="RegistraArresto"/> per l'uptime.</summary>
    private static DateTime _avvioUtc;

    private static bool _arrestoScritto;

    /// <summary>
    /// Scrive la riga di avvio, e con essa il verdetto sul processo precedente. Va chiamata una volta
    /// sola, presto: quello che succede prima non è coperto.
    /// </summary>
    public static void RegistraAvvio(string versione)
    {
        _avvioUtc = DateTime.UtcNow;

        // ⚠️ La sicura contro la riga d'arresto doppia si arma QUI e non una volta per processo: in
        // produzione un processo ospita un avvio solo, ma i test d'integrazione ne accendono e spengono
        // parecchi di fila nello stesso processo, e una sicura che non si riarma farebbe scrivere l'arresto
        // solo al primo — cioè renderebbe il file dei test indistinguibile da una fila di crash.
        _arrestoScritto = false;

        var percorso = StartupDiagnostics.Percorso(FileName);
        if (percorso is null) return;

        try
        {
            var righe = File.Exists(percorso)
                ? File.ReadAllLines(percorso, StartupDiagnostics.Codifica)
                : Array.Empty<string>();

            var testo = new StringBuilder();
            if (righe.Length == 0) testo.Append(Intestazione());
            testo.AppendLine(RigaAvvio(versione, UltimoEvento(righe), _avvioUtc));

            Pota(percorso, righe);
            File.AppendAllText(percorso, testo.ToString(), StartupDiagnostics.Codifica);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[vIPI] impossibile aggiornare {percorso}: {ex.Message}");
        }
    }

    /// <summary>
    /// Scrive la riga di arresto con quanto è rimasto acceso il processo. È <b>l'assenza</b> di questa
    /// riga a raccontare il crash, quindi va agganciata allo spegnimento ordinato dell'host
    /// (<c>ApplicationStopping</c>) e deve restare economica: niente database, niente rete.
    ///
    /// <para>Idempotente: <c>ApplicationStopping</c> e <c>ApplicationStopped</c> possono arrivare
    /// entrambi, e due righe ARRESTO di fila renderebbero il file più difficile da leggere, non più
    /// ricco.</para>
    /// </summary>
    public static void RegistraArresto()
    {
        if (_arrestoScritto || _avvioUtc == default) return;
        _arrestoScritto = true;

        var percorso = StartupDiagnostics.Percorso(FileName);
        if (percorso is null) return;

        var adesso = DateTime.UtcNow;
        try
        {
            File.AppendAllText(percorso, RigaArresto(adesso - _avvioUtc, adesso) + Environment.NewLine,
                StartupDiagnostics.Codifica);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[vIPI] impossibile aggiornare {percorso}: {ex.Message}");
        }
    }

    /// <summary>La riga di avvio, verdetto compreso. Separata dall'I/O perché sia verificabile dai test.</summary>
    public static string RigaAvvio(string versione, (DateTime Quando, bool Arresto)? precedente, DateTime adesso) =>
        $"{Timbro(adesso)}  AVVIO    {versione,-24}  {Verdetto(precedente, adesso)}";

    /// <summary>
    /// La riga di arresto. Vedi <see cref="RigaAvvio"/> per il perché sia separata.
    ///
    /// <para>⚠️ Porta due misure che il solo uptime non dà, e senza le quali il file non risponde alla
    /// domanda per cui è stato scritto — «perché muore?»: <b>quante richieste</b> ha servito il processo
    /// (<see cref="TracciaRichieste"/>) e <b>chi</b> gli ha chiesto di spegnersi
    /// (<see cref="SegnaleDiArresto"/>). Una vita di cinquanta secondi con sei richieste dentro e un
    /// <c>SIGTERM</c> alla fine racconta una storia; la stessa vita con <b>una</b> richiesta ne racconta
    /// un'altra, e le due cure non si somigliano per niente.</para>
    /// </summary>
    public static string RigaArresto(TimeSpan uptime, DateTime adesso) =>
        $"{Timbro(adesso)}  ARRESTO  acceso per {Durata(uptime)}   " +
        $"{TracciaRichieste.Riassunto(adesso)} · {SegnaleDiArresto.Riassunto()}";

    /// <summary>
    /// L'ultima riga che descrive un evento: le righe di commento (<c>#</c>) e quelle vuote non contano.
    /// Torna <c>null</c> se il file non ne ha ancora nessuna.
    /// </summary>
    public static (DateTime Quando, bool Arresto)? UltimoEvento(IReadOnlyList<string> righe)
    {
        for (var i = righe.Count - 1; i >= 0; i--)
        {
            var riga = righe[i];
            if (string.IsNullOrWhiteSpace(riga) || riga.TrimStart().StartsWith('#')) continue;
            if (riga.Length < TimbroLunghezza) continue;

            if (!DateTime.TryParseExact(riga[..TimbroLunghezza], FormatoTimbro, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var quando))
                continue;

            return (quando, riga.Contains("ARRESTO", StringComparison.Ordinal));
        }

        return null;
    }

    /// <summary>
    /// Che cosa è successo al processo di prima, a parole. ⚠️ Il caso che interessa è il terzo: due AVVIO
    /// di fila vogliono dire che nessuno ha chiuso in modo ordinato.
    /// </summary>
    public static string Verdetto((DateTime Quando, bool Arresto)? precedente, DateTime adesso)
    {
        if (precedente is not { } p) return "(primo avvio registrato in questo file)";

        return p.Arresto
            ? $"(il precedente si era spento in modo ordinato {Durata(adesso - p.Quando)} fa)"
            : $"⚠ il processo precedente NON si è spento in modo ordinato — era partito {Durata(adesso - p.Quando)} prima (crash, memoria esaurita, o una dll sovrascritta via FTP)";
    }

    /// <summary>
    /// Riscrive il file tenendo solo le ultime <see cref="RigheTenute"/> righe, se ha passato il tetto.
    /// La potatura avviene all'avvio e mai durante: è il solo momento in cui costare qualche millisecondo
    /// di I/O non toglie niente a nessuno.
    /// </summary>
    private static void Pota(string percorso, string[] righe)
    {
        if (righe.Length <= RigheMassime) return;

        var tenute = new List<string>(RigheTenute + 1)
        {
            $"# … {righe.Length - RigheTenute} righe più vecchie tolte il {Timbro(DateTime.UtcNow)} per non far crescere il file.",
        };
        tenute.AddRange(righe[^RigheTenute..]);
        File.WriteAllLines(percorso, tenute, StartupDiagnostics.Codifica);
    }

    private static string Intestazione()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# vIPI — registro degli avvii. Una riga per avvio, una per arresto, sempre in coda.");
        sb.AppendLine("#");
        sb.AppendLine("# A che serve: contare i riavvii. Se sono pochi e capitano nelle ore vuote, è Passenger che");
        sb.AppendLine("# spegne il processo per inattività — normale su questo hosting, e l'unica conseguenza è che");
        sb.AppendLine("# chi aveva una pagina aperta vede il messaggio di riconnessione (la pagina si ricarica da");
        sb.AppendLine("# sola). Se sono tanti, o raggruppati nelle ore di punta, allora c'è un difetto da cercare.");
        sb.AppendLine("#");
        sb.AppendLine("# ⚠ Un AVVIO che segue un altro AVVIO, senza ARRESTO in mezzo, è un processo morto MALE: la");
        sb.AppendLine("# riga lo dice a parole. Lì vale la pena aprire avvio-errore.txt e errori-richieste.txt.");
        sb.AppendLine("#");
        sb.AppendLine($"# Gli orari sono UTC. File creato il {Timbro(DateTime.UtcNow)}.");
        sb.AppendLine(new string('#', 70));
        sb.AppendLine();
        return sb.ToString();
    }

    private const string FormatoTimbro = "yyyy-MM-dd HH:mm:ss'Z'";

    private static readonly int TimbroLunghezza = "2026-08-30 21:14:07Z".Length;

    private static string Timbro(DateTime utc) => utc.ToString(FormatoTimbro, CultureInfo.InvariantCulture);

    /// <summary>
    /// Durata leggibile senza contare le cifre: <c>00:42:26</c>, e con i giorni davanti quando ci sono.
    /// ⚠️ Non si usa il formato <c>c</c> di <see cref="TimeSpan"/>: stampa anche i tick, che qui sono rumore.
    /// </summary>
    public static string Durata(TimeSpan durata)
    {
        if (durata < TimeSpan.Zero) durata = TimeSpan.Zero;

        return durata.Days > 0
            ? $"{durata.Days}g {durata.Hours:00}:{durata.Minutes:00}:{durata.Seconds:00}"
            : $"{durata.Hours:00}:{durata.Minutes:00}:{durata.Seconds:00}";
    }
}
