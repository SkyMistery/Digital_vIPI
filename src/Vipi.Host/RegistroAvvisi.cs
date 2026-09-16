using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Vipi.Host;

/// <summary>
/// Il registro degli <b>avvisi</b>: ogni riga di log <c>Warning</c>, <c>Error</c> o <c>Critical</c> del processo, su file
/// in <c>diagnostica/avvisi-log.txt</c>, con il <b>contesto</b> per ricostruirla — le ultime dieci richieste servite e
/// le ultime dieci righe informative nostre.
///
/// <para><b>Perché esiste</b> (chiesto dal committente il 16 settembre 2026). <see cref="DiagnosticaErrori"/> e
/// <see cref="DiagnosticaCircuito"/> portano in <c>diagnostica/</c> le <b>eccezioni</b>; tutto il resto che il
/// processo dice — un import che rinuncia, un rate limit, una manutenzione d'avvio che salta, un avviso del
/// framework — finisce su <c>stdout</c>, che su <c>atc.it.ivao.aero</c> non legge nessuno. E una riga d'errore da
/// sola dice che cosa è successo, non che cosa stava succedendo: per quello servono le richieste di prima.</para>
///
/// <para>⚠️ <b>Le ripetizioni.</b> Il processo rinasce ogni ~50 secondi (<c>avvii.txt</c>): un avviso che scatta a
/// ogni avvio, scritto intero ogni volta, riempirebbe i 512 kB in poche ore e porterebbe via la storia che serve.
/// Quindi ogni <b>firma</b> (categoria, evento, modello del messaggio, tipo e punto nostro dell'eccezione) si
/// scrive intera <b>una volta al giorno</b>, e le ripetizioni valgono <b>una riga all'ora</b>. La memoria di che
/// cosa è già stato scritto sta nel file stesso, perché quella del processo dura cinquanta secondi.</para>
///
/// <para>⚠️ <b>Che cosa NON entra.</b> Le stringhe di query — su <c>/signin-oidc</c> portano il <c>code</c> OAuth —
/// né nel percorso delle richieste né dentro un messaggio; cookie e intestazioni non li scrive nessun log di
/// questo processo. Le richieste dei ping (<c>/vsop/health</c>) e dei file statici non occupano i dieci posti: non
/// raccontano niente, e col ping ogni dieci secondi sarebbero tutti loro.</para>
///
/// <para>⚠️ <b>Gli errori che hanno già lo stack altrove</b> (richieste fallite, guasti del circuito) qui portano il
/// contesto e il rimando a <see cref="DiagnosticaErrori.NomeFile"/>, non lo stack una seconda volta.</para>
/// </summary>
public sealed class RegistroAvvisi : ILoggerProvider
{
    public const string NomeFile = "avvisi-log.txt";
    public const string NomeFilePrecedente = "avvisi-log-precedenti.txt";
    internal const long TettoByte = 512 * 1024;

    /// <summary>Quante richieste e quante righe informative si tengono in memoria per il contesto.</summary>
    internal const int Contesto = 10;

    /// <summary>La categoria dei log di richiesta di ASP.NET Core: «Request finished» porta metodo, percorso, esito e durata.</summary>
    internal const string CategoriaRichieste = "Microsoft.AspNetCore.Hosting.Diagnostics";

    /// <summary>L'evento «Request finished» di <see cref="CategoriaRichieste"/> (<c>LoggerEventIds.RequestFinished</c>).</summary>
    internal const int EventoRichiestaFinita = 2;

    /// <summary>
    /// Le regole di filtro <b>di questo provider</b>, da registrare con <c>AddFilter&lt;RegistroAvvisi&gt;</c>. Servono
    /// perché in produzione <c>Microsoft.AspNetCore</c> sta a <c>Warning</c> (<c>appsettings.json</c>), e allora le
    /// richieste non arriverebbero. ⚠️ Una regola di provider vince SEMPRE su quelle generiche: per questo non c'è un
    /// «Information per tutti», che accenderebbe per noi il testo di ogni query di EF.
    /// </summary>
    internal static readonly (string? Categoria, LogLevel Livello)[] Filtri =
    {
        (null, LogLevel.Warning),
        ("Vipi", LogLevel.Information),
        (CategoriaRichieste, LogLevel.Information),
    };

