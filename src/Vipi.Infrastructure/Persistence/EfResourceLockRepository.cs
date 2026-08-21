using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Application.Content;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>EF Core del lock su risorse nominate. Riga presente = lock tenuto; rilascio = rimozione riga. TTL sliding.</summary>
public sealed class EfResourceLockRepository : IResourceLockRepository
{
    private readonly VipiDbContext _db;

    public EfResourceLockRepository(VipiDbContext db) => _db = db;

    public async Task<LockInfo> AcquireOrInspectAsync(string resourceKey, int userId, string? name, int ttlMinutes, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(ttlMinutes);

        // Acquisizione atomica su riga esistente: riesce solo se scaduta o già mia.
        var rows = await _db.EditResourceLocks
            .Where(l => l.ResourceKey == resourceKey && (l.LockExpiresUtc < now || l.LockedByUserId == userId))
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.LockedByUserId, userId)
                .SetProperty(l => l.LockedByName, name)
                .SetProperty(l => l.LockedAtUtc, now)
                .SetProperty(l => l.LockExpiresUtc, expires), ct);
        if (rows > 0) return Mine(userId, name, expires);

        // Nessuna riga aggiornata: o non esiste (→ inserisci) o è tenuta da altri e attiva (→ ispeziona).
        if (!await _db.EditResourceLocks.AnyAsync(l => l.ResourceKey == resourceKey, ct))
        {
            _db.EditResourceLocks.Add(new EditResourceLock
            {
                ResourceKey = resourceKey, LockedByUserId = userId, LockedByName = name,
                LockedAtUtc = now, LockExpiresUtc = expires,
            });
            try { await _db.SaveChangesAsync(ct); return Mine(userId, name, expires); }
            catch (DbUpdateException) { _db.ChangeTracker.Clear(); }   // race: qualcun altro ha inserito prima
        }
        return await InspectAsync(resourceKey, userId, ct);
    }

    public async Task<LockInfo> InspectAsync(string resourceKey, int userId, CancellationToken ct = default)
    {
        var l = await _db.EditResourceLocks.AsNoTracking()
            .Where(x => x.ResourceKey == resourceKey)
            .Select(x => new { x.LockedByUserId, x.LockedByName, x.LockExpiresUtc }).FirstOrDefaultAsync(ct);
        if (l is null || l.LockExpiresUtc <= DateTime.UtcNow) return LockInfo.Free();

        return new LockInfo
        {
            Locked = true,
            IsMine = l.LockedByUserId == userId,
            ByUserId = l.LockedByUserId,
            ByName = l.LockedByName,
            ExpiresUtc = l.LockExpiresUtc,
        };
    }

    public async Task RenewAsync(string resourceKey, int userId, int ttlMinutes, CancellationToken ct = default)
    {
        var expires = DateTime.UtcNow.AddMinutes(ttlMinutes);
        await _db.EditResourceLocks.Where(l => l.ResourceKey == resourceKey && l.LockedByUserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.LockExpiresUtc, expires), ct);
    }

    public Task<bool> IsHeldByAsync(string resourceKey, int userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return _db.EditResourceLocks.AnyAsync(
            l => l.ResourceKey == resourceKey && l.LockedByUserId == userId && l.LockExpiresUtc > now, ct);
    }

    public Task ReleaseAsync(string resourceKey, int userId, CancellationToken ct = default) =>
        _db.EditResourceLocks.Where(l => l.ResourceKey == resourceKey && l.LockedByUserId == userId).ExecuteDeleteAsync(ct);

    public async Task ForceUnlockAsync(string resourceKey, int actorUserId, CancellationToken ct = default)
    {
        // Come per il documento: chi lo teneva si legge prima, e un lock già libero non è un atto.
        var chi = await _db.EditResourceLocks.AsNoTracking().Where(l => l.ResourceKey == resourceKey)
            .Select(l => new { l.LockedByUserId, l.LockedByName, l.LockExpiresUtc }).FirstOrDefaultAsync(ct);
        if (chi is null) return;

        await _db.EditResourceLocks.Where(l => l.ResourceKey == resourceKey).ExecuteDeleteAsync(ct);
        AuditScribe.Write(_db, actorUserId, AuditAction.ForceUnlock, "EditResourceLock", resourceKey,
            new { HeldByUserId = chi.LockedByUserId, HeldByName = chi.LockedByName, chi.LockExpiresUtc });
        await _db.SaveChangesAsync(ct);
    }

    private static LockInfo Mine(int userId, string? name, DateTime expires) =>
        new() { Locked = true, IsMine = true, ByUserId = userId, ByName = name, ExpiresUtc = expires };
}
