using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>Caratterizzazione del fetch ACC esteri (doc refactor 05 §4.3): esclude i domestici, dedup, warning su fetch fallita.</summary>
public class ForeignAccFetcherTests
{
    [Fact]
    public async Task Fetch_excludes_domestic_dedups_and_warns_on_failure()
    {
        var dir = new FakeDir();
        dir.Centers["FR"] = new()
        {
            new SourceCenter("LIRR_CTR", "LIRR", "Roma", false),   // domestico → escluso
            new SourceCenter("LFFF_CTR", "LFFF", "France", false),
            new SourceCenter("LFFF_N_CTR", "LFFF", "France", false), // stesso ACC → dedup
            new SourceCenter("LFEE_CTR", "LFEE", "Reims", false),
        };
        dir.Subs["LFFF"] = () => new() { Sub("LFFF_CTR"), Sub("LFFF_N_CTR") };
        dir.Subs["LFEE"] = () => throw new InvalidOperationException("boom");

        var sut = new ForeignAccFetcher(dir);
        var (foreign, warnings) = await sut.FetchAsync(
            countryIds: new[] { "FR", "FR", " " }, domesticCodes: new HashSet<string> { "LIRR" });

        Assert.DoesNotContain(foreign, f => f.Code == "LIRR");   // domestico escluso
        var lfff = Assert.Single(foreign, f => f.Code == "LFFF");
        Assert.Equal(2, lfff.Subcenters.Count);
        Assert.DoesNotContain(foreign, f => f.Code == "LFEE");   // subcenter falliti → non nel risultato
        Assert.Contains(warnings, w => w.Contains("LFEE"));      // ma segnalato
    }

    [Fact]
    public async Task Fetch_reports_progress_up_to_total_and_counts_failures()
    {
        var dir = new FakeDir();
        dir.Centers["FR"] = new()
        {
            new SourceCenter("LFFF_CTR", "LFFF", "France", false),
            new SourceCenter("LFEE_CTR", "LFEE", "Reims", false),
            new SourceCenter("LFRR_CTR", "LFRR", "Brest", false),
        };
        dir.Subs["LFFF"] = () => new() { Sub("LFFF_CTR") };
        dir.Subs["LFRR"] = () => new() { Sub("LFRR_CTR") };
        dir.Subs["LFEE"] = () => throw new InvalidOperationException("boom");

        // ⚠️ Non `Progress<T>`: posta sul SynchronizationContext e in un test non ce n'è uno, quindi le
        // callback arrivano sul thread pool DOPO le asserzioni. Un IProgress sincrono le raccoglie tutte.
        var sync = new SyncProgress();

        var sut = new ForeignAccFetcher(dir);
        await sut.FetchAsync(new[] { "FR" }, new HashSet<string>(), CancellationToken.None, sync);

        Assert.NotEmpty(sync.Items);
        Assert.All(sync.Items, p => Assert.Equal(3, p.Total));
        // L'ultimo dice che sono finite tutte e tre, e che una è fallita: chi guarda deve sapere quante
        // non sono passate, non solo che il lavoro è finito.
        var last = sync.Items[^1];
        Assert.Equal(3, last.Done);
        Assert.Equal(1, last.Failed);
        // Il contatore non torna mai indietro né supera il totale.
        Assert.Equal(sync.Items.Select(i => i.Done).OrderBy(x => x), sync.Items.Select(i => i.Done));
        Assert.All(sync.Items, p => Assert.InRange(p.Done, 0, p.Total));
    }

    [Fact]
    public async Task Fetch_stops_when_cancelled()
    {
        var dir = new FakeDir();
        dir.Centers["FR"] = new()
        {
            new SourceCenter("LFFF_CTR", "LFFF", "France", false),
            new SourceCenter("LFEE_CTR", "LFEE", "Reims", false),
        };
        using var cts = new CancellationTokenSource();
        // Deterministico: la sorgente ONORA il token, come fa un vero client HTTP. Cancellare da dentro un
        // lambda e sperare che l'altro non sia già partito farebbe passare il test per caso — con sei
        // chiamate in volo, entrambe superano il guard d'ingresso prima che la cancellazione arrivi.
        dir.Cancellable = true;
        cts.Cancel();

        var sut = new ForeignAccFetcher(dir);
        // Chi interrompe deve vedere l'interruzione, non un risultato a metà spacciato per completo.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.FetchAsync(new[] { "FR" }, new HashSet<string>(), cts.Token));
    }

    private sealed class SyncProgress : IProgress<ForeignAccFetchProgress>
    {
        private readonly object _gate = new();
        public List<ForeignAccFetchProgress> Items { get; } = new();
        public void Report(ForeignAccFetchProgress value) { lock (_gate) Items.Add(value); }
    }

    private static SourceSubcenter Sub(string compose) =>
        new(compose, compose.Split('_')[0], null, null, null, null);

    private sealed class FakeDir : IAccDirectory
    {
        public Dictionary<string, List<SourceCenter>> Centers = new();
        public Dictionary<string, Func<List<SourceSubcenter>>> Subs = new();

        public Task<IReadOnlyList<SourceCenter>> GetCentersByCountryAsync(string countryId, CancellationToken ct = default)
        {
            if (Cancellable) ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<SourceCenter>>(Centers.TryGetValue(countryId, out var c) ? c : new());
        }

        /// <summary>Quando true la sorgente onora il token, come un vero client HTTP.</summary>
        public bool Cancellable;

        public Task<IReadOnlyList<SourceSubcenter>> GetSubcentersAsync(string accIcao, CancellationToken ct = default)
        {
            if (Cancellable) ct.ThrowIfCancellationRequested();
            return Subs.TryGetValue(accIcao, out var f)
                ? Task.FromResult<IReadOnlyList<SourceSubcenter>>(f())
                : Task.FromResult<IReadOnlyList<SourceSubcenter>>(new List<SourceSubcenter>());
        }

        public Task<IReadOnlyList<SourceCenter>> GetCentersAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SourceSpecialArea>> GetSpecialAreasAsync(string accIcao, IReadOnlySet<string> skipDetailIds, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
