using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Primitive EF di derivazione live della vIPI ACC (settori, topologia, frequenze): sola lettura dai cataloghi.
/// Lo storage editoriale vive nel <c>Document</c> (doc 08e/08i); qui nessuna tabella profile (droppata in 08i).
/// </summary>
public sealed class EfAccDerivationRepository : IAccDerivationRepository
{
    private readonly VipiDbContext _db;
    public EfAccDerivationRepository(VipiDbContext db) => _db = db;

    public async Task<AccDocumentIdentity?> ResolveAccDocumentIdentityAsync(string accCode, CancellationToken ct = default)
    {
        var accName = await _db.Accs.AsNoTracking().Where(a => a.Code == accCode).Select(a => a.Name).FirstOrDefaultAsync(ct);
        if (accName is null) return null;
        // Settore CTR radice primario (stesso criterio di ResolveRootAsync): chiavizza il Document ACC-wide.
        var root = await _db.Sectors.AsNoTracking()
            .Where(s => s.Acc!.Code == accCode && s.Type == SectorType.Ctr && s.ParentSectorId == null && s.IsActive)
            .OrderBy(s => s.CoverageOrder).ThenBy(s => s.Callsign)
            .Select(s => new { s.Id, s.Callsign, s.DocumentId, DocumentHidden = s.Document != null && s.Document.IsHidden })
            .FirstOrDefaultAsync(ct);
        if (root is null) return null;
        return new AccDocumentIdentity(root.Id, root.Callsign, accCode, accName, root.DocumentId, root.DocumentHidden);
    }