    /// <summary>Le categorie i cui errori hanno già lo stack in <see cref="DiagnosticaErrori.NomeFile"/>.</summary>
    private static readonly string[] ConStackAltrove =
    {
        DiagnosticaErrori.CategoriaLog,
        "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware",
        "Microsoft.AspNetCore.Components.Server",
    };

    private readonly Func<DateTime> _ora;
    private readonly Action<string, string?> _scrivi;
    private readonly Func<string?> _leggi;
    private readonly object _serratura = new();
    private readonly Queue<string> _richieste = new();
    private readonly Queue<string> _righe = new();

    /// <summary>firma → ultima riga scritta (UTC). null finché non si è letto il file.</summary>
    private Dictionary<string, DateTime>? _scritte;

    /// <summary>Vero mentre si scrive: un guasto nello scrivere che finisse nei log non deve rientrare qui.</summary>
    [ThreadStatic] private static bool _dentro;

    public RegistroAvvisi() : this(() => DateTime.UtcNow, ScriviSuFile, LeggiFile) { }

    /// <param name="scrivi">Aggiunge testo al file; il secondo argomento, se non null, è l'intestazione di un file nuovo.</param>
    internal RegistroAvvisi(Func<DateTime> ora, Action<string, string?> scrivi, Func<string?> leggi)
    {
        _ora = ora;
        _scrivi = scrivi;
        _leggi = leggi;
    }

    public ILogger CreateLogger(string categoryName) => new Ascoltatore(this, categoryName);

    public void Dispose() { }

    private sealed class Ascoltatore : ILogger
    {
        private readonly RegistroAvvisi _registro;
        private readonly string _categoria;

