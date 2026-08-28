using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.Application.Stats;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Le letture delle pagine statistiche.
///
/// <para>⚠️ Il filtro delle connessioni-lampo (&lt; 60 s) è <b>uno solo</b>, in <see cref="Contate"/>, e ogni
/// lettura ci passa: una soglia ripetuta in sei query è una soglia che un giorno sarà diversa in una di esse.</para>
/// </summary>
public sealed class EfAtcStatsQueries : IAtcStatsQueries
{
    private readonly VipiDbContext _db;

    public EfAtcStatsQueries(VipiDbContext db) => _db = db;

    private IQueryable<AtcSession> Contate(int? userId, DateTimeOffset from, DateTimeOffset to)
    {
        var da = from.UtcDateTime;
        var a = to.UtcDateTime;
        var soglia = (int)StatsCounting.MinCountedSession.TotalSeconds;

        // ⚠️ DiDivisione() sta qui, nell'imbuto di ogni lettura: dal 28 agosto 2026 la tabella contiene
        // anche le postazioni del resto del mondo, che sono la maggioranza delle righe. Senza questo filtro
        // le ore, la classifica e i movimenti della divisione diventerebbero quelli del pianeta.
        var q = _db.AtcSessions.AsNoTracking().DiDivisione()
            .Where(s => s.StartUtc >= da && s.StartUtc <= a && s.DurationSeconds >= soglia);

        return userId is { } vid ? q.Where(s => s.UserId == vid) : q;
    }

    public async Task<StatsTotals> TotalsAsync(
        int? userId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var q = Contate(userId, from, to);

        // Un solo giro sul database: i turni sono il conteggio dei distinti, non una seconda query.
        var righe = await q
            .Select(s => new { s.ShiftKey, s.DurationSeconds, s.MovementCount, s.TrafficCount })
            .ToListAsync(ct);

        return new StatsTotals(
            Sessions: righe.Count,
            Shifts: righe.Select(r => r.ShiftKey).Distinct().Count(),
            Seconds: righe.Sum(r => (long)r.DurationSeconds),
            Movements: righe.Sum(r => r.MovementCount),
            Presences: righe.Sum(r => r.TrafficCount));
    }

