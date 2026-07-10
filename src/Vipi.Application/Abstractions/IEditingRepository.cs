using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Porta di scrittura dei contenuti (impl. EF in Infrastructure). Persistenza pura: non controlla
/// l'autorizzazione (lo fa <see cref="EditingService"/>) ma applica i vincoli strutturali
/// (versione bozza, profondità sezioni). Concorrenza ottimistica via RowVersion sul Document.
/// </summary>
public interface IEditingRepository
{
    /// <summary>Elenco documenti selezionabili nell'editor.</summary>
    Task<IReadOnlyList<DocumentSummary>> ListDocumentsAsync(CancellationToken ct = default);

    /// <summary>Carica la versione di lavoro (bozza se esiste, sennò la pubblicata corrente) come modello editabile. Null se il documento non esiste.</summary>
    Task<EditableDocument?> LoadForEditAsync(int documentId, CancellationToken ct = default);

    /// <summary>Id della vLOA della coppia (Home=<paramref name="homeAccCode"/>, Neighbour=<paramref name="foreignAccCode"/>).
    /// Una sola vLOA per coppia ACC↔ACC. Null se non esiste.</summary>
    Task<int?> FindVloaIdByPairAsync(string homeAccCode, string foreignAccCode, CancellationToken ct = default);

    /// <summary>Crea una nuova bozza clonando la versione pubblicata corrente. Se esiste già una bozza, ne ritorna l'Id (idempotente). Ritorna l'Id della versione bozza.</summary>
    Task<int> CreateDraftAsync(int documentId, int authorUserId, CancellationToken ct = default);

    /// <summary>
    /// Crea un nuovo documento da zero (vIPI con scope = N settori, uno primario; oppure vLOA con due parti
    /// Home/Neighbour) + la prima versione bozza con una sezione radice vuota. Per le vIPI imposta
    /// <c>Sector.DocumentId</c>/<c>IsPrimary</c> sui settori di scope. Ritorna l'Id del documento.
    /// </summary>
    Task<int> CreateDocumentAsync(DocumentType type, string title, Language language,
        IReadOnlyList<int>? scopeSectorIds, int? primarySectorId,
        (int homeSectorId, int neighbourSectorId)? parties, int authorUserId, CancellationToken ct = default);

    /// <summary>Codice ACC del settore (per l'autorizzazione ACC-scoped alla creazione). Null se il settore non esiste.</summary>
    Task<string?> GetAccCodeBySectorAsync(int sectorId, CancellationToken ct = default);

    /// <summary>
    /// Idempotente: garantisce che il settore primario abbia un documento vIPI (<see cref="DocumentType.Vipi"/>) con
    /// la versione bozza e le sezioni radice indicate (chiave catalogo + titolo, nell'ordine dato). Se il settore ha
    /// già un documento ne ritorna l'Id senza toccarlo. Per la migrazione ACC/APP/Airport su Document (doc refactor 08e).
    /// </summary>
    Task<int> EnsureVipiDocumentAsync(int primarySectorId, string title, Language language,
        IReadOnlyList<(string Key, string Title)> sections, int authorUserId, CancellationToken ct = default);

    /// <summary>Aggiorna i campi editabili di un blocco. Errore se il blocco non appartiene a una versione bozza.</summary>
    Task UpdateBlockAsync(int blockId, BlockEdit edit, CancellationToken ct = default);

    /// <summary>Aggiunge un blocco in coda a una sezione di una bozza. Ritorna l'Id del nuovo blocco.</summary>
    Task<int> AddBlockAsync(int sectionId, BlockFormat format, BlockTier tier, BlockVisibility visibility, CancellationToken ct = default);

    /// <summary>Elimina un blocco da una bozza. Errore se non è una bozza.</summary>
    Task DeleteBlockAsync(int blockId, CancellationToken ct = default);

    /// <summary>Rinomina una sezione di una bozza.</summary>
    Task RenameSectionAsync(int sectionId, string title, CancellationToken ct = default);

    /// <summary>Aggiunge una sezione (radice se parentSectionId è null) in coda ai fratelli. Errore se supera la profondità massima o se non è una bozza. Ritorna l'Id.</summary>
    Task<int> AddSectionAsync(int versionId, int? parentSectionId, string title, BlockSection kind, CancellationToken ct = default);

    /// <summary>Elimina una sezione (ricorsivamente: figli + blocchi) da una bozza.</summary>
    Task DeleteSectionAsync(int sectionId, CancellationToken ct = default);

    /// <summary>Sposta una sezione di un posto tra i fratelli (direction -1 = su, +1 = giù).</summary>
    Task MoveSectionAsync(int sectionId, int direction, CancellationToken ct = default);

    /// <summary>Sposta un blocco di un posto nella sua sezione (direction -1 = su, +1 = giù).</summary>
    Task MoveBlockAsync(int blockId, int direction, CancellationToken ct = default);

    /// <summary>Pubblica una versione bozza: la rende corrente, archivia la precedente pubblicata, aggiorna il documento e scrive l'audit.</summary>
    Task PublishAsync(int versionId, int actorUserId, string? note, CancellationToken ct = default);

    /// <summary>Storico versioni di un documento (più recente prima).</summary>
    Task<IReadOnlyList<VersionInfo>> ListVersionsAsync(int documentId, CancellationToken ct = default);

    // Risoluzione del documento proprietario (per l'autorizzazione ACC-scoped sulle op annidate).
    Task<int?> GetDocumentIdByVersionAsync(int versionId, CancellationToken ct = default);
    Task<int?> GetDocumentIdBySectionAsync(int sectionId, CancellationToken ct = default);
    Task<int?> GetDocumentIdByBlockAsync(int blockId, CancellationToken ct = default);

    // --- Lock di editing esclusivo (acquisizione atomica DB-side) ---
    /// <summary>Acquisisce il lock se libero/scaduto/proprio (TTL minuti, sliding); altrimenti ritorna l'info del titolare corrente.</summary>
    Task<LockInfo> AcquireOrInspectLockAsync(int documentId, int UserId, string? name, int ttlMinutes, CancellationToken ct = default);
    /// <summary>Ispeziona il lock dal punto di vista del UserId (IsMine), senza acquisirlo.</summary>
    Task<LockInfo> InspectLockAsync(int documentId, int UserId, CancellationToken ct = default);
    /// <summary>Estende la scadenza se il UserId è il titolare.</summary>
    Task RenewLockAsync(int documentId, int UserId, int ttlMinutes, CancellationToken ct = default);
    /// <summary>Rilascia il lock se il UserId è il titolare.</summary>
    Task ReleaseLockAsync(int documentId, int UserId, CancellationToken ct = default);
    /// <summary>Rilascia il lock incondizionatamente (force admin).</summary>
    Task ForceUnlockAsync(int documentId, CancellationToken ct = default);
    /// <summary>Vero se il UserId detiene un lock attivo (non scaduto) sul documento.</summary>
    Task<bool> IsLockHeldByAsync(int documentId, int UserId, CancellationToken ct = default);
}