        public Ascoltatore(RegistroAvvisi registro, string categoria)
        {
            _registro = registro;
            _categoria = categoria;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        // I livelli li decide il filtro del provider (Filtri): qui si esclude solo quel che non serve mai.
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information && logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel) || _dentro) return;
            try
            {
                _dentro = true;
                _registro.Ricevi(_categoria, logLevel, eventId, state, exception, formatter);
            }
            catch { /* raccontare un avviso non deve mai diventare un guasto */ }
            finally { _dentro = false; }
        }
    }

    internal void Ricevi<TState>(string categoria, LogLevel livello, EventId evento, TState state, Exception? ex,
        Func<TState, Exception?, string> formatter)
    {
        var ora = _ora();

        if (categoria == CategoriaRichieste)
        {
            // Solo «Request finished» (evento 2). ⚠️ Non basta riconoscerlo dai valori: anche «Request reached the end
            // of the middleware pipeline» (evento 16) porta percorso ed esito, e la stessa richiesta comparirebbe due
            // volte — visto dal vivo il 16 settembre 2026 su un 404.
            if (livello < LogLevel.Warning)
            {
                if (evento.Id == EventoRichiestaFinita && RigaDiRichiesta(state, ora) is { } riga)
                    lock (_serratura) Accoda(_richieste, riga);
                return;
            }
        }

        var messaggio = SenzaQuery(formatter(state, ex));

        if (livello < LogLevel.Warning)
        {
            lock (_serratura) Accoda(_righe, $"{ora:HH:mm:ss} {Breve(categoria)} · {Tronca(messaggio, 300)}");
            return;
        }

        var firma = Firma(categoria, evento, state, ex);
        lock (_serratura)
        {
            _scritte ??= FirmeGiaScritte(_leggi());

            var stackAltrove = ex is not null && ConStackAltrove.Any(c => categoria.StartsWith(c, StringComparison.Ordinal));
            if (_scritte.TryGetValue(firma, out var ultima) && ultima.Date == ora.Date)
            {
                if (ora - ultima < TimeSpan.FromHours(1)) return;
                _scrivi(RigaDiRipetizione(ora, livello, categoria, messaggio, firma), Intestazione());
            }
            else
            {
                _scrivi(Voce(ora, livello, categoria, evento, messaggio, ex, stackAltrove, firma), Intestazione());
            }
            _scritte[firma] = ora;
        }
    }

    private static void Accoda(Queue<string> coda, string riga)
    {
        coda.Enqueue(riga);
        while (coda.Count > Contesto) coda.Dequeue();
    }

    /// <summary>
    /// La riga di una richiesta finita, dai valori strutturati del log e NON dal testo: il testo porta l'URL intero,
    /// query compresa. Null per le richieste che non vale la pena ricordare.
    /// </summary>
    internal static string? RigaDiRichiesta<TState>(TState state, DateTime ora)
    {
        if (state is not IReadOnlyList<KeyValuePair<string, object?>> valori) return null;
        string? V(string k) => valori.FirstOrDefault(x => x.Key == k).Value?.ToString();

        var percorso = V("Path");
        var codice = V("StatusCode");
        if (percorso is null || codice is null) return null;   // è «Request starting», o un'altra riga
        if (DaNonRicordare(percorso)) return null;

        var ms = valori.FirstOrDefault(x => x.Key == "ElapsedMilliseconds").Value is double d
            ? d.ToString("0", CultureInfo.InvariantCulture) + " ms"
            : "";
        return $"{ora:HH:mm:ss} {V("Method")} {V("PathBase")}{percorso} → {codice} {ms}".TrimEnd();
    }

    /// <summary>I ping e i file statici: col ping ogni dieci secondi, i dieci posti sarebbero tutti loro.</summary>
    internal static bool DaNonRicordare(string percorso) =>
        percorso.StartsWith("/vsop/health", StringComparison.OrdinalIgnoreCase)
        || percorso.StartsWith("/_framework/", StringComparison.OrdinalIgnoreCase)
        || percorso.StartsWith("/_content/", StringComparison.OrdinalIgnoreCase)
        || Regex.IsMatch(percorso, @"\.(css|js|map|png|jpe?g|gif|svg|ico|woff2?|br|gz)$", RegexOptions.IgnoreCase);

    /// <summary>Via ogni stringa di query da un testo: su <c>/signin-oidc</c> è una credenziale.</summary>
    internal static string SenzaQuery(string testo) =>
        Regex.Replace(testo, @"(https?://[^\s?#""']*|(?<![\w?])/[^\s?#""']*)\?[^\s""']*", "$1?…");

    /// <summary>
    /// Chi è questo avviso, a meno dei valori: categoria, evento, il MODELLO del messaggio (non il testo, che porta
    /// numeri e nomi) e, se c'è, il tipo dell'eccezione col primo punto nostro. Due righe con la stessa firma
    /// raccontano la stessa cosa.
    /// </summary>
    internal static string Firma<TState>(string categoria, EventId evento, TState state, Exception? ex)
    {
        var modello = state is IReadOnlyList<KeyValuePair<string, object?>> valori
            ? valori.FirstOrDefault(x => x.Key == "{OriginalFormat}").Value?.ToString()
            : null;
        var chiave = $"{categoria}|{evento.Id}|{modello ?? state?.ToString()}|{ex?.GetType().FullName}|{PrimoPuntoNostro(ex)}";
        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(chiave));
        return Convert.ToHexString(hash, 0, 6).ToLowerInvariant();
    }

    private static string PrimoPuntoNostro(Exception? ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
            foreach (var riga in (e.StackTrace ?? "").Split('\n'))
            {
                var t = riga.Trim();
                if (!t.StartsWith("at Vipi.", StringComparison.Ordinal)) continue;
                var i = t.IndexOf(" in ", StringComparison.Ordinal);
                return i > 0 ? t[3..i] : t[3..];
            }
        return "";
    }

    private string Voce(DateTime ora, LogLevel livello, string categoria, EventId evento, string messaggio,
        Exception? ex, bool stackAltrove, string firma)
    {
        var sb = new StringBuilder()
            .AppendLine()
            .AppendLine(new string('-', 78))
            .AppendLine($"{ora:yyyy-MM-dd HH:mm:ss} UTC · {Livello(livello)} · firma {firma}")
            .AppendLine($"{categoria}{(evento.Id != 0 ? $" · evento {evento.Id}{(evento.Name is { } n ? $" {n}" : "")}" : "")}")
            .AppendLine()
            .AppendLine(Tronca(messaggio, 4000));

        if (ex is not null)
        {
            sb.AppendLine();
            if (stackAltrove)
                sb.AppendLine($"{ex.GetType().Name}: {Tronca(SenzaQuery(ex.Message), 500)}")
                  .AppendLine($"(lo stack sta in {DiagnosticaErrori.NomeFile}, alla stessa ora)");
            else
                sb.AppendLine(Tronca(SenzaQuery(ex.ToString()), 8000));
        }

        sb.AppendLine().AppendLine($"Le ultime richieste servite da questo processo ({_richieste.Count}):");
        if (_richieste.Count == 0) sb.AppendLine("  (nessuna: il processo è appena partito, o sono stati solo ping)");
        foreach (var r in _richieste) sb.AppendLine("  " + r);

        sb.AppendLine().AppendLine($"Le ultime righe informative nostre ({_righe.Count}):");
        if (_righe.Count == 0) sb.AppendLine("  (nessuna)");
        foreach (var r in _righe) sb.AppendLine("  " + r);

        return sb.ToString();
    }

    private static string RigaDiRipetizione(DateTime ora, LogLevel livello, string categoria, string messaggio, string firma) =>
        $"ANCORA {ora:yyyy-MM-dd HH:mm:ss} UTC · {Livello(livello)} · firma {firma} · {Breve(categoria)} · "
        + Tronca(messaggio.ReplaceLineEndings(" "), 200) + Environment.NewLine;

    /// <summary>
    /// Le firme già scritte OGGI, lette dal file: la memoria del processo dura cinquanta secondi, quella del file no.
    /// Riconosce le due forme di riga che contengono una firma — l'intestazione di una voce e le righe ANCORA.
    /// </summary>
    internal static Dictionary<string, DateTime> FirmeGiaScritte(string? contenuto)
    {
        var esito = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(contenuto)) return esito;

        foreach (Match m in Regex.Matches(contenuto,
                     @"^(?:ANCORA )?(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}) UTC · \w+ · firma ([0-9a-f]{12})", RegexOptions.Multiline))
        {
            if (!DateTime.TryParseExact(m.Groups[1].Value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var quando)) continue;
            var firma = m.Groups[2].Value;
            if (!esito.TryGetValue(firma, out var prima) || quando > prima) esito[firma] = quando;
        }
        return esito;
    }

    private static string Livello(LogLevel l) => l switch
    {
        LogLevel.Warning => "AVVISO",
        LogLevel.Error => "ERRORE",
        LogLevel.Critical => "CRITICO",
        _ => l.ToString().ToUpperInvariant(),
    };

    /// <summary>L'ultimo pezzo della categoria: nelle righe di contesto il nome intero ruberebbe la riga.</summary>
    private static string Breve(string categoria) =>
        categoria.LastIndexOf('.') is var i and >= 0 ? categoria[(i + 1)..] : categoria;

    private static string Tronca(string s, int max) => s.Length <= max ? s : s[..max] + " …(troncato)";

    private static string Intestazione() =>
        $"""
        vIPI — avvisi ed errori del processo (Warning, Error, Critical). I più recenti stanno in fondo.

        Ogni voce porta le ultime {Contesto} richieste servite da quel processo e le ultime {Contesto} righe
        informative nostre: è quel che stava succedendo, non solo quel che è successo. Ping e file statici non
        contano fra le richieste; le stringhe di query non vengono mai scritte.

        Una stessa FIRMA si scrive intera una volta al giorno; le ripetizioni sono righe ANCORA, al massimo una
        all'ora. Gli errori con uno stack hanno lo stack in {DiagnosticaErrori.NomeFile}: qui c'è il contesto.

        Il file si mette da parte come {NomeFilePrecedente} quando supera {TettoByte / 1024} kB.

        """;

    private static void ScriviSuFile(string testo, string? intestazione)
    {
        if (StartupDiagnostics.Percorso(NomeFile) is not { } file) return;
        try
        {
            var info = new FileInfo(file);
            if (info.Exists && info.Length > TettoByte && StartupDiagnostics.Percorso(NomeFilePrecedente) is { } vecchio)
                File.Move(file, vecchio, overwrite: true);
            if (!File.Exists(file) && intestazione is not null)
                File.WriteAllText(file, intestazione, StartupDiagnostics.Codifica);
            File.AppendAllText(file, testo, StartupDiagnostics.Codifica);
        }
        catch { /* un file che non si scrive non deve fermare niente */ }
    }

    private static string? LeggiFile()
    {
        try
        {
            return StartupDiagnostics.Percorso(NomeFile) is { } file && File.Exists(file)
                ? File.ReadAllText(file, StartupDiagnostics.Codifica)
                : null;
        }
        catch { return null; }
    }
}
