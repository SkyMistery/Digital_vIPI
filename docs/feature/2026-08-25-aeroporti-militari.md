# Quali aeroporti sono militari — il dato c'era già (25 agosto 2026)

**Domanda di partenza**: «sugli aeroporti hai informazioni su quali siano militari e quali no nel DB?»

**No.** La tabella `Airports` non aveva nessun flag: `Id`, `AccId`, `Icao`, `Name`, `TransitionAltitudeFt`,
`IsHidden`, `ParentCallsign`, `FeaturedRank`, `Latitude`, `Longitude`. `IsMilitary` esisteva solo su **`Acc`**,
e nel `vipi.db` di sviluppo era pure inaffidabile — LIBB, LIMM, LIPP e LIRR risultavano `IsMilitary=1` insieme a
LIZZ. Non usabile come sostituto.

Prima di aggiungere una colonna a mano, la domanda giusta era un'altra: **la sorgente lo sa?**

## 1. Sì, e lo buttavamo via

`GET /v2/airports/LIPA` col token app, misurato sul filo:

```json
{ "military": true, "icao": "LIPA", "iata": "AVB", "faaCode": null, "name": "Aviano",
  "centerId": "LIPP", "elevation": 413, "magnetic": 2, "transitionAltitude": 7000, ... }
```

`AirportDto` ne mappava sette campi su quindici. Il campo c'è **anche nella lista paginata**, quella che
alimenta l'import: `?countryId=IT` sono 9 pagine, **221 aeroporti, 34 con `military=true`** — e tutti e 34 erano
già in archivio, nessuno mancava.

> ⚠️ Nota di metodo. La prima chiamata di prova ha risposto **403** al *token endpoint*, non alle API: il filtro
> davanti a `api.ivao.aero` rifiuta l'User-Agent di default di Python. Con un UA qualsiasi il token arriva.
> Non è un problema dell'applicazione, che il suo UA ce l'ha.

## 2. Il campo non vuol dire quello che sembra

I 34 comprendono **Linate, Pisa, Ciampino, Catania Fontanarossa, Cagliari Elmas, Lamezia e Rimini**: scali
civili con sedime militare. `military` dice **«c'è una base militare sul campo»**, non «è un aeroporto
militare». Sette su trentaquattro: scrivere «Militare» accanto a Linate sarebbe falso per un lettore su cinque.

Da qui **due campi e non uno**:

| campo | chi lo scrive | cosa dice |
|---|---|---|
| `Airport.HasMilitaryPresence` | la sorgente, a ogni giro | c'è una base militare sul campo |
| `Airport.IsMilitaryOnly` | un amministratore | nessun traffico civile |

`IsMilitaryOnly` l'import **non lo tocca mai** — un giro notturno che riscrivesse la scelta di una persona la
cancellerebbe in silenzio. L'unica eccezione è la coerenza: tolta la presenza militare, «solo militare» cade con
essa, perché ne è un sottoinsieme. Il servizio **rifiuta** di accenderlo dove la sorgente non vede nessuna base,
invece di uscire in silenzio lasciando la spunta accesa a schermo e spenta in archivio.

Nello stesso giro sono entrati tre campi che la sorgente manda e scartavamo: **`Iata`** (⚠️ la sorgente manda
**stringa vuota**, non null, per i 73 aeroporti che non ne hanno uno — normalizzato, o in archivio finiscono due
modi diversi di dire «non ce l'ha»), **`ElevationFt`**, **`MagneticVariation`** (`double` e non `int`: la
sorgente manda entrambi nella stessa pagina).

## 3. La trappola: l'assegnazione è additiva

`AutoAssignAirportsAsync` **salta gli ICAO già in archivio**. Giusto per le entità — sopra un aeroporto ci sta
del lavoro editoriale — ma per i **campi** sarebbe stato veleno: le cinque colonne nuove sarebbero nate al loro
default su tutti e 93 gli aeroporti esistenti, e **nessun giro sarebbe mai passato a riempirle**. È la stessa
trappola del flag opt-out di `ImportSids`.

