using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Scrive nel registro di audit l'apertura delle statistiche altrui, accorpando gli accessi ravvicinati.
/// </summary>
public sealed class EfStatsAccessLog : IStatsAccessLog
{
    /// <summary>Tipo di entità nel registro. Il bersaglio è la PERSONA guardata, non una pagina.</summary>
    internal const string Entita = "StatsProfile";

    /// <summary>
    /// Quanto dura una «consultazione». ⚠️ Non è un dettaglio estetico: senza questa finestra la pagina
    /// scrive una riga per ricarica, e i cinque chip di periodo fanno cinque righe identiche a mezzo minuto
    /// l'una dall'altra. Mezz'ora è la durata plausibile di uno sguardo alle statistiche di qualcuno; chi
    /// torna il giorno dopo lascia una riga nuova, ed è giusto così.
    /// </summary>
    private static readonly TimeSpan Finestra = TimeSpan.FromMinutes(30);

    private readonly VipiDbContext _db;

    public EfStatsAccessLog(VipiDbContext db) => _db = db;

    public async Task RecordProfileViewAsync(int actorUserId, int subjectUserId, CancellationToken ct = default)
    {
        // Le proprie statistiche non sono un accesso ai dati di un altro: non si registrano.
        if (actorUserId == subjectUserId || actorUserId <= 0 || subjectUserId <= 0) return;

        var soglia = DateTime.UtcNow - Finestra;
        var id = subjectUserId.ToString();

        var giaScritto = await _db.AuditLogs
            .AsNoTracking()
            .AnyAsync(a => a.UserId == actorUserId
                        && a.Action == AuditAction.View
                        && a.EntityType == Entita
                        && a.EntityId == id
                        && a.TimestampUtc >= soglia, ct);
        if (giaScritto) return;

        AuditScribe.Write(_db, actorUserId, AuditAction.View, Entita, id, new { Vid = subjectUserId });
        await _db.SaveChangesAsync(ct);
    }
}
