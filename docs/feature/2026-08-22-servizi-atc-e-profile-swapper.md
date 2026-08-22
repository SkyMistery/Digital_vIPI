# Feature — Servizi ATC: l'hub `/services` e il primo strumento integrato

Data: 2026-08-22 · Stato: **IN CORSO** — slice 0 (questo documento) fatta, slice 1→5 da eseguire.
Ramo: `feature/services-hub-profile-swapper`, da `main`.

Il sito smette di essere «la documentazione operativa» e diventa **il contenitore degli strumenti per gli
ATC di IVAO Italia**, di cui la documentazione è il primo. Il secondo è l'**Aurora Profile Swapper**, che
oggi vive come applicazione separata (repo `AuroraProfileSwapper`, Blazor WebAssembly su GitHub Pages) e
qui trasloca.

Le due cose stanno nello stesso documento perché sono la stessa decisione: il tool nuovo ha bisogno di un
posto dove stare, e quel posto è la forma delle URL del sito.

## Obiettivo

1. Una **home dei servizi** da cui si raggiungono tutti gli strumenti.
2. Il **profile swapper** dentro il sito, con il chrome, il tema, la lingua e il login che il sito ha già.
3. Farlo **una volta sola**: le rotte si spostano adesso, tutte, incluse quelle rimaste in italiano — un
   solo strato di reindirizzamenti invece di due.

## Stato di partenza (rilevato, non ricordato)

| Cosa | Misura |
|---|---|
| Rotte `@page` nel repo | **37**, di cui 35 sotto `/vsop` |
| Occorrenze di `/vsop` in `src` + `tests` | **506** in **124 file** (72 `.cs`, 47 `.razor`, 2 `.js`) |
| Di quelle, righe che contengono anche un segmento italiano | **180** (servono due sostituzioni, non una) |
| Occorrenze di `/vsop` in `docs/` | **451** |
| `/` | Oggi è un `NavigateTo("/vsop")`: la home vera non esiste |
| Motore dello swapper | **3 file, ~255 righe**, zero dipendenze, `net10.0` |
| Test dello swapper | 3 file, 227 righe, su **26 profili `.cpr` reali** (1,5 MB di fixture) |
| UI dello swapper | 1 pagina da 562 righe + ~50 stringhe in un `Dictionary` + 513 righe di CSS proprio |

### Gli accoppiamenti esterni (la parte che nessun test vede)

Tre URL sono scritti **fuori da questo repository**. Sono la ragione della regola §3.

| URL | Chi lo conosce | Se lo sposto |
|---|---|---|
| `/vsop/health/ready` | `render.yaml` **e** la dashboard Render — il servizio non nasce da Blueprint, quindi il file documenta e la dashboard decide — più lo smoke della CI (`ci.yml`) | Render riavvia in ciclo finché qualcuno non aggiorna la dashboard a mano |
| `/vsop/api/v1/transfers/resolve` | il **tool desktop Aurora Bridge già distribuito**, che lo porta come default compilato (`VipiApiClient`) | Le copie in circolazione prendono 404 |
| `/signin-oidc` · `/signout-callback-oidc` | registrati presso IVAO (app OAuth) | **Nulla**: stanno alla radice, il rename non li tocca. Il login è salvo |

## Design

### 1. Forma delle URL

```
/services                          hub «Servizi ATC»
/services/vsop                     documentazione operativa          ← un servizio
/services/vsop/{acc}/vipi          ...tutte le rotte di oggi
/services/profile-swapper          Aurora Profile Swapper            ← un servizio
/services/<prossimo>               i prossimi
```

**Perché un ombrello e non prefissi fratelli alla radice.** ADR-0005 accetta come limite che «il prefisso
non è parametrizzabile a runtime (mitigato via proxy)», e ADR-0002 descrive la RCL come qualcosa che un
sito host «monta su una rotta». Un prefisso solo è ciò che rende quel montaggio una riga di configurazione
invece di un elenco da tenere aggiornato a ogni strumento nuovo.

**Perché `services` e non `home`.** `home` descrive una destinazione, `services` descrive il contenuto. Su
un sottodominio già chiamato `atc.`, `atc.it.ivao.aero/services` si legge come «i servizi per gli ATC»;
`/atc/...` avrebbe ripetuto il sottodominio.

**Perché i figli sono piatti** (niente `/services/tools/` di mezzo): sotto `/services` **ogni figlio è un
servizio**, e la regola si legge dall'URL senza doverla spiegare. Un livello `tools` avrebbe messo la
documentazione a una profondità e lo swapper a un'altra, senza che nulla lo giustifichi.

### 2. Tabella del rename (è la specifica: si esegue questa, non «il senso di questa»)

Prefisso, per tutte le 35 rotte pagina: `/vsop/...` → `/services/vsop/...`.

Più le traduzioni e le sillabazioni, nello stesso giro:

