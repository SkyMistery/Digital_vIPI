using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Cache di processo (singleton) dell'anagrafica aeroporti IVAO: dati di riferimento che cambiano di rado,
/// scaricati in più pagine. Evita di ri-paginare l'API a ogni apertura dell'editor struttura. Thread-safe.
/// <para>
/// Catalogo e scadenza vivono in un unico riferimento immutabile pubblicato con <see cref="Volatile"/>: tenerli in
/// due campi separati e leggerli fuori dal lock significherebbe leggere una coppia non atomica (una
/// <see cref="DateTimeOffset"/> non si scrive atomicamente), con la possibilità di osservare una scadenza
/// strappata o disallineata dagli elementi. Stesso schema di <c>OnlineAtcCache</c>.
/// </para>
/// </summary>
public sealed class IvaoAirportCache
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Catalog? _catalog;

    private sealed record Catalog(IReadOnlyList<SourceAirport> Items, DateTimeOffset ExpiresAt)
    {
        public bool IsFresh => DateTimeOffset.UtcNow < ExpiresAt;
    }

    /// <summary>
    /// Catalogo fresco dalla cache, altrimenti caricato con <paramref name="load"/> una volta sola: i chiamanti
    /// concorrenti attendono lo stesso caricamento invece di ri-paginare l'API in parallelo.
    /// </summary>
    public async Task<IReadOnlyList<SourceAirport>> GetOrLoadAsync(
        Func<CancellationToken, Task<IReadOnlyList<SourceAirport>>> load, TimeSpan ttl, CancellationToken ct = default)
    {
        if (Volatile.Read(ref _catalog) is { IsFresh: true } hit) return hit.Items;

        await _gate.WaitAsync(ct);
        try
        {
            if (Volatile.Read(ref _catalog) is { IsFresh: true } cached) return cached.Items;   // caricato durante l'attesa
            var items = await load(ct);
            Volatile.Write(ref _catalog, new Catalog(items, DateTimeOffset.UtcNow.Add(ttl)));
            return items;
        }
        finally { _gate.Release(); }
    }

    /// <summary>Cache dei lookup per singolo ICAO (anche esteri, fuori dal paese configurato): evita di
    /// ri-chiamare l'API a ogni pressione del tasto «cerca». Non entra nel catalogo IT.</summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SourceAirport> _single = new(StringComparer.OrdinalIgnoreCase);

    public bool TryGetSingle(string icao, out SourceAirport airport) => _single.TryGetValue(icao, out airport!);
    public void PutSingle(string icao, SourceAirport airport) => _single[icao] = airport;
}
