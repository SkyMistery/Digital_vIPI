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

    /// <param name="progress">Facoltativo: riceve l'avanzamento delle GET di dettaglio, che sono la parte
    /// lunga (una per ACC estero). Senza, il chiamante vede solo uno spinner — indistinguibile da
    /// un'applicazione bloccata.</param>
    public async Task<(IReadOnlyList<ForeignAccData> Foreign, IReadOnlyList<string> Warnings)> FetchAsync(
        IReadOnlyList<string> countryIds, ISet<string> domesticCodes, CancellationToken ct = default,
        IProgress<ForeignAccFetchProgress>? progress = null)
    {
        var warnBag = new ConcurrentBag<string>();

        // Elenco ACC esteri: un GET /centers per paese (in parallelo). Cheap.
        var countryList = countryIds.Select(c => c.Trim())
            .Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var foreignAccList = new ConcurrentBag<(string Country, string Code, string Name)>();
        await RunBoundedAsync(countryList, 6, async country =>
        {
            IReadOnlyList<SourceCenter> centers;
            // ⚠️ L'interruzione NON è una fetch fallita: senza questo ramo il `catch (Exception)` la
            // trasformava in un warning («import ACC fallito (A task was canceled)») e l'import proseguiva
            // come se niente fosse — chi ha premuto Interrompi vedeva un elenco di guai al posto dell'esito.
            try { centers = await _directory.GetCentersByCountryAsync(country, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { warnBag.Add($"{country}: import ACC fallito ({ex.Message})."); return; }

            foreach (var fCode in centers.Select(c => c.CenterId)
                         .Where(c => !string.IsNullOrWhiteSpace(c) && !domesticCodes.Contains(c))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var fName = centers.FirstOrDefault(c => string.Equals(c.CenterId, fCode, StringComparison.OrdinalIgnoreCase))?.Name ?? fCode;
                foreignAccList.Add((country, fCode.ToUpperInvariant(), fName));
            }
        });

        // Per ogni ACC estero (in parallelo, throttled): subcenter. È la parte lunga — decine di GET —
        // e l'unica di cui abbia senso raccontare l'avanzamento.
        var result = new ConcurrentBag<ForeignAccData>();
        var all = foreignAccList.ToList();
        var done = 0;
        var failed = 0;
        progress?.Report(new ForeignAccFetchProgress(0, all.Count, 0));
        await RunBoundedAsync(all, 6, async fa =>
        {
            ct.ThrowIfCancellationRequested();
            IReadOnlyList<SourceSubcenter> subs;
            try { subs = await _directory.GetSubcentersAsync(fa.Code, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                warnBag.Add($"{fa.Code}: subcenter non letti ({ex.Message}).");
                progress?.Report(new ForeignAccFetchProgress(Interlocked.Increment(ref done), all.Count, Interlocked.Increment(ref failed)));
                return;
            }
            result.Add(new ForeignAccData(fa.Code, fa.Name, fa.Country, subs.ToList()));
            progress?.Report(new ForeignAccFetchProgress(Interlocked.Increment(ref done), all.Count, Volatile.Read(ref failed)));
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

/// <summary>Avanzamento delle GET di dettaglio del fetch ACC esteri: quante fatte su quante, e quante fallite.
/// È solo un messaggio di trasporto verso la UI, non un modello di dominio.</summary>
/// <param name="Done">Quante chiamate sono tornate (riuscite o fallite).</param>
/// <param name="Total">Quante ne sono in programma.</param>
/// <param name="Failed">Quante di quelle fatte sono fallite: chi si accorge che la sorgente risponde male
/// non deve aspettare le altre.</param>
public sealed record ForeignAccFetchProgress(int Done, int Total, int Failed);
