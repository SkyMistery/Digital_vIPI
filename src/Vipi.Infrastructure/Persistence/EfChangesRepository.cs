using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Application.Routing;
using Vipi.Domain;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Implementazione EF di <see cref="IChangesRepository"/>.</summary>
public sealed class EfChangesRepository : IChangesRepository
{
    private readonly VipiDbContext _db;
    private readonly IReleaseTargetRegistry _targets;
    private readonly IDocRoutesRegistry _routes;
    private readonly IReleaseRepository _releases;

    public EfChangesRepository(VipiDbContext db, IReleaseTargetRegistry targets, IDocRoutesRegistry routes,
        IReleaseRepository releases)
    {
        _db = db;
        _targets = targets;
        _routes = routes;
        _releases = releases;
    }

    public async Task<IReadOnlyList<ChangeRow>> ListChangedAsync(string airacCycle, CancellationToken ct = default)
    {
        var docs = await _db.Documents
            .Where(d => d.CurrentVersionId != null)
            .Include(d => d.Sectors).ThenInclude(s => s.Acc)
            // L'aeroporto descritto: da qui il descrittore prende ICAO e ACC (vedi AirportReleaseTarget).
            .Include(d => d.Airport).ThenInclude(a => a!.Acc)
            // ⚠️ E quello dell'edizione MILITARE: legame diverso, navigazione diversa. Senza, il documento
            // militare non viene descritto da nessuno e sparisce di qui in silenzio — la spiegazione lunga
            // sta su `EfDocumentAdminRepository.ListAsync`, che fa la stessa query.
            .Include(d => d.MilAirport).ThenInclude(a => a!.Acc)
            .Include(d => d.Parties).ThenInclude(p => p.Sector).ThenInclude(s => s!.Acc)
            .Include(d => d.CurrentVersion)
            .AsNoTracking().ToListAsync(ct);

        // Tipo, ACC e ROTTA dai descrittori + registry delle rotte (doc 13 §3e). Qui c'era la QUARTA copia della
        // risoluzione — dopo VersioniPage, ReleasePreviewPage e la ricerca — con lo stesso errore: i documenti di
        // APP standalone puntavano alla vIPI di ACC.
        var described = docs
            .Where(d => d.CurrentVersion!.AiracCycle == airacCycle)
            .Select(d => (Doc: d, Managed: Describe(d)))
            .Where(x => x.Managed is not null && !string.IsNullOrEmpty(x.Managed!.AccCode))
            .ToList();

        // Stesso gate della pagina (doc 13 §3f): niente documenti nascosti né senza release effettiva — l'elenco
        // linkava anche documenti che, aperti, dicono «non disponibile».
        var visible = await PublicDocumentGate.VisibleAsync(described, x => x.Doc, x => x.Managed!, _releases, ct);

        var rows = new List<ChangeRow>();
        foreach (var (d, managed) in visible)
        {
            var cur = d.CurrentVersion!;
            var acc = managed!.AccCode!;
            var url = _routes.For(managed.Kind).PublicUrl(acc.ToLowerInvariant(), managed.ReleaseKey, managed.NeighbourCode);
            if (url is null) continue;

            // versione precedente (numero più alto < corrente)
            var prevVersionId = await _db.DocumentVersions
                .Where(v => v.DocumentId == d.Id && v.VersionNumber < cur.VersionNumber)
                .OrderByDescending(v => v.VersionNumber).Select(v => (int?)v.Id).FirstOrDefaultAsync(ct);

            var currBlocks = await _db.ContentBlocks.CountAsync(b => b.DocumentVersionId == cur.Id, ct);
            var currSections = await _db.DocumentSections.CountAsync(s => s.DocumentVersionId == cur.Id, ct);
            var prevBlocks = prevVersionId is int pv ? await _db.ContentBlocks.CountAsync(b => b.DocumentVersionId == pv, ct) : 0;
            var prevSections = prevVersionId is int pv2 ? await _db.DocumentSections.CountAsync(s => s.DocumentVersionId == pv2, ct) : 0;

            rows.Add(new ChangeRow
            {
                DocTitle = d.Title,
                Type = d.Type,
                AccCode = acc,
                Url = url,
                VersionNumber = cur.VersionNumber,
                Note = cur.Note,
                PublishedByUserId = cur.CreatedByUserId,
                PublishedUtc = cur.CreatedUtc,
                PrevBlocks = prevBlocks,
                CurrBlocks = currBlocks,
                PrevSections = prevSections,
                CurrSections = currSections,
            });
        }

        return rows.OrderByDescending(r => r.PublishedUtc).ToList();
    }

    /// <summary>Attribuisce il documento a un tipo con gli stessi descrittori dell'elenco unificato.</summary>
    private ManagedDoc? Describe(Domain.Entities.Document doc)
    {
        foreach (var target in _targets.ByDescribeOrder)
            if (target.TryDescribe(doc, hasDraft: false, out var managed))
                return managed;
        return null;
    }
}
