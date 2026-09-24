# Filone sito vIPI — stato

> Scrive **solo** l'agente del sito (cartella `vIPI-sito`, ramo `sito/lavori`). Regole:
> [`come-si-lavora-in-parallelo.md`](come-si-lavora-in-parallelo.md). Numerazione del filone: **S1, S2…**
> (le voci §A in `docs/lavori-aperti.md` le scrive l'integratore alla consegna).

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
- ▶ Alla ripresa: `git merge main` (il ramo resta indietro dopo ogni fusione dell'integratore). Nessun lavoro aperto noto nel filone; guardare `da-fare.md`.
- Conteggi del filone: di solito `tests/conteggi/Vipi.Ui.Tests.txt`.
