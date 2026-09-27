using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Stats;
using Vipi.Infrastructure.Ivao;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// 🔴 U-131 (revisione totale 3), la metà del poller: una fotografia che la sorgente ha GIÀ servito (stessa
/// <c>updatedAt</c>, o più vecchia) non si ripubblica e non si registra.
///
/// <para>Il whazzup fermo servito con 200 per mezz'ora sembrava fresco: la vista live mostrava controllori già
/// staccati, e le statistiche regalavano minuti di «occupato» alle sessioni coi piloti congelati nei loro settori
/// (ogni giro contava). Col dato di generazione in mano la regola è una sola: il tempo della sorgente deve
/// avanzare. Se non avanza, la cache tiene la fotografia di prima — che scade da sé dopo tre giri (T-034).</para>
/// </summary>
public class FotografiaFermaTests
{
    private static readonly DateTimeOffset T = new(2026, 8, 24, 13, 10, 5, TimeSpan.Zero);

    [Fact]
    public async Task Una_fotografia_gia_vista_non_si_ripubblica_e_non_si_registra()
    {
        var sorgente = new Sorgente(T, T, T.AddMinutes(-1), T.AddMinutes(1));
        var politica = new Politica();
        var cache = new OnlineAtcCache();
        var pubblicazioni = 0;
        cache.Changed += () => pubblicazioni++;
        var poller = Poller(sorgente, politica, cache);

        await poller.PollOnceAsync(CancellationToken.None);   // T: nuova
        var letturePrima = politica.Letture;
        await poller.PollOnceAsync(CancellationToken.None);   // T di nuovo: ferma
        await poller.PollOnceAsync(CancellationToken.None);   // T-1: più vecchia
        Assert.Equal(letturePrima, politica.Letture);          // niente sessioni, niente traffico
        Assert.Equal(1, pubblicazioni);
        Assert.Equal(T, cache.GetCurrent().AsOf);

        await poller.PollOnceAsync(CancellationToken.None);   // T+1: il tempo riparte
        Assert.Equal(2, pubblicazioni);
        Assert.Equal(T.AddMinutes(1), cache.GetCurrent().AsOf);
        Assert.True(politica.Letture > letturePrima);
    }

    /// <summary>Un salto indietro di ore è un orologio della sorgente riazzerato: si accetta, o la vista live resterebbe
    /// spenta finché la sorgente non torna alla data di prima.</summary>
    [Fact]
    public async Task Un_orologio_della_sorgente_riazzerato_non_spegne_la_vista()
    {
        var sorgente = new Sorgente(T, T.AddHours(-3));
        var cache = new OnlineAtcCache();
        var poller = Poller(sorgente, new Politica(), cache);

        await poller.PollOnceAsync(CancellationToken.None);
        await poller.PollOnceAsync(CancellationToken.None);

        Assert.Equal(T.AddHours(-3), cache.GetCurrent().AsOf);
    }

    private static AtcPollingHostedService Poller(Sorgente sorgente, Politica politica, OnlineAtcCache cache)
    {
        var sp = new ServiceCollection()
            .AddSingleton<IAtcActivitySource>(sorgente)
            .AddSingleton<IImportPolicyStore>(politica)
            .BuildServiceProvider();
        return new AtcPollingHostedService(sp.GetRequiredService<IServiceScopeFactory>(),
            new AtcTrafficRecorder(new CatalogoVuoto()), cache,
            Options.Create(new IvaoOptions()), new Ambiente(), NullLogger<AtcPollingHostedService>.Instance);
    }

    private sealed class Sorgente(params DateTimeOffset[] date) : IAtcActivitySource
    {
        private int _i;
        public Task<NetworkSnapshot> GetSnapshotAsync(CancellationToken ct = default) =>
            Task.FromResult(new NetworkSnapshot
            {
                Atc = Array.Empty<SourceAtcConnection>(),
                Pilots = Array.Empty<SourcePilotFix>(),
                AsOf = date[Math.Min(_i++, date.Length - 1)],
            });
    }

    /// <summary>Statistiche spente: il giro legge la politica (due volte, sessioni e traffico) e si ferma lì.</summary>
    private sealed class Politica : IImportPolicyStore
    {
        public int Letture;
        public Task<ImportPolicySnapshot> GetAsync(CancellationToken ct = default)
        {
            Letture++;
            return Task.FromResult(new ImportPolicySnapshot(true, true, true, AtcSessions: false));
        }
        public Task<ImportPolicyInfo> GetInfoAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveAsync(ImportPolicySnapshot policy, int updatedByUserId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class CatalogoVuoto : ISectorVolumeCatalog
    {
        public Task<IReadOnlyList<SectorVolumeRow>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SectorVolumeRow>>(Array.Empty<SectorVolumeRow>());
    }

    private sealed class Ambiente : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "prova";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
