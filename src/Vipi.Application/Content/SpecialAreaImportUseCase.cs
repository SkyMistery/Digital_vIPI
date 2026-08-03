using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <inheritdoc cref="ISpecialAreaImportUseCase"/>
public sealed class SpecialAreaImportUseCase : ISpecialAreaImportUseCase
{
    private readonly IAccAdminRepository _repo;
    private readonly IAccDirectory _directory;
    private readonly IImportPolicyStore _policy;

    public SpecialAreaImportUseCase(IAccAdminRepository repo, IAccDirectory directory, IImportPolicyStore policy)
    {
        _repo = repo;
        _directory = directory;
        _policy = policy;
    }

    /// <summary>
    /// Ogni quanto ri-scaricare la shape di un'area già in archivio. La geometria di un'area regolamentata cambia
    /// con l'AIP, non con l'import giornaliero: un giro al mese basta, e nel frattempo l'import costa una chiamata
    /// per pagina invece di una per area.
    /// </summary>
    private static readonly TimeSpan ShapeRefreshPeriod = TimeSpan.FromDays(30);

    public async Task<SpecialAreaImportResult> RunAsync(CancellationToken ct = default)
    {
        // Policy opt-out: categoria esclusa → si esce PRIMA della fetch e soprattutto prima del prune, così le aree
        // già in DB restano com'erano. Il gate sta qui e non nell'hosted service perché questo è il corpo condiviso
        // da auto e manual: nel service lo scavalcherebbe il bottone di /vsop/admin/accs.
        if (!(await _policy.GetAsync(ct)).SpecialAreas) return SpecialAreaImportResult.Empty;

        var accs = await _repo.ListAccsAsync(ct);
        var shapeCutoff = DateTime.UtcNow - ShapeRefreshPeriod;
        int created = 0, updated = 0, removed = 0;
        var failures = new List<SpecialAreaImportFailure>();
        foreach (var a in accs)
        {
            // Per-ACC: se la fetch fallisce non facciamo il prune di quell'ACC (evita cancellazioni su errori transitori).
            try
            {
                // Aree la cui shape è già in archivio e recente: alla sorgente si chiede solo l'elenco, non il dettaglio.
                var fresh = await _repo.ListAreasWithFreshShapeAsync(a.Code, shapeCutoff, ct);
                var areas = await _directory.GetSpecialAreasAsync(a.Code, fresh, ct);
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
