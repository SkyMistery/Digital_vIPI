using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="IDocumentAdminRepository"/>
public sealed class EfDocumentAdminRepository : IDocumentAdminRepository
{
    private readonly VipiDbContext _db;
    private readonly IReleaseTargetRegistry _targets;
    private readonly IReleaseRepository _releases;
    public EfDocumentAdminRepository(VipiDbContext db, IReleaseTargetRegistry targets, IReleaseRepository releases)
    {
        _db = db;
        _targets = targets;
        _releases = releases;
    }

    public async Task<IReadOnlyList<ManagedDoc>> ListAsync(CancellationToken ct = default)
    {
        // Post-08 tutti e 4 i tipi sono su Document: una query, poi ogni Document è attribuito al primo descrittore
        // che lo riconosce (doc 09 §3a). Aggiungere un tipo = registrare un IReleaseTarget, niente switch qui.
        var docs = await _db.Documents.AsNoTracking()
            .Include(d => d.Sectors).ThenInclude(s => s.Acc)
            .Include(d => d.Parties).ThenInclude(p => p.Sector).ThenInclude(s => s!.Acc)
            .ToListAsync(ct);
        var draftDocIds = (await _db.DocumentVersions.AsNoTracking()
            .Where(v => v.Status == DocumentStatus.Draft).Select(v => v.DocumentId).Distinct().ToListAsync(ct)).ToHashSet();

        var result = new List<ManagedDoc>();
        foreach (var d in docs)
            foreach (var target in _targets.ByDescribeOrder)
                if (target.TryDescribe(d, draftDocIds.Contains(d.Id), out var managed))
                {
                    result.Add(managed);
                    break;
                }

        // Visibilità pubblica = release effettiva (doc 10 §3f): una sola query batch per popolare HasEffectiveRelease,
        // così i gate delle liste pubbliche filtrano su questo invece di Status==Published.
        var summaries = await _releases.SummariesAsync(
            result.Select(m => (m.ReleaseTarget, m.ReleaseKey)).Distinct().ToList(), ct);
        result = result.Select(m => m with
        {
            HasEffectiveRelease = summaries.TryGetValue((m.ReleaseTarget, m.ReleaseKey), out var s) && s.EffectiveCycle is not null,
        }).ToList();

        return result.OrderBy(r => r.Kind).ThenBy(r => r.Title).ToList();
    }

    public async Task<string?> GetAccCodeAsync(ManagedDocRef doc, CancellationToken ct = default)
    {
        // vLOA: la chiave di release è il docId, ma la deriviamo dal DocumentId del ref (identico all'AuthAccCode del
        // descrittore, che parte dalla chiave = docId). Gli altri tipi hanno chiave = release key.
        var key = doc.Kind == ManagedDocKind.Vloa ? doc.DocumentId?.ToString() ?? "" : doc.ReleaseKey;
        return await _targets.For(doc.Kind).AuthAccCodeAsync(key, ct);
    }

    public async Task SetHiddenAsync(ManagedDocRef doc, bool hidden, CancellationToken ct = default)
    {
        // Post-08 tutti i tipi sono su Document → un solo ramo: il flag vive sul Document.
        if (doc.DocumentId is int id)
        {
            var d = await _db.Documents.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (d is not null) { d.IsHidden = hidden; await _db.SaveChangesAsync(ct); }
        }
    }

    public async Task DeleteAsync(ManagedDocRef doc, CancellationToken ct = default)
    {
        // Rimuovi sempre le release del bersaglio (DocRelease non ha FK → non cascada). Tipo di release dal descrittore.
        var relType = _targets.For(doc.Kind).Type;
        var rels = await _db.DocReleases.Where(r => r.TargetType == relType && r.TargetKey == doc.ReleaseKey).ToListAsync(ct);
        if (rels.Count > 0) _db.DocReleases.RemoveRange(rels);

        // Post-08 tutti i tipi sono su Document → un solo ramo di cancellazione (cascade EF).
        if (doc.DocumentId is int id)
        {
            var d = await _db.Documents.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (d is not null)
            {
                d.CurrentVersionId = null;   // rompi il ciclo CurrentVersion (NoAction) prima del cascade
                await _db.SaveChangesAsync(ct);
                _db.Documents.Remove(d);      // cascade: Versions/Sections/Blocks/Parties/DocumentProfile; Sector.DocumentId→SetNull
            }
        }
        await _db.SaveChangesAsync(ct);
    }
}
