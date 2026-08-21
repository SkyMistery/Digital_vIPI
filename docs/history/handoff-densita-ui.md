# Handoff — il ramo della densità UI (aggiornato 22 agosto 2026: la ricognizione è CHIUSA)

> **A cosa serve.** Ripartire a freddo sul ramo `ui-trasferimenti-densita` senza rileggere la cronologia.
> Chi deve fare **la prossima pagina** legge solo questo file più
> [`docs/design/regole-ui-pagine-admin.md`](../design/regole-ui-pagine-admin.md).

## Dove siamo

Ramo **`ui-trasferimenti-densita`**, allineato col remoto, **non fuso in `main`**.
Cancello a ogni commit: `dotnet build Vipi.slnx -c Release --no-incremental` (**0 avvisi**, gli avvisi sono
errori e `dotnet test` non li vede) + `dotnet test Vipi.slnx` — **entrambi i TFM**, net8 e net10.

Il giro riscrive **la forma** delle pagine di lavoro admin: niente cambia nel modello, nelle rotte o nei dati.
Il perno è che **ogni fascia tolta in testa diventa contenuto visibile**.

## Le regole sono già scritte — leggerle PRIMA di toccare una pagina

[`docs/design/regole-ui-pagine-admin.md`](../design/regole-ui-pagine-admin.md): **192 voci in 27 gruppi**, ognuna
già costata un giro di correzioni, più la **ricognizione misurata** (§15) di tutte le pagine con cosa manca a
ognuna e in che ordine conviene farle. Non è un regolamento di stile: è l'elenco di ciò che, saltato, si ripaga.

Il §«Dove sta la roba» in coda dice quale classe/funzione usare per ogni pezzo: il pacchetto tecnico
(`.st-head`, `.st-msg`, `.res-table.sticky-head`, `.st-pane`/`.st-scroll`, `.struct-bar`, `.sh-chip`,
`.conf-layout`, e in `vipi-ui.js` `vipiFitViewport` / `vipiStickyOffset` / `rootZoom` / `placeHelpPop`) **c'è
già e si riusa**, non si riscrive.

## Diciassette pagine chiuse: TUTTE quelle della ricognizione

| Pagina | Rotta | Prima → dopo | Carta |
|---|---|---|---|
| Accordi | `/vsop/admin/trasferimenti` | → 900 | `2026-08-19-accordi-densita-ui.md` |
| Struttura | `/vsop/admin/sectorstructure` | → 900 | `2026-08-19-struttura-densita-ui.md` |
| ACC | `/vsop/admin/acc` | 8 714 (testata appiccicata) | `2026-08-19-acc-admin-densita-ui.md` |
| Aeroporti | `/vsop/admin/airports` | 13 745 → 900 | `2026-08-19-aeroporti-densita-ui.md` |
| Editor aeroporto | `/vsop/{acc}/airports/editor` | 31 286 → 4 913 (LIRF) | `2026-08-20-editor-aeroporto-densita-ui.md` |
| Editor ACC | `/vsop/{acc}/editor` | 9 690 → 5 595 **in modifica** | `2026-08-20-editor-acc-densita-ui.md` |
| **Confinanti (vLOA)** | `/vsop/admin/confinanti` | **2 515 → 900** | `2026-08-20-confinanti-densita-ui.md` |
| **Versioni** | `/vsop/versioni` | **1 664 → 900** (+ lock e azioni) | `2026-08-21-versioni-lock-e-azioni.md`, `2026-08-21-versioni-densita-ui.md` |
| **Permessi** | `/vsop/admin/permessi` | **2 449 → 900** | `2026-08-22-permessi-densita-ui.md` |
| **Audit** | `/vsop/admin/audit` | **13 293 → 900** (+ cosa registra) | `2026-08-22-audit-cosa-registra.md`, `2026-08-22-audit-densita-ui.md` |
| **Sorgenti** | `/vsop/admin/sorgenti` | **1 252 → 900** (+ cosa fa la policy) | `2026-08-22-sorgenti-cosa-fa-la-policy.md`, `2026-08-22-sorgenti-densita-ui.md` |
| **Diagnostica** | `/vsop/admin/diagnostica` | **1 349 → 900** (+ cosa afferma) | `2026-08-22-diagnostica-cosa-afferma.md`, `2026-08-22-diagnostica-densita-ui.md` |
| **Nuovo documento** | `/vsop/editor/newdoc` | **957 → 900** (+ cosa crea) | `2026-08-22-newdoc-cosa-crea.md`, `2026-08-22-newdoc-densita-ui.md` |
| **Incarichi admin** | `/vsop/admin/tasks` | **1 813 / 4 764 → 900** (+ cosa sono) | `2026-08-22-incarichi-cosa-sono.md`, `2026-08-22-incarichi-densita-ui.md` |
| **Incarichi utente** | `/vsop/tasks` | **1 562 → 900** | (le stesse due carte) |
| **Editor APP** | `/vsop/{acc}/apps/editor` | **3 540 → 3 350**, e **1 654** compresso | `2026-08-22-editori-app-vloa-cosa-fanno.md`, `2026-08-22-editori-app-vloa-densita-ui.md` |
| **Editor vLOA** | `/vsop/{acc}/vloa/editor` | **4 351 → 4 242**, e **1 359** compresso | (le stesse due carte) |

Le carte stanno in `docs/feature/`.

## Il metodo, in sei righe

