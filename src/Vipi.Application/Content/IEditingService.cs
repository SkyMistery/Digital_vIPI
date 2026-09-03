using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Use-case di editing. Autorizzazione ACC-scoped via <see cref="Auth.IEditAuthorizationService"/>
/// (admin o grant sulla ACC del documento); identità (UserId) per audit/CreatedBy. Verifica server-side.
/// </summary>
/// <summary>
/// «Dammi il documento in lavorazione», e nient'altro.
///
/// <para>⚠️ Esiste perché il <b>correttore delle traduzioni</b> ha bisogno di leggere le frasi di un
/// documento e di niente altro: farlo dipendere da tutto <see cref="IEditingService"/> — trenta metodi che
/// creano, pubblicano, scartano — vorrebbe dire dargli in mano l'editing intero per una lettura, e
/// obbligare ogni suo test a implementare trenta metodi che non chiamerà mai.</para>
/// </summary>
public interface IDocumentForReview
{
    /// <inheritdoc cref="IEditingService.LoadForEditAsync"/>
    Task<EditableDocument?> LoadForEditAsync(int documentId, CancellationToken ct = default);
}

public interface IEditingService : IDocumentForReview
{
    Task<IReadOnlyList<DocumentSummary>> ListDocumentsAsync(CancellationToken ct = default);

    /// <summary>Il documento in lavorazione: struttura, sezioni e blocchi della versione di lavoro.</summary>
    new Task<EditableDocument?> LoadForEditAsync(int documentId, CancellationToken ct = default);
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
    /// <summary>Imposta il RenderMode (Live/Frozen) di una sezione derivabile nella bozza (doc 10 §3a/§S4c): governa se
    /// al publish l'output della sezione viene congelato nello snapshot (Frozen) o reso live al view (Live).</summary>
    Task SetSectionRenderModeAsync(int sectionId, RenderMode mode, CancellationToken ct = default);
    /// <summary>Nasconde/mostra una sezione nella bozza (doc 11 §3c): l'editor la mostra comunque, la vista pubblica e
    /// le anteprime release la omettono. Stato versionato ⇒ diventa effettivo solo con la pubblicazione.</summary>
    Task SetSectionHiddenAsync(int sectionId, bool hidden, CancellationToken ct = default);
    /// <summary>Colloca una sotto-sezione PRIMA o dopo il corpo della sezione padre (doc 11 §3g): blocchi per una
    /// sezione editoriale, resa derivata per una strutturata. Fra loro le sotto-sezioni restano ordinate per Order.</summary>
    /// <summary>A chi si rivolge la sezione (carta vSOP militari §3). ⚠️ Non è controllo d'accesso: il
    /// documento resta pubblico, e la vista ATC la apre chiunque cambi l'indirizzo.</summary>
    Task SetSectionAudienceAsync(int sectionId, SectionAudience audience, CancellationToken ct = default);

    Task SetSectionBeforeParentBodyAsync(int sectionId, bool before, CancellationToken ct = default);

    /// <summary>Prosa a CAPOFILA per una sezione derivata a tabelle.</summary>
    Task SetSectionLeadSentenceAsync(int sectionId, bool lead, CancellationToken ct = default);
    Task<int> AddSectionAsync(int versionId, int? parentSectionId, string title, BlockSection kind, CancellationToken ct = default);
    Task DeleteSectionAsync(int sectionId, CancellationToken ct = default);
    Task MoveSectionAsync(int sectionId, int direction, CancellationToken ct = default);

    /// <summary>Sposta una sezione PRIMA del fratello indicato (null = in coda): il trascinamento nel
    /// menu-sezioni. Non riparenta — per cambiare gruppo c'è <see cref="MoveSectionToParentAsync"/>.</summary>
    Task MoveSectionBeforeAsync(int sectionId, int? beforeSectionId, CancellationToken ct = default);

    /// <summary>Sposta una sezione LIBERA in un altro gruppo: nuovo padre (null = radice) e posizione dentro
    /// quel gruppo. Le guardie stanno nel repository — vedi <c>IEditingRepository.MoveSectionToParentAsync</c>.</summary>
    Task MoveSectionToParentAsync(int sectionId, int? newParentSectionId, int? beforeSectionId, CancellationToken ct = default);
    Task MoveBlockAsync(int blockId, int direction, CancellationToken ct = default);
    Task PublishAsync(int versionId, string? note, CancellationToken ct = default);

    /// <summary>
    /// Scarta una bozza: la elimina col suo contenuto, scrive l'audit e libera il lock. Ritorna il numero di
    /// versione scartato.
    ///
    /// <para>Rifiuta con <see cref="ValidationException"/> se la versione non è una bozza, o se è l'<b>unica</b>
    /// versione del documento — in quel caso non ci sarebbe nulla a cui tornare.</para>
    /// </summary>
    Task<int> DiscardDraftAsync(int versionId, CancellationToken ct = default);
    Task<IReadOnlyList<VersionInfo>> ListVersionsAsync(int documentId, CancellationToken ct = default);

    // --- Lock di editing esclusivo ---
    Task<LockInfo> AcquireLockAsync(int documentId, CancellationToken ct = default);
    Task<LockInfo> InspectLockAsync(int documentId, CancellationToken ct = default);
    Task ReleaseLockAsync(int documentId, CancellationToken ct = default);
    Task ForceUnlockAsync(int documentId, CancellationToken ct = default);
}
