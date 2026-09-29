using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Services;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// La struttura dei documenti collegati (§A109): l'albero EFFETTIVO, dalle stesse righe della pagina Struttura
/// (<see cref="EffectiveHierarchyRows"/>), più quel che serve a leggerlo — chi è un settore ACC, quali posizioni ha
/// ogni scalo, quali APP sono remotizzati e che documento hanno.
/// <para>Cinque query su tabelle piccole (al 21 settembre 2026: 155 settori ACC, 193 posizioni, 93 scali, 323
/// settori d'anagrafica).</para>
/// </summary>
internal sealed class EfDocLinkStructureSource : IDocLinkStructureSource
{
    private readonly VipiDbContext _db;
    public EfDocLinkStructureSource(VipiDbContext db) => _db = db;

    public async Task<DocLinkStructure> LoadAsync(CancellationToken ct = default)
    {
        var (righe, padreScalo) = await EffectiveHierarchyRows.LoadAsync(_db, null, ct);
        var padri = EffectiveHierarchy.ParentMap(righe, padreScalo);

        var centri = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in await _db.AccSectors.AsNoTracking()
                     .Select(s => new { s.ComposePosition, s.CenterId }).ToListAsync(ct))
            centri.TryAdd(s.ComposePosition, s.CenterId);

        // Le posizioni da cui parte la risalita: quelle che l'albero conosce (ATIS escluso) e visibili — una
        // nascosta non è un padre possibile nemmeno per la scaletta.
        var posizioni = righe.Where(r => r.AirportIcao is not null && !r.IsHidden)
            .GroupBy(r => r.AirportIcao!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(r => r.Callsign).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var scali = (await _db.Airports.AsNoTracking()
                .Select(a => new { a.Icao, Acc = a.Acc!.Code, a.IsHidden, a.ParentCallsign }).ToListAsync(ct))
            .Select(a => new DocLinkAirport(a.Icao, a.Acc, a.IsHidden, a.ParentCallsign,
                posizioni.GetValueOrDefault(a.Icao) ?? Array.Empty<string>()))
            .ToList();

        // Gli APP d'anagrafica. ⚠️ Un APP disattivato non ha pagina pubblica (EfContentRepository.LoadAppAsync
        // vuole IsActive): come candidato porterebbe a «documento non disponibile».
        // 🔴 Il documento lo dice l'ENTE (S49), non il settore: per il suo codice e per ognuna delle sue posizioni,
        // di qualunque tipo (LIRE_TWR) e qualunque cosa dica IVAO dell'APP. La sua pagina non chiude quando una
        // posizione sparisce, quindi l'ente entra anche senza settori attivi.
        var dellEnte = new Dictionary<string, (int? Doc, string Acc)>(StringComparer.OrdinalIgnoreCase);
        foreach (var u in await _db.AtcUnits.AsNoTracking()
                     .Where(u => u.Mode == AtcUnitMode.OwnDocument)
                     .Select(u => new { u.Code, u.DocumentId, Acc = u.Acc!.Code, Pos = u.Positions.Select(p => p.Callsign).ToList() })
                     .ToListAsync(ct))
            foreach (var cs in u.Pos.Prepend(u.Code))
                dellEnte.TryAdd(cs, (u.DocumentId, u.Acc));

        var app = new Dictionary<string, DocLinkApp>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in await _db.Sectors.AsNoTracking()
                     .Where(s => s.Type == SectorType.App && s.IsActive)
                     .Select(s => new { s.Callsign, s.ApproachKind, Acc = s.Acc!.Code })
                     .ToListAsync(ct))
            app.TryAdd(s.Callsign, dellEnte.TryGetValue(s.Callsign, out var e)
                ? new DocLinkApp(s.Callsign, false, e.Doc, s.Acc)
                : new DocLinkApp(s.Callsign, s.ApproachKind == ApproachKind.Remotized, null, s.Acc));
        foreach (var (cs, e) in dellEnte)
            app.TryAdd(cs, new DocLinkApp(cs, false, e.Doc, e.Acc));

        return new DocLinkStructure(padri, centri, scali, app);
    }
}