1. **Carta prima del codice** ([FEATURE-PROCESS](../FEATURE-PROCESS.md)), una slice per commit.
2. **Misurare la pagina COME SI USA**: in modifica se è un editor, aperta se ha un dettaglio che si apre.
   L'editor ACC pesava 6 466 in lettura e 9 690 in modifica; Confinanti 2 515 chiusa e molto peggio aperta.
3. **Misurare batte stimare, sempre** — e le larghezze di colonna si misurano col **font calcolato** sui
   **valori veri del DB**, non a occhio e non su esempi.
4. **Guidare la pagina** (skill `verifica-live`) a 1600/1440/1280/1024, **IT ed EN**, zoom 0.8→1.5.
5. **Guardare** gli screenshot, non solo produrli: metà dei difetti di questi giri non aveva un'asserzione che
   li cercasse, e il peggiore di tutti (`.sector-pick` che significava due cose) l'ha visto un umano.
6. Chiudere il giro aggiornando **carta + regole + ricognizione §15 + memoria**.

⚠️ Le mie misure (altezze, sfori orizzontali) **non vedono un elemento assoluto che copre il contenuto**, e non
vedono i posti dove *manca* qualcosa. Per quelli servono gli occhi.

## Versioni: chiusa, in due giri

**Sostanza** (carta [`2026-08-21-versioni-lock-e-azioni.md`](../feature/2026-08-21-versioni-lock-e-azioni.md),
regole 95-105): si poteva **eliminare un documento che un'altra persona stava editando**, «nascondi» non
chiedeva niente, «elimina» chiedeva **due volte**. Chiusi: badge «chi ci sta lavorando · fino a che ora»,
hide/delete inibiti **nel service**, force-unlock admin, conferme in linea, chip «in modifica» e per ACC,
tasto «Aggiorna», permessi del markup allineati al grant ACC, e il lock riletto al clic
(`InlineConfirm.CanOpenAsync`).

**Densità** (carta [`2026-08-21-versioni-densita-ui.md`](../feature/2026-08-21-versioni-densita-ui.md),
regole 106-116): **1 664 → 900px**, cioè il viewport — la pagina non scorre più, a nessun assetto né zoom.
Il dettaglio è uscito dall'elenco e sta nel pannello a destra (`.ver-layout`, altezza misurata); i chip dei
filtri **contano** e hanno mangiato la fascia di riepilogo; le azioni sono salite dalla riga al pannello
(riga 118 → 63px); «Espandi tutti» è sparito con il dettaglio in linea; la prosa è nei «?» e la Guida ha la
sua sezione `#versioni`.

⚠️ **Buco dichiarato e non chiuso** (viene dal primo giro): `AeroportoEditorPage` usa
`IAirportEditingService`, **non** `IEditingService`, quindi non prende il lock del documento — sugli aeroporti
il badge non comparirà mai e hide/delete non saranno mai inibiti. Portare l'aeroporto sul lock è un giro suo.

⚠️ Il lock del `Document` dura **30 minuti senza heartbeat** (`EditResourceLock` invece: 3 min + battito):
si rinnova al salvataggio e si libera con «Fine modifica». È la ragione per cui il force-unlock non è un lusso.

## Permessi: chiusa il 22 agosto

Carta [`2026-08-22-permessi-densita-ui.md`](../feature/2026-08-22-permessi-densita-ui.md), regole 117-124.
⚠️ **La ricognizione la dava a 1 346px perché la tabella dei permessi era vuota**: col DB di sviluppo non
c'è nessun grant. Con 16 grant scritti nella copia erano **2 449** (2 623 in inglese) — prima della lista,
non ultima. Da lì a **900**: barra admin (undici voci, componente `AdminNav`) al posto delle sei card da
485px, una riga per **persona** coi chip degli ACC, concessione e revoca nel pannello di destra.

## La barra admin su tutte e undici: chiuso il 22 agosto

Non è il giro di una pagina, è la **testa di tutte**. Regole 125-132 e §21; nessuna carta a sé, il perché sta
nei commenti dei file e nelle regole. Tre commit: `e22929e` (barra ovunque + filtro), `31e253e` (via
l'etichetta «Admin:»), `5e1abca` (via «Nuovo doc» da Struttura), `73cd6c6` (barra sopra il titolo, via le
briciole).

**Cosa c'è adesso.** `AdminNav` sta **sopra il titolo** di tutte e undici le pagine admin, al posto della
briciola di pane. Ogni voce si porta dietro la **propria regola d'accesso** (`Chi.Admin` / `Chi.Chiunque`) e
il filtro lo fa il componente: le pagine scrivono `<AdminNav />` **nuda**, senza `@if`. Se all'utente resta
una voce sola — quella della pagina in cui è già — la barra non si rende affatto.

**Perché così, in tre righe che valgono per il prossimo giro.**
1. La regola 120 («niente elenchi di porte chiuse») **non si difende ripetendo il cancello**: si difende
   mettendolo una volta accanto alla voce. Undici `@if` copiati sono undici posti dove sbagliarsi.
2. La barra **non interroga la banca dati**: girerebbe su undici pagine mentre ognuna carica i propri dati,
   sullo stesso `DbContext` di circuito — la ricetta esatta del «second operation on this context».
3. **Sopra il titolo**, perché un titolo deve toccare il contenuto che intitola; e al posto della briciola,
   perché la briciola faceva già quel lavoro peggio (portava dove porta la barra, e inventava una gerarchia:
   Aeroporti sotto Struttura non ci sta, sono pagine sorelle).

