using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// La mappa dei settori per l'attribuzione del traffico, letta dove le cose sono già decise:
/// l'<b>albero</b> dai settori proiettati (<c>Sector.ParentSectorId</c>, che include la scaletta
/// DEL→GND→TWR→APP già risolta) e i <b>volumi</b> dai cataloghi, per callsign.
///
/// <para>⚠️ Non si scende su <c>AccSector.ParentCallsign</c>: lì il padre ce l'hanno solo ACC e APP, e
/// rifare la scaletta a mano significherebbe sbagliarla in un secondo modo.</para>
/// </summary>
public sealed class EfSectorVolumeCatalog : ISectorVolumeCatalog
{
    private readonly VipiDbContext _db;

    public EfSectorVolumeCatalog(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<SectorVolumeRow>> GetAllAsync(CancellationToken ct = default)
    {
        var settori = await _db.Sectors.AsNoTracking()
            .Where(s => s.IsActive)
            .Select(s => new
            {
                s.Id,
                s.Callsign,
                s.ParentSectorId,
                s.Type,
                s.AirportIcao,
            })
            .ToListAsync(ct);

        var callsignPerId = settori.ToDictionary(s => s.Id, s => s.Callsign);

        var acc = await _db.AccSectors.AsNoTracking()
            .Select(a => new { a.ComposePosition, a.RegionMapPolygon, a.LowerLimit, a.UpperLimit })
            .ToListAsync(ct);

        var aeroporti = await _db.AirportSectors.AsNoTracking()
            .Select(a => new { a.ComposePosition, a.RegionMapPolygon, a.LowerLimit, a.UpperLimit })
            .ToListAsync(ct);

        var volumi = new Dictionary<string, (string? Poly, int? Lower, int? Upper)>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in acc) volumi[a.ComposePosition] = (a.RegionMapPolygon, a.LowerLimit, a.UpperLimit);
        foreach (var a in aeroporti) volumi[a.ComposePosition] = (a.RegionMapPolygon, a.LowerLimit, a.UpperLimit);

        return settori.Select(s =>
        {
            var v = volumi.TryGetValue(s.Callsign, out var trovato) ? trovato : default;
            return new SectorVolumeRow(
                Callsign: s.Callsign,
                ParentCallsign: s.ParentSectorId is { } pid && callsignPerId.TryGetValue(pid, out var padre) ? padre : null,
                Type: s.Type,
                AirportIcao: s.AirportIcao,
                RegionMapPolygon: v.Poly,
                LowerLimit: v.Lower,
                UpperLimit: v.Upper);
        }).ToList();
    }
}
