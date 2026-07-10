using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Persistenza EF della vIPI ACC (documento a blocchi, 1:1 con l'Acc) e primitive di derivazione live.
/// La struttura a blocchi è serializzata in <see cref="AccProfile.BlocksJson"/>; il profilo è creato on-demand.
/// </summary>
public sealed class EfAccProfileRepository : IAccProfileRepository
{
    private readonly VipiDbContext _db;
    public EfAccProfileRepository(VipiDbContext db) => _db = db;

    public async Task<string?> GetAccNameByCodeAsync(string accCode, CancellationToken ct = default) =>
        await _db.Accs.AsNoTracking().Where(a => a.Code == accCode).Select(a => a.Name).FirstOrDefaultAsync(ct);

    public async Task<AccDocumentIdentity?> ResolveAccDocumentIdentityAsync(string accCode, CancellationToken ct = default)
    {
        var accName = await _db.Accs.AsNoTracking().Where(a => a.Code == accCode).Select(a => a.Name).FirstOrDefaultAsync(ct);
        if (accName is null) return null;
        // Settore CTR radice primario (stesso criterio di ResolveRootAsync): chiavizza il Document ACC-wide.
        var root = await _db.Sectors.AsNoTracking()
            .Where(s => s.Acc!.Code == accCode && s.Type == SectorType.Ctr && s.ParentSectorId == null && s.IsActive)
            .OrderBy(s => s.CoverageOrder).ThenBy(s => s.Callsign)
            .Select(s => new { s.Id, s.Callsign, s.DocumentId })
            .FirstOrDefaultAsync(ct);
        if (root is null) return null;
        return new AccDocumentIdentity(root.Id, root.Callsign, accCode, accName, root.DocumentId);
    }

    public async Task<IReadOnlyList<AccBlock>> LoadBlocksAsync(string accCode, string? rootCallsign = null, CancellationToken ct = default)
    {
        var root = await ResolveRootAsync(accCode, rootCallsign, ct);
        var json = await _db.AccProfiles.AsNoTracking()
            .Where(p => p.Acc!.Code == accCode && p.RootCallsign == root)
            .Select(p => p.BlocksJson).FirstOrDefaultAsync(ct);
        return ParseList<AccBlock>(json);
    }

    public async Task<bool> IsHiddenAsync(string accCode, string? rootCallsign = null, CancellationToken ct = default)
    {
        var root = await ResolveRootAsync(accCode, rootCallsign, ct);
        return await _db.AccProfiles.AsNoTracking()
            .Where(p => p.Acc!.Code == accCode && p.RootCallsign == root)
            .Select(p => p.IsHidden).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<AccTreeRoot>> ListTreeRootsAsync(string accCode, CancellationToken ct = default) =>
        await _db.Sectors.AsNoTracking()
            .Where(s => s.Acc!.Code == accCode && s.Type == SectorType.Ctr && s.ParentSectorId == null && s.IsActive)
            .OrderBy(s => s.CoverageOrder).ThenBy(s => s.Callsign)
            .Select(s => new AccTreeRoot(s.Callsign, s.Name))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> ListSubtreeCtrCallsignsAsync(string accCode, string rootCallsign, CancellationToken ct = default)
    {
        var all = await _db.Sectors.AsNoTracking()
            .Where(s => s.Acc!.Code == accCode && s.Type == SectorType.Ctr && s.IsActive)
            .Select(s => new { s.Id, s.Callsign, s.ParentSectorId }).ToListAsync(ct);

        var root = all.FirstOrDefault(s => string.Equals(s.Callsign, rootCallsign, StringComparison.OrdinalIgnoreCase));
        if (root is null) return Array.Empty<string>();

        var byParent = all.ToLookup(s => s.ParentSectorId);
        var result = new List<string> { root.Callsign };
        var queue = new Queue<int>();
        queue.Enqueue(root.Id);
        while (queue.Count > 0)
        {
            var pid = queue.Dequeue();
            foreach (var child in byParent[pid])
            {
                result.Add(child.Callsign);
                queue.Enqueue(child.Id);
            }
        }
        return result;
    }

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

    /// <summary>Radice effettiva: quella indicata (normalizzata) o la primaria dell'ACC (CoverageOrder/alfabetico). null se l'ACC non ha radici.</summary>
    private async Task<string?> ResolveRootAsync(string accCode, string? rootCallsign, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(rootCallsign)) return rootCallsign.Trim().ToUpperInvariant();
        return await _db.Sectors.AsNoTracking()
            .Where(s => s.Acc!.Code == accCode && s.Type == SectorType.Ctr && s.ParentSectorId == null && s.IsActive)
            .OrderBy(s => s.CoverageOrder).ThenBy(s => s.Callsign)
            .Select(s => s.Callsign).FirstOrDefaultAsync(ct);
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

    public async Task<IReadOnlyList<string>> GetAorPolygonsRawAsync(IReadOnlyList<string> callsigns, CancellationToken ct = default)
    {
        if (callsigns.Count == 0) return Array.Empty<string>();
        var set = callsigns.ToList();

        var ctr = await _db.AccSectors.AsNoTracking()
            .Where(s => set.Contains(s.ComposePosition) && s.RegionMapPolygon != null && s.RegionMapPolygon != "")
            .Select(s => s.RegionMapPolygon!).ToListAsync(ct);

        var app = await _db.AirportSectors.AsNoTracking()
            .Where(s => set.Contains(s.ComposePosition) && s.RegionMapPolygon != null && s.RegionMapPolygon != "")
            .Select(s => s.RegionMapPolygon!).ToListAsync(ct);

        return ctr.Concat(app).ToList();
    }

    public async Task<IReadOnlyDictionary<string, string>> GetSectorPolygonsRawByCallsignAsync(IReadOnlyList<string> callsigns, CancellationToken ct = default)
    {
        var res = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (callsigns.Count == 0) return res;
        var set = callsigns.ToList();

        var ctr = await _db.AccSectors.AsNoTracking()
            .Where(s => set.Contains(s.ComposePosition) && s.RegionMapPolygon != null && s.RegionMapPolygon != "")
            .Select(s => new { s.ComposePosition, s.RegionMapPolygon }).ToListAsync(ct);
        foreach (var r in ctr) res[r.ComposePosition] = r.RegionMapPolygon!;

        var app = await _db.AirportSectors.AsNoTracking()
            .Where(s => set.Contains(s.ComposePosition) && s.RegionMapPolygon != null && s.RegionMapPolygon != "")
            .Select(s => new { s.ComposePosition, s.RegionMapPolygon }).ToListAsync(ct);
        foreach (var r in app) res.TryAdd(r.ComposePosition, r.RegionMapPolygon!);

        return res;
    }

    public async Task<IReadOnlyList<string>> GetTowerPolygonsRawForAppsAsync(IReadOnlyList<string> appCallsigns, CancellationToken ct = default)
    {
        if (appCallsigns.Count == 0) return Array.Empty<string>();
        var apps = appCallsigns.ToList();

        var icaos = await _db.AirportSectors.AsNoTracking()
            .Where(s => apps.Contains(s.ComposePosition))
            .Select(s => s.AirportIcao).Distinct().ToListAsync(ct);
        if (icaos.Count == 0) return Array.Empty<string>();

        return await _db.AirportSectors.AsNoTracking()
            .Where(s => icaos.Contains(s.AirportIcao) && s.Position == "TWR" && !s.IsHidden
                        && s.RegionMapPolygon != null && s.RegionMapPolygon != "")
            .OrderBy(s => s.ComposePosition)
            .Select(s => s.RegionMapPolygon!).ToListAsync(ct);
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
        await EfAccProfileRepository.BuildAtcNameMapAsync(_db, ct);

    public async Task<IReadOnlyDictionary<string, string>> GetSectorAccNameMapAsync(CancellationToken ct = default)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in await _db.Sectors.AsNoTracking()
                     .Where(s => s.Acc != null)
                     .Select(s => new { s.Callsign, AccName = s.Acc!.Name }).ToListAsync(ct))
            map[s.Callsign] = s.AccName;
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
            .Select(s => new LinkableFrequencyRow(s.Id, s.AirportIcao, s.Callsign, s.DefaultFrequency!))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SpecialAreaPick>> ListSpecialAreasByAccAsync(string accCode, CancellationToken ct = default) =>
        await _db.SpecialAreas.AsNoTracking()
            .Where(s => s.CenterId == accCode)
            .OrderBy(s => s.Name)
            .Select(s => new SpecialAreaPick(s.IvaoId, s.Name, s.Type, s.MinimumAlt, s.MaximumAlt))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SpecialAreaDetail>> GetSpecialAreasByIdsAsync(IReadOnlyList<string> ivaoIds, CancellationToken ct = default)
    {
        if (ivaoIds.Count == 0) return Array.Empty<SpecialAreaDetail>();
        var ids = ivaoIds.ToList();
        return await _db.SpecialAreas.AsNoTracking()
            .Where(s => ids.Contains(s.IvaoId))
            .Select(s => new SpecialAreaDetail(s.IvaoId, s.Name, s.Type, s.Description, s.ActivationDetails,
                s.MinimumAlt, s.MaximumAlt, s.RegionMapPolygon))
            .ToListAsync(ct);
    }

    public async Task SaveBlocksAsync(string accCode, IReadOnlyList<AccBlock> blocks, string? rootCallsign = null, CancellationToken ct = default)
    {
        var p = await GetOrCreateAsync(accCode, rootCallsign, ct);
        p.BlocksJson = JsonSerializer.Serialize(blocks);
        await _db.SaveChangesAsync(ct);
    }

    // ---- helper ----

    private async Task<AccProfile> GetOrCreateAsync(string accCode, string? rootCallsign, CancellationToken ct)
    {
        var accId = await _db.Accs.Where(a => a.Code == accCode).Select(a => (int?)a.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"ACC {accCode} inesistente.");
        var root = await ResolveRootAsync(accCode, rootCallsign, ct);
        var profile = await _db.AccProfiles.FirstOrDefaultAsync(p => p.AccId == accId && p.RootCallsign == root, ct);
        if (profile is null)
        {
            profile = new AccProfile { AccId = accId, RootCallsign = root };
            _db.AccProfiles.Add(profile);
            await _db.SaveChangesAsync(ct);
        }
        return profile;
    }

    private static List<T> ParseList<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<List<T>>(json) ?? new(); }
        catch (JsonException) { return new(); }
    }

    private static readonly string[] FreqTypeOrder = { "ATIS", "DEL", "GND", "TWR", "APP", "DEP", "CTR" };
    private static int PositionOrder(string position)
    {
        var i = Array.IndexOf(FreqTypeOrder, position.Trim().ToUpperInvariant());
        return i < 0 ? 99 : i;
    }

    private static string PositionFromType(SectorType t) => t switch
    {
        SectorType.Del => "DEL",
        SectorType.Gnd => "GND",
        SectorType.Twr or SectorType.ITwr => "TWR",
        SectorType.App => "APP",
        SectorType.Ctr => "CTR",
        _ => t.ToString().ToUpperInvariant(),
    };

    private static string FreqNameForPosition(string? position) => (position ?? "").Trim().ToUpperInvariant() switch
    {
        "ATIS" => "ATIS",
        "DEL" => "Delivery",
        "GND" => "Ground",
        "TWR" => "Tower",
        "APP" => "Approach",
        "DEP" => "Departure",
        "CTR" => "Control",
        "FSS" => "Information",
        _ => string.IsNullOrWhiteSpace(position) ? "—" : position!,
    };
}
