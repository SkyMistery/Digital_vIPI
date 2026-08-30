using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Domain;

namespace Vipi.Application.Stats;

/// <summary>Una tratta come sta in archivio o in memoria: la riga che il poller aggiorna giro dopo giro.</summary>
public sealed record TrafficLegRow(
    long SessionId,
    string PilotCallsign,
    int LegOrdinal,
    int PilotUserId,
    long? FlightPlanId,
    string? DepIcao,
    string? ArrIcao,
    string? AircraftIcao,
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastSeenUtc,
    int SeenMinutes,
    bool SawMovement,
    bool HasObservationGap,
    FlightPhase? FirstPhase = null,
    FlightPhase? LastPhase = null,
    bool SawAirborne = false,
    int? EntryAltitudeFt = null,
    int? ExitAltitudeFt = null,
    int? MaxAltitudeFt = null,
    long? HandoffToSessionId = null,
    long? HandoffFromSessionId = null,
    Vipi.Domain.ShapeSource ShapeSource = Vipi.Domain.ShapeSource.Source);

/// <summary>
/// Un aeroplano visto in un giro, come lo passa il poller: chi è, cosa dice il suo piano di volo, in che
/// fase si trova e a che quota.
///
/// <para>È un record e non otto parametri perché <see cref="TrafficLedger.Observe"/> ne aveva già sette:
/// l'ottavo e il nono (fase e quota) l'avrebbero reso una firma che si sbaglia a leggere.</para>
/// </summary>
public sealed record LegObservation(
    string PilotCallsign,
    int PilotUserId,
    long? FlightPlanId,
    string? DepIcao,
    string? ArrIcao,
    string? AircraftIcao,
    FlightPhase Phase,
    double AltitudeFt,
    Vipi.Domain.ShapeSource ShapeSource = Vipi.Domain.ShapeSource.Source);

/// <summary>Contatori di una sessione, ricalcolati dalle sue tratte.</summary>
public sealed record SessionCounters(long SessionId, int TrafficCount, int MovementCount, int TrafficMinutes);

/// <summary>Quel che c'è da scrivere: le tratte cambiate e i contatori delle loro sessioni.</summary>
public sealed record TrafficFlush(IReadOnlyList<TrafficLegRow> Legs, IReadOnlyList<SessionCounters> Counters)
{
    public bool Nothing => Legs.Count == 0 && Counters.Count == 0;
}

/// <summary>
/// Lo stato delle tratte in corso, tenuto <b>in memoria</b> fra un giro di poll e l'altro.
///
/// <para><b>Perché non si scrive tutto ogni minuto.</b> Il poll gira ogni 60 secondi, ma il database non deve
/// vederlo: una riga cambia davvero solo quando compare un aereo nuovo. Il resto del tempo si aggiornano
/// «ultimo avvistamento» e minuti, che possono aspettare un checkpoint. Così le scritture calano di circa
/// dieci volte, e il rischio massimo di un riavvio è un checkpoint di <c>LastSeenUtc</c> — non un dato perso,
/// visto che il conteggio dei minuti riparte da quel che c'è in archivio.</para>
///
/// <para><b>I minuti si contano per giro</b>, non come <c>ultimo − primo</c>: chi esce dal settore e rientra
/// nella stessa tratta regalerebbe al controllore anche il tempo in cui non c'era.</para>
///
/// <para>Puro nel senso che conta: nessun I/O e nessun orologio interno (l'istante lo passa il chiamante).
/// Non è thread-safe: lo usa il solo poller, un giro alla volta.</para>
/// </summary>
public sealed class TrafficLedger
{
    private readonly Dictionary<long, Sessione> _sessioni = new();

    private sealed class Sessione
    {
        public readonly List<Leg> Legs = new();
        public int MinutiConTraffico;
        public bool Sporca;
        public DateTimeOffset UltimaScrittura;
    }

