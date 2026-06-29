namespace Vipi.Application.Abstractions;

/// <summary>Pista dalla sorgente esterna. Lunghezza in metri, bearing in gradi (se noti).</summary>
public sealed record SourceRunway(string Ident, int? LengthM, int? Bearing);

/// <summary>
/// Postazione ATC dalla sorgente esterna. Il tipo si deriva dal suffisso del callsign.
/// I campi oltre Callsign/Frequency (position, middleIdentifier, regionMapPolygon, limiti) vengono dal
/// dettaglio per-posizione (<c>/v2/ATCPositions/{compose}</c>) e sono usati dal catalogo settori aeroporto.
/// </summary>
public sealed record SourceAtcPosition(
    string Callsign,
    string? Frequency,
    string? Position = null,
    string? MiddleIdentifier = null,
    string? RegionMapPolygon = null,
    int? LowerLimit = null,
    int? UpperLimit = null);

/// <summary>
/// Porta verso i dettagli per-aeroporto della sorgente esterna (postazioni ATC + piste), usata per generare
/// automaticamente il documento di aeroporto. L'implementazione attiva è scelta via DataSource:Provider (oggi IVAO).
/// </summary>
public interface IAirportDetailProvider
{
    /// <summary>Postazioni ATC pubblicate per l'aeroporto (DEL/GND/TWR/APP…), callsign + frequenza grezzi.</summary>
    Task<IReadOnlyList<SourceAtcPosition>> GetAtcPositionsAsync(string icao, CancellationToken ct = default);
    /// <summary>Dettaglio di una singola postazione ATC (frequenza + shape + limiti se esposti); null se non disponibile.</summary>
    Task<SourceAtcPosition?> GetAtcPositionDetailAsync(string composePosition, CancellationToken ct = default);
    /// <summary>Piste dell'aeroporto (identificativo + dimensioni se note).</summary>
    Task<IReadOnlyList<SourceRunway>> GetRunwaysAsync(string icao, CancellationToken ct = default);
}
