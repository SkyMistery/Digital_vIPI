using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Persistenza EF del profilo editoriale dell'APP standalone (1:1 col Sector APP) e derivazione del catalogo
/// frequenze del sottoalbero. Le scritture per-area sostituiscono il valore; il profilo è creato on-demand.
/// </summary>
public sealed class EfAppProfileRepository : IAppProfileRepository
{
    private readonly VipiDbContext _db;
    public EfAppProfileRepository(VipiDbContext db) => _db = db;

    public async Task<string?> GetAccCodeByAppAsync(string appCallsign, CancellationToken ct = default) =>
        await _db.Sectors.Where(s => s.Callsign == appCallsign && s.Type == SectorType.App)
            .Select(s => s.Acc!.Code).FirstOrDefaultAsync(ct);

    public async Task<string?> GetAorPolygonRawAsync(string appCallsign, CancellationToken ct = default) =>
        await _db.AirportSectors.AsNoTracking().Where(s => s.ComposePosition == appCallsign)
            .Select(s => s.RegionMapPolygon).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<string>> GetTowerPolygonsRawAsync(string appCallsign, CancellationToken ct = default)
    {
        // Aeroporto dell'APP (dal catalogo), poi le sue TWR visibili con poligono.
        var icao = await _db.AirportSectors.AsNoTracking()
            .Where(s => s.ComposePosition == appCallsign)
            .Select(s => s.AirportIcao).FirstOrDefaultAsync(ct);
        if (icao is null) return Array.Empty<string>();

        var polys = await _db.AirportSectors.AsNoTracking()
            .Where(s => s.AirportIcao == icao && s.Position == "TWR" && !s.IsHidden
                        && s.RegionMapPolygon != null && s.RegionMapPolygon != "")
            .OrderBy(s => s.ComposePosition)
            .Select(s => s.RegionMapPolygon!)
            .ToListAsync(ct);
        return polys;
    }

    public async Task<AppProfileData?> LoadAsync(string appCallsign, CancellationToken ct = default)
    {
        var sector = await _db.Sectors.AsNoTracking().Include(s => s.Acc)
            .FirstOrDefaultAsync(s => s.Callsign == appCallsign && s.Type == SectorType.App, ct);
        if (sector is null) return null;

        var profile = await _db.AppProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.SectorId == sector.Id, ct);

        var links = profile is null
            ? new List<AppFreqRow>()
            : await _db.AppFrequencyLinks.AsNoTracking().Where(x => x.AppProfileId == profile.Id)
                .OrderBy(x => x.Order).Include(x => x.SourceSector)
                .Where(x => x.SourceSector != null && x.SourceSector!.DefaultFrequency != null)
                .Select(x => new AppFreqRow(x.SourceSectorId,
                    x.LabelOverride ?? x.SourceSector!.Callsign, x.SourceSector!.Callsign,
                    x.SourceSector!.DefaultFrequency!, x.SourceSector!.Type.ToString().ToUpperInvariant(), false, true))
                .ToListAsync(ct);

