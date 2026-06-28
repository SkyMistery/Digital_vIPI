using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Cache di processo (singleton) dell'anagrafica aeroporti IVAO: dati di riferimento che cambiano di rado,
/// scaricati in più pagine. Evita di ri-paginare l'API a ogni apertura dell'editor struttura. Thread-safe.
/// </summary>
public sealed class IvaoAirportCache
{
    public readonly SemaphoreSlim Gate = new(1, 1);
    public IReadOnlyList<SourceAirport>? Items;
    public DateTimeOffset ExpiresAt = DateTimeOffset.MinValue;

    public bool IsFresh => Items is not null && DateTimeOffset.UtcNow < ExpiresAt;
}
