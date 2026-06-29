using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="ISectorProjectionService"/>
public sealed class EfSectorProjectionService : ISectorProjectionService
{
    private readonly VipiDbContext _db;
    public EfSectorProjectionService(VipiDbContext db) => _db = db;

    public async Task<int> SyncFromCatalogsAsync(CancellationToken ct = default)
    {
        // Mappe di risoluzione: codice ACC → Id, ICAO aeroporto → Id.
        var accIdByCode = await _db.Accs.ToDictionaryAsync(a => a.Code, a => a.Id, StringComparer.OrdinalIgnoreCase, ct);
        var airportIdByIcao = await _db.Airports.ToDictionaryAsync(a => a.Icao, a => a.Id, StringComparer.OrdinalIgnoreCase, ct);

        // ACC nascosti → i loro settori sono effettivamente nascosti.
        var hiddenAccCodes = await _db.Accs.Where(a => a.IsHidden).Select(a => a.Code)
            .ToListAsync(ct);
        var hiddenAcc = hiddenAccCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 1. Insieme desiderato dai cataloghi (callsign → attributi proiettati). ATIS escluso (non è un settore).
        var desired = new Dictionary<string, Desired>(StringComparer.OrdinalIgnoreCase);

        var accSectors = await _db.AccSectors.AsNoTracking().ToListAsync(ct);
        foreach (var s in accSectors)
        {
            if (s.IsHidden || hiddenAcc.Contains(s.CenterId)) continue;
            if (IsAtis(s.Position)) continue;
            if (!accIdByCode.TryGetValue(s.CenterId, out var accId)) continue;
            desired[s.ComposePosition] = new Desired(
                Callsign: s.ComposePosition, AccId: accId, Type: MapType(s.Position),
                Kind: SectorKind.Acc, Frequency: s.Frequency, AirportId: null, AirportIcao: null,
                ParentCallsign: s.ParentCallsign, IsAccApp: true);   // APP da un subcenter ACC è per natura "di ACC"
        }

        var airportSectors = await _db.AirportSectors.AsNoTracking().ToListAsync(ct);
        foreach (var s in airportSectors)
        {
            if (s.IsHidden || hiddenAcc.Contains(s.AccCode)) continue;
            if (IsAtis(s.Position)) continue;
            if (!accIdByCode.TryGetValue(s.AccCode, out var accId)) continue;
            airportIdByIcao.TryGetValue(s.AirportIcao, out var airportId);
            desired[s.ComposePosition] = new Desired(
                Callsign: s.ComposePosition, AccId: accId, Type: MapType(s.Position),
                Kind: SectorKind.Airport, Frequency: s.Frequency,
                AirportId: airportId == 0 ? null : airportId, AirportIcao: s.AirportIcao,
                ParentCallsign: s.ParentCallsign, IsAccApp: s.IsAccApp);
        }

        // 2. Settori già presenti che ci interessano: tutti i proiettati + quelli col callsign desiderato (per adottarli).
        var desiredKeys = desired.Keys.ToList();
        var existing = await _db.Sectors
            .Where(s => s.IsProjected || desiredKeys.Contains(s.Callsign))
            .ToListAsync(ct);
        var byCallsign = existing.ToDictionary(s => s.Callsign, s => s, StringComparer.OrdinalIgnoreCase);

        var changed = 0;

        // 3. Upsert per callsign (preserva Id e i legami editoriali DocumentId/IsPrimary/FeaturedRank).
        foreach (var d in desired.Values)
        {
            if (!byCallsign.TryGetValue(d.Callsign, out var sector))
            {
                sector = new Sector { Callsign = d.Callsign, Name = d.Callsign };
                _db.Sectors.Add(sector);
                byCallsign[d.Callsign] = sector;
            }
            sector.AccId = d.AccId;
            sector.Type = d.Type;
            sector.Kind = d.Kind;
            sector.ApproachKind = d.Type == SectorType.App
                ? (d.IsAccApp ? ApproachKind.Remotized : ApproachKind.Standalone)
                : null;
            sector.DefaultFrequency = d.Frequency;
            sector.AirportId = d.AirportId;
            sector.AirportIcao = d.AirportIcao;
            sector.CoverageOrder = CoverageFor(d.Type);
            if (string.IsNullOrWhiteSpace(sector.Name)) sector.Name = d.Callsign;
            sector.IsProjected = true;
            sector.IsActive = true;
            sector.ImportedAtUtc = DateTime.UtcNow;
            changed++;
        }

        // 4. Padre (contenimento) derivato dal ParentCallsign del catalogo, risolto nella mappa unita.
        foreach (var d in desired.Values)
        {
            var child = byCallsign[d.Callsign];
            if (!string.IsNullOrWhiteSpace(d.ParentCallsign)
                && byCallsign.TryGetValue(d.ParentCallsign!, out var parent)
                && !ReferenceEquals(parent, child))
            {
                child.ParentSector = parent;   // EF risolve l'Id alla SaveChanges anche per le nuove righe
            }
            else
            {
                child.ParentSector = null;
                child.ParentSectorId = null;
            }
        }

        // 5. Orfani: settori PROIETTATI il cui callsign non è più nel catalogo visibile → disattiva (non cancella).
        foreach (var s in existing)
        {
            if (s.IsProjected && !desired.ContainsKey(s.Callsign) && s.IsActive)
            {
                s.IsActive = false;
                changed++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return changed;
    }

    private sealed record Desired(
        string Callsign, int AccId, SectorType Type, SectorKind Kind,
        string? Frequency, int? AirportId, string? AirportIcao, string? ParentCallsign, bool IsAccApp);

    private static bool IsAtis(string? position) =>
        string.Equals(position, "ATIS", StringComparison.OrdinalIgnoreCase);

    /// <summary>Mappa il suffisso position del catalogo al SectorType operativo.</summary>
    private static SectorType MapType(string? position) => (position?.Trim().ToUpperInvariant()) switch
    {
        "DEL" => SectorType.Del,
        "GND" => SectorType.Gnd,
        "TWR" => SectorType.Twr,
        "APP" or "DEP" => SectorType.App,
        "CTR" or "FSS" => SectorType.Ctr,
        _ => SectorType.Ctr,
    };

    private static int CoverageFor(SectorType type) => type switch
    {
        SectorType.App => 5,
        SectorType.Twr or SectorType.ITwr => 10,
        SectorType.Gnd => 20,
        SectorType.Del => 30,
        _ => 0,   // CTR/area = radice
    };
}
