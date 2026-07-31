namespace Vipi.Application.Abstractions;

/// <summary>Tipo di nodo nell'albero di copertura globale (Round 20).</summary>
public enum HierarchyNodeKind
{
    /// <summary>Settore ACC (subcenter), nodo interno. Da <c>AccSector</c>.</summary>
    Acc,
    /// <summary>Posizione d'aeroporto (APP · TWR · GND · DEL), nodo interno. Da <c>AirportSector</c>, ATIS escluso.
    /// Si chiamava <c>App</c> quando qui entravano i soli avvicinamenti.</summary>
    AirportPosition,
    /// <summary>Aeroporto: FOGLIA dell'albero (DEL/GND/TWR condividono la sua vista rapida). Da <c>Airport</c>.</summary>
    Airport,
}
