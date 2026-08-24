using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Riga singola con le scelte sulle statistiche. Scrittura con audit, nella stessa <c>SaveChanges</c>
/// dell'atto che descrive — come per la policy di import: accendere la classifica pubblica è un atto
/// amministrativo, e fra sei mesi deve essere possibile sapere chi l'ha deciso.
/// </summary>
public sealed class EfStatsSettingsStore : IStatsSettingsStore
{
    private readonly VipiDbContext _db;

    public EfStatsSettingsStore(VipiDbContext db) => _db = db;

    public async Task<StatsSettings> GetAsync(CancellationToken ct = default)
    {
        var riga = await _db.StatsSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1, ct);
        return riga is null
            ? Application.Abstractions.StatsSettings.Default
            : new Application.Abstractions.StatsSettings(riga.PublicLeaderboard, riga.UpdatedUtc, riga.UpdatedByUserId);
    }

    public async Task SaveAsync(bool publicLeaderboard, int updatedByUserId, CancellationToken ct = default)
    {
        var riga = await _db.StatsSettings.FirstOrDefaultAsync(x => x.Id == 1, ct);

        // Il non-evento non si scrive: riscriverebbe «deciso da X oggi» su una decisione di qualcun altro.
        if (riga is not null && riga.PublicLeaderboard == publicLeaderboard) return;

        if (riga is null)
        {
            riga = new Domain.Entities.StatsSettings { Id = 1 };
            _db.StatsSettings.Add(riga);
        }

        riga.PublicLeaderboard = publicLeaderboard;
        riga.UpdatedUtc = DateTime.UtcNow;
        riga.UpdatedByUserId = updatedByUserId;

        AuditScribe.Write(_db, updatedByUserId, AuditAction.Update, "StatsSettings", "1",
            new { ClassificaPubblica = publicLeaderboard });

        await _db.SaveChangesAsync(ct);
    }
}
