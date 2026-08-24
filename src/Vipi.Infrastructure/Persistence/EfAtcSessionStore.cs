using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Stats;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Archivio EF delle sessioni ATC. Sottile: decide <see cref="AtcSessionSync"/>, qui si scrive e basta.
///
/// <para>Gli istanti si conservano in UTC (<see cref="DateTimeKind.Utc"/>): il resto dell'applicazione
/// mostra sempre UTC col suffisso Z e lascia al browser l'ora del lettore.</para>
/// </summary>
public sealed class EfAtcSessionStore : IAtcSessionStore
{
    private readonly VipiDbContext _db;

    public EfAtcSessionStore(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<KnownAtcSession>> GetOpenOrRecentAsync(
        DateTimeOffset since, CancellationToken ct = default)
    {
        var soglia = since.UtcDateTime;

        var righe = await _db.AtcSessions.AsNoTracking()
            .Where(s => s.EndUtc == null || s.EndUtc >= soglia)
            .Select(s => new { s.SessionId, s.UserId, s.Callsign, s.StartUtc, s.EndUtc, s.ShiftKey })
            .ToListAsync(ct);

        return righe
            .Select(s => new KnownAtcSession(
                s.SessionId, s.UserId, s.Callsign,
                new DateTimeOffset(DateTime.SpecifyKind(s.StartUtc, DateTimeKind.Utc)),
                s.EndUtc is { } fine ? new DateTimeOffset(DateTime.SpecifyKind(fine, DateTimeKind.Utc)) : null,
                s.ShiftKey))
            .ToList();
    }

    public async Task<int> ApplyAsync(AtcSessionPlan plan, CancellationToken ct = default)
    {
        if (plan.Nothing) return 0;

        var toccate = 0;

        // Le sessioni da aggiornare si caricano in un colpo: sono quelle in frequenza adesso (una manciata).
        var ids = plan.Upserts.Where(u => !u.IsNew).Select(u => u.SessionId)
            .Concat(plan.Closures.Select(c => c.SessionId))
            .Distinct().ToList();

        var esistenti = ids.Count == 0
            ? new Dictionary<long, AtcSession>()
            : await _db.AtcSessions.Where(s => ids.Contains(s.SessionId)).ToDictionaryAsync(s => s.SessionId, ct);

        foreach (var u in plan.Upserts)
        {
            if (esistenti.TryGetValue(u.SessionId, out var riga))
            {
                // Una sessione tornata in frequenza dopo che l'avevamo chiusa per un poll perso: riaprirla è
                // più vero che lasciarla chiusa, e il turno resta quello suo.
                riga.EndUtc = null;
                riga.DurationSeconds = u.DurationSeconds;
                riga.Position ??= u.Position;
                riga.Frequency ??= u.Frequency;
                riga.UpdatedAtUtc = DateTime.UtcNow;
            }
            else
            {
                _db.AtcSessions.Add(new AtcSession
                {
                    SessionId = u.SessionId,
                    UserId = u.UserId,
                    Callsign = u.Callsign,
                    Position = u.Position,
                    Frequency = u.Frequency,
                    Rating = u.Rating,
                    StartUtc = u.StartUtc.UtcDateTime,
                    DurationSeconds = u.DurationSeconds,
                    Source = AtcSessionSource.Live,
                    ShiftKey = u.ShiftKey,
                    UpdatedAtUtc = DateTime.UtcNow,
                });
            }
            toccate++;
        }

        foreach (var c in plan.Closures)
        {
            if (!esistenti.TryGetValue(c.SessionId, out var riga)) continue;
            riga.EndUtc = c.EndUtc.UtcDateTime;
            riga.UpdatedAtUtc = DateTime.UtcNow;
            toccate++;
        }

        await _db.SaveChangesAsync(ct);
        return toccate;
    }

    public async Task<(int Created, int Updated)> UpsertHistoryAsync(
        IReadOnlyList<SourceAtcSessionHistory> sessions, CancellationToken ct = default)
    {
        if (sessions.Count == 0) return (0, 0);

        var ids = sessions.Select(s => s.SessionId).ToList();
        var esistenti = await _db.AtcSessions.Where(s => ids.Contains(s.SessionId)).ToDictionaryAsync(s => s.SessionId, ct);

        int creati = 0, aggiornati = 0;

        foreach (var s in sessions)
        {
            if (esistenti.TryGetValue(s.SessionId, out var riga))
            {
                // La coda è verità della sorgente: quando lei dice che è finita, la nostra chiusura
                // «all'ultimo giro in cui non c'era più» viene sostituita da quella vera.
                if (s.EndUtc is { } fine) riga.EndUtc = fine.UtcDateTime;
                if (s.ConnectedSeconds > riga.DurationSeconds) riga.DurationSeconds = s.ConnectedSeconds;
                riga.UpdatedAtUtc = DateTime.UtcNow;
                aggiornati++;
            }
            else
            {
                _db.AtcSessions.Add(new AtcSession
                {
                    SessionId = s.SessionId,
                    UserId = s.UserId,
                    Callsign = s.Callsign,
                    // ⚠️ La LISTA dello storico non porta posizione e frequenza (stanno solo sul dettaglio
                    // per-sessione): la posizione si ricava dal callsign, la frequenza resta vuota.
                    Position = PosizioneDaCallsign(s.Callsign),
                    Rating = s.Rating,
                    StartUtc = s.StartUtc.UtcDateTime,
                    EndUtc = s.EndUtc?.UtcDateTime,
                    DurationSeconds = s.ConnectedSeconds,
                    Source = AtcSessionSource.Backfill,
                    ShiftKey = s.SessionId,          // provvisorio: lo sistema RecomputeShiftsAsync
                    UpdatedAtUtc = DateTime.UtcNow,
                });
                creati++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return (creati, aggiornati);
    }

    public async Task<int> RecomputeShiftsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var da = from.UtcDateTime;
        var a = to.UtcDateTime;

        var righe = await _db.AtcSessions.Where(s => s.StartUtc >= da && s.StartUtc <= a).ToListAsync(ct);
        if (righe.Count == 0) return 0;

        var chiavi = AtcShiftGrouper.Group(righe.Select(s => new ShiftInput(
            s.SessionId, s.UserId, s.Callsign,
            new DateTimeOffset(DateTime.SpecifyKind(s.StartUtc, DateTimeKind.Utc)),
            s.EndUtc is { } f ? new DateTimeOffset(DateTime.SpecifyKind(f, DateTimeKind.Utc)) : null)));

        var corrette = 0;
        foreach (var riga in righe)
        {
            if (!chiavi.TryGetValue(riga.SessionId, out var chiave) || chiave == riga.ShiftKey) continue;
            riga.ShiftKey = chiave;
            corrette++;
        }

        if (corrette > 0) await _db.SaveChangesAsync(ct);
        return corrette;
    }

    /// <summary>Suffisso di posizione dal callsign: <c>LIRN_US0_APP</c> → <c>APP</c>.</summary>
    private static string? PosizioneDaCallsign(string callsign)
    {
        var pezzi = callsign.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return pezzi.Length >= 2 ? pezzi[^1].ToUpperInvariant() : null;
    }
}
