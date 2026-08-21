# Handoff — il ramo della densità UI (aggiornato 22 agosto 2026, dopo il giro Audit)

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

[`docs/design/regole-ui-pagine-admin.md`](../design/regole-ui-pagine-admin.md): **142 voci in 22 gruppi**, ognuna
già costata un giro di correzioni, più la **ricognizione misurata** (§15) di tutte le pagine con cosa manca a
ognuna e in che ordine conviene farle. Non è un regolamento di stile: è l'elenco di ciò che, saltato, si ripaga.

Il §«Dove sta la roba» in coda dice quale classe/funzione usare per ogni pezzo: il pacchetto tecnico
(`.st-head`, `.st-msg`, `.res-table.sticky-head`, `.st-pane`/`.st-scroll`, `.struct-bar`, `.sh-chip`,
`.conf-layout`, e in `vipi-ui.js` `vipiFitViewport` / `vipiStickyOffset` / `rootZoom` / `placeHelpPop`) **c'è
già e si riusa**, non si riscrive.

## Dieci pagine chiuse

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

Cosa insegna, oltre a questo (regole 133-142): il dato grezzo non si butta e non si mette in colonna (il JSON
sta nel `title`); il vocabolario vecchio si **legge**, non si riscrive (`Archive` e `Delete` per la stessa
revoca dicono la stessa frase); il non-evento non si scrive; **un formattatore per tipo di dato, non uno per
pagina** (`AuditNarrator` è condiviso con la storia di Versioni, dove ha ucciso un parser che leggeva chiavi
che nessuno scrive); **elenco+dettaglio si giustifica con l'azione**, e qui non c'era azione da fare.

### La prossima pagina: Sorgenti

**Sorgenti** (`/vsop/admin/sorgenti`, **1 252px**: sottotitolo, 8 paragrafi d'aiuto, nessun «?», 2 callout in
fascia, tabelle corte — qui il `thead` fermo **non** serve), poi Diagnostica, Nuovo documento, Incarichi,
editor APP/vLOA. L'ordine aggiornato sta in §15.

⚠️ **Prima di misurarle, riempirle** (lezione di Permessi) e, se accumulano, **rimisurarle** (lezione di
Audit). Le due insieme dicono la stessa cosa: il numero della ricognizione è vero il giorno in cui è stato
preso, e su queste due pagine non lo era già più.

## Aperto, e non è di queste pagine

- ⚠️ La **topbar** fa scorrere la pagina in orizzontale a 1280/1024: `div.right` misura **1 385px dentro
  1 280** (rimisurato il 21 agosto; il 20 erano 1 411), identico su home, struttura, viewer e versioni — e
  **niente dentro il `.wrap` sfora**, verificato elencando gli elementi oltre il bordo. È del chrome, non di
  una pagina: va affrontato per sé. È anche la ragione per cui lo sforo orizzontale, da solo, non è più un
  segnale utile sulle singole pagine finché questo non è chiuso.
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
