using Microsoft.EntityFrameworkCore;
using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="ISectorShapeRepository"/>
public sealed class EfSectorShapeRepository : ISectorShapeRepository
{
    private readonly VipiDbContext _db;
    public EfSectorShapeRepository(VipiDbContext db) => _db = db;

    /// <summary>Le posizioni che hanno un volume e quindi un'area, TWR esclusa (ha il suo ripiego).</summary>
    private static readonly string[] PosizioniConArea = { "CTR", "FSS", "APP", "DEP" };

    public async Task<IReadOnlyList<SectorShapeRow>> ListShapeCandidatesAsync(CancellationToken ct = default)
    {
        // ⚠️ Le righe NASCOSTE restano dentro: nascosto vuol dire «fuori dalla navigazione pubblica», non
        // «senza area» — e un settore che l'admin riaccende domani deve trovarsela già lì, non aspettare
        // un altro giro.
        var acc = await _db.AccSectors.AsNoTracking()
            .Where(x => x.Position != null && PosizioniConArea.Contains(x.Position))
            .Select(x => new { x.Id, x.ComposePosition, x.Position, x.RegionMapPolygon })
            .ToListAsync(ct);

        var apt = await _db.AirportSectors.AsNoTracking()
            .Where(x => x.Position != null && PosizioniConArea.Contains(x.Position))
            .Select(x => new { x.Id, x.ComposePosition, x.Position, x.RegionMapPolygon })
            .ToListAsync(ct);

        // La proiezione si fa in memoria: AorPolygonProjector parsa del JSON, cosa che nessun provider sa
        // tradurre in SQL. Le righe sono qualche centinaio.
        return acc
            .Select(x => new SectorShapeRow(SourceCatalog.Subcenter, x.Id, x.ComposePosition, x.Position,
                AorPolygonProjector.Project(x.RegionMapPolygon) is not null))
            .Concat(apt.Select(x => new SectorShapeRow(SourceCatalog.AirportPosition, x.Id, x.ComposePosition, x.Position,
                AorPolygonProjector.Project(x.RegionMapPolygon) is not null)))
            .ToList();
    }

    public async Task SetShapeAsync(
        SourceCatalog catalog, int id, string polygonJson, CancellationToken ct = default)
    {
        if (catalog == SourceCatalog.Subcenter)
        {
            var riga = await _db.AccSectors.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (riga is null) return;
            riga.RegionMapPolygon = polygonJson;
        }
        else
        {
            var riga = await _db.AirportSectors.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (riga is null) return;
            riga.RegionMapPolygon = polygonJson;
            // ⚠️ NON è una shape sintetica: è un poligono vero, disegnato da chi fa il sectorfile. Il flag
            // dice «cerchio di ripiego», e metterlo qui farebbe credere ai ripieghi TWR di poterla sostituire.
            riga.IsShapeSynthetic = false;
        }
        await _db.SaveChangesAsync(ct);
    }
}
