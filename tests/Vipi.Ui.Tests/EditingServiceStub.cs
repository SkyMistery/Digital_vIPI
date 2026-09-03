using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Ui.Tests;

/// <summary>
/// Base per i servizi di editing finti dei test di componente: <b>ogni</b> metodo solleva, e il test
/// sovrascrive i due o tre che il componente in prova deve davvero chiamare.
///
/// <para>⚠️ Sollevare invece di tornare un valore innocuo è la parte che conta: se un giorno il componente
/// chiamasse un metodo che il test non si aspetta, deve cadere il test — non passare in silenzio. È la
/// stessa scelta di <c>DocumentEditorShellTests.EditingFinto</c>, che però è privato e copre il solo
/// guscio; questa base serve a chi monta l'editor per davvero.</para>
/// </summary>
public abstract class EditingServiceStub : IEditingService
{
    protected static NotSupportedException NonUsato(string nome) =>
        new($"Il componente in prova non dovrebbe chiamare {nome}.");

    public virtual Task<EditableDocument?> LoadForEditAsync(int documentId, CancellationToken ct = default) => throw NonUsato(nameof(LoadForEditAsync));
    public virtual Task<IReadOnlyList<DocumentSummary>> ListDocumentsAsync(CancellationToken ct = default) => throw NonUsato(nameof(ListDocumentsAsync));
    public virtual Task<int> CreateDraftAsync(int documentId, CancellationToken ct = default) => throw NonUsato(nameof(CreateDraftAsync));
    public virtual Task<int?> ResolveVloaIdByPairAsync(string homeAccCode, string foreignAccCode, CancellationToken ct = default) => throw NonUsato(nameof(ResolveVloaIdByPairAsync));
    public virtual Task<int> CreateDocumentAsync(DocumentType type, string title, IReadOnlyList<int>? scopeSectorIds,
        int? primarySectorId, int? homeSectorId, int? neighbourSectorId, CancellationToken ct = default) => throw NonUsato(nameof(CreateDocumentAsync));
    public virtual Task UpdateBlockAsync(int blockId, BlockEdit edit, CancellationToken ct = default) => throw NonUsato(nameof(UpdateBlockAsync));
    public virtual Task<int> AddBlockAsync(int sectionId, BlockFormat format, BlockTier tier, BlockVisibility visibility, CancellationToken ct = default) => throw NonUsato(nameof(AddBlockAsync));
    public virtual Task DeleteBlockAsync(int blockId, CancellationToken ct = default) => throw NonUsato(nameof(DeleteBlockAsync));
    public virtual Task RenameSectionAsync(int sectionId, string title, CancellationToken ct = default) => throw NonUsato(nameof(RenameSectionAsync));
    public virtual Task SetSectionRenderModeAsync(int sectionId, RenderMode mode, CancellationToken ct = default) => throw NonUsato(nameof(SetSectionRenderModeAsync));
    public virtual Task SetSectionHiddenAsync(int sectionId, bool hidden, CancellationToken ct = default) => throw NonUsato(nameof(SetSectionHiddenAsync));
    public virtual Task SetSectionAudienceAsync(int sectionId, SectionAudience audience, CancellationToken ct = default) => throw NonUsato(nameof(SetSectionAudienceAsync));
    public virtual Task SetSectionBeforeParentBodyAsync(int sectionId, bool before, CancellationToken ct = default) => throw NonUsato(nameof(SetSectionBeforeParentBodyAsync));
    public virtual Task SetSectionLeadSentenceAsync(int sectionId, bool lead, CancellationToken ct = default) => throw NonUsato(nameof(SetSectionLeadSentenceAsync));
    public virtual Task<int> AddSectionAsync(int versionId, int? parentSectionId, string title, BlockSection kind, CancellationToken ct = default) => throw NonUsato(nameof(AddSectionAsync));
    public virtual Task DeleteSectionAsync(int sectionId, CancellationToken ct = default) => throw NonUsato(nameof(DeleteSectionAsync));
    public virtual Task MoveSectionAsync(int sectionId, int direction, CancellationToken ct = default) => throw NonUsato(nameof(MoveSectionAsync));
    public virtual Task MoveSectionBeforeAsync(int sectionId, int? beforeSectionId, CancellationToken ct = default) => throw NonUsato(nameof(MoveSectionBeforeAsync));
    public virtual Task MoveSectionToParentAsync(int sectionId, int? newParentSectionId, int? beforeSectionId, CancellationToken ct = default) => throw NonUsato(nameof(MoveSectionToParentAsync));
    public virtual Task MoveBlockAsync(int blockId, int direction, CancellationToken ct = default) => throw NonUsato(nameof(MoveBlockAsync));
    public virtual Task PublishAsync(int versionId, string? note, CancellationToken ct = default) => throw NonUsato(nameof(PublishAsync));
    public virtual Task<int> DiscardDraftAsync(int versionId, CancellationToken ct = default) => throw NonUsato(nameof(DiscardDraftAsync));
    public virtual Task<IReadOnlyList<VersionInfo>> ListVersionsAsync(int documentId, CancellationToken ct = default) => throw NonUsato(nameof(ListVersionsAsync));
    public virtual Task<LockInfo> AcquireLockAsync(int documentId, CancellationToken ct = default) => throw NonUsato(nameof(AcquireLockAsync));
    public virtual Task<LockInfo> InspectLockAsync(int documentId, CancellationToken ct = default) => throw NonUsato(nameof(InspectLockAsync));
    public virtual Task ReleaseLockAsync(int documentId, CancellationToken ct = default) => throw NonUsato(nameof(ReleaseLockAsync));
    public virtual Task ForceUnlockAsync(int documentId, CancellationToken ct = default) => throw NonUsato(nameof(ForceUnlockAsync));
}
