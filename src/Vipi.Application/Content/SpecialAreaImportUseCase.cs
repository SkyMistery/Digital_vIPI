using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <inheritdoc cref="ISpecialAreaImportUseCase"/>
public sealed class SpecialAreaImportUseCase : ISpecialAreaImportUseCase
{
    private readonly IAccAdminRepository _repo;
    private readonly IAccDirectory _directory;
    private readonly IImportPolicyStore _policy;
    private readonly IDocumentImpactService? _impacts;

    /// <param name="impacts">Opzionale, come nella proiezione: un import senza casella importa e basta.</param>
    public SpecialAreaImportUseCase(IAccAdminRepository repo, IAccDirectory directory, IImportPolicyStore policy,
        IDocumentImpactService? impacts = null)
    {
        _repo = repo;
        _directory = directory;
        _policy = policy;
        _impacts = impacts;
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
        // da auto e manual: nel service lo scavalcherebbe il bottone di /services/vsop/admin/accs.
        if (!(await _policy.GetAsync(ct)).SpecialAreas) return SpecialAreaImportResult.Empty;

        // Solo gli ACC abilitati: gli esteri nascono spenti e li accende l'admin col primo import manuale, altrimenti
        // il giro delle 24h si porterebbe dietro centinaia di aree che nessun documento italiano userà mai.
        var accs = (await _repo.ListAccsAsync(ct)).Where(a => a.SpecialAreasEnabled).ToList();
        var shapeCutoff = DateTime.UtcNow - ShapeRefreshPeriod;
        int created = 0, updated = 0, removed = 0;
        var failures = new List<SpecialAreaImportFailure>();
        foreach (var a in accs)
        {
            // Per-ACC: se la fetch fallisce non facciamo il prune di quell'ACC (evita cancellazioni su errori transitori).
            try
            {
                var (c, u, r) = await ImportOneAsync(a.Code, shapeCutoff, ct);
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

    public async Task<SpecialAreaImportResult> RunForAccAsync(string accCode, CancellationToken ct = default)
    {
        // Niente gate sul flag dell'ACC: è proprio l'atto con cui l'admin lo accende (il primo import di un estero).
        // La policy globale invece vale: se le aree sono congelate non si scarica niente, nemmeno a mano.
        if (!(await _policy.GetAsync(ct)).SpecialAreas) return SpecialAreaImportResult.Empty;

        accCode = (accCode ?? "").Trim().ToUpperInvariant();
        if (accCode.Length == 0) return SpecialAreaImportResult.Empty;

        try
        {
            var (c, u, r) = await ImportOneAsync(accCode, DateTime.UtcNow - ShapeRefreshPeriod, ct);
            return new SpecialAreaImportResult(c, u, r, Array.Empty<SpecialAreaImportFailure>());
        }
        catch (InvalidOperationException) { throw; }       // credenziali assenti: la UI lo dice all'utente
        catch (Exception ex)
        {
            return new SpecialAreaImportResult(0, 0, 0, new[] { new SpecialAreaImportFailure(accCode, ex) });
        }
    }

    // Fetch + upsert + prune di UN ACC. Corpo unico: il giro completo e l'import di un singolo ente devono
    // lasciare lo stesso stato per quell'ACC.
    private async Task<(int Created, int Updated, int Removed)> ImportOneAsync(
        string accCode, DateTime shapeCutoff, CancellationToken ct)
    {
        // Aree la cui shape è già in archivio e recente: alla sorgente si chiede solo l'elenco, non il dettaglio.
        var fresh = await _repo.ListAreasWithFreshShapeAsync(accCode, shapeCutoff, ct);
        var areas = await _directory.GetSpecialAreasAsync(accCode, fresh, ct);
        var upsert = await _repo.ImportSpecialAreasAsync(areas, ct);
        var prune = await _repo.PruneSpecialAreasNotInAsync(accCode, areas.Select(x => x.IvaoId).ToList(), ct);

        // La casella: solo le aree CAMBIATE (non le «aggiornate») e quelle sparite. ⚠️ Un'area sparisce dalla
        // vista di un ACC anche quando resta in archivio per un altro ente: per i documenti di QUESTO ACC il
        // fatto è lo stesso — non la vedono più — e va detto lo stesso.
        if (_impacts is not null)
        {
            foreach (var a in upsert.Changed)
                await _impacts.RaiseForAreaAsync(ImpactKind.AreaChanged, a.IvaoId, a.Name, ct);
            foreach (var a in prune.Gone)
                await _impacts.RaiseForAreaAsync(ImpactKind.AreaGone, a.IvaoId, a.Name, ct);
        }

        return (upsert.Created, upsert.Updated, prune.Removed);
    }
}