    private sealed class Leg
    {
        public required string PilotCallsign { get; init; }
        public required int LegOrdinal { get; init; }
        public required int PilotUserId { get; init; }
        public long? FlightPlanId { get; set; }
        public string? DepIcao { get; set; }
        public string? ArrIcao { get; set; }
        public string? AircraftIcao { get; set; }
        public DateTimeOffset FirstSeenUtc { get; set; }
        public DateTimeOffset LastSeenUtc { get; set; }
        public int SeenMinutes { get; set; }
        public bool SawMovement { get; set; }
        public bool HasObservationGap { get; set; }
        public FlightPhase? FirstPhase { get; set; }
        public FlightPhase? LastPhase { get; set; }
        public bool SawAirborne { get; set; }
        public int? EntryAltitudeFt { get; set; }
        public int? ExitAltitudeFt { get; set; }
        public int? MaxAltitudeFt { get; set; }
        public long? HandoffToSessionId { get; set; }
        public long? HandoffFromSessionId { get; set; }

        /// <summary>Con quale forma è stato contato l'ultimo avvistamento: si scrive accanto alla tratta.</summary>
        public Vipi.Domain.ShapeSource ShapeSource { get; set; }
    }

    /// <summary>Sessioni di cui il registro sa già qualcosa: il chiamante non deve rileggerle dall'archivio.</summary>
    public bool Knows(long sessionId) => _sessioni.ContainsKey(sessionId);

    /// <summary>
    /// Rimette in memoria le tratte già in archivio di una sessione. Serve dopo un riavvio: senza, il poller
    /// ripartirebbe da zero su una sessione ancora in corso e i minuti tornerebbero indietro.
    /// </summary>
    /// <param name="trafficMinutes">I minuti «occupato» già contati in archivio: senza, dopo un riavvio il
    /// contatore ripartirebbe da zero e sovrascriverebbe con un numero più piccolo quello vero.</param>
    public void Hydrate(long sessionId, IEnumerable<TrafficLegRow> legs, int trafficMinutes = 0)
    {
        var s = new Sessione { UltimaScrittura = DateTimeOffset.MinValue, MinutiConTraffico = trafficMinutes };
        foreach (var l in legs)
            s.Legs.Add(new Leg
            {
                PilotCallsign = l.PilotCallsign,
                LegOrdinal = l.LegOrdinal,
                PilotUserId = l.PilotUserId,
                FlightPlanId = l.FlightPlanId,
                DepIcao = l.DepIcao,
                ArrIcao = l.ArrIcao,
                AircraftIcao = l.AircraftIcao,
                FirstSeenUtc = l.FirstSeenUtc,
                LastSeenUtc = l.LastSeenUtc,
                SeenMinutes = l.SeenMinutes,
                SawMovement = l.SawMovement,
                HasObservationGap = l.HasObservationGap,
                FirstPhase = l.FirstPhase,
                LastPhase = l.LastPhase,
                SawAirborne = l.SawAirborne,
                EntryAltitudeFt = l.EntryAltitudeFt,
                ExitAltitudeFt = l.ExitAltitudeFt,
                MaxAltitudeFt = l.MaxAltitudeFt,
                HandoffToSessionId = l.HandoffToSessionId,
                HandoffFromSessionId = l.HandoffFromSessionId,
                ShapeSource = l.ShapeSource,
            });
        _sessioni[sessionId] = s;
    }

