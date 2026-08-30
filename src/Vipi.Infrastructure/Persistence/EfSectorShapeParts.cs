using Microsoft.EntityFrameworkCore;
using Vipi.Application.Airspace;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="ISectorShapeParts"/>
public sealed class EfSectorShapeParts : ISectorShapeParts
{
    private readonly VipiDbContext _db;

    public EfSectorShapeParts(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<ShapePart>> ListAsync(
        SourceCatalog catalog, int sectorId, ShapeSource source, ShapePartState state,
        CancellationToken ct = default)
    {
        var righe = await _db.SectorShapeParts.AsNoTracking()
            .Where(x => x.Catalog == catalog && x.SectorId == sectorId && x.Source == source && x.State == state)
            .OrderBy(x => x.Ordinal)
            .ToListAsync(ct);

        return righe.Select(Pezzo).ToList();
    }

    public async Task<ShapePartsWriteResult> ReplacePartsAsync(
        SourceCatalog catalog, int sectorId, string callsign, ShapeSource source, ShapePartState state,
        IReadOnlyList<ShapePart> parts, string? airacCycle = null, bool forcePublished = false,
        CancellationToken ct = default)
    {
        // ⚠️ L'ASSENZA NON CANCELLA. Una sorgente che non risponde, o che risponde `[]`, non deve portare via
        // quel che c'era: è la lezione del 26 agosto 2026 (83 poligoni azzerati da un `[]`). Svuotare è
        // ClearPartsAsync, che è un gesto che qualcuno ha chiesto.
        if (parts.Count == 0) return ShapePartsWriteResult.Silent;

        // ⚠️ LA REGOLA D'ORO, e sta qui: il filtro porta SEMPRE la fonte e lo stato. Non c'è nessun percorso
        // in questo file che cancelli i pezzi di un settore senza dire di chi sono — ed è per questo che lo
        // sgancio dall'AIP non ha niente da ri-importare: i pezzi di IVAO non sono mai stati toccati.
        var vecchi = await _db.SectorShapeParts
            .Where(x => x.Catalog == catalog && x.SectorId == sectorId && x.Source == source && x.State == state)
            .ToListAsync(ct);
        _db.SectorShapeParts.RemoveRange(vecchi);

        var cs = (callsign ?? "").Trim().ToUpperInvariant();
        var ora = DateTime.UtcNow;
        // Il ciclo AIRAC vale solo su un insieme in attesa: su quello in vigore il ciclo è già arrivato, e
        // lasciarcelo scritto lo farebbe promuovere una seconda volta.
        var ciclo = state == ShapePartState.Pending ? airacCycle : null;

        var ordinale = 0;
        foreach (var p in parts)
            _db.SectorShapeParts.Add(new SectorShapePart
            {
                Catalog = catalog,
                SectorId = sectorId,
                Callsign = cs,
                Source = source,
                State = state,
                Ordinal = ordinale++,
                PolygonJson = p.PolygonJson,
                BaseFeet = p.BaseFeet,
                TopFeet = p.TopFeet,
                BaseDatum = p.BaseDatum,
                TopDatum = p.TopDatum,
                BaseRaw = p.BaseRaw ?? "",
                TopRaw = p.TopRaw ?? "",
                AiracCycle = ciclo,
                ForcePublished = forcePublished,
                SourceRef = p.SourceRef,
                WrittenUtc = ora,
            });

        await _db.SaveChangesAsync(ct);
        return new ShapePartsWriteResult(parts.Count, false);
    }

    public async Task<int> ClearPartsAsync(
        SourceCatalog catalog, int sectorId, ShapeSource source, ShapePartState? state = null,
        CancellationToken ct = default)
    {
        // Anche qui la fonte è obbligatoria: svuotare «tutti i pezzi di un settore» non è un gesto che questa
        // porta sappia fare, ed è voluto.
        var righe = await _db.SectorShapeParts
            .Where(x => x.Catalog == catalog && x.SectorId == sectorId && x.Source == source
                        && (state == null || x.State == state))
            .ToListAsync(ct);

        if (righe.Count == 0) return 0;

        _db.SectorShapeParts.RemoveRange(righe);
        await _db.SaveChangesAsync(ct);
        return righe.Count;
    }

    private static ShapePart Pezzo(SectorShapePart x) => new(
        x.PolygonJson, x.BaseFeet, x.TopFeet, x.BaseDatum, x.TopDatum, x.BaseRaw, x.TopRaw, x.SourceRef);
}
