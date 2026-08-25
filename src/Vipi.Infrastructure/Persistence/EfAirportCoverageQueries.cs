using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Le letture della sezione «Aeroporti»: si sommano i giorni già consolidati, non si chiama nessuna
/// sorgente. Il traffico di un anno di novantatré campi è una chiamata sola a IVAO per blocco, di notte —
/// qui si legge quel che ne è rimasto.
/// </summary>
public sealed class EfAirportCoverageQueries : IAirportCoverageQueries
{
    private readonly VipiDbContext _db;

    public EfAirportCoverageQueries(VipiDbContext db) => _db = db;

    public async Task<AirportCoverageSummary> ByAirportAsync(
        DateTimeOffset from, DateTimeOffset to, string? accCode = null, CancellationToken ct = default)
    {
        var da = from.UtcDateTime.Date;
        var a = to.UtcDateTime.Date;

        // L'anagrafica prima: serve il nome e l'ACC anche di un campo che nel periodo non ha una riga —
        // «nessun traffico» è una risposta, e un aeroporto che sparisce dalla tabella non lo è.
        var anagrafica = await _db.Airports.AsNoTracking()
            .Where(x => !x.IsHidden)
            .Where(x => accCode == null || x.Acc!.Code == accCode)
            .Select(x => new { x.Icao, x.Name, Acc = x.Acc!.Code })
            .ToListAsync(ct);

        var icaos = anagrafica.Select(x => x.Icao).ToList();

        var giorni = await _db.AirportDayTraffic.AsNoTracking()
            .Where(r => r.Day >= da && r.Day <= a && icaos.Contains(r.Icao))
            .Select(r => new { r.Icao, r.Day, r.Inbound, r.Outbound, r.CoveredMovements, r.AtcMinutes })
            .ToListAsync(ct);

        var perCampo = giorni
            .GroupBy(r => r.Icao, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (
                    Movimenti: g.Sum(r => r.Inbound + r.Outbound),
                    Coperti: g.Sum(r => r.CoveredMovements),
                    Minuti: g.Sum(r => r.AtcMinutes),
                    Giorni: g.Select(r => r.Day).Distinct().Count()),
                StringComparer.OrdinalIgnoreCase);

        var righe = anagrafica
            .Select(x =>
            {
                var c = perCampo.TryGetValue(x.Icao, out var v)
                    ? v
                    : (Movimenti: 0, Coperti: 0, Minuti: 0, Giorni: 0);

                // ⚠️ Il denominatore delle ore aperte sono i giorni CONSOLIDATI di quel campo, non i giorni
                // chiesti: su un periodo recuperato a metà, dividere per il periodo intero direbbe che il
                // campo è aperto la metà di quanto è vero.
                return new AirportCoverageRow(
                    x.Icao, x.Name, x.Acc, c.Movimenti, c.Coperti, c.Minuti, c.Giorni * 24 * 60);
            })
            .OrderByDescending(r => r.Movements)
            .ThenBy(r => r.Icao, StringComparer.Ordinal)
            .ToList();

        var giorniDistinti = giorni.Select(r => r.Day).Distinct().Count();

        // ⚠️ Le DATE toccate, «+1» compreso, non i giorni interi fra le due: il consolidamento scrive una
        // riga per data, e una finestra di trenta giorni ne tocca trentuno. Il «30 su 31» visto a schermo il
        // 25 agosto sembrava un errore di conto e non lo era — mancava davvero la data più vecchia. Chi
        // togliesse il «+1» per far sparire quel messaggio nasconderebbe un giorno mancante, non lo riempirebbe.
        var chiesti = (int)Math.Round((a - da).TotalDays) + 1;

        return new AirportCoverageSummary(
            righe,
            righe.Sum(r => r.Movements),
            righe.Sum(r => r.Covered),
            righe.Sum(r => r.AtcMinutes),
            giorni.Count == 0 ? null : new DateTimeOffset(DateTime.SpecifyKind(giorni.Min(r => r.Day), DateTimeKind.Utc)),
            giorniDistinti,
            chiesti);
    }

    public async Task<IReadOnlyList<(string Code, string Name, int Airports)>> GroupsAsync(
        CancellationToken ct = default)
    {
        var righe = await _db.Airports.AsNoTracking()
            .Where(x => !x.IsHidden)
            .GroupBy(x => new { x.Acc!.Code, x.Acc!.Name })
            .Select(g => new { g.Key.Code, g.Key.Name, Quanti = g.Count() })
            .ToListAsync(ct);

        return righe
            .OrderByDescending(x => x.Quanti)
            .Select(x => (x.Code, x.Name, x.Quanti))
            .ToList();
    }
}
