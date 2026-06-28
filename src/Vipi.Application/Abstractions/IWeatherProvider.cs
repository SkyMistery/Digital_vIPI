namespace Vipi.Application.Abstractions;

/// <summary>Bollettino meteo di un aeroporto (METAR + TAF grezzi). F3 dati reali.</summary>
public sealed record WeatherReport(string Icao, string? Metar, string? Taf, DateTimeOffset? AsOf)
{
    public bool HasData => !string.IsNullOrWhiteSpace(Metar) || !string.IsNullOrWhiteSpace(Taf);
    public static WeatherReport Empty(string icao) => new(icao, null, null, null);
}

/// <summary>
/// Porta verso una sorgente METAR/TAF reale (impl. NOAA aviationweather.gov in Infrastructure).
/// Con cache TTL: il METAR aggiorna ~ogni ora, niente senso interrogare a ogni render.
/// </summary>
public interface IWeatherProvider
{
    Task<WeatherReport> GetAsync(string icao, CancellationToken ct = default);
}
