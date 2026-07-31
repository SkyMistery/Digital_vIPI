namespace Vipi.Application.Abstractions;

/// <summary>
/// Nodo dell'albero di copertura. Il legame col padre è per <c>ParentCallsign</c> (cross-ACC).
/// <c>Callsign</c> è null per i nodi <see cref="HierarchyNodeKind.Airport"/> (foglie, non referenziabili come padre).
/// </summary>
/// <param name="DerivedParentCallsign">
/// Padre <b>ereditato</b> quando <paramref name="ParentCallsign"/> è null: per le posizioni d'aeroporto è quello
/// che la scaletta DEL→GND→TWR→APP assegna davvero nella proiezione
/// (<see cref="Vipi.Domain.Services.AirportPositionLadder"/>). Serve perché l'editor mostri il legame reale invece
/// di un «da assegnare» che contraddice la vista live. Null = nessun padre, né scritto né derivabile.
/// </param>
public sealed record HierarchyNode(
    HierarchyNodeKind Kind,
    int Id,
    string? Callsign,
    string Label,
    string AccCode,
    string? ParentCallsign,
    bool IsHidden,
    bool IsForeign = false,
    string CountryPrefix = "",
    string? DerivedParentCallsign = null)
{
    /// <summary>Padre effettivo: quello scritto, altrimenti quello ereditato dalla scaletta.</summary>
    public string? EffectiveParentCallsign => ParentCallsign ?? DerivedParentCallsign;

    /// <summary>Il padre non è scritto sul nodo ma dedotto: l'editor lo segnala e non lo tratta da orfano.</summary>
    public bool IsInherited => ParentCallsign is null && DerivedParentCallsign is not null;
}
