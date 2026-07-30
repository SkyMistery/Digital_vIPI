using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="IDocumentMaintenance"/>
public sealed class EfDocumentMaintenance : IDocumentMaintenance
{
    private readonly VipiDbContext _db;

    public EfDocumentMaintenance(VipiDbContext db) => _db = db;

    public async Task<int> ReconcileCustomSectionKeysAsync(CancellationToken ct = default)
    {
        // Solo le sezioni con la chiave storica ambigua: le nuove nascono già univoche (doc 11 §3a).
        var legacy = await _db.DocumentSections
            .Where(s => s.SectionKey == SectionKeys.LegacyCustom)
            .ToListAsync(ct);
        if (legacy.Count == 0) return 0;

        foreach (var s in legacy)
        {
            s.SectionKey = SectionKeys.NewCustom();
            s.RowVersion = Guid.NewGuid().ToByteArray();
        }
        await _db.SaveChangesAsync(ct);
        return legacy.Count;
    }
}