    public async Task<IReadOnlyList<StatsByKey>> ByPositionAsync(
        int? userId, DateTimeOffset from, DateTimeOffset to, int limit = 20, CancellationToken ct = default)
    {
        limit = Math.Max(1, limit);

        // ⚠️ La proiezione del raggruppamento va in un tipo ANONIMO, non nel record: EF non sa tradurre un
        // `GroupBy` che costruisce un record, e lancia a runtime — cioè con la pagina già aperta, non in
        // compilazione. Trovato aprendo la pagina davvero, ed è il motivo per cui questa classe ora ha i test.
        var gruppi = Contate(userId, from, to)
            .GroupBy(s => s.Callsign)
            .Select(g => new
            {
                Key = g.Key,
                Sessions = g.Count(),
                Seconds = g.Sum(s => s.DurationSeconds),
                Movements = g.Sum(s => s.MovementCount),
            })
            .OrderByDescending(r => r.Seconds);

        // Le sessioni portano il nominativo di ALLORA, e giustamente: dicono un fatto. Ma una postazione
        // rinominata a giugno non deve comparire come due righe che si dividono le ore. Si traduce in lettura.
        var storia = await StoriaDeiNominativiAsync(ct);
        if (storia.IsEmpty)
            return (await gruppi.Take(limit).ToListAsync(ct))
                .Select(r => new StatsByKey(r.Key, r.Sessions, r.Seconds, r.Movements)).ToList();

        // ⚠️ Con le rinomine in mezzo il `Take` NON può restare nel database: due righe che si fondono possono
        // entrare fra le prime dopo essere state sommate, e tagliare prima le escluderebbe. Si tronca dopo, e
        // le righe da fondere sono poche — sul database vero le postazioni distinte sono ~200.
        return (await gruppi.ToListAsync(ct))
            .GroupBy(r => storia.Canonical(r.Key), StringComparer.OrdinalIgnoreCase)
            .Select(g => new StatsByKey(g.Key, g.Sum(r => r.Sessions), g.Sum(r => (long)r.Seconds),
                g.Sum(r => r.Movements)))
            .OrderByDescending(r => r.Seconds)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Le rinomine in archivio, per tradurre i nominativi storici. Normalmente la tabella è vuota e il
    /// chiamante non fa niente di diverso.
    /// </summary>
    private async Task<CallsignHistory> StoriaDeiNominativiAsync(CancellationToken ct)
    {
        var coppie = await _db.CallsignAliases.AsNoTracking()
            .Select(a => new { a.OldCallsign, a.NewCallsign })
            .ToListAsync(ct);
        return new CallsignHistory(coppie.Select(a => (a.OldCallsign, a.NewCallsign)));
    }

    public async Task<IReadOnlyList<StatsByKey>> ByMonthAsync(
        int? userId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        // Il raggruppamento per mese si fa in memoria: le funzioni di data cambiano da un provider all'altro
        // (SQLite, Postgres, MariaDB) e qui le righe sono quelle di un anno di una persona, non un archivio.
        var righe = await Contate(userId, from, to)
            .Select(s => new { s.StartUtc, s.DurationSeconds, s.MovementCount })
            .ToListAsync(ct);

        return righe
            .GroupBy(r => r.StartUtc.ToString("yyyy-MM"))
            .Select(g => new StatsByKey(
                g.Key, g.Count(), g.Sum(r => (long)r.DurationSeconds), g.Sum(r => r.MovementCount)))
            .OrderBy(r => r.Key, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<IReadOnlyList<StatsSessionRow>> SessionsAsync(
        int? userId, DateTimeOffset from, DateTimeOffset to, int limit = 50, CancellationToken ct = default)
    {
        var righe = await Contate(userId, from, to)
            .OrderByDescending(s => s.StartUtc)
            .Take(Math.Max(1, limit))
            .ToListAsync(ct);

        return righe.Select(Riga).ToList();
    }

    public async Task<StatsSessionDetail?> SessionAsync(long sessionId, CancellationToken ct = default)
    {
        var sessione = await _db.AtcSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);
        if (sessione is null) return null;

        var traffico = await _db.AtcSessionTraffic.AsNoTracking()
            .Where(t => t.SessionId == sessionId)
            .OrderBy(t => t.FirstSeenUtc).ThenBy(t => t.PilotCallsign)
            .ToListAsync(ct);

        // I callsign delle consegne: due id sulla riga, e un giro solo per tradurli. ⚠️ Non è una join
        // sulla tratta perché la sessione dall'altra parte può non esistere più (potatura del dettaglio):
        // quel che manca resta `null`, e la targhetta lo dice senza collegamento.
        var idConsegne = traffico
            .SelectMany(t => new[] { t.HandoffToSessionId, t.HandoffFromSessionId })
            .OfType<long>().Distinct().ToList();

        var callsignConsegne = idConsegne.Count == 0
            ? new Dictionary<long, string>()
            : await _db.AtcSessions.AsNoTracking()
                .Where(x => idConsegne.Contains(x.SessionId))
                .ToDictionaryAsync(x => x.SessionId, x => x.Callsign, ct);

        var piste = await _db.AtcSessionRunways.AsNoTracking()
            .Where(r => r.SessionId == sessionId)
            .OrderBy(r => r.FromUtc)
            .Select(r => new { r.FromUtc, r.Arrival, r.Departure })
            .ToListAsync(ct);

        return new StatsSessionDetail(
            Riga(sessione),
            traffico.Select(t => new StatsTrafficRow(
                t.PilotCallsign, t.LegOrdinal, t.DepIcao, t.ArrIcao, t.AircraftIcao,
                Utc(t.FirstSeenUtc), Utc(t.LastSeenUtc), t.SeenMinutes,
                t.SawMovement, t.HasObservationGap, t.Origin,
                t.FirstPhase, t.LastPhase, t.SawAirborne,
                t.EntryAltitudeFt, t.ExitAltitudeFt, t.MaxAltitudeFt,
                Consegna(callsignConsegne, t.HandoffToSessionId),
                Consegna(callsignConsegne, t.HandoffFromSessionId))).ToList(),
            piste.Select(r => new StatsRunwayRow(Utc(r.FromUtc), r.Arrival, r.Departure)).ToList());
    }

    public async Task<IReadOnlyList<ControllerRanking>> TopControllersAsync(
        DateTimeOffset from, DateTimeOffset to, int limit = 20, CancellationToken ct = default)
    {
        var righe = await Contate(null, from, to)
            .Select(s => new { s.UserId, s.ShiftKey, s.DurationSeconds, s.MovementCount })
            .ToListAsync(ct);

        return righe
            .GroupBy(r => r.UserId)
            .Select(g => new ControllerRanking(
                g.Key,
                g.Select(r => r.ShiftKey).Distinct().Count(),
                g.Sum(r => (long)r.DurationSeconds),
                g.Sum(r => r.MovementCount)))
            .OrderByDescending(r => r.Seconds)
            .Take(Math.Max(1, limit))
            .ToList();
    }

    public async Task<IReadOnlyList<CoverageCell>> CoverageAsync(
        int? userId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var righe = await Contate(userId, from, to)
            .Select(s => new { s.StartUtc, s.EndUtc, s.DurationSeconds })
            .ToListAsync(ct);

        if (righe.Count == 0) return CoverageGrid.Build(Array.Empty<OnlineSpan>(), from, to);

        // ⚠️ La fine si prende da EndUtc quando c'è; per una sessione ancora aperta si usa la DURATA
        // dichiarata dalla sorgente — che è il dato autorevole — invece di tirarla fino ad adesso.
        var spans = righe
            .Select(r => new OnlineSpan(
                Utc(r.StartUtc),
                r.EndUtc is { } fine ? Utc(fine) : Utc(r.StartUtc).AddSeconds(r.DurationSeconds)))
            .ToList();

        // ⚠️ La finestra si stringe al periodo di cui abbiamo DAVVERO dati, e non è un dettaglio: chiedere
        // dodici mesi a un archivio che ne contiene una settimana dà una griglia di «2%» in ogni casella —
        // vera, inutile e scoraggiante. Con l'inizio vero, il lunedì sera si vede che è coperto.
        var primo = spans.Min(s => s.StartUtc);
        var inizio = primo > from ? primo : from;

        return CoverageGrid.Build(spans, inizio, to);
    }

    public Task<IReadOnlyList<StatsByKey>> TopAirportsAsync(
        int? userId, DateTimeOffset from, DateTimeOffset to, int limit = 15, CancellationToken ct = default) =>
        PerChiave(userId, from, to, limit, aeroporti: true, ct);

    public Task<IReadOnlyList<StatsByKey>> TopAircraftAsync(
        int? userId, DateTimeOffset from, DateTimeOffset to, int limit = 15, CancellationToken ct = default) =>
        PerChiave(userId, from, to, limit, aeroporti: false, ct);

    /// <summary>
    /// Gli aeroporti gestiti, col traffico da e per ciascuno. Il campo lo dice il <b>callsign</b> per le
    /// postazioni d’aeroporto e la <b>geometria</b> per i settori d’area.
    ///
    /// <para>⚠️ Le due strade non sono un doppione: <c>LIRF_TWR</c> porta l’ICAO scritto nel nome, e quello
    /// è storia — non cambierà mai più. <c>LIRR_NE_CTR</c> no, e per lui l’unica risposta possibile è
    /// «quali aeroporti stanno dentro il mio poligono», che è il poligono di <b>oggi</b>: una risettorizzazione
    /// cambia i numeri dei turni passati. Scelta consapevole del committente il 25 agosto 2026, contro
    /// l’alternativa di congelare il dato al poll (§15 della carta).</para>
    ///
    /// <para>⚠️ Non si usa l’albero dei settori (<c>Airport.ParentCallsign</c>): è un campo che l’admin
    /// compila a mano, e il 25 agosto 2026 lo avevano <b>31 aeroporti su 93</b>, con <b>12 CTR su 140</b>
    /// che avessero qualcosa sotto. I poligoni invece ci sono tutti (153 su 153) e le coordinate le hanno
    /// 84 aeroporti su 93 — i 9 che mancano sono voci di FIR/TMA («Roma TMA», «Milano TMA») e sei campi
    /// minuscoli. Un elenco vuoto per metà della divisione sarebbe stato uno zero che sembra un dato.</para>
    /// </summary>
    public async Task<IReadOnlyList<StatsByKey>> ManagedAirportsAsync(
        int? userId, DateTimeOffset from, DateTimeOffset to, int limit = 15, CancellationToken ct = default)
    {
        var sessioni = await Contate(userId, from, to)
            .Select(s => new { s.SessionId, s.Callsign })
            .ToListAsync(ct);
        if (sessioni.Count == 0) return Array.Empty<StatsByKey>();

        // Due famiglie di sessioni, due modi di sapere qual è il campo. Chi non ricade in nessuna delle due
        // (un FSS senza poligono, un callsign storto) semplicemente non porta aeroporti.
        var campoPerSessione = new Dictionary<long, string>();
        var areaPerSessione = new Dictionary<long, string>();
        foreach (var s in sessioni)
        {
            if (TrafficStory.StationIcao(s.Callsign) is { } icao) campoPerSessione[s.SessionId] = icao;
            else areaPerSessione[s.SessionId] = s.Callsign.Trim().ToUpperInvariant();
        }

        var dentroPerSettore = await AeroportiPerSettoreAsync(areaPerSessione.Values.ToHashSet(StringComparer.OrdinalIgnoreCase), ct);

        var ids = campoPerSessione.Keys.Concat(areaPerSessione.Keys).Distinct().ToList();
        var righe = await _db.AtcSessionTraffic.AsNoTracking()
            .Where(t => ids.Contains(t.SessionId))
            .Select(t => new { t.SessionId, t.DepIcao, t.ArrIcao, t.SawMovement })
            .ToListAsync(ct);

        var conteggi = new Dictionary<string, (int Tratte, int Movimenti)>(StringComparer.OrdinalIgnoreCase);
        var daAccreditare = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var t in righe)
        {
            // ⚠️ Un insieme, non due contatori: un LIRF→LIRF (circuito, rientro) è UNA tratta per LIRF, e
            // lo stesso vale per un settore d’area che ha tutti e due i capi in casa.
            daAccreditare.Clear();

            if (campoPerSessione.TryGetValue(t.SessionId, out var campo))
            {
                // Da O per: un sorvolo vettorato mentre si copriva LIRF non è traffico «di» LIRF.
                if (Uguale(t.DepIcao, campo) || Uguale(t.ArrIcao, campo)) daAccreditare.Add(campo);
            }
            else if (areaPerSessione.TryGetValue(t.SessionId, out var settore)
                     && dentroPerSettore.TryGetValue(settore, out var dentro))
            {
                // Stessa regola, ma «tuo» lo decide il poligono. I capi fuori area (EGLL, EDDF) restano fuori:
                // sono traffico gestito, non traffico DI un tuo aeroporto.
                if (Normale(t.DepIcao) is { } dep && dentro.Contains(dep)) daAccreditare.Add(dep);
                if (Normale(t.ArrIcao) is { } arr && dentro.Contains(arr)) daAccreditare.Add(arr);
            }

            foreach (var k in daAccreditare)
            {
                var v = conteggi.GetValueOrDefault(k);
                conteggi[k] = (v.Tratte + 1, v.Movimenti + (t.SawMovement ? 1 : 0));
            }
        }

        return conteggi
            .Select(kv => new StatsByKey(kv.Key, kv.Value.Tratte, 0, kv.Value.Movimenti))
            .OrderByDescending(r => r.Sessions)
            .ThenBy(r => r.Key, StringComparer.Ordinal)
            .Take(Math.Max(1, limit))
            .ToList();
    }

    /// <summary>
    /// Per ogni callsign d’area chiesto, gli ICAO degli aeroporti che cadono dentro il suo poligono.
    ///
    /// <para>Il punto-nel-poligono è lo stesso di <c>PolygonGeometry</c> che usa l’attribuzione del traffico:
    /// una seconda regola qui si scollerebbe dalla prima al primo cambiamento.</para>
    ///
    /// <para>⚠️ Si calcola una volta per <b>settore</b>, non per tratta: sono un centinaio di aeroporti per
    /// una manciata di callsign, mentre le tratte di un anno sono decine di migliaia.</para>
    /// </summary>
    private async Task<IReadOnlyDictionary<string, HashSet<string>>> AeroportiPerSettoreAsync(
        IReadOnlySet<string> callsigns, CancellationToken ct)
    {
        var vuoto = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        if (callsigns.Count == 0) return vuoto;

        var poligoni = await _db.AccSectors.AsNoTracking()
            .Where(a => callsigns.Contains(a.ComposePosition) && a.RegionMapPolygon != null)
            .Select(a => new { a.ComposePosition, a.RegionMapPolygon })
            .ToListAsync(ct);
        if (poligoni.Count == 0) return vuoto;

        // ⚠️ Nove aeroporti su 93 non hanno coordinate, e non è un difetto da aggirare: tre sono voci di
        // FIR/TMA («Roma TMA», «Milano TMA», «Apulia») che un aeroporto non lo sono, gli altri sei sono campi
        // minuscoli. Restano fuori, e va bene così.
        var aeroporti = await _db.Airports.AsNoTracking()
            .Where(a => a.Latitude != null && a.Longitude != null)
            .Select(a => new { a.Icao, Lat = a.Latitude!.Value, Lon = a.Longitude!.Value })
            .ToListAsync(ct);

        foreach (var p in poligoni)
        {
            var anello = PolygonGeometry.ToRing(p.RegionMapPolygon);
            if (anello is null) continue;

            var dentro = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in aeroporti)
                if (PolygonGeometry.Contains(anello, a.Lat, a.Lon))
                    dentro.Add(a.Icao.Trim().ToUpperInvariant());

            if (dentro.Count > 0) vuoto[p.ComposePosition.Trim().ToUpperInvariant()] = dentro;
        }

        return vuoto;
    }

