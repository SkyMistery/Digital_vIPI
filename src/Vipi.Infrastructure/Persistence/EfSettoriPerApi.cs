using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// <see cref="ISettoriPerApi"/> sul database: la proiezione <c>Sectors</c>, la stessa che leggono la Struttura, il
/// banco di prova e i coordinamenti (S119).
/// </summary>
public sealed class EfSettoriPerApi : ISettoriPerApi
{
    private readonly VipiDbContext _db;

    public EfSettoriPerApi(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<ApiAccRow>> CentriAsync(CancellationToken ct = default) =>
        await _db.Accs.AsNoTracking()
            .Where(a => !a.IsHidden && !a.IsForeign)
            .OrderBy(a => a.Code)
            .Select(a => new ApiAccRow(a.Code, a.Name, a.IsMilitary))
            .ToListAsync(ct);

    public async Task<ApiAccRow?> CentroAsync(string codice, CancellationToken ct = default)
    {
        var c = (codice ?? "").Trim().ToUpperInvariant();
        return await _db.Accs.AsNoTracking()
            .Where(a => a.Code == c && !a.IsHidden && !a.IsForeign)
            .Select(a => new ApiAccRow(a.Code, a.Name, a.IsMilitary))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<ApiSectorRow>> SettoriAsync(string codice, CancellationToken ct = default)
    {
        if (await CentroAsync(codice, ct) is not { } centro) return Array.Empty<ApiSectorRow>();

        // ⚠️ Il padre si legge per Id su TUTTI i settori, non solo su quelli del centro: può stare in un altro
        // (`TopologyBuilder.BuildAsync` lo scarta, perché la topologia di un centro guarda dentro il centro).
        var righe = await _db.Sectors.AsNoTracking()
            .Where(s => s.IsActive && s.Acc!.Code == centro.Code)
            .Select(s => new
            {
                s.Callsign, s.Name, s.Type, s.Kind, s.AirportIcao, s.DefaultFrequency, s.CoverageOrder,
                Padre = s.ParentSector != null && s.ParentSector.IsActive ? s.ParentSector.Callsign : null,
            })
            .ToListAsync(ct);

        return righe
            .OrderBy(r => r.CoverageOrder).ThenBy(r => r.Callsign, StringComparer.Ordinal)
            .Select(r => new ApiSectorRow(r.Callsign, r.Name, r.Type, r.Kind, r.AirportIcao, r.DefaultFrequency,
                                          r.Padre, r.CoverageOrder))
            .ToList();
    }
}
