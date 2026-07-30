using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Dati per creare/aggiornare un punto di un flusso.</summary>
public sealed class TransferPointInput
{
    public required string Cop { get; init; }
    public int? LevelValue { get; init; }
    public required LevelUnit LevelUnit { get; init; }
    public required LevelConstraint LevelConstraint { get; init; }
    public string? LevelSpecial { get; init; }
    public LevelParity Parity { get; init; } = LevelParity.Any;
    public TransferVerticalState VerticalState { get; init; } = TransferVerticalState.Unspecified;
    public int? NextSectorId { get; init; }

    // Condizione operativa: tre dimensioni INDIPENDENTI e additive (tutte opzionali). Tutte vuote = riga sempre valida.
    public string? ConditionLabel { get; init; }        // pista/e in uso ("16R / 16L")
    public int? ConditionRefId { get; init; }           // soft-ref pista singola (opz.)
    public string? ConditionAreaLabel { get; init; }    // area attiva
    public string? ConditionCustomLabel { get; init; }  // condizione personalizzata (testo libero)
}
