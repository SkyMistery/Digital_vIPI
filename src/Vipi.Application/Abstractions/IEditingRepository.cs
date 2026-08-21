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

    /// <summary>Carica la versione PUBBLICATA corrente (<c>CurrentVersionId</c>) come modello (stessa forma di
    /// <see cref="LoadForEditAsync"/>), ignorando le bozze: vista pubblica della vIPI ACC su Document (doc 08e-acc).
    /// Null se il documento non esiste o non ha una versione pubblicata.</summary>
    Task<EditableDocument?> LoadForViewAsync(int documentId, CancellationToken ct = default);

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
    /// <param name="liveKeys">Chiavi delle sezioni "live" (derivate o editoriali-strutturate: aor/frequencies/…): ricevono
    /// un blocco placeholder alla creazione così NON vengono potate dalla vista quando sono senza contenuto memorizzato
    /// (rese live dal renderer). Le altre sezioni restano senza blocchi (potate se vuote). Null = nessuna.</param>
    Task<int> EnsureVipiDocumentAsync(int primarySectorId, string title, Language language,
        IReadOnlyList<(string Key, string Title)> sections, int authorUserId,
        IReadOnlyCollection<string>? liveKeys = null, CancellationToken ct = default);

    /// <summary>
    /// Idempotente, variante <b>annidata</b> per la vIPI ACC (doc refactor 08e-acc): garantisce il documento vIPI del
    /// settore primario con una versione bozza e un albero a <b>blocchi</b> — ogni blocco è una sezione radice (depth 0,
    /// chiave = <c>Block.Key</c>) con le sue sezioni-catalogo come figli (depth 1). Se il settore ha già un documento
    /// ne ritorna l'Id senza toccarlo. Le sezioni figlie in <paramref name="liveKeys"/> ricevono il blocco placeholder
    /// (come <see cref="EnsureVipiDocumentAsync"/>). Ritorna l'Id del documento.
    /// </summary>
    Task<int> EnsureVipiDocumentTreeAsync(int primarySectorId, string title, Language language,
        IReadOnlyList<VipiBlockSpec> blocks, int authorUserId,
        IReadOnlyCollection<string>? liveKeys = null, CancellationToken ct = default);

    /// <summary>
    /// Aggiunge un blocco (sezione radice depth 0 + sue sezioni-catalogo figlie depth 1, con placeholder sulle live)
    /// in coda ai blocchi esistenti di una versione bozza. Per l'aggiunta di un gruppo APP dall'editor vIPI ACC (doc
    /// refactor 08e-acc). Errore se la versione non è una bozza. Ritorna l'Id della sezione-blocco creata.
    /// </summary>
    Task<int> AddBlockToVersionAsync(int versionId, VipiBlockSpec block, IReadOnlyCollection<string>? liveKeys = null, CancellationToken ct = default);

    /// <summary>
    /// Legge il <c>BodyJson</c> del primo blocco di UNA sezione identificata dall'Id (non per chiave radice): serve alla
    /// vIPI ACC dove le sezioni editoriali-strutturate vivono a depth 1 sotto il blocco, e il metadata del blocco
    /// (kind/membri/override) vive sulla sezione-blocco stessa. Null se sezione/blocco assenti. Doc refactor 08e-acc.
    /// </summary>
    Task<string?> GetSectionBlockJsonBySectionAsync(int sectionId, CancellationToken ct = default);

    /// <summary>
    /// Upsert del <c>BodyJson</c> in un unico blocco <c>Table</c> della sezione identificata dall'Id (crea il blocco se
    /// manca; <paramref name="json"/> null/vuoto lo azzera). Errore se la versione della sezione non è una bozza. Doc
    /// refactor 08e-acc. Il ciclo bozza→pubblicazione resta del chiamante.
    /// </summary>
    Task SaveSectionBlockJsonBySectionAsync(int sectionId, string? json, int authorUserId, CancellationToken ct = default);

    /// <summary>
    /// Legge il JSON strutturato di una sezione editoriale-strutturata (separations/vfr/config…): il <c>BodyJson</c>
    /// del primo blocco della sezione radice con quella chiave, dalla versione di lavoro (bozza se esiste, sennò la
    /// pubblicata). Null se documento/sezione/blocco assenti. Per lo storage APP/ACC/Airport su Document (doc refactor 08e).
    /// </summary>
    Task<string?> GetSectionBlockJsonAsync(int documentId, string sectionKey, CancellationToken ct = default);

    /// <summary>
    /// Upsert del JSON strutturato di una sezione editoriale-strutturata: scrive il <c>BodyJson</c> in un unico blocco
    /// <c>Table</c> della sezione radice keyed della bozza (crea il blocco se manca; <paramref name="json"/> null/vuoto lo
    /// azzera). Errore se la versione di lavoro non è una bozza o la sezione manca. Per lo storage APP/ACC/Airport su
    /// Document (doc refactor 08e). Il ciclo bozza→pubblicazione resta del chiamante.
    /// </summary>
    Task SaveSectionBlockJsonAsync(int documentId, string sectionKey, string? json, int authorUserId, CancellationToken ct = default);

    /// <summary>Aggiorna i campi editabili di un blocco. Errore se il blocco non appartiene a una versione bozza.</summary>
    Task UpdateBlockAsync(int blockId, BlockEdit edit, CancellationToken ct = default);

    /// <summary>Aggiunge un blocco in coda a una sezione di una bozza. Ritorna l'Id del nuovo blocco.</summary>
    Task<int> AddBlockAsync(int sectionId, BlockFormat format, BlockTier tier, BlockVisibility visibility, CancellationToken ct = default);

    /// <summary>Elimina un blocco da una bozza. Errore se non è una bozza.</summary>
    Task DeleteBlockAsync(int blockId, CancellationToken ct = default);

    /// <summary>Rinomina una sezione di una bozza.</summary>
    Task RenameSectionAsync(int sectionId, string title, CancellationToken ct = default);

    /// <summary>Imposta il <see cref="RenderMode"/> di una sezione di una bozza (doc 10 §3a). Errore se non è una bozza.</summary>
    Task SetSectionRenderModeAsync(int sectionId, RenderMode mode, CancellationToken ct = default);

    /// <summary>Nasconde/mostra una sezione di una bozza (doc 11 §3c). Errore se non è una bozza.</summary>
    Task SetSectionHiddenAsync(int sectionId, bool hidden, CancellationToken ct = default);

    /// <summary>Colloca una sotto-sezione prima/dopo il corpo del padre (doc 11 §3g). Errore se non è una bozza.</summary>
    Task SetSectionBeforeParentBodyAsync(int sectionId, bool before, CancellationToken ct = default);

    /// <summary>Prosa a CAPOFILA per una sezione derivata a tabelle: una frase che introduce la tabella invece
    /// di una per clausola.</summary>
    Task SetSectionLeadSentenceAsync(int sectionId, bool lead, CancellationToken ct = default);

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

    /// <summary>
    /// Elimina una versione <c>Draft</c> con tutto il suo contenuto e scrive l'audit: è lo «scarta bozza».
    /// Stesso ordine di cancellazione della potatura (blocchi → sezioni post-order → versione) per i FK
    /// <c>Restrict</c>, e stessa liberazione delle immagini rimaste orfane.
    ///
    /// <para>Le condizioni (che sia davvero una bozza, e che ci sia una versione pubblicata a cui tornare)
    /// le impone il <b>servizio</b>: qui si esegue. Ritorna il numero di versione scartato, per il messaggio.</para>
    /// </summary>
    Task<int> DiscardDraftAsync(int versionId, int actorUserId, CancellationToken ct = default);

    /// <summary>Pota le versioni <c>Archived</c> del documento oltre le più recenti <paramref name="keepN"/> (per
    /// VersionNumber): elimina ogni versione eccedente coi suoi <c>ContentBlock</c> e <c>DocumentSection</c> in ordine
    /// esplicito (blocchi → sezioni post-order → versione) per rispettare i FK Restrict su <c>Section</c> e
    /// <c>ParentSection</c>. La versione corrente (Published) e le bozze (Draft) non vengono mai toccate. Retention:
    /// la release porta già la fotografia. Ritorna il numero di versioni rimosse.</summary>
    Task<int> PruneArchivedVersionsAsync(int documentId, int keepN, CancellationToken ct = default);

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
    /// <summary>Libera il lock chiunque lo tenga. <paramref name="actorUserId"/> è chi forza: togliere il lock a
    /// un'altra persona è un atto d'autorità, e finisce nel registro di audit con il nome di chi lo teneva.</summary>
    Task ForceUnlockAsync(int documentId, int actorUserId, CancellationToken ct = default);
    /// <summary>Vero se il UserId detiene un lock attivo (non scaduto) sul documento.</summary>
    Task<bool> IsLockHeldByAsync(int documentId, int UserId, CancellationToken ct = default);
}
