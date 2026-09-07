namespace Vipi.Application.Content;

/// <summary>
/// Esito dell'import manuale «da sorgente» completo: risultato ACC + subcenter (<see cref="Acc"/>) e gli
/// eventuali ACC saltati durante l'import aree speciali (<see cref="SpecialAreaFailures"/>). I fallimenti
/// sono ritornati (non loggati qui: Application non logga) perché la UI li logghi — direttiva logging,
/// doc refactor 02/03 e REFACTOR-PROCESS invariante #7.
/// </summary>
// ⚠️ Pubblico perché compare nella FIRMA di un tipo pubblico: chi lo restringe scopre che il
// compilatore lo dice da sé (CS0050/CS0051/CS0053). È superficie del modulo quanto il tipo che lo
// espone (ADR-0005 D6, revisione del 6 settembre 2026, R-009).
public sealed record AccImportOutcome(
    AccImportResult Acc, IReadOnlyList<SpecialAreaImportFailure> SpecialAreaFailures);
