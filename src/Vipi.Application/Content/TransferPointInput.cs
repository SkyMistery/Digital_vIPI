using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Dati per creare/aggiornare un punto di un flusso.
/// <para><c>record</c> e non <c>class</c>: con la faccetta trasferimento e la velocità i campi sono diventati
/// tanti, e chi ne cambia uno solo — l'editor che apre una variante, un test che varia un parametro — deve poter
/// scrivere <c>with</c> invece di ricopiare quindici assegnazioni e sbagliarne una in silenzio.</para></summary>
public sealed record TransferPointInput
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

    // Faccetta trasferimento (ACC→APP). Unspecified = il trasferimento coincide con l'ingresso: riga come prima.
    public TransferHandoffKind HandoffKind { get; init; } = TransferHandoffKind.Unspecified;
    public string? HandoffLabel { get; init; }
    public int? HandoffLevelValue { get; init; }
    public LevelUnit HandoffLevelUnit { get; init; } = LevelUnit.Fl;
    public LevelConstraint HandoffLevelConstraint { get; init; } = LevelConstraint.Exact;
    public TransferHandoffKind CommsHandoffKind { get; init; } = TransferHandoffKind.Unspecified;
    public string? CommsHandoffLabel { get; init; }

    // Velocità al trasferimento (nodi IAS). Unspecified = nessuna restrizione.
    public int? SpeedValue { get; init; }
    public SpeedConstraint SpeedConstraint { get; init; } = SpeedConstraint.Unspecified;

    /// <summary>La riga «negli altri casi» di un gruppo di varianti. Il gruppo in sé NON sta qui: lo assegna il
    /// repository (<c>AddVariantAsync</c>/<c>DetachVariantAsync</c>), perché è un'identità condivisa fra righe
    /// e non un campo che l'editor possa comporre da solo.</summary>
    public bool IsOtherwise { get; init; }
}
