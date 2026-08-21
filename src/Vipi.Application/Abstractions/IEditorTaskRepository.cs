using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Application.Abstractions;

/// <summary>Dati per creare un incarico.</summary>
public sealed record EditorTaskInput(
    string Title, string? Description, int AssigneeUserId, string? AssigneeName,
    EditorTaskPriority Priority, string? DueAiracCycle,
    ReleaseTargetType? TargetType, string? TargetKey, string? TargetLabel);

/// <summary>
/// Persistenza degli incarichi editoriali.
///
/// <para>⚠️ Ogni scrittura porta <b>chi la fa</b>: dal 22 agosto 2026 gli incarichi lasciano traccia nel
/// registro di audit, e la riga si scrive nella stessa transazione dell'atto che descrive — quindi l'attore
/// deve arrivare fin qui, non fermarsi al service.</para>
/// </summary>
public interface IEditorTaskRepository
{
    Task<IReadOnlyList<EditorTask>> ListByAssigneeAsync(int userId, CancellationToken ct = default);
    Task<IReadOnlyList<EditorTask>> ListAllAsync(CancellationToken ct = default);
    Task<EditorTask?> GetAsync(int id, CancellationToken ct = default);
    Task<int> AddAsync(EditorTaskInput input, int createdByUserId, CancellationToken ct = default);
    Task UpdateStatusAsync(int id, EditorTaskStatus status, int actorUserId, CancellationToken ct = default);
    Task AssignAsync(int id, int assigneeUserId, string? assigneeName, int actorUserId, CancellationToken ct = default);
    Task DeleteAsync(int id, int actorUserId, CancellationToken ct = default);
}
