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
public sealed class EfAppDerivationRepository : IAppDerivationRepository
{
    private readonly VipiDbContext _db;
    public EfAppDerivationRepository(VipiDbContext db) => _db = db;

    public async Task<string?> GetAccCodeByAppAsync(string appCallsign, CancellationToken ct = default) =>
        await _db.Sectors.Where(s => s.Callsign == appCallsign && s.Type == SectorType.App)
            .Select(s => s.Acc!.Code).FirstOrDefaultAsync(ct);

    public async Task<AppDocumentIdentity?> ResolveForDocumentAsync(string appCallsign, CancellationToken ct = default)
    {
        var s = await _db.Sectors.AsNoTracking().Include(x => x.Acc)
            .FirstOrDefaultAsync(x => x.Callsign == appCallsign && x.Type == SectorType.App, ct);
        if (s is null || s.Acc is null) return null;

        // Titolo = nome IVAO (AtcCallsign, es. "Palermo Approach") dal catalogo, fallback al nome settore, poi callsign.
        var display = await _db.AirportSectors.AsNoTracking()
            .Where(a => a.ComposePosition == appCallsign).Select(a => a.AtcCallsign).FirstOrDefaultAsync(ct);
        var title = string.IsNullOrWhiteSpace(display) ? (string.IsNullOrWhiteSpace(s.Name) ? s.Callsign : s.Name) : display!;
        return new AppDocumentIdentity(s.Id, s.Callsign, title, s.Acc.Code, s.DocumentId);
    }

    public async Task<IReadOnlyList<AppFreqRow>> ResolveFreqLinksAsync(IReadOnlyList<int> sourceSectorIds, CancellationToken ct = default)
    {
        if (sourceSectorIds.Count == 0) return Array.Empty<AppFreqRow>();
        var byId = await _db.Sectors.AsNoTracking()
            .Where(s => sourceSectorIds.Contains(s.Id) && s.DefaultFrequency != null)
            .ToDictionaryAsync(s => s.Id, ct);
        var rows = new List<AppFreqRow>();
        foreach (var id in sourceSectorIds)   // preserva l'ordine dei link salvati
            if (byId.TryGetValue(id, out var s))
                rows.Add(new AppFreqRow(id, s.Callsign, s.Callsign, s.DefaultFrequency!,
                    s.Type.ToString().ToUpperInvariant(), false, true));
        return await ApplyAtcNamesAsync(rows, ct);
    }

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

    public async Task<IReadOnlyList<(string Callsign, string Poly)>> GetTowerPolygonsWithCallsignRawAsync(string appCallsign, CancellationToken ct = default)
    {
        var icao = await _db.AirportSectors.AsNoTracking()
            .Where(s => s.ComposePosition == appCallsign)
            .Select(s => s.AirportIcao).FirstOrDefaultAsync(ct);
        if (icao is null) return Array.Empty<(string, string)>();

        var rows = await _db.AirportSectors.AsNoTracking()
            .Where(s => s.AirportIcao == icao && s.Position == "TWR" && !s.IsHidden
                        && s.RegionMapPolygon != null && s.RegionMapPolygon != "")
            .OrderBy(s => s.ComposePosition)
            .Select(s => new { s.ComposePosition, Poly = s.RegionMapPolygon! })
            .ToListAsync(ct);
        return rows.Select(r => (r.ComposePosition, r.Poly)).ToList();
    }

    public async Task<IReadOnlyList<LinkableFrequencyRow>> ListLinkableFrequenciesAsync(CancellationToken ct = default) =>
        await _db.Sectors.AsNoTracking()
            .Where(s => s.DefaultFrequency != null)
            .OrderBy(s => s.AirportIcao).ThenBy(s => s.Callsign)
            .Select(s => new LinkableFrequencyRow(s.Id, s.AirportIcao, s.Callsign, s.DefaultFrequency!, null))
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
        await EfAccDerivationRepository.BuildAtcNameMapAsync(_db, ct);

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

        // Nome visualizzato reale (IVAO atcCallsign, es. "Palermo Approach") dal catalogo: sovrascrive il nome-posizione dove disponibile.
        return await ApplyAtcNamesAsync(ordered, ct);
    }

    /// <summary>Sostituisce <see cref="AppFreqRow.Name"/> con l'atcCallsign IVAO (dal catalogo) dove presente; altrimenti lascia il nome-posizione.</summary>
    private async Task<IReadOnlyList<AppFreqRow>> ApplyAtcNamesAsync(IReadOnlyList<AppFreqRow> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return rows;
        var atc = await EfAccDerivationRepository.BuildAtcNameMapAsync(_db, ct);
        if (atc.Count == 0) return rows;
        return rows.Select(r => atc.TryGetValue(r.Callsign, out var n) ? r with { Name = n } : r).ToList();
    }

    // ---- helper ----

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
