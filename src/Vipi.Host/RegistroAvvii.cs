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
    /// Il file lo scrivono tre fili diversi — l'avvio, lo spegnimento e il gestore del segnale — e gli ultimi due
    /// arrivano insieme: il segnale È ciò che fa partire lo spegnimento. Una riga per volta.
    /// </summary>
    private static readonly object Penna = new();

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
            lock (Penna)
            {
                var righe = File.Exists(percorso)
                    ? File.ReadAllLines(percorso, StartupDiagnostics.Codifica)
                    : Array.Empty<string>();

                var testo = new StringBuilder();
                if (righe.Length == 0) testo.Append(Intestazione());
                testo.AppendLine(RigaAvvio(versione, Verdetto(righe, _avvioUtc, ProcessoVivo), _avvioUtc, Environment.ProcessId));

                Pota(percorso, righe);
                File.AppendAllText(percorso, testo.ToString(), StartupDiagnostics.Codifica);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[vIPI] impossibile aggiornare {percorso}: {ex.Message}");
        }
    }

    /// <summary>
    /// Scrive la riga <c>SEGNALE</c>: il sistema ha chiesto a questo processo di spegnersi. La chiama il gestore
    /// di <see cref="SegnaleDiArresto"/> nel momento stesso in cui il segnale arriva.
    ///
    /// <para>🔴 <b>Perché una riga sua e non solo una parola nella riga ARRESTO</b> (25 settembre 2026). Dal passaggio
    /// a .NET 10 tutte le righe ARRESTO dicevano «DA DENTRO», anche le ~580 al giorno che sono Passenger che spegne
    /// per inattività. Il segnale arrivava, ma lo gestiscono in due: <c>ConsoleLifetime</c> di .NET, che chiama
    /// <c>StopApplication</c> — ed è DENTRO quella chiamata che si scrive la riga ARRESTO — e il nostro, che se lo
    /// annota. Se il suo passa per primo, la riga ARRESTO è già scritta quando il nostro arriva. L'ordine dei due non
    /// lo decidiamo noi e con .NET 8 ci era andata bene. Questa riga non dipende dall'ordine: si scrive quando il
    /// segnale c'è, prima o dopo l'ARRESTO.</para>
    /// </summary>
    public static void RegistraSegnale(string nome)
    {
        if (_avvioUtc == default) return;

        var percorso = StartupDiagnostics.Percorso(FileName);
        if (percorso is null) return;

        try
        {
            lock (Penna)
                File.AppendAllText(percorso, RigaSegnale(nome, DateTime.UtcNow, Environment.ProcessId) + Environment.NewLine,
                    StartupDiagnostics.Codifica);
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
            lock (Penna)
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
        RigaAvvio(versione, Verdetto(precedente, adesso), adesso, Environment.ProcessId);

    /// <summary>
    /// La riga di avvio col suo <b>pid</b>. ⚠️ Il pid sta DOPO la colonna della versione: <c>errori-per-era.py</c>
    /// legge la versione subito dopo «AVVIO», e il pid è lo stesso numero della colonna <c>pid</c> di
    /// <c>richieste-*.tsv</c> e di <c>log-*.txt</c> — è così che le tre fonti si incrociano.
    /// </summary>
    public static string RigaAvvio(string versione, string verdetto, DateTime adesso, int pid) =>
        $"{Timbro(adesso)}  AVVIO    {versione,-24}  pid {pid,-8} {verdetto}";

    /// <summary>La riga del segnale. Vedi <see cref="RegistraSegnale"/>.</summary>
    public static string RigaSegnale(string nome, DateTime adesso, int pid) =>
        $"{Timbro(adesso)}  SEGNALE  {nome} dal sistema   pid {pid}";

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
        $"{Timbro(adesso)}  ARRESTO  acceso per {Durata(uptime)}   pid {Environment.ProcessId} · " +
        $"{TracciaRichieste.Riassunto(adesso)} · {SegnaleDiArresto.Riassunto()} · memoria {MemoriaDelProcesso.Breve()}";

    /// <summary>
    /// L'ultima riga che descrive un AVVIO o un ARRESTO: le righe di commento (<c>#</c>), quelle vuote e le righe
    /// <c>SEGNALE</c> non contano. Torna <c>null</c> se il file non ne ha ancora nessuna.
    /// <para>⚠️ La riga SEGNALE può cadere DOPO l'ARRESTO (vedi <see cref="RegistraSegnale"/>): se contasse, uno
    /// spegnimento ordinato si leggerebbe come «ultimo evento non è un arresto», cioè come un crash.</para>
    /// </summary>
    public static (DateTime Quando, bool Arresto)? UltimoEvento(IReadOnlyList<string> righe)
    {
        for (var i = righe.Count - 1; i >= 0; i--)
        {
            if (Evento(righe[i]) is not { } e || e.Tipo == Segnale) continue;
            return (e.Quando, e.Tipo == Arresto);
        }

        return null;
    }

    private const string Avvio = "AVVIO", Arresto = "ARRESTO", Segnale = "SEGNALE";

    /// <summary>Una riga di evento letta: quando, che cosa, e di quale processo (le righe di prima del 25 settembre
    /// 2026 il pid non l'hanno).</summary>
    private static (DateTime Quando, string Tipo, int? Pid)? Evento(string riga)
    {
        if (string.IsNullOrWhiteSpace(riga) || riga.TrimStart().StartsWith('#')) return null;
        if (riga.Length < TimbroLunghezza) return null;

        if (!DateTime.TryParseExact(riga[..TimbroLunghezza], FormatoTimbro, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var quando))
            return null;

        var resto = riga[TimbroLunghezza..].TrimStart();
        var tipo = resto.StartsWith(Arresto, StringComparison.Ordinal) ? Arresto
            : resto.StartsWith(Segnale, StringComparison.Ordinal) ? Segnale
            : resto.StartsWith(Avvio, StringComparison.Ordinal) ? Avvio
            : null;
        if (tipo is null) return null;

        var pid = PidRegex.Match(resto) is { Success: true } m && int.TryParse(m.Groups[1].Value, out var n) ? n : (int?)null;
        return (quando, tipo, pid);
    }

    private static readonly System.Text.RegularExpressions.Regex PidRegex = new(@"\bpid (\d+)\b");

    /// <summary>
    /// Il verdetto sul processo precedente, letto <b>per pid</b>.
    ///
    /// <para>🔴 <b>Perché non basta l'ultima riga</b> (25 settembre 2026). Passenger a volte tiene DUE processi accesi
    /// insieme: alle 15:57:07 ne è partito un secondo mentre il primo serviva ancora richieste, e l'ultima riga del
    /// file — l'AVVIO del primo, senza ARRESTO dietro — faceva scrivere «NON si è spento in modo ordinato». Era vivo.
    /// Ora si guarda il processo dell'ultimo AVVIO: se ha il suo ARRESTO si è spento bene, se è ancora vivo lo si
    /// dice, e solo se non è né l'uno né l'altro è morto male.</para>
    ///
    /// <para>⚠️ Guarda solo l'ULTIMO avvio: con due processi accesi, se muore male il più vecchio dei due, il verdetto
    /// dell'avvio dopo non lo vede. Nel file resta comunque il suo AVVIO senza ARRESTO, che si legge a occhio.</para>
    /// </summary>
    /// <param name="vivo">Se il processo con quel pid è ancora acceso. In produzione <see cref="ProcessoVivo"/>.</param>
    public static string Verdetto(IReadOnlyList<string> righe, DateTime adesso, Func<int, bool> vivo)
    {
        var i = righe.Count - 1;
        for (; i >= 0; i--)
            if (Evento(righe[i]) is { Tipo: Avvio }) break;

        // Nessun avvio col pid (file nuovo, o scritto prima del 25 settembre 2026): la regola di prima.
        if (i < 0 || Evento(righe[i]) is not { Pid: int pid } avvio) return Verdetto(UltimoEvento(righe), adesso);

        DateTime? segnale = null;
        for (var j = i + 1; j < righe.Count; j++)
        {
            if (Evento(righe[j]) is not { } e || e.Pid != pid) continue;
            if (e.Tipo == Arresto)
                return $"(il precedente, pid {pid}, si era spento in modo ordinato {Durata(adesso - e.Quando)} fa)";
            if (e.Tipo == Segnale) segnale = e.Quando;
        }

        if (vivo(pid))
            return $"(il precedente, pid {pid}, è ancora acceso: Passenger ne tiene due insieme)";

        // Il segnale c'era ma l'ARRESTO no: qualcuno da fuori gli ha chiesto di chiudere, e poi non gli ha dato il
        // tempo di farlo. È la firma di un'uccisione dell'hosting, e dice di non cercare nel nostro codice.
        var coda = segnale is { } s
            ? $" — aveva ricevuto il segnale di spegnimento {Durata(adesso - s)} fa e non ha fatto in tempo a chiudere: fermato da fuori"
            : "";
        return $"⚠ il processo precedente (pid {pid}) NON si è spento in modo ordinato — era partito {Durata(adesso - avvio.Quando)} prima (crash, memoria esaurita, uccisione dall'hosting, o una dll sovrascritta via FTP){coda}";
    }

    /// <summary>
    /// Se il processo <paramref name="pid"/> è ancora acceso ed è dei nostri. ⚠️ Il nome si confronta perché i pid si
    /// riusano: un pid vecchio di un'ora può essere diventato un altro programma. Il nostro pid non conta mai come
    /// «il precedente vivo»: se l'ultimo AVVIO porta il nostro numero, quel processo è morto e il sistema ce l'ha
    /// riassegnato.
    /// </summary>
    internal static bool ProcessoVivo(int pid)
    {
        if (pid == Environment.ProcessId) return false;
        try
        {
            using var altro = System.Diagnostics.Process.GetProcessById(pid);
            using var io = System.Diagnostics.Process.GetCurrentProcess();
            return !altro.HasExited && string.Equals(altro.ProcessName, io.ProcessName, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
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
        sb.AppendLine("# ⚠ Un AVVIO il cui processo (pid) non ha mai scritto il suo ARRESTO, e che non è più acceso, è un");
        sb.AppendLine("# processo morto MALE: la riga dell'avvio dopo lo dice a parole. Lì vale la pena aprire");
        sb.AppendLine("# avvio-errore.txt e errori-richieste.txt.");
        sb.AppendLine("#");
        sb.AppendLine("# SEGNALE = il sistema ha chiesto a quel pid di spegnersi (SIGTERM: l'hosting). Può cadere prima o");
        sb.AppendLine("# dopo il suo ARRESTO. Un SEGNALE senza ARRESTO = fermato da fuori e ucciso prima di chiudere.");
        sb.AppendLine("# Nell'ARRESTO, «memoria» è quanta ne usava il processo e il suo picco: la misura che esclude");
        sb.AppendLine("# (o conferma) un limite di memoria dell'hosting.");
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
