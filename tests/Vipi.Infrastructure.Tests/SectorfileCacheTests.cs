using Vipi.Application.Abstractions;
using Vipi.Infrastructure.Sectorfile;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// <see cref="SectorfileCache"/> è il singleton che sostituisce le cache in campo d'istanza degli adapter Aurora
/// (transient per <c>AddHttpClient&lt;,&gt;</c>, quindi con cache e lock per-risoluzione: file ri-scaricato a ogni
/// click e nessuna mutua esclusione reale). Qui si verifica che il caricamento avvenga una volta sola anche sotto
/// chiamate concorrenti, e che un caricamento fallito non venga memorizzato.
/// </summary>
public class SectorfileCacheTests
{
    [Fact]
    public async Task Navaid_Caricati_Una_Sola_Volta()
    {
        var cache = new SectorfileCache();
        var loads = 0;

        Task<NavaidCatalog> Load(CancellationToken _)
        {
            Interlocked.Increment(ref loads);
            return Task.FromResult(Catalog("ELB", "TAQ"));
        }

        var a = await cache.GetNavaidsAsync(Load);
        var b = await cache.GetNavaidsAsync(Load);
        var c = await cache.GetNavaidsAsync(Load);

        Assert.Equal(1, loads);
        Assert.Same(a, b);
        Assert.Same(b, c);
    }

    [Fact]
    public async Task Chiamate_Concorrenti_Condividono_Un_Solo_Caricamento()
    {
        var cache = new SectorfileCache();
        var loads = 0;
        using var release = new SemaphoreSlim(0);

        async Task<NavaidCatalog> SlowLoad(CancellationToken ct)
        {
            Interlocked.Increment(ref loads);
            await release.WaitAsync(ct);   // tiene aperto il caricamento finché tutti i chiamanti sono in coda
            return Catalog("ELB");
        }

        var callers = Enumerable.Range(0, 16).Select(_ => cache.GetNavaidsAsync(SlowLoad)).ToArray();
        release.Release();
        var results = await Task.WhenAll(callers);

        Assert.Equal(1, loads);
        Assert.All(results, r => Assert.Same(results[0], r));
    }

    [Fact]
    public async Task Caricamento_Fallito_Non_Viene_Memorizzato()
    {
        var cache = new SectorfileCache();
        var attempts = 0;

        Task<IReadOnlyDictionary<string, string>> Flaky(CancellationToken _)
        {
            attempts++;
            if (attempts == 1) throw new HttpRequestException("GitHub non raggiungibile");
            return Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string> { ["LIRF_TWR"] = "[]" });
        }

        await Assert.ThrowsAsync<HttpRequestException>(() => cache.GetTowerPolygonsAsync(Flaky));

        // Il secondo tentativo deve poter riprovare: un errore transitorio non deve avvelenare la cache per
        // tutta la vita del processo (a differenza di un Lazy<Task> che memorizzerebbe il task in errore).
        var ok = await cache.GetTowerPolygonsAsync(Flaky);

        Assert.Equal(2, attempts);
        Assert.True(ok.ContainsKey("LIRF_TWR"));
    }

    [Fact]
    public async Task Le_Due_Fette_Sono_Indipendenti()
    {
        var cache = new SectorfileCache();

        await cache.GetNavaidsAsync(_ => Task.FromResult(Catalog("ELB")));
        var twrLoads = 0;
        await cache.GetTowerPolygonsAsync(_ =>
        {
            twrLoads++;
            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        });

        Assert.Equal(1, twrLoads);   // il caricamento navaid non deve "riempire" lo slot dei poligoni
    }

    [Fact]
    public async Task Invalidate_Fa_Riscaricare_Entrambe_Le_Fette()
    {
        var cache = new SectorfileCache();
        int navLoads = 0, twrLoads = 0;

        Task<NavaidCatalog> Nav(CancellationToken _) { navLoads++; return Task.FromResult(Catalog("ELB")); }
        Task<IReadOnlyDictionary<string, string>> Twr(CancellationToken _)
        {
            twrLoads++;
            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        }

        await cache.GetNavaidsAsync(Nav);
        await cache.GetTowerPolygonsAsync(Twr);
        cache.Invalidate();
        await cache.GetNavaidsAsync(Nav);
        await cache.GetTowerPolygonsAsync(Twr);

        // Due caricamenti per fetta: senza questo, un fix pubblicato oggi su GitHub resterebbe invisibile ai
        // suggerimenti dell'editor fino al riavvio dell'applicazione.
        Assert.Equal(2, navLoads);
        Assert.Equal(2, twrLoads);
    }

    /// <summary>
    /// 🔴 U-039 (revisione totale 3): un caricamento MRVA fallito non si ricordava, e ogni richiesta riprovava — in
    /// fila su un semaforo solo per tutte le carte. Con GitHub giù tre editor che aprivano tre vIPI ACC aspettavano
    /// 15, 30 e 45 secondi. Il guasto ora si ricorda per poco: chi arriva subito dopo lo sa senza aspettare.
    /// </summary>
    [Fact]
    public async Task Mva_Un_Guasto_Si_Ricorda_Per_Poco()
    {
        var orologio = new Orologio();
        var cache = new SectorfileCache(orologio);
        var loads = 0;

        Task<MvaChart> Rotto(CancellationToken _)
        {
            loads++;
            throw new HttpRequestException("timeout");
        }

        await Assert.ThrowsAsync<HttpRequestException>(() => cache.GetMvaChartAsync("ENRMVA/lirr.mva", Rotto));
        await Assert.ThrowsAnyAsync<Exception>(() => cache.GetMvaChartAsync("ENRMVA/lirr.mva", Rotto));
        Assert.Equal(1, loads);   // il secondo non ha riprovato

        orologio.Adesso += SectorfileCache.DurataDelGuasto;
        await Assert.ThrowsAsync<HttpRequestException>(() => cache.GetMvaChartAsync("ENRMVA/lirr.mva", Rotto));
        Assert.Equal(2, loads);   // passato il tempo, si riprova
    }

    /// <summary>🔴 U-039: una carta che non arriva non tiene in fila le altre.</summary>
    [Fact]
    public async Task Mva_Una_Carta_Lenta_Non_Ferma_Le_Altre()
    {
        var cache = new SectorfileCache();
        using var sblocca = new SemaphoreSlim(0);

        async Task<MvaChart> Lenta(CancellationToken ct) { await sblocca.WaitAsync(ct); return MvaChart.Empty; }

        var lenta = cache.GetMvaChartAsync("ENRMVA/lirr.mva", Lenta);
        var altra = cache.GetMvaChartAsync("lirn.mva", _ => Task.FromResult(MvaChart.Empty));

        Assert.Same(altra, await Task.WhenAny(altra, Task.Delay(TimeSpan.FromSeconds(5))));
        sblocca.Release();
        await lenta;
    }

    private sealed class Orologio : TimeProvider
    {
        public DateTimeOffset Adesso { get; set; } = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Adesso;
    }

    private static NavaidCatalog Catalog(params string[] names) =>
        new(names.Select(n => new NavaidName(n, NavaidKind.Fix)));
}
