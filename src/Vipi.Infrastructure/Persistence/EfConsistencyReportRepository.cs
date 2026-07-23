using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Diagnostics;

namespace Vipi.Infrastructure.Persistence;

/// <summary>EF: fotografia di sola lettura dei dati con soft-ref per il report di consistenza (nessuna scrittura).</summary>
public sealed class EfConsistencyReportRepository : IConsistencyReportRepository
{
    private readonly VipiDbContext _db;
    public EfConsistencyReportRepository(VipiDbContext db) => _db = db;

    public async Task<ConsistencyDataset> LoadAsync(CancellationToken ct = default)
    {
        // Condizioni pista/area: solo i punti che hanno effettivamente un soft-ref o un'area da verificare.
        var conditions = await (
            from p in _db.TransferPoints.AsNoTracking()
            join f in _db.TransferFlows.AsNoTracking() on p.FlowId equals f.Id
            join a in _db.Accs.AsNoTracking() on f.AccId equals a.Id
            where p.ConditionRefId != null || p.ConditionAreaLabel != null
            select new TransferConditionRow(p.Id, a.Code, p.Cop, p.ConditionRefId, p.ConditionLabel, p.ConditionAreaLabel)
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
            .Select(s => new ParentRefRow("Settore ACC", s.ComposePosition, s.ParentCallsign!)).ToListAsync(ct));
        parentRefs.AddRange(await _db.AirportSectors.AsNoTracking()
            .Where(s => s.ParentCallsign != null)
            .Select(s => new ParentRefRow("Settore APT", s.ComposePosition, s.ParentCallsign!)).ToListAsync(ct));
        parentRefs.AddRange(await _db.Airports.AsNoTracking()
            .Where(a => a.ParentCallsign != null)
            .Select(a => new ParentRefRow("Aeroporto", a.Icao, a.ParentCallsign!)).ToListAsync(ct));

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
        };
    }
}
