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
- ✅ **S23** lotto **L9 «Design su telefono e tablet»** della revisione 3 (via del committente il 27-set): U-106,
  U-107, U-251…U-255, e con loro gli NV della stessa carta (`docs/history/revisione-totale-3/design-responsive.md`)
  che la sonda ha confermato: U-085, U-086, U-087, U-088, U-201, U-202, U-203, U-205, U-206, U-211, U-212. Nessuna
  migrazione, niente codice comune. **`wwwroot` sì** (`vipi-theme.css`, `vipi-awos.css`, `vipi-aor3d.css`,
  `vipi-swapper.css`, `vipi-aor.js`, `vipi-mva.js`, `vipi-aor3d.js`, `vipi-tour.js`, `vipi-zoom.js`) e `App.razor`.
  - **La sonda** (`docs/history/revisione-totale-3/sonda-telefono.js`): telefono vero (`isMobile` + `hasTouch`),
    26 pagine a 360, 375, 390, 768 e 1024, stessa misura prima e dopo. Pagine pubbliche, somme su tutti gli assetti:
    **pagina più larga dello schermo 1625 px → 0**, **parole spezzate 671 → 67** (restano la prosa della guida e
    gli estratti di ricerca, dove `anywhere` serve), **bersagli sotto 24 px 2033 → 256**, campi sotto 16 px 43 → 11
    (tutti a 1024: la regola dei 16 px vale sotto i 900), fasce appiccicate oltre il 20% dello schermo 3 → 0, mappe
    che prendono il dito 10 → 0. Nella sonda di prima il «salta al contenuto» e i campi file sono tolti a mano, come
    in quella di dopo.
  - **Larghezze.** `minmax(min(Npx,100%),1fr)` su `.stats-cols` (U-107/U-086, +61 px a 375), `.choice-grid`
    (U-201), `#xl-cols`, `.red-top-grid`. Due pagine nuove, non nella carta: **vAWOS di uno scalo** a 375 ne misurava
    509 (colonne `1fr` = `minmax(auto,1fr)`, che non scendono sotto le tre celle QNH/temperatura/rugiada) e il
    **convertitore di coordinate** 587. Tutte e due `minmax(0,1fr)`, più `flex-wrap` sulla riga del file.
  - **U-106**: sotto i 900 px la tabella è a blocco e scorre, ma `overflow-wrap:anywhere` sulle celle ne azzerava il
    minimo: si stringeva e spezzava «LIBD_CS0_/APP», «109./95». Ora `break-word` su tutte le celle (il selettore
    pesa di più delle regole `anywhere` scritte per `table-layout:fixed`), e «5000 ft» su una riga. Viewer LIBD
    a 375: 106 → 0 parole spezzate; vSOP LIBV 59 → 1; vIPI LIBB 17 → 0. A 768 la tabella SID sta intera.
  - **U-251**: non riprodotto ai tagli standard (753, 768, 900, 1024, 1100, 1180, 1280: il pannello release sta
    nella colonna centrale). La causa è nel foglio: riga release `nowrap` e tasti `flex:none`. Rosso forzando il
    pannello a 181 px (tasti a 785 con bordo a 533), verde dopo: la riga va a capo solo quando non ci sta, e a
    larghezza normale resta di 36 px.
  - **Sovrapposizioni.** U-211: pastiglia AIRAC nel flusso sotto i 900 px, titolo a 30 px sotto i 760, «N online»
    sopra il codice ACC. U-205: `.hero .muted` bianco. U-206: `--accent` fuori da `.nav-card` → `--ivao-blue`
    (anelli di fuoco di due tasti, Profile Swapper). vAWOS: le finestrelle in basso sul telefono.
  - **U-087**: la testata della vista live non si appiccica sotto i 760 px (329 px su 812) né con l'altezza
    sotto i 520 px.
  - **U-088**: mappe AoR e MRVA con `dragging` spento sui dispositivi mobili (un dito scorre la pagina, due
    spostano e ingrandiscono); 3D con `touch-action:pan-y`, pizzico per avvicinare, alto al più il 60% dello schermo.
  - **U-085**: zoom anche nel menu «☰» (allo scaglione tb-4 la barra lo nasconde); la percentuale si aggiorna in
    tutti e due i posti.
  - **U-212**: 16 px ai campi di live, vista rapida, archivio, ricerca VID, convertitore, vAWOS.
  - **U-255**: con `pointer:coarse` bersagli di almeno 24 px senza spostare niente (padding e margine negativo, o
    un `::after` che sborda). Il tasto «Compatta» della live aveva lo stile in linea: ora è una classe.
  - **Lingua.** U-252/U-203: i testi del tour vengono dal resx, in `data-tour-testi` sul tag di `vipi-boot.js`.
    U-253/U-202: intestazioni della vista rapida dal resx («Punto»/«Salita iniz.» accanto a «Transition»/«Condition»
    in tutte e due le lingue), «Nessuna SID per la pista» dal resx, titolo dell'archivio «IVAO Italy», titolo
    dell'anteprima release. U-254: «Tutte le schermate» si genera dalle rotte dell'assembly (55 indirizzi, quelli
    con parametri non collegati) invece della lista a mano ferma alla mockup v2.
  - **Non fatto**: U-204 (intestazioni scritte a mano in inglese nelle tabelle pubbliche: lotto L11). Restano
    spezzate, per scelta, le parole della prosa (guida, estratti di ricerca: `anywhere` serve lì). Editor sul
    telefono: fuori perimetro (si usano da 1024 in su, carta del 22 agosto).
  - **Test**: `ScreensIndexTests` (2), rossi sulla pagina di prima e verdi dopo; il resto è CSS/JS, e la prova è la sonda.
    Suite intera: tutto verde (Ui 1771 → **1773**) tranne `TransientRetryHandlerTests.Un_errore_di_rete…` su
    net8, rosso di tempo a suite piena (12 s contro il timeout di 10 del client), verde tre volte da solo: fuori da
    L9, già così prima.
  - **Non provato**: il pizzico a due dita sul 3D (la logica è letta, non guidata con dita vere); il 3D a pagina
    intera tiene la sua altezza, il tetto del 60% vale per quello incassato nel documento
  - **Prova dal vivo** (copia del DB): foto a 360/375/768 di home vSOP, vAWOS LIRF, convertitore, viewer LIBD,
    vSOP LIBV, menu «☰» con lo zoom; tour in italiano e in inglese; vista rapida LIBD nelle due lingue
    («FIX · SID · Transizione · Quota iniziale · … · Condizione» e l'inglese).
- ✅ **S24** lotto **L10 «Allineamento dei documenti»** della revisione 3 (via del committente il 28-set): U-105,
  U-245, U-246, U-248, U-249 (U-009 e U-014 già chiusi nel codice da L1, S10). Nessuna migrazione di schema.
  **Codice comune sì**: `Vipi.Application` (`ReleaseService.Signature` e `Confronta`, `IDocumentMaintenance` con tre
  metodi nuovi, `ImportCategories.StarCiviliLive`). **Dati in produzione al primo avvio**: le STAR civili Frozen
  → Live (una volta sola) e le sezioni «sempre live» Frozen → Live.
  - **U-248** (le passate d'avvio toccavano solo l'ultima versione): ora curano l'ultima **e** la pubblicata
    corrente (`ConVersioniDaCurareAsync` in `EfDocumentMaintenance`, otto passate). Con una bozza aperta la
    pubblicata restava vecchia, e «Scarta bozza» faceva ripartire la bozza dalla struttura di prima (vSOP LIBA,
    STAR solo nella bozza). Il trasloco degli extra d'aeroporto resta solo sull'ultima: è un trasloco.
  - **U-245** (STAR Frozen accanto a SID Live): la regola «nasce Live» sta in un posto solo,
    `DocumentBirth.NasceLive(profilo)`, e la usa anche la manutenzione. Scelta del committente: **anche le 45
    esistenti** → `StarCiviliLiveAsync`, passata **una tantum** col registro `StarCiviliLive` (dopo, una STAR
    Frozen è la scelta di un Editor e nessuno gliela disfa).
  - **U-246**: la vLOA nasce con la validità Live, pubblico e nascosta del catalogo (`VloaStructureSeeder`); i
    blocchi della vIPI ACC seminano le figlie nella lingua del documento, con pubblico e nascosta
    (`SeminaFiglie`). Per i dati: `RiallineaSezioniSempreLiveAsync` riporta a Live meteo e validità rimaste
    Frozen (idempotente: per quelle sezioni l'editor non ha il toggle).
  - **U-249**, una regola sola: il titolo di una sezione di catalogo lo risolve **la chiave**, ovunque. Il viewer
    e l'editor lo facevano già (`TitoliDiCatalogo`); la firma di deriva (`ReleaseService.Signature`) ora
    riconosce le sezioni di catalogo per chiave, le libere e le chiavi ripetute fra sorelle per titolo, e accoppia
    le voci rimaste spaiate con lo stesso percorso di titoli (release vecchie con le sezioni «cotte»).
    L'etichetta a schermo resta il percorso dei titoli. Tolto il «passo 1-bis» della riconciliazione d'aeroporto,
    che riscriveva nel DB i titoli delle sole radici e apriva righe «da ripubblicare» invisibili (LIRL, #128).
  - **U-105**, scelta del committente: **lo sistema lui, a mano**, con «Sposta in…»: vIPI LIBB (blocco Brindisi
    CS0: le tre sezioni libere IFR dentro la IFR di catalogo) e Perugia Approach (una sola «Gestione del
    traffico»). Nel codice solo l'avviso: `TrafficoDaSistemareAManoAsync`, in sola lettura, scrive all'avvio una
    riga di avviso nel registro per ogni documento in quello stato.
  - **Test**: nuovi `AllineamentoDocumentiTests` (8), `NascitaDocumentoParitaTests` (+1, vIPI ACC in inglese),
    `ReleaseGenericFlowTests` (+3, deriva). Rossi sul comportamento di prima 6 su 7 di quelli che lo toccano (il
    settimo, la chiave diventata di catalogo con lo stesso titolo, è la rete della regola nuova e passa anche
    prima); le tre passate nuove non esistevano. `ReconcileAirportSectionsTests`: il titolo di «Frequencies» non
    si riscrive più. Suite intera verde:
    Infrastructure 1683 → **1695**, il resto invariato.
  - **Prova dal vivo**: copia del DB di sviluppo (del 15-set: senza STAR; la copia di produzione
    `vipi_rev3` dell'audit non è più su questa macchina). Primo avvio: 70 sezioni di catalogo aggiunte, fra cui 11
    STAR tutte **Live**; 25 «sempre live» riportate a Live; il registro `StarCiviliLive` scritto; le 12 validità
    Frozen rimaste stanno solo in versioni **archiviate**; nei 9 documenti con una bozza sopra la pubblicata, bozza e
    pubblicata hanno le stesse chiavi di catalogo. Secondo avvio: nessuna passata trova lavoro. Editor e viewer
    LIBD, editor vIPI LIBB e pagina versioni si aprono senza errori. L'avviso U-105 non scatta su questa copia (il
    caso c'è solo in produzione): lo provano i test.
- ✅ **S25** lotto **L11 «Il resto», fetta A — import SID e sectorfile** della revisione 3 (via del committente il
  28-set; L11 diviso in fette per area, A…I, tabella §4 del registro). U-034, U-035/U-064, U-036/U-040, U-038,
  U-039, U-111/U-161, U-132, U-133, U-134, U-171, U-173. **Resta fuori U-037** (aree TWR scritte una volta, MRVA e
  radioassistenze senza il cancello del ciclo AIRAC): chiede una colonna di provenienza e un ridisegno, si propone
  a parte. Nessuna migrazione. **Codice comune sì**: `Vipi.Application` (`IAirportRepository`,
  `AirportEditingService`, `ProcedureImporter`, `MinimaView`/`MinimaCharts` e i due fornitori Frozen,
  `NavaidImportOutcome`).
  - **U-034** (SID e transizione separate da uno SPAZIO: «SRN6A ARL2A» a LIML, «LAT1E PEM1T» a LIRL): il parser
    divideva solo sul «-», e il vSOP pubblico di LIRL stampava «LAT1E PEM» come FIX. `ParteSid` separa anche sullo
    spazio, ma solo se il primo pezzo ha la forma di un designatore: le partenze a vista di `lied.sid` («FRASCA
    DEP16») restano un nome solo. La chiave stabile si ricalcola sui due lati al riaggancio: niente migrazione.
  - **U-133** (parser `.tfl`): un commento a riga intera dentro un blocco chiudeva l'anello coi vertici visti fin
    lì, e una coppia «E…;N…» entrava invertita. Ora il commento si salta e la coppia si legge solo N/S poi E/W.
    Sui file veri il commento dentro il blocco **c'è** (lirrctr, limmfic, libb_es_ctr, lmmm, lyba): sonda sui 27
    file di `DYNAMIC_SEC` prima/dopo, ritrovano l'area LIEE_MIL_APP (183 vertici), LMMM_CTR/FSS, LYBA_CTR/WES_CTR,
    nessuna persa. Siccome i quattro laghi di `limmfic.tfl` non cadono più, lo stesso callsign in più blocchi
    tiene l'**area più grande** (vinceva l'ultimo blocco: LIMM_WS2_CTR prendeva uno spezzone del confine svizzero).
  - **U-035/U-064** (ogni giro cancellava e ricreava le importate, anche identiche: l'editor aperto scriveva su Id
    spariti sotto «Salvato»): `ReplaceImportedProceduresAsync` aggiorna **sul posto** la riga che continua, che
    tiene l'Id — anche la revisione nuova; la versione vecchia che vale ancora è la copia sostituita (U-003). Le
    scritture su una riga tolta dal reimport sollevano `EditConflictException` «ricarica» (prima tornavano mute;
    `SetImportedSidsHiddenAsync` restituiva un numero che nessuno leggeva).
  - **U-173**: `UpdateImportedSidAsync` filtra per scalo oltre che per Id. **U-111/U-161**: «Reimporta SID» chiede
    `EnsureNotOtherAsync`, la guardia del re-import completo che sta nello stesso gesto. **U-171**: l'alias del
    punto lo scriveva la pagina prima del lock; ora lo scrive il servizio dopo la riga (`aliasDalPrefisso`).
  - **U-134**: a contenuto invariato il timbro è il ciclo più vicino fra quello di prima e quello calcolato.
  - **U-038**: il giro delle procedure non timbra (solleva, `GatedImportLoop` scrive l'errore e ritenta fra
    un'ora) se fallisce almeno metà degli scali o non arriva nessuna procedura; con le SID escluse in Sorgenti non
    timbra e non chiama la sorgente. Uno scalo rotto da solo non ferma il timbro degli altri.
  - **U-039**: cache MRVA con un semaforo per carta e il guasto ricordato 2 minuti; a view-time la sezione delle
    minime arriva vuota con l'avviso «la carta non è arrivata» (distinto da «nessuna carta»); la cattura di una
    release invece rifiuta (`MinimaCharts.DaCongelare`).
  - **U-036/U-040**: il giro delle radioassistenze **stacca** le righe che la sorgente non manda più (restano coi
    loro valori, ma i campi tornano «a mano»: si correggono e si tolgono; se la sorgente le rimanda si riprendono) e
    le conta nell'esito, nella pagina e nel log. Il canale non si scrive a mano sulle righe della sorgente.
    **Dati in produzione**: al primo giro TRP|VHF|25X diventa modificabile; le tabelle alternati MIL di LICT che
    la citano vanno ripuntate a mano su TRP|VHF| (il committente).
  - **U-132**: pagina Radioassistenze, import, celle, aggiunta, eliminazione e caricamento in una fila sola; celle
    spente durante l'import; gli errori si dicono sulla riga o in testata.
  - **Test**: nuovi `GiroProcedureNonRegalaVerdeTests` (4), `MinimeSorgenteGiuTests` (Application 4, Ui 2); in più
    parser (+7), repository e servizio SID (+8), importatore (+1), cache (+2), anagrafica (+3), pagina
    Radioassistenze (+2). Ognuno rosso sul comportamento di prima (file vecchio al posto del nuovo, o ritocco
    mirato del solo comportamento per il repository). Suite intera verde, net8 e net10: Application 3008 →
    **3012**, Infrastructure 1695 → **1720**, Ui 1773 → **1777**, il resto invariato.
  - **Prova dal vivo** (copia del DB di sviluppo del 15-set): con il sectorfile irraggiungibile la vIPI LIBB si apre,
    le due sezioni MRVA mostrano l'avviso, nessun errore di circuito. Col sectorfile vero il giro procedure fa 59
    scali e 2354 righe senza errori: LIRL «LAT1E PEM1T» ha il punto LAT (prima «LAT1E PEM»), LIML SRN/MMP/TZO e
    LIMF TOP risolti; restano «da verificare» i prefissi ambigui veri (TOV, RUV, ABS, LIM, SIR). Radioassistenze: su
    148 righe della sorgente nessuna casella del canale; una TRP|VHF|25X simulata col timbro d'import viene staccata
    dal tasto «Importa dal sectorfile» («1 riga… tornata modificabile»), e da lì ha le caselle e il cestino.
- ✅ **S26** **U-037** della revisione 3 (fetta A di L11, lasciata fuori da S25; via del committente il 28-set): il
  sectorfile ha **una regola sola** — si segna la provenienza, si aggiorna quel che abbiamo scritto noi, e un cambio
  entra **dal ciclo AIRAC successivo** tenendo in vigore la versione di prima per le release del ciclo in corso.
  Era già così per le aree di settore (`SectorShapeFallbackService`, `ShapeAiracGate`); ora vale anche per torri,
  radioassistenze e carte MRVA. **Migrazione SÌ**: `SectorfileDifferito` (SQLite + MySQL; Postgres dal
  riconciliatore), solo aggiunte — cinque colonne su `Navaids` (valori in vigore, ciclo, forzatura), la tabella
  `MvaChartStates`. **Codice comune sì**: `Vipi.Domain` (`Navaid`, `MvaChartState`), `Vipi.Application`
  (`IAirportSectorRepository`, `TwrShapeRow`, `GithubTowerShapeService`, `NavaidRow.SourceAiracCycle`,
  `ShapeGateNoticeService`, `ISectorfileGateRepository`, `DeferredKind`).
  - **TWR** (niente migrazione: le colonne c'erano): l'area presa da `twrs.tfl` restava marcata come
    dell'anagrafica IVAO e non era più un bersaglio — 66 torri su 70, mai più aggiornate. Ora porta
    `ShapeSource.Sectorfile`; un'area ridisegnata entra dal ciclo successivo (in vigore quella di prima); le torri
    scritte prima si riconoscono per geometria e si segnano senza toccarle; una shape IVAO diversa dal file resta
    di IVAO. La promozione dei differiti è quella dei settori (`PromoteDueShapesAsync` copre già le TWR).
  - **Radioassistenze**: un cambio di frequenza o posizione su una riga che la sorgente mandava già entra dal ciclo
    successivo (`SourceAiracCycle`, valori `…InForce`); la cattura di una release (`GetManyAsync` dentro
    `ShapeReleaseContext`) congela i valori in vigore al suo ciclo; fuori dalla cattura valgono i correnti. Il giro
    chiude i differimenti maturati. La pagina Radioassistenze dice «valori nuovi dal ciclo X».
  - **MRVA**: `EfMvaChartStates` ricorda il **testo** di ogni file `.mva` (non la carta letta: il parser può
    cambiare, il file no). Il provider confronta il testo una volta per caricamento; dentro la cattura di una
    release dà la carta in vigore a quel ciclo. Un file che sparisce (404) non tocca il ricordo.
  - **L'avviso a chi pubblica e la forzatura** (chiesti dal committente il 28-set, dopo la prima stesura): il
    pannello release elencava solo le aree che la release porterebbe nella versione di prima. Ora elenca anche le
    radioassistenze che il documento **cita** (in una sua versione qualsiasi) e le carte MRVA dell'ente (vIPI ACC:
    l'enroute e quelle degli aeroporti della ACC; APP: quella del suo aeroporto), ognuna col suo tipo; «Pubblica
    comunque i dati nuovi» le forza tutte (`SourceForcePublished`, `MvaChartState.ForcePublished`). La forzatura
    si spegne da sé quando il ciclo arriva o la sorgente cambia di nuovo, come per le aree. Il percorso delle
    carte sta in un posto solo (`AuroraMvaProvider.PercorsoAcc/PercorsoAeroporto`). La migrazione, non ancora
    fusa, è stata rigenerata con dentro le due colonne invece di aggiungerne una seconda.
  - **Test**: `GithubTowerShapeServiceTests` (+3), `RadioassistenzeCicloAiracTests` (6), `MvaCicloAiracTests` (6),
    `SezioneRadioassistenzeTests` (+1), `ReleasePanelTests` (+1), pagina Radioassistenze (+1). Sul codice di prima
    quelli del dominio non compilano (costruttori e colonne nuove): il rosso è il comportamento che descrivono —
    provenienza mai scritta, valori e carte che entravano subito; il pannello release di prima non dice il tipo
    (rosso provato). Suite intera verde, net8 e net10: Infrastructure 1720 → **1736**, Ui 1777 → **1779**, il resto
    invariato.
  - **Prova dal vivo** (copia pulita del DB di sviluppo del 15-set, sectorfile vero, IVAO irraggiungibile): la
    migrazione si applica all'avvio; il giro dei settori d'aeroporto riconosce **66 torri su 68** come del
    sectorfile (le altre 2 hanno un'area diversa dal file e restano di IVAO; 16 cerchi di ripiego invariati), nessun
    differimento perché `twrs.tfl` non è cambiato. La vIPI LIBB si apre con le due carte, e le 7 carte lette
    finiscono ricordate senza differimento. Simulato sulla copia un valore vecchio (MNL 115.20, un testo diverso
    di `libd.mva`) e riavviato: il tasto «Importa dal sectorfile» apre il differimento di MNL al **2610** con
    115.20 in vigore, e la pagina scrive «valori nuovi dal ciclo 2610»; `libd.mva` passa al 2610 col testo di
    prima in vigore. Zero errori nel log. La cattura di una release al ciclo precedente la provano i test, non
    la prova a schermo.
  - **Prova dal vivo dell'avviso** (copia pulita, migrazione rigenerata): simulata una revisione di
    `ENRMVA/libb.mva`, l'editor della vIPI LIBB mostra nel pannello release «Dati del sectorfile non ancora in
    vigore — carta MRVA ENRMVA/libb.mva, in vigore dal ciclo 2610»; «Pubblica comunque i dati nuovi» scrive la
    forzatura (il ciclo resta), l'avviso sparisce, zero errori. La prima stesura diceva «chartMRVA …»: etichetta
    senza spazio e «MRVA» ripetuto, corretta dopo averla vista a schermo.
- ✅ **S27** lotto **L11 «Il resto», fetta B — lock, release e unioni** della revisione 3 (via del committente il
  28-set). U-053, U-054, U-055, U-056, U-057, U-058, U-139, U-141, U-142, U-143, U-144, U-145, U-146, U-147.
  **U-059** è U-051 e **U-140** è U-109, già chiusi in S11: niente da fare. Nessuna migrazione, `deploy/` no.
  **Codice comune sì**: `Vipi.Application` (`IDocumentAdminRepository.ResolveDocumentIdAsync`,
  `IEditingRepository.GetSectionCatalogPlaceAsync`, `EditingService`, `DocumentUnionService`, `AirportLockGuard`,
  `ReleaseService`, `IFrozenSectionReader`, `AnteprimaDiRelease` nuovo, `AccReleaseView`,
  `ResourceLockKeys.RichiedonoEditor`).
  - **U-053** (l'anteprima `?as=rel:N` mostrava le derivate di oggi): nuovo `AnteprimaDiRelease`, un contesto
    asincrono per chiamata che i loader aprono solo dopo che `GetPreviewAsync` ha autorizzato; dentro, il
    lettore delle congelate legge lo snapshot di **quella** release per quel bersaglio. Aeroporto, vSOP militare
    (anche radioassistenze e alternati, e ora al ciclo dell'anteprima), APP, vLOA e vIPI ACC. La vIPI ACC in
    anteprima non aveva i blocchi e derivava tutto live: ora `AccReleaseView` porta blocchi e chiave. E
    `ProgrammataAllineataAsync` confronta anche le derivate congelate (per identità di sezione, non per Id):
    una TORA corretta dopo aver programmato riapre la riga «da ripubblicare».
  - **U-054/U-139** (lingua dal pannello release): la guardia del lock risolve l'Id dalla chiave come il
    repository (prima usciva su Id nullo e il repository scriveva); la vLOA cerca l'ACC anche dalla chiave (prima
    «Documento inesistente», la lingua di una vLOA non si salvava mai).
  - **U-055**: unire, togliere, spostare e sciogliere pretendono che nessun documento dell'unione sia in mano a
    un altro (il proprio lock non ferma). Il pannello mostra già il conflitto.
  - **U-056**: `AirportLockGuard` rinnova il lock a ogni scrittura dello scalo, come la prosa.
  - **U-057/U-142**: ricerca e «Cosa è cambiato» senza il filtro `CurrentVersionId` (la vLOA 65 ne era fuori), e
    il cancello pubblico chiude anche lo scalo nascosto e l'APP disattivato, come le pagine.
  - **U-058**: la vLOA generata dalle ACC confinanti nasce in bozza (documento e versione).
  - **U-141**: elenco pubblico dei vSOP militari e ponte civile↔militare guardano documento e scalo nascosti.
  - **U-143**: «Scarta bozza» su un'unione salta il membro mai pubblicato (la sua bozza è l'unica versione) invece
    di fermare il gesto. **U-145**: «Pubblica versione» e «Scarta bozza» su un'unione in una transazione
    (`IUnitOfWork`), lock compresi.
  - **U-144**: la porta dei gesti della pagina Versioni ha la sentinella in testa e la rete generale; il
    `ReleasePanel` l'aveva già da U-017.
  - **U-146**: commenti, tasto «Sblocca comunque» e Guida dicono la regola vera (ogni Editor, e resta nel
    registro); `RichiedonoAdmin` → `RichiedonoEditor`.
  - **U-147**: `DeleteSectionAsync` rifiuta la sezione di catalogo del profilo (del documento, o del blocco ACC).
  - **Test**: `AnteprimaDiReleaseTests` (4), `MilitareNascostoTests` (3), unioni (+4), guardia admin (+2), lock
    dello scalo (+1), ricerca/novità (+3), vLOA in bozza (+1), editing (+3), programmata (+1), pagina Versioni (2).
    Tutti rossi sul codice di prima (sorgenti di `HEAD` rimessi al loro posto, tenuti solo il tipo nuovo e la firma
    del costruttore), tranne le guardie dei casi che non devono cambiare. Suite intera verde, net8 e net10:
    Application 3012 → **3016**, Infrastructure 1736 → **1754**, Ui 1779 → **1781**, il resto invariato.
  - **Prova dal vivo** (copia del DB di sviluppo del 15-set, TORA della 17 di LIBC portata a 1999 nella copia):
    l'anteprima della release 65 di LIBC mostra TORA **2000** (congelata), la bozza **1999**; l'anteprima della
    release 37 della vIPI ACC LIBB mostra le frequenze dei blocchi, che prima derivava live. Guida con la regola
    nuova; pagina Versioni con tre «Refresh» di fila senza cadute. Nel log solo IVAO irraggiungibile (voluto).
    Le altre voci (unioni, lock, ricerca, ponte militare, scarta/pubblica in transazione) le provano i test.
- ✅ **S28** lotto **L11 «Il resto», fetta C — registri e superficie anonima** della revisione 3 (via del
  committente il 28-set). Registri: U-022/U-098, U-099/U-239, U-023, U-120, U-124, U-125, U-127, U-234. Porte
  anonime: U-019, U-101, U-102, U-103, U-104/U-121, U-238, U-240, U-242, U-243; più U-113 (gli ultimi commenti
  «solo admin»). **Resta fuori U-021** (IP vero dietro Cloudflare/Plesk/Passenger): va prima misurata la catena
  in produzione. Nessuna migrazione, `deploy/` no. **Codice comune sì**: `Vipi.Domain` (`RfoLimits`),
  `Vipi.Application` (`IRfoSharedStateStore.PotaStoriaAsync`, `EditorTaskService`).
  - **Registri** (`TestoDiRegistro` nuovo): un valore da fuori — percorso, Referer, errore del portale IVAO —
    entra in una riga sola e con un tetto; messaggi e stack dentro una voce rientrano di due spazi, così a
    colonna 0 stanno solo le righe nostre. Prima un %0A scriveva voci finte e poteva zittire una firma vera
    fino a mezzanotte (U-120, U-023); dieci percorsi da 8 kB facevano una voce da 80 kB (U-127).
  - **/Error** (U-022/U-098): la nota «pagina senza eccezione» una al minuto, col conto delle taciute; 300
    richieste non fanno più ruotare il file. E risponde a tutti i metodi (U-234): un'eccezione in un PUT del
    ponte o in un POST esce 500, non 405.
  - **Ponte RFO** (U-099/U-239, U-104/U-121, scelte del committente): chi sbaglia chiave, oltre 30 al minuto
    per IP, riceve 429 senza scrivere niente; il rifiuto vale una riga di log al minuto per IP; 401 e 429 del
    ponte fuori dal registro delle richieste. Scritture: 120 al minuto per evento. Storia: ultime 100 versioni
    per evento (nella stessa transazione), e 30 giorni dopo l'ultima scrittura via tutta (giro notturno di
    `TrafficRetentionHostedService`). Il documento corrente resta.
  - **Segreti** (U-124, U-125): un file dei segreti malformato dice il numero e il difetto, non il nome; il GUID
    della chiave di DataProtection non entra in `avvisi-log.txt`; la password fra virgolette con un «;» non esce
    più in `avvio-diagnostica.txt` (`DbConnectionStringBuilder`).
  - **Porte anonime**: cache delle letture solo con le chiavi di query che le pagine leggono (U-102; il tetto per
    IP sulle pagine, l'altra metà, sta meglio al bordo e non è fatto); riconvalida delle posizioni staff una
    volta ogni 5 minuti per VID, anche con un cookie vecchio rimandato uguale (U-103); 100 circuiti staccati
    invece di 25 (U-238); regex di MarkdownLite con un tempo massimo di 1 s (U-240: una riga patologica prendeva 10 s;
    100 ms, la prima stesura, scadevano sotto carico anche su «__a__», visto nella suite intera); 304 delle immagini senza leggere il database (U-242); chiavi API false oltre il tetto senza
    query (U-243); stream live al massimo 5 per persona (U-101); incarichi solo dall'Editor, e il tasto solo a
    lui (U-019).
  - **Test**: `RegistriEPorteAnonimeTests` (14), ponte RFO (+2 e un caso in più), `StoriaDelPonteRfoTests` (2),
    riconvalida (+1), circuiti (+1), incarichi (+1), MarkdownLite (+1). Rossi col comportamento di ogni
    correzione spento (le firme nuove sono troppe per rimettere i sorgenti di `HEAD`): 25 su 25. Suite intera
    verde, net8 e net10: E2E 423 → **441**, Application 3016 → **3017**, Infrastructure 1754 → **1756**,
    Ui 1781 → **1782**, il resto invariato.
  - **Prova dal vivo** (copia del DB di sviluppo, identità senza livello VID 123456): POST e PUT su `/Error` → 200;
    `/vsop/media/<sha inesistente>` con If-None-Match uguale → 304 (senza, 404); 35 richieste al ponte con la
    chiave sbagliata → 30 × 401 poi 429, e **una** riga di log; la pagina Incarichi si apre senza il tasto
    «Nuovo incarico personale». Nel log solo IVAO irraggiungibile (voluto).
- ✅ **S29** **login IVAO: «The sign-in expired along the way» al primo login dopo un logout** (assegnato dal Master
  il 28-set, segnalato dal committente con un utente generico). Registro di produzione 28-set 10:24Z, 1.46.5: logout
  10:24:05 → `signout-callback` → **una sola** `/auth/login` 10:24:07 → `/signin-oidc` 10:24:19 = IDX21323 «Nonce was
  null», «Stato del giro recuperato: True». Quindi non il doppio avvio del 18-set (§A78): il cookie di correlazione è
  tornato, quello del nonce no — oppure IVAO ha rimandato un nonce diverso. L'handler non distingue i due casi: cerca
  il cookie del nonce che l'id_token porta, e se non lo trova il nonce è «null» in tutti e due.
  - **Correzione** (`Vipi.Host/Auth/NonceNelloStato.cs`): all'andata il nonce entra anche nelle proprietà del giro,
    cioè nello `state` (cifrato con le chiavi del sito, accettato solo col cookie di correlazione del browser); al
    ritorno, in `OnTokenValidated`, se il cookie non c'era e l'id_token porta **quel** nonce, si valida con quello.
    Un id_token con un altro nonce resta fuori, col cookie o senza. La chiave esce dalle proprietà prima del cookie di
    sessione. Ogni recupero lascia un avviso `Vipi.Auth.Ivao` («nonce recuperato dallo stato del giro»).
  - **Diagnosi**: la voce `login-*` di `errori-richieste.txt` e il log hanno una riga **Nonce** — cookie trovato o no
    (quanti in richiesta), token col nonce mandato / con un nonce DIVERSO / senza nonce. ▶ Al prossimo guasto `nonce`
    in produzione: «DIVERSO» = lato IVAO, e il recupero non lo copre. Se invece compaiono gli avvisi di recupero e
    niente guasti, era il cookie.
  - **Test**: `NonceDelLoginTests` (12), il giro intero contro un IVAO finto dentro il test (token endpoint, userinfo,
    id_token non firmato come nel flusso con code). Rosso prima della correzione: senza il cookie del nonce →
    `accesso-non-riuscito?motivo=nonce`, il sintomo di produzione. E2E 441 → **453**, suite intera verde.
  - **Prova dal vivo** (host locale su `localhost:5034`, database vuoto, portale IVAO vero, credenziali inserite dal
    committente): login → logout → login, tutti e tre riusciti. Il guasto in locale non si è ripetuto (cookie tornato,
    nessun recupero): che la causa fosse il cookie lo dirà la riga nuova in produzione.
  - Nessuna migrazione, `deploy/` no, codice comune no (solo `Vipi.Host`).
- ✅ **S30** lotto **L11 «Il resto», fetta D — circuito e doppio clic** della revisione 3 (via del committente il
  28-set). Smistamento dei 27 aperti del tema contro il codice di oggi: **già chiusi per altra via** U-046 e U-180
  (tetto SignalR a 512 KB, U-016), U-175 (U-029), U-083 (U-108), U-118 (U-001); U-114 = U-181 + U-199; U-065 e U-081
  sono lo stesso difetto e ne resta la sola parte di «Differenze» (il resto l'ha chiuso U-017). Si procede a gruppi.
  - **Gruppo 1**: **U-167** («¶ Prosa capofila/distesa» sui Coordinamenti salvava e non ricaricava: etichetta ferma,
    il secondo clic riscriveva lo stesso valore) → `OnChanged` dopo la scrittura, come ogni altro comando della riga.
    **U-197** (`EditLockBar` possiede uno scope ed è `IAsyncDisposable`, ma il suo `DisposeAsync` fermava il battito e
    basta: uno scope col suo DbContext lasciato in piedi a ogni pagina di struttura) → lo chiude a mano; presidio sul
    sorgente esteso a OGNI componente `OwningComponentBase` + `IAsyncDisposable`. **U-114/U-181/U-199** (Permessi e
    Chiavi API: lettura in fila, gesti no; doppio clic su «Salva»/«Crea» = due scritture sullo stesso contesto;
    guasti non tradotti fuori dal gestore) → sentinella prima del primo await, scrittura in `InFilaAsync`, catch
    generale col messaggio; nota dei permessi con `maxlength` 500 e rifiuto in parole nel servizio
    (`RoleAdminService.NotaMassima`, **codice comune** `Vipi.Application`).
  - **Test**: `PaginaPermessiTests` (3), `PaginaChiaviApiTests` (+2), `ProsaCapofilaRicaricaTests` (1),
    `ScopeDellEditingTests` (+1), `RoleAdminServiceTests` (+1). Tutti rossi sul codice di prima (i due doppi clic:
    2 scritture invece di 1). Ui 1782 → **1789**, Application 3017 → **3018**, net8 e net10.
  - **Gruppo 2**: **U-049** (Spazi aerei admin: servizi del circuito, cinque gesti con `_busy` alzato e mai guardato
    in ingresso e un `finally` e basta) → una porta `Gesto` per tutti: sentinella prima del primo await, catch
    generale col messaggio e una riga di log. **U-188** (Diagnostica: «Aggiorna» acceso durante «Rilancia la
    deriva», e il giro con la marcatura fuori dalla fila) → tasto spento e gestore che non parte durante la deriva;
    giro e marcatura in `InFilaAsync`. **U-179** (Glossario: il cestino toglieva al primo clic, senza traccia) →
    `InlineConfirm` con la formula nella domanda (`Gl_DeletePrompt`, it/en); `EfGlossaryStore` scrive l'audit
    (`GlossaryTerm`: Create/Update per le voci di una persona, Delete con la resa nel dettaglio; il seme resta
    muto). **U-190**, parte Glossario e pulizia immagini: i gesti in fila del Glossario passano da una porta con
    sentinella e catch, e su errore la pagina non ricarica (quel che si stava scrivendo resta); «Analizza» e
    «Cancella» della pulizia immagini con sentinella e catch. La parte Struttura di U-190 va con U-192.
  - **Test**: `SpaziAereiAdminUnGestoAllaVoltaTests` (2), `DiagnosticaUnGiroAllaVoltaTests` (2: un finto che conta
    le operazioni sovrapposte sul contesto della pagina, più il presidio sul tasto), `GlossarioGestiTests` (2),
    `MediaCleanupCardTests` (+1), `GlossarioSuDatabaseTests` (+1); `ServizioVuoto` (finto che risponde vuoto, per
    montare pagine con molti servizi). Tutti rossi sul codice di prima: il secondo gesto con 2 scritture o 2
    operazioni insieme invece di 1, le eccezioni fuori dal gestore, la voce tolta al primo clic, nessuna traccia.
    ⚠️ Il doppio clic in bUnit si prova su DUE righe: il secondo clic sullo stesso tasto trova il gestore già
    rimpiazzato dal disegno (le lambda in un `foreach` cambiano id a ogni render), dal vivo no.
    Ui 1789 → **1796**, Infrastructure 1756 → **1757**, net8 e net10. **Codice comune**: `Vipi.Infrastructure` (`EfGlossaryStore`).
  - **Gruppo 3**: **U-065/U-081** (pannello release: «Differenze» senza sentinella né catch generale, e il
    ricarico del ciclo di vita con i tasti accesi) → `ToggleDiff` con la sentinella e la rete di `Run` (chiudere
    passa sempre, un diff già letto si riapre senza leggere); durante il ricarico del ciclo di vita `_busy` acceso,
    senza spegnere quello di un gesto già in volo. **U-198** (`IProssimoAiracService` fra i servizi «sicuri» del
    presidio, ma legge e scrive il database; Versioni lo prendeva dal circuito) → tolto dai sicuri; Versioni lo
    prende dallo scope proprio, la lettura del ciclo entrante e la programmazione in blocco passano dalla fila,
    sentinella su «Ricarica» e «Programma mancanti». **U-174** (ACC: il clic su una riga leggeva le aree dal
    circuito mentre un import scriveva; «Salva limiti (N)», anche da «Fine modifica», partiva sopra un import;
    la prima lettura senza rete) → guardia e catch su `TogglePick` e `SaveAllLimits` (con un import in volo le righe
    restano pendenti e il lock non si rilascia), rete sulla prima lettura. La parte Radioassistenze di U-174 era già
    chiusa da U-132.
  - **Test**: `ReleasePanelTests` (+1, il finto delle differenze esplode con «A second operation» se due letture si
    sovrappongono), `ScopeProprioDellePagineTests` (rosso naturale su Versioni appena tolto il servizio dai sicuri),
    `GestiDegliImportNonCadonoTests` (+2, presidio sul testo come per U-029: la pagina ACC tira una dozzina di
    servizi). Rossi sul codice di prima. Ui 1796 → **1799**, net8 e net10.
  - **Gruppo 4** (editor dello scalo): **U-066** (vSOP militare, radioassistenze: la ✕ passa l'indice e l'elenco
    si leggeva dentro la fila; il secondo clic di un doppio clic toglieva la riga DOPO) → la riga si sceglie al clic
    e si ritrova per chiave, anche per «sposta». **U-163** («+ Alternato»: nome cercato a IVAO fuori dal guardiano;
    l'avviso «ICAO sconosciuto» scritto prima del salvataggio lo cancellava il guardiano entrando) → tutto dentro un
    solo `GuardAsync`, avviso dopo il salvataggio; `EfAirportNameLookup` tratta il timeout di HttpClient e una
    risposta non JSON come «IVAO non risponde» (nome sconosciuto), un annullamento vero passa (**codice comune**
    `Vipi.Infrastructure`). **U-164** (scrittura di un campo di radioassistenza in fila ma fuori dal guardiano) →
    `GuardedAsync`. **U-165** («Crea vSOP militare» dall'editor d'aeroporto senza sentinella, fuori dal tornello) →
    sentinella e `Guarded`.
  - **Test**: `GestiDegliEditorDelloScaloTests` (5, presidio sul testo: nessun test monta i due editor, il guardiano
    ha i suoi test in `DocumentEditorShellTests`), `SezioneAlternatiTests` (+3: timeout e JSON → nome sconosciuto,
    annullamento vero che passa). Rossi sul codice di prima. Ui 1799 → **1804**, Infrastructure 1757 → **1760**.
  - **Gruppo 5**: **U-070 + U-169** (pannello dell'unione: scope proprio senza fila; entrare e uscire di modifica in
    fretta faceva partire un secondo ricarico sopra il primo; i gesti prendevano tre soli tipi di eccezione, e il
    tornello dell'editor che non dà il turno dopo 30 s — pensato proprio per questo pannello — usciva dal gestore)
    → `ScopeProprioCheAspetta`: ricarico del ciclo di vita in fila, fatto solo se la chiave è ancora quella, con una
    rete che azzera la chiave (il ridisegno dopo riprova); gesto e ricarico in fila, l'avviso all'host fuori; catch
    generale. Tolto da `SenzaAttesaNoto` della terza porta. **U-082** (Trasferimenti: la barra degli ACC leggeva il
    resolver nel markup; dopo che chiunque scrive un ACC la copia si rilegge dal database, dentro il render, sul
    DbContext del circuito) → campo `_accs` riempito all'apertura, e la scelta dell'ACC lo cerca lì.
  - **Test**: `PannelloUnioneUnGiroAllaVoltaTests` (2: tre ricarichi sovrapposti sul codice di prima, l'eccezione
    fuori dal gestore), `StationResolverPrewarmTests` (+1, più severo del precedente: nel markup di una pagina
    interattiva il resolver non si legge proprio, commenti esclusi). Rossi sul codice di prima. Ui 1804 → **1807**.
  - **Gruppo 6**: **U-192** (Struttura: cliccare un nodo, anche lo stesso, rileggeva la catena di ripiego e buttava
    quella che si stava scrivendo; «Fine modifica» rilasciava il lock con la catena non salvata) → lo stesso nodo non
    fa niente; con la catena non salvata cambiare nodo chiede prima («Scarta e cambia settore» / «Resta qui»);
    `BeforeRelease` della barra del lock risponde no e dice perché. **U-190**, parte Struttura: `Guarded` prende ogni
    eccezione, «Proponi» ha la sua rete. **U-048** (allegati: `TitleMaxLength` definito e mai usato, nota di versione
    senza tetto; un salvataggio rifiutato lasciava righe `Added` nel DbContext del circuito, e ogni salvataggio dopo
    cadeva con loro) → rifiuti in parole prima di scrivere (`TitoloTroppoLungo`, `NoteTroppoLunghe`,
    `NotaTroppoLunga`: **codice comune** `Vipi.Application`), `maxlength` sulle caselle, `ChangeTracker.Clear()` su
    un `DbUpdateException` come in `EfMediaStore` (**codice comune** `Vipi.Infrastructure`).
  - **Test**: `StrutturaUnaOperazionePerVoltaTests` (+3, più il doppio clic esistente che ora legge UNA volta: il
    secondo clic trova il nodo già scelto), `BibliotecaAllegatiSuDatabaseTests` (3, nuovo: un intercettore fa fallire
    il salvataggio e il contesto deve restare pulito). Rossi sul codice di prima. Ui 1807 → **1810**,
    Infrastructure 1760 → **1763**.
  - **Prova dal vivo** (copia del DB di sviluppo del clone, :5199, utente di sviluppo): `doppio-clic.js` su 17 pagine
    staff (Struttura, ACC, Aeroporti, Confinanti, Sorgenti, Trasferimenti, Da sistemare, Radioassistenze, Allegati,
    Spazi aerei, Audit, Diagnostica, Permessi, Chiavi API, Fraseologia, Compiti, Versioni LIBB) → nessun circuito
    caduto. Glossario: il cestino apre la domanda con la formula, la voce resta fino alla conferma, «Cancel» chiude.
    Struttura, con il lock: riga aggiunta alla catena di DAAA_CTR, clic sullo stesso nodo → niente, clic su
    DAAA_MIL_CTR → la domanda; «Resta qui» tiene la catena; «Fine modifica» → no e il perché; «Scarta e cambia
    settore» → DAAA_MIL_CTR scelto, «Applica» spento; poi «Fine modifica» rilascia. Nel log zero `fail:` e zero
    «second operation». ⚠️ **Trovato dal vivo e corretto**: il perché del «Fine modifica» negato compariva solo al
    gesto dopo — la barra del lock chiama un `Func`, non un `EventCallback`, e la pagina non si ridisegnava; ora
    `InvokeAsync(StateHasChanged)`, e scartando la catena l'avviso sparisce (test di comportamento al posto del
    presidio sul testo, rosso sul commit di prima). Il pannello non si disegnava (finestra dietro): verifiche lette dal
    DOM, non da screenshot. Su SQLite le chiamate «async» non cedono il turno: la corsa vera la provano i bUnit.
- ✅ **S31** lotto **L11 «Il resto», fetta E — import da testo/XLSX** della revisione 3 (via del committente il
  28-set). Perimetro: U-042, U-043, U-044, U-045, U-050 (import), U-178 («Incolla tabella» dei Trasferimenti), e i due
  ingressi da file AIP U-119, U-136. Il resto degli «altri ingressi» (U-041, U-047, U-135, U-137, U-138: rinomina,
  eliminazione, traduzione, immagini) va con la fetta F.
  - **Gruppo 1** (tetti dell'import, **codice comune** `Vipi.Application`): **U-043** (una riga di separatori vuoti
    in coda portava la proposta a righe × ventimila colonne) → `Griglia.Colonne` conta fino all'ultima cella non
    vuota. **U-043/U-050** (una cella in XFD passava il tetto T-022 del lettore, che conta le celle vere, e la proposta
    faceva 2000 × 16 384 celle) → tetto sulle celle della PROPOSTA (`CostruttoreProposta.MaxCelle`, 200 000, righe ×
    colonne vuote comprese), controllato prima di costruire; oltre, `Proposta.Guasto` in parole e l'anteprima non si
    mostra (`ImportaTabella` porta il motivo dove si leggono i guasti del file). **U-044** (la regola delle ancore
    degli alternati provava in tempo circa cubico gli spazi Unicode non ridotti: 1 600 spazi, 8 s misurati) →
    `NormalizzaSegni` riduce OGNI spazio Unicode (non l'a-capo), e la regex ha un tempo massimo di 200 ms (scaduto:
    la riga non si spezza). **U-045** (foglio e stringhe condivise in un DOM `XDocument` intero prima di ogni tetto) →
    lettura in streaming con `XmlReader` su un flusso che smette oltre i 32 MB decompressi MENTRE legge, tetto sulle
    stringhe condivise (`MaxStringheCondivise`), cartella di lavoro e relazioni con un tetto di 1 MB; un guasto
    lascia l'elenco dei fogli.
  - **Test**: `ImportPropostaTests` (+2), `ImportAncoreTests` (+2), `ImportXlsxTests` (+2: un milione di stringhe
    condivise rifiutate con meno di 100 MB allocati; una cella in XFD che il lettore accetta e la proposta rifiuta),
    `ImportaTabellaBarraTests` (+1). Tutti rossi sul codice di prima; i test dell'XLSX che c'erano passano uguali
    sulla lettura nuova. Application 3018 → **3024**, Ui 1810 → **1811**, net8 e net10.
  - **Gruppo 2**: **U-042** (import in coda con «la prima riga è l'intestazione»: le colonne incollate prendevano il
    posto di quelle della tabella e il pareggio tagliava TUTTE le righe; tre colonne su una tabella da quattro, e la
    quarta spariva) → la regola sta in `TabellaGenerica.Importa` (**codice comune** `Vipi.Application`), usata dai due
    editor che importavano allo stesso modo (`DocumentSectionsEditor`, `DocumentBlocksEditor`): in coda le righe che
    c'erano non si toccano, colonne = massimo fra le due, intestazioni sostituite solo a colonne uguali; sostituendo,
    come prima. **U-178** («Incolla tabella» dei Trasferimenti scriveva le clausole una per una: una riga rifiutata a
    metà lasciava salvate le precedenti, invisibili, e al nuovo invio entravano due volte) → `AddClausesAsync` nel
    servizio (valida TUTTE le righe prima, e il rifiuto dice quale) e nel repository (un solo `SaveChanges`: tutte o
    nessuna) — **codice comune** `Vipi.Application` (`IAgreementService`, `IAgreementRepository`) e
    `Vipi.Infrastructure`.
  - **Test**: `ImportInTabellaGenericaTests` (4, nuovo: rossi i due casi in coda con la regola di prima, scritta
    tale e quale nel metodo nuovo), `AgreementValidationTests` (+1, sul database: riga 2 oltre il tetto → niente
    scritto e il messaggio dice la riga; corretta, tre righe una volta sola), `FilaDeiTrasferimentiTests` (+1,
    presidio: la pagina non si monta nei test). Application 3024 → **3028**, Infrastructure 1763 → **1764**,
    Ui 1811 → **1812**.
  - **Gruppo 3** (file AIP, **codice comune** `Vipi.Application`): **U-119** (AirspaceConverter codifica gli
    apostrofi due volte e il parser XML ne toglie una: 32 volumi su 1 536 «VAL D&apos;AOSTA», in pagina e nella
    chiave naturale, e la ricerca di «VAL D'AOSTA» non li trovava) → `WebUtility.HtmlDecode` una volta sui campi
    `SimpleData` e sul `<name>`, negli spazi aerei e nelle radioassistenze; una «&» vera resta «&». ⚠️ La chiave
    naturale dei 32 volumi cambia (nessuno agganciato, dice la revisione), e quelli GIÀ salvati in produzione
    tengono il nome vecchio fino al prossimo caricamento dell'AIP: si ricarica `it.kmz` dopo il pacchetto.
    **U-136** («Confronta con l'AIP» apriva sempre il file in vigore come KMZ: dopo un caricamento `.kml`, nessuna
    radioassistenza letta e il rapporto le dava tutte per mancanti) → `AirspaceNavaidReader.Leggi(byte[])` sceglie il
    lettore dai primi byte («PK» = zip) e torna null se il file non si legge; la pagina lo dice
    (`Asp_NavUnreadable`, it/en) invece di un rapporto falso.
  - **Test**: `AirspaceKmlReaderTests` (+2), `NavaidAipReportTests` (+4: KML salvato letto come KML, KMZ come KMZ,
    file illeggibile → null, nome di radioassistenza decodificato). Rossi i quattro che il difetto tocca; KMZ e «&»
    vera passavano già e restano come controllo. Application 3028 → **3034**.
  - **Prova dal vivo** (copia del DB di sviluppo autorizzata dal committente, :5199, poi cancellata): Trasferimenti
    LIRR, accordo LIRR_ES_CTR ⇄ LIBB_ES_CTR, «Incolla tabella» di tre righe con la seconda oltre il tetto →
    «Row 2: The condition: 600 characters, the maximum is 500.», pannello aperto, nel database 50 clausole prima e
    50 dopo; corretta la riga e reinviato → «Imported 3 clauses», 53 clausole, ognuna delle tre una volta sola.
    «Confronta con l'AIP» sul KMZ in vigore → 218 righe di rapporto, nessun «file non leggibile». Nel log zero
    `fail:` e zero «second operation». ⚠️ Non provati a schermo: l'anteprima di «Importa tabella» con il rifiuto
    per troppe celle (nella copia nessun documento ha una tabella generica né un vSOP militare; la prova è il test
    bUnit sul componente vero) e la decodifica dei nomi AIP (i volumi salvati si rileggono solo ricaricando l'AIP).
- ✅ **S32** lotto **L11 «Il resto», fetta F — derivazioni e dati** della revisione 3 (via del committente il
  28-set). Perimetro: d08 (U-060, U-061, U-148…U-160), d10 (U-076…U-080, U-194, U-195, U-196), U-200 (d11) e il
  resto di d06 (U-041, U-047, U-135, U-137, U-138). Smistamento sul codice di `9a7dc868` con due agenti in sola
  lettura: **già chiuse** U-159 (= U-058, S27: la vLOA generata nasce bozza) e U-160 (= U-040/U-036, S25: canale
  a mano rifiutato sulle righe importate, righe «staccate»); **in parte** U-194 (la nota della promozione è di
  S30, resta la nota LVP) e U-080 (la nascita è di S27, resta la pubblicazione); **dubbia** U-078 (tre test verdi
  dicono il contrario del meccanismo: si decide eseguendoli).
  - **Scelte del committente** (28-set): U-060 la vIPI e la vLOA **continuano a stampare** l'accordo verso un ente
    sparito, e parte una voce «da rivedere» per chi lo possiede; U-077 la passata del pubblico di catalogo gira
    **un'ultima volta** e poi un timbro la spegne; U-135 le frequenze collegate a un settore eliminato sono **da
    rivedere** (non bloccano), ripieghi e agganci AIP si tolgono nella stessa transazione; U-080 una **passata
    d'avvio** rimette il puntatore ai documenti pubblicati che non l'hanno (oggi la vLOA 65).
  - **Gruppo 1** (editor degli accordi, **codice comune** `Vipi.Application` e `Vipi.Infrastructure`): **U-154**
    (la barra «in blocco» scriveva la condizione senza il tetto delle colonne, e poteva svuotarla a una clausola
    «in ogni caso», che il pannello non salverebbe) → `SetConditionAsync` passa dagli stessi tetti di
    `ValidateClause`, il repository rifiuta la condizione vuota su una «in ogni caso» PRIMA di toccare le righe
    tracciate, `maxlength` sul campo della barra. **U-061** (eliminare una variante scioglie il gruppo rimasto di
    una; «Annulla» rimetteva l'eccezione a profondità 1 accanto a una capofila fuori dal gruppo, e il controllo
    dell'outline la rifiutava DOPO averla salvata) → la foto dell'eliminazione porta anche la posizione delle
    sorelle (`AgreementOutlineRestore.SorelleDi`, una regola sola per la riga e per il blocco), il ripristino la
    rimette a chi è ancora fuori da ogni gruppo, e l'outline si controlla in memoria prima di scrivere — anche nel
    ripristino di un accordo o di una sezione, che prima restavano salvati con l'orfano dentro.
  - **Test**: `AgreementValidationTests` (+2), `AgreementRepositoryTests` (+3, e il test dell'outline rotto ora
    chiede che non resti scritto niente), `FilaDeiTrasferimentiTests` (+2, presidio: la pagina non si monta nei
    test). Rossi sul codice di prima (U-061 con la firma nuova e le sorelle ignorate). Infrastructure 1764 →
    **1769**, Ui 1812 → **1814**, net8 e net10.
  - **Gruppo 2** (giro della deriva e anagrafica, **codice comune** `Vipi.Application` e `Vipi.Infrastructure`):
    **U-156/U-200** (ogni giro decide su un insieme letto all'inizio e riconcilia alla fine: il notturno e quello
    dopo le modifiche si chiudevano le righe a vicenda, e la riconciliazione di una pubblicazione arrivata a metà
    giro veniva disfatta — «da ripubblicare» su un documento appena pubblicato) → `ImpactDriftUseCase` un giro
    alla volta in tutto il processo (semaforo statico: ogni giro vive nel suo scope); la pubblicazione aspetta il
    giro in corso e richiude dopo di lui. Vale per un processo solo, che è il deploy di oggi. **U-157** (il canale
    è nell'identità, e il documento cita per identità: cambiarlo su una riga citata la faceva sparire in silenzio
    dalle tabelle militari) → `SetChannelAsync` rifiuta come l'eliminazione (`NavaidWrite.Citata`, in coda
    all'enum), e le due pagine lo dicono (`Nav_ChannelCited`, it/en). Scelta del Sito: rifiutare, non riscrivere
    le citazioni — è la regola che l'eliminazione ha già. Il resolver che scarta in silenzio una chiave non
    trovata resta com'è: con la porta chiusa non se ne creano di nuove.
  - **Test**: `ImpactDriftTests` (+2, un giro trattenuto a metà con un `TaskCompletionSource` nel finto),
    `SezioneRadioassistenzeTests` (+1), `PaginaRadioassistenzeTests` (+1). Rossi sul codice di prima.
    Infrastructure 1769 → **1772**, Ui 1814 → **1815**.
  - **Gruppo 3** (frasi e tabelle dei coordinamenti, **codice comune** `Vipi.Application`): **U-153** (il codice
    del mittente si ometteva se le sue LETTERE stavano nel nome: la «N» di DTTC_N_CTR dentro «TUNIS») → si omette
    solo se il nome lo porta come parola. ⚠️ Effetto visibile: il mittente «Athinai Radar West» (codice W) ora è
    «Athinai Radar West W», la forma che il ricevente aveva già — un lato e l'altro con la stessa regola; il
    file approvato della caratterizzazione cambia su quelle 20 righe. **U-152** (ponte Aurora: la condizione
    della capofila non pesava sulle eccezioni) → si valuta la riga e ogni antenato, vince l'esito peggiore; oggi
    il ponte è spento, la regola è pronta per quando si riaccende. **U-149** (in una tabella mista la capofila
    veniva dalla prima riga e nominava un ricevente solo) → capofila solo se tutte le righe dicono la stessa,
    altrimenti le frasi distese (scelta del Sito: la regola che la tabella ha già senza capofila). **U-155** (la
    vLOA leggeva gli accordi di ciascuna ACC, e un accordo di confine entrava due volte; il collasso lo
    nascondeva, ma senza punti scriveva «, ») → gli accordi si tolgono dai doppioni per Id PRIMA di espanderli, e
    il collasso non accoda un punto vuoto o già detto.
  - **Test**: `CoordinationSentenceComposerTests` (+1), `TransferMatcherTests` (+1), `CoordTableTests` (+2),
    `VloaOrdineFrequenzeTests` (+1, servizio vero su SQLite), `CoordinationCharacterizationTests` (file approvato).
    Rossi sul codice di prima. Application 3034 → **3036**, Infrastructure 1772 → **1773**, Ui 1815 → **1817**.
  - **Gruppo 4** (ciclo e snapshot delle derivate, **codice comune** `Vipi.Application`): **U-151** (una release
    programmata al ciclo entrante congela la vIPI dentro `ShapeReleaseContext.Capturing`, ma le SID/STAR fra i
    punti degli accordi si chiedevano al ciclo di OGGI) → `ProcedureReferenceResolver` legge il ciclo della
    cattura, se c'è (stesso scope del contesto); fuori, oggi come prima. **U-148** (una vIPI ACC pubblicata prima
    delle sezioni SCCAM/FIC: la vista rifiuta di derivarle, ma l'assemblatore le accodava lo stesso, e la pagina
    pubblica — e dopo S27 anche l'anteprima di release — mostrava due sezioni «Nessun settore») → dallo snapshot
    le sezioni di `AccDocumentAssembler.SoloSePubblicate` non si accodano; nella bozza sì, come prima. Scelta del
    Sito: nasconderle, la regola che il commento della vista aveva già scritto.
  - **Test**: `ProcedureReferenceResolverTests` (+1), `AccDocumentAssemblerTests` (+1). Rossi sul codice di
    prima (U-151 con la correzione spenta). Application 3036 → **3038**.
  - **Gruppo 5** (passate d'avvio e pubblicazione, **codice comune** `Vipi.Application` e `Vipi.Infrastructure`,
    tocca `Vipi.Hosting`): **U-076** (il passo delle LVP «subito dopo le Procedure generali» era un invariante e
    non un trasloco: a ogni consegna disfaceva il riordino dell'editor, da S24 anche nella pubblicata) → le LVP si
    spostano solo insieme al trasloco delle regole piste, cioè sui documenti col vecchio indice del 12-set.
    **U-077** (il pubblico di catalogo rimetteva «piloti» a ogni consegna, anche nelle archiviate) → un'ultima
    volta e poi il timbro `ImportCategories.PubblicoDiCatalogo` la spegne (scelta del committente): ⚠️ un «per
    tutti» scelto dopo l'ultima consegna si ribalta quest'ultima volta. **U-195** (la chiave «purpose» data a
    ogni radice libera «Purpose», anche con la sezione di catalogo già presente) → una sola per versione.
    **U-080** (la pubblicazione archiviava solo la versione del puntatore: un documento «Published» senza
    puntatore — la vLOA 65 — ne avrebbe tenute due «Published») → `PublishAsync` e `PublishWorkingVersionAsync`
    archiviano ogni altra pubblicata; e la passata d'avvio `RestorePublishedCurrentVersionAsync` (scelta del
    committente) rimette il puntatore ai documenti pubblicati con una sola versione pubblicata — con due non
    indovina. Dati toccati al primo avvio: il puntatore della vLOA 65; l'ultimo giro del pubblico di catalogo.
  - **Test**: `ReconcileAirportSectionsTests` (+1), `IndiceDelSodTests` (+1), `DocumentMaintenanceTests` (+2),
    `EditingRepositoryTests` (+1), `ReleaseRepositoryTests` (+1). Rossi sul codice di prima (la passata nuova con
    un corpo vuoto). Infrastructure 1773 → **1779**; Hosting 68 invariato.
  - **Gruppo 6** (scritture che cadono, **codice comune** `Vipi.Application`, `Vipi.Infrastructure`, `Vipi.Domain`):
    **U-078 FALSA** — un `false` scritto alla nascita arriva nella colonna nonostante `HasDefaultValue(true)`:
    riletto da un SECONDO contesto (il default del database è `true`, quindi è stato scritto). Resta la prova
    come presidio (`AccEsteroNasceSpentoTests`). **U-079** (un ATIS con troppe piste superava i 32 caratteri di
    `AtcSessionRunways`, e la riga non salvata restava «Added» nel contesto del giro: il salvataggio del traffico
    di tutta la divisione cadeva con lei, a ogni giro) → oltre `AtisRunways.MaxLunghezza` (la stessa costante
    della colonna) il parser non dice niente — scelta del Sito, la regola del parser «meglio niente che
    indovinare» —, e `AppendRunwayAsync` stacca la riga se il salvataggio cade. **U-194** (la nota LVP senza tetto
    sulla colonna da 2000) → `AirportLvpMinima.NotaMassima` nel modello, nel servizio (frase) e nel campo
    (`maxlength`); la nota della promozione era già di S30. **U-196** (dentro una transazione un salvataggio
    caduto e catturato buttava anche le famiglie dei salvataggi già riusciti: alla conferma il giro della deriva
    non riceveva niente) → `SegnalaModificheInterceptor` tiene due insiemi, «in volo» e «riuscite»: chi cade
    butta solo le sue.
  - **Test**: `AccEsteroNasceSpentoTests` (+1, verde anche prima: è la prova che U-078 non c'è), `AtisRunwaysTests`
    (+1), `PisteInUsoTests` (+1), `AirportLockGuardTests` (+1), `SegnalaModificheInterceptorTests` (+1),
    `EditorLvpTests` (+1, nuovo). Rossi sul codice di prima gli altri cinque. Application 3038 → **3039**,
    Infrastructure 1779 → **1783**, Ui 1817 → **1818**.
  - **Gruppo 7** (immagini, **codice comune** `Vipi.Infrastructure`): **U-137** (sostituire o togliere l'immagine
    di un blocco lasciava la vecchia nel deposito, fuori dalla quota e mai ripulita) → `UpdateBlockAsync` libera
    le foto che il blocco non cita più, dalla stessa porta della cancellazione (`DeleteOrphansAsync` ricontrolla
    tutti i posti: una foto citata altrove resta). Gemello trovato nel giro: l'**intro di pagina**
    (`EfPageIntroStore`) aveva la stessa perdita, chiusa allo stesso modo. **U-138** (la pulizia controllava le
    citazioni e POI cancellava: una foto ricaricata in mezzo — il deposito deduplica per sha, niente riga nuova —
    e citata in un blocco spariva da sotto) → dopo la cancellazione si ricontrolla, e una foto tornata in uso si
    rimette coi byte ancora in mano. Resta scoperto solo un blocco salvato DOPO il secondo controllo con una foto
    caricata PRIMA della cancellazione: millisecondi contro i minuti di prima. Senza migrazione (scelta del Sito:
    niente colonna «ultimo uso»).
  - **Test**: `MediaMaintenanceTests` (+2; U-138 con un intercettore che cita la foto nell'istante del DELETE),
    `IntroDiPaginaTests` (+1). Rossi sul codice di prima. Infrastructure 1783 → **1786**.
  - **Gruppo 8** (catalogo dei settori, **codice comune** `Vipi.Application` e `Vipi.Infrastructure`): **U-041**
    (la rinomina di un callsign lasciava indietro gli agganci AIP, che si risolvono per callsign: il settore
    tornava in silenzio alla forma di IVAO) → `EfCallsignRenameService` riscrive `SectorAirspaceBindings.Callsign`
    e, scelta del Sito, anche le scelte per callsign del profilo dei documenti (AoR e frequenze nascoste, ordine
    delle frequenze) con lo stesso riscrittore dei blocchi. **U-135** (eliminare un settore portava via in cascata
    le frequenze d'aeroporto collegate senza dirlo né marcare il documento dello scalo, e lasciava appesi ripieghi
    e agganci AIP) → i fatti del settore contano i tre legami; il piano dice le frequenze «da rivedere» (scelta
    del committente: non bloccano) e marca il documento dello scalo, e nomina ripieghi e agganci, che
    l'esecuzione toglie nella stessa transazione. `SectorFacts` cresce con tre parametri in coda, con default:
    i costruttori di prima restano validi.
  - **Test**: `RinominaSettoreTests` (+1), `DeletionRepositoryTests` (+1, fatti + piano + esecuzione). Rossi sul
    codice di prima. Infrastructure 1786 → **1788**.
  - **Gruppo 9** (traduzione, **codice comune** `Vipi.Application` e `Vipi.Infrastructure`): **U-047** (il lotto
    verso Azure si tagliava a 50 testi e non ai 50 000 caratteri per richiesta: 50 testi lunghi facevano un 400,
    guasto definitivo del giro) → il lotto si chiude al primo dei due tetti (`AzureOptions.MaxCaratteriPerChiamata`,
    45 000: un margine per il corpo JSON); un testo da solo oltre il tetto parte da solo. Scelta del Sito: niente
    salvataggio parziale dei lotti riusciti — cambierebbe il contratto del giro, e il conto dei lotti pagati
    (T-045) esce già.
  - **Test**: `AzureTranslationEngineTests` (+1, un finto che fa come Azure: 400 oltre i 50 000). Rosso sul codice
    di prima. Infrastructure 1788 → **1789**.
  - **Gruppo 10** (proiezione e confinanti, **codice comune** `Vipi.Application` e `Vipi.Infrastructure`):
    **U-150** (un settore disattivato — nascosto, o con l'ACC nascosto — teneva la frequenza del giorno in cui era
    uscito dal giro, e vIPI e frequenze collegate degli scali la leggono ancora) → la proiezione la aggiorna dal
    catalogo intero senza riattivarlo. ⚠️ Dati al primo avvio: i settori disattivati (Barca, Pioppo, Legion…)
    prendono la frequenza di catalogo; dove differisce da quella congelata, la deriva chiede di ripubblicare.
    **U-158** (tre definizioni di «settori confinanti»: la vLOA per geometria, la ricerca dei documenti da
    avvisare sull'elenco fermo all'ultimo import, e un terzo elenco nella coppia che nessuno leggeva) → una regola
    sola, `VloaConfinanti.Calcola` (geometria di oggi, la stessa soglia), usata dalla vLOA e dalla ricerca, che
    tiene l'elenco solo come «oppure» (un settore sparito non ha più il poligono); tolti i due campi morti e il
    loro ripiego sul catalogo. **U-060** (accordo verso un ente sparito: la vIPI lo stampava, la vLOA lo toglieva,
    e alla controparte non arrivava niente) → scelta del committente, **si stampa e si segnala**: la vLOA tiene
    nei coordinamenti anche i settori disattivati, e sparizione e nascondimento avvisano anche i documenti
    della controparte degli accordi (il suo documento o quello dello scalo, la vIPI ACC del suo centro, la vLOA
    della coppia). Un riparentamento no: non cambia la frase. ⚠️ Non fatto: il genere «controparte sparita» fra
    le lacune della pagina Trasferimenti (`AgreementGaps`) — la segnalazione basta a portare l'editor
    all'accordo; resta un'idea, non un difetto.
  - **Test**: `SectorProjectionTests` (+1), `DocumentImpactLookupTests` (+2), `VloaOrdineFrequenzeTests` (+1).
    Rossi sul codice di prima. Infrastructure 1789 → **1793**.
  - **Chiusura**: suite intere verdi net8 e net10 — Application **3039**, Infrastructure **1793**, Ui **1818**,
    Hosting **68**, E2E **453** (net10); CI verde su tutti e dieci i commit. **Prova dal vivo** (copia del DB di
    sviluppo autorizzata dal committente, :5199, poi cancellata): all'avvio il timbro `PubblicoDiCatalogo` è
    scritto (U-077, l'ultimo giro è passato); Trasferimenti LIBB, gruppo LGKF di due varianti — eliminata la
    seconda il gruppo si scioglie, «Undo» lo ricompone (nel database gruppo 1 su tutte e due le righe, U-061);
    il campo «free condition» della barra in blocco porta `maxlength=500` (U-154); editor LIRF, «Declare minima» →
    la nota LVP porta `maxlength=2000` (U-194); vIPI ACC LIBB e vLOA LIBB ↔ LGGG si aprono senza errori, nessuna
    sezione «Nessun settore» e nessuna cella «, ». Nel log zero `fail:` e zero «second operation». ⚠️ Non
    provati a schermo: il canale di una radioassistenza citata (U-157: nella copia nessun vSOP militare, la
    prova è il test sul database e il bUnit della pagina), la vLOA 65 e il suo puntatore (non c'è nella copia),
    e le segnalazioni U-060 (richiedono una sparizione dal catalogo: la prova è il test d'integrazione).
- ✅ **S33** lotto **L11 «Il resto», fetta G — dominio aeronautico** della revisione 3 (via del committente il
  28-set; stesso ramo, un unico caricamento con S9…S32). Perimetro: d13 aperte, U-214…U-230 (17 voci; U-015 e
  U-089…U-095 già chiuse in L8/L2). Smistamento sul codice di `5728ffd6` con due agenti in sola lettura:
  **già chiusa** U-216 (S22/U-015: `AirportViewFormat.TlAdesso` dà «—» senza QNH; resta «una funzione sola»,
  che si chiude con U-227); **da una scelta del committente** U-214 (vento ignoto e regole piste), U-217 (ATZ
  in AGL), U-219 (aeroporti gestiti da un APP); le altre ancora vere. La prova rossa del registro per U-223 era
  sbagliata (vento 250 sulla 163 è di prua): quella giusta è 070/15 sulla «16» di rotta vera 163.
  - **Scelte del committente** (28-set): U-214 senza vento noto (METAR assente, NIL, «/////KT», scaduto) le
    regole piste **non decidono**: nessuna pista, «—»; VRB sopra i 2 kt conta come vento ignoto. U-217 le quote
    AGL delle ATZ si alzano dell'elevazione dello scalo **nelle statistiche e nel 3D**. U-219 un `_APP` gestisce
    gli scali delle **TWR figlie nell'albero** dei settori proiettato. U-218/U-228 lo storico si rifà con una
    **passata d'avvio una tantum** (turni dell'anno ricalcolati, giorni aeroporto già consolidati rimessi in
    coda), poi un timbro la spegne.
  - **Gruppo 1** (TAF, **codice comune** `Vipi.Application`): **U-215** («PROB30 TEMPO periodo» apriva una riga
    PROB vuota «dall'inizio della validità» e poi un TEMPO senza probabilità) → il TEMPO subito dopo un PROB
    senza periodo né gruppi eredita la probabilità, niente riga vuota; il meteo dello scalo lo scrive
    «PROB30 TEMPO». Un «PROB40 periodo» da solo resta un gruppo PROB.
  - **Test**: `WeatherParsingTests` (+2). Rosso sul codice di prima (3 segmenti invece di 2). Application
    3039 → **3041**.
  - **Gruppo 2** (QFE, **codice comune** `Vipi.Application`): **U-226** (il QFE del vAWOS usava la retta dei
    27 ft/hPa «perché gli scali italiani sono tutti sotto i 1 500 ft», premessa falsa: 2–4 hPa di errore sugli
    scali in quota) → pressione dell'atmosfera standard all'altezza della soglia, `q·(1 − 6,8756·10⁻⁶·h)^5,2559`.
    In pianura non cambia niente (1013 a 81 ft resta 1010).
  - **Test**: `AwosCompositionTests` (+1: 990 hPa a 1796 ft → 927, la retta dava 923). Rosso sul codice di
    prima. Application 3041 → **3042**.
  - **Gruppo 3** (pannello vento del vAWOS, solo Ui): **U-225** (con vento calmo o VRB le caselle CROSS e TAIL
    dicevano «00» al primo disegno e «--» dal primo aggiornamento del JavaScript) → traverso e coda passano in
    `AwosTesto.Componenti`, che con vento assente, calmo o variabile non dà componenti; la casella scrive «--»
    come il JavaScript.
  - **Test**: `AwosVentoTests` (nuova, +4). Rosso con la logica di prima spostata tale e quale (VRB e calmo
    davano «00»). Ui 1818 → **1822**.
  - **Gruppo 4** (motore delle piste, **codice comune** `Vipi.Application`): **U-223** (regole e ripiego
    misuravano coda e traverso sull'ident×10, il pannello vento del vAWOS sulla rotta vera dell'anagrafica: su
    una «16» di 163° un 070/15 dava coda 0 alla regola e «TAIL 01» sul quadro) → `Suggest`, `EvaluateRules` ed
    `ExplainRules` prendono le rotte vere (`RunwayRow.Rotte`, `RwEdit.Rotte`; senza rotta si ripiega
    sull'ident×10) e tutti e cinque i posti che decidono la pista le passano: vAWOS, vIPI e vSOP (i loader ora
    leggono sempre l'anagrafica: la rotta è un dato fisico e la sezione Piste non la porta — scelta del Sito,
    niente campo nuovo nello snapshot, che avrebbe fatto sembrare da ripubblicare ogni aeroporto), vista
    rapida, elenco aeroporti, banco di prova dell'editor. **U-224** (con tre parallele gli arrivi andavano
    sulla «C», perché «16C» < «16L» in ordine alfabetico) → le parallele si riconoscono dal numero dell'ident (con
    le rotte vere 16L e 16R possono differire di un grado) e si ordinano per lato: arrivi a sinistra, partenze a
    destra, la centrale nel mezzo.
  - **Test**: `WeatherParsingTests` (+4: tre parallele, regola sulla rotta vera, ripiego sulla rotta vera,
    parallele a un grado di differenza), `AwosCompositionTests` (+1, il percorso del vAWOS). I primi tre rossi
    col solo parametro aggiunto e non usato; il quarto è la guardia del raggruppamento. Application 3042 →
    **3047**.
  - **Gruppo 5** (vento ignoto, **codice comune** `Vipi.Application`): **U-214** (senza vento noto — METAR
    assente, NIL, «/////KT», scaduto oltre i 90 minuti — il motore lo trattava come calmo e vinceva la prima
    regola «asciutta»: col METAR scaduto il vAWOS proponeva una pista che U-092 aveva promesso di non proporre;
    idem il VRB sopra i 2 kt) → scelta del committente, **le regole non decidono**: verdetto nuovo
    `RuleVerdict.NoWind`, `RunwaySuggestion.VentoNoto` dice quando il vento basta (calmo sì, direzione misurata
    sì, VRB sopra i 2 kt no), e i quattro posti che leggono un METAR la passano (vAWOS, vIPI/vSOP, vista rapida,
    elenco aeroporti). Il ripiego sul vento non cambia: senza direzione non proponeva già niente. Il banco di
    prova dell'editor lascia il vento noto (lo batte chi prova); la scritta «vento non noto» c'è in it/en.
  - **Test**: `AwosCompositionTests` (+6: NIL, «/////KT», METAR assente, VRB05 → nessuna pista, rossi sul
    codice di prima; 00000KT e VRB02 → la regola vale), `WeatherParsingTests` (+1, il verdetto del motore).
    `PistaMaiUsareTests.Una_regola_che_nomina_la_soglia_esclusa_continua_a_valere` passava un METAR nullo per
    comodità, cioè proprio il comportamento tolto: ora ha un vento calmo, e prova ancora le esclusioni.
    Application 3047 → **3054**.
  - **Gruppo 6** (TA e TL del vAWOS, **codice comune** `Vipi.Application`): **U-227** (il vAWOS prendeva TA e fasce
    TL dall'anagrafica viva, mentre documento e vista rapida mostrano la sezione congelata: una modifica non
    pubblicata cambiava il quadro pubblico) → estensione diretta della scelta del 17 settembre già applicata a
    regole, LVP e soglie escluse: `PisteDecisive.Transizione` porta la sezione congelata, e senza si proietta la
    tabella viva come fa il documento. Con lui si chiude il resto di **U-216** («una funzione sola»): il TL
    «adesso» ha una regola sola, `LivelloDiTransizione.Adesso` in `Vipi.Application`, che usano vAWOS, vista
    rapida e sezione del documento; `AwosComposition.TransitionLevel` (la seconda regola, sui numeri) è tolta e
    `AirportViewFormat.QnhRowMatches` delega.
  - **Test**: `PisteDalPubblicatoTests` (+2: la transizione congelata arriva, senza non si inventa; sul codice di
    prima non compilano, il campo non c'era), `AwosCompositionTests` (i due test del TL passano dalla funzione
    unica). Application 3054 → **3056**.
  - **Gruppo 7** (convertitore, i numeri, **codice comune** `Vipi.Application`): **U-220** («N-41.99» o «-41.99N»
    diventavano latitudine sud senza avvisi) → segno ed emisfero insieme fanno un token fuori intervallo, che si
    segnala; il segno da solo resta la forma di sempre per sud e ovest. **U-221** («45,4642, 9,1900»: decimali
    all'italiana con la coppia separata da «, » si spezzavano in quattro numeri) → dopo la conversione dei
    decimali le virgole rimaste valgono da separatore se sono tutte seguite da uno spazio.
  - **Test**: `CoordinateParserTests` (+4, tre rossi sul codice di prima; il quarto è la guardia del segno
    senza emisfero). Application 3056 → **3060**.
  - **Gruppo 8** (convertitore, il JSON, **codice comune** `Vipi.Application`): **U-222** (un GeoJSON `Feature` o
    `FeatureCollection` non si riconosceva: `PolygonGeometry` in un oggetto cerca solo `points/coordinates/…` in
    cima, usciva vuoto e il testo cadeva nel lettore a righe, lat/lon invertite) → `GeoJsonReader` nuovo, solo
    per il convertitore: `Feature`, `FeatureCollection`, `Polygon`, `MultiPolygon`, un'area per poligono, il
    nome da `properties.name`, i buchi scartati e segnalati come nel KML. `PolygonGeometry` resta com'è per i
    cataloghi (un anello per settore, misurato). **U-229**, metà JSON (un numero oltre il double, «1e999»,
    faceva lanciare `GetDouble` con una `FormatException` che il `catch (JsonException)` non prende, fino alla
    pagina) → `TryGetDouble` e valori finiti, il resto si scarta; sui numeri buoni AoR e statistiche non
    cambiano (Infrastructure 1793 verde).
  - **Test**: `CoordinateParserTests` (+6: Feature, FeatureCollection con MultiPolygon, buco, tre forme con
    «1e999»; tutti rossi sul codice di prima). Application 3060 → **3066**.
  - **Gruppo 9** (convertitore, il testo AIP, **codice comune** `Vipi.Application`): **U-229**, metà segnaposto
    («⟦R3⟧» scritto nel testo passava per un segnaposto del raggio e indicizzava un raggio che non c'era:
    `ArgumentOutOfRange` fino alla pagina) → le parentesi del segnaposto arrivate col testo si tolgono, e
    l'indice si controlla. **U-230** (un arco senza verso si disegnava orario in silenzio, e il verso scritto
    dopo il centro — «… centred on X anti-clockwise till point Y» — si perdeva) → il verso detto mentre l'arco
    aspetta centro o fine vale per quell'arco; senza verso si disegna ancora orario (scelta del Sito: un arco
    serve) ma si segnala come arco incompleto, «verso». Carta F1 §5 aggiornata.
  - **Test**: `AipGeometryReaderTests` (+4: arco senza verso, verso dopo il centro, due segnaposto a mano;
    tutti rossi sul codice di prima). Application 3066 → **3070**.
  - **Gruppo 10** (statistiche, turni e movimenti, **codice comune** `Vipi.Application`, `Vipi.Infrastructure`,
    `Vipi.Hosting` avvio): **U-218** (una riconnessione che si sovrapponeva di pochi secondi alla caduta — la
    sorgente chiude la sessione in ritardo — restava un turno a parte per sempre; e dal vivo una nota ancora
    aperta non più in frequenza non cedeva mai il turno) → `AtcShiftGrouper.Sovrapposizione` (5 minuti: oltre è
    una doppia connessione), la stessa tolleranza in `AtcSessionSync`, e la nota aperta che non è più in
    frequenza vale chiusa al suo ultimo avvistamento, come la chiude quel giro stesso. **U-228** (il
    consolidamento giornaliero degli aeroporti riconosceva lo stesso volo dal piano di volo, e alla
    riconnessione il pilota ne deposita uno nuovo: due arrivi; e del gruppo teneva il primo pezzo, per un
    arrivo la caduta a metà volo) → identità callsign + partenza + arrivo + verso, a pezzi distanti meno di
    3 ore; l'arrivo è il pezzo visto per ultimo, la partenza quello collegato per primo. Il riempimento a
    posteriori (U-094) non usa questa funzione. **Storico** (scelta del committente): passata d'avvio una tantum
    `IStatsMaintenance.RifaiStoricoAsync`, registro `StoricoStatistiche` — turni degli ultimi 400 giorni
    ricalcolati, giorni aeroporto già consolidati **rimessi in coda** (non cancellati: `FetchedUtc` torna al
    giorno stesso, e il consolidamento notturno li riprende dalla sorgente dal più recente, un blocco alla volta;
    un giorno che la sorgente non dà più resta col conto di prima).
  - **Test**: `AtcShiftGrouperTests` (+2), `AtcSessionSyncTests` (+2), `AirportCoverageTests` (+3); rossi sul
    codice di prima tranne le due guardie (sovrapposizione lunga, stesso callsign a ore di distanza).
    `StoricoStatisticheTests` (nuova, +1: turni ricuciti, giorno rimesso in coda con la riga intatta, seconda
    volta niente). Application 3070 → **3077**, Infrastructure 1793 → **1794**, Hosting 68.
  - **Gruppo 11** (aeroporti gestiti da un APP, solo `Vipi.Infrastructure`): **U-219** (per un `_APP` le
    statistiche accreditavano il solo ICAO del callsign: `LIBD_CS0_APP` non gestiva mai Brindisi) → scelta del
    committente, **l'albero**: gli si accreditano anche gli scali che hanno lui come padre di copertura
    (`Airport.ParentCallsign`, il campo della Struttura), figli diretti, albero di oggi come per i poligoni
    d'area; si calcola a ogni lettura, quindi cambia anche i numeri passati, niente da ricalcolare. Misurato
    sulla copia del DB di sviluppo (28-set): 31 scali su 93 hanno un padre, e tutti e 31 sono un `_APP`; gli
    altri restano al solo prefisso, come prima. ⚠️ Fuori dalla voce, e non fatto: la copertura
    del consolidamento giornaliero (`EfAirportTrafficRollupStore`) usa ancora il solo prefisso — con
    `LIRF_PN1_APP` in linea conta coperto LIRF e non LIRA.
  - **Test**: `AtcStatsQueriesTests` (+1, rosso sul codice di prima: solo LIBD). Infrastructure 1794 →
    **1795**.
  - **Gruppo 12** (quote AGL, **codice comune** `Vipi.Application`, `Vipi.Infrastructure`): **U-217** (le quote
    AGL delle ATZ si leggevano come AMSL: una torre «GND–1500 FT AGL» su un campo a 1050 ft rivendicava il cielo
    fino a 1500 ft sul mare, 450 sopra la pista, e il 3D la disegnava lì) → scelta del committente, **statistiche
    e 3D**: il risolutore delle forme mette sulla forma l'elevazione dello scalo del settore
    (`SectorShape.ElevazioneFt`), il catalogo dei volumi la passa (`SectorVolumeRow.ElevazioneFt`), e
    `ShapePart.QuoteAmsl` è la regola sola che usano attribuzione del traffico e proiezione AoR. Carta refactor
    15 §3i aggiornata. Nessun dato storico cambia (0 sessioni su quei callsign, misurato dall'audit). ⚠️ Nei
    documenti: le sezioni AoR derivate degli scali con un'ATZ in AGL cambiano banda, e la deriva può chiedere di
    ripubblicarle — è il dato corretto.
  - **Test**: `SectorVolumeMapTests` (+1), `SectorVolumeTests` (+1, la mappa), rossi coi soli campi aggiunti e
    non usati; `SectorShapeResolverTests` (+1, l'elevazione arriva dalla porta unica). Application 3077 →
    **3079**, Infrastructure 1795 → **1796**.
  - **Chiusura**: suite intere verdi net8 e net10 — Application **3079**, Infrastructure **1796**, Ui **1822**,
    Hosting **68**, E2E **453** (net10); CI verde sui commit dei gruppi. Nessuna migrazione, `deploy/` no.
    **Prova dal vivo** (copia del DB di sviluppo autorizzata dal committente, :5199, poi cancellata): all'avvio la
    passata una tantum scrive «642 sessioni con il turno corretto, 22 725 giorni aeroporto rimessi in coda» (più
    dei ~282 turni stimati dall'audit: il ricalcolo sistema anche le chiavi provvisorie lasciate dal backfill);
    vAWOS LIRF col METAR di prova — VRB05KT: CROSS/TAIL «--» e «no runway in use», 00000KT: «--» già
    nell'HTML prerenderizzato (prima «00»), 16012KT: pista dal vento 16R partenze · 16L arrivi, TL FL70; banco
    di prova dell'editor LIRF: ripiego 16R/16L sulle rotte vere; vIPI LIBD si apre (senza METAR nessuna pista in
    uso). Nel log zero `fail:` e zero «second operation». ⚠️ Non provati a schermo: l'etichetta «PROB30 TEMPO»
    (a sorgenti spente non c'è nessun TAF; la prova è il test del parser), il QFE (pannello EXT. DATA; test
    d'unità), il 3D di un'ATZ in AGL (test sulla proiezione e sul risolutore). ⚠️ In produzione, dopo il
    pacchetto: i giorni aeroporto rimessi in coda li riprende il consolidamento notturno un blocco alla volta,
    dal più recente — l'arretrato si svuota in più notti.
- ✅ **S34** lotto **L11 «Il resto», fetta H — carte e commenti falsi** della revisione 3 (via del committente il
  28-set; stesso ramo, un unico caricamento). Il registro non assegna le voci alle fette: smistate con due agenti in
  sola lettura **tutte le 52 aperte** sul codice di `4532ba78`. **Già chiuse sotto altri numeri**: U-067 (= U-077,
  S32), U-068 (= U-076, S32), U-069 (= U-108, S12; resta un catch mancante in `ApriAsync`), U-182 (= U-019, S28),
  U-244 (= U-137, S32), U-250 (S10, il timbro conta già il traffico). **In parte**: U-115 (da U-103), U-176 (da
  U-154). **I** (migrazioni all'avvio): U-096, U-097, U-100. **Altro**, che non sta in nessuna fetta: 34 voci —
  scelta del committente, **fette nuove J…M** dopo I (J editor documenti, K pagine admin, L JS e pubblico,
  M identità e il resto).
  - **Scelte del committente** (28-set): U-112 la guardia dell'identità di sviluppo diventa **una guardia vera**;
    U-122 **Render non si usa più**.
  - **Codice e commenti** (**codice comune** `Vipi.Application`, `Vipi.Infrastructure`, `Vipi.Hosting`, `Vipi.Host`):
    **U-191** (`ChangeKind`/`_kindEpoch` in Trasferimenti descrivevano una tendina del tipo che non esiste: nessun
    markup, nessun chiamante) → tolti. **U-172** (il commento della riconciliazione del VFR diceva «non la vIPI
    ACC» e due righe sotto la si trattava) → dice il vero. **U-247** («Sposta in…» aveva il tetto di profondità 3,
    mentre `DocumentSection.MaxDepth` è 5 dal 16-set e il repository accetta fino a lì; e il commento diceva
    «= MaxDepth») → il default è `DocumentSection.MaxDepth`. **U-235** (`Program.cs`: il filtro guardava solo il
    nome `StopTheHostException`, e il commento lo attribuiva a WebApplicationFactory, che da .NET 7 non lancia
    niente) → `HostAbortedException` (la lancia `dotnet ef`), il nome vecchio come ripiego, commento vero.
    **U-112** (nel Vipi.Host l'identità di sviluppo nasce solo in Development, quindi la guardia «dev identity
    fuori da Development» non scattava mai) → rifiuta anche l'identità di sviluppo su un indirizzo non loopback
    (`ASPNETCORE_URLS`/`--urls`) o sul MySQL. ⚠️ Non guarda gli endpoint scritti in `Kestrel:Endpoints`.
    **U-231** (`Un_avvio_intero_non_lascia_segnalazioni` asseriva su un report nuovo e vuoto) → tolto; al suo
    posto un E2E sull'avvio vero, `SmokeTests.L_avvio_vero_non_lascia_segnalazioni_di_manutenzione`, provato
    rosso con una passata rotta di proposito.
  - **Carte** (U-233, U-126, U-122, parti del Sito): README (il Vipi.Host è l'host di produzione, non «di
    esempio»; l'identità di sviluppo è il VID 704798, non «admin IT-AOC»; i progetti con esito si contano su
    `tests/conteggi/`, non «15»), `ci.yml` (cosa la CI NON copre: MariaDB oltre lo schema, il ramo Production,
    il pacchetto), testata della carta «vista condivisa» (online dalla 1.43.0, non «in PR»), commento dei proxy
    in `VipiStartup` e `render.yaml` marcato SUPERATO. ⚠️ **Al Master** (file suoi): `HANDOFF.md:1689` («devono
    essere 15»), `deploy/atc-ivao/LEGGIMI-DEPLOY.md:13-17` (rimanda a `LEGGIMI-AGGIORNAMENTO.md`, che è storia e
    comincia con DROP DATABASE: va rimandato a `LEGGIMI-AGGIORNARE-VIA-FTP.md`), `deploy/render/README.md` (da
    marcare superato).
  - **Test**: `SectionMoveTargetsTests` (+1, rosso; `Esclude_le_destinazioni_troppo_profonde_per_il_sottoalbero`
    fissava il tetto vecchio: ora passa 3 esplicito e prova ancora la regola del sottoalbero),
    `ProductionIdentityGuardTests` (+11: sei casi esposti rossi col solo parametro aggiunto, quattro locali, uno
    senza identità di sviluppo), `StartupMaintenanceTests` (−1), `SmokeTests` (+1). Suite intere verdi net8 e
    net10: Application **3080**, Infrastructure **1796**, Ui **1822**, Hosting **78**, E2E **454**. Nessuna
    migrazione, `deploy/` no. Prova a schermo: non serve (nessuna pagina cambia aspetto; la tendina tolta non era
    nel markup). ⚠️ Non provato: il filtro di `Program.cs` sotto `dotnet ef` (non ho lanciato una migrazione).
- ✅ **S35** lotto **L11, fetta I — migrazioni all'avvio** (via del committente il 28-set): U-096, U-097, U-100.
  - **U-096** (su MariaDB ogni DDL fa commit da sé: una migrazione interrotta a metà lasciava le prime istruzioni
    senza la riga in `__EFMigrationsHistory`, e ogni avvio dopo cadeva sulla prima già fatta; due avvii insieme
    migravano insieme). Tre pezzi, solo nel ramo MySQL: `MigrazioniRieseguibili` (nuovo, `Vipi.Infrastructure`)
    sostituisce il generatore di Pomelo in DI e nella factory di `dotnet ef` e riscrive ogni forma che Pomelo genera
    con `IF [NOT] EXISTS` (i `DROP` anche con `ALTER TABLE IF EXISTS`: al secondo giro la tabella può essere già
    rinominata; per le chiavi esterne MariaDB vuole `FOREIGN KEY IF NOT EXISTS`, provato); `TurnoDelleMigrazioni`
    (nuovo) prende `GET_LOCK('vipi-migrazioni:<db>')` sulla connessione di EF attorno a `Migrate()`, 5 minuti
    d'attesa, e ferma l'avvio se non lo ottiene; `SetCommandTimeout(10 min)` per la sola migrazione. Regola per chi
    scrive migrazioni in ADR-0007 §D5. **Prove su un MariaDB 11.4.10 in Docker** (la versione di produzione):
    rosso = storia senza `CausaDelleSegnalazioni` e `database update` → «Duplicate column name 'CauseArgsJson'»,
    verde col generatore nuovo; le 11 migrazioni dal 16-set tolte dalla storia e rieseguite → schema identico
    (`mariadb-dump --no-data`) a un database migrato una volta; migrazione con la tabella rinominata fermata dopo
    la terza istruzione e ripresa → schema identico; due `Vipi.Host` insieme su un database vuoto → senza turno il
    secondo muore con «Duplicate entry '20260805213003_InitialCreate'», col turno partono tutti e due. Nella CI un
    passo nuovo del job `mariadb-schema` rifà la prova delle 11 rieseguite. ⚠️ Visto nella prova dei due avvii
    insieme, non corretto: la passata d'avvio isolata «PubblicoDiCatalogo» del secondo processo registra un
    «Duplicate entry» (l'ha fatta il primo); è idempotente e sparisce al riavvio dopo.
  - **U-100** (`/vsop/health` era «Degraded» per costruzione dalla 1.26.1: contava anche gli avvisi permanenti del
    report, TWR a 5 NM e FSS senza poligono, e una passata d'avvio fallita non cambiava niente) → contano solo gli
    **Error** fuori dal sectorfile, come ha scelto il committente; gli avvisi restano nel corpo
    (`dataConsistencyWarnings`). Le passate d'avvio fallite e le sonde rotte sono Error: muovono il verdetto.
  - **U-097** (scelta del committente: MINOR marcata «non si torna indietro»). Nessun codice: i due file sono del
    Master, il testo gli va con l'avviso. Le tre migrazioni di questo pacchetto (`AliasPerScalo`,
    `ProcedureSostituite`, `SectorfileDifferito`) sono additive (la prima toglie solo un indice): il rollback a due
    rinomine vale ancora.
  - **Test**: `MigrazioniRieseguibiliTests` (+17: le 12 forme una volta sola, 3 intatte, il turno, la guardia su
    ogni istruzione dello script, provata rossa togliendo `UPDATE`/`DELETE` dalle forme ammesse),
    `MySqlMigrationsTests` (ricerche sulla DDL con `IF NOT EXISTS`), `VipiHealthCheckTests` (+1, rosso sul codice di
    prima). Conteggi: Infrastructure **1813**, Hosting **79**, E2E **454** (net8 e net10). Nessuna migrazione, `deploy/` no, codice comune sì (`Vipi.Infrastructure`
    DependencyInjection e due classi nuove, `Vipi.Hosting` avvio e salute). Prova a schermo: non serve (nessuna
    pagina cambia).
  - **Scelte del committente** (28-set): **U-097** una migrazione con DROP o RENAME resta **MINOR, marcata «non si
    torna indietro»**: il foglio di consegna lo dice, e il rollback «a due rinomine» non vale per quel pacchetto
    (⚠️ `Directory.Build.props:84` e `deploy/atc-ivao/LEGGIMI-AGGIORNARE-VIA-FTP.md:219` sono file del Master:
    il Sito prepara il testo e lo passa). **U-100** `/vsop/health` va in Degraded **solo per gli errori che
    danno problemi al sito** (gravità Error), non per i Warning permanenti del report di consistenza (torri a
    5 NM, FSS).
- ✅ **S36** lotto **L11, fetta J — editor documenti** (via del committente il 28-set; stesso ramo, un unico caricamento).
  Smistate le voci ancora aperte (19 mai citate da S10 in poi, più le scelte rimandate): alla J U-062, U-063, U-071,
  U-084, U-117, U-162, U-166, U-176, U-187, U-193, U-207, U-241, U-256. Restano per K/L/M: U-184 (Sorgenti, K);
  U-208, U-209, U-210, U-213 (L); U-123, U-232, U-236 (M); U-116 (scelta da chiedere); U-021 (da misurare in
  produzione); il resto di U-115.
  - **Scelte del committente** (28-set): **U-176** «Applica condizione» spento a campi vuoti, e svuotare è un gesto
    suo («Togli condizione») con la conferma del numero di righe e l'annulla. **U-241** «Pubblica ora» identica alla
    release in vigore non crea niente e lo dice; e la pubblicazione smette di caricare i payload vecchi.
  - **Gruppo 1** (**codice comune** `Vipi.Application`, `Vipi.Infrastructure`): **U-062** (la riconciliazione d'avvio
    guardava le chiavi di catalogo solo sulle radici: una radice libera «Configurazioni pista» diventava a ogni
    consegna una seconda «Regole piste» e perdeva i blocchi) → guarda tutta la versione. **U-071** (il viewer
    riconosceva per titolo a ogni profondità) → solo le radici, e solo se la chiave non c'è già nell'albero.
  - **Gruppo 2** (Trasferimenti; `Vipi.Domain` solo una costante): **U-187** tetto `AgreementClauseLimits.Pista` (80)
    sulla condizione di pista. **U-176** come da scelta; l'annulla rimette la condizione di ogni riga, raggruppata
    per valore, con la stessa porta in blocco. **U-193** `Guarded` torna `bool`: le quattro eliminazioni armano
    l'annulla su quello (con la pagina occupata un'eliminazione mai fatta lo armava), «0 righe» non lo arma, un
    ripristino fallito lo lascia.
  - **Gruppo 3**: **U-162** le celle della tabella generica si modificano dentro il tornello sul blocco corrente e col
    suo token (prima: dal blocco visto all'evento, e la seconda cella in fila era «modificata nel frattempo»).
    **U-063** il selettore delle aree regolamentate, finché un salvataggio è in volo, parte da ciò che ha già
    mandato; nel vSOP militare tabelle fisse e alternati passano una modifica, non la tabella intera. **U-166** rete
    attorno alla lettura dei riferimenti citati nell'editor.
  - **Gruppo 4**: **U-117** l'intro di pagina rilegge alla presa del lock (`Acquired`, come U-011). **U-256** negli
    editor documentali «Modifica» col lock di un altro dice chi lo tiene (`Ed_LockedByOtherNow`).
  - **Gruppo 5**: **U-084** nuovo componente `CampoTesto` (il testo lo tiene il browser; il server lo scrive solo
    quando il valore cambia da parte sua, e ricrea l'elemento solo se il diff non lo vedrebbe) in 27 campi, compreso
    `TypeaheadPicker`; i due numeri dei ripieghi in Struttura passano a `@onchange`; guardia sul sorgente contro
    `value="@…" @oninput`. ⚠️ Chi usa `CampoTesto` assegna il suo campo prima di ogni `await` e non lo trasforma
    (Diagnostica faceva `Trim` nel gestore: spostato dove si cerca). **U-207** la maniglia delle immagini misura in
    unità di layout (divide per lo zoom di `<html>`, come `rootZoom`).
  - **Gruppo 6** (**codice comune**: `IReleaseService.PublishNowAsync` ora `Task<bool>`): **U-241** come da scelta. Si
    salta solo se il payload è identico byte per byte a quello in vigore E non c'è una programmata futura (U-009:
    «Pubblica ora» serve a scavalcarla); la bozza si promuove comunque. `SaveReleaseAsync` legge quattro colonne e
    scrive gli stati cambiati sulla sola colonna (⚠️ non `ExecuteUpdate`: lasciava stantie le istanze già seguite, e
    l'annullo dopo le riscriveva — due test rossi l'hanno detto).
  - **Test**: prove rosse sul codice di prima (dettaglio nei sei commit). Suite intere verdi net8 e net10:
    Application **3082**, Infrastructure **1819**, Ui **1840**, Hosting **79**, E2E **454**. Nessuna migrazione,
    `deploy/` no, resx it/en (7 chiavi), wwwroot sì (`vipi-media.js`).
  - **Prova a schermo** sulla copia del DB (cancellata a fine prova), dal DOM perché il pannello non si disegnava:
    Trasferimenti LIRR, vista elenco, due righe → «Apply condition» spento col motivo; condizione applicata e
    annullata; «Remove condition» con la conferma «2 clausole», poi annulla → rimesse (l'etichetta del tasto di
    conferma diceva «Yes, delete»: ora «Yes, remove»). Ricerca della Diagnostica: `AGNI7G` e sei Backspace a 60 ms →
    il campo resta `AGN` dopo le risposte del server; il ✕ lo svuota. Selettore postazione della vista live: stessa
    sonda, e la scelta da elenco funziona. vSOP MIL LIBG a zoom 120%: freccia sulla maniglia 60% → 65%, salvato 65.
    «Publish now» due volte: la seconda dice «Nothing new…» e nel DB c'è una release sola. Lock scritto nel DB a
    un collega, «✎ Edit» → resta fuori e dice «Being edited by Collega Prova…». ⚠️ Non provati a schermo: U-117
    (serve una seconda identità), U-162 e U-063 (la corsa sta nei test bUnit), U-062/U-071 (dati latenti).
- ✅ **S37** lotto **L11, fetta K — pagine admin** (via del committente il 28-set; solo K, L dopo).
  - **U-184** (Sorgenti, **codice comune** `Vipi.Application`): «Salva» scriveva l'intera policy letta all'apertura
    e riportava indietro in silenzio la categoria cambiata nel frattempo da un altro amministratore.
    `IImportPolicyService.SaveChangesAsync(letta, voluta)` rilegge la policy e scrive solo le categorie toccate;
    restituisce quelle cambiate da altri e tenute, e la pagina lo dice (`Sorg_SavedKept`). Una categoria toccata
    non può essere in conflitto (due valori soli). `ImportPolicySnapshot.With` sostituisce lo switch della pagina.
  - **U-116** come da scelta del committente (variante mista): il dettaglio di un turno altrui scrive la riga
    `StatsProfile` (stessa finestra di 30 minuti) e mostra la fascia «Turno di un altro controllore» con il
    collegamento alle statistiche della persona; l'archivio mondiale resta senza audit e la sua carta lo dice.
  - **Test**: prove rosse sul codice di prima (servizio che scrive la policy intera; pagina di HEAD).
  - **Prova a schermo** sulla copia del DB (cancellata a fine prova), dal DOM: Sorgenti in due schede; B toglie la
    spunta alle SID e salva; A, senza ricaricare, toglie le Piste e salva → «Policy saved. Meanwhile another
    administrator had changed: SID…», nel DB piste e SID entrambe manuali, due righe di audit distinte.
    `/services/stats/session/63433982` (turno di un altro) → fascia «Another controller's shift» col collegamento
    a `/services/stats/user/456130` e riga `View`/`StatsProfile`/`456130` in AuditLogs.
- ✅ **S38** lotto **L11, fetta L — JS e pubblico** (via del committente il 29-set). Solo `Vipi.Ui`, nessun codice comune.
  - **U-208** vista live senza documento e senza indirizzo: la frase «apri la vIPI» riceveva `href="#"`, che Blazor
    risolve contro `<base href="/">` → hub. Senza indirizzo ora la frase è senza collegamento (`Live_NoDocBodyNoLink`).
  - **U-209** `vipi-boot.js`: il segno «caricato» lo mette `onload` (prima si scriveva prima di chiedere il file);
    `onerror` lo toglie, toglie lo `<script>` e riprova da solo dopo 2 s e 4 s, tetto 3 tentativi (anche
    `restaDaCaricare` ignora gli esauriti, o l'osservatore non si spegnerebbe). Simulazione in Node salvata in
    `docs/history/revisione-totale-3/sim-boot.js` (prima: una richiesta sola; dopo: ritenta; con 99 guasti: 3).
  - **U-210** landing dell'ACC: le tre liste in evidenza sono `<li><a href>` invece di `<li onclick>`; nel tema il
    `<a>` riempie la riga (stessa area di clic) e ha il contorno al fuoco da tastiera.
  - **U-213** «Cosa è cambiato»: orario con `VipiTime.DayZ` e `data-utc` (prima UTC senza «Z»).
  - **Test**: rossi tutti e quattro sulle sorgenti di prima. Suite intere verdi: Ui **1847** (net8 e net10), Assets
    63, E2E 454. Nessuna migrazione, `deploy/` no, resx it/en 1 chiave, wwwroot sì (`vipi-boot.js`, `vipi-theme.css`).
  - **Prova a schermo** sulla copia del DB (cancellata a fine prova), dal DOM: landing LIRR, cinque righe `<a href>`,
    nessun `li[onclick]`, il collegamento largo quanto la riga; Tab vero → riga successiva con `:focus-visible` e
    contorno 2px. «What changed»: `15 Sep 2026 · 04:44Z` con `data-utc`, e `vipiApplyOreLocali` aggiunge «· 06:44
    UTC+2» (nel pannello nascosto `requestAnimationFrame` non scatta da solo). vAWOS: da /services/vsop, prima
    richiesta di `vipi-awos.js` dirottata su un file che non c'è, navigazione enhanced a /services/vawos/libc →
    seconda richiesta, orologio che scorre, lo `<script>` fallito tolto, nessuna richiesta dopo l'arrivo.
    ⚠️ U-208 non riproducibile coi dati: `IcaoFromCallsign` ricava lo scalo da ogni callsign con «_», quindi
    l'indirizzo manca solo in un caso latente; resta il test bUnit.
- ✅ **S39** lotto **L11, fetta M — il resto** (via del committente il 29-set). Codice comune: `Vipi.Application`
  (`IModificheInAttesa.PrendiSubito`), `Vipi.Infrastructure` (`DerivaDopoLeModificheHostedService`), `tools/`.
  - **U-123** `tools/prepara-pacchetto.ps1`: la seconda rete cerca **chiavi con un valore** invece dei nomi (una chiave
    nota non vuota e non segnaposto, `Password=` con valore, `rfo_` + 40 caratteri, chiave privata, key-ring), e
    guarda anche il ramo `docs/` e i `.md`. Prima fermava sempre `appsettings.json` (nomi con valori vuoti) e lasciava
    passare il file del ponte RFO e tutto `docs/`. Trovato strada facendo: con **un** file dichiarato `$percorsi`
    era una stringa e «stringa + array» concatenava i percorsi (`@()`). Prova dei cinque casi in
    `docs/history/revisione-totale-3/prova-rete-segreti.ps1` (prima: 1 FERMO, 2 e 3 PASSA, 5 FERMO; ora come
    atteso). Passata su `deploy/` e `docs/` (327 file): resta solo `registro.json`, che nomina «BEGIN PRIVATE KEY»
    per descriverlo e non entra in nessun pacchetto. Guida `preparare-un-pacchetto.md` aggiornata.
  - **U-232** porte provate tutte: `PorteTutteLeScrittureTests` prende per riflessione ogni metodo dei sette servizi
    delle pagine di struttura (55 scritture, 21 letture; l'eccezione voluta `RecomputeFromArchiveAsync` col suo
    perché); `LockDelleScrittureStrutturateTests` ogni `Save…` di APP e vSOP militare, con la guardia che un'altra
    scrittura debba chiamarsi `Save` o essere dichiarata; `PorteDelleAnagraficheTests` dal livello subito sotto la
    soglia (`DivisionStaff` per le porte da Editor) e la controprova alla soglia della classifica. Le quattro
    mutazioni (lock tolto da `DeleteClauseAsync`, `EnsureAsync` in `SaveRegulatedAsync` dell'APP, glossario a
    `DivisionStaff`, classifica a `Editor`) ora fanno rosso: `docs/history/revisione-totale-3/mutazioni-u232.sh`.
    ⚠️ La vIPI ACC resta ai casi a mano: le sue scritture verificano prima che la sezione sia del documento.
  - **U-236** come da scelta del committente: allo spegnimento `StopAsync` prende la finestra aperta e fa il giro nel
    tempo concesso dall'host; un giro già partito finisce invece di interrompersi al segnale di arresto.
  - **Test**: rossi sul codice di prima (U-236 servizio di HEAD; U-232 le quattro mutazioni; U-123 lo script
    vecchio). Suite intere verdi: Application 3084, Infrastructure **1984**, Hosting 79 (net8 e net10), E2E 454.
    Nessuna migrazione, `deploy/` no, nessuna UI (niente prova a schermo: il comportamento è di processo e di script).
- ✅ **S40** rosso intermittente segnalato dal Master dopo la fusione (29-set): `TranslationReviewPanelTests.
  Due_clic_ravvicinati_sulle_righe_ne_aprono_una`, rosso su net8 nella corsa intera della soluzione (Release,
  `--no-build`), verde da solo. Non riprodotto qui (suite Ui 6 volte, e 4 sotto carico con Application e
  Infrastructure in parallelo); l'unica dipendenza dal tempo era il finto, con `Task.Delay(40)` nel conto e 3 s
  di attesa. Ora il conto resta fermo su un `TaskCompletionSource` finché il secondo clic non è passato (un giro a
  vuoto sul dispatcher lo garantisce), attese a 10 s, e le due righe si aspettano prima di cliccare. Controprova:
  tolta la sentinella `_busy` da `ApriAsync`, il test è rosso (`Conti` 2) invece di piantarsi. Solo test.
  ⚠️ `StrutturaUnaOperazionePerVoltaTests` (stesso commit `334267c0`) ha la stessa forma a tempo: non è segnalato
  rosso, resta da rifare allo stesso modo se lo diventa.
- ✅ **S41** diagnostica della 1.47.0 (29-set, file scaricati dal committente in `diagnostica/`). `/vsop/health`
  «Degraded» = un solo errore, «Transfer with no fallback» su LIMM: `LIMM_WS2_CTR → LSAG_TST_CTR`. Scelta del
  committente: «Ginevra si gestisce lo spazio aereo svizzero, WS2 quello italiano più Lugano» → il rilievo non conta
  un settore di un ACC **estero** come chi copre il punto (`CoverageFallbackContext.AccDi`, prefissi della divisione
  passati ad `Analyze`; `ConsistencyReportService` legge `IOptions<DivisionOptions>`). Codice comune `Vipi.Application`.
  Test +1 rosso sul codice di prima; Application **3085**, Infrastructure 1984, Hosting 79, E2E 454. Nessuna UI.
  Dagli stessi file: passate d'avvio 1.47 tutte riuscite; U-236 visto al lavoro («allo spegnimento», 23 segnalazioni);
  Azure Translator 401 dal 27-set 09:18Z (segreti, non codice); U-105 Perugia Approach sez. 5716 da fare a mano.
- ✅ **S42** login di un utente nuovo (29-set 09:16Z): «The sign-in expired along the way», motivo `nonce`, al
  «riprova» entra. Registro: «Cookie del nonce: non trovato (1 in richiesta); token con un nonce DIVERSO da quello
  mandato» — non il cookie perso del 28-set (quello lo recupera `NonceNelloStato`), ma la pagina di **consenso** di
  `sso.ivao.aero/authorize`, che al primo accesso di un membro a un client rimanda avanti state, redirectUrl e PKCE
  ma **non il nonce**. Stessa causa misurata sull'hub IVAO Italy (SkyMistery/Ivao-Italy-Hub, PR 174, nota
  `2026-09-28-il-nonce-e-il-consenso-di-ivao.md`), stessa cura: `HandleIvaoRemoteFailure` su un guasto `nonce`, con
  lo stato letto e senza il segno `vipi.secondo-giro` (nelle proprietà, quindi nello state cifrato), rifà il
  challenge con lo stesso ritorno e `IsPersistent`; un secondo guasto va alla pagina come prima. Il segno esce in
  `OnTicketReceived`. Il nonce resta validato; `RelaxProtocolValidation` resta la via di fuga in config, spenta.
  Test +7 (giro intero col finto IVAO: riparte ed entra, estraneo anche al secondo giro resta fuori; la decisione
  `DeveRipartire`), 4 rossi senza la correzione; E2E **461**. Da provare sul server: un VID mai entrato nel client
  (o revocando il consenso dal profilo IVAO) entra al primo clic; nel registro «Secondo giro: False» e poi «si
  riparte una volta».
- ✅ **S43** giro di sei punti del committente (29-set) sulla porta della vSOP:
  1. «Cosa è cambiato» esce dalle schede pubbliche di `/services/vsop` e va nella sezione **Staff** (la pagina resta
     raggiungibile dal suo indirizzo).
  2. `/services/vsop/mil`: al pubblico, accanto a «Pubblicato», usciva «Nessun documento». L'ultimo ramo della
     colonna Stato era un `else` nudo legato ai tasti dello staff e scattava per ogni non Editor.
  3. Barra in alto: un'altezza sola per i comandi (`--tb-ctl` 34px; misurati prima 32/34/36/38) e, da `tb-3`, la
     lente è un collegamento a `/services/vsop/search` (prima toccarla non faceva niente). Verificato a schermo a 1900,
     1000 e 375px su un database vuoto. Carta `2026-08-22-topbar-misurata.md` aggiornata.
  4. Nascosti nei collegati (verifica): i «Documenti collegati» filtravano documento nascosto e release, e i link nel
     testo possono puntare solo agli allegati; ma due buchi — la vIPI ACC elencava anche un APP disattivato, e nei
     posti **congelati** di una release uno scalo nascosto o un APP disattivato dopo la pubblicazione restava fra i
     collegati. Ora `DocumentiCollegati.PaginaAperta` al disegno (struttura in cache, 2 minuti) e `PerAcc` sui soli
     APP attivi. Codice comune `Vipi.Application`. Resta, minore: `PubblicatoAsync`/`GetCivilEditionAsync` (ponte
     civile↔militare) non escludono una release `Superseded`.
  5. Nascosti e non pubblicati nella ricerca (verifica): no. Cancello `PublicDocumentGate` a ogni ricerca, testo
     solo dalla release in vigore, sezioni nascoste fuori dall'indice; lo stesso per «Cosa è cambiato». Già coperto
     da `SearchAndChangesTests`.
  6. La Guida per ruolo: la parte «Modificare» (e le anteprime di bozza, spostate lì) solo a chi può modificare
     (`IsEditor`), e lo stesso confine nella ricerca (`GuideSearchCatalog.AncorePubbliche`: l'elenco è dei pubblici,
     un capitolo nuovo nasce riservato). Codice comune `Vipi.Application` (SearchService).
- ✅ **S44** il piè di pagina del sito (29-set, committente, con davanti quello dell'hub): `SitoFooter` in
  `SopLayout`, quindi su ogni pagina tranne il vAWOS (layout suo, `AwosLayout`); non si rende quando un host
  aggancia il modulo senza la nostra barra, né in stampa. Marchio, a che cosa serve il sito, l'avviso di simulazione;
  i collegamenti di IVAO (ivao.aero e la pagina della wiki con regole, regolamento e privacy, in una scheda nuova —
  le tre pagine dell'hub su ivao.aero rispondono 404; un link solo all'indice, scelta del committente); in
  fondo diritti e «Parte della International Virtual Aviation Organisation». La **versione** esce dalla barra e va
  qui, allo **staff** (chi può modificare; prima ai soli admin), con la stessa classe `ver-chip`. Su pagina corta il
  piè resta in fondo alla finestra. Verificato a schermo a 1500 (scuro, IT/EN) e 375px; `/services/vawos` senza.
  Test +5 (`PieDiPaginaTests`); Ui **1863**.
  Poi (S46, stesso giorno): i link legali passano alla wiki di IVAO (le pagine su ivao.aero rispondono 404); in fondo
  «Realizzato da Carmine (704798)» col link al profilo IVAO; il piè prende il colore della barra (`--ivao-blue`),
  per uniformità, in entrambi i temi. Test +1; Ui **1870**.
- ✅ **S45** scheda di uno scalo con vIPI **e** vSOP nell'elenco aeroporti di un'ACC (29-set, committente): il clic
  fuori dalle due voci apre il documento principale per categoria — la vIPI su uno scalo civile (anche con presenza
  militare), il vSOP su un campo militare (anche aperto al civile). Prima il riquadro non si cliccava, e dove si
  apriva un solo documento vinceva sempre la vIPI (anche nei «in evidenza» della landing ACC, che usano la stessa
  regola `AeroportoInElenco.Href`). Un collegamento `.apt-main` steso sotto il contenuto, le voci sopra: niente `<a>`
  annidati. Impilamento dei clic provato in Edge (ICAO, nome, meteo, badge, angolo → principale; voci → la loro).
  Test +6; Ui **1869**.
- ✅ **S47** il vAWOS in uno schermo, con una pista come con tre (29-set, committente): prima una pista lasciava
  spazio vuoto sopra e sotto il vento con un riquadro fisso da 420px, e con due o tre piste la pagina scorreva. Il pannello del
  vento non scendeva sotto i 204px delle sue tre righe. Ora `.awos` è alto 100vh (con `min-height: max-content` come
  rete) e il pannello vento è lo stesso in tutti e due gli impianti: `container-type: size`, minimo 96px, e sotto i
  204px d'altezza (`@container`) le tre righe diventano **una fila** (DIR SPEED · EXTREMES GUST · CROSS TAIL) con i
  caratteri legati ad altezza e larghezza. Pista sola: il vento prende l'altezza che resta, fino a 480px. Più piste:
  colonne fisse a `clamp(…, 14vw, 270px)` e colonna visibilità/nubi più compatta. Nuovo `awos-wcorpo` attorno alle
  tre righe (JS invariato). Provato in Edge su copie statiche di LIBF/LIRP/LIRF col foglio nuovo: nessuno scorrimento
  e nessun pannello tagliato a 1900×920, 1536×730, 2560×1300 (e a 1366×650 e 1280×600 con una o due piste); con tre
  piste sotto i ~700px la pagina scorre di poco invece di tagliare; telefono senza scorrimento orizzontale. Test
  invariati; Ui **1870**.
- ✅ **S48** la vIPI e il vSOP sono dello **scalo**, non di una posizione; le posizioni sparite da IVAO escono da sole
  (29-set, committente). Il caso: a LIBG e LIRE IVAO ha tolto la TWR il 21-set (l'APP fa da torre, «Tower/Approach»).
  Il nostro catalogo non potava mai, quindi la torre fantasma restava nelle frequenze della vIPI. E non si poteva
  eliminare: la regola D6 («la torre cade solo con lo scalo») la proteggeva, e il documento le veniva riagganciato
  a ogni apertura dell'editor. Dal 25-ago il legame vero era già `Airport.DocumentId`; restava il legame vecchio sui settori.
  Ora: (1) `EnsureDocumentAsync` sgancia invece di riagganciare DEL/GND/TWR; (2) il giro d'avvio
  `LinkAirportDocumentsAsync`, dopo il ponte, sgancia i settori che portano la vIPI del loro scalo (sulla copia del
  29-set: 70 settori in 46 scali; restano legati solo gli APP non remotizzati al loro documento); (3) via la D6,
  l'«unica torre» di `DeleteSectorAsync` e la `{ICAO}_TWR` inventata quando uno scalo non ha posizioni; (4) l'import
  toglie dal catalogo d'aeroporto le posizioni che IVAO non manda da due giri (`SogliaEliminazione`), solo se la risposta
  non è vuota, mai le manuali, figli al nonno, riga nel registro (sulla copia usciranno solo `LIBG_TWR` e `LIRE_TWR`).
  La proiezione spegne il settore e lo segnala; la vIPI dello scalo ora riceve la segnalazione passando dal settore
  (`DocsForCallsignsAsync`), e la deriva segnala la sezione Frequenze congelata da ripubblicare. Badge «no TWR» in
  Aeroporti: filtro neutro, non più un avviso. Le frequenze restano derivate dal catalogo, come nella vIPI ACC.
  Test +8 (import, ponte, generazione, segnalazione; i 4 del comportamento nuovo ROSSI sul codice di prima), 4
  riscritti sulla regola nuova. Infrastructure **1992**. Migrazione no. Codice comune `Vipi.Application`
  (DeletionRules, StructureEditModels, StaleCatalogRow). Resta com'è il catalogo ACC (non pota).
- ✅ **S49** enti ATC, fase 1 (29-set, committente, ramo `fix/enti-atc`): la vIPI APP è di un **ente** (`AtcUnit`:
  codice stabile = chiave di pubblicazione e indirizzo, nome, ACC, modo, documento; posizioni IVAO per nome, la
  prima è la principale), non del settore APP. Casi: Pratica di Mare vuole `LIRE_TWR` (torre che fa l'APP) e non
  `LIRE_APP`; Palermo remotizzato non deve riscrivere la vIPI. Trovato e chiuso un guasto vero: spuntare
  «remotizzato» su un APP con vIPI la rendeva irraggiungibile (descrittore → aeroporto con ICAO vuoto). Ponte
  d'avvio `LinkAppUnitsAsync` (copia 29-set: 18 enti), tutti i punti «è una vIPI APP» dall'ente, derivazione dalla
  posizione principale, vista live per posizione dell'ente, `?app=` posizione → codice, rinomina IVAO che non
  riscrive più la chiave APP, pannello «Ente» nell'editor. Scelta: la pagina APP non chiude più quando una
  posizione sparisce (si nasconde il documento). Migrazione `EntiAtc` (SQLite+MySQL, additiva). Provato a schermo
  sulla copia del 29-set travasata in SQLite (la guardia vieta l'identità dev su MySQL). Carta
  `docs/feature/2026-09-29-enti-atc.md` (fasi 2 e 3). Test +6 (Infrastructure 1997, Application 3098), 10 file di test
  portati al modello nuovo. Codice comune `Vipi.Application`, `Vipi.Domain`.
- ✅ **S50** enti ATC, fase 2: «Remotizza» (29-set, committente: «gli app remotizzati si spostano nella vIPI di ACC e
  lì rimangono»; ramo `fix/enti-atc`). Riquadro «Ente» → «Sposta nella vIPI dell'ACC»: l'albero intero della vIPI APP
  (sezioni, flag, contenuti) si copia sotto un gruppo APP nuovo nella bozza della vIPI ACC
  (`IEditingRepository.CopyVersionIntoBlockAsync`), il blockmeta prende membri = posizioni dell'ente, ordine e
  collegamenti delle frequenze, `UnitId`; l'ente passa a `InAccVipi`, la vIPI APP esce dall'unione, si nasconde e
  restituisce il lock. Lock della vIPI ACC preso per il gesto (rifiuta se è di un altro). Dopo: `?app=` ed editor APP
  portano alla vIPI ACC, vista live sul gruppo (anche da una torre), elenco APP e documenti collegati la trattano da
  remotizzata. Niente migrazione (`Mode` c'era; blockmeta in JSON). Provato a schermo su Palermo con la copia del
  29-set: 15/15 sezioni e 18/18 blocchi identici, vista live «Palermo Radar» dopo la pubblicazione della vIPI di Roma
  (nella copia la vIPI di Roma è nascosta, come in produzione). Test +4 (Infrastructure 2000, Application 3099).
- ✅ **S51** enti ATC, fase 3: pulizia (29-set, committente; ramo `fix/enti-atc`). La derivazione della vIPI APP parte
  da **tutte** le posizioni dell'ente (`AppDocumentIdentity.Posizioni`), non dalla sola principale: dominio =
  unione dei domini, antenati posizione per posizione, ★ su ogni posizione dell'ente, scalo di ogni posizione;
  vale per frequenze, coordinamenti, AoR, configurazioni e minime. Via le ultime letture degli APP dal settore:
  `ScopeOf`, «Nuovo documento» (solo l'ente dice «ha già un documento»), e `CreateDocumentAsync` che rifiuta un APP
  non remotizzato nello scope. Restano di proposito il ponte, gli orfani (vIPI ACC) e il vSOP militare. Test +3
  (Infrastructure 2003), tutti ROSSI sul codice di prima; uno portato alla regola nuova (sceglieva `LIRP_APP` come
  «primo settore libero»). Niente migrazione. Codice comune `Vipi.Application`. Opzionale non fatto: vIPI ACC
  legata all'ACC. Carta `docs/feature/2026-09-29-enti-atc.md` §5.
- ✅ **S52** revisione delle fasi 1–3 degli enti ATC (29-set, committente: «rivedi il lavoro… fai finta di non averlo
  scritto tu»; ramo `fix/enti-atc`). Tre revisori indipendenti, rilievi verificati sul codice: due gravi, otto medi,
  una decina lievi. Scelte del committente: **A** «Sposta» in due tempi (copia nella bozza ACC, la vIPI APP resta
  pubblica e si nasconde da sola quando la vIPI ACC col gruppo va in vigore: `ConcludiSpostamentiAsync` dopo la
  pubblicazione e nel giro delle release); **B** un ACC con enti che hanno una vIPI APP non si elimina (frase), gli
  enti vuoti se ne vanno con lui. Gravi: ordine dei ponti d'avvio (una vIPI APP diventava vIPI dello scalo),
  «Sposta» senza transazione (due gruppi riprovando). Medi: codice come posizione altrui, lock nel riquadro «Ente»,
  pagina dell'ACC, segnalazioni di un ente spostato, vIPI APP nascosta fuori da Gestione documenti, scelte salvate
  per nominativo (avviso), rifiuti tardivi. `EfUnitOfWork` ripulisce il tracker al rollback. Test +19 (Application
  3102, Infrastructure 2014, Ui 1875), una guardia dei nomi aggiornata (`WhereCitedAsync` è una lettura); i test
  rossi sul codice di prima dove compilavano contro di esso (ponti, codice-posizione, ACC con enti, lock), gli altri
  usano API nuove. Niente migrazione. Codice comune `Vipi.Application`, `Vipi.Hosting`. Carta §6.
- ▶ Alla ripresa: `git merge main` (il ramo resta indietro dopo ogni fusione dell'integratore). Guardare `da-fare.md` e i lotti di S9.
- Conteggi del filone: di solito `tests/conteggi/Vipi.Ui.Tests.txt`.
