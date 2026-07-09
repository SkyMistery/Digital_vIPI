namespace Vipi.Application.Abstractions;

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
    bool IsHidden,
    bool IsForeign = false,
    string CountryPrefix = "");
