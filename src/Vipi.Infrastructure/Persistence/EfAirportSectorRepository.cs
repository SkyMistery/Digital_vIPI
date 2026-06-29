using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Implementazione EF di <see cref="IAirportSectorRepository"/>. Import = upsert dei settori ATC d'aeroporto
/// (DEL/GND/TWR/APP…) dalla sorgente, preservando IsHidden e i limiti admin. L'ACC di competenza è ereditato
/// dall'aeroporto. Niente cancellazioni (i settori spariti dalla sorgente restano; l'admin li nasconde).
/// </summary>
public sealed class EfAirportSectorRepository : IAirportSectorRepository
{
    private readonly VipiDbContext _db;
    public EfAirportSectorRepository(VipiDbContext db) => _db = db;

    private const int DefaultLowerFt = 0;        // GND
    private const int DefaultUpperFt = 19500;    // limite superiore di default dei settori d'aeroporto

    /// <summary>
    /// Solo le postazioni con un volume di spazio aereo hanno limiti (inferiore/superiore) e shape:
    /// TWR, APP/DEP, CTR (ACC), FSS. GND/DEL/ATIS no (terra/informativa).
    /// </summary>
    private static bool SupportsLimits(string? position)
    {
        var p = (position ?? "").Trim().ToUpperInvariant();
        return p is "TWR" or "APP" or "DEP" or "CTR" or "FSS";
    }

    public async Task<IReadOnlyList<AirportSectorRow>> ListByAirportAsync(string icao, CancellationToken ct = default)
    {
        icao = Norm(icao);
        return await _db.AirportSectors.AsNoTracking()
            .Where(s => s.AirportIcao == icao)
            .OrderBy(s => s.ComposePosition)
            .Select(s => new AirportSectorRow(s.Id, s.ComposePosition, s.AirportIcao, s.AccCode, s.Position,
                s.MiddleIdentifier, s.Frequency, s.LowerLimit, s.UpperLimit, s.IsHidden, s.RegionMapPolygon != null, s.IsPrimary,
                s.IsAccApp))
            .ToListAsync(ct);
    }

