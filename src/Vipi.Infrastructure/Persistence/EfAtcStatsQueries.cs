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

        return new StatsSessionDetail(Riga(sessione), traffico.Select(t => new StatsTrafficRow(
            t.PilotCallsign, t.LegOrdinal, t.DepIcao, t.ArrIcao, t.AircraftIcao,
            Utc(t.FirstSeenUtc), Utc(t.LastSeenUtc), t.SeenMinutes,
            t.SawMovement, t.HasObservationGap, t.Origin)).ToList());
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

    private static StatsSessionRow Riga(AtcSession s) => new(
        s.SessionId, s.UserId, s.Callsign, s.Position, s.Frequency,
        Utc(s.StartUtc), s.EndUtc is { } f ? Utc(f) : null,
        s.DurationSeconds, s.TrafficCount, s.MovementCount, s.TrafficMinutes, s.ShiftKey);

    private static DateTimeOffset Utc(DateTime t) => new(DateTime.SpecifyKind(t, DateTimeKind.Utc));
}
