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

    /// <summary>TTL da applicare a un risultato, in base al fatto che porti dati o sia vuoto. Minimo 1 minuto.</summary>
    public TimeSpan CacheTtlFor(bool hasData) =>
        TimeSpan.FromMinutes(Math.Max(1, hasData ? TtlMinutes : EmptyTtlMinutes));
}
