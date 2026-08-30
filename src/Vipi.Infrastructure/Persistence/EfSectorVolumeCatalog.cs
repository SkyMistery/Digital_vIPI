using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Airspace;
using Vipi.Domain;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// La mappa dei settori per l'attribuzione del traffico, letta dove le cose sono già decise:
/// l'<b>albero</b> dai settori proiettati (<c>Sector.ParentSectorId</c>, che include la scaletta
/// DEL→GND→TWR→APP già risolta) e le <b>forme</b> dalla porta unica
/// (<see cref="ISectorShapeResolver"/>, carta refactor 15).
///
/// <para>⚠️ Non si scende su <c>AccSector.ParentCallsign</c>: lì il padre ce l'hanno solo ACC e APP, e
/// rifare la scaletta a mano significherebbe sbagliarla in un secondo modo.</para>
///
/// <para>⚠️ <b>Prima leggeva le colonne dei cataloghi</b>, e un settore agganciato al suo CTR disegnava
/// nel documento un confine e rivendicava il traffico dentro un altro. Adesso la forma è la stessa che si
/// vede a schermo: se è di due zone sono due pezzi, ognuno con la sua banda.</para>
/// </summary>
public sealed class EfSectorVolumeCatalog : ISectorVolumeCatalog
{
    private readonly VipiDbContext _db;
    private readonly ISectorShapeResolver _forme;

    public EfSectorVolumeCatalog(VipiDbContext db, ISectorShapeResolver forme)
    {
        _db = db;
        _forme = forme;
    }

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

        // Una domanda sola per tutti i settori attivi: la porta unica sa già la precedenza fra le fonti.
        var forme = await _forme.ResolveAsync(settori.Select(s => s.Callsign).ToList(), ct);

        return settori.Select(s =>
        {
            var forma = forme.GetValueOrDefault(s.Callsign);
            return new SectorVolumeRow(
                Callsign: s.Callsign,
                ParentCallsign: s.ParentSectorId is { } pid && callsignPerId.TryGetValue(pid, out var padre) ? padre : null,
                Type: s.Type,
                AirportIcao: s.AirportIcao,
                Parts: forma?.Parts ?? Array.Empty<Vipi.Application.Airspace.ShapePart>(),
                Source: forma?.Source ?? ShapeSource.Source);
        }).ToList();
    }
}