| Oggi | Domani | Nota |
|---|---|---|
| `/vsop/guida` | `/services/vsop/guide` | |
| `/vsop/versioni` | `/services/vsop/versions` | |
| `/vsop/{acc}/versioni` | `/services/vsop/{acc}/versions` | |
| `/vsop/admin/permessi` | `/services/vsop/admin/permissions` | |
| `/vsop/admin/trasferimenti` | `/services/vsop/admin/transfers` | |
| `/vsop/admin/confinanti` | `/services/vsop/admin/neighbours` | grafia britannica: è quella del modello (`VloaRow.NeighbourCode`) |
| `/vsop/admin/diagnostica` | `/services/vsop/admin/diagnostics` | |
| `/vsop/admin/sorgenti` | `/services/vsop/admin/sources` | |
| `/vsop/admin/sectorstructure` | `/services/vsop/admin/sector-structure` | sillabata |
| `/vsop/editor/newdoc` | `/services/vsop/editor/new-document` | sillabata |
| `/vsop/admin/aeroporti` | **cancellata** | è un alias di `admin/airports`, che resta |
| `/vsop/{acc}/aeroporto/editor` | **cancellata** | alias di `{acc}/airports/editor`, che resta |

**Non si toccano** i termini che sono nomi propri e non parole: `vsop`, `vipi`, `vloa`, `acc`, `apps`,
`aor3d`, `airports`, `editor`, `live`, `tasks`, `screens`, `search`, `changed`, `release`, `audit`.

### 3. Regola: si sposta ciò che è una pagina, gli endpoint macchina restano

`/vsop/health`, `/vsop/health/ready`, `/vsop/api/v1/*` e `/vsop/live/atc` **non si spostano**, e accanto a
ognuno va scritto il perché (la tabella degli accoppiamenti esterni, sopra). Nessuno li digita: li conoscono
un file di deploy, una dashboard, una CI e dei binari già consegnati. Un URL che nessun essere umano legge
non guadagna nulla a essere bello, e perde tutto a cambiare.

Conseguenza accettata: il prefisso `/vsop` sopravvive per quattro endpoint. È scritto qui perché fra sei
mesi sembrerà una dimenticanza, e non lo è.

### 4. I reindirizzamenti sono una tabella, non una riscrittura di prefisso

Con le traduzioni dentro, «riscrivi `/vsop` in `/services/vsop`» non basta più: `/vsop/guida` deve arrivare
a `/services/vsop/guide` in **un salto solo**, non in due. Quindi: una **tabella ordinata** in un posto solo
(`LegacyRoutes`), consultata da un unico gestore, che porta ogni URL storico all'indirizzo finale.

| Vecchio | Nuovo |
|---|---|
| `/` | `/services` |
| `/vsop`, `/vsop/{*rest}` | `/services/vsop/...` (query preservata) |
| `/sop`, `/sop/{*rest}` | `/services/vsop/...` — la regola esistente si **ripunta**, non si incatena |
| le compat già presenti (`operativa`, `live-app`, `admin/struttura`) | direttamente al bersaglio finale |
| i dieci segmenti tradotti | direttamente alla forma inglese |

Con un **test** che per ogni riga della tabella verifica il 301 e l'indirizzo esatto d'arrivo: «un salto
solo» smette di essere un'intenzione e diventa una cosa verificata.

### 5. L'hub

`ServicesHome.razor`, `@page "/services"`, **nella RCL** (`Vipi.Ui`) e non nell'host: quando il modulo sarà
montato dentro il sito di Ivao.It, `/` non è nostro e l'hub deve esistere lo stesso. L'host tiene solo il
reindirizzamento da `/`.

Card: «vSOP — documentazione operativa» e «Aurora Profile Swapper», più il posto per i prossimi. Etichette
`Services_*` nei due `.resx` («Servizi» / «Services»).

### 6. Lo strumento

