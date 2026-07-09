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
    public EfDocumentAdminRepository(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<ManagedDoc>> ListAsync(CancellationToken ct = default)
    {
        var result = new List<ManagedDoc>();

        // 1) Document (vLOA + vIPI aeroporto). Una query, con settori/parti per scope/ACC.
        var docs = await _db.Documents.AsNoTracking()
            .Include(d => d.Sectors).ThenInclude(s => s.Acc)
            .Include(d => d.Parties).ThenInclude(p => p.Sector).ThenInclude(s => s!.Acc)
            .ToListAsync(ct);
        var draftDocIds = (await _db.DocumentVersions.AsNoTracking()
            .Where(v => v.Status == DocumentStatus.Draft).Select(v => v.DocumentId).Distinct().ToListAsync(ct)).ToHashSet();

        foreach (var d in docs)
        {
            if (d.Type == DocumentType.Vloa)
            {
                var home = d.Parties.FirstOrDefault(p => p.Role == PartyRole.Home)?.Sector?.Acc?.Code;
                var neigh = d.Parties.FirstOrDefault(p => p.Role == PartyRole.Neighbour)?.Sector?.Acc?.Code;
                result.Add(new ManagedDoc(ManagedDocKind.Vloa, d.Title, home ?? "", home,
                    d.Status == DocumentStatus.Published, draftDocIds.Contains(d.Id), d.IsHidden,
                    ReleaseTargetType.Vloa, d.Id.ToString(), d.Id, neigh));
            }
            else
            {
                // vIPI aeroporto: settore aeroporto → ICAO + ACC.
                var airSec = d.Sectors.FirstOrDefault(s => s.Kind == SectorKind.Airport && s.AirportIcao != null);
                var icao = airSec?.AirportIcao ?? "";
                var acc = airSec?.Acc?.Code;
                result.Add(new ManagedDoc(ManagedDocKind.AirportVipi, d.Title, icao, acc,
                    d.Status == DocumentStatus.Published, draftDocIds.Contains(d.Id), d.IsHidden,
                    ReleaseTargetType.Airport, icao, d.Id));
            }
        }

        // 2) vIPI ACC (AccProfile, per albero). Una query.
        var accProfiles = await _db.AccProfiles.AsNoTracking().Include(p => p.Acc).ToListAsync(ct);
        foreach (var p in accProfiles)
        {
            var acc = p.Acc?.Code ?? "";
            var root = p.RootCallsign ?? "";
            result.Add(new ManagedDoc(ManagedDocKind.AccVipi, $"vIPI ACC {acc}{(root.Length > 0 ? $" · {root}" : "")}",
                root.Length > 0 ? root : acc, acc,
                true, false, p.IsHidden, ReleaseTargetType.AccVipi, $"{acc}|{root}", null));
        }

        // 3) APP standalone (AppProfile → Sector). Una query.
        var appProfiles = await _db.AppProfiles.AsNoTracking()
            .Join(_db.Sectors.AsNoTracking().Include(s => s.Acc), p => p.SectorId, s => s.Id, (p, s) => new { p, s })
            .ToListAsync(ct);
        foreach (var x in appProfiles)
        {
            result.Add(new ManagedDoc(ManagedDocKind.AppVipi, $"vIPI APP {x.s.Callsign}", x.s.Callsign, x.s.Acc?.Code,
                true, false, x.p.IsHidden, ReleaseTargetType.App, x.s.Callsign, null));
        }

        return result.OrderBy(r => r.Kind).ThenBy(r => r.Title).ToList();
    }

    public async Task<string?> GetAccCodeAsync(ManagedDocRef doc, CancellationToken ct = default)
    {
        switch (doc.Kind)
        {
            case ManagedDocKind.AccVipi:
                return doc.ReleaseKey.Split('|', 2)[0];
            case ManagedDocKind.AppVipi:
                return await _db.Sectors.AsNoTracking().Where(s => s.Callsign == doc.ReleaseKey)
                    .Select(s => s.Acc!.Code).FirstOrDefaultAsync(ct);
            case ManagedDocKind.AirportVipi:
                return await _db.Airports.AsNoTracking().Where(a => a.Icao == doc.ReleaseKey)
                    .Select(a => a.Acc!.Code).FirstOrDefaultAsync(ct);
            case ManagedDocKind.Vloa:
                return doc.DocumentId is int id
                    ? await _db.Documents.AsNoTracking().Where(d => d.Id == id)
                        .SelectMany(d => d.Parties).Where(p => p.Role == PartyRole.Home)
                        .Select(p => p.Sector!.Acc!.Code).FirstOrDefaultAsync(ct)
                    : null;
            default: return null;
        }
    }

    public async Task SetHiddenAsync(ManagedDocRef doc, bool hidden, CancellationToken ct = default)
    {
        switch (doc.Kind)
        {
            case ManagedDocKind.Vloa:
            case ManagedDocKind.AirportVipi:
                if (doc.DocumentId is int id)
                {
                    var d = await _db.Documents.FirstOrDefaultAsync(x => x.Id == id, ct);
                    if (d is not null) { d.IsHidden = hidden; await _db.SaveChangesAsync(ct); }
                }
                break;
            case ManagedDocKind.AccVipi:
            {
                var parts = doc.ReleaseKey.Split('|', 2);
                var acc = parts[0]; var root = parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null;
                var accId = await _db.Accs.Where(a => a.Code == acc).Select(a => (int?)a.Id).FirstOrDefaultAsync(ct);
                var p = await _db.AccProfiles.FirstOrDefaultAsync(x => x.AccId == accId && x.RootCallsign == root, ct);
                if (p is not null) { p.IsHidden = hidden; await _db.SaveChangesAsync(ct); }
                break;
            }
            case ManagedDocKind.AppVipi:
            {
                var sectorId = await _db.Sectors.Where(s => s.Callsign == doc.ReleaseKey).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
                var p = await _db.AppProfiles.FirstOrDefaultAsync(x => x.SectorId == sectorId, ct);
                if (p is not null) { p.IsHidden = hidden; await _db.SaveChangesAsync(ct); }
                break;
            }
        }
    }

    public async Task DeleteAsync(ManagedDocRef doc, CancellationToken ct = default)
    {
        // Rimuovi sempre le release del bersaglio (DocRelease non ha FK → non cascada).
        var relType = doc.Kind switch
        {
            ManagedDocKind.Vloa => ReleaseTargetType.Vloa,
            ManagedDocKind.AirportVipi => ReleaseTargetType.Airport,
            ManagedDocKind.AccVipi => ReleaseTargetType.AccVipi,
            _ => ReleaseTargetType.App,
        };
        var rels = await _db.DocReleases.Where(r => r.TargetType == relType && r.TargetKey == doc.ReleaseKey).ToListAsync(ct);
        if (rels.Count > 0) _db.DocReleases.RemoveRange(rels);

        switch (doc.Kind)
        {
            case ManagedDocKind.Vloa:
            case ManagedDocKind.AirportVipi:
                if (doc.DocumentId is int id)
                {
                    var d = await _db.Documents.FirstOrDefaultAsync(x => x.Id == id, ct);
                    if (d is not null)
                    {
                        d.CurrentVersionId = null;   // rompi il ciclo CurrentVersion (NoAction) prima del cascade
                        await _db.SaveChangesAsync(ct);
                        _db.Documents.Remove(d);      // cascade: Versions/Sections/Blocks/Parties/VloaProfile; Sector.DocumentId→SetNull
                    }
                }
                break;
            case ManagedDocKind.AccVipi:
            {
                var parts = doc.ReleaseKey.Split('|', 2);
                var acc = parts[0]; var root = parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null;
                var accId = await _db.Accs.Where(a => a.Code == acc).Select(a => (int?)a.Id).FirstOrDefaultAsync(ct);
                var p = await _db.AccProfiles.FirstOrDefaultAsync(x => x.AccId == accId && x.RootCallsign == root, ct);
                if (p is not null) _db.AccProfiles.Remove(p);
                break;
            }
            case ManagedDocKind.AppVipi:
            {
                var sectorId = await _db.Sectors.Where(s => s.Callsign == doc.ReleaseKey).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
                var p = await _db.AppProfiles.Include(x => x.FrequencyLinks).FirstOrDefaultAsync(x => x.SectorId == sectorId, ct);
                if (p is not null) _db.AppProfiles.Remove(p);   // FrequencyLinks cascade su AppProfile
                break;
            }
        }
        await _db.SaveChangesAsync(ct);
    }
}
