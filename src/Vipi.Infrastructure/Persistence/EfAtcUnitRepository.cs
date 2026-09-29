using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using static Vipi.Application.Messaggio;

namespace Vipi.Infrastructure.Persistence;

/// <summary>EF degli enti ATC (vedi <see cref="AtcUnit"/>).</summary>
public sealed class EfAtcUnitRepository : IAtcUnitRepository
{
    private readonly VipiDbContext _db;
    private readonly IAiracService _airac;

    public EfAtcUnitRepository(VipiDbContext db, IAiracService? airac = null)
    {
        _db = db;
        _airac = airac ?? new AiracService();
    }

    private static string Norm(string? s) => (s ?? "").Trim().ToUpperInvariant();

    private IQueryable<AtcUnit> Con() =>
        _db.AtcUnits.AsNoTracking().Include(u => u.Positions).Include(u => u.Acc);

    internal static AtcUnitRow Riga(AtcUnit u) => new(u.Id, u.Code, u.Name, u.Acc?.Code ?? "", u.Mode, u.DocumentId,
        u.Positions.OrderBy(p => p.Order).ThenBy(p => p.Id).Select(p => p.Callsign).ToList());

    public async Task<AtcUnitRow?> FindAsync(string key, CancellationToken ct = default)
    {
        var k = Norm(key);
        if (k.Length == 0) return null;
        // Il codice vince sulla posizione: un codice è per sempre, una posizione può passare a un altro ente.
        var u = await Con().FirstOrDefaultAsync(x => x.Code == k, ct)
                ?? await Con().FirstOrDefaultAsync(x => x.Positions.Any(p => p.Callsign == k), ct);
        return u is null ? null : Riga(u);
    }

    public async Task<IReadOnlyList<AtcUnitRow>> ListAsync(string? accCode = null, CancellationToken ct = default)
    {
        var q = Con();
        if (!string.IsNullOrWhiteSpace(accCode))
        {
            var acc = Norm(accCode);
            q = q.Where(u => u.Acc!.Code == acc);
        }
        return (await q.OrderBy(u => u.Code).ToListAsync(ct)).Select(Riga).ToList();
    }

    public async Task<int> EnsureDocumentAsync(string code, string name, string accCode, SectionProfile profile,
        int authorUserId, CancellationToken ct = default)
    {
        code = Norm(code);
        var unit = await _db.AtcUnits.Include(u => u.Positions).FirstOrDefaultAsync(u => u.Code == code, ct);
        if (unit?.DocumentId is int esistente) return esistente;   // idempotente

        if (unit is null)
        {
            var acc = Norm(accCode);
            var accId = await _db.Accs.Where(a => a.Code == acc).Select(a => (int?)a.Id).FirstOrDefaultAsync(ct)
                        ?? throw new InvalidOperationException(Lingua($"ACC {acc} inesistente.", $"ACC {acc} does not exist."));
            unit = new AtcUnit { Code = code, Name = string.IsNullOrWhiteSpace(name) ? code : name.Trim(), AccId = accId };
            // Nasce con la posizione da cui nasce, se nessun altro ente la tiene già.
            if (!await _db.AtcUnitPositions.AnyAsync(p => p.Callsign == code, ct))
                unit.Positions.Add(new AtcUnitPosition { Callsign = code, Order = 0 });
            _db.AtcUnits.Add(unit);
        }

        // La nascita è condivisa con le altre famiglie (Seed/DocumentBirth): documento, prima versione bozza e
        // le sezioni del profilo, coi segnaposto sulle sezioni rese dalla pagina.
        var (doc, _) = Seed.DocumentBirth.Crea(_db, _airac, unit.Name, Domain.Language.It, profile, authorUserId);
        await _db.SaveChangesAsync(ct);
        unit.DocumentId = doc.Id;
        await _db.SaveChangesAsync(ct);
        return doc.Id;
    }

    private async Task<AtcUnit> CaricaAsync(int unitId, CancellationToken ct) =>
        await _db.AtcUnits.Include(u => u.Positions).FirstOrDefaultAsync(u => u.Id == unitId, ct)
        ?? throw new ValidationException(Lingua($"Ente {unitId} inesistente.", $"Unit {unitId} does not exist."));

    public async Task AddPositionAsync(int unitId, string callsign, CancellationToken ct = default)
    {
        var cs = Norm(callsign);
        if (cs.Length == 0) throw new ValidationException(Lingua("Nominativo vuoto.", "Empty callsign."));
        var unit = await CaricaAsync(unitId, ct);
        if (unit.Positions.Any(p => p.Callsign == cs)) return;
        var altro = await _db.AtcUnitPositions.Where(p => p.Callsign == cs).Select(p => p.AtcUnit!.Name).FirstOrDefaultAsync(ct);
        if (altro is not null)
            throw new ValidationException(Lingua($"{cs} è già una posizione di «{altro}».", $"{cs} is already a position of «{altro}»."));
        unit.Positions.Add(new AtcUnitPosition
        {
            Callsign = cs, Order = unit.Positions.Count == 0 ? 0 : unit.Positions.Max(p => p.Order) + 1,
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemovePositionAsync(int unitId, string callsign, CancellationToken ct = default)
    {
        var cs = Norm(callsign);
        var unit = await CaricaAsync(unitId, ct);
        var p = unit.Positions.FirstOrDefault(x => x.Callsign == cs);
        if (p is null) return;
        _db.AtcUnitPositions.Remove(p);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MakePrimaryAsync(int unitId, string callsign, CancellationToken ct = default)
    {
        var cs = Norm(callsign);
        var unit = await CaricaAsync(unitId, ct);
        if (unit.Positions.All(p => p.Callsign != cs)) return;
        var ordine = 1;
        foreach (var p in unit.Positions.OrderBy(p => p.Order).ThenBy(p => p.Id))
            p.Order = p.Callsign == cs ? 0 : ordine++;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RenameAsync(int unitId, string name, CancellationToken ct = default)
    {
        var n = (name ?? "").Trim();
        if (n.Length == 0) throw new ValidationException(Lingua("Il nome dell'ente è vuoto.", "The unit name is empty."));
        var unit = await CaricaAsync(unitId, ct);
        unit.Name = n;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RenamePositionAsync(string oldCallsign, string newCallsign, CancellationToken ct = default)
    {
        var vecchio = Norm(oldCallsign);
        var nuovo = Norm(newCallsign);
        var p = await _db.AtcUnitPositions.FirstOrDefaultAsync(x => x.Callsign == vecchio, ct);
        if (p is null || vecchio == nuovo) return;
        if (await _db.AtcUnitPositions.AnyAsync(x => x.Callsign == nuovo, ct)) { _db.AtcUnitPositions.Remove(p); }
        else p.Callsign = nuovo;
        await _db.SaveChangesAsync(ct);
    }
}
