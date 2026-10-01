using Microsoft.EntityFrameworkCore;
using Vipi.Application.EventKits;
using Vipi.Domain;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Scrive nel registro di audit chi ha usato quale account dell'evento (carta 2026-10-01-account-evento.md). Il
/// bersaglio è il VID dell'EVENTO; nei dettagli la postazione. Una riga per persona, VID e evento al giorno: chi
/// riscrive il VID dopo un ricarico non ne lascia dieci.
/// </summary>
public sealed class EfAccountEventoTraccia : IAccountEventoTraccia
{
    internal const string Entita = "EventAccount";

    private readonly VipiDbContext _db;
    public EfAccountEventoTraccia(VipiDbContext db) => _db = db;

    public async Task RegistraAsync(int vidPersonale, int vidEvento, string callsign, CancellationToken ct = default)
    {
        var id = vidEvento.ToString();
        var oggi = DateTime.UtcNow.Date;
        var gia = await _db.AuditLogs.AsNoTracking()
            .AnyAsync(a => a.UserId == vidPersonale && a.Action == AuditAction.View && a.EntityType == Entita
                           && a.EntityId == id && a.TimestampUtc >= oggi, ct);
        if (gia) return;
        AuditScribe.Write(_db, vidPersonale, AuditAction.View, Entita, id, new { Callsign = callsign });
        await _db.SaveChangesAsync(ct);
    }
}
