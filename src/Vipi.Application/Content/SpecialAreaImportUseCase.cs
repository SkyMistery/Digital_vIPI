using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <inheritdoc cref="ISpecialAreaImportUseCase"/>
public sealed class SpecialAreaImportUseCase : ISpecialAreaImportUseCase
{
    private readonly IAccAdminRepository _repo;
    private readonly IAccDirectory _directory;

    public SpecialAreaImportUseCase(IAccAdminRepository repo, IAccDirectory directory)
    {
        _repo = repo;
        _directory = directory;
    }

    public async Task<SpecialAreaImportResult> RunAsync(CancellationToken ct = default)
    {
        var accs = await _repo.ListAccsAsync(ct);
        int created = 0, updated = 0, removed = 0;
        var failures = new List<SpecialAreaImportFailure>();
        foreach (var a in accs)
        {
            // Per-ACC: se la fetch fallisce non facciamo il prune di quell'ACC (evita cancellazioni su errori transitori).
            try
            {
                var areas = await _directory.GetSpecialAreasAsync(a.Code, ct);
                var (c, u) = await _repo.ImportSpecialAreasAsync(areas, ct);
                var r = await _repo.PruneSpecialAreasNotInAsync(a.Code, areas.Select(x => x.IvaoId).ToList(), ct);
                created += c; updated += u; removed += r;
            }
            catch (InvalidOperationException) { throw; }   // credenziali assenti: gestito a monte dal chiamante
            catch (Exception ex)
            {
                failures.Add(new SpecialAreaImportFailure(a.Code, ex));
            }
        }
        return new SpecialAreaImportResult(created, updated, removed, failures);
    }
}
