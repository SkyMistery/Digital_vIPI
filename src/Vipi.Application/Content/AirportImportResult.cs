namespace Vipi.Application.Content;

/// <summary>
/// Esito dell'import anagrafica aeroporti (auto-assegnazione): quanti aeroporti assegnati alla loro ACC
/// + eventuali aeroporti il cui import settori è fallito (<see cref="Failures"/>), che il chiamante logga.
/// </summary>
/// <param name="Refreshed">Aeroporti già in archivio i cui campi anagrafici (presenza militare, IATA, quota,
/// variazione magnetica) sono cambiati in questo giro. Sta separato da <paramref name="Assigned"/> perché
/// risponde a un'altra domanda: quello dice quanti scali sono ENTRATI, questo quanti erano già dentro e sono
/// stati CORRETTI — e a regime il primo è zero mentre il secondo no.</param>
public sealed record AirportImportResult(int Assigned, IReadOnlyList<AirportImportFailure> Failures, int Refreshed = 0);
