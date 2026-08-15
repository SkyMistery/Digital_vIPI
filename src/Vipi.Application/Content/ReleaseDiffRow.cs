namespace Vipi.Application.Content;

/// <summary>Cosa è successo a una voce fra la baseline e la release in esame.</summary>
public enum ReleaseChangeKind
{
    Added,
    Modified,
    Removed,
}

/// <summary>
/// Una voce del riepilogo differenze di una release. Il conteggio degli elementi viaggia come NUMERO e il tipo di
/// modifica come enum: prima erano frasi italiane composte in Application («Aggiunta», «3 → 5 elementi»), che la
/// UI non poteva tradurre — e comparivano così anche nel diff di una vLOA, che è un documento inglese (doc 13 §3k).
/// </summary>
/// <param name="PreviousCount">Elementi nella baseline; null se la voce non c'era.</param>
/// <param name="Count">Elementi nella release in esame; null se la voce è stata rimossa.</param>
public sealed record ReleaseDiffRow(string Label, ReleaseChangeKind Change, int? PreviousCount, int? Count);
