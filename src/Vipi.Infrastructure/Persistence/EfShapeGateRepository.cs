using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Il perimetro di shape di un documento: <b>quali settori quel documento può disegnare</b>.
///
/// <para>⚠️ È volutamente il perimetro dell'<b>ente</b> (la ACC, o l'aeroporto), non l'elenco esatto dei
/// settori scelti nelle configurazioni AoR. Ricavare quell'elenco vorrebbe dire rieseguire la derivazione
/// del documento — cioè il congelamento — solo per decidere se mostrare un avviso. Il costo dell'imprecisione
/// è dalla parte giusta: si può avvisare per un settore che quella mappa non disegna, mai tacere per uno che
/// disegna davvero.</para>
/// </summary>
public sealed class EfShapeGateRepository : IShapeGateRepository
{
    private readonly VipiDbContext _db;

    public EfShapeGateRepository(VipiDbContext db) => _db = db;

    public async Task<ShapeGateScope> GetScopeAsync(
        ReleaseTargetType target, string key, CancellationToken ct = default)
    {
        key = (key ?? "").Trim();
        if (key.Length == 0) return ShapeGateScope.Empty;

        switch (target)
        {
            case ReleaseTargetType.AccVipi:
                // Chiave «{ACC}|{radice}»: la ACC è la prima metà.
                return await PerAccAsync(key.Split('|')[0].ToUpperInvariant(), ct);

            case ReleaseTargetType.Vloa:
            {
                // Chiave = id del documento. Le sue parti dicono le ACC in gioco; quella estera non ha mai
                // shape differite (il ripiego non tocca gli esteri), ma chiederla non costa e non mente.
                if (!int.TryParse(key, out var docId)) return ShapeGateScope.Empty;
                var accs = await _db.DocumentParties.AsNoTracking()
                    .Where(p => p.DocumentId == docId && p.Sector!.Acc != null)
                    .Select(p => p.Sector!.Acc!.Code)
                    .Distinct().ToListAsync(ct);
                var righe = new List<ShapeGateRow>();
                foreach (var acc in accs) righe.AddRange((await PerAccAsync(acc, ct)).Rows);
                return new ShapeGateScope(null, docId, Dedup(righe));
            }

            case ReleaseTargetType.Airport:
                return await PerAeroportoAsync(key.ToUpperInvariant(), ct);

            case ReleaseTargetType.App:
                // Chiave = callsign dell'APP (es. «LIRA_APP»): l'aeroporto sono le prime quattro lettere.
                return key.Length < 4
                    ? ShapeGateScope.Empty
                    : await PerAeroportoAsync(key.Substring(0, 4).ToUpperInvariant(), ct);

            default:
                return ShapeGateScope.Empty;
        }
    }

    private async Task<ShapeGateScope> PerAccAsync(string accCode, CancellationToken ct)
    {
        if (accCode.Length == 0) return ShapeGateScope.Empty;

        var subcenter = await _db.AccSectors.AsNoTracking()
            .Where(x => x.CenterId == accCode)
            .Select(x => new Grezza(SourceCatalog.Subcenter, x.Id, x.ComposePosition,
                x.RegionMapPolygon, x.RegionMapPolygonInForce, x.ShapeAiracCycle, x.ShapeSource, x.ShapeForcePublished))
            .ToListAsync(ct);

        var aeroporti = await _db.AirportSectors.AsNoTracking()
            .Where(x => x.AccCode == accCode)
            .Select(x => new Grezza(SourceCatalog.AirportPosition, x.Id, x.ComposePosition,
                x.RegionMapPolygon, x.RegionMapPolygonInForce, x.ShapeAiracCycle, x.ShapeSource, x.ShapeForcePublished))
            .ToListAsync(ct);

        return new ShapeGateScope(accCode, null, await ConNomiAsync(subcenter.Concat(aeroporti).ToList(), ct));
    }

    private async Task<ShapeGateScope> PerAeroportoAsync(string icao, CancellationToken ct)
    {
        var righe = await _db.AirportSectors.AsNoTracking()
            .Where(x => x.AirportIcao == icao)
            .Select(x => new Grezza(SourceCatalog.AirportPosition, x.Id, x.ComposePosition,
                x.RegionMapPolygon, x.RegionMapPolygonInForce, x.ShapeAiracCycle, x.ShapeSource, x.ShapeForcePublished))
            .ToListAsync(ct);

        // La ACC che governa l'aeroporto: è lei a dire chi può forzare (il permesso è ACC-scoped).
        var acc = await _db.AirportSectors.AsNoTracking()
            .Where(x => x.AirportIcao == icao && x.AccCode != null)
            .Select(x => x.AccCode!).FirstOrDefaultAsync(ct);

        return new ShapeGateScope(acc, null, await ConNomiAsync(righe, ct));
    }

    /// <summary>Il nome leggibile arriva dalla proiezione <c>Sector</c>: i cataloghi non ce l'hanno.</summary>
    private async Task<IReadOnlyList<ShapeGateRow>> ConNomiAsync(IReadOnlyList<Grezza> righe, CancellationToken ct)
    {
        if (righe.Count == 0) return Array.Empty<ShapeGateRow>();
        var callsigns = righe.Select(r => r.Callsign).Distinct().ToList();
        var nomi = await _db.Sectors.AsNoTracking()
            .Where(s => callsigns.Contains(s.Callsign))
            .Select(s => new { s.Callsign, s.Name })
            .ToDictionaryAsync(s => s.Callsign, s => s.Name, StringComparer.OrdinalIgnoreCase, ct);

        return righe.Select(g => new ShapeGateRow(
            g.Catalog, g.Id, g.Callsign,
            nomi.TryGetValue(g.Callsign, out var n) ? n : null,
            new ShapeState(g.Polygon, g.InForce, g.Cycle, g.Source, g.Force))).ToList();
    }

    /// <summary>Una vLOA ha due parti che possono stare sulla stessa ACC: la riga non si conta due volte.</summary>
    private static IReadOnlyList<ShapeGateRow> Dedup(IEnumerable<ShapeGateRow> righe) =>
        righe.GroupBy(r => (r.Catalog, r.Id)).Select(g => g.First()).ToList();

    private sealed record Grezza(
        SourceCatalog Catalog, int Id, string Callsign,
        string? Polygon, string? InForce, string? Cycle, ShapeSource Source, bool Force);

    public async Task<int> SetForcePublishedAsync(
        IReadOnlyList<(SourceCatalog Catalog, int Id)> rows, CancellationToken ct = default)
    {
        if (rows.Count == 0) return 0;

        var accIds = rows.Where(r => r.Catalog == SourceCatalog.Subcenter).Select(r => r.Id).ToList();
        var aptIds = rows.Where(r => r.Catalog == SourceCatalog.AirportPosition).Select(r => r.Id).ToList();

        var toccate = 0;
        foreach (var x in await _db.AccSectors.Where(x => accIds.Contains(x.Id)).ToListAsync(ct))
        {
            x.ShapeForcePublished = true;
            toccate++;
        }
        foreach (var x in await _db.AirportSectors.Where(x => aptIds.Contains(x.Id)).ToListAsync(ct))
        {
            x.ShapeForcePublished = true;
            toccate++;
        }

        // ⚠️ Non si tocca ShapeAiracCycle: il differimento resta scritto. La forzatura dice «pubblicala lo
        // stesso», non «è in vigore» — e quando il ciclo arriva davvero, la promozione chiude la pratica e
        // spegne la forzatura da sé (PromoteDueShapesAsync).
        if (toccate > 0) await _db.SaveChangesAsync(ct);
        return toccate;
    }
}
