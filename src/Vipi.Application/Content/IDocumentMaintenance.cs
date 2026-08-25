namespace Vipi.Application.Content;

/// <summary>
/// Riconciliazioni one-shot sui documenti, eseguite all'avvio dopo la migrazione dello schema (doc 11 §3a/§3c).
/// Sono **idempotenti**: rieseguirle non cambia nulla. Stanno qui e non in una migrazione EF perché le migrazioni
/// del repo sono SQLite-flavored, mentre il deploy hostato crea lo schema col <c>PostgresSchemaReconciler</c>: un
/// backfill scritto in SQL di migrazione non girerebbe in produzione.
/// </summary>
public interface IDocumentMaintenance
{
    /// <summary>Assegna una chiave univoca alle sezioni libere nate con la chiave storica ambigua
    /// <c>"custom"</c>. Ritorna il numero di sezioni riconciliate.</summary>
    Task<int> ReconcileCustomSectionKeysAsync(CancellationToken ct = default);

    /// <summary>Porta lo stato «sezione nascosta» dai tre storage storici (blockmeta ACC per chiave,
    /// <c>DocumentProfile</c> per chiave nell'APP e per titolo nella vLOA) al flag versionato
    /// <c>DocumentSection.IsHidden</c> (doc 11 §3c), e azzera le sorgenti. Ritorna le sezioni marcate.</summary>
    Task<int> MigrateHiddenSectionsAsync(CancellationToken ct = default);

    /// <summary>Toglie dalle sezioni <c>minima</c> i blocchi placeholder vuoti (né testo né JSON) creati quando
    /// la sezione era derivata (doc 13 §3b): ora è editoriale, e un blocco tabella vuoto le darebbe un editor di
    /// tabella che nessuno ha chiesto. Ritorna il numero di blocchi rimossi.</summary>
    Task<int> ClearMinimaPlaceholderBlocksAsync(CancellationToken ct = default);

    /// <summary>
    /// Porta le vLOA esistenti sulle chiavi del catalogo (doc 13 §3c): le due sotto-sezioni dei coordinamenti
    /// smettono di ripetere la chiave del padre e prendono <c>coordination:out</c>/<c>coordination:in</c> secondo
    /// l'ordine (la prima è Home→vicino, come le semina il registro), e la sezione «Purpose» — che nasceva con una
    /// chiave libera perché il catalogo non la conosceva — prende <c>purpose</c>. Toglie anche i blocchi delle due
    /// direzioni: il corpo lo produce la pagina, quindi erano testo scritto nel DB e invisibile in ogni vista.
    /// Ritorna il numero di sezioni riconciliate.
    /// </summary>
    Task<int> ReconcileVloaSectionKeysAsync(CancellationToken ct = default);

    /// <summary>
    /// Aggiunge alle vIPI APP e alle vLOA esistenti le sezioni FISSE del catalogo che non hanno (doc 13 §3d),
    /// nella posizione che il catalogo prevede. Serve a rendere uniforme un comportamento che era di una famiglia
    /// sola: la vIPI ACC le sezioni mancanti se le inventa a view-time (<c>AccDocumentAssembler</c>), APP e vLOA no —
    /// quindi una chiave aggiunta al catalogo compariva subito su tutte le ACC e mai sugli altri documenti già
    /// creati. Tocca la versione di lavoro più recente; è idempotente. Ritorna il numero di sezioni aggiunte.
    /// </summary>
    Task<int> AddMissingCatalogSectionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Scrive su ogni aeroporto il documento che lo descrive (<c>Airport.DocumentId</c>), leggendolo dove viveva
    /// prima: sui suoi settori d'aeroporto. Ritorna quanti aeroporti sono stati collegati.
    ///
    /// <para>Serve perché dal 25 agosto 2026 la vIPI d'aeroporto è legata all'AEROPORTO e non più a un suo
    /// settore. Senza questo passo, i documenti già scritti resterebbero raggiungibili solo per la vecchia
    /// strada, e la nuova li vedrebbe come inesistenti — cioè l'editor ne creerebbe di nuovi accanto a quelli
    /// buoni.</para>
    ///
    /// <para>⚠️ Sta qui e non in una migrazione EF per la ragione di sempre in questo file: le migrazioni del
    /// repo sono SQLite-flavored e il deploy hostato crea lo schema col <c>PostgresSchemaReconciler</c>.</para>
    /// </summary>
    Task<int> LinkAirportDocumentsAsync(CancellationToken ct = default);
}
