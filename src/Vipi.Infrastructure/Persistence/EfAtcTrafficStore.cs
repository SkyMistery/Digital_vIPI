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
            riga.FirstPhase = l.FirstPhase;
            riga.LastPhase = l.LastPhase;
            riga.SawAirborne = l.SawAirborne;
            riga.EntryAltitudeFt = l.EntryAltitudeFt;
            riga.ExitAltitudeFt = l.ExitAltitudeFt;
            riga.MaxAltitudeFt = l.MaxAltitudeFt;
            // Con quale forma è stato contato: si riscrive a ogni flush, come le altre misure dell'ultimo
            // avvistamento (carta refactor 15 §3h).
            riga.ShapeSource = l.ShapeSource;

            // ⚠️ La consegna si scrive solo quando c'è: il registro la conosce dal giro in cui è avvenuta e
            // la ripete nei flush successivi, ma una riga riletta dall'archivio dopo un riavvio la riporta
            // com'era. Assegnare `null` sopra un valore già scritto la cancellerebbe.
            riga.HandoffToSessionId = l.HandoffToSessionId ?? riga.HandoffToSessionId;
            riga.HandoffFromSessionId = l.HandoffFromSessionId ?? riga.HandoffFromSessionId;
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
        t.SeenMinutes, t.SawMovement, t.HasObservationGap,
        t.FirstPhase, t.LastPhase, t.SawAirborne, t.EntryAltitudeFt, t.ExitAltitudeFt, t.MaxAltitudeFt,
        t.HandoffToSessionId, t.HandoffFromSessionId, t.ShapeSource);

    public async Task<(IReadOnlyList<AirportSessionWindow> ToFill, IReadOnlyList<AirportSessionWindow> Concurrent)>
        GetAirportSessionsToFillAsync(DateTimeOffset notBefore, int max, CancellationToken ct = default)
    {
        var soglia = notBefore.UtcDateTime;

        // Solo sessioni CHIUSE: di una ancora in corso la finestra non è nota, e riempirla adesso vorrebbe
        // dire perdere quel che succede dopo. Solo senza traffico: dove il campionamento dal vivo ha già
        // lavorato non si mescola una seconda fonte. Solo mai provate: la marca evita di riprovarci ogni notte.
        var candidate = await _db.AtcSessions.AsNoTracking().DiDivisione()
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

        var vicine = await _db.AtcSessions.AsNoTracking().DiDivisione()
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
                    // ⚠️ Questa tratta non l'ha contata nessuna forma: la dà la sorgente. `Source` qui vuol
                    // dire «l'anagrafica», ed è la lettura giusta — non c'è nessun confine nostro di mezzo.
                    ShapeSource = Vipi.Domain.ShapeSource.Source,
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

    public async Task<int> PruneTrafficAsync(
        DateTimeOffset notAfter, int batch, CancellationToken ct = default)
    {
        if (batch <= 0) return 0;

        var limite = notAfter.UtcDateTime;

        // ⚠️ `RemoveRange` e non `ExecuteDelete`: il change-tracker di questo contesto è condiviso col
        // resto del giro, e una cancellazione fuori dal tracker lo lascerebbe a raccontare righe che non
        // esistono più. È una lezione già pagata (audit del 30 luglio).
        var righe = await _db.AtcSessionTraffic
            .Where(t => t.LastSeenUtc < limite)
            .OrderBy(t => t.LastSeenUtc)
            .Take(batch)
            .ToListAsync(ct);

        if (righe.Count == 0) return 0;

        _db.AtcSessionTraffic.RemoveRange(righe);
        await _db.SaveChangesAsync(ct);
        return righe.Count;
    }

    public async Task<int> RollupAndPruneSessionsAsync(
        DateTimeOffset notAfter, int batch, CancellationToken ct = default)
    {
        if (batch <= 0) return 0;
        var limite = notAfter.UtcDateTime;

        // ⚠️ Solo le sessioni CHIUSE: una ancora aperta non ha una durata definitiva, e riassumerla
        // vorrebbe dire congelare un numero sbagliato. Una connessione aperta da più di un anno non
        // esiste, ma se esistesse sarebbe un guasto da guardare, non da cancellare.
        var righe = await _db.AtcSessions
            .Where(s => s.StartUtc < limite && s.EndUtc != null)
            .OrderBy(s => s.StartUtc)
            .Take(batch)
            .ToListAsync(ct);
        if (righe.Count == 0) return 0;

        // Riassunto e cancellazione nella STESSA transazione: separate, un'interruzione fra le due
        // conterebbe due volte lo stesso mese al giro successivo.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var adesso = DateTime.UtcNow;

        // ⚠️ Il riassunto è SOLO della divisione: <c>AtcMonthRollup</c> è la memoria lunga delle ore
        // italiane (mese · persona · callsign) e regge la classifica. Le sessioni fuori divisione si
        // cancellano e basta, alla stessa scadenza — dodici mesi — senza lasciare niente dietro: sono
        // archivio, non un conto di qualcuno, e riassumerle vorrebbe dire mettere il pianeta in classifica.
        foreach (var g in righe.Where(s => !s.IsOutsideDivision).GroupBy(s => (
            Mese: new DateTime(s.StartUtc.Year, s.StartUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            s.UserId, s.Callsign)))
        {
            var riga = await _db.AtcMonthRollups.FirstOrDefaultAsync(
                x => x.Month == g.Key.Mese && x.UserId == g.Key.UserId && x.Callsign == g.Key.Callsign, ct);
            if (riga is null)
            {
                riga = new AtcMonthRollup { Month = g.Key.Mese, UserId = g.Key.UserId, Callsign = g.Key.Callsign };
                _db.AtcMonthRollups.Add(riga);
            }

            riga.Position ??= g.Select(s => s.Position).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
            riga.Sessions += g.Count();
            riga.Seconds += g.Sum(s => (long)s.DurationSeconds);
            riga.TrafficSeen += g.Sum(s => s.TrafficCount);
            riga.TrafficMoved += g.Sum(s => s.MovementCount);
            riga.BusyMinutes += g.Sum(s => s.TrafficMinutes);
            riga.UpdatedUtc = adesso;
        }

        _db.AtcSessions.RemoveRange(righe);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return righe.Count;
    }
}
