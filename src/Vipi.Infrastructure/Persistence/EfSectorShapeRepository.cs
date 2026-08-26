using Microsoft.EntityFrameworkCore;
using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Services;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="ISectorShapeRepository"/>
public sealed class EfSectorShapeRepository : ISectorShapeRepository
{
    private readonly VipiDbContext _db;
    private readonly IAiracService _airac;

    public EfSectorShapeRepository(VipiDbContext db, IAiracService airac)
    {
        _db = db;
        _airac = airac;
    }

    /// <summary>Le posizioni che hanno un volume e quindi un'area, TWR esclusa (ha il suo ripiego).</summary>
    private static readonly string[] PosizioniConArea = { "CTR", "FSS", "APP", "DEP" };

    public async Task<IReadOnlyList<SectorShapeRow>> ListShapeCandidatesAsync(CancellationToken ct = default)
    {
        // ⚠️ Le righe NASCOSTE restano dentro: nascosto vuol dire «fuori dalla navigazione pubblica», non
        // «senza area» — e un settore che l'admin riaccende domani deve trovarsela già lì, non aspettare
        // un altro giro.
        var acc = await _db.AccSectors.AsNoTracking()
            .Where(x => x.Position != null && PosizioniConArea.Contains(x.Position))
            .Select(x => new Grezza(SourceCatalog.Subcenter, x.Id, x.ComposePosition, x.Position,
                x.RegionMapPolygon, x.RegionMapPolygonInForce, x.ShapeAiracCycle, x.ShapeSource, x.ShapeForcePublished))
            .ToListAsync(ct);

        var apt = await _db.AirportSectors.AsNoTracking()
            .Where(x => x.Position != null && PosizioniConArea.Contains(x.Position))
            .Select(x => new Grezza(SourceCatalog.AirportPosition, x.Id, x.ComposePosition, x.Position,
                x.RegionMapPolygon, x.RegionMapPolygonInForce, x.ShapeAiracCycle, x.ShapeSource, x.ShapeForcePublished))
            .ToListAsync(ct);

        // La proiezione si fa in memoria: AorPolygonProjector parsa del JSON, cosa che nessun provider sa
        // tradurre in SQL. Le righe sono qualche centinaio.
        return acc.Concat(apt).Select(Riga).ToList();
    }

    private sealed record Grezza(
        SourceCatalog Catalog, int Id, string Callsign, string? Position,
        string? Polygon, string? InForce, string? Cycle, ShapeSource Source, bool Force);

    private static SectorShapeRow Riga(Grezza g) => new(
        g.Catalog, g.Id, g.Callsign, g.Position,
        AorPolygonProjector.Project(g.Polygon) is not null,
        new ShapeState(g.Polygon, g.InForce, g.Cycle, g.Source, g.Force));

    public async Task ApplyShapeAsync(ShapeWrite w, CancellationToken ct = default)
    {
        if (w.Catalog == SourceCatalog.Subcenter)
        {
            var riga = await _db.AccSectors.FirstOrDefaultAsync(x => x.Id == w.Id, ct);
            if (riga is null) return;
            riga.RegionMapPolygon = w.PolygonJson;
            riga.RegionMapPolygonInForce = w.InForce;
            riga.ShapeAiracCycle = w.FromCycle;
            riga.ShapeSource = ShapeSource.Sectorfile;
            // ⚠️ La forzatura si azzera su una geometria NUOVA: valeva per quella di prima, e lasciarla
            // accesa pubblicherebbe in anticipo la prossima senza che nessuno l'abbia chiesto.
            riga.ShapeForcePublished = false;
        }
        else
        {
            var riga = await _db.AirportSectors.FirstOrDefaultAsync(x => x.Id == w.Id, ct);
            if (riga is null) return;
            riga.RegionMapPolygon = w.PolygonJson;
            riga.RegionMapPolygonInForce = w.InForce;
            riga.ShapeAiracCycle = w.FromCycle;
            riga.ShapeSource = ShapeSource.Sectorfile;
            riga.ShapeForcePublished = false;
            // ⚠️ NON è una shape sintetica: è un poligono vero, disegnato da chi fa il sectorfile. Il flag
            // dice «cerchio di ripiego», e metterlo qui farebbe credere ai ripieghi TWR di poterla sostituire.
            riga.IsShapeSynthetic = false;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> PromoteDueShapesAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var accs = await _db.AccSectors.Where(x => x.ShapeAiracCycle != null).ToListAsync(ct);
        var apts = await _db.AirportSectors.Where(x => x.ShapeAiracCycle != null).ToListAsync(ct);

        var promossi = 0;
        foreach (var x in accs)
            if (Maturato(x.ShapeAiracCycle, x.ShapeSource, x.ShapeForcePublished, nowUtc))
            {
                x.ShapeAiracCycle = null;
                x.RegionMapPolygonInForce = null;   // la corrente È quella in vigore: la vecchia non serve più
                x.ShapeForcePublished = false;
                promossi++;
            }
        foreach (var x in apts)
            if (Maturato(x.ShapeAiracCycle, x.ShapeSource, x.ShapeForcePublished, nowUtc))
            {
                x.ShapeAiracCycle = null;
                x.RegionMapPolygonInForce = null;
                x.ShapeForcePublished = false;
                promossi++;
            }

        if (promossi > 0) await _db.SaveChangesAsync(ct);
        return promossi;
    }

    private bool Maturato(string? ciclo, ShapeSource source, bool force, DateTime nowUtc) =>
        ShapeAiracGate.IsPromotable(new ShapeState(null, null, ciclo, source, force), nowUtc, _airac);
}
