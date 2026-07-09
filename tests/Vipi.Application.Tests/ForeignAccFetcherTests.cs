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

    private static SourceSubcenter Sub(string compose) =>
        new(compose, compose.Split('_')[0], null, null, null, null);

    private sealed class FakeDir : IAccDirectory
    {
        public Dictionary<string, List<SourceCenter>> Centers = new();
        public Dictionary<string, Func<List<SourceSubcenter>>> Subs = new();

        public Task<IReadOnlyList<SourceCenter>> GetCentersByCountryAsync(string countryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SourceCenter>>(Centers.TryGetValue(countryId, out var c) ? c : new());

        public Task<IReadOnlyList<SourceSubcenter>> GetSubcentersAsync(string accIcao, CancellationToken ct = default) =>
            Subs.TryGetValue(accIcao, out var f)
                ? Task.FromResult<IReadOnlyList<SourceSubcenter>>(f())
                : Task.FromResult<IReadOnlyList<SourceSubcenter>>(new List<SourceSubcenter>());

        public Task<IReadOnlyList<SourceCenter>> GetCentersAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SourceSpecialArea>> GetSpecialAreasAsync(string accIcao, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
