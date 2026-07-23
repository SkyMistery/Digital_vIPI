using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Riga CoP di un flusso (lettura). <see cref="LevelText"/> è il livello già formattato.</summary>
public sealed class TransferPointRow
{
    public required int Id { get; init; }
    public required string Cop { get; init; }
    public int? LevelValue { get; init; }
    public required LevelUnit LevelUnit { get; init; }
    public required LevelConstraint LevelConstraint { get; init; }
    public string? LevelSpecial { get; init; }
    public LevelParity Parity { get; init; } = LevelParity.Any;
    public required string LevelText { get; init; }
    public int? NextSectorId { get; init; }
    public string? NextSectorCallsign { get; init; }

    // Condizione operativa: tre dimensioni indipendenti (pista/e · area · personalizzata). Tutte null = sempre valida.
    public string? ConditionLabel { get; init; }        // pista/e in uso
    public int? ConditionRefId { get; init; }           // soft-ref pista singola
    public string? ConditionAreaLabel { get; init; }    // area attiva
    public string? ConditionCustomLabel { get; init; }  // condizione personalizzata

    /// <summary>Etichetta condizione combinata per il display (pill/chip): pista · area · personalizzata. Vuota se nessuna.</summary>
    public string? ConditionDisplay => TransferConditionText.Display(ConditionLabel, ConditionAreaLabel, ConditionCustomLabel);

    public required int Order { get; init; }
}