⚠️ Le briciole delle **pagine pubbliche restano**: lì non c'è barra, e sono l'unico modo di risalire.
⚠️ `/vsop/editor/newdoc` e `/vsop/tasks` **non hanno la barra** e hanno ancora la briciola: la prima non è in
`AdminNav.Voci` (ci si arriva da Documenti), la seconda è una pagina d'utente. Per newdoc la decisione è
aperta ed è parte del suo giro.

## Audit: chiusa il 22 agosto, in due giri

**Sostanza** (carta [`2026-08-22-audit-cosa-registra.md`](../feature/2026-08-22-audit-cosa-registra.md),
regole 133-142 insieme alla densità). Aprendo la pagina per il `thead` appiccicato è venuto fuori che in
tutto il codice l'audit si scriveva in **quattro punti**, e che quindi:

- ⚠️ **eliminare un documento non lasciava traccia** (né nasconderlo), che è l'atto meno reversibile
  dell'applicazione ed è in mano ad admin **e** responsabili dell'ACC dal 21 agosto;
- ⚠️ **la revoca di un permesso registrava l'attore sbagliato**: scriveva chi aveva *concesso*;
- il **force-unlock** (documenti e risorse) non era tracciato, ed è esposto in UI dal 21 agosto;
- `AuditAction.HierarchyChange` era un valore d'enum che **nessuno scriveva**, mentre il sottotitolo
  prometteva «pubblicazioni, permessi, **struttura**».

Ora tutti e cinque scrivono, con l'attore giusto e **il nome accanto all'Id** (un registro deve restare vero
quando l'entità di cui parla non esiste più). Un solo punto di scrittura, `AuditScribe`, con encoder JSON
rilassato — con quello di serie «vIPI — Roma ACC» finiva nel DB come `vIPI \u2014 Roma ACC`, e il registro
lo si legge anche in SQL. Fuori, dichiarati: gli **import** e i **salvataggi** di contenuto.

**Densità** (carta [`2026-08-22-audit-densita-ui.md`](../feature/2026-08-22-audit-densita-ui.md)):
**13 293 → 900**, e resta 900 con 248 righe, con 500, a 1600/1440/1280/1024, IT ed EN, zoom 0.8→1.5.

⚠️ **I 13 293 sono la vera lezione del giro**: la ricognizione diceva 1 166, poi 1 556. Erano fotografie di
una tabella con 20 e 28 righe in un DB di sviluppo quasi vuoto — ed erano numeri **col tetto**, perché il
lettore tagliava a 200 righe in silenzio. Un registro cresce **per sempre**: la misura si rifà, non si cita.

⚠️ **Il bersaglio dice il titolo, e ha tre fonti in ordine**: il titolo **scritto nella riga** (quello che
il documento aveva al momento dell'atto, e per un documento eliminato l'unico rimasto), poi una mappa
Id→titolo letta in **una query per pagina** per le righe vecchie che portano solo l'Id, poi l'Id nudo. Su una
riga di pubblicazione l'`EntityId` è la **versione**, non il documento: cercare la mappa con quello darebbe
il titolo di un **altro** documento, in silenzio e in modo plausibile.

Cosa insegna, oltre a questo (regole 133-142): il dato grezzo non si butta e non si mette in colonna (il JSON
sta nel `title`); il vocabolario vecchio si **legge**, non si riscrive (`Archive` e `Delete` per la stessa
revoca dicono la stessa frase); il non-evento non si scrive; **un formattatore per tipo di dato, non uno per
pagina** (`AuditNarrator` è condiviso con la storia di Versioni, dove ha ucciso un parser che leggeva chiavi
che nessuno scrive); **elenco+dettaglio si giustifica con l'azione**, e qui non c'era azione da fare.

## Sorgenti: chiusa il 22 agosto, in due giri

**Sostanza** (carta [`2026-08-22-sorgenti-cosa-fa-la-policy.md`](../feature/2026-08-22-sorgenti-cosa-fa-la-policy.md),
regole 143-152 insieme alla densità). Aperta per la densità, e come su Versioni e Audit è saltato fuori che la
pagina **prometteva una cosa che il codice non faceva**: «escludi una categoria e l'import non la tocca più»
era vero per SID e Aree, **falso per Settori, Transition Altitude e Piste**.

- ⚠️ Il gate dei **Settori** non c'era in **nessuno** dei quattro import (job 24h, bottone dell'editor
  aeroporto, massivo di `/vsop/admin/airports`, «Genera documenti»): escludere la categoria permetteva di
  aggiungere settori a mano e poi il giro notturno ci ripassava sopra.
- ⚠️ «Genera documenti» **scavalcava TA e Piste**: stessa `MergeFromSourceAsync` del reimport, ma senza
  leggere la policy — TA scritta a mano sovrascritta, misure delle piste riportate dalla sorgente, piste
  tolte a mano che rientravano, TL di fascia ricalcolati.
- Il cambio di policy non lasciava traccia: era l'ultimo atto amministrativo **muto** dopo il giro Audit.
- `UpdatedUtc`/`UpdatedByUserId` esistevano dal primo giorno e non li leggeva nessuno. ⚠️ Contano davvero:
  `ImportSids` è nato `false` su un DB già popolato (migration `AddSidImport`, luglio 2026), e dal valore non
  si distingue una scelta dell'admin dall'effetto della migrazione. Ora `UpdatedByUserId = 0` lo **dichiara**.
