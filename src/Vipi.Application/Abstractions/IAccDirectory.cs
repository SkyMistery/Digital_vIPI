namespace Vipi.Application.Abstractions;

/// <summary>
/// Posizione center (CTR) dall'anagrafica della sorgente esterna. <see cref="CenterId"/> raggruppa le
/// posizioni in un ACC (es. "LIRR"); <see cref="Callsign"/> è la singola posizione (es. "LIRR_N_CTR").
/// </summary>
public sealed record SourceCenter(
    string Callsign,
    string CenterId,
    string Name,
    bool Military,
    string? Frequency = null);

/// <summary>
/// Settore ATC (subcenter) di un ACC dalla sorgente esterna. <see cref="ComposePosition"/> è la chiave
/// naturale (callsign, es. "LIBB_ES_CTR"); <see cref="CenterId"/> è l'ACC di appartenenza.
/// <see cref="LowerLimit"/>/<see cref="UpperLimit"/> sono predisposti: oggi la sorgente IVAO non li espone (null).
/// </summary>
public sealed record SourceSubcenter(
    string ComposePosition,
    string CenterId,
    string? Position,
    string? MiddleIdentifier,
    string? Frequency,
    string? RegionMapPolygon,
    string? AtcCallsign = null,
    int? LowerLimit = null,
    int? UpperLimit = null);

/// <summary>
/// Area speciale/regolamentata di un ACC dalla sorgente (es. IVAO <c>/v2/centers/{ACC}/specialAreas</c> + dettaglio
/// <c>/v2/specialAreas/{id}</c>). <see cref="IvaoId"/> è la chiave naturale (reference per gli update); la shape
/// (<see cref="RegionMapPolygon"/>) è risolta dal dettaglio.
/// </summary>
public sealed record SourceSpecialArea(
    string IvaoId,
    string? Type,
    string Name,
    string? Description,
    string? ActivationDetails,
    int? MinimumAlt,
    int? MaximumAlt,
    bool Range,
    string CenterId,
    string? RegionMapPolygon);

/// <summary>
/// Porta verso l'anagrafica ACC/center della sorgente esterna. Gli ACC e i loro settori ATC del sito
/// sono SOLO quelli forniti dalla sorgente (sito agnostico): l'implementazione attiva è scelta da
/// DataSource:Provider (oggi IVAO: <c>/v2/centers?countryId=IT</c>).
/// </summary>
public interface IAccDirectory
{
    /// <summary>Tutte le posizioni center del paese configurato (default IT), normalizzate e ordinate per callsign.</summary>
    Task<IReadOnlyList<SourceCenter>> GetCentersAsync(CancellationToken ct = default);

    /// <summary>Settori ATC (subcenter) di un ACC, con frequenza e shape risolte dal dettaglio.</summary>
    Task<IReadOnlyList<SourceSubcenter>> GetSubcentersAsync(string accIcao, CancellationToken ct = default);

    /// <summary>Aree speciali/regolamentate di un ACC (paginato), con shape risolta dal dettaglio.</summary>
    Task<IReadOnlyList<SourceSpecialArea>> GetSpecialAreasAsync(string accIcao, CancellationToken ct = default);
}
