namespace Vipi.Application.Content;

/// <summary>
/// Fallimento non fatale dell'import aree speciali per un singolo ACC (fetch/upsert): quell'ACC è saltato
/// (nessun prune, per non cancellare su errori transitori). Ritornato al chiamante Infra, che lo logga.
/// </summary>
public sealed record SpecialAreaImportFailure(string AccCode, Exception Error);
