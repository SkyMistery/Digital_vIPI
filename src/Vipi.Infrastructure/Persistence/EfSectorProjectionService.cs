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
                ParentCallsign: s.ParentCallsign, IsAccApp: true,   // APP da un subcenter ACC è per natura "di ACC"
                AtcCallsign: s.AtcCallsign, Position: s.Position);
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
                ParentCallsign: s.ParentCallsign, IsAccApp: s.IsAccApp,
                AtcCallsign: s.AtcCallsign, Position: s.Position);
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
            var friendly = FriendlyName(d);
            if (!byCallsign.TryGetValue(d.Callsign, out var sector))
            {
                sector = new Sector { Callsign = d.Callsign, Name = friendly };
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
            // Nome amichevole dalla sorgente (AtcCallsign IVAO, fallback "{ICAO} {Tipo}"). Assegnato quando il Name
            // è vuoto o un SEGNAPOSTO (== callsign grezzo, residuo di proiezioni vecchie): riarmonizza senza clobberare
            // un nome realmente personalizzato dall'admin.
            if (string.IsNullOrWhiteSpace(sector.Name)
                || string.Equals(sector.Name, sector.Callsign, StringComparison.OrdinalIgnoreCase))
                sector.Name = friendly;
            sector.IsProjected = true;
            sector.IsActive = true;
            sector.ImportedAtUtc = DateTime.UtcNow;
            changed++;
        }

        // 4. Padre (contenimento) derivato dal ParentCallsign del catalogo. Se il padre diretto è NASCOSTO
        //    (non è in `desired`), il figlio risale la catena dei ParentCallsign fino al primo antenato VISIBILE
        //    (nonno, bisnonno…). Un solo code-path che copre settore nascosto, ACC nascosto e orfano: si aggancia
        //    solo a callsign confermati in `desired` (tutti upsertati IsActive=true), mai a un settore disattivato.
        var parentOf = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in accSectors) parentOf[s.ComposePosition] = s.ParentCallsign;
        foreach (var s in airportSectors) parentOf[s.ComposePosition] = s.ParentCallsign;

        foreach (var d in desired.Values)
        {
            var child = byCallsign[d.Callsign];
            var visibleParentCs = NearestVisibleAncestor(d.ParentCallsign, desired, parentOf);
            if (visibleParentCs != null
                && byCallsign.TryGetValue(visibleParentCs, out var parent)
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
        //    Recide anche i legami editoriali (DocumentId/IsPrimary/FeaturedRank): un settore che non esiste più
        //    nella sorgente non deve restare agganciato a un Document (FK dangling → artefatti doppio-documento
        //    in rigenerazione, e "primario" fantasma). Se il callsign riappare, il sync lo re-upserta pulito.
        foreach (var s in existing)
        {
            if (s.IsProjected && !desired.ContainsKey(s.Callsign) && s.IsActive)
            {
                s.IsActive = false;
                s.DocumentId = null;
                s.IsPrimary = false;
                s.FeaturedRank = null;
                changed++;
            }
        }

        await _db.SaveChangesAsync(ct);

        // I poligoni/visibilità dei settori appena riproiettati possono aver cambiato i confini esteri: invalida la
        // cache del set confinanti (altrimenti resta stantia fino al TTL di 5 min). Questo è il choke point comune di
        // ogni mutazione catalogo (import ACC/aeroporti, hide, neighbour), quindi basta invalidare qui.
        EfHierarchyEditingService.InvalidateConfiningCache();
        return changed;
    }

    /// <summary>Risale la catena dei <c>ParentCallsign</c> partendo da <paramref name="parentCallsign"/> e ritorna il
    /// primo antenato presente in <paramref name="desired"/> (cioè VISIBILE), saltando gli antenati nascosti; null se la
    /// catena finisce (radice reale) o si esaurisce. Guard anti-ciclo con un set dei callsign già visitati.</summary>
    private static string? NearestVisibleAncestor(
        string? parentCallsign, Dictionary<string, Desired> desired, Dictionary<string, string?> parentOf)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cur = parentCallsign;
        while (!string.IsNullOrWhiteSpace(cur) && seen.Add(cur))
        {
            if (desired.ContainsKey(cur)) return cur;                       // antenato visibile → stop
            cur = parentOf.TryGetValue(cur, out var p) ? p : null;          // nascosto → sali di un livello
        }
        return null;
    }

    private sealed record Desired(
        string Callsign, int AccId, SectorType Type, SectorKind Kind,
        string? Frequency, int? AirportId, string? AirportIcao, string? ParentCallsign, bool IsAccApp,
        string? AtcCallsign, string? Position);

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

    /// <summary>Nome amichevole del settore: nome display IVAO (<c>AtcCallsign</c>) se presente, altrimenti
    /// composto <c>"{ICAO} {Tipo}"</c> (es. "LIRF Approach"), infine il callsign grezzo come ultima spiaggia.</summary>
    private static string FriendlyName(Desired d)
    {
        if (!string.IsNullOrWhiteSpace(d.AtcCallsign)) return d.AtcCallsign!.Trim();
        var icao = IcaoPrefix(d.Callsign) ?? d.Callsign;
        var label = LabelOf(d.Position, d.Type);
        return string.IsNullOrEmpty(label) ? d.Callsign : $"{icao} {label}";
    }

    /// <summary>Etichetta leggibile del ruolo (allineata ai nomi già in DB). Fallback sul <see cref="SectorType"/> se
    /// la position del catalogo è assente.</summary>
    private static string LabelOf(string? position, SectorType type) => (position?.Trim().ToUpperInvariant()) switch
    {
        "DEL" => "Delivery",
        "GND" => "Ground",
        "TWR" => "Tower",
        "APP" => "Approach",
        "DEP" => "Departure",
        "CTR" => "Control",
        "FSS" => "Information",
        "ATIS" => "ATIS",
        _ => type switch
        {
            SectorType.Del => "Delivery",
            SectorType.Gnd => "Ground",
            SectorType.Twr or SectorType.ITwr => "Tower",
            SectorType.App => "Approach",
            SectorType.Ctr => "Control",
            _ => "",
        },
    };

    /// <summary>ICAO = i 4 caratteri prima del primo '_' del callsign (LIRF_TW1_APP → LIRF); null se non conforme.</summary>
    private static string? IcaoPrefix(string callsign)
    {
        var i = callsign.IndexOf('_');
        return i == 4 ? callsign[..4].ToUpperInvariant() : null;
    }

    private static int CoverageFor(SectorType type) => type switch
    {
        SectorType.App => 5,
        SectorType.Twr or SectorType.ITwr => 10,
        SectorType.Gnd => 20,
        SectorType.Del => 30,
        _ => 0,   // CTR/area = radice
    };
}