    /// <summary>
    /// Registra un aeroplano visto dentro l'area di una sessione. Ritorna <c>true</c> se ha aperto una
    /// <b>tratta nuova</b> — il caso in cui vale la pena scrivere subito, invece di aspettare il checkpoint.
    /// </summary>
    public bool Observe(long sessionId, LegObservation obs, DateTimeOffset now)
    {
        var (pilotCallsign, pilotUserId, flightPlanId, depIcao, arrIcao, aircraftIcao, phase, altitudeFt,
             shapeSource) = obs;

        if (!_sessioni.TryGetValue(sessionId, out var s))
            _sessioni[sessionId] = s = new Sessione { UltimaScrittura = now };

        var aperte = s.Legs
            .Select(l => new OpenLeg(l.PilotCallsign, l.FlightPlanId, l.DepIcao, l.ArrIcao, l.LegOrdinal, l.LastSeenUtc))
            .ToList();

        var trovata = FlightLegResolver.Match(aperte, pilotCallsign, flightPlanId, depIcao, arrIcao, now);
        var nuova = false;

        Leg leg;
        if (trovata is null)
        {
            leg = new Leg
            {
                PilotCallsign = pilotCallsign,
                LegOrdinal = FlightLegResolver.NextOrdinal(aperte, pilotCallsign),
                PilotUserId = pilotUserId,
                FlightPlanId = flightPlanId,
                DepIcao = depIcao,
                ArrIcao = arrIcao,
                AircraftIcao = aircraftIcao,
                FirstSeenUtc = now,
                LastSeenUtc = now,
                SeenMinutes = 0,
                FirstPhase = phase,
                EntryAltitudeFt = Piedi(altitudeFt),
            };
            s.Legs.Add(leg);
            nuova = true;
        }
        else
        {
            leg = s.Legs.First(l => l.PilotCallsign == trovata.PilotCallsign && l.LegOrdinal == trovata.Ordinal);

            // Il buco è nostro (poller fermo, deploy, rete): la tratta non si spezza, ma va detto su QUESTO
            // volo che i suoi minuti sono incompleti.
            if (FlightLegResolver.HasObservationGap(trovata, now)) leg.HasObservationGap = true;

            // Il piano di volo può arrivare dopo il primo avvistamento (VFR che lo deposita in volo).
            leg.FlightPlanId ??= flightPlanId;
            leg.DepIcao ??= depIcao;
            leg.ArrIcao ??= arrIcao;
            leg.AircraftIcao ??= aircraftIcao;
        }

        leg.LastSeenUtc = now;
        leg.SeenMinutes++;
        if (FlightPhases.IsMovement(phase)) leg.SawMovement = true;

        // La fase dell'ULTIMO avvistamento, non «la fase del volo»: insieme alla prima è ciò che distingue
        // una partenza da un arrivo da un sorvolo. ⚠️ `SawAirborne` è cumulativo apposta: senza, di un volo con
        // prima fase Airborne e ultima Parked non si saprebbe se in mezzo l'abbiamo visto volare o se è
        // rientrato al parcheggio rullando.
        leg.LastPhase = phase;
        if (phase == FlightPhase.Airborne) leg.SawAirborne = true;

        // ⚠️ La forma con cui è stato contato: quella dell'ULTIMO avvistamento. Se un settore viene agganciato
        // mentre un volo è dentro, la tratta finisce marcata con la forma che l'ha contata per ultima — che è
        // la risposta giusta a «con quale confine è stato contato questo?», visto che i minuti si sommano.
        leg.ShapeSource = shapeSource;

        var quota = Piedi(altitudeFt);
        leg.EntryAltitudeFt ??= quota;
        leg.ExitAltitudeFt = quota;
        if (quota is { } q && (leg.MaxAltitudeFt is null || q > leg.MaxAltitudeFt)) leg.MaxAltitudeFt = q;

        s.Sporca = true;
        return nuova;
    }

    /// <summary>
    /// Segna che <paramref name="pilotCallsign"/> è passato dalla sessione <paramref name="fromSessionId"/>
    /// alla sessione <paramref name="toSessionId"/>: una <b>consegna</b>.
    ///
    /// <para>⚠️ Si scrive sulla tratta <b>aperta più di recente</b> di ciascuna delle due sessioni, non su
    /// tutte: chi ha fatto due tratte nello stesso turno (LIRF→LIRN, poi LIRN→LIRF) consegna quella in corso.
    /// Se una delle due sessioni non è più in memoria non si scrive niente — meglio una consegna mancante
    /// che una attribuita a caso.</para>
    /// </summary>
    public void NoteHandoff(long fromSessionId, long toSessionId, string pilotCallsign)
    {
        if (fromSessionId == toSessionId) return;

        var uscente = Ultima(fromSessionId, pilotCallsign);
        var entrante = Ultima(toSessionId, pilotCallsign);
        if (uscente is null || entrante is null) return;

        uscente.HandoffToSessionId = toSessionId;
        entrante.HandoffFromSessionId = fromSessionId;
        _sessioni[fromSessionId].Sporca = true;
        _sessioni[toSessionId].Sporca = true;
    }

