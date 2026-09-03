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
    /// Rinomina in <b>«MRVA»</b> le sezioni <c>minima</c> dei documenti in lavorazione che portano ancora il
    /// titolo vecchio.
    ///
    /// <para>⚠️ Il titolo di una sezione di catalogo sta <b>nel documento</b>, non nel catalogo: cambiare
    /// <c>SectionCatalog</c> vale per i documenti nuovi, e su quelli già scritti non cambia niente. Senza
    /// questo passo la stessa sezione si chiamerebbe in due modi a seconda di quando è nata.</para>
    ///
    /// <para>⚠️ Le <b>release già pubblicate non si toccano</b> (doc 13 §9): il titolo vecchio resta nel
    /// pubblico finché quel documento non viene ripubblicato. È la stessa regola di ogni altra correzione
    /// editoriale, e non è un'eccezione fatta qui.</para>
    /// </summary>
    Task<int> RenameMinimaSectionsAsync(CancellationToken ct = default);

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
    /// Aggiunge ai documenti esistenti le sezioni FISSE del catalogo che non hanno (doc 13 §3d), nella posizione
    /// che il catalogo prevede. Serve a rendere uniforme un comportamento che era di una famiglia sola: la vIPI
    /// ACC le sezioni mancanti se le inventa a view-time (<c>AccDocumentAssembler</c>), le altre no — quindi una
    /// chiave aggiunta al catalogo compariva subito su tutte le ACC e mai sugli altri documenti già creati.
    /// Tocca la versione di lavoro più recente; è idempotente. Ritorna il numero di sezioni aggiunte.
    ///
    /// <para>⚠️ Dal 3 settembre 2026 copre anche i <b>vSOP militari</b> e scende nelle <b>sotto-sezioni</b>. Erano
    /// due buchi dello stesso passo: il vSOP militare non era nell'elenco dei documenti da guardare, e il
    /// confronto si fermava alle sezioni di primo livello — cioè proprio dove il profilo militare non ha quasi
    /// niente, avendo ventisei sezioni dentro sei contenitori.</para>
    ///
    /// <para>⚠️ La presenza si misura sulla CHIAVE in tutta la versione, non dentro il singolo gruppo: una
    /// sezione che sta nel posto sbagliato non va duplicata in quello giusto — va spostata, ed è un altro passo
    /// (<see cref="ReparentMilParkingsAsync"/>). Le chiavi di un profilo sono uniche, e lo pretende un test.</para>
    /// </summary>
    Task<int> AddMissingCatalogSectionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Sposta la sezione <c>parkings</c> dei vSOP militari già scritti da «Procedure di terra» a ultima figlia di
    /// «Dati generali», dove il catalogo la mette dal 3 settembre 2026.
    ///
    /// <para>⚠️ <b>Senza questo passo i documenti vecchi non si sistemano né da soli né a mano</b>: il catalogo
    /// decide la struttura solo alla nascita (<c>DocumentBirth</c>), e il motore di riordino sposta soltanto fra
    /// FRATELLI — apposta, perché un riordino non diventi una riparentazione silenziosa. Chi scrive non ha nessun
    /// gesto per portare una sezione in un altro gruppo.</para>
    ///
    /// <para>⚠️ <b>Prudente</b>: tocca solo i documenti militari, solo la sezione con chiave <c>parkings</c>, e
    /// solo se il suo padre è ancora <c>groundprocedures</c>. Se qualcuno l'ha già portata altrove resta dov'è —
    /// spostare la scelta di un altro sarebbe peggio del difetto.</para>
    ///
    /// <para>⚠️ Il <b>contenuto viaggia con la sezione</b>: è la stessa riga, quindi blocchi, tabella fissa,
    /// «nascosta» e marcatura pilota/ATC restano attaccati. E il corpo si cerca per CHIAVE, ricorsivamente
    /// (<c>MilMemberLoader</c>), non per posizione.</para>
    ///
    /// <para>⚠️ Le <b>release già pubblicate non si toccano</b> (doc 13 §9): il pubblico continua a vedere i
    /// parcheggi dov'erano finché quel vSOP non viene ripubblicato.</para>
    /// </summary>
    /// <returns>Quanti documenti sono stati sistemati.</returns>
    Task<int> ReparentMilParkingsAsync(CancellationToken ct = default);

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

    /// <summary>
    /// Porta i documenti d'aeroporto già scritti sulle chiavi del catalogo (carta 2026-08-26 §3). Fino a quella
    /// carta il documento era una proiezione <b>cotta</b>: le sue sezioni si riconoscevano per TITOLO e nascevano
    /// con una chiave <c>custom:{guid}</c> nuova a ogni rigenerazione (<c>BlockSection.Airport</c> non ha una
    /// chiave di catalogo, quindi il builder ricadeva su <c>SectionKeys.NewCustom()</c>).
    /// <para>Tre cose, idempotenti, sulla versione di lavoro più recente:</para>
    /// <list type="number">
    ///   <item>assegna la chiave giusta per titolo — <c>runwayrules</c>, <c>transition</c>, <c>runways</c>
    ///     (<c>frequencies</c> e <c>sids</c> ce l'avevano già), riconoscendo sia i titoli inglesi correnti sia
    ///     quelli italiani legacy;</item>
    ///   <item><b>svuota i blocchi</b> di quelle sezioni: da qui in poi il corpo lo produce la pagina derivandolo
    ///     dalle tabelle del profilo, e un blocco rimasto sarebbe testo scritto nel DB e invisibile in ogni vista;</item>
    ///   <item>trasforma le sezioni <c>airportextra</c> in sezioni libere normali (<c>custom:{guid}</c>), una
    ///     chiave per sezione: erano tutte indistinguibili, quindi «nascondi» ne avrebbe nascosta una a caso.</item>
    /// </list>
    /// <para>⚠️ Le release già pubblicate NON si toccano: il pubblico legge <c>payload.Doc</c>, e uno snapshot
    /// vecchio porta ancora le sezioni cotte. Il viewer le rende come editoriali (chiave sconosciuta + blocchi
    /// dentro), quindi uno snapshot storico continua a mostrare le sue tabelle.</para>
    /// <para>Ritorna il numero di sezioni riconciliate.</para>
    /// </summary>
    Task<int> ReconcileAirportSectionKeysAsync(CancellationToken ct = default);

    /// <summary>
    /// Toglie dalle vLOA già scritte la riga «Effective from — AIRAC ####» che il seminatore piantava a mano
    /// (doc 14 §3b). Quel numero era il ciclo del GIORNO DELLA CREAZIONE e non si aggiornava mai, mentre la
    /// scheda sopra mostra quello della release mostrata: sull'archivio di sviluppo tutte e quattro dicevano
    /// «AIRAC 2607» e una era pubblicata al 2608.
    /// <para>
    /// ⚠️ Idempotente e <b>prudente</b>: tocca solo la riga la cui prima cella è esattamente «Effective from» e
    /// la cui seconda è «AIRAC » + quattro cifre, dentro un blocco tabella di una sezione <c>validity</c> di un
    /// documento vLOA. Un testo che l'editore ha riscritto non ha quella forma e resta dov'è — togliere la
    /// parola di qualcun altro sarebbe peggio del difetto.
    /// </para>
    /// <para>Se la tabella resta senza righe il blocco se ne va: una tabella a zero righe è un rettangolo vuoto.</para>
    /// </summary>
    /// <returns>Quante righe sono state tolte.</returns>
    Task<int> ClearVloaSeededAiracRowAsync(CancellationToken ct = default);

    /// <summary>
    /// Azzera <c>Document.CurrentVersionId</c> dove punta a una versione <b>non pubblicata</b>. Doc 14 §3i.
    /// <para>
    /// Quel campo vuol dire «la versione PUBBLICATA corrente»: lo scrive <c>PublishAsync</c> e l'eliminazione
    /// lo azzera. Due porte su quattro — l'aeroporto e la vLOA generata da «ACC confinanti» — lo puntavano
    /// invece alla bozza appena creata, quindi un documento mai pubblicato dichiarava di avere una versione
    /// pubblicata che non esisteva.
    /// </para>
    /// <para>
    /// ⚠️ Oggi non fa danno visibile: ogni lettore pubblico ha un secondo cancello più forte (release
    /// effettiva, e stato Published). È il PROSSIMO lettore il rischio — chi si fida del nome del campo e
    /// non mette il secondo cancello si porta a casa una bozza.
    /// </para>
    /// <para>
    /// ⚠️ Prudente: tocca solo il puntatore, mai le versioni. E non guarda lo stato del DOCUMENTO ma quello
    /// della VERSIONE puntata — un documento archiviato che ha davvero pubblicato qualcosa resta com'è.
    /// </para>
    /// </summary>
    /// <returns>Quanti puntatori sono stati azzerati.</returns>
    Task<int> ClearUnpublishedCurrentVersionAsync(CancellationToken ct = default);
}
