# Confinanti (vLOA) — densità e uso (20 agosto 2026)

> Settima pagina del ramo di modifica, prima della lista «da rifare» di
> [regole-ui-pagine-admin](../design/regole-ui-pagine-admin.md) §15: **2 515px** misurati **chiusa**.
> Ma la pagina si usa **aperta** (regola 69): «Verifica» espande dentro la tabella una riga `colspan=10` con
> la tabella delle adiacenze e **due mappe** (SVG 320px + Leaflet 340px). Il numero che conta è quello.

## Pre-flight (FEATURE-PROCESS)

1. **Modello** — nessun concetto nuovo. `NeighbourCandidate` resta l'unica sede della coppia; la selezione
   di riga è **stato di pagina**, non entità. L'unica firma che cambia è `ImportAndComputeAsync`, che prende
   un `IProgress<NeighbourImportProgress>` opzionale (nuovo record + enum di fase: un tipo di *trasporto*, non
   un modello di dominio).
2. **Dispatch** — nessun `switch(tipo)` nuovo: lo `switch (r.Status)` dello stato resta uno solo, in pagina.
3. **Ingressi + verifica** — la pagina si raggiunge da Struttura (link già presente) e dal breadcrumb; il
   «?» punta a una sezione della Guida **che va creata** (`#admin-confinanti`) e registrata in
   `GuideSearchCatalog`, altrimenti la ricerca globale non la trova (regola 12). Verifica: live con
   `verifica-live` a 1600/1440/1280/1024, IT ed EN, zoom 0.8→1.5.
4. **Propagazione** — spariscono la mappa SVG e la sua chiave `Conf_MapHint` (regola 8: il testo tolto porta
   via la sua chiave da entrambi i resx). Le chiavi dei testi **spostati** nel «?» restano identiche.

## Cosa non va, misurato

| | Difetto | Regola |
|---|---|---|
| 1 | Testata in tre pezzi: `h2` + sottotitolo sempre a schermo, comandi nella riga-titolo della tabella | 1, 6, 7 |
| 2 | Fino a **4 callout in fascia** (errore, esito, avvisi import, admin-only) sopra il contenuto | 5 |
| 3 | **10 paragrafi** d'aiuto sempre a schermo, **nessun «?»** | 7, 9 |
| 4 | Tabella da 33 righe **senza `thead` fermo**, in una pagina che scorre | 23 |
| 5 | Il dettaglio è una **riga espansa**: la tabella salta di ~700px e la riga sotto sparisce | 0, 18 |
| 6 | **Due mappe della stessa cosa** (SVG proiettato a mano + Leaflet geografica) | — |
| 7 | Colonna azioni con **fino a 6 tasti** per riga | 53 |
| 8 | Form «aggiungi coppia a mano» — fallback raro — **sempre aperto** in fondo | 0 |
| 9 | Emoji 💾 📐 come comandi | 40 |
| 10 | I 4 chip di stato **non contano** niente; nessun modo di vedere la coda di lavoro | 30, 31 |
| 11 | Poligono e settore estero si scrivono alla cieca: errore solo dopo il salva | — |
| 12 | Import e «Verifica» sono lunghi (decine di GET IVAO) e **non si interrompono**, `CancellationToken` già in firma e mai cablato | 52 |
| 13 | ⚠️ **Nessun lock**: le altre quattro pagine di struttura prendono `ResourceLockKeys.Structure`, questa scrive `AccSector` esteri e genera documenti **senza** | — |

## La forma nuova

```
.wrap.struct
 └ .doc-head.st-head        titolo + conteggio · «?» · [Import & calcola] [+ Coppia a mano] [← Struttura] · chip esito/avanzamento · lock
 └ .conf-layout             griglia 1.35fr / 1fr, altezza MISURATA (vipiFitViewport, collapseBelow 900)
    ├ .panel.st-pane        SINISTRA — le coppie
    │   ├ .struct-bar       ricerca · chip stato con i conteggi · chip «senza shape» / «senza vLOA» · ✕
    │   └ .st-scroll        tabella `sticky-head` (thead top:0 dentro lo scroller, regola 51)
    └ .panel.st-pane        DESTRA — due modi: dettaglio della coppia scelta, oppure nuova coppia a mano
```

**Perché il dettaglio a destra** (regola 24-26 + 18): la riga espansa faceva saltare la tabella su cui si sta
lavorando; nel pannello la tabella **non si muove** e le coppie si verificano in fila. La riga intera è il
comando di selezione, i tasti dentro la riga fermano la propagazione, `Invio`/`Spazio` fanno quello che fa il
clic.

**Selezionare non scarica niente.** I dati che la riga già porta (distanza minima, settori adiacenti, shape,
stato, vLOA) si vedono subito; «Verifica adiacenza» — che **riscarica i settori esteri da IVAO** — resta un
gesto esplicito, con «Interrompi». Caricare in automatico a ogni clic di riga significherebbe bombardare la
sorgente mentre si scorre l'elenco.

**Una mappa sola.** SVG e Leaflet disegnavano le stesse shape con gli stessi colori. Resta la geografica
(chip accendi/spegni, sfondo CartoDB): −320px e un modo solo di guardare la stessa cosa.
⚠️ Il contenitore `.aor-leaflet` è idempotente per `data-init`: cambiando coppia va **ricreato**, non
riusato → `@key` sull'id della coppia, altrimenti resta la mappa di prima.

## Slice (un commit per passo)

1. **Testata in riga, lock, esito che non spinge, «?» + Guida** — A1/A2/A3/A4/B8.
2. **Due pannelli con l'altezza misurata**: tabella con `thead` fermo a sinistra, dettaglio a destra, una
   mappa sola — A5/B2/B3.
3. **Colonne misurate col font sui dati veri, chip con i conteggi, azioni compatte** — A6/B4/B5.
4. **Coppia a mano nel pannello, icone al posto delle emoji, poligono validato mentre si scrive** — A7/A8/B7.
5. **Import con avanzamento vero e Interrompi** — B6 (`IProgress` nel service + test sul fetcher).

## Verifica

- `dotnet build Vipi.slnx -c Release --no-incremental` (avvisi = errori, **entrambi** i TFM) + `dotnet test`.
- Live: 1600/1440/1280/1024, **IT ed EN** (`Accept-Language: en`), zoom 0.8→1.5; altezza pagina, quota della
  prima riga, larghezze di colonna col font calcolato, `thead` che resta, mappa che cambia con la coppia.
- ⚠️ Nessun test copriva questa pagina né `NeighbourImportService` (unica menzione di striscio in
  `AgreementFillingTests`): la slice 5 porta il suo test sul `ForeignAccFetcher`.
