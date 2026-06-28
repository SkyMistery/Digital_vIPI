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
            .Include(g => g.Fir)
            .OrderBy(g => g.Fir!.Code).ThenBy(g => g.UserId)
            .Select(g => new GrantRow
            {
                Id = g.Id, UserId = g.UserId, DisplayName = g.DisplayName,
                FirCode = g.Fir!.Code, GrantedByUserId = g.GrantedByUserId, GrantedAtUtc = g.GrantedAtUtc,
            })
            .ToListAsync(ct);
    }

    public async Task<int> AddAsync(int UserId, string? displayName, string firCode, int GrantedByUserId, CancellationToken ct = default)
    {
        var firId = await _db.Firs.Where(f => f.Code == firCode).Select(f => (int?)f.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"FIR {firCode} inesistente.");

        var existing = await _db.EditGrants.FirstOrDefaultAsync(g => g.UserId == UserId && g.FirId == firId, ct);
        if (existing is not null)
        {
            existing.DisplayName = displayName;     // aggiorna nome, evita duplicati (indice unico)
            Audit(GrantedByUserId, AuditAction.Update, existing.Id, UserId, firCode);
            await _db.SaveChangesAsync(ct);
            return existing.Id;
        }

        var grant = new EditGrant
        {
            UserId = UserId, DisplayName = displayName, FirId = firId,
            GrantedByUserId = GrantedByUserId, GrantedAtUtc = DateTime.UtcNow,
        };
        _db.EditGrants.Add(grant);
        await _db.SaveChangesAsync(ct);
        Audit(GrantedByUserId, AuditAction.Create, grant.Id, UserId, firCode);
        await _db.SaveChangesAsync(ct);
        return grant.Id;
    }

    public async Task RevokeAsync(int grantId, CancellationToken ct = default)
    {
        var g = await _db.EditGrants.Include(x => x.Fir).FirstOrDefaultAsync(x => x.Id == grantId, ct);
        if (g is null) return;
        _db.EditGrants.Remove(g);
        Audit(g.GrantedByUserId, AuditAction.Archive, g.Id, g.UserId, g.Fir?.Code ?? g.FirId.ToString());
        await _db.SaveChangesAsync(ct);
    }

    // Traccia un'azione sui permessi nell'audit log (chi, cosa, su quale UserId/FIR).
    private void Audit(int actorUserId, AuditAction action, int grantId, int targetUserId, string firCode) =>
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = actorUserId,
            Action = action,
            EntityType = "EditGrant",
            EntityId = grantId.ToString(),
            TimestampUtc = DateTime.UtcNow,
            DetailsJson = $"{{\"UserId\":{targetUserId},\"fir\":\"{firCode}\"}}",
        });

    public Task<bool> HasGrantAsync(int UserId, string firCode, CancellationToken ct = default) =>
        _db.EditGrants.AnyAsync(g => g.UserId == UserId && g.Fir!.Code == firCode, ct);

    public async Task<string?> GetDocumentFirCodeAsync(int documentId, CancellationToken ct = default)
    {
        // vIPI: FIR da un settore di scope.
        var firFromScope = await _db.Sectors
            .Where(s => s.DocumentId == documentId)
            .Select(s => s.Fir!.Code)
            .FirstOrDefaultAsync(ct);
        if (firFromScope is not null) return firFromScope;

        // vLOA: niente settore di scope → FIR della parte Home.
        return await _db.DocumentParties
            .Where(pa => pa.DocumentId == documentId && pa.Role == PartyRole.Home)
            .Select(pa => pa.Sector!.Fir!.Code)
            .FirstOrDefaultAsync(ct);
    }
}
