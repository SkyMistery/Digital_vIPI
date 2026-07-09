namespace Vipi.Application.Content;

/// <summary>
/// Esito dell'import manuale «da sorgente» completo: risultato ACC + subcenter (<see cref="Acc"/>) e gli
/// eventuali ACC saltati durante l'import aree speciali (<see cref="SpecialAreaFailures"/>). I fallimenti
/// sono ritornati (non loggati qui: Application non logga) perché la UI li logghi — direttiva logging,
/// doc refactor 02/03 e REFACTOR-PROCESS invariante #7.
/// </summary>
public sealed record AccImportOutcome(
    AccImportResult Acc, IReadOnlyList<SpecialAreaImportFailure> SpecialAreaFailures);
