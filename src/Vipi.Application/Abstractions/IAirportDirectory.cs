namespace Vipi.Application.Abstractions;

/// <summary>Aeroporto dall'anagrafica della sorgente esterna. <see cref="AccCode"/> = centerId (ACC di competenza).</summary>
public sealed record SourceAirport(string Icao, string Name, string? AccCode, string? City, int? TransitionAltitude = null,
    double? Latitude = null, double? Longitude = null);

/// <summary>
/// Porta verso l'anagrafica aeroporti della sorgente esterna, usata dall'editor struttura per scegliere
/// un aeroporto reale invece di digitarne ICAO/nome a mano. L'implementazione attiva è scelta via DataSource:Provider
/// (oggi IVAO: IvaoApiClient con cache di processo — dati di riferimento, cambiano di rado).
/// </summary>
public interface IAirportDirectory
{
    /// <summary>Tutti gli aeroporti del paese configurato (default IT), normalizzati e ordinati per ICAO.</summary>
    Task<IReadOnlyList<SourceAirport>> GetAirportsAsync(CancellationToken ct = default);
}
