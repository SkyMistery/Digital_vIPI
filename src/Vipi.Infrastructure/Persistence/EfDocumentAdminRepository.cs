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
            // L'aeroporto descritto: da qui il descrittore prende ICAO e ACC (vedi AirportReleaseTarget).
            .Include(d => d.Airport).ThenInclude(a => a!.Acc)
            .Include(d => d.Parties).ThenInclude(p => p.Sector).ThenInclude(s => s!.Acc)
            .ToListAsync(ct);
        var draftDocIds = (await _db.DocumentVersions.AsNoTracking()
            .Where(v => v.Status == DocumentStatus.Draft).Select(v => v.DocumentId).Distinct().ToListAsync(ct)).ToHashSet();

        var now = DateTime.UtcNow;
        var result = new List<ManagedDoc>();
        foreach (var d in docs)
            foreach (var target in _targets.ByDescribeOrder)
                if (target.TryDescribe(d, draftDocIds.Contains(d.Id), out var managed))
                {
                    // Il lock di editing esce dalla query che c'è già: il Document è caricato intero, i tre campi
                    // sono in memoria. Un lock SCADUTO non è un lock — si normalizza qui, così nessun chiamante
                    // deve ricordarsi di confrontare la scadenza con l'ora (il gate del servizio e il badge della
                    // pagina leggono lo stesso fatto).
                    var attivo = d.LockedByUserId is not null && d.LockExpiresUtc is { } exp && exp > now;
                    result.Add(attivo
                        ? managed with
                        {
                            LockedByUserId = d.LockedByUserId,
                            LockedByName = d.LockedByName,
                            LockExpiresUtc = d.LockExpiresUtc,
                        }
                        : managed);
                    break;
                }

        // Stato release del bersaglio (doc 10 §3f): una sola query batch. Porta i cicli, non un bool —
        // HasEffectiveRelease è calcolato da EffectiveCycle, e /services/vsop/versions mostra gli stessi cicli senza
        // rifare la query per conto proprio (fino al 21 agosto 2026 la faceva due volte).
        var summaries = await _releases.SummariesAsync(
            result.Select(m => (m.ReleaseTarget, m.ReleaseKey)).Distinct().ToList(), ct);
        result = result.Select(m => summaries.TryGetValue((m.ReleaseTarget, m.ReleaseKey), out var s)
            ? m with { EffectiveCycle = s.EffectiveCycle, NextScheduledCycle = s.NextScheduledCycle, HasAnyRelease = s.HasAnyRelease }
            : m).ToList();

        return result.OrderBy(r => r.Kind).ThenBy(r => r.Title).ToList();
    }

    public async Task<IReadOnlyDictionary<int, string>> GetTitlesAsync(IReadOnlyCollection<int> documentIds, CancellationToken ct = default)
    {
        if (documentIds.Count == 0) return new Dictionary<int, string>();
        var ids = documentIds.Distinct().ToList();
        return await _db.Documents.AsNoTracking()
            .Where(d => ids.Contains(d.Id))
            .Select(d => new { d.Id, d.Title })
            .ToDictionaryAsync(x => x.Id, x => x.Title, ct);
    }

    public async Task<string?> GetAccCodeAsync(ManagedDocRef doc, CancellationToken ct = default)
    {
        // vLOA: la chiave di release è il docId, ma la deriviamo dal DocumentId del ref (identico all'AuthAccCode del
        // descrittore, che parte dalla chiave = docId). Gli altri tipi hanno chiave = release key.
        var key = doc.Kind == ManagedDocKind.Vloa ? doc.DocumentId?.ToString() ?? "" : doc.ReleaseKey;
        return await _targets.For(doc.Kind).AuthAccCodeAsync(key, ct);
    }

    public async Task SetHiddenAsync(ManagedDocRef doc, bool hidden, int actorUserId, CancellationToken ct = default)
    {
        // Post-08 tutti i tipi sono su Document → un solo ramo: il flag vive sul Document.
        if (doc.DocumentId is int id)
        {
            var d = await _db.Documents.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (d is null) return;
            // Il non-evento non si scrive: rimettere «nascosto» su un documento già nascosto non è un atto.
            if (d.IsHidden == hidden) return;
            d.IsHidden = hidden;
            AuditScribe.Write(_db, actorUserId, AuditAction.Update, "Document", id.ToString(),
                new { d.Title, Kind = doc.Kind.ToString(), Acc = await GetAccCodeAsync(doc, ct), Hidden = hidden });
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task DeleteAsync(ManagedDocRef doc, int actorUserId, CancellationToken ct = default)
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
                // ⚠️ L'audit va scritto PRIMA della cancellazione (come in EliminaBozzaAsync): dopo, il titolo
                // non è più leggibile e resterebbe un registro che dice «eliminato il documento 7». Il nome
                // accanto all'Id è tutto ciò che, fra sei mesi, distingue una pulizia da un incidente.
                AuditScribe.Write(_db, actorUserId, AuditAction.Delete, "Document", id.ToString(),
                    new
                    {
                        d.Title,
                        Kind = doc.Kind.ToString(),
                        Acc = await GetAccCodeAsync(doc, ct),
                        Releases = rels.Count,
                    });

                d.CurrentVersionId = null;   // rompi il ciclo CurrentVersion (NoAction) prima del cascade
                await _db.SaveChangesAsync(ct);
                _db.Documents.Remove(d);      // cascade: Versions/Sections/Blocks/Parties/DocumentProfile; Sector.DocumentId→SetNull
            }
        }
        await _db.SaveChangesAsync(ct);
    }
}
