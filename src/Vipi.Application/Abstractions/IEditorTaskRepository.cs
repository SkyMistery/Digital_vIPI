using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Application.Abstractions;

/// <summary>Dati per creare un incarico.</summary>
/// <param name="FromImpactId">La segnalazione di sistema da cui questo incarico è stato «preso in carico»
/// (<c>docs/feature/2026-08-26-da-fare-una-lista-sola.md</c> §2/D5). <c>null</c> = incarico nato da una
/// persona. Serve a non mostrare <b>due volte</b> lo stesso lavoro nella lista «Da fare»: una come fatto e
/// una come impegno.</param>
public sealed record EditorTaskInput(
    string Title, string? Description, int AssigneeUserId, string? AssigneeName,
    EditorTaskPriority Priority, string? DueAiracCycle,
    ReleaseTargetType? TargetType, string? TargetKey, string? TargetLabel,
    int? FromImpactId = null);

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
