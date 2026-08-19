using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <inheritdoc cref="IEditingService"/>
public sealed class EditingService : IEditingService
{
    private const int LockTtlMinutes = 30;

    private readonly IEditingRepository _repo;
    private readonly IEditAuthorizationService _authz;
    private readonly ReleaseRetentionOptions _retention;

    public EditingService(IEditingRepository repo, IEditAuthorizationService authz, IOptions<ReleaseRetentionOptions> retention)
    {
        _repo = repo;
        _authz = authz;
        _retention = retention.Value;
    }

    // Lista dei documenti = metadati per il picker dell'editor (non sensibile). Le aperture/modifiche sono ACC-gated.
    public Task<IReadOnlyList<DocumentSummary>> ListDocumentsAsync(CancellationToken ct = default) =>
        _repo.ListDocumentsAsync(ct);

    // Picker editor: filtra i documenti per i permessi del UserId corrente (admin = tutti; altri = grant sulla ACC).
    public async Task<IReadOnlyList<DocumentSummary>> ListEditableDocumentsAsync(CancellationToken ct = default)
    {
        var all = await _repo.ListDocumentsAsync(ct);
        if (_authz.IsAdmin) return all;

        var editable = new List<DocumentSummary>();
        foreach (var d in all)
            if (await _authz.CanEditDocumentAsync(d.Id, ct))
                editable.Add(d);
        return editable;
    }

    public Task<IReadOnlyList<VersionInfo>> ListVersionsAsync(int documentId, CancellationToken ct = default) =>
        _repo.ListVersionsAsync(documentId, ct);

    public async Task<EditableDocument?> LoadForEditAsync(int documentId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditDocumentAsync(documentId, ct);
        return await _repo.LoadForEditAsync(documentId, ct);
    }

    public Task<int?> ResolveVloaIdByPairAsync(string homeAccCode, string foreignAccCode, CancellationToken ct = default) =>
        _repo.FindVloaIdByPairAsync((homeAccCode ?? "").Trim().ToUpperInvariant(), (foreignAccCode ?? "").Trim().ToUpperInvariant(), ct);

    public async Task<int> CreateDocumentAsync(DocumentType type, string title, IReadOnlyList<int>? scopeSectorIds,
        int? primarySectorId, int? homeSectorId, int? neighbourSectorId, CancellationToken ct = default)
    {
        title = (title ?? "").Trim();
        if (title.Length == 0) throw new Aor.ValidationException("Titolo documento obbligatorio.");

        (int, int)? parties = null;
        IReadOnlyList<int>? scope = null;
        int? primary = null;
        string accCode;
        if (type == DocumentType.Vloa)
        {
            if (homeSectorId is not int home || neighbourSectorId is not int neigh)
                throw new Aor.ValidationException("La vLOA richiede un settore Home e uno Neighbour.");
            if (home == neigh) throw new Aor.ValidationException("Home e Neighbour non possono coincidere.");
            parties = (home, neigh);
            accCode = await _repo.GetAccCodeBySectorAsync(home, ct)
                ?? throw new Aor.ValidationException("Settore Home inesistente.");
        }
        else
        {
            if (scopeSectorIds is null || scopeSectorIds.Count == 0)
                throw new Aor.ValidationException("La vIPI richiede almeno un settore di scope.");
            scope = scopeSectorIds.Distinct().ToList();
            primary = primarySectorId ?? scope[0];
            if (!scope.Contains(primary.Value))
                throw new Aor.ValidationException("Il settore primario deve far parte dello scope.");
            accCode = await _repo.GetAccCodeBySectorAsync(primary.Value, ct)
                ?? throw new Aor.ValidationException("Settore di scope inesistente.");
        }

        await _authz.EnsureCanEditAccAsync(accCode, ct);
        var language = type == DocumentType.Vloa ? Language.En : Language.It;
        return await _repo.CreateDocumentAsync(type, title, language, scope, primary, parties, _authz.CurrentUserId ?? 0, ct);
    }

    public async Task<int> CreateDraftAsync(int documentId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditDocumentAsync(documentId, ct);
        // Creare/aprire una bozza = iniziare a editare → acquisisce (o conferma) il lock.
        var lk = await _repo.AcquireOrInspectLockAsync(documentId, _authz.CurrentUserId ?? 0, _authz.CurrentName, LockTtlMinutes, ct);
        if (!lk.IsMine) throw LockedByOther(lk);
        return await _repo.CreateDraftAsync(documentId, _authz.CurrentUserId ?? 0, ct);
    }

