namespace Vipi.Application.Content;

/// <summary>
/// Esito dell'import anagrafica aeroporti (auto-assegnazione): quanti aeroporti assegnati alla loro ACC
/// + eventuali aeroporti il cui import settori è fallito (<see cref="Failures"/>), che il chiamante logga.
/// </summary>
public sealed record AirportImportResult(int Assigned, IReadOnlyList<AirportImportFailure> Failures);
