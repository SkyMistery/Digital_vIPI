namespace Vipi.Application.Abstractions;

/// <summary>Pista dalla sorgente esterna. Lunghezza in metri, bearing in gradi (se noti).</summary>
/// <param name="ThresholdLat">
/// Latitudine della SOGLIA, in gradi decimali. ⚠️ La sorgente manda una riga <b>per soglia</b>, quindi questa
/// è la posizione di <i>quella</i> testata — non del centro pista. È il dato che il vSOP militare stampa.
/// </param>
/// <param name="ElevationFt">Elevazione della soglia in piedi. Viaggia nella stessa risposta: prenderla dopo
/// sarebbe stata una seconda migrazione per un campo che era già nella busta.</param>
public sealed record SourceRunway(string Ident, int? LengthM, int? Bearing,
    double? ThresholdLat = null, double? ThresholdLon = null, int? ElevationFt = null);

/// <summary>
/// Postazione ATC dalla sorgente esterna. Il tipo si deriva dal suffisso del callsign.
/// I campi oltre Callsign/Frequency (position, middleIdentifier, regionMapPolygon, limiti) vengono dal
/// dettaglio per-posizione (<c>/v2/ATCPositions/{compose}</c>) e sono usati dal catalogo settori aeroporto.
/// </summary>
/// <param name="IvaoId">
/// Id numerico della riga alla sorgente (IVAO: <c>id</c> della postazione, presente già nella lista).
/// È l'<b>identità</b>: sopravvive a una rinomina del <paramref name="Callsign"/>, che invece è un
/// attributo. null = la sorgente non l'ha mandato (riga sintetica, o dettaglio non disponibile).
/// </param>
public sealed record SourceAtcPosition(
    string Callsign,
    string? Frequency,
    string? Position = null,
    string? MiddleIdentifier = null,
    string? RegionMapPolygon = null,
    int? LowerLimit = null,
    int? UpperLimit = null,
    // Coordinate del riferimento aeroporto (gradi decimali), dal blocco "airport" del dettaglio postazione.
    // Presenti su OGNI postazione dell'aeroporto → usate per centrare la shape tonda di fallback delle TWR.
    double? AirportLatitude = null,
    double? AirportLongitude = null,
    string? AtcCallsign = null,          // nome visualizzato IVAO, es. "Pisa Approach"
    int? IvaoId = null);

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
