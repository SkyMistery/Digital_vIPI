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

    public EfChangesRepository(VipiDbContext db, IReleaseTargetRegistry targets, IDocRoutesRegistry routes)
    {
        _db = db;
        _targets = targets;
        _routes = routes;
    }

    public async Task<IReadOnlyList<ChangeRow>> ListChangedAsync(string airacCycle, CancellationToken ct = default)
    {
        var docs = await _db.Documents
            .Where(d => d.CurrentVersionId != null)
            .Include(d => d.Sectors).ThenInclude(s => s.Acc)
            .Include(d => d.Parties).ThenInclude(p => p.Sector).ThenInclude(s => s!.Acc)
            .Include(d => d.CurrentVersion)
            .AsNoTracking().ToListAsync(ct);

        var rows = new List<ChangeRow>();
        foreach (var d in docs)
        {
            var cur = d.CurrentVersion!;
            if (cur.AiracCycle != airacCycle) continue;

            // Tipo, ACC e ROTTA dai descrittori + registry delle rotte (doc 13 §3e). Qui c'era la QUARTA copia
            // della risoluzione — dopo VersioniPage, ReleasePreviewPage e la ricerca — con lo stesso errore:
            // i documenti di APP standalone puntavano alla vIPI di ACC.
            ManagedDoc? managed = null;
            foreach (var target in _targets.ByDescribeOrder)
                if (target.TryDescribe(d, hasDraft: false, out var m)) { managed = m; break; }
            if (managed is null) continue;

            var acc = managed.AccCode;
            if (string.IsNullOrEmpty(acc)) continue;
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
}
