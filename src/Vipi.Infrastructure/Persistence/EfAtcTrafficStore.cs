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
}
