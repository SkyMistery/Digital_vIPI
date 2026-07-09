using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Use-case di editing. Autorizzazione ACC-scoped via <see cref="Auth.IEditAuthorizationService"/>
/// (admin o grant sulla ACC del documento); identità (UserId) per audit/CreatedBy. Verifica server-side.
/// </summary>
public interface IEditingService
{
    Task<IReadOnlyList<DocumentSummary>> ListDocumentsAsync(CancellationToken ct = default);

    /// <summary>Solo i documenti che l'utente corrente può editare (admin = tutti; altri = filtrati per grant sulla ACC).</summary>
    Task<IReadOnlyList<DocumentSummary>> ListEditableDocumentsAsync(CancellationToken ct = default);

    Task<EditableDocument?> LoadForEditAsync(int documentId, CancellationToken ct = default);
    Task<int> CreateDraftAsync(int documentId, CancellationToken ct = default);

    /// <summary>Id della vLOA della coppia (Home, Neighbour), o null. Una vLOA per coppia ACC↔ACC.</summary>
    Task<int?> ResolveVloaIdByPairAsync(string homeAccCode, string foreignAccCode, CancellationToken ct = default);

    /// <summary>
    /// Crea un nuovo documento da zero: vIPI ACC/aeroporto (scope = uno o più settori, uno primario) o
    /// vLOA (due settori Home/Neighbour). Ritorna l'Id del nuovo documento; poi si edita con la pipeline normale.
    /// </summary>
    Task<int> CreateDocumentAsync(DocumentType type, string title, IReadOnlyList<int>? scopeSectorIds,
        int? primarySectorId, int? homeSectorId, int? neighbourSectorId, CancellationToken ct = default);
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