    private Leg? Ultima(long sessionId, string pilotCallsign) =>
        _sessioni.TryGetValue(sessionId, out var s)
            ? s.Legs.Where(l => string.Equals(l.PilotCallsign, pilotCallsign, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(l => l.LegOrdinal).FirstOrDefault()
            : null;

    /// <summary>
    /// Quota in piedi interi. ⚠️ Sotto zero non si scrive: la sorgente dà quote negative per gli aerei al
    /// suolo sotto la pressione standard, e «−200 ft» in una scheda di volo sembra un errore nostro.
    /// </summary>
    private static int? Piedi(double altitudeFt) =>
        double.IsFinite(altitudeFt) && altitudeFt >= 0 ? (int)Math.Round(altitudeFt) : null;

    /// <summary>Chiude il giro: la sessione ha avuto traffico in questo minuto (per i minuti «occupato»).</summary>
    public void EndPoll(long sessionId, bool hadTraffic)
    {
        if (!_sessioni.TryGetValue(sessionId, out var s)) return;
        if (hadTraffic) s.MinutiConTraffico++;
    }

    /// <summary>
    /// Cosa scrivere adesso: le sessioni con una tratta nuova (<paramref name="subito"/>) e quelle il cui
    /// ultimo salvataggio è più vecchio di <paramref name="checkpoint"/>.
    /// </summary>
    public TrafficFlush Take(DateTimeOffset now, TimeSpan checkpoint, IReadOnlySet<long>? subito = null)
    {
        var legs = new List<TrafficLegRow>();
        var counters = new List<SessionCounters>();

        foreach (var (id, s) in _sessioni)
        {
            if (!s.Sporca) continue;
            var forzata = subito?.Contains(id) == true;
            if (!forzata && now - s.UltimaScrittura < checkpoint) continue;

            legs.AddRange(s.Legs.Select(l => Row(id, l)));
            counters.Add(Counters(id, s));
            s.Sporca = false;
            s.UltimaScrittura = now;
        }

        return new TrafficFlush(legs, counters);
    }

    /// <summary>Tutto quel che c'è in memoria, senza guardare i checkpoint: per lo spegnimento dell'applicazione.</summary>
    public TrafficFlush TakeAll(DateTimeOffset now) => TakeOnly(_sessioni.Keys.ToHashSet(), now);

    /// <summary>
    /// Come <see cref="TakeAll"/> ma per le sole sessioni indicate. ⚠️ Serve perché segnare «salvata» una
    /// sessione che non si sta scrivendo le farebbe perdere il proprio checkpoint: era il difetto della
    /// chiusura, che prendeva tutto per scriverne una parte.
    /// </summary>
    public TrafficFlush TakeOnly(IReadOnlySet<long> sessionIds, DateTimeOffset now)
    {
        var legs = new List<TrafficLegRow>();
        var counters = new List<SessionCounters>();

        foreach (var (id, s) in _sessioni)
        {
            if (!sessionIds.Contains(id)) continue;
            legs.AddRange(s.Legs.Select(l => Row(id, l)));
            counters.Add(Counters(id, s));
            s.Sporca = false;
            s.UltimaScrittura = now;
        }
        return new TrafficFlush(legs, counters);
    }

    /// <summary>Dimentica una sessione (chiusa e già scritta): il registro non deve crescere per sempre.</summary>
    public void Forget(long sessionId) => _sessioni.Remove(sessionId);

    /// <summary>Sessioni attualmente in memoria.</summary>
    public IReadOnlyCollection<long> Sessions => _sessioni.Keys;

    private static TrafficLegRow Row(long sessionId, Leg l) => new(
        sessionId, l.PilotCallsign, l.LegOrdinal, l.PilotUserId, l.FlightPlanId, l.DepIcao, l.ArrIcao,
        l.AircraftIcao, l.FirstSeenUtc, l.LastSeenUtc, l.SeenMinutes, l.SawMovement, l.HasObservationGap,
        l.FirstPhase, l.LastPhase, l.SawAirborne, l.EntryAltitudeFt, l.ExitAltitudeFt, l.MaxAltitudeFt,
        l.HandoffToSessionId, l.HandoffFromSessionId, l.ShapeSource);

    private static SessionCounters Counters(long sessionId, Sessione s) => new(
        sessionId,
        TrafficCount: s.Legs.Count,
        MovementCount: s.Legs.Count(l => l.SawMovement),
        TrafficMinutes: s.MinutiConTraffico);
}