- ⚠️ La tabella degli stati **regalava il verde**: `GatedImportLoop` marca il successo anche quando il run
  esce subito perché la categoria è esclusa. E ospitava `SpecialAreaForeignOptOut` e
  `TransferFlowsToAgreements`, che import non sono, mentre mancava l'anagrafica **ACC**, che si importa ogni
  giorno.

**Densità** (carta [`2026-08-22-sorgenti-densita-ui.md`](../feature/2026-08-22-sorgenti-densita-ui.md)):
**1 252 → 900**, a 1600/1440/1280/1024, IT ed EN, zoom 0.8→1.5. Le **due** tabelle diventano **una** (sopra
diceva «Settori», sotto `AirportSector`; sopra «da sorgente / manuale», sotto «ok / errore»), la spunta e la
colonna «Provenienza» erano la stessa informazione scritta due volte, e «importa dalla sorgente» stava scritto
cinque volte, una per riga.

⚠️ **La lezione riusabile del giro è `max-height` contro `height`.** `vipiFitViewport` scrive `height` ed è
giusto dove il contenuto è più alto dello schermo **per mestiere** (Audit, Aeroporti). Qui il contenuto è
corto e **fisso**: stirato, il riquadro lasciava **mezzo pannello di bianco**; non misurato affatto, a
1024×768 e da zoom 1.25 la pagina tornava a scorrere. Da qui **`vipiCapViewport`** in `vipi-ui.js`. «La
pagina non scorre» non è l'obiettivo: l'obiettivo è che **ciò che si guarda stia a schermo**.

⚠️ E metà dei difetti li ha visti **l'occhio, non le misure**: due tasti «Annulla» affiancati che fanno cose
diverse (l'uno del componente, l'altro della pagina); le sei caselle **non incolonnate** perché
`.se-row input{flex:1}` è la regola dei campi di testo e si applicava anche alle checkbox; i link «Dove si
modifica» che a `display:block` sembravano campi; un errore di rete lungo quattro righe che faceva la riga
SID alta il doppio; e `e'`/`piu'` al posto di `è`/`più` in tre stringhe nuove.

## Diagnostica: chiusa il 22 agosto, in due giri

**Sostanza** (carta [`2026-08-22-diagnostica-cosa-afferma.md`](../feature/2026-08-22-diagnostica-cosa-afferma.md),
regole 153-162 insieme alla densità). La regola lasciata dal giro Sorgenti — *prima di renderla bella,
verificare che dica il vero* — ha pagato subito, e ha trovato il difetto peggiore del ramo:

- ⚠️ **la pagina che diagnostica i guasti moriva se ne aveva uno.** Le cinque parti del report giravano in
  fila senza protezione e `OnInitializedAsync` chiamava `RunAsync` senza `try/catch`. Peggio del circuito
  morto: il guasto di **una** sonda cancellava il lavoro di **tutte** le altre — un problema del server di
  database nascondeva una pista orfana già trovata. Era la lezione di `StartupMaintenanceReport`, che sta
  nella stessa cartella e che quel servizio **consuma**, non applicata a sé;
- ⚠️ il sottotitolo prometteva **meno** di quello che la tabella mostrava («soft-ref», mentre ospita anche
  schema, server, avvio e «nessuno può editare»): è Audit al contrario;
- ⚠️ in pagina inglese i rilievi erano **in italiano** — è l'unica pagina admin il cui contenuto è prosa
  scritta dall'applicazione invece che dati;
- otto rilievi e **zero link**: la riga diceva «Clausola #1 (LIBB, punti Y01-Y12)» e la si andava a cercare
  a mano, benché chi produce il rilievo sappia dove si ripara;
- nessun «Aggiorna»: una fotografia da ~1,3 s che per rifarsi voleva un ricaricamento della pagina.

**Densità** (carta [`2026-08-22-diagnostica-densita-ui.md`](../feature/2026-08-22-diagnostica-densita-ui.md)):
**1 349 → 900**, e resta 900 **con 76 rilievi**, a 1600/1440/1280/1024, IT ed EN, zoom 0.8→1.5. Il `thead`
resta fermo dopo 4 024px di scorrimento interno. Tre domande diverse in due colonne; `.wrap` a larghezza
piena (a 1 100px la barra admin andava su **due righe**); chip che contano per **area** e non per categoria.

⚠️ **La ricognizione diceva 900 perché il report era VUOTO**: nel DB di sviluppo nessun soft-ref è rotto.
Terza volta nel giro, dopo Permessi (1 346 a tabella vuota → 2 449) e Audit (1 556 con 28 righe → 13 293) —
ma qui il numero non era *vecchio*: era la misura di una pagina che **non aveva niente da dire**.

⚠️ E la coppia `height`/`max-height` si è chiusa: su Sorgenti serviva `vipiCapViewport` perché il contenuto
è corto e fisso, qui serve `vipiFitViewport` perché cresce senza tetto. **Quale delle due dipende da cosa
c'è dentro**, e la differenza si vede a occhio prima che nei numeri.

⚠️ Un difetto lo ha trovato la verifica live e non i test: la localizzazione era **a metà**. Tradotti
categoria e dettaglio, il **bersaglio** restava italiano — «severe | Broken hierarchy | *Settore ACC*
LGGG_W_CTR». Metà dei bersagli non è un identificatore ma una frase.

## Nuovo documento: chiusa il 22 agosto, in due giri

