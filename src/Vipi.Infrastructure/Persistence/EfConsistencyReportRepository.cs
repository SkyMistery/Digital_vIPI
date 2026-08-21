using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Diagnostics;
using Vipi.Domain;

namespace Vipi.Infrastructure.Persistence;

/// <summary>EF: fotografia di sola lettura dei dati con soft-ref per il report di consistenza (nessuna scrittura).</summary>
public sealed class EfConsistencyReportRepository : IConsistencyReportRepository
{
    private readonly VipiDbContext _db;
    public EfConsistencyReportRepository(VipiDbContext db) => _db = db;

    public async Task<ConsistencyDataset> LoadAsync(CancellationToken ct = default)
    {
        // Condizioni pista/area: solo le clausole che hanno effettivamente un soft-ref o un'area da verificare.
        // Legge gli ACCORDI, non piu' i flussi: dopo il travaso la verita' sta li', e un report che guardasse le
        // tabelle storiche direbbe cose vere di un archivio che nessuno modifica piu'.
        var conditions = await (
            from c in _db.AgreementClauses.AsNoTracking()
            join s in _db.AgreementSections.AsNoTracking() on c.SectionId equals s.Id
            join g in _db.CoordinationAgreements.AsNoTracking() on s.AgreementId equals g.Id
            join a in _db.Accs.AsNoTracking() on g.OwnerAccId equals a.Id
            where c.ConditionRefId != null || c.ConditionAreaLabel != null
            select new TransferConditionRow(c.Id, a.Code, c.Cops, c.ConditionRefId, c.ConditionLabel, c.ConditionAreaLabel)
        ).ToListAsync(ct);

        var runwayIdents = await _db.AirportRunways.AsNoTracking()
            .Select(r => new { r.Id, r.Ident })
            .ToDictionaryAsync(x => x.Id, x => x.Ident, ct);

        var areaNames = (await _db.SpecialAreas.AsNoTracking().Select(s => s.Name).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Padri di copertura dichiarati (soft-ref per callsign, cross-catalogo, no FK).
        var parentRefs = new List<ParentRefRow>();
        parentRefs.AddRange(await _db.AccSectors.AsNoTracking()
            .Where(s => s.ParentCallsign != null)
            .Select(s => new ParentRefRow("Settore ACC", s.ComposePosition, s.ParentCallsign!, "Diag_Ent_SettoreAcc")).ToListAsync(ct));
        parentRefs.AddRange(await _db.AirportSectors.AsNoTracking()
            .Where(s => s.ParentCallsign != null)
            .Select(s => new ParentRefRow("Settore APT", s.ComposePosition, s.ParentCallsign!, "Diag_Ent_SettoreApt")).ToListAsync(ct));
        parentRefs.AddRange(await _db.Airports.AsNoTracking()
            .Where(a => a.ParentCallsign != null)
            .Select(a => new ParentRefRow("Aeroporto", a.Icao, a.ParentCallsign!, "Diag_Ent_Aeroporto")).ToListAsync(ct));

        // Callsign validi come padre = chiavi naturali dei cataloghi (ACC + aeroporto).
        var valid = (await _db.AccSectors.AsNoTracking().Select(s => s.ComposePosition).ToListAsync(ct))
            .Concat(await _db.AirportSectors.AsNoTracking().Select(s => s.ComposePosition).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new ConsistencyDataset
        {
            TransferConditions = conditions,
            RunwayIdents = runwayIdents,
            AreaNames = areaNames,
            ParentRefs = parentRefs,
            ValidCallsigns = valid,
            RegulatedRefs = await LoadRegulatedRefsAsync(ct),
            SpecialAreaIds = (await _db.SpecialAreas.AsNoTracking().Select(s => s.IvaoId).ToListAsync(ct))
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
        };
    }

    /// <summary>
    /// Sezioni <c>regulated</c> (vIPI ACC: figlia di un blocco; vIPI APP non remotizzata: di primo livello) con il
    /// JSON della selezione. Solo la <b>versione di lavoro</b> di ogni documento — bozza più recente, altrimenti la
    /// pubblicata corrente, altrimenti l'ultima: le versioni storiche sono congelate per definizione e segnalarle
    /// sarebbe rumore su qualcosa che nessuno può più correggere.
    /// </summary>
    private async Task<IReadOnlyList<RegulatedRefRow>> LoadRegulatedRefsAsync(CancellationToken ct)
    {
        var docs = await _db.Documents.AsNoTracking()
            .Select(d => new { d.Id, d.Title, d.Type, d.CurrentVersionId })
            .ToListAsync(ct);
        if (docs.Count == 0) return Array.Empty<RegulatedRefRow>();

        var versions = await _db.DocumentVersions.AsNoTracking()
            .Select(v => new { v.Id, v.DocumentId, v.VersionNumber, v.Status })
            .ToListAsync(ct);

        var working = new Dictionary<int, int>();   // versionId → documentId
        foreach (var d in docs)
        {
            var draft = versions.Where(v => v.DocumentId == d.Id && v.Status == DocumentStatus.Draft)
                .OrderByDescending(v => v.VersionNumber).Select(v => (int?)v.Id).FirstOrDefault();
            var last = versions.Where(v => v.DocumentId == d.Id)
                .OrderByDescending(v => v.VersionNumber).Select(v => (int?)v.Id).FirstOrDefault();
            if ((draft ?? d.CurrentVersionId ?? last) is int id) working[id] = d.Id;
        }
        if (working.Count == 0) return Array.Empty<RegulatedRefRow>();

        var versionIds = working.Keys.ToList();
        var rows = await (
            from s in _db.DocumentSections.AsNoTracking()
            where s.SectionKey == "regulated" && versionIds.Contains(s.DocumentVersionId)
            select new
            {
                s.DocumentVersionId,
                Json = _db.ContentBlocks.AsNoTracking()
                    .Where(b => b.SectionId == s.Id).OrderBy(b => b.Order).Select(b => b.BodyJson).FirstOrDefault(),
            }).ToListAsync(ct);

        var byId = docs.ToDictionary(d => d.Id);
        return rows
            .Where(r => r.Json != null)
            .Select(r =>
            {
                var doc = byId[working[r.DocumentVersionId]];
                return new RegulatedRefRow(doc.Type == DocumentType.Vloa ? "vLOA" : "vIPI", doc.Title, r.Json);
            })
            .ToList();
    }
}
