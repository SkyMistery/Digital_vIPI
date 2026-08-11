using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Riga CoP di un flusso (lettura). <see cref="LevelText"/> è il livello già formattato.
/// <para><c>record</c> come <see cref="TransferPointInput"/> e per la stessa ragione: con la faccetta
/// trasferimento i campi sono venticinque, e chi ne varia uno — un test, una proiezione — deve poter scrivere
/// <c>with</c> invece di ricopiarli tutti e sbagliarne uno in silenzio.</para></summary>
public sealed record TransferPointRow
{
    public required int Id { get; init; }
    public required string Cop { get; init; }
    public int? LevelValue { get; init; }
    public required LevelUnit LevelUnit { get; init; }
    public required LevelConstraint LevelConstraint { get; init; }
    public string? LevelSpecial { get; init; }
    public LevelParity Parity { get; init; } = LevelParity.Any;
    public TransferVerticalState VerticalState { get; init; } = TransferVerticalState.Unspecified;
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

    // Faccetta trasferimento. HandoffKind = Unspecified ⇒ trasferimento e ingresso coincidono (riga «come prima»).
    // La PAROLA del luogo («al confine dell'AoR») non sta qui: è lingua, e la lingua vive nel template della frase,
    // che esiste in italiano e in inglese (vLOA). Qui viaggiano il tipo e l'etichetta grezza.
    public TransferHandoffKind HandoffKind { get; init; } = TransferHandoffKind.Unspecified;
    public string? HandoffLabel { get; init; }
    public int? HandoffLevelValue { get; init; }
    public LevelUnit HandoffLevelUnit { get; init; } = LevelUnit.Fl;
    public LevelConstraint HandoffLevelConstraint { get; init; } = LevelConstraint.Exact;
    public TransferHandoffKind CommsHandoffKind { get; init; } = TransferHandoffKind.Unspecified;
    public string? CommsHandoffLabel { get; init; }
    public int? SpeedValue { get; init; }
    public SpeedConstraint SpeedConstraint { get; init; } = SpeedConstraint.Unspecified;

    /// <summary>Livello al trasferimento già formattato («FL110»); vuoto se la riga non lo porta.</summary>
    public string HandoffLevelText => LevelFormatting.FormatHandoffLevel(HandoffLevelValue, HandoffLevelUnit, HandoffLevelConstraint);
    /// <summary>Velocità già formattata («≤250 kt»); vuota se la riga non la porta.</summary>
    public string SpeedText => LevelFormatting.FormatSpeed(SpeedValue, SpeedConstraint);
    /// <summary>True se la riga usa la faccetta trasferimento: è il criterio con cui le viste decidono se mostrare
    /// le colonne relative (per presenza di dati, mai per tipo di ente).</summary>
    public bool HasHandoff => HandoffKind != TransferHandoffKind.Unspecified;

    // Varianti: righe alternative dello stesso accordo. Il gruppo è assegnato dal repository.
    public int? VariantGroup { get; init; }
    public bool IsOtherwise { get; init; }

    public required int Order { get; init; }
}
