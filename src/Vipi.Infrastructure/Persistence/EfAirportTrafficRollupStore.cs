using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Stats;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Archivio EF del traffico d'aeroporto consolidato. Sottile per scelta: che cosa chiedere lo decide
/// <see cref="AirportRollupPlanner"/>, quanto è coperto <see cref="AirportCoverage"/>.
/// </summary>
public sealed class EfAirportTrafficRollupStore : IAirportTrafficRollupStore
{
    private readonly VipiDbContext _db;

    public EfAirportTrafficRollupStore(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<string>> AirportsAsync(CancellationToken ct = default) =>
        await _db.Airports.AsNoTracking()
            .Where(a => !a.IsHidden)
            .OrderBy(a => a.Icao)
            .Select(a => a.Icao)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<KnownAirportDay>> KnownDaysAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var da = from.UtcDateTime.Date;
        var a = to.UtcDateTime.Date;

        return await _db.AirportDayTraffic.AsNoTracking()
            .Where(r => r.Day >= da && r.Day <= a)
            .Select(r => new KnownAirportDay(r.Icao, r.Day, r.FetchedUtc))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AirportAtcOpening>> AtcOpeningsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var da = from.UtcDateTime;
        var a = to.UtcDateTime;
        var soglia = (int)StatsCounting.MinCountedSession.TotalSeconds;

        // ⚠️ La stessa soglia di ogni altra lettura: una connessione sotto il minuto non è un'apertura, e
        // contarla direbbe che il campo era «aperto» per il tempo di un errore di collegamento.
        var sessioni = await _db.AtcSessions.AsNoTracking()
            .Where(s => s.StartUtc < a && s.DurationSeconds >= soglia)
            .Where(s => s.EndUtc == null || s.EndUtc > da)
            .Select(s => new { s.Callsign, s.StartUtc, s.EndUtc, s.DurationSeconds })
            .ToListAsync(ct);

        var esito = new List<AirportAtcOpening>(sessioni.Count);
        foreach (var s in sessioni)
        {
            // Il campo lo dice il callsign, e solo per le posizioni che ne dichiarano uno: un CTR comincia
            // per un codice di FIR, e prenderlo per un aeroporto farebbe nascere «arrivi a LIRR».
            if (TrafficStory.StationIcao(s.Callsign) is not { } icao) continue;

            var inizio = new DateTimeOffset(DateTime.SpecifyKind(s.StartUtc, DateTimeKind.Utc));

            // Una sessione ancora aperta finisce «adesso» per quel che ne sappiamo; la durata dichiarata da
            // IVAO è comunque la misura autorevole, non End−Start.
            var fine = s.EndUtc is { } e
                ? new DateTimeOffset(DateTime.SpecifyKind(e, DateTimeKind.Utc))
                : inizio.AddSeconds(s.DurationSeconds);

            if (fine > inizio) esito.Add(new AirportAtcOpening(icao, inizio, fine));
        }

        return esito;
    }

    public async Task<int> SaveAsync(
        IReadOnlyList<AirportDayCount> righe, DateTimeOffset now, CancellationToken ct = default)
    {
        if (righe.Count == 0) return 0;

        var icaos = righe.Select(r => r.Icao).Distinct().ToList();
        var primo = righe.Min(r => r.Day).UtcDateTime.Date;
        var ultimo = righe.Max(r => r.Day).UtcDateTime.Date;

        // Le righe esistenti della sola finestra toccata: caricarle tutte per aggiornarne venti sarebbe
        // leggere l'anno per riscrivere un giorno.
        var esistenti = await _db.AirportDayTraffic
            .Where(r => icaos.Contains(r.Icao) && r.Day >= primo && r.Day <= ultimo)
            .ToDictionaryAsync(r => (r.Icao, r.Day), ct);

        var quando = now.UtcDateTime;

        foreach (var r in righe)
        {
            var giorno = r.Day.UtcDateTime.Date;
            if (!esistenti.TryGetValue((r.Icao, giorno), out var riga))
            {
                riga = new AirportDayTraffic { Icao = r.Icao, Day = giorno };
                _db.AirportDayTraffic.Add(riga);
                esistenti[(r.Icao, giorno)] = riga;
            }

            riga.Inbound = r.Inbound;
            riga.Outbound = r.Outbound;
            riga.Overflight = r.Overflight;
            riga.CoveredMovements = r.Covered;
            riga.AtcMinutes = r.AtcMinutes;
            riga.FetchedUtc = quando;
        }

        await _db.SaveChangesAsync(ct);
        return righe.Count;
    }
}
