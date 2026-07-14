using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Application.Content;

/// <summary>Stato di revisione d'un documento (per il banner nell'editor). null/valorizzato = nessuna/pendente.</summary>
public sealed record DocumentReviewState(DateTime? NeedsReviewUtc, string? ReviewReason);

/// <summary>Documento impattato da un evento a monte (id + titolo leggibile per l'etichetta del task).</summary>
public sealed record AffectedDoc(int Id, string Title);

/// <summary>
/// Segnalazione di revisione ai documenti: quando un evento a monte (oggi: un settore nascosto in
/// <c>/vsop/admin/acc</c>) può aver reso stantii FREQUENZE / AoR / CONFIGURAZIONI, marca i documenti impattati
/// (banner nell'editor) e apre un incarico di revisione. Le due rappresentazioni — flag sul <see cref="Document"/>
/// e <see cref="EditorTask"/> — sono due facce dello stesso fatto e vengono create/gestite qui insieme.
/// </summary>
public interface IDocumentReviewService
{
    /// <summary>Marca per revisione i documenti (ACC + APP + vLOA) dove il settore compare e apre 1 incarico/doc
    /// (idempotente: nessun task duplicato se ne esiste già uno aperto con lo stesso titolo). Contesto già admin.</summary>
    Task FlagForHiddenSectorAsync(string composePosition, string accCode, CancellationToken ct = default);

    /// <summary>Stato di revisione del documento (per il banner). null se il documento non esiste.</summary>
    Task<DocumentReviewState?> GetAsync(int documentId, CancellationToken ct = default);

    /// <summary>Scioglie la revisione (l'editor ha rivisto). Richiede il permesso di editare l'ACC del documento.</summary>
    Task ClearReviewAsync(int documentId, CancellationToken ct = default);
}

/// <inheritdoc cref="IDocumentReviewService"/>
public sealed class DocumentReviewService : IDocumentReviewService
{
    private readonly IDocumentReviewRepository _repo;
    private readonly IEditorTaskService _tasks;
    private readonly IEditAuthorizationService _authz;

    public DocumentReviewService(IDocumentReviewRepository repo, IEditorTaskService tasks, IEditAuthorizationService authz)
    {
        _repo = repo;
        _tasks = tasks;
        _authz = authz;
    }

    public async Task FlagForHiddenSectorAsync(string composePosition, string accCode, CancellationToken ct = default)
    {
        var docs = await _repo.FindDocumentsForSectorAsync(composePosition, accCode, ct);
        if (docs.Count == 0) return;

        var reason = $"Settore {composePosition} nascosto: verifica FREQUENZE, AoR e CONFIGURAZIONI.";
        var now = DateTime.UtcNow;
        var existing = await _tasks.ListAllAsync(ct);   // contesto admin (chiamato dall'occultamento settore)

        foreach (var d in docs)
        {
            await _repo.SetReviewAsync(d.Id, now, reason, ct);

            var title = $"Revisione «{d.Title}» dopo occultamento {composePosition}";
            if (existing.Any(t => t.Status != EditorTaskStatus.Done
                                  && string.Equals(t.Title, title, StringComparison.Ordinal)))
                continue;   // incarico già aperto per questo doc+settore → non duplicare

            await _tasks.CreateAsync(new EditorTaskInput(
                Title: title, Description: reason,
                AssigneeUserId: 0, AssigneeName: null,           // pool: lo prende un editor dell'ACC
                Priority: EditorTaskPriority.Normal, DueAiracCycle: null,
                TargetType: null, TargetKey: null, TargetLabel: d.Title), ct);
        }
    }

    public Task<DocumentReviewState?> GetAsync(int documentId, CancellationToken ct = default) =>
        _repo.GetReviewAsync(documentId, ct);

    public async Task ClearReviewAsync(int documentId, CancellationToken ct = default)
    {
        var acc = await _repo.GetDocAccCodeAsync(documentId, ct);
        if (acc is not null) await _authz.EnsureCanEditAccAsync(acc, ct);
        await _repo.ClearReviewAsync(documentId, ct);
    }
}