        return new AppProfileData
        {
            SectorId = sector.Id,
            AppCallsign = sector.Callsign,
            Name = sector.Name,
            AccCode = sector.Acc!.Code,
            Separations = ParseList<AppSeparationRow>(profile?.SeparationsJson),
            VfrJson = profile?.VfrJson,
            SectionOrder = ParseList<string>(profile?.SectionOrderJson),
            HiddenSections = ParseList<string>(profile?.HiddenSectionsJson),
            FreqOrder = ParseFreqOrder(profile?.FreqOrderJson),
            FrequencyLinks = links,
            CustomSections = ParseList<AppCustomSection>(profile?.CustomSectionsJson),
            CoordinationSentenceTemplate = profile?.CoordinationSentenceTemplate,
        };
    }

    public async Task<IReadOnlyList<LinkableFrequencyRow>> ListLinkableFrequenciesAsync(CancellationToken ct = default) =>
        await _db.Sectors.AsNoTracking()
            .Where(s => s.DefaultFrequency != null)
            .OrderBy(s => s.AirportIcao).ThenBy(s => s.Callsign)
            .Select(s => new LinkableFrequencyRow(s.Id, s.AirportIcao, s.Callsign, s.DefaultFrequency!))
            .ToListAsync(ct);

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

    public async Task<IReadOnlyDictionary<string, string>> GetSectorNameMapAsync(CancellationToken ct = default)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in await _db.Sectors.AsNoTracking().Select(s => new { s.Callsign, s.Name }).ToListAsync(ct))
            map[s.Callsign] = s.Name;
        return map;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetSectorAtcNameMapAsync(CancellationToken ct = default) =>
        await EfAccProfileRepository.BuildAtcNameMapAsync(_db, ct);

    public async Task<string?> GetCoordinationTemplateAsync(string appCallsign, CancellationToken ct = default) =>
        await _db.AppProfiles.AsNoTracking()
            .Where(p => p.Sector!.Callsign == appCallsign && p.Sector!.Type == SectorType.App)
            .Select(p => p.CoordinationSentenceTemplate).FirstOrDefaultAsync(ct);

    public async Task SaveCoordinationTemplateAsync(string appCallsign, string? template, CancellationToken ct = default)
    {
        var p = await GetOrCreateAsync(appCallsign, ct);
        p.CoordinationSentenceTemplate = string.IsNullOrWhiteSpace(template) ? null : template;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AppFreqRow>> DeriveCatalogFrequenciesAsync(
        string appCallsign, IReadOnlySet<string> domainCallsigns,
        IReadOnlyList<string> ancestorCallsigns, CancellationToken ct = default)
    {
        var domain = domainCallsigns.ToList();

        // Aeroporti SOTTO l'APP: nella proiezione (Round 20) le posizioni DEL/GND/TWR NON sono figlie del Sector APP
        // (sono radici), ma l'AEROPORTO punta all'APP via ParentCallsign. "Sottostanti" = ParentCallsign nel sottoalbero.
        var icaos = await _db.Airports.AsNoTracking()
            .Where(a => a.ParentCallsign != null && domain.Contains(a.ParentCallsign))
            .Select(a => a.Icao).ToListAsync(ct);

        // Difensivo: includi comunque l'aeroporto che possiede la posizione APP stessa.
        var appIcao = await _db.AirportSectors.AsNoTracking()
            .Where(s => s.ComposePosition == appCallsign).Select(s => s.AirportIcao).FirstOrDefaultAsync(ct);
        if (appIcao != null && !icaos.Contains(appIcao)) icaos.Add(appIcao);

        if (icaos.Count == 0) return Array.Empty<AppFreqRow>();

        // Frequenze dal catalogo AirportSector (fonte autoritativa: ATIS·DEL·GND·TWR·APP con frequenza).
        var cat = await _db.AirportSectors.AsNoTracking()
            .Where(s => icaos.Contains(s.AirportIcao) && !s.IsHidden && s.Frequency != null)
            .Select(s => new { s.ComposePosition, s.Position, s.Frequency })
            .ToListAsync(ct);

        var rows = cat.Select(s => new AppFreqRow(
            null, FreqNameForPosition(s.Position), s.ComposePosition, s.Frequency!,
            (s.Position ?? "").Trim().ToUpperInvariant(),
            string.Equals(s.ComposePosition, appCallsign, StringComparison.OrdinalIgnoreCase), false)).ToList();

        // Ordine ATIS·DEL·GND·TWR·APP; a parità, primaria (★) prima, poi callsign.
        var ordered = rows
            .OrderBy(r => PositionOrder(r.Position))
            .ThenByDescending(r => r.IsPrimary)
            .ThenBy(r => r.Callsign, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Genitori di copertura (CTR superiori): settori ancestor con frequenza, in coda, nell'ordine di vicinanza.
        if (ancestorCallsigns.Count > 0)
        {
            var ancList = ancestorCallsigns.ToList();
            var anc = await _db.Sectors.AsNoTracking()
                .Where(s => ancList.Contains(s.Callsign) && s.DefaultFrequency != null)
                .Select(s => new { s.Callsign, s.Type, s.DefaultFrequency })
                .ToListAsync(ct);
            var byCs = anc.ToDictionary(s => s.Callsign, StringComparer.OrdinalIgnoreCase);
            foreach (var cs in ancestorCallsigns)   // preserva l'ordine vicino→lontano
                if (byCs.TryGetValue(cs, out var s))
                {
                    var pos = PositionFromType(s.Type);
                    ordered.Add(new AppFreqRow(null, FreqNameForPosition(pos), s.Callsign, s.DefaultFrequency!, pos, false, false));
                }
        }

        return ordered;
    }

    // ---- scritture (get-or-create del profilo) ----

    public async Task SaveSeparationsAsync(string appCallsign, IReadOnlyList<AppSeparationRow> rows, CancellationToken ct = default)
    {
        var p = await GetOrCreateAsync(appCallsign, ct);
        p.SeparationsJson = JsonSerializer.Serialize(rows
            .Select(r => new AppSeparationRow((r.Vertical ?? "").Trim(), (r.Lateral ?? "").Trim(),
                string.IsNullOrWhiteSpace(r.Applicability) ? null : r.Applicability!.Trim()))
            .Where(r => !(string.IsNullOrWhiteSpace(r.Vertical) && string.IsNullOrWhiteSpace(r.Lateral) && r.Applicability is null))
            .ToList());
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveVfrAsync(string appCallsign, string? vfrJson, CancellationToken ct = default)
    {
        var p = await GetOrCreateAsync(appCallsign, ct);
        p.VfrJson = string.IsNullOrWhiteSpace(vfrJson) ? null : vfrJson;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveSectionOrderAsync(string appCallsign, IReadOnlyList<string> order, CancellationToken ct = default)
    {
        var p = await GetOrCreateAsync(appCallsign, ct);
        p.SectionOrderJson = JsonSerializer.Serialize(order);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveFrequencyOrderAsync(string appCallsign, IReadOnlyList<AppFreqOrderOverride> overrides, CancellationToken ct = default)
    {
        var p = await GetOrCreateAsync(appCallsign, ct);
        var map = overrides.GroupBy(o => o.Callsign, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().Order, StringComparer.OrdinalIgnoreCase);
        p.FreqOrderJson = JsonSerializer.Serialize(map);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveHiddenSectionsAsync(string appCallsign, IReadOnlyList<string> hiddenKeys, CancellationToken ct = default)
    {
        var p = await GetOrCreateAsync(appCallsign, ct);
        p.HiddenSectionsJson = JsonSerializer.Serialize(hiddenKeys.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveCustomSectionsAsync(string appCallsign, IReadOnlyList<AppCustomSection> sections, CancellationToken ct = default)
    {
        var p = await GetOrCreateAsync(appCallsign, ct);
        p.CustomSectionsJson = JsonSerializer.Serialize(sections);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveFrequencyLinksAsync(string appCallsign, IReadOnlyList<int> sourceSectorIds, CancellationToken ct = default)
    {
        var p = await GetOrCreateAsync(appCallsign, ct);
        _db.AppFrequencyLinks.RemoveRange(_db.AppFrequencyLinks.Where(x => x.AppProfileId == p.Id));
        var valid = await _db.Sectors.Where(s => sourceSectorIds.Contains(s.Id)).Select(s => s.Id).ToListAsync(ct);
        var order = 0;
        foreach (var sid in sourceSectorIds.Where(valid.Contains))
            _db.AppFrequencyLinks.Add(new AppFrequencyLink { AppProfileId = p.Id, Order = order++, SourceSectorId = sid });
        await _db.SaveChangesAsync(ct);
    }

    // ---- helper ----

    private async Task<AppProfile> GetOrCreateAsync(string appCallsign, CancellationToken ct)
    {
        var sectorId = await _db.Sectors.Where(s => s.Callsign == appCallsign && s.Type == SectorType.App)
            .Select(s => (int?)s.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"APP {appCallsign} inesistente.");
        var profile = await _db.AppProfiles.FirstOrDefaultAsync(p => p.SectorId == sectorId, ct);
        if (profile is null)
        {
            profile = new AppProfile { SectorId = sectorId };
            _db.AppProfiles.Add(profile);
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

    private static IReadOnlyList<AppFreqOrderOverride> ParseFreqOrder(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<AppFreqOrderOverride>();
        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
            return map is null ? Array.Empty<AppFreqOrderOverride>()
                : map.Select(kv => new AppFreqOrderOverride(kv.Key, kv.Value)).ToList();
        }
        catch (JsonException) { return Array.Empty<AppFreqOrderOverride>(); }
    }

    private static readonly string[] FreqTypeOrder = { "ATIS", "DEL", "GND", "TWR", "APP", "DEP" };
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
