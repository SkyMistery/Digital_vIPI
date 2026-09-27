# Filone sito vIPI — stato

> Scrive **solo** la chat Sito (`/sito`: cartella `vipi-sito`, ramo `sito/lavori`; fino al 25 settembre la cartella
> era `vIPI-sito`). Regole: [`come-si-lavora-in-parallelo.md`](come-si-lavora-in-parallelo.md). Numerazione del
> filone: **S1, S2…** (le voci §A in `docs/lavori-aperti.md` le scrive il Master alla consegna).

## Dove siamo — 23 settembre 2026

- ✅ **S1** editor APP unito, «sezioni comuni» non ricarica più la pagina: fuso e **online in 1.43.0**
  (`docs/lavori-aperti.md` §S1, §A118). Al prossimo scarico di diagnostica: che non tornino gli
  `ObjectDisposedException` di `UnionPanel` su `/services/vsop/<icao>/apps/editor`.
- ✅ **S2** dopo «Hide» (sezioni in comune) i MEMBRI si ricaricano — fuso, **online in 1.43.1**.
  - **Difetto**: `UnioneCambiata` delle tre pagine ospite (APP, aeroporto, vSOP MIL) ricaricava solo il
    proprio documento; le sezioni nascoste nei membri restavano a schermo fino al ricarico della pagina.
  - **Perché non era banale**: `UnionMembersEditor` teneva una lista in cui i membri si aggiungevano e basta —
    un membro tolto o rimontato restava dentro con lo scope chiuso, e ricaricarli tutti = `ObjectDisposedException`.
  - **Rimedio**: `RegistroMembri` (nuovo, `Components/Doc/`) tiene gli editor **per documento**: un editor nuovo
    dello stesso documento sostituisce il vecchio, `OnParametersSet` toglie chi non è più membro e riordina
    secondo l'unione (prima l'indice seguiva l'ordine di registrazione, non «Sposta»). `@key="m.DocumentId"`
    sulle sezioni dei membri. L'ospite chiama `_membri.RicaricaAsync(<id dei membri appena riletti>)`: chi è
    appena uscito non viene toccato.
  - **Test**: `RegistroMembriTests` (9: registro + guardie sul sorgente delle tre pagine e del `@key`).
    `Vipi.Ui.Tests` 1659 → **1668** su net8 e net10.
  - **Prova dal vivo** (copia del DB di sviluppo, unione vIPI LIBV + vSOP MIL LIBV, editor aeroporto): «Hide» su
    «Piste» nel solo MIL → il membro passa da 1 a 2 sezioni nascoste senza ricarico, zero errori nel log.
    **Controprova** con la riga spenta: il servizio nasconde «LVP» («one section changed») e il membro resta fermo.
- ✅ **S3** fra i punti dei trasferimenti mancavano le STAR (segnalato: «ERIKA» a LIRN non proponeva ERIKA 1A) —
  fuso, **online in 1.43.1**.
  - **Causa**: i suggerimenti vengono dal DB (tabella viva, rilette a ogni apertura del form), ma solo le righe in
    vigore OGGI. Le STAR il sito le legge dal 21 settembre (1.35.0): al primo import sono righe nuove e prendono il
    ciclo che il sectorfile dichiara — `2610.txt`, in vigore dal 1° ottobre — quindi tutte fuori nel 2609.
    Riprodotto su copia del DB con l'import acceso: 10 STAR di LIRN timbrate `2610`, le SID restano `2607`.
  - **Rimedio** (scelto dal committente fra tre): `ProcedureReferenceResolver.ElencoAsync` deriva la tabella al
    ciclo ENTRANTE (`IAiracService` facoltativo nel costruttore). Vale per i punti dei trasferimenti e per il
    selettore «Cita». ⚠️ **Codice in comune toccato**: `Vipi.Application` (solo questo file).
  - ⚠️ **Non toccato, di proposito**: fino al 1° ottobre le sezioni «STAR» dei documenti aeroporto restano vuote
    (stesso timbro 2610); si sistemano da sole col ciclo.
  - Test: `L_elenco_guarda_al_ciclo_ENTRANTE`. `Vipi.Application.Tests` 2903 → **2904**. Dal vivo: accordo
    LIBB_ES → LIRR_TS, sezione Arrivi LIRN, «ERIK» propone ERIKA 1A (24) ed ERIKA 1C (06).
- ✅ **S4** larghezza delle colonne delle tabelle editabile da chi scrive (richiesta del committente, 23-set) —
  fuso, **online in 1.44.0** (§A120), **nessuna migrazione**.
  - **Dato**: chiave nuova `widths` nel JSON della tabella generica (`{"columns":…,"widths":[25,null],"rows":…}`),
    una per colonna, percento 1–100, `null` = automatica. Si scrive solo se almeno una è fissata: le tabelle
    senza larghezze restano byte per byte quelle di prima. Letta/scritta in `TabellaGenerica.Larghezze/Scrivi`
    (⚠️ **codice in comune toccato**: `Vipi.Application`, solo questo file). La traduzione non la tocca (numeri).
  - **Editor** (tutti e due: `DocumentSectionsEditor` e `DocumentBlocksEditor`): campo «auto %» sotto il nome di
    ogni colonna. Ogni mutazione rilegge e riscrive le larghezze (senza, un clic su una cella le cancellava);
    togliere una colonna si porta via la sua. Avviso sotto la tabella quando la pagina non potrà rispettarle
    alla lettera (tutte fissate e somma ≠ 100, o somma > 100): nell'editor lo spazio avanzato lo prende la
    colonna dei tasti, e la tabella sembrerebbe giusta.
  - **Render**: `Blocks/ColonneTabella.razor` (un `<colgroup>` + classe `tab-larg`) per pagina, anteprima ed
    editor. `tab-larg` rimette `auto` alle colonne automatiche, che altrimenti prendevano il 26/38/18/18% cablato
    di `.cfg-table`. Nella tabella `unified` non si applica (la cella di gruppo è una colonna in più).
  - **Test**: `TabellaGenericaLarghezzeTests` (18) · `LarghezzeColonneTests` (9). Application 2904 → **2922**,
    Ui 1668 → **1677**, net8 e net10.
  - **Prova dal vivo** (copia del DB, editor aeroporto LIBV, «Procedure generali» + blocco Tabella): 25% sulla
    prima → 171/683 px nell'editor, 240/959 nella bozza; sopravvive al riavvio e alla modifica di una cella;
    colonna aggiunta a 30% e tolta la seconda → `[25,30]`; avviso a somma 55; svuotata → `widths:[25,null]` nel DB.
  - **Col mouse** (seconda richiesta, stesso giorno): maniglia `.col-grip` sul bordo destro di ogni intestazione
    nell'editor (`vipi-editor.js`, delegato sul documento, installato una volta). Trascinando si allarga il `<col>`
    dal vivo e il campo «%» segue; al rilascio si scrive il campo e gli si manda un `change` → si salva dalla
    STESSA strada del numero scritto a mano (nessuna chiamata .NET nuova). Doppio clic = automatica;
    `pointercancel` rimette com'era. Nell'editor il colgroup c'è sempre (`ColonneTabella Sempre`) e la tabella è
    sempre `tab-larg`: senza, le colonne automatiche prendevano il 26/38% e la colonna dei tasti 246px.
    Test: `LarghezzeColonneTests` +4 (bUnit `Sempre` + guardie sul sorgente: bUnit non esegue il JS).
    Ui 1677 → **1681**. Dal vivo: trascinamento VERO col mouse 25% → 50% (salvato nel DB), doppio clic →
    `widths` sparisce dal JSON; da automatica a 25% con anteprima dal vivo a metà gesto (35%); annullamento ok.
