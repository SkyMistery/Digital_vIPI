using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
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

        var q = _db.AtcSessions.AsNoTracking()
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
        // ⚠️ La proiezione del raggruppamento va in un tipo ANONIMO, non nel record: EF non sa tradurre un
        // `GroupBy` che costruisce un record, e lancia a runtime — cioè con la pagina già aperta, non in
        // compilazione. Trovato aprendo la pagina davvero, ed è il motivo per cui questa classe ora ha i test.
        var righe = await Contate(userId, from, to)
            .GroupBy(s => s.Callsign)
            .Select(g => new
            {
                Key = g.Key,
                Sessions = g.Count(),
                Seconds = g.Sum(s => s.DurationSeconds),
                Movements = g.Sum(s => s.MovementCount),
            })
            .OrderByDescending(r => r.Seconds)
            .Take(Math.Max(1, limit))
            .ToListAsync(ct);

        return righe.Select(r => new StatsByKey(r.Key, r.Sessions, r.Seconds, r.Movements)).ToList();
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
                t.SawMovement, t.HasObservationGap, t.Origin)).ToList(),
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

    private static StatsSessionRow Riga(AtcSession s) => new(
        s.SessionId, s.UserId, s.Callsign, s.Position, s.Frequency,
        Utc(s.StartUtc), s.EndUtc is { } f ? Utc(f) : null,
        s.DurationSeconds, s.TrafficCount, s.MovementCount, s.TrafficMinutes, s.ShiftKey);

    private static DateTimeOffset Utc(DateTime t) => new(DateTime.SpecifyKind(t, DateTimeKind.Utc));
}