**Motore**: `src/Vipi.AuroraProfiles`, multi-target `net8.0;net10.0` (in produzione l'host è net8), con i tre
file del Core copiati **verbatim** dal repo esterno — `CprProfile`, `CprSection`, `ProfileSwapper` — e i suoi
test con le 26 fixture reali. Zero righe nuove di logica: se qui un test diventa rosso, è il multi-target,
non il parser.

Il contratto dello swap è quello già concordato e provato nel repo d'origine, e resta invariato:

- un `.cpr` è una **lista ordinata di blocchi verbatim**; caricare e riscrivere un profilo non modificato
  produce un file **byte-identico** (test di round-trip su tutti e 26);
- una sezione selezionata **esiste** nel destinazione → sostituita **sul posto**; **non esiste** → appesa in
  fondo; **manca nel sorgente** → errore esplicito e **niente viene toccato** (validazione prima, atomica);
- tutto ciò che non è selezionato resta invariato byte per byte.

**Pagina**: `/services/profile-swapper`, `SopLayout`, `InteractiveServer`. È il porting della pagina esterna
**senza il suo chrome**: header, tema e IT/EN li dà il layout, le ~50 stringhe vanno nei due `.resx` con
prefisso `Swap_` (il test d'integrità pretende la parità delle chiavi), il CSS va sui token di
`vipi-theme.css` — niente colori letterali, è la regola del ramo brand.

**Cambio di sostanza, da dichiarare in pagina.** Fuori è WebAssembly e i profili non lasciano il browser;
qui è Blazor Server, quindi i file **salgono al server**, vengono elaborati **in memoria** e non toccano mai
il disco. L'intro va **riscritta**, non tradotta: oggi promette il contrario.

Nome: percorso corto `profile-swapper`, etichetta piena **«Aurora Profile Swapper»**. `aurora-swapper` si
sarebbe letto come «scambia le Aurora»; `profile-swapper` da solo non dice di quali profili si parla, e
l'etichetta lo dice.

### 7. Ingressi (FEATURE-PROCESS §3: nessun catch-22)

Card nell'hub · voce nella topbar · card in `SopHome` · voce nella Guida in-app (`GuideSearchCatalog`).
Il logo della topbar resta puntato a `/services/vsop` — è l'abitudine di chi controlla — con una voce
**«Servizi»** separata che porta all'hub.

## Pre-flight (le 4 domande del runbook)

1. **Modello** — nessun concetto nuovo: il motore è una libreria di parsing senza stato e senza DB. Nessuna
   entità, nessuna migrazione, niente che tocchi lo schema. (Rilevante: il deploy resta fuori dal blocco del
   cutover MariaDB.)
2. **Dispatch** — nessuno `switch` per tipo, in nessun punto. L'elenco dei servizi nell'hub è dato, non
   dispatch: se un giorno i servizi avranno comportamenti propri, allora sarà un registry, non prima.
3. **Ingressi + verifica** — §7 per gli ingressi; §10 per la verifica.
4. **Propagazione** — ⚠️ **questa modifica rinomina**, quindi la domanda 4 è viva e vale per tutto il giro:
   `docs/spec/mappa-pagine.md`, `docs/spec/pagine-disabilitate.md`, i deep-link della Guida, i `data-tour`,
   i due `.js`, i test E2E, `deploy/atc-ivao/LEGGIMI-DEPLOY.md`, `deploy/mariadb/README.md`,
   `deploy/render/README.md`, le 451 citazioni in `docs/` e **le memorie** che citano rotte italiane
   (`/vsop/admin/diagnostica` in staff-code-reali, fra le altre). Aggiornate **nello stesso giro**, non «dopo».

## Slice (una per commit, build verde a ogni passo)

| # | Cosa | Verifica |
|---|---|---|
| 0 | Questo documento + riga in `docs/index.md` | — |
| 1 | **Il rename, da solo**: 506 occorrenze, tabella §2, redirect §4, doc e deploy aggiornati | build Release sui **due** TFM + suite + test della tabella dei redirect + una passata a mano sugli URL vecchi |
| 2 | **Il motore**: `Vipi.AuroraProfiles` + test + 26 fixture, in `Vipi.slnx`, `packages.lock.json` committati | i test del repo d'origine, verdi su net8 e net10 |
| 3 | **La pagina** `/services/profile-swapper` | bUnit + prova a mano con un `.cpr` vero |
| 4 | **L'hub** `/services` + gli ingressi §7 | bUnit sull'hub |
| 5 | **Verifica live** end-to-end | sotto |

La slice 1 va **da sola e per prima**: è meccanica, è la sola irreversibile per i segnalibri, e tenerla
separata la rende revertibile senza portarsi dietro il resto.

## Verifica live (come proverò che funziona, deciso ora)

Con la skill `verifica-live`, sul flusso vero:

1. carico un `.cpr` reale come sorgente e **due** destinazioni;
2. copio `[TRAFFICLISTS]`, scarico lo zip;
3. **confronto i byte** dei file usciti con gli originali: devono differire **solo** dentro quella sezione;
4. una decina di URL storici — italiani e no — devono arrivare a destinazione **con un solo 301**;
5. `/services` e `/services/vsop` a schermo, tema chiaro e scuro, e da **375 px** di larghezza (è una pagina
   pubblica: ricade nel perimetro delle pubbliche, non in quello admin).

## Trappole note, messe in conto prima di incontrarle

- **Avvisi = errori** su entrambi i TFM: si chiude con `dotnet build Vipi.slnx -c Release --no-incremental`,
  non con `dotnet test`, che quel flag non lo applica.
- **Progetto nuovo** = riga in `Vipi.slnx` + `packages.lock.json` committato, o la CI in locked mode si ferma.
- **Il diff nell'anteprima**: in WebAssembly disegnare 400 righe × N destinazioni è gratis; su un circuito
  Server è payload vero. Tetto alle righe mostrate, con «mostra tutto».
- **`InputFile` va ri-chiavato** dopo ogni caricamento o il drop successivo non scatta (la pagina d'origine
  lo fa già: `@key="_destInputKey"`, va portato con sé).
- **Il download** non passa da base64: `DotNetStreamReference` + blob.
- **Attributo `string` senza `@`** = letterale, non variabile: è la regressione silenziosa di questa casa.
