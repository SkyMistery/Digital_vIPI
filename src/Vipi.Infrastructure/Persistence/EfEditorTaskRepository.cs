using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="IEditorTaskRepository"/>
public sealed class EfEditorTaskRepository : IEditorTaskRepository
{
    private readonly VipiDbContext _db;
    public EfEditorTaskRepository(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<EditorTask>> ListByAssigneeAsync(int userId, CancellationToken ct = default) =>
        await _db.EditorTasks.AsNoTracking()
            .Where(t => t.AssigneeUserId == userId)
            .OrderBy(t => t.Status == EditorTaskStatus.Done).ThenByDescending(t => t.UpdatedUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<EditorTask>> ListAllAsync(CancellationToken ct = default) =>
        await _db.EditorTasks.AsNoTracking()
            .OrderBy(t => t.Status == EditorTaskStatus.Done).ThenByDescending(t => t.UpdatedUtc)
            .ToListAsync(ct);

    public Task<EditorTask?> GetAsync(int id, CancellationToken ct = default) =>
        _db.EditorTasks.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<int> AddAsync(EditorTaskInput input, int createdByUserId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var t = new EditorTask
        {
            Title = input.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
            AssigneeUserId = input.AssigneeUserId,
            AssigneeName = input.AssigneeName,
            CreatedByUserId = createdByUserId,
            Status = EditorTaskStatus.Todo,
            Priority = input.Priority,
            DueAiracCycle = string.IsNullOrWhiteSpace(input.DueAiracCycle) ? null : input.DueAiracCycle.Trim(),
            TargetType = input.TargetType,
            TargetKey = input.TargetKey,
            TargetLabel = input.TargetLabel,
            CreatedUtc = now,
            UpdatedUtc = now,
        };
        _db.EditorTasks.Add(t);
        await _db.SaveChangesAsync(ct);
        return t.Id;
    }

    public async Task UpdateStatusAsync(int id, EditorTaskStatus status, CancellationToken ct = default)
    {
        var t = await _db.EditorTasks.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new InvalidOperationException($"Incarico {id} inesistente.");
        t.Status = status;
        t.UpdatedUtc = DateTime.UtcNow;
        t.CompletedUtc = status == EditorTaskStatus.Done ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync(ct);
    }

    public async Task AssignAsync(int id, int assigneeUserId, string? assigneeName, CancellationToken ct = default)
    {
        var t = await _db.EditorTasks.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new InvalidOperationException($"Incarico {id} inesistente.");
        t.AssigneeUserId = assigneeUserId;
        t.AssigneeName = assigneeName;
        t.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var t = await _db.EditorTasks.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return;
        _db.EditorTasks.Remove(t);
        await _db.SaveChangesAsync(ct);
    }
}
