using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="ISectorCatalogMaintenance"/>
public sealed class EfSectorCatalogMaintenance : ISectorCatalogMaintenance
{
    private readonly VipiDbContext _db;
    private readonly IImportStateStore _states;

    public EfSectorCatalogMaintenance(VipiDbContext db, IImportStateStore states)
    {
        _db = db;
        _states = states;
    }

    public async Task<int> MarkManualCatalogRowsAsync(CancellationToken ct = default)
    {
        var categoria = ImportCategories.ManualCatalogRows;
        if (await _states.GetLastSuccessAsync(categoria, ct) is not null) return 0;   // già fatto

        // Il confronto sta in memoria perché SQL Server/SQLite/MySQL traducono `Substring` in tre modi e la
        // tabella è di poche centinaia di righe: qui la portabilità vale più della query.
        var righe = await _db.AccSectors.ToListAsync(ct);
        var toccate = 0;
        foreach (var r in righe)
        {
            if (r.IsManual) continue;
            var prefisso = r.ComposePosition.Length >= 4 ? r.ComposePosition[..4] : r.ComposePosition;
            if (string.Equals(prefisso, r.CenterId, StringComparison.OrdinalIgnoreCase)) continue;
            r.IsManual = true;
            toccate++;
        }

        if (toccate > 0) await _db.SaveChangesAsync(ct);
        await _states.MarkSuccessAsync(categoria, DateTime.UtcNow, ct);
        return toccate;
    }
}
