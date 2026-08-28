using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using static Vipi.Application.Messaggio;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Implementazione EF di <see cref="IAirportSectorRepository"/>. Import = upsert dei settori ATC d'aeroporto
/// (DEL/GND/TWR/APP…) dalla sorgente, preservando IsHidden e i limiti admin. L'ACC di competenza è ereditato
/// dall'aeroporto. Niente cancellazioni (i settori spariti dalla sorgente restano; l'admin li nasconde).
/// </summary>
public sealed class EfAirportSectorRepository : IAirportSectorRepository
{
    private readonly VipiDbContext _db;
    private readonly ICallsignRenameService _rinomine;

    /// <inheritdoc cref="EfAccAdminRepository(VipiDbContext, ICallsignRenameService?)"/>
    public EfAirportSectorRepository(VipiDbContext db, ICallsignRenameService? rinomine = null)
    {
        _db = db;
        _rinomine = rinomine ?? new EfCallsignRenameService(db);
    }

    private const int DefaultLowerFt = 0;        // GND
    private const int DefaultUpperFt = 19500;    // limite superiore di default (APP/DEP)
    private const int DefaultTowerUpperFt = 3000;  // le TORRI arrivano a 3000 ft, non a FL195

    /// <summary>
    /// Tetto di default per una posizione priva di limiti dalla sorgente. Le <b>torri</b> valgono 3000 ft
    /// (regola di divisione, committente 24-ago-2026): col vecchio default unico a 19500 ft una TWR
    /// rivendicava fino a FL195 e, essendo più in basso nella scaletta, si prendeva il traffico che stava
    /// lavorando l'APP. Rilevante da quando l'attribuzione del traffico usa questi limiti sul serio
    /// (docs/feature/2026-08-24-servizio-statistiche-atc.md §4.5).
    /// </summary>
    private static int DefaultUpperFor(string? position) =>
        (position ?? "").Trim().ToUpperInvariant() is "TWR" ? DefaultTowerUpperFt : DefaultUpperFt;

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
                s.IsAccApp, s.LimitsFromSource, s.AtcCallsign, s.ImportedAtUtc))
            .ToListAsync(ct);
    }

    public async Task SetHiddenAsync(int id, bool hidden, CancellationToken ct = default)
    {
        var s = await _db.AirportSectors.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new InvalidOperationException(Lingua($"Settore d'aeroporto id {id} inesistente.", $"Airport sector id {id} does not exist."));
        s.IsHidden = hidden;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetLimitsAsync(int id, int? lower, int? upper, CancellationToken ct = default)
    {
        var s = await _db.AirportSectors.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new InvalidOperationException(Lingua($"Settore d'aeroporto id {id} inesistente.", $"Airport sector id {id} does not exist."));
        // Limiti da sorgente = verità primaria: read-only (la UI li disabilita; qui la difesa server).
        if (s.LimitsFromSource)
            throw new InvalidOperationException(Lingua("I limiti di questo settore provengono dalla sorgente (IVAO): non modificabili.", "This sector's limits come from the source (IVAO): they cannot be changed."));
        s.LowerLimit = lower ?? DefaultLowerFt;   // inferiore: vuoto → 0 (GND)
        s.UpperLimit = upper;                      // superiore: vuoto → null = UNL
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetPrimaryAsync(int id, CancellationToken ct = default)
    {
        var s = await _db.AirportSectors.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new InvalidOperationException(Lingua($"Settore d'aeroporto id {id} inesistente.", $"Airport sector id {id} does not exist."));
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
                ?? throw new InvalidOperationException(Lingua($"Settore d'aeroporto id {id} inesistente.", $"Airport sector id {id} does not exist."));
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

    public async Task<IReadOnlyList<TwrShapeRow>> ListTwrShapesAsync(CancellationToken ct = default) =>
        await _db.AirportSectors.AsNoTracking()
            .Where(s => s.Position == "TWR" && !s.IsHidden)
            .Join(_db.Airports.AsNoTracking(), s => s.AirportIcao, a => a.Icao,
                (s, a) => new TwrShapeRow(s.Id, s.ComposePosition, s.AirportIcao, a.Latitude, a.Longitude, s.RegionMapPolygon, s.IsShapeSynthetic))
            .ToListAsync(ct);

    public async Task SetSyntheticShapeAsync(int sectorId, string polygonJson, CancellationToken ct = default)
    {
        var s = await _db.AirportSectors.FirstOrDefaultAsync(x => x.Id == sectorId, ct)
                ?? throw new InvalidOperationException(Lingua($"Settore d'aeroporto id {sectorId} inesistente.", $"Airport sector id {sectorId} does not exist."));
        s.RegionMapPolygon = polygonJson;
        s.IsShapeSynthetic = true;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetRealShapeAsync(int sectorId, string polygonJson, CancellationToken ct = default)
    {
        var s = await _db.AirportSectors.FirstOrDefaultAsync(x => x.Id == sectorId, ct)
                ?? throw new InvalidOperationException(Lingua($"Settore d'aeroporto id {sectorId} inesistente.", $"Airport sector id {sectorId} does not exist."));
        s.RegionMapPolygon = polygonJson;
        s.IsShapeSynthetic = false;   // poligono reale (GitHub): non è un cerchio di ripiego
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AirportPolygonRow>> ListNonSyntheticPolygonsAsync(CancellationToken ct = default) =>
        await _db.AirportSectors.AsNoTracking()
            .Where(s => !s.IsShapeSynthetic && s.RegionMapPolygon != null && s.RegionMapPolygon != "")
            .Select(s => new AirportPolygonRow(s.AirportIcao, s.RegionMapPolygon!))
            .ToListAsync(ct);

    public async Task<(int Created, int Updated)> ImportForAirportAsync(
        string icao, IReadOnlyList<SourceAtcPosition> positions, CancellationToken ct = default)
    {
        icao = Norm(icao);
        var now = DateTime.UtcNow;
        int created = 0, updated = 0;

        // L'ACC di competenza è quello dell'aeroporto. Senza aeroporto/ACC non si importa (FK).
        var airport = await _db.Airports.FirstOrDefaultAsync(a => a.Icao == icao, ct);
        if (airport is null) return (0, 0);
        var accCode = await _db.Accs.AsNoTracking().Where(a => a.Id == airport.AccId).Select(a => a.Code).FirstOrDefaultAsync(ct);
        if (accCode is null) return (0, 0);

        // Coordinate del riferimento aeroporto dal dettaglio postazione (uguali per tutte): centro della shape TWR.
        var coord = positions.FirstOrDefault(p => p.AirportLatitude is not null && p.AirportLongitude is not null);
        if (coord is not null) { airport.Latitude = coord.AirportLatitude; airport.Longitude = coord.AirportLongitude; }

        // Le rinomine PRIMA di leggere `existing`: applicate qui, l'upsert per callsign ritrova le righe al
        // loro posto. ⚠️ Le righe che si confrontano sono quelle di QUESTO aeroporto: la sorgente ci ha appena
        // mandato il suo elenco, e un id che non compare non vuol dire sparito — vuol dire che sta altrove.
        await _rinomine.ApplyAsync(
            CallsignRenameDetector.Detect(
                SourceCatalog.AirportPosition,
                await _db.AirportSectors.AsNoTracking()
                    .Where(x => x.AirportIcao == icao && x.IvaoId != null)
                    .ToDictionaryAsync(x => x.IvaoId!.Value, x => x.ComposePosition, ct),
                positions.Select(p => (p.IvaoId, p.Callsign))),
            ct);

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
                row.IvaoId ??= p.IvaoId;   // backfill: la riga c'era prima che l'identità esistesse
                row.AirportIcao = icao;
                row.AccCode = accCode;
                row.Position = position;
                row.MiddleIdentifier = p.MiddleIdentifier;
                row.AtcCallsign = p.AtcCallsign;
                row.Frequency = p.Frequency;
                if (hasLimits)
                {
                    // ⚠️ Solo una shape VERA sovrascrive: l'assenza non è un ordine di cancellare. Oggi IVAO manda
                    // `[]` su tutte le posizioni, e l'assegnazione secca portava a zero i poligoni presi da GitHub
                    // e i cerchi di ripiego — 83 su 83, misurati sul database vero. Il giro notturno li rimetteva
                    // subito dopo, ma gli altri tre chiamanti (bottone dell'editor, massivo, «Genera documenti»)
                    // no: lì la TWR restava senza area fino al giorno dopo. Vedi PolygonGeometry.HasShape.
                    if (!PolygonGeometry.IsEmptyShape(p.RegionMapPolygon))
                    {
                        row.RegionMapPolygon = p.RegionMapPolygon;
                        row.IsShapeSynthetic = false;   // shape reale dalla sorgente: non è un ripiego
                        // ⚠️ E l'anagrafica riprende il comando PER INTERO. IVAO ha confermato il 26 agosto
                        // 2026 che l'assenza dei poligoni è un guasto loro e che lo sistemeranno, quindi
                        // questa riga scatterà davvero: senza, la riga resterebbe marcata `Sectorfile` e il
                        // gate AIRAC continuerebbe ad applicarsi a una geometria che non ne ha bisogno —
                        // peggio, con un differimento aperto la release pubblicherebbe la vecchia shape del
                        // sectorfile al posto di quella vera, per settimane.
                        row.ShapeSource = ShapeSource.Source;
                        row.RegionMapPolygonInForce = null;
                        row.ShapeAiracCycle = null;
                        row.ShapeForcePublished = false;
                    }
                    // Limiti: la SORGENTE è verità primaria. Se li espone → sovrascrive e li blocca (LimitsFromSource);
                    // se null → l'admin comanda (preserva il suo valore, o default) e restano editabili.
                    if (p.LowerLimit is not null) row.LowerLimit = p.LowerLimit;
                    else row.LowerLimit ??= DefaultLowerFt;
                    if (p.UpperLimit is not null) row.UpperLimit = p.UpperLimit;
                    else row.UpperLimit ??= DefaultUpperFor(position);
                    row.LimitsFromSource = p.LowerLimit is not null || p.UpperLimit is not null;
                }
                else
                {
                    // GND/DEL/ATIS: niente limiti né shape.
                    row.RegionMapPolygon = null;
                    row.LowerLimit = null;
                    row.UpperLimit = null;
                    row.LimitsFromSource = false;
                }
                row.ImportedAtUtc = now;
                updated++;
            }
            else
            {
                _db.AirportSectors.Add(new AirportSector
                {
                    IvaoId = p.IvaoId,
                    ComposePosition = compose,
                    AirportIcao = icao,
                    AccCode = accCode,
                    Position = position,
                    MiddleIdentifier = p.MiddleIdentifier,
                    AtcCallsign = p.AtcCallsign,
                    Frequency = p.Frequency,
                    // Riga nuova: si tiene la shape solo se è una shape. Un `"[]"` in colonna direbbe «ho una
                    // forma, ed è vuota», e i ripieghi cercano proprio chi non ne ha.
                    RegionMapPolygon = hasLimits && !PolygonGeometry.IsEmptyShape(p.RegionMapPolygon) ? p.RegionMapPolygon : null,
                    LowerLimit = hasLimits ? (p.LowerLimit ?? DefaultLowerFt) : null,
                    UpperLimit = hasLimits ? (p.UpperLimit ?? DefaultUpperFor(position)) : null,
                    LimitsFromSource = hasLimits && (p.LowerLimit is not null || p.UpperLimit is not null),
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

    /// <summary>Default "di ACC" di una posizione APP/DEP: vero solo se il pezzo di MEZZO ha più di un carattere
    /// (es. LIRN_UN0_APP → mezzo "UN0" = remotizzato). Falso se a 2 pezzi (es. LIRP_APP = APP proprio dell'aeroporto)
    /// o se il mezzo è un solo carattere (es. LIPE_W_APP / LIPE_E_APP = APP proprio; LIRN_G_APP = precision militare):
    /// NON remotizzato. Per le altre posizioni resta falso (irrilevante).</summary>
    private static bool DefaultIsAccApp(string compose, string? position)
    {
        var p = (position ?? "").Trim().ToUpperInvariant();
        if (p is not ("APP" or "DEP")) return false;
        var parts = compose.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return false;                                          // 2 pezzi = APP proprio dell'aeroporto
        return parts[1].Trim().Length > 1;                                           // mezzo mono-carattere (W/E/G…) = non remotizzato
    }
}