    private static string? Normale(string? icao) =>
        string.IsNullOrWhiteSpace(icao) ? null : icao.Trim().ToUpperInvariant();

    private static bool Uguale(string? a, string b) =>
        !string.IsNullOrWhiteSpace(a) && string.Equals(a.Trim(), b, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Aeroporti o tipi del traffico gestito. Un volo LIRF→LIRN conta per <b>tutti e due</b> gli scali:
    /// la domanda è «quali aeroporti ti passano davanti», non «da dove partivano».
    /// </summary>
    private async Task<IReadOnlyList<StatsByKey>> PerChiave(
        int? userId, DateTimeOffset from, DateTimeOffset to, int limit, bool aeroporti, CancellationToken ct)
    {
        var sessioni = Contate(userId, from, to).Select(s => s.SessionId);

        var righe = await _db.AtcSessionTraffic.AsNoTracking()
            .Where(t => sessioni.Contains(t.SessionId))
            .Select(t => new { t.DepIcao, t.ArrIcao, t.AircraftIcao, t.SawMovement })
            .ToListAsync(ct);

        var conteggi = new Dictionary<string, (int Tratte, int Movimenti)>(StringComparer.OrdinalIgnoreCase);
        void Conta(string? chiave, bool movimento)
        {
            if (string.IsNullOrWhiteSpace(chiave)) return;
            var k = chiave.Trim().ToUpperInvariant();
            var v = conteggi.GetValueOrDefault(k);
            conteggi[k] = (v.Tratte + 1, v.Movimenti + (movimento ? 1 : 0));
        }

        foreach (var t in righe)
        {
            if (aeroporti) { Conta(t.DepIcao, t.SawMovement); Conta(t.ArrIcao, t.SawMovement); }
            else Conta(t.AircraftIcao, t.SawMovement);
        }

        return conteggi
            .Select(kv => new StatsByKey(kv.Key, kv.Value.Tratte, 0, kv.Value.Movimenti))
            .OrderByDescending(r => r.Sessions)
            .ThenBy(r => r.Key, StringComparer.Ordinal)
            .Take(Math.Max(1, limit))
            .ToList();
    }

    public async Task<StatsStreak> StreakAsync(
        int userId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var inizi = await Contate(userId, from, to).Select(s => s.StartUtc).ToListAsync(ct);
        return ControllerStreak.Build(inizi.Select(Utc), to);
    }

    public async Task<StatsRank> RankAsync(
        int userId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        // ⚠️ Tutta la classifica, non i primi venti: la posizione di chi è 47° esiste solo se si contano
        // anche gli altri 46. Sono le sessioni di un anno di una divisione — poche migliaia di righe, e la
        // proiezione porta due colonne.
        var righe = await Contate(null, from, to)
            .Select(s => new { s.UserId, s.DurationSeconds })
            .ToListAsync(ct);

        var perVid = righe
            .GroupBy(r => r.UserId)
            .Select(g => new { Vid = g.Key, Secondi = g.Sum(r => (long)r.DurationSeconds) })
            .OrderByDescending(r => r.Secondi)
            .ToList();

        var posizione = perVid.FindIndex(r => r.Vid == userId) + 1;   // FindIndex dà -1 se non c'è: 0 va bene
        return new StatsRank(posizione, perVid.Count);
    }

    public async Task<DateTimeOffset?> ArchiveStartAsync(int? userId, CancellationToken ct = default)
    {
        var q = _db.AtcSessions.AsNoTracking().DiDivisione();
        if (userId is { } vid) q = q.Where(s => s.UserId == vid);

        // ⚠️ Senza soglia sulla durata: qui la domanda è «da quando esiste l'archivio», e una connessione
        // lampo è comunque un giorno in cui il poller stava registrando.
        var prima = await q.OrderBy(s => s.StartUtc).Select(s => (DateTime?)s.StartUtc).FirstOrDefaultAsync(ct);

        // ⚠️ E il RIASSUNTO mensile, che è più vecchio delle sessioni per costruzione: dal 26 agosto 2026 le
        // sessioni si potano a dodici mesi, e questa risposta senza il riassunto direbbe che l'archivio
        // comincia esattamente un anno fa — cioè si accorcerebbe da sola ogni notte, mentre i numeri dei mesi
        // vecchi ci sono ancora.
        var r = _db.AtcMonthRollups.AsNoTracking();
        if (userId is { } vid2) r = r.Where(x => x.UserId == vid2);
        var primoMese = await r.OrderBy(x => x.Month).Select(x => (DateTime?)x.Month).FirstOrDefaultAsync(ct);

        var inizio = (prima, primoMese) switch
        {
            ({ } a, { } b) => a < b ? a : b,
            ({ } a, null) => a,
            (null, { } b) => b,
            _ => (DateTime?)null,
        };
        return inizio is { } t ? Utc(t) : null;
    }

    private static string? Consegna(IReadOnlyDictionary<long, string> callsign, long? sessionId) =>
        sessionId is { } id && callsign.TryGetValue(id, out var c) ? c : null;

    private static StatsSessionRow Riga(AtcSession s) => new(
        s.SessionId, s.UserId, s.Callsign, s.Position, s.Frequency,
        Utc(s.StartUtc), s.EndUtc is { } f ? Utc(f) : null,
        s.DurationSeconds, s.TrafficCount, s.MovementCount, s.TrafficMinutes, s.ShiftKey);

    private static DateTimeOffset Utc(DateTime t) => new(DateTime.SpecifyKind(t, DateTimeKind.Utc));
}
