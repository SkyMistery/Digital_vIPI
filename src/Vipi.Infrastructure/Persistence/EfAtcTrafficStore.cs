using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Stats;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Archivio EF delle tratte gestite. Sottile: decide <see cref="TrafficLedger"/>, qui si scrive.</summary>
public sealed class EfAtcTrafficStore : IAtcTrafficStore
{
    private readonly VipiDbContext _db;

    public EfAtcTrafficStore(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<long, (IReadOnlyList<TrafficLegRow> Legs, int TrafficMinutes)>> GetLegsAsync(
        IReadOnlyCollection<long> sessionIds, CancellationToken ct = default)
    {
        var esito = new Dictionary<long, (IReadOnlyList<TrafficLegRow>, int)>();
        if (sessionIds.Count == 0) return esito;

        var ids = sessionIds.ToList();

        var minuti = await _db.AtcSessions.AsNoTracking()
            .Where(s => ids.Contains(s.SessionId))
            .ToDictionaryAsync(s => s.SessionId, s => s.TrafficMinutes, ct);

        var righe = await _db.AtcSessionTraffic.AsNoTracking()
            .Where(t => ids.Contains(t.SessionId))
            .ToListAsync(ct);

        foreach (var gruppo in righe.GroupBy(t => t.SessionId))
            esito[gruppo.Key] = (gruppo.Select(Row).ToList(), minuti.GetValueOrDefault(gruppo.Key));

        // Sessioni note ma ancora senza traffico: vanno comunque restituite coi loro minuti.
        foreach (var id in ids.Where(i => !esito.ContainsKey(i) && minuti.ContainsKey(i)))
            esito[id] = (Array.Empty<TrafficLegRow>(), minuti[id]);

        return esito;
    }

    public async Task<int> SaveAsync(TrafficFlush flush, CancellationToken ct = default)
    {
        if (flush.Nothing) return 0;

        var sessioni = flush.Legs.Select(l => l.SessionId)
            .Concat(flush.Counters.Select(c => c.SessionId))
            .Distinct().ToList();

        // ⚠️ Si scrive solo per sessioni che esistono: una riga di traffico senza la sua sessione violerebbe
        // la chiave esterna, e il caso capita davvero (il poller perde il giro in cui la sessione nasce).
        var esistenti = await _db.AtcSessions
            .Where(s => sessioni.Contains(s.SessionId))
            .ToDictionaryAsync(s => s.SessionId, ct);

        var chiavi = flush.Legs
            .Where(l => esistenti.ContainsKey(l.SessionId))
            .Select(l => new { l.SessionId, l.PilotCallsign, l.LegOrdinal })
            .ToList();

        var sessioniDaLeggere = chiavi.Select(k => k.SessionId).Distinct().ToList();
        var attuali = sessioniDaLeggere.Count == 0
            ? new List<AtcSessionTraffic>()
            : await _db.AtcSessionTraffic.Where(t => sessioniDaLeggere.Contains(t.SessionId)).ToListAsync(ct);

        var indice = attuali.ToDictionary(t => (t.SessionId, t.PilotCallsign, t.LegOrdinal));
        var toccate = 0;

        foreach (var l in flush.Legs)
        {
            if (!esistenti.ContainsKey(l.SessionId)) continue;

            if (!indice.TryGetValue((l.SessionId, l.PilotCallsign, l.LegOrdinal), out var riga))
            {
                riga = new AtcSessionTraffic
                {
                    SessionId = l.SessionId,
                    PilotCallsign = l.PilotCallsign,
                    LegOrdinal = l.LegOrdinal,
                    FirstSeenUtc = l.FirstSeenUtc.UtcDateTime,
                    Origin = TrafficOrigin.Aor,
                };
                _db.AtcSessionTraffic.Add(riga);
                indice[(l.SessionId, l.PilotCallsign, l.LegOrdinal)] = riga;
            }

            riga.PilotUserId = l.PilotUserId;
            riga.FlightPlanId = l.FlightPlanId;
            riga.DepIcao = l.DepIcao;
            riga.ArrIcao = l.ArrIcao;
            riga.AircraftIcao = l.AircraftIcao;
            riga.LastSeenUtc = l.LastSeenUtc.UtcDateTime;
            riga.SeenMinutes = l.SeenMinutes;
            riga.SawMovement = l.SawMovement;
            riga.HasObservationGap = l.HasObservationGap;
            toccate++;
        }

        foreach (var c in flush.Counters)
        {
            if (!esistenti.TryGetValue(c.SessionId, out var sessione)) continue;
            sessione.TrafficCount = c.TrafficCount;
            sessione.MovementCount = c.MovementCount;
            sessione.TrafficMinutes = c.TrafficMinutes;
            sessione.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return toccate;
    }

    private static TrafficLegRow Row(AtcSessionTraffic t) => new(
        t.SessionId, t.PilotCallsign, t.LegOrdinal, t.PilotUserId, t.FlightPlanId, t.DepIcao, t.ArrIcao,
        t.AircraftIcao,
        new DateTimeOffset(DateTime.SpecifyKind(t.FirstSeenUtc, DateTimeKind.Utc)),
        new DateTimeOffset(DateTime.SpecifyKind(t.LastSeenUtc, DateTimeKind.Utc)),
        t.SeenMinutes, t.SawMovement, t.HasObservationGap);

    public async Task<(IReadOnlyList<AirportSessionWindow> ToFill, IReadOnlyList<AirportSessionWindow> Concurrent)>
        GetAirportSessionsToFillAsync(DateTimeOffset notBefore, int max, CancellationToken ct = default)
    {
        var soglia = notBefore.UtcDateTime;

        // Solo sessioni CHIUSE: di una ancora in corso la finestra non è nota, e riempirla adesso vorrebbe
        // dire perdere quel che succede dopo. Solo senza traffico: dove il campionamento dal vivo ha già
        // lavorato non si mescola una seconda fonte. Solo mai provate: la marca evita di riprovarci ogni notte.
        var candidate = await _db.AtcSessions.AsNoTracking()
            .Where(s => s.EndUtc != null && s.StartUtc >= soglia
                        && s.TrafficCount == 0 && s.TrafficFilledUtc == null
                        && s.DurationSeconds >= 60)
            .OrderByDescending(s => s.StartUtc)
            .Take(Math.Max(1, max))
            .Select(s => new { s.SessionId, s.Callsign, s.StartUtc, s.EndUtc })
            .ToListAsync(ct);

        var daRiempire = candidate.Select(Finestra).OfType<AirportSessionWindow>().ToList();
        if (daRiempire.Count == 0)
            return (daRiempire, Array.Empty<AirportSessionWindow>());

        // Le concorrenti: stesso aeroporto, tempi che si toccano. Si legge una finestra sola, quella che
        // copre tutte le candidate, e si filtra in memoria — sono poche righe.
        var da = daRiempire.Min(s => s.StartUtc).UtcDateTime;
        var a = daRiempire.Max(s => s.EndUtc).UtcDateTime;

        var vicine = await _db.AtcSessions.AsNoTracking()
            .Where(s => s.EndUtc != null && s.EndUtc >= da && s.StartUtc <= a)
            .Select(s => new { s.SessionId, s.Callsign, s.StartUtc, s.EndUtc })
            .ToListAsync(ct);

        return (daRiempire, vicine.Select(Finestra).OfType<AirportSessionWindow>().ToList());

        static AirportSessionWindow? Finestra(dynamic s)
        {
            var parsed = AirportBackfillPlanner.Parse((string)s.Callsign);
            if (parsed is not { } p) return null;
            return new AirportSessionWindow(
                (long)s.SessionId, (string)s.Callsign, p.Icao, p.Type,
                new DateTimeOffset(DateTime.SpecifyKind((DateTime)s.StartUtc, DateTimeKind.Utc)),
                new DateTimeOffset(DateTime.SpecifyKind((DateTime)s.EndUtc!, DateTimeKind.Utc)));
        }
    }

    public async Task<int> FillAirportMovementsAsync(
        long sessionId, IReadOnlyList<SourceAirportMovement> movements, DateTimeOffset filledAtUtc,
        CancellationToken ct = default)
    {
        var sessione = await _db.AtcSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);
        if (sessione is null) return 0;

        var esistenti = await _db.AtcSessionTraffic
            .Where(t => t.SessionId == sessionId)
            .ToDictionaryAsync(t => (t.PilotCallsign, t.LegOrdinal), ct);

        var scritte = 0;
        foreach (var gruppo in movements.GroupBy(m => m.PilotCallsign, StringComparer.OrdinalIgnoreCase))
        {
            // Lo stesso callsign in arrivo E in partenza nella stessa finestra sono due tratte: è atterrato
            // e poi ripartito. A distinguerle è la ROTTA col verso, non il piano di volo.
            //
            // ⚠️ Qui il piano di volo NON serve, e usarlo era un difetto: alla riconnessione il pilota ne
            // deposita uno nuovo, e sul dato vero `AZA1430 LIRF→LICJ` è finito due volte nella stessa
            // sessione di `LICJ_TWR` — un solo atterraggio contato per due. Dal vivo il caso è coperto dalla
            // finestra temporale (FlightLegResolver), che qui non c'è: della finestra sappiamo solo gli
            // estremi della sessione.
            var tratte = gruppo
                .GroupBy(m => (
                    Dep: (m.DepIcao ?? "").Trim().ToUpperInvariant(),
                    Arr: (m.ArrIcao ?? "").Trim().ToUpperInvariant(),
                    m.Kind))
                .Select((g, i) => (Ordinale: i + 1, Movimento: g.First()))
                .ToList();

            foreach (var (ordinale, m) in tratte)
            {
                if (esistenti.ContainsKey((gruppo.Key, ordinale))) continue;

                _db.AtcSessionTraffic.Add(new AtcSessionTraffic
                {
                    SessionId = sessionId,
                    PilotCallsign = gruppo.Key,
                    LegOrdinal = ordinale,
                    PilotUserId = m.PilotUserId,
                    FlightPlanId = m.FlightPlanId,
                    DepIcao = m.DepIcao,
                    ArrIcao = m.ArrIcao,
                    AircraftIcao = m.AircraftIcao,
                    // ⚠️ La finestra è tutto quel che sappiamo: la sorgente dice CHE il volo c'è stato, non
                    // per quanti minuti fosse in frequenza. First/Last sono gli estremi della sessione e
                    // SeenMinutes resta 0 — un numero inventato sarebbe peggio di un numero assente.
                    FirstSeenUtc = sessione.StartUtc,
                    LastSeenUtc = sessione.EndUtc ?? sessione.StartUtc,
                    SeenMinutes = 0,
                    SawMovement = true,          // un arrivo, una partenza o un sorvolo si è mosso per definizione
                    HasObservationGap = false,
                    Origin = TrafficOrigin.AirportApi,
                });
                scritte++;
            }
        }

        sessione.TrafficCount += scritte;
        sessione.MovementCount += scritte;
        sessione.TrafficFilledUtc = filledAtUtc.UtcDateTime;
        sessione.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return scritte;
    }
}