**Sostanza** (carta [`2026-08-22-newdoc-cosa-crea.md`](../feature/2026-08-22-newdoc-cosa-crea.md), regole
163-170 insieme alla densità). Quinta pagina di fila con un difetto di sostanza sotto la densità, e qui il
difetto era **nel nome**: si chiama «Nuovo documento» e per **tre tipi su quattro non crea niente** — apre
l'editor, che crea *se serve*. Il quarto, la vLOA, creava davvero. E creava male:

- ⚠️ **da qui si creavano vLOA duplicate**, mentre la generazione da «ACC confinanti» è idempotente per
  parti. Il contratto dichiarava «una sola vLOA per coppia ACC↔ACC» dal primo giorno e nessuno lo imponeva.
  E il resto dell'applicazione **non sa gestirne due**: la ricerca per coppia fa `FirstOrDefault`, quindi
  l'editor ne apriva una senza un criterio e l'altra restava invisibile — pur potendo avere release
  pubblicate;
- ⚠️ **la vLOA nasceva fuori catalogo**: una sezione a chiave *libera* invece delle sette del profilo, mentre
  dall'altra porta nasceva con le canoniche. La pagina lo **dichiarava** — «la vLOA nasce vuota» — e un
  difetto documentato resta un difetto;
- ⚠️ **la porta era più stretta della serratura**: pagina dietro `IsAdmin`, servizi autorizzati per **grant
  di ACC**. Il responsabile di un ACC non vedeva la pagina ma poteva creare andando all'URL dell'editor
  (regola 95, in un altro punto);
- il tasto diceva «Crea» anche quando apriva.

**Densità** (carta [`2026-08-22-newdoc-densita-ui.md`](../feature/2026-08-22-newdoc-densita-ui.md)):
**957 → 900**, e 900 su **tutte e quattro le schede**, a 1600/1440/1280/1024, IT ed EN, zoom 0.8→1.5.

⚠️ **Su una pagina a schede si misura OGNI scheda**: «la pagina» è quella che si apre per prima, e non è
detto che sia quella che pesa (qui 957 la vLOA, 900 le altre tre). E si misura in **due stati**: vuota e con
un bersaglio scelto, perché è allora che compaiono le tendine dipendenti.

⚠️ **Il difetto peggiore non era un'altezza**: il tasto che conclude stava **prima** di quattro dei cinque
campi che gli servono. Si leggeva come se «titolo + Crea» bastasse, e chi lo premeva otteneva un errore che
la pagina causava con la propria disposizione. Nessuna misura lo trova.

⚠️ E **la coppia `height`/`max-height` si affina ancora**: avevo deciso «niente riquadro misurato, il
contenuto è corto e fisso» — vero a zoom 1, **falso a 1.25**, dove la pagina tornava a scorrere. «Corto e
fisso» non vuol dire «non misurare»: vuol dire **`max-height`**.

**Decisione chiusa, lasciata aperta dalla ricognizione**: la barra admin **sì**, la voce nell'elenco **no**.
Sono due cose diverse — `AdminNav` è già a undici voci e sotto i 1 200px va su due righe, e «Nuovo documento»
non è una destinazione che si cerca. ⚠️ Regola generale: **la barra mostra dove si può andare, non dove si
è**; la voce accesa non è un requisito per renderla. E per un utente con un solo grant la barra **non si
rende affatto** (il componente si nasconde con una voce sola): tolta la briciola, la risalita è il tasto
«Bozze & versioni» in testata — verificato, perché togliere una briciola in una pagina senza barra sarebbe
stato lasciarla senza uscita.

## Incarichi: chiuse il 22 agosto, due pagine in un giro

**Sostanza** (carta [`2026-08-22-incarichi-cosa-sono.md`](../feature/2026-08-22-incarichi-cosa-sono.md),
regole 171-182 insieme alla densità). Sesta pagina di fila con un difetto di sostanza sotto la densità, e qui
erano **dodici** — dieci trovati leggendo, **due guardando**, e il secondo dei due è il peggiore:

- ⚠️ **si creavano incarichi assegnati a NESSUNO**: «Seleziona» valeva `0` e nessuno validava. Quell'incarico
  non compare negli incarichi di nessuno, si vede solo nell'elenco admin, e non era nemmeno riassegnabile;
- ⚠️ **`AssignAsync` non aveva un solo chiamante** — né UI né test: un incarico dato alla persona sbagliata si
  poteva solo cancellare e rifare;
- ⚠️ **«Apri documento» non apriva il documento per 3 tipi su 4**: `TaskDocLink` sapeva comporre l'URL solo
  per la vIPI ACC, per gli altri rimandava a `/vsop/versioni` — un tasto che dice «apri» e mostra un elenco;
- ⚠️ **l'elenco si riordinava sotto le mani**: ordinato per `UpdatedUtc`, che il cambio di stato riscrive;
- ⚠️ **l'audit non registrava niente** degli incarichi — gli ultimi atti amministrativi muti dopo Sorgenti;
- **gli errori del service erano in italiano fisso**, e la voce «Incarichi» in topbar era l'unica scritta a
  mano non tradotta;
- ⚠️ **zero test**: in tutta la suite `EditorTask` compariva una volta, per la lunghezza delle colonne.

**Decisione del committente**: l'audit registra **tutto** (creazione, stato, riassegnazione, eliminazione);
niente archiviazione, la crescita si chiude col **filtro «non conclusi»** — una colonna nuova sarebbe una
migrazione, e il deploy è già fermo sul cutover MariaDB.