    public async Task UpdateBlockAsync(int blockId, BlockEdit edit, CancellationToken ct = default)
    {
        var docId = await AuthorizeBlockAsync(blockId, ct);
        await EnsureLockAsync(docId, ct);
        await _repo.UpdateBlockAsync(blockId, edit, ct);
    }

    public async Task<int> AddBlockAsync(int sectionId, BlockFormat format, BlockTier tier, BlockVisibility visibility, CancellationToken ct = default)
    {
        var docId = await AuthorizeSectionAsync(sectionId, ct);
        await EnsureLockAsync(docId, ct);
        return await _repo.AddBlockAsync(sectionId, format, tier, visibility, ct);
    }

    public async Task DeleteBlockAsync(int blockId, CancellationToken ct = default)
    {
        var docId = await AuthorizeBlockAsync(blockId, ct);
        await EnsureLockAsync(docId, ct);
        await _repo.DeleteBlockAsync(blockId, ct);
    }

    public async Task RenameSectionAsync(int sectionId, string title, CancellationToken ct = default)
    {
        var docId = await AuthorizeSectionAsync(sectionId, ct);
        await EnsureLockAsync(docId, ct);
        await _repo.RenameSectionAsync(sectionId, title, ct);
    }

    public async Task SetSectionRenderModeAsync(int sectionId, RenderMode mode, CancellationToken ct = default)
    {
        var docId = await AuthorizeSectionAsync(sectionId, ct);
        await EnsureLockAsync(docId, ct);
        await _repo.SetSectionRenderModeAsync(sectionId, mode, ct);
    }

    public async Task SetSectionBeforeParentBodyAsync(int sectionId, bool before, CancellationToken ct = default)
    {
        var docId = await AuthorizeSectionAsync(sectionId, ct);
        await EnsureLockAsync(docId, ct);
        await _repo.SetSectionBeforeParentBodyAsync(sectionId, before, ct);
    }

    public async Task SetSectionLeadSentenceAsync(int sectionId, bool lead, CancellationToken ct = default)
    {
        var docId = await AuthorizeSectionAsync(sectionId, ct);
        await EnsureLockAsync(docId, ct);
        await _repo.SetSectionLeadSentenceAsync(sectionId, lead, ct);
    }

    public async Task SetSectionHiddenAsync(int sectionId, bool hidden, CancellationToken ct = default)
    {
        var docId = await AuthorizeSectionAsync(sectionId, ct);
        await EnsureLockAsync(docId, ct);
        await _repo.SetSectionHiddenAsync(sectionId, hidden, ct);
    }

    public async Task<int> AddSectionAsync(int versionId, int? parentSectionId, string title, BlockSection kind, CancellationToken ct = default)
    {
        var docId = await AuthorizeVersionAsync(versionId, ct);
        await EnsureLockAsync(docId, ct);
        return await _repo.AddSectionAsync(versionId, parentSectionId, title, kind, ct);
    }

    public async Task DeleteSectionAsync(int sectionId, CancellationToken ct = default)
    {
        var docId = await AuthorizeSectionAsync(sectionId, ct);
        await EnsureLockAsync(docId, ct);
        await _repo.DeleteSectionAsync(sectionId, ct);
    }

    public async Task MoveSectionAsync(int sectionId, int direction, CancellationToken ct = default)
    {
        var docId = await AuthorizeSectionAsync(sectionId, ct);
        await EnsureLockAsync(docId, ct);
        await _repo.MoveSectionAsync(sectionId, direction, ct);
    }

    public async Task MoveBlockAsync(int blockId, int direction, CancellationToken ct = default)
    {
        var docId = await AuthorizeBlockAsync(blockId, ct);
        await EnsureLockAsync(docId, ct);
        await _repo.MoveBlockAsync(blockId, direction, ct);
    }

    public async Task PublishAsync(int versionId, string? note, CancellationToken ct = default)
    {
        var docId = await AuthorizeVersionAsync(versionId, ct);
        await EnsureLockAsync(docId, ct);
        await _repo.PublishAsync(versionId, _authz.CurrentUserId ?? 0, note, ct);
        // Retention versioni: dopo l'archiviazione della precedente (in _repo.PublishAsync) → cap Archived esatto, non
        // N+1. Le release non referenziano le versioni (portano la fotografia), quindi potare è sicuro. Stessa regola del
        // release-publish (ReleaseService.PublishNowAsync) e del boot sweep.
        await _repo.PruneArchivedVersionsAsync(docId, _retention.KeepArchivedVersionsPerDocument, ct);
        await _repo.ReleaseLockAsync(docId, _authz.CurrentUserId ?? 0, ct); // pubblicato → lascia il documento libero
    }

