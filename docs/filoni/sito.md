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
- ▶ Alla ripresa: `git merge main` (il ramo resta indietro dopo ogni fusione dell'integratore). Guardare `da-fare.md` e i lotti di S9.
- Conteggi del filone: di solito `tests/conteggi/Vipi.Ui.Tests.txt`.