**Densità** (carta [`2026-08-22-incarichi-densita-ui.md`](../feature/2026-08-22-incarichi-densita-ui.md)):
**1 813 (12 incarichi) e 4 764 (60) → il viewport**, e **1 562 → il viewport** sulla pagina utente. A
1600/1440/1280/1024, IT ed EN, zoom 0.8→1.25.

⚠️ **I due difetti peggiori li ha trovati la verifica live, non la lettura.**

1. Il sottotitolo della pagina utente diceva «**trascina lo stato** per aggiornare l'avanzamento»: il
   trascinamento non è mai esistito. Sesta pagina del ramo con prosa che promette ciò che il codice non fa.
2. ⚠️ **La tendina fabbricava la chiave del documento** — `"{acc}|"` per la vIPI ACC — mentre la chiave vera è
   `{acc}|{callsign primario}` (verificato sui dati: `LIBB|LIBB_ES_CTR`). L'incarico nasceva puntando a un
   documento **inesistente**. E non si vedeva perché **un secondo difetto lo copriva**: finché il link si
   componeva spezzando la stringa, funzionava per caso. **Riparare una cosa ne scopre un'altra** — e la
   seconda non poteva stare nella carta iniziale.

⚠️ **Due trappole di misura, entrambe già scritte e non applicate qui.** `vipiFitViewport` non vede cosa sta
**sotto** il riquadro: sotto il tabellone ci sono le colonne chiuse (da qui il terzo argomento `reserveSel`,
facoltativo) e sotto ancora i **70px** del `.wrap` — **52px**, gli stessi identici di Audit. E una **griglia**
col tetto vuole `grid-template-rows:minmax(0,1fr)`, o la riga implicita si dimensiona sul contenuto e ignora
l'altezza misurata: è la regola del `min-width:0` sull'asse verticale.

⚠️ **E prima di dire «l'ho rotto io», misurare le gemelle**: a zoom 1.5 sotto i 1 440px queste pagine
scorrono, e lo fanno **Permessi e Audit con gli stessi identici numeri** (1 196 e 1 148). È il pavimento
condiviso, non un difetto del giro.

### Cosa lascia in giro, per chi viene dopo

- **`ValidationException` porta ora una chiave** accanto al messaggio grezzo, e `ServiceErrorNarrator` la
  traduce — terzo narratore della famiglia. È **facoltativa**: gli altri service la prendono quando qualcuno
  li tocca. Non è un cantiere aperto, è un posto pronto.
- ⚠️ **L'audit ha una famiglia nuova, ed è la più prolifica che abbia mai avuto** (quattro stati per
  incarico): la misura di `/vsop/admin/audit` **va rifatta** con gli incarichi dentro.
- **L'archiviazione degli incarichi resta da fare**, dopo il cutover MariaDB: oggi la crescita è tenuta dal
  filtro, che basta ma non toglie righe dal database.

## Editor APP e vLOA: chiusi il 22 agosto — e con loro la ricognizione

Le **ultime due**, e le uniche che non avevano mai avuto un giro: montano lo stesso `DocumentSectionsEditor`
degli altri editor, ma sopra erano rimaste a due generazioni fa. Il lavoro non è stato inventare una forma, è
stato **portarle su quella già pagata** dall'editor ACC.

⚠️ **Il loro «900» era una stima, e lo diceva la ricognizione stessa.** Misurati su documenti veri e **in
modifica** — che è come si usa un editor — erano **3 540** (APP, `LIBP_APP`, 11 sezioni) e **4 351** (vLOA,
`LIBB ↔ LGGG`). Un numero dichiarato «non verificato» dentro una tabella di misure **si legge come una
misura**: meglio una casella vuota.

**Sostanza** (carta [`2026-08-22-editori-app-vloa-cosa-fanno.md`](../feature/2026-08-22-editori-app-vloa-cosa-fanno.md),
regole 183-192 insieme alla densità):

- ⚠️ **due file di chip identiche che significano cose opposte.** Nella sezione AoR della vLOA, a sessanta
  pixel di distanza e con la **stessa classe `.aor-chip`**: quelli sopra **scrivono nel documento**, quelli
  sotto accendono un poligono finché sei lì. È il difetto `.sector-pick` nella forma peggiore — i due gesti
  **adiacenti**, e uno solo distruttivo. Ora chi scrive è un **elenco con caselle**, che dice anche quanti
  settori sono dentro;
- ⚠️ **il «?» dell'anteprima bozza era lo stesso paragrafo italiano cablato TRE volte** (APP, vLOA, ACC),
  `AriaLabel` compreso: in pagina inglese si leggeva in italiano. Ora chiavi IT+EN e **un** componente
  (`DraftPreviewLink`). Stessa famiglia: «Tutti/Nessuno» di `AccAor3d`, cablati mentre il gemello `AccAor` li
  localizzava;
- **nessun «?» di pagina** su nessuna delle due, e le loro sezioni di Guida descrivevano il **viewer**.

**Densità** (carta [`2026-08-22-editori-app-vloa-densita-ui.md`](../feature/2026-08-22-editori-app-vloa-densita-ui.md)):

| Editor | Lettura | In modifica | **Compresso** |
|---|---:|---:|---:|
| APP | 2 403 | **3 350** | **1 654** |
| vLOA | 3 333 | **4 242** | **1 359** |
| ACC (già chiuso) | 3 988 | **5 144** (era 5 595) | 1 468 |

