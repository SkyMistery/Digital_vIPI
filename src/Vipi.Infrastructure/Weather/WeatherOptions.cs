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

    /// <summary>TTL da applicare a un risultato, in base al fatto che porti dati o sia vuoto. Minimo 1 minuto.</summary>
    public TimeSpan CacheTtlFor(bool hasData) =>
        TimeSpan.FromMinutes(Math.Max(1, hasData ? TtlMinutes : EmptyTtlMinutes));
}