- ✅ **S5** avviso «procedura non trovata» nell'editor degli accordi (dalla coda `da-fare.md`; il manager diceva S4,
  ma S4 era già preso). **Carta scritta, codice ZERO**: [`2026-09-23-procedure-non-trovate-negli-accordi.md`](../feature/2026-09-23-procedure-non-trovate-negli-accordi.md).
  ✅ Decise dal committente il 23-set (carta §0): D1 ENTRANTE ma sparizione avvisata solo 3 giorni prima del cambio
  ciclo · D2 terzo tasto diagnostico sulla barra dei trasferimenti · D3 riga fissa sulle copie congelate.
  ✅ **Codice fatto**, fuso con S4 e **online in 1.44.0** (§A120).
  - `ProceduraNeiPunti.NonTrovate` (funzione pura, `GiorniDiAnticipo = 3`) + `ProceduraNonTrovata`, `NomiAlCambio`.
  - `IProcedureReferenceResolver.PerTabelleEntrantiAsync` (tabelle al ciclo entrante + data del cambio; la lettura
    resta a OGGI). `IAgreementService.ProcedureNonTrovateAsync`: i due cicli in fila, via breve senza procedure.
    ⚠️ **Codice in comune toccato**: `Vipi.Application` (questi tre file + `AgreementEditingService`).
  - `AdminTrasferimentiPage`: terzo tasto «⚠ Procedure non trovate (n)» dopo «Lacune», pannello con nome · accordo ·
    sezione · punti · stato · ↗, riga fissa sulle copie congelate; ricalcolo a ogni `LoadAsync` (cioè dopo ogni
    scrittura). Guida `#accordi` aggiornata (IT/EN).
  - Test: `ProcedureNonTrovateTests` (9) + 1 in `ProcedureReferenceResolverTests`. Application 2922 → **2932**.
  - Dal vivo (copia del DB, LIBB, clausola 4 partenze LIBD scritta «PISIP, BANAV 6W, ZOPPA 9Z»): tasto (1), solo
    ZOPPA 9Z (BANAV 6W è viva), ↗ → `accordo=1&sezione=1`; corretta la clausola DALL'EDITOR («Start editing», 💾)
    → tasto (0) spento. Log senza errori. Il ramo «sparisce col ciclo» solo nei test (in locale un ciclo entrante
    diverso non c'è).
- ✅ **S6** STAR nei trasferimenti: «autorizzato **alla STAR** X» invece di «via» (richiesta del committente, 23-set:
  la STAR la assegna l'APP; le SID restano «via», le autorizza la torre). Fuso, **online in 1.44.1** (§A121).
  - Nuovo segnaposto `{cleared}` nei quattro template «autorizzato» (IT/EN, uscente/entrante) + parole
    `ClearedVia` («via {points}») e `ClearedStar` («alla STAR {points}» / EN «via the {points} arrival», scelta del committente),
    sovrascrivibili dal file `content/coordination-sentence.json` (`CoordinationSentenceOptions`).
  - Solo negli ARRIVI (`CoordinationSentenceComposer.Cleared`): partenze, sorvoli e altri restano «via».
    Misti: «via MAREL o alla STAR TOPNO 3A»; più nomi → «via MAREL o ELB, o alla STAR PIS 1A o PIS 1B».
  - ⚠️ Un template del file SENZA `{cleared}` (la forma vecchia «via {point}») continua a dire «via» a tutto.
    Il file di produzione oggi non sovrascrive i template «autorizzato».
  - Caratterizzazione `real-coordination.approved.txt`: cambiano SOLO le 4 righe TOPNO 3A (arrivo LIBP, IT+EN).
  - Test: +4 in `ProceduraNeiPuntiTests` (2 riscritti). Application 2932 → **2936**. ⚠️ **Codice in comune**:
    `Vipi.Application` (template + composer), `Vipi.Hosting` (opzioni del file).
  - Dal vivo (copia del DB, editor trasferimenti LIBB, anteprima frase): IT «autorizzato via MAREL o alla STAR
    TOPNO 3A», partenza «autorizzato via PISIP o BANAV 6W»; EN «cleared via MAREL or via the TOPNO 3A arrival».
- ✅ **S4 bis** (online in 1.44.1, §A121; 23-set, committente: «la linea si vede solo passandoci sopra»): la maniglia `.col-grip` ha una linea
  SEMPRE visibile, 2px `--brand-ink` al 70%; piena e larga 4px sotto il mouse e mentre si trascina (solo quella
  presa, `:active`). `--brand-ink` e non `--ivao-lightblue`: sul tema scuro il blu al 60% misurava ~1,5:1 sullo
  sfondo dell'intestazione, `--brand-ink` ~3,3:1; sul chiaro è il blu IVAO scuro su bianco.
- ✅ **S7** la Ricerca «non trova niente» (dalla coda `da-fare.md`, 24-set). **La ricerca non era rotta: partiva solo
  coi tasti.** `SearchPage` aggiornava il testo a `input` (`@bind:event="oninput"`) ma cercava a `keyup` (`@onkeyup`).
  Tutto ciò che scrive senza tasti — incolla col mouse, completamento automatico, testo trascinato, e le prove via
  script col browser integrato (Edge automatico non parte) — lasciava la parola NUOVA col conteggio VECCHIO.
  - **Misurato in produzione** (da anonimo, sola lettura, 24-set): `?q=Brindisi` → 16; campo con `input`+`keyup` →
    LIRF 13, radar 19, Brindisi 16; barra in alto → PISIP 1; **solo `input`** → «0 results for Brindisi», e appena
    arriva un `keyup` → 16. In locale (copia del 15-set) idem: 4 per Brindisi. Indice, `AccCode`, testa della
    release in vigore: tutti a posto, non c'entravano.
  - ⚠️ Occhio nelle prove: in pagina ci sono **due** campi con lo stesso segnaposto, quello della barra in alto
    (`form.top-search`, GET verso `/search?q=`) e quello della pagina (`.wrap input`). Scrivere nel primo non
    tocca la pagina.
  - **Correzione**: la ricerca parte da `@bind:after` (il cambio del testo), con la stessa pausa di 200 ms e la
    stessa porta (`InFilaAsync`, un giro alla volta). Tolto `@onkeyup`.
  - **Test**: `RicercaUnaAllaVoltaTests.Il_testo_arrivato_senza_tasti_cerca_lo_stesso`, rosso sul codice di prima;
    quello delle ricerche sovrapposte ora scrive solo con `input`. Ui 1699 → **1700**.
  - **Dal vivo** (locale, dopo la correzione): solo `input` → «4 risultati per Brindisi», «5 risultati per radar».
- ✅ **S8** la verifica di consegna pretende un risultato (dalla coda `da-fare.md`, 24-set).
  - `pacchetto-verifica.js`: scrive **`LIRF`** (costante `TERMINE`, col perché; sovrascrivibile da fuori) e, dopo
    «la Ricerca risponde», un controllo nuovo **«la Ricerca trova LIRF fra i documenti pubblici»**: conta i
    `.res-row` che NON portano alla Guida (le voci della Guida escono da un catalogo in memoria e ci sarebbero anche
    a database vuoto). Zero documenti con la ricerca che ha risposto → «termine di prova da cambiare, oppure la ricerca
    non trova», non «sito rotto». Prima scriveva `LI`, che trova anche la Guida.
  - Selettori provati sulla pagina VERA di produzione (browser integrato, sola lettura): LIRF → 13 documenti, 0 Guida.
  - 🔎 **Trovato strada facendo**: la pagina riconosceva le voci della Guida dal testo «Guida ›», e in inglese la
    Guida scrive «Guide ›»: in inglese perdevano il libro e sembravano documenti. Ora `IsGuide` guarda l'indirizzo
    (`/services/vsop/guide`). Test `Una_voce_della_Guida_in_inglese_resta_una_voce_della_Guida`, rosso con la
    regola di prima. Ui 1700 → **1701**.
  - Runbook `docs/guide/preparare-un-pacchetto.md` §6, §6-bis, §7 e trappole: «la Ricerca TROVA», e la frase del
    foglio da copiare. ⚠️ **Per l'integratore** (`deploy/` è suo): `deploy/atc-ivao/LEGGIMI-AGGIORNARE-VIA-FTP.md`
    riga «scrivendo `LI` nel campo … deve cambiare» va portata alla frase nuova, e così il foglio del prossimo
    pacchetto.
  - ⚠️ `pacchetto-verifica.js` **non l'ho potuto lanciare**: Edge 153 in modalità automatica si chiude subito, con
    ogni variante (`headless` new/true/shell, profilo pulito, `pipe` → «Target closed»). Da riga di comando
    `msedge --headless=new --dump-dom` funziona, e nessun criterio di Edge vieta il controllo remoto. È l'ambiente,
    non il codice: sintassi controllata (`node --check`), selettori provati a mano.
- 🟢 **Stato 24-set: PRONTO DA FONDERE** — S7 (`Ricerca: parte dal cambio del testo`) e S8 (`Verifica di consegna`),
  CI verde su `sito/lavori` (corse 35973468894, 35974009415). Nessuna migrazione. Nel pacchetto: `Vipi.Ui.dll`
  (SearchPage). Resta all'integratore: fondere, la frase nuova in `deploy/atc-ivao/LEGGIMI-AGGIORNARE-VIA-FTP.md` e
  nel foglio del prossimo pacchetto, voce §A in `lavori-aperti.md`.
- ✅ **S9** revisione totale, terzo giro (chiesta dal committente il 26-set sera, chiusa il 27-set). Registro
  [`docs/history/audit-2026-09-26-revisione-totale-3.md`](../history/audit-2026-09-26-revisione-totale-3.md) con gli
  allegati in `docs/history/revisione-totale-3/`: **256 findings U-001…U-256, 0 S1, 18 S2**, 5 confutati.
  - Base su `e24557e` (1.46.5): build 0 avvisi, 14 874 test verdi, nessun pacchetto vulnerabile; `sql_mode` di
    produzione è strict (letto dalla Diagnostica).
  - Workflow a 17 dimensioni + verificatori; tre stop sul limite di sessione, completato a pezzi: 6 dimensioni
    senza verificatore (marcate NV, gli S2 riletti a mano).
  - Dal vivo su due copie del DB di produzione (MariaDB locale, uscite spente), produzione in sola lettura:
    riprodotti 9 S2 (backfill che ripubblica la bozza, schede che si sovrascrivono, riga incompleta, testo oltre
    32 KB perso, doppio clic che uccide Struttura e «Pubblica ora», vista rapida FL100 → «10000 ft», archivio API
    aperto). Design misurato a 375/768/1024 px.
  - ⚠️ **U-009 ha una scadenza**: il 1-ott 00:00Z la release programmata #187 di LIBV_APP (9-set) sostituisce la
    #423 in vigore. Gesto in produzione prima di quella data (lotto L0 del registro).
  - Nessun codice cambiato: le correzioni vanno per lotti (L0…L11), col via del committente.
- ✅ **S10** lotto **L1 «Pubblicazione e release»** della revisione 3 (via del committente il 27-set): U-009, U-006,
  U-017, U-013, U-014. Ogni correzione è partita da una prova rossa. **Nessuna migrazione.** ⚠️ **Codice in comune
  toccato**: `Vipi.Application` (`IReleaseService`/`ReleaseService` perdono il backfill, `IDocumentMaintenance`
  guadagna `RiallineaProfonditaAsync`).
  - **U-009** (programmata vecchia che torna al rollover): in `EfReleaseRepository.RecomputeStatuses` una
    programmata perde anche contro una release **più recente** (numero più alto) che entra in vigore non dopo di
    lei. «Una per ciclo» è il caso a data uguale. Annullare la release nuova rimette in piedi il piano. Lo stato
    Superseded non dipende dall'ora, quindi non invecchia. In produzione la #187 di LIBV_APP diventa Superseded
    al primo giro di `ReleaseSweepHostedService` (ricalcola gli stati di tutti i bersagli poco dopo l'avvio): ⚠️
    **solo se il pacchetto entra prima del 1-ott**, altrimenti resta il gesto a mano di L0. Il pannello dice
    PRIMA del gesto quali programmate saranno sostituite: `Rel_NowReplacesScheduled`, `Rel_CycleReplacesScheduled`.
  - **U-006** (backfill d'avvio che ripubblica la bozza): **tolto** del tutto. Chiamata d'avvio,
    `BackfillVipiReleases`, `BackfillMissingReleasesAsync` e il suo test. La migrazione A è finita a luglio, e
    ormai l'ingresso lo aprivano solo casi sbagliati: l'annullo dell'unica release, un documento con la sola
    programmata, uno scheletro di vLOA generato. Le manutenzioni d'avvio tornano **quattro**.
  - **U-017** (doppio clic su «Pubblica ora»): sentinella `if (_busy) return` in `Run`, `ForceShapes`, `SalvaLingua`
    e rete generale con «Errore imprevisto: …». `Releases` resta sul contesto del circuito, di proposito (la nota in
    cima al pannello: il publish si compone con `BeforePublishAsync` della pagina).
  - **U-013** («Validità e revisione» svuotata a ogni consegna): il passo 2 di `ReconcileCookedSections` salta le
    sezioni `HostAndBlocks` (`KeepsOwnBlocks`).
  - **U-014** (profondità rimasta indietro, «Crea bozza» che esplode): `CreateDraftAsync` copia seguendo l'albero e
    ricava `Depth` dal padre. Passata nuova `RiallineaProfonditaAsync`, dopo tutte quelle che spostano: sistema la
    riga 5720 di Perugia Approach e ogni spostamento futuro. Nel giro c'era anche un buco: la somma che decide il
    timbro non contava `traffico` (lo spostamento del VFR). Aggiunto.
  - **Test**: `Pubblica_ora_dopo_una_programmata_resta_in_vigore_al_rollover`,
    `Programmata_dopo_una_pubblica_ora_entra_al_suo_ciclo`, `I_blocchi_propri_di_Validita_e_revisione_restano`,
    `CreateDraft_Non_Si_Fida_Della_Colonna_Depth`, `La_passata_d_avvio_riallinea_la_profondita_all_albero`,
    `Il_doppio_clic_su_Pubblica_ora_…`, `Un_guasto_imprevisto_resta_un_messaggio_nel_pannello`, due
    sull'avviso delle sostituite. `StartupMaintenanceTests` vuole 4 passate e nessuna che pubblica. Tolto
    `Backfill_Creates_…`. Infrastructure 1622 → **1626**, Ui 1715 → **1719**, net8 e net10.
  - **Prova dal vivo** (copia del DB di sviluppo, SQLite, :5199, editor aeroporto LIBC): programmata al 2610 → il
    pannello dice che «Publish now» la sostituisce. Doppio clic (del browser e sincrono da JS) → circuito su,
    zero eccezioni, la 2610 è Superseded. ⚠️ Su SQLite i due clic escono come DUE pubblicazioni in fila, perché le
    chiamate «async» del provider non cedono il turno. La corsa vera (secondo clic con la prima pubblicazione in
    volo) la prova solo il bUnit: su MariaDB non l'ho rifatta.
  - Resta, non toccato: `DocumentBirth.Semina` e `AggiungiPlaceholderSeServe` seminano un blocco tabella vuoto
    anche nelle sezioni `HostAndBlocks` (la nota in fondo a U-013). Da guardare con L11.
  - ⚠️ `tools/conta-test.sh` non si può lanciare da una chat: il cancello globale ne legge il contenuto e lo
    rifiuta («Comando annidato troppe volte»). I due file di `tests/conteggi/` sono scritti a mano dal log della
    corsa intera. Il confronto lo rifà la CI.
- ✅ **S11** lotto **L2 «Scritture che si perdono o si sovrascrivono»** della revisione 3 (via del committente il
  27-set): U-016, U-051, U-109, U-010, U-011. Ogni correzione è partita da una prova rossa. **Nessuna migrazione.**
  ⚠️ **Codice in comune toccato**: `Vipi.Application` (`VloaDerivationService` prende `IDocumentLockGuard`).
  - **U-016** (tetto SignalR a 32 KB, testo oltre ~16 KB perso in silenzio): `MaximumReceiveMessageSize` a
    512 KB in `VipiStartup.AddHubOptions`, come il Lab. Test `TettoDelCircuitoTests` (E2E, legge l'opzione vera di
    `ComponentHub`). Riga aggiunta anche in `docs/guide/integration.md`: l'host di ivao.it deve metterla lui.
  - **U-051** (vLOA: nascondi AoR/frequenze e ordine delle frequenze scritti senza lock): `EnsureMineAsync` del
    documento in `ToggleAsync` e `SaveFrequencyOrderAsync`. L'editor mostra già l'`EditConflictException`.
  - **U-109** (intro di pagina salvata senza verificare `editor:page-intro:*`): `EfPageIntroStore.SalvaAsync`
    chiama `IResourceLockService.EnsureHeldAsync`, e `PageIntroZone` mostra il rifiuto senza perdere il testo.
  - **U-010** (una riga a metà ferma TUTTA la tabella, e «Fine modifica» buttava le correzioni valide): salvare
    solo le righe complete non si può (i service sostituiscono la tabella intera: saltare una riga esistente che si
    riscrive la cancellerebbe). Quindi `AirportSaveGate.Ferme` + `IMembroEditor.PercheResta`: «Fine modifica» non
    esce e dice quale tabella è ferma, nell'editor aeroporto, nel vSOP militare e nelle tre pagine ospite dei
    documenti uniti (chiede a tutti i membri PRIMA di mollare il primo lock). L'etichetta di riga e l'aiuto
    dicono il vero: finché c'è la riga a metà, la tabella non si salva.
  - **U-011** (prendere il lock non rileggeva: la prima scrittura riportava indietro il lavoro di un collega):
    evento nuovo `EditLockBar.Acquired`, solo sul gesto «Inizia modifica» e PRIMA di `LockChanged` (all'apertura
    la pagina sta ancora leggendo, e una seconda lettura sullo stesso DbContext lo farebbe saltare). Rileggono
    Trasferimenti (e chiude pannello e caselle aperte), Struttura (albero e ripieghi), ACC, Aeroporti, Confinanti.
  - **Non fatto** (resta per L3/L11): il lock di struttura è per UTENTE, quindi due schede dello stesso Admin sono
    entrambe «in modifica» e la rilettura alla presa non le protegge; niente token di versione sulle clausole.
  - **Test**: E2E 415 → **416**, Infrastructure 1626 → **1628**, Ui 1719 → **1733**. Le guardie sul sorgente
    (`RigaIncompletaNonSiPerdeTests`, `PresaDelLockRileggeTests`) sono rosse sul codice di prima. Il comportamento
    di `Acquired` (prima di `LockChanged`, mai all'apertura) è provato in bUnit.
  - **Prova dal vivo** (copia del DB di sviluppo, :5199). LIRA, Quote di transizione: riga nuova col solo QNH,
    FL90 → FL95 su un'altra → «Fine modifica» rifiuta e dice «Transition levels», FL95 NON è in archivio;
    completata la riga, FL95 e la riga nuova vanno in archivio insieme. Trasferimenti LIBB aperto in sola
    lettura, clausola 4 cambiata nel DB (140 → 370) → «Start editing» e la pagina mostra 370. Confinanti,
    «Add a pair manually»: un poligono da 41 KB arriva al server («1500 vertices»), circuito su. Zero `fail:` nel
    log. ⚠️ Un primo giro con eventi `change` sintetici da JS aveva «perso» FL90: era il mio evento, non il
    codice. Con la battitura vera il salvataggio c'è.
- ✅ **S12** lotto **L3 «Circuito che cade»** della revisione 3 (via del committente il 27-set): U-012, U-108.
  U-017 era già in L1 (S10). **U-237 non è codice**: è una domanda al committente (tetto ai circuiti anonimi con un
  `CircuitHandler`, o regola di rate limit su `/_blazor` in Cloudflare), e sta anche in L6. **Nessuna migrazione**,
  nessun codice comune: solo `Vipi.Ui`.
  - **U-012** (Struttura: un clic su un nodo mentre un'operazione è in volo faceva cadere il circuito): la pagina
    prende i servizi dal circuito, tutti sullo stesso `DbContext`. Fila di pagina rientrante (`InFilaAsync`, la
    stessa dei Trasferimenti) per apertura, `Guarded`, `Select`, ripieghi, proposte, rilettura alla presa del lock
    e dopo un'eliminazione. `Guarded` ha la sentinella PRIMA dell'await e dice se il gesto è andato: «Applica» dei
    ripieghi rilegge solo dopo un salvataggio vero, sennò butterebbe le righe non salvate. Un clic dato mentre si
    salva **aspetta il suo turno** e arriva, non si perde (le righe non sono state rese inerti: con la fila non
    serve, e il clic perso sarebbe stato un difetto nuovo).
  - **U-108** (pannello «Translation»: due clic ravvicinati su una riga, circuito giù): `ApriAsync` con la
    sentinella e il conto dei documenti toccati dalla porta del componente; dalla porta anche le riletture dopo
    «Traduci ora» e dopo un salvataggio; sentinella anche su quei due. Le scritture restano sul circuito (la regola
    in testa al file).
  - Resta, non toccato: `DeleteDialog` legge e scrive col SUO `IDeletionService` dal circuito, fuori dalla fila
    della pagina. È una finestra modale, quindi il clic sull'albero dietro non arriva; da riguardare se diventa
    non modale.
  - **Test**: Ui 1733 → **1737** (net8 e net10). `StrutturaUnaOperazionePerVoltaTests` (tre bUnit: doppio clic su
    un nodo, clic su un nodo con il trascinamento ancora in volo, due rilasci nello stesso istante) e
    `TranslationReviewPanelTests.Due_clic_ravvicinati_sulle_righe_ne_aprono_una`: un finto che conta le operazioni
    sovrapposte sul contesto. Tutti e quattro rossi sul codice di prima (2 insieme invece di 1). ⚠️ Il pannello si
    prova con due righe: dal vivo il secondo clic arriva sulla stessa riga perché il server tiene vivo il gestore
    finché il browser non conferma il disegno, bUnit lo smaltisce subito.
  - **Prova dal vivo** (copia del DB di sviluppo, :5199). Struttura: doppio clic vero su LIMM_WS2_CTR → selezionato,
    circuito su. Trascinato LIMP_APP sotto LIMM_ES5_CTR e subito due clic su LIMM_ES2_CTR → padre salvato, ES2
    selezionato. «Suggest» doppio, proposta accettata, «Apply» doppio → una riga, catena giusta. Editor LIBB,
    pannello Translation: doppio clic vero su una riga e due righe nello stesso tick → una sola aperta, col conto
    dei documenti. Controllo di chiusura del lotto: `doppio-clic.js` su 17 pagine staff (Struttura, ACC, Aeroporti,
    Confinanti, Sorgenti, Trasferimenti, Versioni, Da sistemare, Compiti, Fraseologia, Radioassistenze, Allegati,
    Spazi aerei, Audit, Diagnostica, Permessi, Chiavi API) → nessun circuito caduto. Zero `fail:` nel log. ⚠️ Su
    SQLite le chiamate «async» non cedono il turno: la corsa vera la provano i bUnit, su MariaDB non l'ho rifatta.
- ✅ **S13** **U-237** della revisione 3, tetto ai circuiti anonimi (scelta del committente il 27-set: «una soglia
  nel codice»). Solo `Vipi.Host`: nessuna migrazione, niente `deploy/`, niente codice comune.
  - `TettoDeiCircuitiAnonimi`, montato dopo `UseAuthentication`: conta le GET di trasporto su `/_blazor` ancora
    aperte (il WebSocket resta in volo per tutta la connessione) e oltre la soglia risponde 503 con `Retry-After`,
    anche alla negoziazione. Chi è entrato col VID non si conta e non si ferma. Le POST del long polling passano
    sempre. Soglia **200**, configurabile con `Circuiti:TettoAnonimi` (zero = nessun tetto). Una riga di avviso al
    minuto nel log, non una per rifiuto.
  - **Un tetto solo, globale, niente tetto per IP**: dietro Cloudflare e nginx l'IP o lo sceglie il chiamante o è
    quello del nodo di Cloudflare (stessa ragione del tetto complessivo del bridge Aurora). I circuiti staccati li
    limita già `DisconnectedCircuitMaxRetained` (25).
  - Chi resta fuori vede la pagina disegnata dal server, senza interattività: `vipi-riconnessione.js` scrive
    l'avvio fallito in console e non ricarica, quindi niente giro di ricariche che moltiplica il carico.
  - **Test**: E2E 416 → **422** (`TettoDeiCircuitiAnonimiTests`: cinque sul middleware con trasporti tenuti aperti,
    uno sul sito vero che riempie la sala e bussa a `/_blazor/negotiate`). Rossi col corpo spento (5 su 6: il
    «tetto a zero» è verde per costruzione) e col solo montaggio spento (il test sul sito). Hosting 68 e Assets 63
    invariati.
  - **Prova dal vivo** (:5199, `Circuiti__TettoAnonimi=1`): prima scheda interattiva; seconda disegnata ma senza
    circuito (503 alla negoziazione), nessuna ricarica da sola in 8 s, modale di riconnessione spenta; chiusa la
    prima, la seconda ricaricata si collega. Nel log la riga «Tetto dei circuiti anonimi raggiunto (1)», zero `fail:`.
- ✅ **S14** lotto **L4 «Procedure e sectorfile»**, fette A e B (via del committente il 27-set): U-004, U-005, U-032,
  U-033. Solo `Vipi.Infrastructure`: **nessuna migrazione** (la registro la prevedeva per U-005: non serve), niente
  codice comune.
  - **U-005** (la chiave conteneva il punto RISOLTO: un alias nuovo o un catalogo cambiato staccavano la riga dal
    suo passato, che rinasceva senza priorità, forzatura, WTC, IC, «nascosta» e col ciclo nuovo):
    `AuroraSectorfileParser.ChiaveStabile` usa il prefisso GREZZO del codice. E il riaggancio non si fida della
    chiave salvata: la **ricalcola** dai dati delle righe vecchie (nome, transition, pista), quindi le chiavi scritte
    nel formato di prima si riscrivono da sole al primo reimport.
  - **U-004** (coppie di procedure diverse con la stessa chiave: la seconda ereditava le decisioni della prima e si
    ritimbrava a ogni giro): `EfAirportRepository.Riaggancia`, una riga vecchia per una nuova, in tre passi: nome
    esatto, radice del nome (cifre come `?`: `XIB?A-OKU?R` non è `XIB?A-OKU?A`), chiave. ⚠️ Cambia di proposito la
    regola «first-wins» che un test teneva ferma: ora ogni riga tiene le sue decisioni (test riscritto).
  - **U-032** (il «Reimporta» usava il ciclo dichiarato rimasto in cache dal giro prima): la risposta della sorgente
    in `SectorfileCache` vale 5 minuti (un giro intero, non un tasto premuto ore dopo).
  - **U-033** (indice `ITALY.isc` non-2xx: catalogo ridotto a 3 file su 8 tenuto in cache in silenzio): il ripiego,
    e un catalogo vuoto, si consegnano ma non si tengono; il chiamante dopo riprova l'indice.
  - **Test**: Infrastructure 1628 → **1633** (net8 e net10). Rossi sul codice di prima: chiave uguale con e senza
    alias; decisioni per riga con chiave condivisa; nessun ritimbro nella coppia XIB5A-OKU5R/OKU6A; decisioni
    conservate col punto risolto in un altro modo; ciclo riletto dopo 6 minuti; catalogo completo dopo un 503.
  - **Prova dal vivo** (copia del DB di sviluppo, import vero da GitHub, ciclo dichiarato 2610, catalogo 3744 punti
    da 8 file): 59 scali, 2354 righe (1503 SID + 851 STAR). Arricchimenti tutti conservati (WTC 69, IC 43, priorità 2,
    nascoste 3, forzate 83, come prima). ROBO5H (LIBG) e XIB5A-OKU6A (LIRF) restano al 2608 e non passano al 2610.
    Chiavi riscritte col prefisso grezzo (`LIBG|ROBO|H||17`). Secondo giro: le 2354 righe identiche al primo.
- ✅ **S15** lotto L4, fetta C: **U-031**, l'alias dei fix vale per lo scalo da cui nasce. **Migrazione sì**
  (`AliasPerScalo`, SQLite e MySQL: `DropIndex` + `AddColumn` + `CreateIndex`, niente `DropTable`). **Codice comune
  toccato**: `Vipi.Application` (`ISidFixAliasRepository`: `GetMapAsync(icao)`, `UpsertAsync(icao, …)`,
  `SidFixAliasRow.Icao`) e `Vipi.Domain` (`SidFixAlias.Icao`).
  - Era globale: la radice risolta in uno scalo riscriveva, senza «da verificare», il punto delle procedure di un
    altro (LUMA = LUMAR a LIBD, LUMAV a LIPE), e lo creava qualunque Editor. Ora colonna `Icao`, indice unico
    (`Icao`, `Prefix`); l'import di uno scalo legge i suoi alias più quelli senza scalo, e a parità di prefisso vince
    il suo. Gli alias vecchi (senza scalo) restano validi per tutti: scelta mia, perché sono stati scritti così e
    toglierli è della pagina Sorgenti, che ora mostra la colonna «Scalo» («tutti» per i vecchi).
  - L'editor aeroporto e il vSOP militare passano lo scalo; il suggerimento della casella «alias» dice la portata
    («per gli import futuri di questo scalo (gli altri scali non lo vedono)»), e anche l'aiuto di Sorgenti.
  - Postgres (nessuna migrazione): `PostgresSchemaReconciler.IndiciRitirati` toglie `IX_SidFixAliases_Prefix`,
    che altrimenti rifiuterebbe lo stesso prefisso per due scali.
  - ⚠️ Il `Down` della migrazione ricrea l'unico su `Prefix`: fallisce se nel frattempo due scali hanno lo stesso
    prefisso. Accettato: il dietrofront in produzione non si fa con `Down`.
  - **Test**: Infrastructure 1633 → **1636** (`AliasPerScaloTests`, rossi col filtro per scalo spento).
  - **Prova dal vivo** (copia del DB, :5199): migrazione applicata all'avvio; editor LICG, DOBI7C «fix da
    verificare» → battuto DOBIX, spuntato «alias», «Fine modifica» → in archivio l'alias `DOBI → DOBIX` con scalo
    `LICG`, la riga risolta; Sorgenti mostra la riga con la colonna Airport. Zero `fail:`.
  - ⚠️ La CI di S15 è stata rossa per un **test mio di S12**, intermittente: `Due_clic_ravvicinati_sulle_righe_…`
    (net10) cercava il tasto e poi cliccava, e in mezzo un render da un altro thread cambiava l'albero
    («no event handler with ID»). Ricerca e clic ora stanno dentro `cut.InvokeAsync`, anche nei tre test della
    Struttura, che avevano la stessa forma. Riverificati rossi sul codice di prima di L3. Correzione in S16.
- ✅ **S16** lotto L4, fetta D: **U-003**, fra il changelog del ciclo nuovo e la sua entrata in vigore una procedura
  rivista non sparisce più. **Migrazione sì** (`ProcedureSostituite`, SQLite e MySQL: una `AddColumn`). **Codice
  comune toccato**: `Vipi.Domain` (`AirportProcedure.SupersededFromCycle`) e `Vipi.Application`
  (`SidRow.SupersededFromCycle`, `IsSuperseded`, `IsPublicAt`).
  - Prima: il reimport cancellava tutte le importate, la revisione nuova prendeva il ciclo dichiarato (spesso il
    prossimo) e `IsPublicAt` la nascondeva fino ad allora. In mezzo, niente (LIMF, TOP1B, 25 settembre). Ora la
    versione in vigore resta in archivio **sostituita dal** ciclo dichiarato, con tutte le sue decisioni; ognuna delle
    due si vede nel suo tratto. Vale anche per le righe che la sorgente non manda più. Regole
    (`EfAirportRepository.ConservaVersioneVecchia`): si tiene se era entrata PRIMA del ciclo dichiarato (entrata nello
    stesso ciclo è una correzione dentro il ciclo, e si toglie come prima); una già sostituita si toglie quando la
    sorgente dichiara un ciclo successivo. Una riga che la sorgente rimanda si riprende la sua versione sostituita,
    con le decisioni (è anche l'altra metà di U-005: la TOP1B LAG2L malformata per un giro).
  - ⚠️ **La forzatura non passa più** a una revisione nuova quando la vecchia resta: dal vivo, a LIRN, ALAX7G forzata
    usciva insieme ad ALAX6G ancora in vigore (due SID dello stesso punto e pista). Passa ancora nella correzione
    dentro il ciclo. Due test vecchi dicevano «la forzatura segue la revisione»: adeguati, e spiegato perché.
  - Gli editor (aeroporto e vSOP militare) non mostrano le versioni sostituite: valgono solo sulla pagina pubblica.
  - **Test**: Infrastructure 1636 → **1642**. Rossi con la conservazione spenta: versione in vigore fino al ciclo
    nuovo (anche dalla scheda, come la prova scritta nella registro), riga tolta dalla sorgente, sostituite scadute,
    riga che torna; più la forzatura nella correzione dentro il ciclo.
  - **Prova dal vivo** (copia del DB, oggi ciclo 2609, sorgente che dichiara 2610): simulate a LIRF e LIRN una
    revisione vecchia (XIB4A-OKU5R, ALAX6G) e una riga sparita dalla sorgente (ZZZZ1A). Dopo l'import: le due
    vecchie «sostituite dal 2610», le nuove dal 2610, le altre 2354 righe identiche. Editor LIRF: solo XIB5A-OKU5R,
    col WTC ereditato, «dal 2610». Pagina pubblica LIRN, pista 06: ALAX6G e ZZZZ1A sì, ALAX7G no. Zero `fail:`.
- ✅ **S17** lotto **L5 «Documenti uniti»** della revisione 3 (via del committente il 27-set): U-007, U-008, U-052.
  **Nessuna migrazione**, niente `deploy/`. **Codice comune toccato**: `Vipi.Application` (`SezioniComuni`,
  `EditingService`/`IEditingService.RimostraPrimaDiSeparareAsync`, `DocumentUnionService`: `SciogliAsync` e
  `RimuoviMembroAsync` tornano `Task<int>`, costruttore con `IEditingService` facoltativo).
  - **U-007** (la scheda «in comune» nascondeva nel vSOP sottoalberi che il civile non ha: procedure VFR/IFR,
    soglie, carte militari): `SezioniComuni.Di` riceve il profilo di ogni documento; si propongono spuntate solo le
    sezioni di DATI (`Host` nel catalogo e non nate nascoste). Le `HostAndBlocks` e quelle scritte a mano restano in
    elenco, non spuntate. Una sezione si nasconde solo se tutto quel che ha sotto c'è anche in un documento che
    resta; altrimenti resta visibile (e si rimostra, se la scheda di prima l'aveva nascosta: LIRP), e si nascondono
    le sole figlie comuni. La scheda dice accanto alla voce «resta: ha sotto contenuti solo suoi» o «+N
    sottosezioni con lei».
  - Trovati dal vivo su LIBV e corretti nello stesso lavoro: (1) la scheda si apriva con **tutti e due** i documenti
    spuntati, perché le STAR nate nascoste in entrambi contavano come «scheda già usata»: `DoveNascondere` ora
    guarda solo l'impronta (nascosta qui, visibile là); (2) le STAR erano proposte spuntate, e «tenerle» nel civile le
    MOSTRAVA: una sezione nata nascosta non è più proposta.
  - **U-008** (sciogliere lasciava nascoste le sezioni comuni: LIRS e LIRL con tutte le radici nascoste, e il
    prompt diceva «non si perde niente»): PRIMA di sciogliere, o quando esce uno della coppia vIPI/vSOP, si
    rimettono visibili nelle bozze le sezioni nascoste in un documento e visibili nell'altro
    (`SezioniComuni.DaRimostrare`). **Senza colonna nuova**, scelta mia: è l'impronta che lascia la scheda, vale anche
    per le unioni fatte prima di oggi, e lascia stare le STAR nate nascoste e ciò che si è scelto di nascondere
    dappertutto. Stessa porta di autorizzazione e lock della scheda: se manca un lock, l'unione resta in piedi.
    Prompt di scioglimento e di uscita riscritti; a schermo il conto e «nelle pagine pubbliche tornano con la
    prossima pubblicazione».
  - **U-052** (un membro col lock di un collega riprovato a ogni render: prese, ricarichi, ridisegni in giro
    continuo; non riprodotto dal vivo nemmeno dalla registro): `LockNegatiDeiMembri` ricorda i negati, che non si
    riprovano fino a un gesto (Modifica, ricarico, un membro che entra o esce); l'ospite si avvisa solo se il
    rifiuto è nuovo.
  - **Test**: Application 2978 → **2986**, Ui 1737 → **1741** (net8 e net10). Rossi con le regole spente: il piano
    dal catalogo vero (niente del vSOP sparisce se il civile non ce l'ha), la sezione con figlie solo sue, la
    rimostrata di LIRP, le proposte solo di dati (e non le STAR), `DoveNascondere` con le STAR, `DaRimostrare`,
    l'ordine nel servizio (si rimostra PRIMA di sciogliere, e l'uscita di un APP non tocca niente); il presidio su
    `UnionMembersEditor` è rosso sul componente di prima.
  - **Prova dal vivo** (copia del DB, unione 3 = vSOP e vIPI di LIBV): la scheda si apre con la sola vIPI spuntata;
    STAR, frequenze, piste e le sezioni scritte a mano non spuntate; «Piste» dice «+1 sottosezioni» dal civile e
    «resta» dal vSOP. «Hide» dal vSOP: 5 sezioni (METAR, quote, SID, regole piste, LVP), piste e STAR non toccate.
    «Dissolve»: prompt nuovo, a schermo «5 sections … visible again in the drafts», in archivio le 5 di nuovo
    visibili e le STAR nascoste in tutti e due. Zero `fail:`. U-052 non provato dal vivo.
- ✅ **S18** lotto **L6 «Superficie pubblica»** della revisione 3 (via del committente il 27-set): U-001, U-020,
  U-018 (U-237 era già in S13). Nessuna migrazione, niente codice comune, `wwwroot` sì (`vipi-aor.js`).
  - **U-001**, Profile Swapper anonimo che esauriva memoria e CPU. Scelta del committente: **la pagina resta
    pubblica**, niente login; bastano i tetti sul server. Tetti: 512 KB per file (era 4 MB), 10.000 righe per file
    contate sui byte prima di spezzare il testo (un file di soli «\r» sta in pochi KB e diventava una riga per
    carattere), **10 destinazioni in tutto** (erano 50 per selezione, senza tetto sul totale). Misura dei 26
    profili veri dei test: il più grande è 74 KB e 1.517 righe. `GetMultipleFiles` ora chiede `FileCount`: col
    numero fisso, un file in più faceva sollevare FUORI dal `try` e cadeva il circuito. `LineDiff` toglie testa e
    coda comuni e costruisce la tabella LCS solo se il mezzo sta in 250.000 celle (1 MB); oltre, il mezzo è un
    blocco sostituito, un diff giusto ma meno minimo. Il diff di una sezione per una destinazione si calcola una
    volta (prima a ogni render: una spunta, un tasto nel filtro).
  - **U-020**, XSS nel tooltip 2D dell'AoR: `vipi-aor.js` passava a `bindTooltip` il nome del volume, e Leaflet lo
    scrive con `innerHTML`. Ora passa da `esc`, come il 3D.
  - **U-018**, archivio `/vsop/api/v1/atc/sessions` aperto. Scelta del committente: **`Api:RichiediChiave` vale
    `true` nel codice** se la configurazione non dice niente. ⚠️ Dal pacchetto che porta S18 l'archivio in
    produzione risponde 401 a chi non ha chiave, anche senza toccare la configurazione. Non si ferma nessuno
    (committente, 27-set): nessuna chiave emessa, e il validatore dei tour gira ancora su Cloudflare col suo
    archiviatore, non legge il nostro.
  - **Test**: rossi sul codice di prima, poi verdi. `LineDiff_non_cresce_col_quadrato_delle_righe` (96 MB prima,
    tetto 10 MB), la ricostruzione delle due sezioni su tre taglie, i tre tetti di pagina in bUnit (12 file → 10 e
    il messaggio; 600 KB; 30.000 «\r»), il presidio sul testo di `vipi-aor.js` (ogni `bindTooltip`/`bindPopup`/
    `setContent` passa da `esc`), E2E `Senza_configurazione_l_archivio_vuole_la_chiave`. Ui 1741 → **1750** (net8 e net10), E2E 422 →
    **423**; suite intera verde.
  - **Prova dal vivo** (copia del DB): archivio anonimo **401**. Editor APP di LIBA con il volume «AMENDOLA CTR
    Z1» rinominato `<img src=x onerror=…>`: il tooltip mostra il testo, nessun `img`, niente eseguito;
    controprova sulla stessa mappa con `bindTooltip` senza `esc`, come prima, e l'`onerror` gira. Swapper: 12
    destinazioni → 10 e «At most 10 destination profiles…: 2 files were not loaded»; 600 KB → «exceeds the 512 KB
    limit»; 30.000 «\r» → «more than 10000 lines», e il file buono della stessa selezione entra; anteprima del diff
    giusta (`b=1` tolta, `b=2` aggiunta); circuito vivo per tutta la prova.
- ✅ **S19** lotto **L7 «Import IVAO»** della revisione 3 (via del committente il 27-set), fetta **A: i giri che
  timbravano verde senza aver letto** — U-002, U-024, U-128, U-129. Nessuna migrazione. **Codice comune sì**:
  `Vipi.Application` (`AirportDataImportUseCase`, `SourceMergeInputs`, `DeletionService`, `AccFacts.IsForeign`).
  Il filo che li lega: il timbro «riuscito» del giro è il penultimo giro che `DeletionService` legge per la D8. Un
  giro verde che non ha letto niente, due notti di fila, rende eliminabile ogni riga che la sorgente manda ancora.
  - **U-002**: gli ELENCHI (postazioni d'aeroporto, piste, subcenter di un ACC) passano da `IvaoHttp.GetElencoAsync`:
    404 = vuoto legittimo, ogni altro non-2xx solleva con lo status. Prima un 401/403/429/5xx era «nessuna
    postazione». I dettagli per voce restano best-effort. Il giro dei settori ha un `try` per scalo: gli altri si
    leggono, la proiezione si rifà, e il giro risulta fallito col nome degli scali mancanti (è il testo che Sorgenti
    mostra). Le pagine che chiamano gli import (editor aeroporto e militare) passano da `_shell.GuardedAsync`, che
    regge l'eccezione nuova; `AeroportiPage` e `AccAdminPage` prendono solo tre tipi: fetta D (U-029).
  - **U-024**: con «Settori» esclusa il giro non legge e **non timbra** (prima ritornava `true`).
  - **U-128**: il giro TA/piste legge l'anagrafica una volta sola; senza credenziali risale col suo tipo (il ramo
    «non configurata» del servizio, che era codice morto, ora scatta); un altro guasto lascia passare le piste e poi
    fa fallire il giro. Stesso `SourceMergeInputs.ReadAsync` del bottone, con l'anagrafica già letta.
  - **U-129**: a un ACC **estero** «Chiedi alla sorgente» non chiede niente e risponde «non si sa». Prima la sonda
    scorreva i center italiani e diceva «non c'è più». La D8 sugli esteri resta com'era (non li ritimbra il giro
    ACC ma l'import dei confinanti): non toccata, è una scelta da fare a parte se serve.
  - **Test** rossi sul codice di prima: 15 casi di status su tre elenchi + 404 e dettaglio mancante
    (`ElenchiSorgenteNonMutiTests`), il giro dei settori (`GiroSettoriNonRegalaVerdeTests`: esclusa, uno scalo rotto,
    senza credenziali), tre su `AirportDataImportTests`, due su `DeletionProbeTests`. Infrastructure 1642 →
    **1666**, Application 2986 → **2988**.
- ✅ **S20** lotto L7, fetta **B: whazzup e sessioni ATC** — U-025, U-026, U-131. Nessuna migrazione, niente codice
  comune.
  - **U-025**: il whazzup è pubblico e ora si chiede **senza token** (`IvaoHttp.SendGetPubblicoAsync`). Prima un
    segreto ruotato male o un token endpoint giù spegnevano vista live e statistiche, pur col whazzup che rispondeva.
  - **U-026**: una sessione ricomparsa con lo stesso id dopo più di 15 minuti era «nuova» e l'`Add` su una chiave
    esistente faceva cadere il `SaveChanges` di tutte, ogni minuto. `EfAtcSessionStore.ApplyAsync` carica anche le
    «nuove»: la riga trovata passa dal ramo che la riapre, col suo turno.
  - **U-131**: la fotografia porta `updatedAt` (data di generazione) invece dell'ora d'arrivo; una risposta senza
    `clients.atcs`/`clients.pilots` è un poll fallito, non «nessuno online»; il poller non ripubblica né registra una
    fotografia con data uguale o più vecchia della precedente (la cache scade da sé, T-034), salvo un salto
    indietro di oltre 10 minuti (orologio della sorgente riazzerato). **Verificato sul whazzup vero** il 27-set:
    `updatedAt` c'è, con **nove** cifre di frazione (`…15.107470159Z`), e il parser le regge su net8 e net10 (il
    test usa quel formato); `clients.atcs` e `clients.pilots` ci sono sempre; ritardo ~15 s.
  - Non fatto: la «cache negativa» del token proposta dal registro per U-025 (col whazzup senza token non serve
    più alla vista live; resta un POST fallito per giro sugli altri endpoint, che girano di notte).
  - **Test** rossi sul codice di prima: 7 in `WhazzupClientTests`, 2 in `FotografiaFermaTests` (poller), 1 in
    `AtcSessionStoreTests`. Infrastructure 1666 → **1676**.
- ✅ **S21** lotto L7, fette **C (import che perdono dati)** e **D (la pagina che cade)** — U-027, U-028, U-030, U-130,
  U-029. Nessuna migrazione. **Codice comune sì**: `Vipi.Application` (`SourceAirport.MilitaryPresenceKnown`,
  `IAccAdminRepository.SetSpecialAreasEnabledAsync` che rende le aree sparite, `ISpecialAreaImportUseCase.SpegniAccAsync`,
  `AccAdminService`, `AirportTrafficRollupUseCase`), `Vipi.Domain` (`AirportCategories.Normalize` e `Divergente`).
  - **U-030**: l'import dei confinanti non azzera più la frequenza di un subcenter estero il cui dettaglio non si è
    letto (gemello di T-007: stessa guardia di `EfAccAdminRepository`).
  - **U-027**: il consolidamento del traffico d'aeroporto ha un `try` per blocco. Uno scalo che IVAO non conosce
    (404) si salta per il resto del giro, con **una** chiamata sola, e si dice per nome nel registro. Non è un guasto.
    Ogni altro errore salva prima i blocchi letti, poi fa fallire il giro col nome dello scalo. ICAO nell'URL con
    `Uri.EscapeDataString`. Non fatto: le tre categorie statistiche fra le righe di Sorgenti (è una pagina, non il
    difetto).
  - **U-028**, scelta del committente il 27-set: **una categoria militare non scende più da sola a Civile**.
    `HasMilitaryPresence` segue la sorgente; la categoria no. «Military» falso con categoria militare = riga
    **divergente** (`AirportCategories.Divergente`). La pagina Aeroporti la mostra con «IVAO: senza presenza
    militare» e offre «Civile»: è il gesto che la chiude. Le militari restano sceglibili sulla riga divergente.
    Quando la presenza torna, la scelta è ancora lì. Un «military» **assente** dal JSON (`MilitaryPresenceKnown`
    falso) non tocca né presenza né categoria. La passata d'avvio non «ripara» più la divergenza. Rovesciati i tre
    test che presidiavano il declassamento automatico (dominio, sync, passata).
  - **U-130**: «Escludi aree» passa da `SpecialAreaImportUseCase.SpegniAccAsync`, lo stesso corpo dell'import:
    pota e apre un impatto AreaGone per ogni area che l'ACC non vede più.
  - **U-029**: pagina ACC e pagina Aeroporti, una porta sola per i gesti (`Gesto` / `Guarded`). La guardia `_busy`
    sta prima dell'await (niente doppio import sullo stesso DbContext) e c'è un `catch (Exception)` con messaggio e
    log: timeout IVAO, token rifiutato, JSON non valido e collisione col giro notturno non fanno più cadere il
    circuito. Non fatto: lo scope DI proprio e il semaforo col giro automatico (la collisione ora è un messaggio,
    non una caduta).
  - **Test**: `AccEsteroNasceSpentoTests` (U-030), due in `AirportTrafficRollupTests` (U-027), categorie su tre
    assiemi (U-028), `SpecialAreaImportTests` (U-130), presidio sul testo delle due pagine
    `GestiDegliImportNonCadonoTests` (U-029). Rossi sul codice di prima: U-027 e U-030 provati; U-028, U-130 e
    U-029 per costruzione (metodi o regole nuove; i test vecchi di U-028 passavano sulla regola opposta).
    Suite intera verde: Domain 152 → **156**, Infrastructure 1676 → **1682**, Ui 1750 → **1760**, Application
    2988, E2E 423.
  - **Prova dal vivo** (copia del DB, IVAO e token su 127.0.0.1:9 con credenziali finte). U-028: LIBA messa a
    «Solo militare» senza presenza; la passata d'avvio la lascia com'è. In pagina Aeroporti la riga mostra «IVAO: no
    military presence» e quattro voci con «Civile». Scelto «Civile» (niente vSOP, quindi nessuna conferma): il segno
    sparisce e l'archivio dice `Civil`. U-029: doppio clic su «Import from source» → **un** import solo (una riga
    `Pagina ACC: import da IVAO fallito` nel registro), dopo ~29 s a schermo «Unexpected error: No connection could
    be made… (127.0.0.1:9)», circuito vivo. ⚠️ Al primo avvio della prova l'opzione del token aveva il nome
    sbagliato: **una** richiesta di token con credenziali finte è arrivata al vero IVAO (400 «application doesn't
    exist»). Nessun dato scritto; la app è stata fermata e rilanciata subito col token locale.
- ✅ **S22** lotto **L8 «Dominio e vista rapida»** della revisione 3 (via del committente il 27-set): U-015, U-089,
  U-090, U-091, U-092, U-093/U-095, U-094. Nessuna migrazione. **Codice comune sì**: `Vipi.Application`
  (`ParsedMetar.CeilingUnknown`, `MetarParser.OraOsservazione`, `LvpValutatore`, `LvpValutazione.RvrSopraScala`,
  `AwosComposition.MetarVecchio`, `AwosView.MetarObservedUtc/MetarStale`, `AirportBackfillPlanner`,
  `AirportTrafficBackfillUseCase`). `wwwroot` sì (`vipi-awos.js`).
  - **U-015**: la vista rapida legge TA e fasce TL dalla stessa vista del documento pubblico
    (`IAirportViewDerivationService`, congelata o derivata). Prima le cercava nei blocchi di una sezione Host, che
    nello snapshot non ne ha: TA «N/A», TL «—», initial climb in piedi sopra la TA. Il TL «adesso» è la fascia del
    QNH (`AirportViewFormat.TlAdesso`), senza più ripiegare sulla prima riga della tabella.
  - **U-089**: `VV///` e gli strati con base `///` (`BKN///`, `OVC///`, `//////`) sono un soffitto **ignoto**
    (`ParsedMetar.CeilingUnknown`), non un cielo sgombro. Le LVP non si propongono da cancellare, e la riga delle
    nubi scrive «VV ///».
  - **U-090**: con tutti i gruppi RVR «P» la misura resta l'RVR, come limite inferiore («RVR above 2000 m» nel
    titolo). Prima si ricadeva sulla visibilità e si accendevano le LVP.
  - **U-091**, scelta del committente: **resta il vento medio**. Nessun cambio nel calcolo; scritto nel commento di
    `ExplainRules` e nella carta del vAWOS.
  - **U-092**: l'età del quadro si misura dall'**ora del METAR** (`ddhhmmZ` risolto in UTC dal server), non
    dall'ultima risposta. Oltre 90 minuti il server non propone né LVP né pista; il quadro va in «morto». Non fatto:
    il tetto d'età dentro il ripiego stantio del client NOAA (col controllo nel server non guida più niente).
  - **U-093/U-095**, scelta del committente: senza RVR, `P2000` solo con visibilità ≥ 1500 m o CAVOK. Sotto, `///`.
  - **U-094**: il riempimento del traffico d'aeroporto attribuisce **per istante**: la sessione chiede la sua
    finestra e tiene i movimenti avvenuti quando non c'era in frequenza nessuno più titolato. Una finestra coperta
    per intero da posizioni più titolate resta saltata senza chiamare la sorgente, come prima. Istante assente, o
    nessuno in frequenza: vale la regola della finestra, così nessun movimento si conta due volte. **Non fatto**: il
    recupero delle 198 sessioni già saltate. Serve rimettere `TrafficFilledUtc` a NULL in produzione, entro l'anno
    della sorgente: è un gesto sui dati veri, lo decide il committente.
  - **Test**: rossi sul codice di prima quelli di U-094 (GND a zero), U-093 (P2000) e i due U-089/U-090 in
    `LvpTests`; U-015 e U-092 sono funzioni nuove (`TlAdesso`, `OraOsservazione`, `MetarVecchio`). Suite intera
    verde: Application 2988 → **3008**, Infrastructure 1682 → **1683**, Ui 1760 → **1771**.
  - **Prova dal vivo** (copia del DB, METAR veri da NOAA). `/services/vsop/live/libr_twr`: TA «5000 ft», TL «FL60»
    col QNH 1021, initial climb «FL100» (prima «N/A», «—», «10000 ft»). vAWOS LIBD con
    `?test=…0400 FG VV/// 08/08…`: RVR `///` sulle due testate, nubi «VV ///», LVP accese. vAWOS LIBD vero: età
    «24m» (METAR 20:20Z letto alle 20:43Z). Con la risposta sostituita da un METAR di tre ore fa: «180m», stato
    «morto». Per la prova la scheda era nascosta, e il quadro non interroga il server a scheda nascosta (voluto):
    `document.hidden` forzato a falso solo per la prova. Zero `fail:` nel registro.
- ▶ Alla ripresa: `git merge main` (il ramo resta indietro dopo ogni fusione dell'integratore). Guardare `da-fare.md` e i lotti di S9.
- Conteggi del filone: di solito `tests/conteggi/Vipi.Ui.Tests.txt`.