⚠️ **Su un editor il numero che conta è il COMPRESSO.** Un editor scorre per mestiere: la domanda non è
«quanto è alto tutto aperto» ma **quanto costa arrivare alla sezione che serve** — e su queste due il comando
«comprimi tutto» semplicemente **non c'era**, benché le sezioni fossero già `<details>` con persistenza.

⚠️ **Il «+ Blocco» sta nel componente CONDIVISO**, quindi ha restituito ~450px anche all'editor ACC senza
riaprirne il giro. Toccare un componente condiviso è una decisione che va **misurata su tutti i suoi host**:
qui erano quattro.

### Tre cose trovate guidando, che la carta non prevedeva

1. ⚠️ **Una sezione CHIUSA era alta 92px invece di ~50**: la riga-titolo andava a capo, e su dieci sezioni
   sono 900px di sole intestazioni — pagati **dopo** aver premuto «Comprimi tutto», cioè proprio quando si è
   chiesto di non vederle.
2. ⚠️ **A 1024 la testata andava a capo per NOVE pixel**, e il pezzo più largo era il **chip del lock**
   (266px). Il chip dice l'ora, la frase sta nel `title`: **stessa cura del giro Versioni** (647 → 289),
   applicata stavolta a tutti e tre gli editor invece che solo a quello in mano.
3. **«Bozza v2» compariva due volte**, in testata e nel rail: chi sposta uno stato deve **toglierlo** da dove
   stava, o non l'ha spostato.

### E due cose che hanno fermato me

- ⚠️ **La rete ha visto un doppione che io non avevo visto**: le ancore `#editor-app` e `#editor-vloa`
  esistevano già, e le mie erano un secondo blocco con lo stesso `id`
  (`GuideSearchTests.Catalog_anchors_are_unique` rosso). Sostituite, non affiancate.
- ⚠️ **Le voci di Guida vecchie erano DATATE**: «6 sezioni fisse» dove ne ho misurate undici. Il testo di un
  aiuto invecchia come il codice, ma in un posto dove nessuno passa: **chi tocca una pagina rilegge la sua
  voce di Guida**.

### E adesso?

La ricognizione (§15) **non ha più pagine da rifare**. Quello che resta non è di una pagina:

- la **topbar** che scorre in orizzontale sotto i 1 280px — ⚠️ rimisurato in questo giro: `div.right` arriva
  a **1 396px**, che è *esattamente* lo `scrollWidth` della pagina, **niente dentro il `.wrap` sfora**, e il
  numero è identico sulla home. È del chrome, va affrontato per sé;
