namespace Vipi.Application.Abstractions;

/// <summary>Tipo di nodo nell'albero di copertura globale (Round 20).</summary>
public enum HierarchyNodeKind
{
    /// <summary>Settore ACC (subcenter), nodo interno. Da <c>AccSector</c>.</summary>
    Acc,
    /// <summary>Posizione APP d'aeroporto, nodo interno. Da <c>AirportSector</c> con Position=APP.</summary>
    App,
    /// <summary>Aeroporto: FOGLIA dell'albero (DEL/GND/TWR condividono la sua vista rapida). Da <c>Airport</c>.</summary>
    Airport,
}

/// <summary>
/// Nodo dell'albero di copertura. Il legame col padre è per <c>ParentCallsign</c> (cross-ACC).
/// <c>Callsign</c> è null per i nodi <see cref="HierarchyNodeKind.Airport"/> (foglie, non referenziabili come padre).
/// </summary>
public sealed record HierarchyNode(
    HierarchyNodeKind Kind,
    int Id,
    string? Callsign,
    string Label,
    string AccCode,
    string? ParentCallsign,
    bool IsHidden);

/// <summary>
/// Editing della gerarchia di copertura GLOBALE (cross-ACC) sui cataloghi importati (Round 20, SPEC §9.12).
/// Albero a padre unico per callsign: nodi interni = settori ACC (<c>AccSector</c>) + APP (<c>AirportSector</c>);
/// foglie = aeroporti. Ogni modifica riproietta i <c>Sector</c> operativi (<see cref="ISectorProjectionService"/>).
/// </summary>
public interface IHierarchyEditingService
{
    /// <summary>Carica tutti i nodi dell'albero (settori ACC + APP + aeroporti). DEL/GND/TWR esclusi.</summary>
    Task<IReadOnlyList<HierarchyNode>> LoadTreeAsync(CancellationToken ct = default);

    /// <summary>
    /// Imposta il padre (per callsign) del nodo indicato. <paramref name="parentCallsign"/> null = stacca (radice).
    /// Valida: il padre esiste ed è un nodo interno (ACC/APP); anti-ciclo per i nodi interni; cross-ACC ammesso.
    /// Autorizzazione ACC-scoped sul nodo figlio.
    /// </summary>
    Task SetParentAsync(HierarchyNodeKind kind, int nodeId, string? parentCallsign, CancellationToken ct = default);
}
