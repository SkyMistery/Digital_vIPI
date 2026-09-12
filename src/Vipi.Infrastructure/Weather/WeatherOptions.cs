namespace Vipi.Infrastructure.Weather;

/// <summary>Config della sorgente meteo (sezione "Weather" di appsettings). Default: NOAA aviationweather.gov.</summary>
public sealed class WeatherOptions
{
    public const string SectionName = "Weather";

    /// <summary>Base delle API NOAA Aviation Weather.</summary>
    public string BaseUrl { get; set; } = "https://aviationweather.gov";

    /// <summary>TTL della cache per ICAO in minuti (il METAR aggiorna ~oraria).</summary>
    public int TtlMinutes { get; set; } = 10;

    /// <summary>
    /// TTL della cache per un esito vuoto (nessun bollettino / servizio irraggiungibile). Deliberatamente molto
    /// più corto di <see cref="TtlMinutes"/>: col TTL pieno un blip di pochi secondi di NOAA azzererebbe il meteo
    /// dell'aeroporto per tutta la finestra normale.
    /// </summary>
    public int EmptyTtlMinutes { get; set; } = 1;

    /// <summary>
    /// La sorgente METAR di <b>scorta</b>, interrogata solo quando la principale non dà il bollettino.
    /// <c>{0}</c> è l'ICAO. Vuoto = nessuna scorta.
    ///
    /// <para>⚠️ È VATSIM e non l'altro servizio di NOAA (<c>tgftp.nws.noaa.gov</c>), che pure risponde: una
    /// scorta che sta sullo stesso fornitore cade insieme alla principale, e allora non è una scorta.</para>
    /// </summary>
    public string FallbackMetarUrl { get; set; } = "https://metar.vatsim.net/{0}";

    /// <summary>Come si chiama la scorta a schermo, accanto al METAR che ha dato lei.</summary>
    public string FallbackMetarName { get; set; } = "VATSIM";

    /// <summary>
    /// Quanto si aspetta la sorgente principale, in secondi. ⚠️ Molto meno del timeout della
    /// <c>HttpClient</c> (10 s), che resta il tetto ultimo: qui si decide quanto vale la pena aspettare **un
    /// bollettino meteo**, e la risposta non è «quanto un'operazione qualsiasi».
    ///
    /// <para>🔴 Misurato l'8 settembre 2026 con NOAA che <b>non risponde</b> (un host che ingoia i pacchetti,
    /// non un DNS morto — quello fallisce in 4 ms e non prova niente): aprire un aeroporto costava
    /// <b>20 secondi</b>. Erano due attese da dieci in fila, METAR e TAF, su due bollettini che non hanno
    /// nulla da spartire.</para>
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Quanto si aspetta <b>ogni</b> scorta, in secondi — una per una, non tutta la catena insieme.
    ///
    /// <para>⚠️ Più corto della principale, e deve restarlo: una scorta serve a coprire un buco <b>in
    /// fretta</b>. Una scorta lenta quanto ciò che sostituisce non salva la pagina, la fa aspettare due
    /// volte.</para>
    ///
    /// <para>🔴 <b>Era il budget della CATENA fino al 12 settembre 2026</b>, e la prima scorta che si
    /// piantava se lo portava via intero: la seconda, che il METAR ce l'aveva, non veniva nemmeno chiamata.
    /// Il prezzo del cambio è dichiarato: nel caso pessimo si aspetta questo tetto <b>una volta per
    /// sorgente</b> (oggi due scorte ⇒ 6 s dopo i 5 della principale). Quel che va sorvegliato è il
    /// <b>numero</b> delle scorte, non il tetto della singola.</para>
    /// </summary>
    public int FallbackTimeoutSeconds { get; set; } = 3;

    /// <summary>Il tetto d'attesa della principale, minimo un secondo.</summary>
    public TimeSpan Timeout => TimeSpan.FromSeconds(Math.Max(1, TimeoutSeconds));

    /// <summary>Il tetto d'attesa di UNA scorta — non della catena — minimo un secondo.</summary>
    public TimeSpan FallbackTimeout => TimeSpan.FromSeconds(Math.Max(1, FallbackTimeoutSeconds));

    /// <summary>TTL da applicare a un risultato, in base al fatto che porti dati o sia vuoto. Minimo 1 minuto.</summary>
    public TimeSpan CacheTtlFor(bool hasData) =>
        TimeSpan.FromMinutes(Math.Max(1, hasData ? TtlMinutes : EmptyTtlMinutes));
}
