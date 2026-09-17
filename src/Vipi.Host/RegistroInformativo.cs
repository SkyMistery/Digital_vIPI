using System.Globalization;

namespace Vipi.Host;

/// <summary>
/// Le righe di log <b>nostre</b> (<c>Vipi.*</c>, da Information in su), una per voce, in
/// <c>diagnostica/log-AAAA-MM-GG.txt</c>: sette giorni, poi si cancellano da sole (<see cref="RegistroGiornaliero"/>).
///
/// <para><b>Perché esiste</b> (§A59). <see cref="RegistroAvvisi"/> tiene gli avvisi, e delle righe informative solo le
/// ultime dieci accanto a un avviso. Quel che fanno i lavori in background quando va tutto bene — un poll ATC, un import,
/// un giro di traduzioni, una manutenzione d'avvio — finiva su <c>stdout</c>, che su <c>atc.it.ivao.aero</c> non legge
/// nessuno. È il pezzo che serve per dire «l'import delle 03:00 ci mette il doppio da martedì».</para>
///
/// <para>⚠️ <b>Solo <c>Vipi.*</c>, per filtro DEL PROVIDER</b> (<see cref="Filtri"/>). <c>Microsoft.*</c> a Information
/// vorrebbe dire il testo di ogni query di EF — misurato il 27 agosto 2026, quattrocento volte il volume — e i suoi avvisi
/// li tiene già <see cref="RegistroAvvisi"/>.</para>
///
/// <para>⚠️ Una riga per voce: gli a capo diventano <c> ⏎ </c>, e di un'eccezione si scrive solo tipo e messaggio. Lo
/// stack sta in <c>errori-richieste.txt</c> o <c>avvisi-log.txt</c>: tre copie dello stesso stack non aiutano a leggere.</para>
/// </summary>
public sealed class RegistroInformativo : ILoggerProvider
{
    public const string Prefisso = "log";
    public const string Estensione = "txt";

    /// <summary>Le regole di filtro di questo provider, da registrare con <c>AddFilter&lt;RegistroInformativo&gt;</c>.</summary>
    internal static readonly (string? Categoria, LogLevel Livello)[] Filtri =
    {
        (null, LogLevel.None),
        ("Vipi", LogLevel.Information),
    };

    private readonly Func<DateTime> _ora;
    private readonly Action<DateTime, string> _scrivi;

    /// <summary>Vero mentre si scrive: un guasto nello scrivere che finisse nei log non deve rientrare qui.</summary>
    [ThreadStatic] private static bool _dentro;

    public RegistroInformativo() : this(() => DateTime.UtcNow,
        new RegistroGiornaliero(Prefisso, Estensione, Intestazione, RegistroGiornaliero.CartellaVera).Scrivi) { }

    internal RegistroInformativo(Func<DateTime> ora, Action<DateTime, string> scrivi)
    {
        _ora = ora;
        _scrivi = scrivi;
    }

    public ILogger CreateLogger(string categoryName) => new Ascoltatore(this, categoryName);

    public void Dispose() { }

    private sealed class Ascoltatore : ILogger
    {
        private readonly RegistroInformativo _registro;
        private readonly string _categoria;

        public Ascoltatore(RegistroInformativo registro, string categoria)
        {
            _registro = registro;
            _categoria = categoria;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        // I livelli li decide il filtro del provider (Filtri).
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information && logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel) || _dentro) return;
            try
            {
                _dentro = true;
                var ora = _registro._ora();
                _registro._scrivi(ora, Riga(ora, Environment.ProcessId, logLevel, _categoria, formatter(state, exception), exception));
            }
            catch { /* raccontare non deve mai diventare un guasto */ }
            finally { _dentro = false; }
        }
    }

    /// <summary>La riga, separata dall'I/O perché si provi da sola.</summary>
    internal static string Riga(DateTime ora, int pid, LogLevel livello, string categoria, string messaggio, Exception? ex)
    {
        var testo = UnaRiga(RegistroAvvisi.SenzaQuery(messaggio), 1000);
        if (ex is not null)
            testo += $" ‖ {ex.GetType().Name}: {UnaRiga(RegistroAvvisi.SenzaQuery(ex.Message), 300)}";
        var breve = categoria.LastIndexOf('.') is var i and >= 0 ? categoria[(i + 1)..] : categoria;
        return string.Create(CultureInfo.InvariantCulture, $"{ora:HH:mm:ss.fff} {pid} {Livello(livello)} {breve} · {testo}");
    }

    private static string UnaRiga(string s, int max)
    {
        var r = s.ReplaceLineEndings(" ⏎ ");
        return r.Length <= max ? r : r[..max] + " …(troncato)";
    }

    private static string Livello(LogLevel l) => l switch
    {
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => l.ToString()[..3].ToUpperInvariant(),
    };

    private static string Intestazione() =>
        $"""
        # vIPI — le righe di log nostre (Vipi.*, da Information in su), una per voce (§A59). Orari UTC; il giorno è nel nome.
        # Si legge con: python tools/registro-del-giorno.py <cartella diagnostica>
        #
        # Forma: ora pid livello (INF/WRN/ERR/CRT) categoria · messaggio [‖ TipoEccezione: messaggio]
        # Gli stack NON stanno qui: errori-richieste.txt e avvisi-log.txt, alla stessa ora. Le query non si scrivono mai.
        # Il file si tiene {RegistroGiornaliero.GiorniTenuti} giorni; oltre {RegistroGiornaliero.TettoByte / 1024 / 1024} MB il resto del giorno tace.

        """.Replace("\r\n", "\n");
}
