using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Lettura EF dell'archivio delle connessioni ATC (divisione e resto del mondo). Nessun conto, nessuna
/// soglia: le righe come sono in tabella.
/// </summary>
public sealed class EfAtcArchiveQueries : IAtcArchiveQueries
{
    /// <summary>Tetto duro delle righe per richiesta, qualunque cosa chieda il chiamante.</summary>
    public const int MaxRighe = 500;

    private readonly VipiDbContext _db;

    public EfAtcArchiveQueries(VipiDbContext db) => _db = db;

    public async Task<AtcArchivePage> SearchAsync(AtcArchiveFilter filter, CancellationToken ct = default)
    {
        var q = _db.AtcSessions.AsNoTracking();

        q = filter.Scope switch
        {
            AtcArchiveScope.Division => q.DiDivisione(),
            AtcArchiveScope.World => q.Where(s => s.IsOutsideDivision),
            _ => q,
        };

        if (filter.From is { } da)
        {
            // ⚠️ Una sessione entra nella finestra se si SOVRAPPONE, non se comincia dentro: chi ha aperto
            // alle 19:50 e ha chiuso alle 22:00 fa parte di «cosa c'era alle 21», e un filtro sul solo
            // inizio lo perderebbe — che è l'errore classico di questa domanda.
            var d = da.UtcDateTime;
            q = q.Where(s => s.EndUtc == null || s.EndUtc >= d);
        }

        if (filter.To is { } a)
        {
            var t = a.UtcDateTime;
            q = q.Where(s => s.StartUtc <= t);
        }

        if (!string.IsNullOrWhiteSpace(filter.CallsignPrefix))
        {
            var p = filter.CallsignPrefix.Trim().ToUpperInvariant();
            q = q.Where(s => s.Callsign.StartsWith(p));
        }

        if (filter.UserId is { } vid) q = q.Where(s => s.UserId == vid);
        if (filter.OnlyOpen) q = q.Where(s => s.EndUtc == null);

        var totale = await q.CountAsync(ct);

        // ⚠️ La proiezione va in un tipo ANONIMO e il record si costruisce in memoria: gli istanti in
        // colonna sono <c>DateTime</c> e il record vuole <c>DateTimeOffset</c>, e una conversione dentro
        // l'albero di espressione è una traduzione che può mancare a runtime — cioè con la pagina già
        // aperta. Il Kind si rimette qui: EF le restituisce senza, e senza <c>Utc</c> il browser
        // sposterebbe l'ora una seconda volta.
        var righe = await q
            .OrderByDescending(s => s.StartUtc)
            .Skip(Math.Max(0, filter.Offset))
            .Take(Math.Clamp(filter.Limit, 1, MaxRighe))
            .Select(s => new
            {
                s.SessionId, s.UserId, s.Callsign, s.Position, s.Frequency, s.Rating,
                s.StartUtc, s.EndUtc, s.DurationSeconds, s.IsOutsideDivision,
            })
            .ToListAsync(ct);

        return new AtcArchivePage(
            righe.Select(s => new AtcArchiveRow(
                s.SessionId, s.UserId, s.Callsign, s.Position, s.Frequency, s.Rating,
                Utc(s.StartUtc), s.EndUtc is { } f ? Utc(f) : null,
                s.DurationSeconds, s.IsOutsideDivision)).ToList(),
            totale);
    }

    private static DateTimeOffset Utc(DateTime t) => new(DateTime.SpecifyKind(t, DateTimeKind.Utc));
}