    public async Task<IReadOnlyList<AccTreeRoot>> ListTreeRootsAsync(string accCode, CancellationToken ct = default) =>
        await _db.Sectors.AsNoTracking()
            .Where(s => s.Acc!.Code == accCode && s.Type == SectorType.Ctr && s.ParentSectorId == null && s.IsActive)
            .OrderBy(s => s.CoverageOrder).ThenBy(s => s.Callsign)
            .Select(s => new AccTreeRoot(s.Callsign, s.Name))
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<string, (string Name, int Order)>> GetCtrBranchMapAsync(string accCode, string rootCallsign, CancellationToken ct = default)
    {
        var res = new Dictionary<string, (string, int)>(StringComparer.OrdinalIgnoreCase);
        var all = await _db.Sectors.AsNoTracking()
            .Where(s => s.Acc!.Code == accCode && s.Type == SectorType.Ctr && s.IsActive)
            .Select(s => new { s.Id, s.Callsign, s.Name, s.ParentSectorId }).ToListAsync(ct);

        var root = all.FirstOrDefault(s => string.Equals(s.Callsign, rootCallsign, StringComparison.OrdinalIgnoreCase));
        if (root is null) return res;
        var byId = all.ToDictionary(s => s.Id);

        // Rami = radice (ordine 0) + suoi figli diretti (ordine 1..N per callsign).
        var directChildren = all.Where(s => s.ParentSectorId == root.Id).OrderBy(s => s.Callsign).ToList();
        var branchName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [root.Callsign] = root.Name };
        var branchOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [root.Callsign] = 0 };
        for (var i = 0; i < directChildren.Count; i++)
        {
            branchName[directChildren[i].Callsign] = directChildren[i].Name;
            branchOrder[directChildren[i].Callsign] = i + 1;
        }

        foreach (var node in all)
        {
            // Sali finché il genitore è la radice (→ questo nodo è il ramo) o il nodo è la radice.
            var cur = node;
            string? branch = null;
            while (true)
            {
                if (cur.Id == root.Id) { branch = root.Callsign; break; }
                if (cur.ParentSectorId is int pid && byId.TryGetValue(pid, out var par))
                {
                    if (par.Id == root.Id) { branch = cur.Callsign; break; }
                    cur = par;
                }
                else break;   // radice diversa / fuori dal sottoalbero
            }
            if (branch is null) continue;
            res[node.Callsign] = (branchName.GetValueOrDefault(branch, branch), branchOrder.GetValueOrDefault(branch, 99));
        }
        return res;
    }

    public async Task<IReadOnlyList<AccSectorPick>> ListCtrSectorsAsync(string accCode, CancellationToken ct = default) =>
        await _db.Sectors.AsNoTracking()
            .Where(s => s.Acc!.Code == accCode && s.Type == SectorType.Ctr && s.IsActive)
            .OrderBy(s => s.Callsign)
            .Select(s => new AccSectorPick(s.Callsign, s.Name))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AccSectorPick>> ListAppSectorsAsync(string accCode, CancellationToken ct = default) =>
        await _db.Sectors.AsNoTracking()
            .Where(s => s.Acc!.Code == accCode && s.Type == SectorType.App && s.IsActive)
            .OrderBy(s => s.Callsign)
            .Select(s => new AccSectorPick(s.Callsign, s.Name))
            .ToListAsync(ct);

    public Task<IReadOnlyDictionary<string, string>> GetSectorPolygonsRawByCallsignAsync(IReadOnlyList<string> callsigns, CancellationToken ct = default) =>
        SectorPolygonsRawByCallsignAsync(_db, callsigns, ct);

    public Task<IReadOnlyDictionary<string, SectorFlLimits>> GetSectorLimitsByCallsignAsync(IReadOnlyList<string> callsigns, CancellationToken ct = default) =>
        SectorLimitsByCallsignAsync(_db, callsigns, ct);

    public Task<IReadOnlyList<SectorShapePick>> ListSelectableSectorShapesAsync(CancellationToken ct = default) =>
        SelectableSectorShapesAsync(_db, ct);

    // Helper statici condivisi con EfAppDerivationRepository (stessa semantica: CTR AccSector + APP/TWR AirportSector).

    internal static async Task<IReadOnlyDictionary<string, string>> SectorPolygonsRawByCallsignAsync(
        VipiDbContext db, IReadOnlyList<string> callsigns, CancellationToken ct)
    {
        var res = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (callsigns.Count == 0) return res;
        var set = callsigns.ToList();

        var ctr = await db.AccSectors.AsNoTracking()
            .Where(s => set.Contains(s.ComposePosition) && s.RegionMapPolygon != null && s.RegionMapPolygon != "")
            .Select(s => new { s.ComposePosition, s.RegionMapPolygon }).ToListAsync(ct);
        foreach (var r in ctr) res[r.ComposePosition] = r.RegionMapPolygon!;

        var app = await db.AirportSectors.AsNoTracking()
            .Where(s => set.Contains(s.ComposePosition) && s.RegionMapPolygon != null && s.RegionMapPolygon != "")
            .Select(s => new { s.ComposePosition, s.RegionMapPolygon }).ToListAsync(ct);
        foreach (var r in app) res.TryAdd(r.ComposePosition, r.RegionMapPolygon!);

        return res;
    }

    internal static async Task<IReadOnlyDictionary<string, SectorFlLimits>> SectorLimitsByCallsignAsync(
        VipiDbContext db, IReadOnlyList<string> callsigns, CancellationToken ct)
    {
        var res = new Dictionary<string, SectorFlLimits>(StringComparer.OrdinalIgnoreCase);
        if (callsigns.Count == 0) return res;
        var set = callsigns.ToList();

        var ctr = await db.AccSectors.AsNoTracking()
            .Where(s => set.Contains(s.ComposePosition))
            .Select(s => new { s.ComposePosition, s.LowerLimit, s.UpperLimit }).ToListAsync(ct);
        foreach (var r in ctr) res[r.ComposePosition] = new SectorFlLimits(r.LowerLimit, r.UpperLimit);

        var app = await db.AirportSectors.AsNoTracking()
            .Where(s => set.Contains(s.ComposePosition))
            .Select(s => new { s.ComposePosition, s.LowerLimit, s.UpperLimit }).ToListAsync(ct);
        foreach (var r in app) res.TryAdd(r.ComposePosition, new SectorFlLimits(r.LowerLimit, r.UpperLimit));

        return res;
    }

    // Picker globale delle shape extra: ogni settore DB con poligono (CTR di aerovia + APP/torri d'aeroporto), non nascosto.
    // Nome = AtcCallsign IVAO se presente, altrimenti il callsign. ACC = CenterId (CTR) / AccCode (aeroporto), per cercare l'ente.
    internal static async Task<IReadOnlyList<SectorShapePick>> SelectableSectorShapesAsync(VipiDbContext db, CancellationToken ct)
    {
        var ctr = await db.AccSectors.AsNoTracking()
            .Where(s => !s.IsHidden && s.RegionMapPolygon != null && s.RegionMapPolygon != "")
            .Select(s => new { s.ComposePosition, s.AtcCallsign, Acc = s.CenterId }).ToListAsync(ct);
        var app = await db.AirportSectors.AsNoTracking()
            .Where(s => !s.IsHidden && s.RegionMapPolygon != null && s.RegionMapPolygon != "")
            .Select(s => new { s.ComposePosition, s.AtcCallsign, Acc = s.AccCode }).ToListAsync(ct);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var res = new List<SectorShapePick>();
        foreach (var r in ctr.Concat(app))
        {
            if (!seen.Add(r.ComposePosition)) continue;
            var name = string.IsNullOrWhiteSpace(r.AtcCallsign) ? r.ComposePosition : r.AtcCallsign!;
            res.Add(new SectorShapePick(r.ComposePosition, name, r.Acc));
        }
        return res.OrderBy(r => r.AccCode).ThenBy(r => r.Callsign, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<IReadOnlyList<AppFreqRow>> DeriveFrequenciesForMembersAsync(
        IReadOnlyList<string> memberCallsigns, IReadOnlyList<string> linkCallsigns, CancellationToken ct = default)
    {
        var rows = new List<AppFreqRow>();
        if (memberCallsigns.Count > 0)
        {
            var members = memberCallsigns.ToList();

            // Settori membri con frequenza propria (CTR e APP): dal Sector operativo.
            var sectors = await _db.Sectors.AsNoTracking()
                .Where(s => members.Contains(s.Callsign) && s.DefaultFrequency != null)
                .Select(s => new { s.Callsign, s.Type, s.DefaultFrequency }).ToListAsync(ct);
            foreach (var s in sectors)
            {
                var pos = PositionFromType(s.Type);
                rows.Add(new AppFreqRow(null, FreqNameForPosition(pos), s.Callsign, s.DefaultFrequency!, pos, false, false));
            }

            // Espansione APP: catalogo aeroporto (ATIS·DEL·GND·TWR·APP con frequenza) degli aeroporti degli APP membri.
            var appIcaos = await _db.AirportSectors.AsNoTracking()
                .Where(s => members.Contains(s.ComposePosition))
                .Select(s => s.AirportIcao).Distinct().ToListAsync(ct);
            if (appIcaos.Count > 0)
            {
                var cat = await _db.AirportSectors.AsNoTracking()
                    .Where(s => appIcaos.Contains(s.AirportIcao) && !s.IsHidden && s.Frequency != null)
                    .Select(s => new { s.ComposePosition, s.Position, s.Frequency }).ToListAsync(ct);
                foreach (var s in cat)
                    rows.Add(new AppFreqRow(null, FreqNameForPosition(s.Position), s.ComposePosition, s.Frequency!,
                        (s.Position ?? "").Trim().ToUpperInvariant(),
                        members.Contains(s.ComposePosition, StringComparer.OrdinalIgnoreCase), false));
            }
        }

        // Link extra (riferimento vivo a un altro settore).
        if (linkCallsigns.Count > 0)
        {
            var links = linkCallsigns.ToList();
            var ls = await _db.Sectors.AsNoTracking()
                .Where(s => links.Contains(s.Callsign) && s.DefaultFrequency != null)
                .Select(s => new { s.Callsign, s.Type, s.DefaultFrequency }).ToListAsync(ct);
            foreach (var s in ls)
            {
                var pos = PositionFromType(s.Type);
                rows.Add(new AppFreqRow(null, FreqNameForPosition(pos), s.Callsign, s.DefaultFrequency!, pos, false, true));
            }
        }

        // Nome visualizzato reale (IVAO atcCallsign, es. "Roma Radar"): sovrascrive il nome generico dove disponibile.
        var csAll = rows.Select(r => r.Callsign).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (csAll.Count > 0)
        {
            var nameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in await _db.AccSectors.AsNoTracking()
                .Where(a => csAll.Contains(a.ComposePosition) && a.AtcCallsign != null && a.AtcCallsign != "")
                .Select(a => new { a.ComposePosition, a.AtcCallsign }).ToListAsync(ct))
                nameMap[a.ComposePosition] = a.AtcCallsign!;
            foreach (var a in await _db.AirportSectors.AsNoTracking()
                .Where(a => csAll.Contains(a.ComposePosition) && a.AtcCallsign != null && a.AtcCallsign != "")
                .Select(a => new { a.ComposePosition, a.AtcCallsign }).ToListAsync(ct))
                nameMap.TryAdd(a.ComposePosition, a.AtcCallsign!);
            if (nameMap.Count > 0)
                rows = rows.Select(r => nameMap.TryGetValue(r.Callsign, out var n) ? r with { Name = n } : r).ToList();
        }

        // Dedup per callsign (preferisci la riga membro/primaria) e ordina ATIS·DEL·GND·TWR·APP·CTR.
        return rows
            .GroupBy(r => r.Callsign, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(r => r.IsPrimary).ThenBy(r => r.IsLink).First())
            .OrderBy(r => PositionOrder(r.Position))
            .ThenByDescending(r => r.IsPrimary)
            .ThenBy(r => r.Callsign, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyDictionary<string, SectorType>> GetSectorTypeMapAsync(CancellationToken ct = default)
    {
        var rows = await _db.Sectors.AsNoTracking().Select(s => new { s.Callsign, s.Type }).ToListAsync(ct);
        var map = new Dictionary<string, SectorType>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows) map[r.Callsign] = r.Type;
        return map;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetSectorCodeMapAsync(CancellationToken ct = default)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in await _db.AccSectors.AsNoTracking()
                     .Where(s => s.MiddleIdentifier != null && s.MiddleIdentifier != "")
                     .Select(s => new { s.ComposePosition, s.MiddleIdentifier }).ToListAsync(ct))
            map[r.ComposePosition] = r.MiddleIdentifier!;
        foreach (var r in await _db.AirportSectors.AsNoTracking()
                     .Where(s => s.MiddleIdentifier != null && s.MiddleIdentifier != "")
                     .Select(s => new { s.ComposePosition, s.MiddleIdentifier }).ToListAsync(ct))
            map.TryAdd(r.ComposePosition, r.MiddleIdentifier!);
        return map;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAirportNameMapAsync(CancellationToken ct = default)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in await _db.Airports.AsNoTracking().Select(a => new { a.Icao, a.Name }).ToListAsync(ct))
            map[a.Icao] = a.Name;
        return map;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetSectorAtcNameMapAsync(CancellationToken ct = default) =>
        await EfAccDerivationRepository.BuildAtcNameMapAsync(_db, ct);

    public async Task<IReadOnlyDictionary<string, AccRef>> GetSectorAccRefMapAsync(CancellationToken ct = default)
    {
        var map = new Dictionary<string, AccRef>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in await _db.Sectors.AsNoTracking()
                     .Where(s => s.Acc != null)
                     .Select(s => new { s.Callsign, s.Acc!.Name, s.Acc!.Code, s.Acc!.IsForeign }).ToListAsync(ct))
            map[s.Callsign] = new AccRef(s.Name, s.Code, s.IsForeign);
        return map;
    }

    internal static async Task<IReadOnlyDictionary<string, string>> BuildAtcNameMapAsync(VipiDbContext db, CancellationToken ct)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in await db.AccSectors.AsNoTracking()
                     .Where(s => s.AtcCallsign != null && s.AtcCallsign != "")
                     .Select(s => new { s.ComposePosition, s.AtcCallsign }).ToListAsync(ct))
            map[a.ComposePosition] = a.AtcCallsign!;
        foreach (var a in await db.AirportSectors.AsNoTracking()
                     .Where(s => s.AtcCallsign != null && s.AtcCallsign != "")
                     .Select(s => new { s.ComposePosition, s.AtcCallsign }).ToListAsync(ct))
            map.TryAdd(a.ComposePosition, a.AtcCallsign!);
        return map;
    }

    public async Task<IReadOnlyList<LinkableFrequencyRow>> ListLinkableFrequenciesAsync(CancellationToken ct = default) =>
        await _db.Sectors.AsNoTracking()
            .Where(s => s.DefaultFrequency != null)
            .OrderBy(s => s.AirportIcao).ThenBy(s => s.Callsign)
            .Select(s => new LinkableFrequencyRow(s.Id, s.AirportIcao, s.Callsign, s.DefaultFrequency!, null))
            .ToListAsync(ct);

    // ---- helper ----
    // Ordine, nome e sigla-da-tipo vengono da FrequencyPositions (Application): erano triplicati qui, in
    // EfAppDerivationRepository e in EfAirportRepository, e le copie avevano già divergiato.

    private static int PositionOrder(string position) => FrequencyPositions.OrderOf(position);

    private static string PositionFromType(SectorType t) => FrequencyPositions.FromSectorType(t);

    private static string FreqNameForPosition(string? position) => FrequencyPositions.NameOf(position);
}
