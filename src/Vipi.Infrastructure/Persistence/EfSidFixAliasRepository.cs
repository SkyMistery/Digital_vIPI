using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>EF: alias prefisso-troncato → fix reale (globali, uno per prefisso).</summary>
public sealed class EfSidFixAliasRepository : ISidFixAliasRepository
{
    private readonly VipiDbContext _db;
    public EfSidFixAliasRepository(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<SidFixAliasRow>> ListAsync(CancellationToken ct = default) =>
        await _db.SidFixAliases.AsNoTracking().OrderBy(x => x.Prefix)
            .Select(x => new SidFixAliasRow(x.Id, x.Prefix, x.FixName)).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<string, string>> GetMapAsync(CancellationToken ct = default) =>
        (await _db.SidFixAliases.AsNoTracking().ToListAsync(ct))
            .ToDictionary(x => x.Prefix, x => x.FixName, StringComparer.OrdinalIgnoreCase);

    public async Task UpsertAsync(string prefix, string fixName, CancellationToken ct = default)
    {
        prefix = prefix.Trim().ToUpperInvariant();
        fixName = fixName.Trim().ToUpperInvariant();
        if (prefix.Length == 0 || fixName.Length == 0) return;
        var row = await _db.SidFixAliases.FirstOrDefaultAsync(x => x.Prefix == prefix, ct);
        if (row is null) { row = new SidFixAlias { Prefix = prefix }; _db.SidFixAliases.Add(row); }
        row.FixName = fixName;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var row = await _db.SidFixAliases.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return;
        _db.SidFixAliases.Remove(row);
        await _db.SaveChangesAsync(ct);
    }
}
