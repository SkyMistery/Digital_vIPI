using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Le promozioni a mano su database. Carta <c>docs/feature/2026-08-28-autorizzazioni-a-livelli.md</c> §5.
///
/// <para>⚠️ Nessuna delle sue query sta su un percorso caldo: chi <b>decide</b> un permesso legge il
/// fotogramma in memoria (<see cref="Vipi.Application.Auth.IRoleOverrides"/>). Qui si passa quando un admin
/// apre la pagina dei permessi o scrive una riga.</para>
/// </summary>
public sealed class EfRoleOverrideStore : IRoleOverrideStore
{
    private readonly VipiDbContext _db;
    public EfRoleOverrideStore(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<RoleOverrideRow>> ListAsync(CancellationToken ct = default) =>
        await _db.RoleOverrides.AsNoTracking()
            .OrderBy(o => o.UserId)
            .Select(o => new RoleOverrideRow(o.UserId, o.Level, o.GrantedByUserId, o.GrantedAtUtc, o.Note, o.DisplayName))
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task SetAsync(
        int userId, VipiRole level, int grantedByUserId, string? displayName, string? note,
        CancellationToken ct = default)
    {
        // Una riga per persona: promuovere due volte riscrive, non accumula. Se accumulasse, a decidere
        // sarebbe l'ordine della query — cioè il caso.
        var riga = await _db.RoleOverrides.FirstOrDefaultAsync(o => o.UserId == userId, ct).ConfigureAwait(false);
        var nuova = riga is null;
        if (riga is null)
        {
            riga = new RoleOverride { UserId = userId };
            _db.RoleOverrides.Add(riga);
        }

        riga.Level = level;
        riga.GrantedByUserId = grantedByUserId;
        riga.GrantedAtUtc = DateTime.UtcNow;
        riga.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        // ⚠️ Il nome si aggiorna solo se ne arriva uno: la pagina dei permessi lo prende dal roster, che per
        // qualcuno può non averlo. Sovrascrivere con null cancellerebbe un nome buono scritto prima.
        if (!string.IsNullOrWhiteSpace(displayName)) riga.DisplayName = displayName.Trim();

        // ⚠️ L'attore è chi FIRMA la promozione, non chi la riceve: è il registro dei permessi, e per due
        // anni quello dei grant ha attribuito le revoche alla persona sbagliata.
        AuditScribe.Write(_db, grantedByUserId, nuova ? AuditAction.Create : AuditAction.Update,
            "RoleOverride", userId.ToString(), new { UserId = userId, Level = level.ToString() });

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> RemoveAsync(int userId, int actorUserId, CancellationToken ct = default)
    {
        var riga = await _db.RoleOverrides.FirstOrDefaultAsync(o => o.UserId == userId, ct).ConfigureAwait(false);
        if (riga is null) return false;

        // Delete e non Archive: la promozione non si conserva da nessuna parte, la riga esce dalla tabella.
        AuditScribe.Write(_db, actorUserId, AuditAction.Delete,
            "RoleOverride", userId.ToString(), new { UserId = userId, Level = riga.Level.ToString() });

        // RemoveRange e non ExecuteDelete: il secondo desincronizza il change-tracker del contesto, ed è già
        // costato dei rossi altrove (memoria «ef-executedelete-tracker-constraint»).
        _db.RoleOverrides.Remove(riga);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }
}
