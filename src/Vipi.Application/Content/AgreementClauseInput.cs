using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Una clausola in scrittura. È l'ex <c>TransferPointInput</c> (rimosso col modello vecchio) con due
/// differenze: i punti sono un
/// <b>elenco</b> (<see cref="CopList"/>) e il ricevente <b>non c'è</b> — è il lato opposto dell'accordo, e
/// ripeterlo su ogni clausola era il modo di lasciarle contraddire fra loro.
/// <para>Come prima, <b>gruppo, profondità e ordine non stanno qui</b>: li decide il repository quando la
/// clausola nasce o si sposta. Un editor che potesse scriverli a mano potrebbe creare una clausola orfana o un
/// salto di profondità che nessuna validazione a valle saprebbe attribuire a un'intenzione. La direzione invece
/// sì: quella è una scelta editoriale, non una posizione.</para>
/// </summary>
public sealed record AgreementClauseInput
{
    /// <summary>I punti d'ingresso, in elenco.</summary>
    public required string Cops { get; init; }

    public int? LevelValue { get; init; }
    public required LevelUnit LevelUnit { get; init; }
    public required LevelConstraint LevelConstraint { get; init; }
    public string? LevelSpecial { get; init; }
    public LevelParity Parity { get; init; } = LevelParity.Any;
    public TransferVerticalState VerticalState { get; init; } = TransferVerticalState.Unspecified;

    // Condizione operativa: tre dimensioni indipendenti, tutte opzionali.
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

    /// <summary>La clausola scavalca le alternative del gruppo. Ha senso solo dentro un gruppo: fuori non ci
    /// sono alternative da scavalcare, e il repository lo ignora.</summary>
    public bool IsGroupWide { get; init; }
}
