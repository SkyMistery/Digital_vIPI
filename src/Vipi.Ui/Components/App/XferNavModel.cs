using Vipi.Domain;

namespace Vipi.Ui.Components.App;

/// <summary>
/// Un gruppo come lo vede il navigatore: una foglia dell'albero.
/// <para>Porta i due conteggi che fanno alzare la mano — righe senza ricevente e righe da rivedere — perché
/// l'avviso deve stare dove si sceglie, non dentro il gruppo che bisogna aprire per vederlo.</para>
/// </summary>
public sealed record XferNavFlow(int Id, TransferFlowKind Kind, string? Description, int RowCount,
                                int NoReceiver, int ToReview)
{
    public bool HasWarning => NoReceiver > 0 || ToReview > 0;
}

/// <summary>Un aeroporto nel navigatore: intestazione già composta («✈ LIRF — Roma Fiumicino») e i suoi gruppi.</summary>
/// <param name="Key">Chiave di collasso, univoca nell'albero: «settore|icao».</param>
public sealed record XferNavAirport(string Key, string Header, IReadOnlyList<XferNavFlow> Flows);

/// <summary>Un settore mittente nel navigatore, col suo elenco di aeroporti.</summary>
public sealed record XferNavSector(string Callsign, IReadOnlyList<XferNavAirport> Airports)
{
    public int FlowCount => Airports.Sum(a => a.Flows.Count);

    public bool HasWarning => Airports.Any(a => a.Flows.Any(f => f.HasWarning));
}
