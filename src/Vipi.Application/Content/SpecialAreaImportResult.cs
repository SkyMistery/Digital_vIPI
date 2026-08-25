namespace Vipi.Application.Content;

/// <summary>
/// Esito dell'import aree speciali su tutti gli ACC: conteggi aggregati + eventuali ACC saltati
/// (<see cref="Failures"/>), che il chiamante Infra logga come warning.
/// </summary>
public sealed record SpecialAreaImportResult(
    int Created, int Updated, int Removed, IReadOnlyList<SpecialAreaImportFailure> Failures)
{
    /// <summary>Nessun lavoro svolto: categoria esclusa dalla policy di import (nessuna fetch, nessun prune).</summary>
    public static SpecialAreaImportResult Empty { get; } =
        new(0, 0, 0, Array.Empty<SpecialAreaImportFailure>());
}

/// <summary>Un'area toccata da un import, con quel poco che serve per raccontarlo: l'id e il nome.</summary>
public sealed record SpecialAreaRef(string IvaoId, string Name);

/// <summary>
/// Esito dell'upsert delle aree. ⚠️ <paramref name="Changed"/> non è <paramref name="Updated"/>: «aggiornata»
/// vuol dire che l'import è passato di lì, «cambiata» che qualcosa che un documento MOSTRA è diverso da prima.
/// Confonderle riempirebbe la casella degli impatti ogni notte, per ogni area, senza che sia successo niente.
/// </summary>
public sealed record SpecialAreaUpsertOutcome(int Created, int Updated, IReadOnlyList<SpecialAreaRef> Changed)
{
    public static SpecialAreaUpsertOutcome Empty { get; } = new(0, 0, Array.Empty<SpecialAreaRef>());
}

/// <summary>Esito della potatura: quanti legami tolti, e quali aree hanno smesso di essere visibili da quell'ACC.</summary>
public sealed record SpecialAreaPruneOutcome(int Removed, IReadOnlyList<SpecialAreaRef> Gone)
{
    public static SpecialAreaPruneOutcome Empty { get; } = new(0, Array.Empty<SpecialAreaRef>());
}
