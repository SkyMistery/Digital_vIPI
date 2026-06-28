using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Implementazione EF di <see cref="IEditGrantRepository"/>.</summary>
public sealed class EfEditGrantRepository : IEditGrantRepository
{
    private readonly VipiDbContext _db;
    public EfEditGrantRepository(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<GrantRow>> ListAsync(CancellationToken ct = default)
    {
        return await _db.EditGrants
            .Include(g => g.Acc)
            .OrderBy(g => g.Acc!.Code).ThenBy(g => g.UserId)
            .Select(g => new GrantRow
            {
                Id = g.Id, UserId = g.UserId, DisplayName = g.DisplayName,
                AccCode = g.Acc!.Code, GrantedByUserId = g.GrantedByUserId, GrantedAtUtc = g.GrantedAtUtc,
            })
            .ToListAsync(ct);
    }

    public async Task<int> AddAsync(int UserId, string? displayName, string accCode, int GrantedByUserId, CancellationToken ct = default)
    {
        var accId = await _db.Accs.Where(f => f.Code == accCode).Select(f => (int?)f.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"ACC {accCode} inesistente.");

        var existing = await _db.EditGrants.FirstOrDefaultAsync(g => g.UserId == UserId && g.AccId == accId, ct);
        if (existing is not null)
        {
            existing.DisplayName = displayName;     // aggiorna nome, evita duplicati (indice unico)
            Audit(GrantedByUserId, AuditAction.Update, existing.Id, UserId, accCode);
            await _db.SaveChangesAsync(ct);
            return existing.Id;
        }

        var grant = new EditGrant
        {
            UserId = UserId, DisplayName = displayName, AccId = accId,
            GrantedByUserId = GrantedByUserId, GrantedAtUtc = DateTime.UtcNow,
        };
        _db.EditGrants.Add(grant);
        await _db.SaveChangesAsync(ct);
        Audit(GrantedByUserId, AuditAction.Create, grant.Id, UserId, accCode);
        await _db.SaveChangesAsync(ct);
        return grant.Id;
    }

    public async Task RevokeAsync(int grantId, CancellationToken ct = default)
    {
        var g = await _db.EditGrants.Include(x => x.Acc).FirstOrDefaultAsync(x => x.Id == grantId, ct);
        if (g is null) return;
        _db.EditGrants.Remove(g);
        Audit(g.GrantedByUserId, AuditAction.Archive, g.Id, g.UserId, g.Acc?.Code ?? g.AccId.ToString());
        await _db.SaveChangesAsync(ct);
    }

    // Traccia un'azione sui permessi nell'audit log (chi, cosa, su quale UserId/ACC).
    private void Audit(int actorUserId, AuditAction action, int grantId, int targetUserId, string accCode) =>
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = actorUserId,
            Action = action,
            EntityType = "EditGrant",
            EntityId = grantId.ToString(),
            TimestampUtc = DateTime.UtcNow,
            DetailsJson = $"{{\"UserId\":{targetUserId},\"acc\":\"{accCode}\"}}",
        });

    public Task<bool> HasGrantAsync(int UserId, string accCode, CancellationToken ct = default) =>
        _db.EditGrants.AnyAsync(g => g.UserId == UserId && g.Acc!.Code == accCode, ct);

    public async Task<string?> GetDocumentAccCodeAsync(int documentId, CancellationToken ct = default)
    {
        // vIPI: ACC da un settore di scope.
        var accFromScope = await _db.Sectors
            .Where(s => s.DocumentId == documentId)
            .Select(s => s.Acc!.Code)
            .FirstOrDefaultAsync(ct);
        if (accFromScope is not null) return accFromScope;

        // vLOA: niente settore di scope → ACC della parte Home.
        return await _db.DocumentParties
            .Where(pa => pa.DocumentId == documentId && pa.Role == PartyRole.Home)
            .Select(pa => pa.Sector!.Acc!.Code)
            .FirstOrDefaultAsync(ct);
    }
}
