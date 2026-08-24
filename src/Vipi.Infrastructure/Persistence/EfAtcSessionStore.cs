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
}
