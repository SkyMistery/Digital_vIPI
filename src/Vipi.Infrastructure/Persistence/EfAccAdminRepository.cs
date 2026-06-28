using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Implementazione EF di <see cref="IAccAdminRepository"/>. Import = upsert ACC + settori CTR dalla sorgente,
/// preservando IsHidden e il contenimento esistenti. Niente cancellazioni (gli ACC non più in sorgente
/// restano nel DB; l'admin li nasconde).
/// </summary>
public sealed class EfAccAdminRepository : IAccAdminRepository
{
    private readonly VipiDbContext _db;
    public EfAccAdminRepository(VipiDbContext db) => _db = db;

    private const int FssUpperFt = 19000;   // limite superiore di default dei settori FSS (GND→19000)
    private static bool IsFss(string? position) =>
        string.Equals(position?.Trim(), "FSS", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<AccAdminRow>> ListAccsAsync(CancellationToken ct = default) =>
        await _db.Accs.AsNoTracking()
            .OrderBy(a => a.Code)
            .Select(a => new AccAdminRow(a.Id, a.Code, a.Name, a.IsMilitary, a.IsHidden))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AccSectorRow>> ListSubcentersAsync(CancellationToken ct = default) =>
        await _db.AccSectors.AsNoTracking()
            .OrderBy(s => s.CenterId).ThenBy(s => s.ComposePosition)
            .Select(s => new AccSectorRow(s.Id, s.ComposePosition, s.CenterId, s.Position, s.MiddleIdentifier,
                s.Frequency, s.LowerLimit, s.UpperLimit, s.IsHidden, s.RegionMapPolygon != null,
                s.Acc!.IsHidden))
            .ToListAsync(ct);

    public async Task SetHiddenAsync(int accId, bool hidden, CancellationToken ct = default)
    {
        var acc = await _db.Accs.FirstOrDefaultAsync(a => a.Id == accId, ct)
                  ?? throw new InvalidOperationException($"ACC id {accId} inesistente.");
        acc.IsHidden = hidden;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetSubcenterHiddenAsync(int id, bool hidden, CancellationToken ct = default)
    {
        var s = await _db.AccSectors.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new InvalidOperationException($"Settore ATC id {id} inesistente.");
        s.IsHidden = hidden;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetSubcenterLimitsAsync(int id, int? lower, int? upper, CancellationToken ct = default)
    {
        var s = await _db.AccSectors.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new InvalidOperationException($"Settore ATC id {id} inesistente.");
        s.LowerLimit = lower ?? 0;     // inferiore: vuoto → 0
        s.UpperLimit = upper;          // superiore: vuoto → null = UNL
        await _db.SaveChangesAsync(ct);
    }

    public async Task<(int Created, int Updated)> ImportSubcentersAsync(IReadOnlyList<SourceSubcenter> subs, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        int created = 0, updated = 0;

        var accCodes = (await _db.Accs.Select(a => a.Code).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = await _db.AccSectors
            .ToDictionaryAsync(s => s.ComposePosition, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var s in subs)
        {
            var compose = s.ComposePosition.Trim().ToUpperInvariant();
            if (compose.Length == 0) continue;
            var center = s.CenterId.Trim().ToUpperInvariant();
            if (!accCodes.Contains(center)) continue;   // niente ACC corrispondente → salta (FK)

            if (existing.TryGetValue(compose, out var row))
            {
                row.CenterId = center;
                row.Position = s.Position;
                row.MiddleIdentifier = s.MiddleIdentifier;
                row.Frequency = s.Frequency;
                row.RegionMapPolygon = s.RegionMapPolygon;
                // Limiti: l'admin comanda; aggiorna solo se la sorgente li espone (oggi null → preserva).
                if (s.LowerLimit is not null) row.LowerLimit = s.LowerLimit;
                else row.LowerLimit ??= 0;                 // default inferiore = GND (0)
                if (s.UpperLimit is not null) row.UpperLimit = s.UpperLimit;
                else if (row.UpperLimit is null && IsFss(s.Position)) row.UpperLimit = FssUpperFt;  // FSS: GND→19000
                // (altri) superiore null = UNL (illimitato)
                row.ImportedAtUtc = now;
                updated++;
            }
            else
            {
                _db.AccSectors.Add(new AccSector
                {
                    ComposePosition = compose,
                    CenterId = center,
                    Position = s.Position,
                    MiddleIdentifier = s.MiddleIdentifier,
                    Frequency = s.Frequency,
                    RegionMapPolygon = s.RegionMapPolygon,
                    LowerLimit = s.LowerLimit ?? 0,        // default GND (0)
                    UpperLimit = s.UpperLimit ?? (IsFss(s.Position) ? FssUpperFt : (int?)null),  // FSS→19000, altri→UNL
                    IsHidden = false,
                    ImportedAtUtc = now,
                });
                created++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return (created, updated);
    }

    public async Task<(int Created, int Updated)> ImportAsync(IReadOnlyList<SourceCenter> centers, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        int accsCreated = 0, accsUpdated = 0;

        // Ogni center area della sorgente = un ACC. Upsert per codice (centerId). Niente settori.
        var groups = centers
            .GroupBy(c => c.CenterId, StringComparer.OrdinalIgnoreCase)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .ToList();

        var existingAccs = await _db.Accs.ToDictionaryAsync(a => a.Code, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var g in groups)
        {
            var code = g.Key.Trim().ToUpperInvariant();
            var name = g.Select(c => c.Name).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? code;
            var military = g.Any(c => c.Military);

            if (existingAccs.TryGetValue(code, out var acc))
            {
                acc.Name = name;
                acc.IsMilitary = military;
                acc.ImportedAtUtc = now;
                accsUpdated++;
            }
            else
            {
                _db.Accs.Add(new Acc
                {
                    Code = code,
                    Name = name,
                    CountryPrefix = code.Length >= 2 ? code[..2] : code,
                    IsMilitary = military,
                    IsHidden = false,
                    ImportedAtUtc = now,
                });
                accsCreated++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return (accsCreated, accsUpdated);
    }
}
