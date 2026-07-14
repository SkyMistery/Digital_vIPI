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

    /// <summary>Cache dei lookup per singolo ICAO (anche esteri, fuori dal paese configurato): evita di
    /// ri-chiamare l'API a ogni pressione del tasto «cerca». Non entra in <see cref="Items"/> (catalogo IT).</summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SourceAirport> _single = new(StringComparer.OrdinalIgnoreCase);

    public bool TryGetSingle(string icao, out SourceAirport airport) => _single.TryGetValue(icao, out airport!);
    public void PutSingle(string icao, SourceAirport airport) => _single[icao] = airport;
}
