using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Una clausola di un accordo in lettura: i punti a cui si applica, il livello, la faccetta trasferimento, la
/// condizione, la posizione nell'outline delle varianti.
/// <para>È l'ex <see cref="TransferPointRow"/> meno il ricevente — che è il lato opposto dell'accordo — e con i
/// punti in <b>elenco</b> (<see cref="CopList"/>) invece che uno solo. Il <b>verso</b> non è qui: lo dice la
/// sezione che la ospita.</para>
/// </summary>
public sealed record AgreementClauseRow : IOutlineRow
{
    public required int Id { get; init; }

    /// <summary>La sezione che la ospita: è lo scopo dentro cui l'ordine e l'outline hanno significato.</summary>
    public required int SectionId { get; init; }

    /// <summary>I punti d'ingresso, in elenco (vedi <see cref="CopList"/>).</summary>
    public required string Cops { get; init; }

    public int? LevelValue { get; init; }
    public required LevelUnit LevelUnit { get; init; }
    public required LevelConstraint LevelConstraint { get; init; }
    public string? LevelSpecial { get; init; }
    public LevelParity Parity { get; init; } = LevelParity.Any;
    public TransferVerticalState VerticalState { get; init; } = TransferVerticalState.Unspecified;

    // Condizione operativa: tre dimensioni indipendenti (pista/e · area · personalizzata).
    public string? ConditionLabel { get; init; }
    public int? ConditionRefId { get; init; }
    public string? ConditionAreaLabel { get; init; }
    public string? ConditionCustomLabel { get; init; }

    // Faccetta trasferimento. Unspecified = il trasferimento coincide con l'ingresso.
    public TransferHandoffKind HandoffKind { get; init; } = TransferHandoffKind.Unspecified;
    public string? HandoffLabel { get; init; }
    public int? HandoffLevelValue { get; init; }
    public LevelUnit HandoffLevelUnit { get; init; } = LevelUnit.Fl;
    public LevelConstraint HandoffLevelConstraint { get; init; } = LevelConstraint.Exact;
    public TransferHandoffKind CommsHandoffKind { get; init; } = TransferHandoffKind.Unspecified;
    public string? CommsHandoffLabel { get; init; }
    public int? SpeedValue { get; init; }
    public SpeedConstraint SpeedConstraint { get; init; } = SpeedConstraint.Unspecified;

    // Varianti a outline: l'ordine è la struttura.
    public int? VariantGroup { get; init; }
    public int VariantDepth { get; init; }
    public bool IsGroupWide { get; init; }

    public required int Order { get; init; }

    // ---- come si legge -------------------------------------------------------------------------------
    // Le stesse proprietà calcolate di TransferPointRow, e per la stessa ragione: tabella, frase e vista live
    // confrontano questi testi a occhio, quindi la formattazione vive in un posto solo (LevelFormatting).

    /// <summary>Il livello autorizzato già formattato («FL130- ↓ (pari)»).</summary>
    public string LevelText =>
        LevelFormatting.Format(LevelValue, LevelUnit, LevelConstraint, LevelSpecial, Parity, VerticalState);

    /// <summary>Livello al trasferimento già formattato («FL110»); vuoto se la clausola non lo porta.</summary>
    public string HandoffLevelText =>
        LevelFormatting.FormatHandoffLevel(HandoffLevelValue, HandoffLevelUnit, HandoffLevelConstraint);

    /// <summary>Velocità già formattata («≤250 kt»); vuota se assente.</summary>
    public string SpeedText => LevelFormatting.FormatSpeed(SpeedValue, SpeedConstraint);

    /// <summary>True se la clausola usa la faccetta trasferimento.</summary>
    public bool HasHandoff => HandoffKind != TransferHandoffKind.Unspecified;

    /// <summary>Etichetta condizione combinata per il display (pista · area · personalizzata); vuota se nessuna.</summary>
    public string? ConditionDisplay =>
        TransferConditionText.Display(ConditionLabel, ConditionAreaLabel, ConditionCustomLabel);
}
