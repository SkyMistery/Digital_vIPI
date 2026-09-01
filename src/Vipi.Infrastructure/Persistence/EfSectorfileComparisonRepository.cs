using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Diagnostics;
using Vipi.Infrastructure.Sectorfile;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// EF: il lato vIPI del confronto col sectorfile. Sola lettura, quattro query, nessuna scrittura.
/// </summary>
public sealed class EfSectorfileComparisonRepository : ISectorfileComparisonRepository
{
    private readonly VipiDbContext _db;
    public EfSectorfileComparisonRepository(VipiDbContext db) => _db = db;

    public async Task<SectorfileComparisonDataset> LoadAsync(CancellationToken ct = default)
    {
        // Le posizioni stanno in due cataloghi (subcenter d'ACC e postazioni d'aeroporto) e per questo
        // confronto sono la stessa cosa: un callsign con una frequenza.
        var posizioni = new List<VipiAtcPosition>();
        posizioni.AddRange(await _db.AccSectors.AsNoTracking()
            .Select(s => new VipiAtcPosition(s.ComposePosition, s.Frequency, s.IsManual)).ToListAsync(ct));
        posizioni.AddRange(await _db.AirportSectors.AsNoTracking()
            .Select(s => new VipiAtcPosition(s.ComposePosition, s.Frequency, s.IsManual)).ToListAsync(ct));

        var aeroporti = await _db.Airports.AsNoTracking()
            .Select(a => new VipiAirport(a.Icao, a.TransitionAltitudeFt, a.ElevationFt, a.Latitude, a.Longitude))
            .ToListAsync(ct);

        // ⚠️ L'ident si normalizza QUI, con la stessa funzione che normalizza quello del sectorfile: due
        // normalizzazioni diverse sui due lati sono il modo piu' rapido di far divergere un confronto.
        var piste = (await (
                from r in _db.AirportRunways.AsNoTracking()
                join a in _db.Airports.AsNoTracking() on r.AirportId equals a.Id
                select new { a.Icao, r.Ident, r.ThresholdLat, r.ThresholdLon })
            .ToListAsync(ct))
            .Select(r => new VipiRunwayEnd(r.Icao.ToUpperInvariant(),
                AuroraSectorfileParser.NormalizzaIdentPista(r.Ident), r.ThresholdLat, r.ThresholdLon))
            .ToList();

        var acc = (await _db.Accs.AsNoTracking().Select(a => a.Code).ToListAsync(ct))
            .Select(c => c.ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);

        return new SectorfileComparisonDataset
        {
            Positions = posizioni.Select(p => p with { Callsign = p.Callsign.ToUpperInvariant() }).ToList(),
            Airports = aeroporti.Select(a => a with { Icao = a.Icao.ToUpperInvariant() }).ToList(),
            RunwayEnds = piste,
            AccCodes = acc,
        };
    }
}
