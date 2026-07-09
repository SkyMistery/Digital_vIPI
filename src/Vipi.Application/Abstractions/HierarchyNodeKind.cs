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
