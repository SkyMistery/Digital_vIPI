using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>EF: stato di freschezza degli import periodici (una riga per categoria).</summary>
public sealed class EfImportStateStore : IImportStateStore
{
    private readonly VipiDbContext _db;
    public EfImportStateStore(VipiDbContext db) => _db = db;

    public async Task<DateTime?> GetLastSuccessAsync(string category, CancellationToken ct = default)
    {
        var row = await _db.ImportStates.AsNoTracking().FirstOrDefaultAsync(x => x.Category == category, ct);
        return row?.LastSuccessUtc;
    }

    public async Task MarkSuccessAsync(string category, DateTime utc, CancellationToken ct = default)
    {
        var row = await _db.ImportStates.FirstOrDefaultAsync(x => x.Category == category, ct);
        if (row is null) { row = new ImportState { Category = category }; _db.ImportStates.Add(row); }
        row.LastSuccessUtc = utc;
        await _db.SaveChangesAsync(ct);
    }
}
