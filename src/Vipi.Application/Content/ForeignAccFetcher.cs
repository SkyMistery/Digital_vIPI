using System.Collections.Concurrent;
using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <summary>
/// Scarica dalla sorgente (porta <see cref="IAccDirectory"/>) gli ACC esteri dei paesi vicini e i loro subcenter,
/// escludendo gli ACC domestici. Parallelismo throttled (il costo dominante sono le GET di dettaglio). Isolato
/// da <see cref="NeighbourImportService"/> per testabilità (doc refactor 05 §4.3). Ritorna i dati grezzi + i
/// warning delle fetch fallite (il chiamante li propaga alla UI).
/// </summary>
public sealed class ForeignAccFetcher
{
    private readonly IAccDirectory _directory;

    public ForeignAccFetcher(IAccDirectory directory) => _directory = directory;

    public async Task<(IReadOnlyList<ForeignAccData> Foreign, IReadOnlyList<string> Warnings)> FetchAsync(
        IReadOnlyList<string> countryIds, ISet<string> domesticCodes, CancellationToken ct = default)
    {
        var warnBag = new ConcurrentBag<string>();

        // Elenco ACC esteri: un GET /centers per paese (in parallelo). Cheap.
        var countryList = countryIds.Select(c => c.Trim())
            .Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var foreignAccList = new ConcurrentBag<(string Country, string Code, string Name)>();
        await RunBoundedAsync(countryList, 6, async country =>
        {
            IReadOnlyList<SourceCenter> centers;
            try { centers = await _directory.GetCentersByCountryAsync(country, ct); }
            catch (Exception ex) { warnBag.Add($"{country}: import ACC fallito ({ex.Message})."); return; }

            foreach (var fCode in centers.Select(c => c.CenterId)
                         .Where(c => !string.IsNullOrWhiteSpace(c) && !domesticCodes.Contains(c))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var fName = centers.FirstOrDefault(c => string.Equals(c.CenterId, fCode, StringComparison.OrdinalIgnoreCase))?.Name ?? fCode;
                foreignAccList.Add((country, fCode.ToUpperInvariant(), fName));
            }
        });

        // Per ogni ACC estero (in parallelo, throttled): subcenter.
        var result = new ConcurrentBag<ForeignAccData>();
        await RunBoundedAsync(foreignAccList.ToList(), 6, async fa =>
        {
            IReadOnlyList<SourceSubcenter> subs;
            try { subs = await _directory.GetSubcentersAsync(fa.Code, ct); }
            catch (Exception ex) { warnBag.Add($"{fa.Code}: subcenter non letti ({ex.Message})."); return; }
            result.Add(new ForeignAccData(fa.Code, fa.Name, fa.Country, subs.ToList()));
        });

        return (result.ToList(), warnBag.ToList());
    }

    /// <summary>Esegue <paramref name="body"/> su tutti gli item con al massimo <paramref name="dop"/> in volo.</summary>
    private static async Task RunBoundedAsync<T>(IReadOnlyList<T> items, int dop, Func<T, Task> body)
    {
        using var gate = new SemaphoreSlim(dop);
        var tasks = items.Select(async item =>
        {
            await gate.WaitAsync();
            try { await body(item); }
            finally { gate.Release(); }
        });
        await Task.WhenAll(tasks);
    }
}
