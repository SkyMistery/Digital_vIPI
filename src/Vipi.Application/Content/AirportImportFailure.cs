namespace Vipi.Application.Content;

/// <summary>
/// Fallimento non fatale dell'import settori per un singolo aeroporto durante l'auto-assegnazione
/// (aeroporto senza settori nella sorgente o sorgente non disponibile): quell'aeroporto è saltato.
/// Ritornato al chiamante, che lo logga (direttiva logging).
/// </summary>
public sealed record AirportImportFailure(string Icao, Exception Error);