- il **pannello release** (974px con 13 rilasci sull'editor ACC), che è roba del giro di `ReleasePanel`;
- e le due cose lasciate dai giri precedenti: l'**archiviazione degli incarichi** dopo il cutover MariaDB, e
  la misura di **`/vsop/admin/audit`** da rifare con la famiglia «Incarico» dentro.

## Aperto, e non è di queste pagine

- ⚠️ **La guardia «una coppia, una vLOA» non è atomica.** `CreateDocumentAsync` cerca la coppia e poi crea;
  non c'è un **indice unico** su `DocumentParty` per (Home, Neighbour). Due richieste in parallelo possono
  passare entrambe il controllo. In pratica lo copre il lock `newdoc` — che è la ragione per cui quel lock
  resta anche adesso che la guardia c'è — ma il lock vale solo per **quella porta**: una creazione che
  arrivi da un seed, da un test o da una pagina futura non lo prende. La difesa definitiva è l'indice unico,
  ed è una migrazione sullo schema del committente: ⚠️ prima va verificato **in produzione** che non
  esistano già coppie duplicate, altrimenti la migrazione fallisce all'avvio.

- ⚠️ La **topbar** fa scorrere la pagina in orizzontale a 1280/1024: `div.right` misura **1 396px** a
  1 024 (rimisurato il 22 agosto sugli editor; il 21 erano 1 385 dentro 1 280, il 20 erano 1 411) — e quel
  numero è **esattamente lo `scrollWidth`** della pagina, mentre **niente dentro il `.wrap` sfora**,
  verificato elencando gli elementi oltre il bordo su editor, home e vIPI ACC. È del chrome, non di una
  pagina: va affrontato per sé, ed è ormai **l'ultima cosa aperta che si vede a schermo**. È anche la ragione
  per cui lo sforo orizzontale, da solo, non è un segnale utile sulle singole pagine finché non è chiuso.
- ⚠️ `Vipi.AuroraBridge.Tests` ha **un test instabile**, ora identificato:
  `AuroraClientTests.Richieste_in_sequenza_non_si_mescolano`. Fallisce circa **una volta su tre** con
  «Nessuna risposta a #TRPOS entro 15000 ms» e passa da solo. Usa un `FakeAuroraServer` su socket di
  loopback con due richieste concorrenti serializzate dal client: cede quando la macchina è carica (suite in
  parallelo, app accesa). Non è del ramo densità — è roba del bridge Aurora — ma smettere di chiamarlo
  «instabile e basta» costa un `for` di quattro giri.
- Sull'editor ACC a blocchi chiusi il pezzo più alto è ormai il **pannello release** (974px con 13 rilasci):
  è roba del giro di `ReleasePanel`, non della densità.

## Ambiente di verifica

Skill di progetto `.claude/skills/verifica-live/` — copia del DB, `VipiAuth__Enabled=false`, Edge +
puppeteer-core, e si attende `window.Blazor`, **non** il DOM (la prima risposta è il prerender).

⚠️ Se l'app di sviluppo è già in esecuzione, i `bin/` sono **bloccati**: `dotnet publish` in una cartella dello
scratchpad e avviare **su un'altra porta** invece di uccidere l'istanza di chi sta lavorando. E fermare solo
la propria (`Get-Process Vipi.Host | Where-Object { $_.Path -like '*scratchpad*' }`).

⚠️ **`Vipi.Host` è `net8.0` soltanto**: `dotnet publish -f net10.0` fallisce con `NETSDK1005`. Si pubblica
senza `-f`. E **5034 è del committente, 5035 può essere già presa**: il 21 e il 22 agosto ha funzionato la
**5037**. Il sintomo della porta occupata è **uscita 82** con `Failed to bind to address` — e ⚠️ se non lo si
riconosce si finisce a *misurare l'istanza di qualcun altro*: il 22 agosto una tornata di misure è uscita
sballata perché sulla 5034 rispondeva un build vecchio, senza la barra. Il controllo che lo smaschera è una
sentinella nella pagina (`nav.admin-nav` assente ⇒ non è il mio build), non il codice HTTP.
La propria si ferma per **porta**, non per nome, così non si uccide quella di chi sta lavorando:

```powershell
$mia = (Get-NetTCPConnection -LocalPort 5037 -State Listen).OwningProcess
Stop-Process -Id $mia -Force
```
Un `dotnet test Vipi.slnx` a app accesa muore con `MSB3021` sui `bin/Debug`; `-c Release --no-build` gira lo
stesso, perché i `bin/Release` non sono bloccati.

⚠️ **Il browser headless parla la lingua del sistema**, non quella che credi: senza
`setExtraHTTPHeaders({'Accept-Language': 'it-IT,it;q=0.9'})` Edge chiede `en-US` e la prova «in italiano»
verifica l'inglese. È così che «No release» non tradotto è passato per un giro.

### Due trappole dell'attrezzo, non della pagina (22 agosto)

⚠️ **L'exe pubblicato va avviato DALLA SUA CARTELLA.** La content root e' la directory corrente: lanciando
`$sc\pub\Vipi.Host.exe` restando nel repo, l'app parte, risponde 200 e serve una pagina **senza CSS ne' JS**
(`_content/...` in 404, «MIME type "" is not a supported stylesheet»). La misura che ne esce non e' della
pagina, e' di una pagina nuda: 8 304px invece di 13 293. Prima di misurare, un `Set-Location "$sc\pub"`.

⚠️ **La riserva e la griglia.** Due cose che il giro Incarichi ha aggiunto all'attrezzo, e che valgono per
chiunque misuri un riquadro: `vipiFitViewport`/`vipiCapViewport` accettano un **terzo argomento**
(`reserveSel`, facoltativo) per togliere dallo spazio quello che sta **sotto** il riquadro — e se il riquadro
è una **griglia**, gli serve `grid-template-rows:minmax(0,1fr)`, o la riga si dimensiona sul contenuto e
l'altezza misurata non ha effetto.

⚠️ **Prima di dire «l'ho rotto io», misurare le pagine gemelle.** A zoom 1.5 sotto i 1 440px le pagine a due
pannelli scorrono **tutte** con gli stessi numeri (1 196 e 1 148): è il pavimento condiviso, non il giro in
corso. Un numero brutto su una pagina sola è un difetto; lo stesso numero su tre è una scelta di progetto.

⚠️ **Lo zoom si mette con `window.vipiSetZoom(z)`, non scrivendo `style.zoom`.** A mano non scatta il
`resize`, quindi `vipiFitViewport` non rimisura e il driver denuncia uno scorrimento che nella pagina vera non
c'e' (visto a 1.2 e 1.5 su Audit). E il confronto «scorre?» sotto zoom si fa con `clientHeight`, non con
`innerHeight`: `scrollHeight` sta in unita' di layout, `innerHeight` in px di finestra.

### Riempire una pagina che il DB di sviluppo lascia vuota

⚠️ **Costato un'analisi sbagliata su Permessi**: la ricognizione la dava a 1 346px perché la tabella dei
permessi era vuota; con 16 grant erano 2 449. I dati finti si scrivono nella copia **prima** di misurare, con
i casi che rompono le colonne — nomi lunghi, persone con più righe:

```python
c.execute("INSERT INTO EditGrants (AccId,DisplayName,GrantedAtUtc,GrantedByUserId,UserId) VALUES (?,?,?,?,?)",
          (acc_id, 'Alessandra Ferrari-Colombo', '2026-06-01 09:30:00', 704798, 555003))
```

### Provare uno stato che l'app non ti lascia costruire

Per verificare i lock servono **due persone**. Si simula scrivendo nel DB della copia mentre l'app gira:

```python
c.execute("UPDATE Documents SET LockedByUserId=?, LockedByName=?, LockExpiresUtc=? WHERE Id=3",
          (555001, 'Giulia Bianchi', fra_25_minuti_utc))
```

Un lock **mio** = `LockedByUserId` uguale al VID dell'identità di sviluppo (**704798**). Per provare il caso
«il lock nasce **dopo** il caricamento» si parte con `UPDATE Documents SET LockedByUserId=NULL…`, si carica la
pagina, **poi** si scrive il lock da fuori e si clicca.
