namespace Vipi.Application.Content;

/// <summary>Cosa è successo a una voce fra la baseline e la release in esame.</summary>
/// <remarks><see cref="Hidden"/> e <see cref="Shown"/> (S133): la sezione c'è in tutt'e due, ma il lettore smette di
/// vederla o torna a vederla. Prima il confronto non guardava la visibilità.</remarks>
public enum ReleaseChangeKind
{
    Added,
    Modified,
    Removed,
    Hidden,
    Shown,
}

/// <summary>
/// Una voce del riepilogo differenze di una release. Il conteggio degli elementi viaggia come NUMERO e il tipo di
/// modifica come enum: prima erano frasi italiane composte in Application («Aggiunta», «3 → 5 elementi»), che la
/// UI non poteva tradurre — e comparivano così anche nel diff di una vLOA, che è un documento inglese (doc 13 §3k).
/// </summary>
/// <param name="PreviousCount">Elementi nella baseline; null se la voce non c'era.</param>
/// <param name="Count">Elementi nella release in esame; null se la voce è stata rimossa.</param>
/// <param name="Lines">Le righe di testo tolte e aggiunte (S133, <see cref="TestoDelConfronto"/>); per una sezione
/// aggiunta o tolta, tutto il suo testo. Null = nessuna riga di testo cambiata.</param>
public sealed record ReleaseDiffRow(string Label, ReleaseChangeKind Change, int? PreviousCount, int? Count,
    IReadOnlyList<TextDiffLine>? Lines = null)
{
    /// <summary>Le righe di testo cambiate, mai null.</summary>
    public IReadOnlyList<TextDiffLine> Righe => Lines ?? Array.Empty<TextDiffLine>();

    /// <summary>Vero se è cambiato il testo, e non solo il numero di elementi o la visibilità.</summary>
    public bool TestoCambiato => Lines is { Count: > 0 };
}
