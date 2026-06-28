namespace Vipi.Infrastructure.Weather;

/// <summary>Config della sorgente meteo (sezione "Weather" di appsettings). Default: NOAA aviationweather.gov.</summary>
public sealed class WeatherOptions
{
    public const string SectionName = "Weather";

    /// <summary>Base delle API NOAA Aviation Weather.</summary>
    public string BaseUrl { get; set; } = "https://aviationweather.gov";

    /// <summary>TTL della cache per ICAO in minuti (il METAR aggiorna ~oraria).</summary>
    public int TtlMinutes { get; set; } = 10;
}
