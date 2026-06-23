using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Use-case di editing. Autorizzazione FIR-scoped via <see cref="IEditAuthorizationService"/>
/// (admin o grant sulla FIR del documento); identità (VID) per audit/CreatedBy. Verifica server-side.
/// </summary>
public interface IEditingService
{
    Task<IReadOnlyList<DocumentSummary>> ListDocumentsAsync(CancellationToken ct = default);
    Task<EditableDocument?> LoadForEditAsync(int documentId, CancellationToken ct = default);
    Task<int> CreateDraftAsync(int documentId, CancellationToken ct = default);
    Task UpdateBlockAsync(int blockId, BlockEdit edit, CancellationToken ct = default);
    Task<int> AddBlockAsync(int sectionId, BlockFormat format, BlockTier tier, BlockVisibility visibility, CancellationToken ct = default);
    Task DeleteBlockAsync(int blockId, CancellationToken ct = default);
    Task RenameSectionAsync(int sectionId, string title, CancellationToken ct = default);
    Task<int> AddSectionAsync(int versionId, int? parentSectionId, string title, BlockSection kind, CancellationToken ct = default);
    Task DeleteSectionAsync(int sectionId, CancellationToken ct = default);
    Task MoveSectionAsync(int sectionId, int direction, CancellationToken ct = default);
    Task MoveBlockAsync(int blockId, int direction, CancellationToken ct = default);
    Task PublishAsync(int versionId, string? note, CancellationToken ct = default);
    Task<IReadOnlyList<VersionInfo>> ListVersionsAsync(int documentId, CancellationToken ct = default);

    // --- Lock di editing esclusivo ---
    Task<LockInfo> AcquireLockAsync(int documentId, CancellationToken ct = default);
    Task<LockInfo> InspectLockAsync(int documentId, CancellationToken ct = default);
    Task ReleaseLockAsync(int documentId, CancellationToken ct = default);
    Task ForceUnlockAsync(int documentId, CancellationToken ct = default);
}

/// <summary>Sollevata quando l'utente non è autorizzato a editare (non admin e senza grant sulla FIR).</summary>
public sealed class EditNotAllowedException : Exception
{
    public EditNotAllowedException() : base("Editing non consentito: serve un permesso sulla FIR (o ruolo admin).") { }
}

/// <summary>Sollevata quando il documento è bloccato da un altro editor (o il lock è scaduto e va riacquisito).</summary>
public sealed class EditConflictException : Exception
{
    public EditConflictException(string message) : base(message) { }
}

/// <inheritdoc cref="IEditingService"/>
public sealed class EditingService : IEditingService
{
    private const int LockTtlMinutes = 30;

    private readonly IEditingRepository _repo;
    private readonly IEditAuthorizationService _authz;

    public EditingService(IEditingRepository repo, IEditAuthorizationService authz)
    {
        _repo = repo;
        _authz = authz;
    }

    // Lista dei documenti = metadati per il picker dell'editor (non sensibile). Le aperture/modifiche sono FIR-gated.
    public Task<IReadOnlyList<DocumentSummary>> ListDocumentsAsync(CancellationToken ct = default) =>
        _repo.ListDocumentsAsync(ct);

    public Task<IReadOnlyList<VersionInfo>> ListVersionsAsync(int documentId, CancellationToken ct = default) =>
        _repo.ListVersionsAsync(documentId, ct);

    public async Task<EditableDocument?> LoadForEditAsync(int documentId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditDocumentAsync(documentId, ct);
        return await _repo.LoadForEditAsync(documentId, ct);
    }

    public async Task<int> CreateDraftAsync(int documentId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditDocumentAsync(documentId, ct);
        // Creare/aprire una bozza = iniziare a editare → acquisisce (o conferma) il lock.
        var lk = await _repo.AcquireOrInspectLockAsync(documentId, _authz.CurrentVid ?? 0, _authz.CurrentName, LockTtlMinutes, ct);
        if (!lk.IsMine) throw LockedByOther(lk);
        return await _repo.CreateDraftAsync(documentId, _authz.CurrentVid ?? 0, ct);
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
        await _repo.PublishAsync(versionId, _authz.CurrentVid ?? 0, note, ct);
        await _repo.ReleaseLockAsync(docId, _authz.CurrentVid ?? 0, ct); // pubblicato → lascia il documento libero
    }

    // --- Lock ---
    public async Task<LockInfo> AcquireLockAsync(int documentId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditDocumentAsync(documentId, ct);
        return await _repo.AcquireOrInspectLockAsync(documentId, _authz.CurrentVid ?? 0, _authz.CurrentName, LockTtlMinutes, ct);
    }

    public Task<LockInfo> InspectLockAsync(int documentId, CancellationToken ct = default) =>
        _repo.InspectLockAsync(documentId, _authz.CurrentVid ?? 0, ct);

    public async Task ReleaseLockAsync(int documentId, CancellationToken ct = default) =>
        await _repo.ReleaseLockAsync(documentId, _authz.CurrentVid ?? 0, ct);

    public async Task ForceUnlockAsync(int documentId, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        await _repo.ForceUnlockAsync(documentId, ct);
    }

    /// <summary>Le mutazioni richiedono che il VID corrente detenga il lock; rinnova la scadenza (sliding).</summary>
    private async Task EnsureLockAsync(int documentId, CancellationToken ct)
    {
        if (!await _repo.IsLockHeldByAsync(documentId, _authz.CurrentVid ?? 0, ct))
            throw new EditConflictException("Documento bloccato da un altro editor o lock scaduto: riapri l'editor per riacquisirlo.");
        await _repo.RenewLockAsync(documentId, _authz.CurrentVid ?? 0, LockTtlMinutes, ct);
    }

    private static EditConflictException LockedByOther(LockInfo lk) =>
        new($"In modifica da VID {lk.ByVid} ({lk.ByName}) fino alle {lk.ExpiresUtc:HH:mm} UTC.");

    // --- Risoluzione del documento proprietario per l'autorizzazione FIR-scoped (ritorna il documentId) ---
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