    public async Task<int> DiscardDraftAsync(int versionId, CancellationToken ct = default)
    {
        var docId = await AuthorizeVersionAsync(versionId, ct);
        await EnsureLockAsync(docId, ct);

        var versions = await _repo.ListVersionsAsync(docId, ct);
        var draft = versions.FirstOrDefault(v => v.Id == versionId)
                    ?? throw new KeyNotFoundException($"Versione {versionId} inesistente.");

        // Si scarta una BOZZA, non una versione qualsiasi: pubblicate e archiviate sono storia del documento,
        // e cancellarle romperebbe ciò che le release dichiarano di aver fotografato.
        if (draft.Status != DocumentStatus.Draft)
            throw new ValidationException($"La versione {draft.VersionNumber} non è una bozza ({draft.Status}): non si scarta.");

        // Serve qualcosa a cui tornare. Su un documento mai pubblicato la bozza È il documento: scartarla
        // lascerebbe un guscio senza contenuto e senza vista pubblica — chi vuole disfarsene elimini il
        // documento, che è un'altra azione con altre conseguenze.
        if (!versions.Any(v => v.Id != versionId && v.Status is DocumentStatus.Published or DocumentStatus.Archived))
            throw new ValidationException(
                "Questa bozza è l'unica versione del documento: scartandola non resterebbe nulla da mostrare. " +
                "Pubblicala, oppure elimina il documento.");

        var numero = await _repo.DiscardDraftAsync(versionId, _authz.CurrentUserId ?? 0, ct);
        // Scartare è finire di editare: come la pubblicazione, lascia il documento libero per gli altri.
        await _repo.ReleaseLockAsync(docId, _authz.CurrentUserId ?? 0, ct);
        return numero;
    }

    // --- Lock ---
    public async Task<LockInfo> AcquireLockAsync(int documentId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditDocumentAsync(documentId, ct);
        return await _repo.AcquireOrInspectLockAsync(documentId, _authz.CurrentUserId ?? 0, _authz.CurrentName, LockTtlMinutes, ct);
    }

    public Task<LockInfo> InspectLockAsync(int documentId, CancellationToken ct = default) =>
        _repo.InspectLockAsync(documentId, _authz.CurrentUserId ?? 0, ct);

    public async Task ReleaseLockAsync(int documentId, CancellationToken ct = default) =>
        await _repo.ReleaseLockAsync(documentId, _authz.CurrentUserId ?? 0, ct);

    public async Task ForceUnlockAsync(int documentId, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        await _repo.ForceUnlockAsync(documentId, ct);
    }

    /// <summary>Le mutazioni richiedono che il UserId corrente detenga il lock; rinnova la scadenza (sliding).</summary>
    private async Task EnsureLockAsync(int documentId, CancellationToken ct)
    {
        if (!await _repo.IsLockHeldByAsync(documentId, _authz.CurrentUserId ?? 0, ct))
            throw new EditConflictException("Documento bloccato da un altro editor o lock scaduto: riapri l'editor per riacquisirlo.");
        await _repo.RenewLockAsync(documentId, _authz.CurrentUserId ?? 0, LockTtlMinutes, ct);
    }

    private static EditConflictException LockedByOther(LockInfo lk) =>
        new($"In modifica da VID {lk.ByUserId} ({lk.ByName}) fino alle {lk.ExpiresUtc:HH:mm} UTC.");

    // --- Risoluzione del documento proprietario per l'autorizzazione ACC-scoped (ritorna il documentId) ---
    private async Task<int> AuthorizeVersionAsync(int versionId, CancellationToken ct)
    {
        var docId = await _repo.GetDocumentIdByVersionAsync(versionId, ct) ?? throw new EditNotAllowedException();
        await _authz.EnsureCanEditDocumentAsync(docId, ct);
        return docId;
    }

    private async Task<int> AuthorizeSectionAsync(int sectionId, CancellationToken ct)
    {
        var docId = await _repo.GetDocumentIdBySectionAsync(sectionId, ct) ?? throw new EditNotAllowedException();
        await _authz.EnsureCanEditDocumentAsync(docId, ct);
        return docId;
    }

    private async Task<int> AuthorizeBlockAsync(int blockId, CancellationToken ct)
    {
        var docId = await _repo.GetDocumentIdByBlockAsync(blockId, ct) ?? throw new EditNotAllowedException();
        await _authz.EnsureCanEditDocumentAsync(docId, ct);
        return docId;
    }
}