    public async Task SetHiddenAsync(int id, bool hidden, CancellationToken ct = default)
    {
        var s = await _db.AirportSectors.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new InvalidOperationException($"Settore d'aeroporto id {id} inesistente.");
        s.IsHidden = hidden;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetLimitsAsync(int id, int? lower, int? upper, CancellationToken ct = default)
    {
        var s = await _db.AirportSectors.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new InvalidOperationException($"Settore d'aeroporto id {id} inesistente.");
        s.LowerLimit = lower ?? DefaultLowerFt;   // inferiore: vuoto → 0 (GND)
        s.UpperLimit = upper;                      // superiore: vuoto → null = UNL
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetPrimaryAsync(int id, CancellationToken ct = default)
    {
        var s = await _db.AirportSectors.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new InvalidOperationException($"Settore d'aeroporto id {id} inesistente.");
        // Esclusiva per TIPO: una principale per Delivery, una per Ground, una per TWR, una per APP…
        var pos = (s.Position ?? "").Trim().ToUpperInvariant();
        var siblings = await _db.AirportSectors
            .Where(x => x.AirportIcao == s.AirportIcao && (x.Position ?? "").ToUpper() == pos).ToListAsync(ct);
        foreach (var x in siblings) x.IsPrimary = x.Id == id;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetIsAccAppAsync(int id, bool isAccApp, CancellationToken ct = default)
    {
        var s = await _db.AirportSectors.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new InvalidOperationException($"Settore d'aeroporto id {id} inesistente.");
        s.IsAccApp = isAccApp;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<string?> GetAccCodeByIcaoAsync(string icao, CancellationToken ct = default)
    {
        icao = Norm(icao);
        return await _db.Airports.AsNoTracking()
            .Where(a => a.Icao == icao)
            .Select(a => a.Acc!.Code)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<string?> GetAccCodeBySectorIdAsync(int id, CancellationToken ct = default) =>
        await _db.AirportSectors.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => s.AccCode)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<string>> ListAirportIcaosAsync(CancellationToken ct = default) =>
        await _db.Airports.AsNoTracking().OrderBy(a => a.Icao).Select(a => a.Icao).ToListAsync(ct);

    public async Task<(int Created, int Updated)> ImportForAirportAsync(
        string icao, IReadOnlyList<SourceAtcPosition> positions, CancellationToken ct = default)
    {
        icao = Norm(icao);
        var now = DateTime.UtcNow;
        int created = 0, updated = 0;

        // L'ACC di competenza è quello dell'aeroporto. Senza aeroporto/ACC non si importa (FK).
        var accCode = await _db.Airports.AsNoTracking()
            .Where(a => a.Icao == icao).Select(a => a.Acc!.Code).FirstOrDefaultAsync(ct);
        if (accCode is null) return (0, 0);

        var existing = await _db.AirportSectors
            .Where(s => s.AirportIcao == icao)
            .ToDictionaryAsync(s => s.ComposePosition, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var p in positions)
        {
            var compose = (p.Callsign ?? "").Trim().ToUpperInvariant();
            if (compose.Length == 0) continue;

            var position = p.Position ?? SuffixOf(compose);
            var hasLimits = SupportsLimits(position);

            if (existing.TryGetValue(compose, out var row))
            {
                row.AirportIcao = icao;
                row.AccCode = accCode;
                row.Position = position;
                row.MiddleIdentifier = p.MiddleIdentifier;
                row.Frequency = p.Frequency;
                if (hasLimits)
                {
                    row.RegionMapPolygon = p.RegionMapPolygon;
                    // Limiti: l'admin comanda; aggiorna solo se la sorgente li espone (oggi null → preserva).
                    if (p.LowerLimit is not null) row.LowerLimit = p.LowerLimit;
                    else row.LowerLimit ??= DefaultLowerFt;
                    if (p.UpperLimit is not null) row.UpperLimit = p.UpperLimit;
                    else row.UpperLimit ??= DefaultUpperFt;
                }
                else
                {
                    // GND/DEL/ATIS: niente limiti né shape.
                    row.RegionMapPolygon = null;
                    row.LowerLimit = null;
                    row.UpperLimit = null;
                }
                row.ImportedAtUtc = now;
                updated++;
            }
            else
            {
                _db.AirportSectors.Add(new AirportSector
                {
                    ComposePosition = compose,
                    AirportIcao = icao,
                    AccCode = accCode,
                    Position = position,
                    MiddleIdentifier = p.MiddleIdentifier,
                    Frequency = p.Frequency,
                    RegionMapPolygon = hasLimits ? p.RegionMapPolygon : null,
                    LowerLimit = hasLimits ? (p.LowerLimit ?? DefaultLowerFt) : null,
                    UpperLimit = hasLimits ? (p.UpperLimit ?? DefaultUpperFt) : null,
                    IsHidden = false,
                    IsAccApp = DefaultIsAccApp(compose, position),   // 3 pezzi (LIRN_UN0_APP) = di ACC; 2 pezzi (LIRP_APP) = no
                    ImportedAtUtc = now,
                });
                created++;
            }
        }

        await _db.SaveChangesAsync(ct);

        // Default: una frequenza principale PER TIPO (Delivery/Ground/TWR/APP/DEP) se quel tipo non ne ha già una.
        var sectors = await _db.AirportSectors.Where(s => s.AirportIcao == icao).ToListAsync(ct);
        var changed = false;
        foreach (var type in PrimaryTypes)
        {
            var ofType = sectors.Where(s => (s.Position ?? "").Trim().ToUpperInvariant() == type).ToList();
            if (ofType.Count == 0 || ofType.Any(s => s.IsPrimary)) continue;
            ofType.OrderBy(s => s.ComposePosition).First().IsPrimary = true;
            changed = true;
        }
        if (changed) await _db.SaveChangesAsync(ct);

        return (created, updated);
    }

    // Tipi di postazione che hanno una frequenza principale selezionabile (ATIS escluso).
    private static readonly string[] PrimaryTypes = { "DEL", "GND", "TWR", "APP", "DEP" };

    private static string Norm(string icao) => (icao ?? "").Trim().ToUpperInvariant();

    /// <summary>Suffisso del callsign dopo l'ultimo '_' (es. LIRN_US0_APP → APP).</summary>
    private static string SuffixOf(string callsign) =>
        callsign.Contains('_') ? callsign[(callsign.LastIndexOf('_') + 1)..] : callsign;

    /// <summary>Default "di ACC" di una posizione APP/DEP: vero se il callsign ha 3+ pezzi (es. LIRN_UN0_APP),
    /// falso se a 2 pezzi (es. LIRP_APP = APP proprio dell'aeroporto). Eccezione: lettera di mezzo <c>G</c>
    /// (es. LIRN_G_APP = precision/PAR di aeroporto militare) = NON di ACC. Per le altre posizioni resta falso (irrilevante).</summary>
    private static bool DefaultIsAccApp(string compose, string? position)
    {
        var p = (position ?? "").Trim().ToUpperInvariant();
        if (p is not ("APP" or "DEP")) return false;
        var parts = compose.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return false;                                          // 2 pezzi = APP proprio dell'aeroporto
        if (string.Equals(parts[1], "G", StringComparison.OrdinalIgnoreCase)) return false;   // _G_ = precision militare, non remotizzato
        return true;
    }
}