Ora lo stesso giro dell'anagrafica chiama **`SyncAirportSourceFieldsAsync`** su tutto l'elenco della sorgente e
conta a parte quanti ne ha corretti: «assegnati» e «aggiornati» rispondono a due domande diverse, e a regime il
primo è zero mentre il secondo no. Restano fuori nome e ACC di competenza.

Cinque test di caratterizzazione (`AirportSourceFieldsSyncTests`) presidiano: scrive sugli esistenti, non conta
un secondo giro senza novità, non tocca nome né scelta dell'amministratore, azzera «solo militare» quando cade
la presenza, e **lascia stare gli aeroporti che la sorgente non nomina** — «non lo so» non deve diventare «non
ce l'ha».

## 4. A schermo

**Pagina Aeroporti** (`/services/vsop/admin/airports`): una pastiglia per riga con le due etichette distinte, un
chip di filtro «militari» che conta la **presenza** (34, non la dozzina di soli militari: chi cerca «dov'è il
militare» vuole i trentaquattro), e il comando per segnare «solo militare».

**Elenco pubblico** e **testata del documento**: le stesse due etichette, più due chip militari/civili che
compaiono solo dove c'è davvero qualcosa da filtrare.

⚠️ Nella testata i due flag **non si leggono da `_profile`**: in anteprima di release quello viene azzerato di
proposito, e la presenza di una base non è un dato di release — sparire dalla testata solo perché si guarda un
ciclo passato sarebbe un'informazione persa per sbaglio.

Il set di icone non aveva un equivalente per «militare» — mancanza già annotata il 19 agosto. Ora c'è `shield`.

## 5. Due difetti visti solo a schermo, e la misura che ha assolto il terzo

La prima resa metteva la pastiglia nella colonna Stato **più** un tasto scudo accanto agli altri tre: il cestino
finiva **oltre il bordo** del riquadro e non lo si poteva premere. Spostata la pastiglia sotto il nome nella
cella ICAO, andava a capo su due parole e **alzava la riga di quaranta pixel**.

La forma buona è una sola cosa invece di due: **la pastiglia È il comando**. Sta nella colonna Stato, dice ciò
che è, e premuta cambia «solo militare». La colonna azioni torna a tre tasti.

Poi la misura, invece del giudizio a occhio: lo sforo orizzontale che resta è **17px a 1600 e 171px a 1280**,
**identico** su righe militari e civili, con la tabella larga 750px in entrambi i casi. Non è di questo lavoro:
è lo sforo già aperto in **§H3** dell'audit del 23 agosto.

## 6. Verificato

Guidando l'applicazione vera su una copia del `vipi.db` (Edge + puppeteer-core, porta 5099):

- premuto **il bottone vero** dell'anagrafica — stesso core del giro notturno — con le credenziali IVAO reali:
  **34 aeroporti** passati a `HasMilitaryPresence`, 55 con IATA, 93 con la quota;
- LIPA marcato e smarcato «solo militare» dalla pastiglia, con la pill che cambia;
- chip militari: da 93 righe a **34, tutte militari**;
- elenco pubblico e testata documento: tre rese distinte su LIBC / LIBD / LIBR, chip militari 2 / civili 1
  che filtrano davvero (⚠️ i flag di quei tre erano **forzati nella copia**: nel DB di sviluppo nessuno scalo
  militare ha una release pubblicata, quindi il dato era finto ma la resa vera);
- nessun errore in console, nessun letterale Razor, nessun callout.

⚠️ **Il lock di modifica**: lo stesso bottone prende E rilascia. Cliccarlo quando dice «Finish editing» lo molla
e tutti i comandi tornano disabilitati — costato un giro intero prima di accorgersene.

## 7. Cosa resta

- La produzione riempirà i campi **al primo giro dell'anagrafica** dopo il deploy. Nessun backfill in
  migrazione: il dato è di sorgente e la sorgente lo dà ogni volta.
- `IsMilitaryOnly` nasce **falso per tutti**, anche su Aviano e Ghedi: è un giudizio, e va dato una volta a mano
  sui trentaquattro. Un'ora di lavoro di un amministratore, non del codice.
- `faaCode`, `state`, `time_zone`, `status` restano scartati: nessuno li ha chiesti.
