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
/// Settore ATC (subcenter) di un ACC dalla sorgente esterna. <see cref="IvaoId"/> è l'<b>identità</b>
/// (l'id numerico della sorgente, stabile attraverso una rinomina); <see cref="ComposePosition"/> è il
/// callsign (es. "LIBB_ES_CTR"), che la sorgente può cambiare; <see cref="CenterId"/> è l'ACC di appartenenza.
/// <see cref="LowerLimit"/>/<see cref="UpperLimit"/> sono predisposti: oggi la sorgente IVAO non li espone (null).
/// </summary>
/// <param name="IvaoId">
/// Id numerico della riga alla sorgente (IVAO: <c>id</c> del subcenter, presente già nella lista).
/// null = riga <b>sintetica</b>, che la sorgente non ha mai mandato: è il caso dei settori esteri
/// risolti a mano da <c>ForeignSectorResolver</c>. Per quelle l'identità resta il callsign, ed è giusto
/// così — non c'è nessun id da conservare.
/// </param>
public sealed record SourceSubcenter(
    string ComposePosition,
    string CenterId,
    string? Position,
    string? MiddleIdentifier,
    string? Frequency,
    string? RegionMapPolygon,
    string? AtcCallsign = null,
    int? LowerLimit = null,
    int? UpperLimit = null,
    int? IvaoId = null);

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

    /// <summary>Come <see cref="GetCentersAsync"/> ma per un countryId arbitrario (paesi confinanti). Usato dalla
    /// generazione vLOA per scaricare gli ACC esteri e valutarne l'adiacenza; non persiste il catalogo estero.</summary>
    Task<IReadOnlyList<SourceCenter>> GetCentersByCountryAsync(string countryId, CancellationToken ct = default);

    /// <summary>Settori ATC (subcenter) di un ACC, con frequenza e shape risolte dal dettaglio.</summary>
    Task<IReadOnlyList<SourceSubcenter>> GetSubcentersAsync(string accIcao, CancellationToken ct = default);

    /// <summary>
    /// Aree speciali/regolamentate di un ACC (paginato), con shape risolta dal dettaglio.
    /// <para>
    /// L'elenco porta già tutti i metadati; il dettaglio serve SOLO per la shape, che è la parte più stabile del
    /// dato. <paramref name="skipDetailIds"/> elenca le aree la cui shape è già in archivio e ancora buona: per
    /// quelle il dettaglio non viene chiamato e la shape torna <c>null</c>, che l'upsert interpreta come «tieni
    /// quella che hai». Insieme vuoto = scarica tutto (primo giro). Evita N chiamate per ACC a ogni import.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<SourceSpecialArea>> GetSpecialAreasAsync(
        string accIcao, IReadOnlySet<string> skipDetailIds, CancellationToken ct = default);
}
