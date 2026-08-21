using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Implementazione EF di <see cref="IAuditLogReader"/> (lettura audit per il viewer admin).</summary>
public sealed class EfAuditLogReader : IAuditLogReader
{
    private readonly VipiDbContext _db;
    public EfAuditLogReader(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<AuditEntry>> ListRecentAsync(DateTime? sinceUtc = null, int max = 500, CancellationToken ct = default)
    {
        return await _db.AuditLogs
            .Where(a => sinceUtc == null || a.TimestampUtc >= sinceUtc)
            .OrderByDescending(a => a.Id)
            .Take(Math.Clamp(max, 1, 2000))
            .Select(a => new AuditEntry(a.Id, a.UserId, a.Action, a.EntityType, a.EntityId, a.TimestampUtc, a.DetailsJson))
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public Task<int> CountAsync(DateTime? sinceUtc = null, CancellationToken ct = default) =>
        _db.AuditLogs.CountAsync(a => sinceUtc == null || a.TimestampUtc >= sinceUtc, ct);

    public async Task<IReadOnlyList<AuditEntry>> ListForEntityAsync(string entityType, string entityId, int max = 50, CancellationToken ct = default)
    {
        return await _db.AuditLogs
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.Id)
            .Take(Math.Clamp(max, 1, 500))
            .Select(a => new AuditEntry(a.Id, a.UserId, a.Action, a.EntityType, a.EntityId, a.TimestampUtc, a.DetailsJson))
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AuditEntry>> ListForEntitiesAsync(string entityType, IReadOnlyList<string> entityIds, int max = 50, CancellationToken ct = default)
    {
        if (entityIds.Count == 0) return Array.Empty<AuditEntry>();
        return await _db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == entityType && entityIds.Contains(a.EntityId))
            .OrderByDescending(a => a.TimestampUtc)
            .Take(max)
            .Select(a => new AuditEntry(a.Id, a.UserId, a.Action, a.EntityType, a.EntityId, a.TimestampUtc, a.DetailsJson))
            .ToListAsync(ct);
    }
}
