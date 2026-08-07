using Vipi.Application.Aor;
using Vipi.Hosting;
using Xunit;

namespace Vipi.Hosting.Tests;

/// <summary>
/// Cache della topologia globale per il solo bridge Aurora. Senza, ogni richiesta dell'endpoint anonimo
/// rilegge tutti i settori attivi — su un database condiviso col sito che ci ospita è il costo che si nota
/// per primo, e lo pagherebbe qualcun altro.
/// </summary>
public class GlobalTopologyCacheTests
{
    private static Topology Topologia(string marcatore) => new()
    {
        Sectors = new List<string> { marcatore },
        Parent = new Dictionary<string, string>(),
        Rules = Array.Empty<UnificationRuleSpec>(),
    };

    [Fact]
    public async Task Dentro_la_finestra_si_costruisce_una_volta_sola()
    {
        var cache = new GlobalTopologyCache();
        var costruzioni = 0;

        Task<Topology> Costruisci(CancellationToken _)
        {
            costruzioni++;
            return Task.FromResult(Topologia($"giro {costruzioni}"));
        }

        for (var i = 0; i < 5; i++)
            await cache.GetAsync(Costruisci, TimeSpan.FromMinutes(1), default);

        Assert.Equal(1, costruzioni);
        Assert.Equal(4, cache.Riusi);
    }

    [Fact]
    public async Task Scaduta_la_finestra_si_ricostruisce()
    {
        var cache = new GlobalTopologyCache();
        var costruzioni = 0;

        Task<Topology> Costruisci(CancellationToken _)
        {
            costruzioni++;
            return Task.FromResult(Topologia($"giro {costruzioni}"));
        }

        var primo = await cache.GetAsync(Costruisci, TimeSpan.FromMilliseconds(30), default);
        await Task.Delay(80);
        var secondo = await cache.GetAsync(Costruisci, TimeSpan.FromMilliseconds(30), default);

        Assert.Equal(2, costruzioni);
        Assert.NotEqual(primo.Sectors.First(), secondo.Sectors.First());
    }

    /// <summary>TTL zero = nessuna cache: si può spegnere da configurazione senza toccare il codice.</summary>
    [Fact]
    public async Task Ttl_zero_significa_nessuna_cache()
    {
        var cache = new GlobalTopologyCache();
        var costruzioni = 0;

        for (var i = 0; i < 3; i++)
            await cache.GetAsync(_ => { costruzioni++; return Task.FromResult(Topologia("x")); }, TimeSpan.Zero, default);

        Assert.Equal(3, costruzioni);
        Assert.Equal(0, cache.Riusi);
    }

    /// <summary>
    /// Il caso che la cache esiste per evitare: N richieste concorrenti su cache fredda devono produrre UNA
    /// lettura del database, non N. Senza il ricontrollo dentro il lock passerebbero tutte.
    /// </summary>
    [Fact]
    public async Task Richieste_concorrenti_su_cache_fredda_costruiscono_una_volta_sola()
    {
        var cache = new GlobalTopologyCache();
        var costruzioni = 0;

        async Task<Topology> Costruisci(CancellationToken _)
        {
            Interlocked.Increment(ref costruzioni);
            await Task.Delay(50);       // la lettura del database dura
            return Topologia("unica");
        }

        await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => cache.GetAsync(Costruisci, TimeSpan.FromMinutes(1), default)));

        Assert.Equal(1, costruzioni);
    }

    /// <summary>
    /// Il wrapper mette in cache SOLO la topologia globale: quella per singola ACC la chiede la UI, e lì il
    /// dato fresco conta (un admin cambia la gerarchia e vuole vederla).
    /// </summary>
    [Fact]
    public async Task La_topologia_di_una_singola_acc_non_passa_dalla_cache()
    {
        var interno = new ProviderFinto();
        var provider = new CachedGlobalTopologyProvider(interno, new GlobalTopologyCache(), TimeSpan.FromMinutes(1));

        await provider.BuildGlobalAsync();
        await provider.BuildGlobalAsync();
        await provider.BuildByAccCodeAsync("LIRR");
        await provider.BuildByAccCodeAsync("LIRR");

        Assert.Equal(1, interno.ChiamateGlobali);
        Assert.Equal(2, interno.ChiamatePerAcc);
    }

    private sealed class ProviderFinto : Vipi.Application.Abstractions.ITopologyProvider
    {
        public int ChiamateGlobali;
        public int ChiamatePerAcc;

        public Task<Topology> BuildGlobalAsync(CancellationToken ct = default)
        {
            ChiamateGlobali++;
            return Task.FromResult(Topologia("globale"));
        }

        public Task<Topology?> BuildByAccCodeAsync(string accCode, CancellationToken ct = default)
        {
            ChiamatePerAcc++;
            return Task.FromResult<Topology?>(Topologia(accCode));
        }
    }
}
