namespace Vipi.Application.Abstractions;

/// <summary>
/// Editing della gerarchia di copertura GLOBALE (cross-ACC) sui cataloghi importati (Round 20, SPEC §9.12).
/// Albero a padre unico per callsign: nodi interni = settori ACC (<c>AccSector</c>) + APP (<c>AirportSector</c>);
/// foglie = aeroporti. Ogni modifica riproietta i <c>Sector</c> operativi (<see cref="ISectorProjectionService"/>).
/// </summary>
public interface IHierarchyEditingService
{
    /// <summary>Carica tutti i nodi dell'albero (settori ACC + APP + aeroporti). DEL/GND/TWR esclusi.</summary>
    Task<IReadOnlyList<HierarchyNode>> LoadTreeAsync(CancellationToken ct = default);

    /// <summary>Callsign (ComposePosition) dei settori ESTERI che confinano geometricamente con almeno un settore
    /// domestico. Per mostrare, nell'editor struttura, solo i settori esteri realmente al confine con l'Italia.</summary>
    Task<IReadOnlySet<string>> ListConfiningForeignCallsignsAsync(CancellationToken ct = default);

    /// <summary>
    /// Imposta il padre (per callsign) del nodo indicato. <paramref name="parentCallsign"/> null = stacca (radice).
    /// Valida: il padre esiste ed è un nodo interno (ACC/APP); anti-ciclo per i nodi interni; cross-ACC ammesso.
    /// Autorizzazione ACC-scoped sul nodo figlio.
    /// </summary>
    Task SetParentAsync(HierarchyNodeKind kind, int nodeId, string? parentCallsign, CancellationToken ct = default);
}
