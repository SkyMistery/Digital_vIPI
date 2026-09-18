# Aurora Sector Lab — carta di progetto e risultati di F0 (18 settembre 2026)

> **Stato: F0 ✅ FATTA** (quattro prove, tutte passate). **Nessuna riga nel prodotto, nessun dato toccato.**
> Prossimo passo: **F0-bis**, l'inventario dei PDF AIP scaricati (§9), poi la **carta di F1** (archi sul sito).
> F0-bis, prova 5 ✅ (§10): dalla **Cover Page** dell'AIRAC alla **checklist** del sector, controprovata sul 2609 vero.
> F0-bis ✅ (§11): inventario dei 154 PDF + confronto oggetto per oggetto col sector. ▶ Prossimo: **carta di F1**.
> Materiale delle prove, fuori dal repo: `D:\Programmazione\IVAO_Test\vIPI Ivao Italy\sector-lab-f0\`.
> PDF AIP, fuori dal repo: `D:\Programmazione\IVAO_Test\vIPI Ivao Italy\RealDOCS\`.
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md). Parte da [`2026-08-29-convertitore-coordinate.md`](2026-08-29-convertitore-coordinate.md).

## §0 — La domanda, e come è cresciuta

Punto di partenza, 17 settembre: estendere `/services/coordinates` perché aiuti l'AOD a scrivere il sectorfile.
«**Non deve agire direttamente sui file, ma darci strumenti per avere le coordinate o i gruppi di coordinate**»;
in particolare deve riconoscere gli **archi** scritti come nell'AIP:

```
44°51'24" N 008°14'57" E
then arc of circle in clockwise direction radius 17 NM centred on
44°55'29" N 007°51'43" E
till point
44°41'08" N 008°04'34" E
```

⚠️ **Oggi quel testo produce una forma SBAGLIATA senza avvisare**: le righe di testo si scartano e il **centro**
dell'arco (`44°55'29" N 007°51'43" E`) entra come vertice. È un difetto, oltre che una funzione mancante.

Poi il committente ha allargato la visione, in tre passi:
1. **GitHub**: l'AOD modifica i file del repo `ivao-italy/it-aurora-sector` da un attrezzo nostro, con un **ramo per
   l'AIRAC successivo** mentre `main` resta per gli aggiornamenti immediati.
2. **App desktop invece del sito**: git, file e Aurora sono locali — l'app locale serviva comunque.
3. **Aurora ricarica il sector a mano e ci mette fino a un minuto** → serve una **mappa nostra** per l'anteprima.

## §1 — Le decisioni prese (17-18 settembre)

| # | Decisione | Perché |
|---|---|---|
| 1 | **App desktop Windows «Aurora Sector Lab»**, separata dal `Vipi.AuroraBridge` | mestieri diversi: il Bridge serve in frequenza, il Lab a chi edita; aggiornare uno non deve cambiare l'altro |
| 2 | L'app **ospita un server Blazor su `127.0.0.1`** (porta a caso, segreto nell'indirizzo) e lo mostra in **WebView2** | riusa la NOSTRA interfaccia e la NOSTRA mappa; nessun ponte browser↔agente, nessun CORS |
| 3 | **Un motore solo, condiviso**: `Vipi.Application` (già multitarget) usato dal sito E dall'app | la regola del DMS traslocato: due verità sullo stesso formato divergono |
| 4 | **Git locale** (Credential Manager di Windows), **non** l'API contenuti di GitHub | l'API fa un commit per file e non sa fondere; il riporto `main` → ramo AIRAC con git è gratis. Nessun segreto sul nostro host |
| 5 | **Nessuna GitHub App** per ora. Al più un'autorizzazione *device flow* per **aprire le PR** dall'app (F4) | la scrittura la protegge GitHub, non noi |
| 6 | Rami: `main` = immediati · `airac/AYYMM` = ciclo successivo · `aod/<VID>/<argomento>` = lavoro | il ciclo lo sappiamo già leggere (`changelog.md`, vedi [il ciclo entrante](2026-09-02-il-ciclo-entrante.md)) |
| 7 | **Ruleset GitHub** su ramo predefinito **e** `airac/*`: PR obbligatoria con **0 approvazioni** (chi la apre la fonde: la PR è il **secondo sguardo**, non un permesso), *Block force pushes*, *Restrict deletions*, bypass vuoto | siete in tre: un'approvazione obbligatoria si aggirerebbe |
| 8 | Permesso **«Sectorfile (AOD)» ORTOGONALE** a `VipiRole`, acceso dall'Admin | `VipiRole` è **ordinato e cumulativo** (`User 0 … Admin 4`): in mezzo rinumera gli ordinali, in coda starebbe sopra Admin, e un AOD non è «più» di un chief |
| 9 | **Due cancelli**: il nostro permesso apre la pagina, **GitHub** decide chi scrive | un solo elenco di chi può scrivere, e non è nostro |
| 10 | **La mappa nostra è l'ossatura**; Aurora si guarda **a lotti** («7 modifiche da vedere») | un minuto per ricarica: Aurora non sta nel giro corto |
| 11 | **Nessuna firma** dell'eseguibile per ora (3 persone). Se esce dall'AOD → **Azure Trusted Signing** (~10 $/mese) | OV richiede token hardware dal 2023; EV ha senso solo per molti utenti |
| 12 | `update.ini` e `changelog.md` **scritti dal servizio**, con revisione manuale nella PR | sono meccanici e si dimenticano |
| 13 | Archi: densità **regolabile**, di base **1 punto per grado**. Testo AIP in **inglese e italiano**. Confini da `GEO/itgeo.geo` | risposte del committente |
| 14 | 🔴 **Nessun dato di vIPI si tocca.** F1 = codice del sito senza migrazioni, nel pacchetto normale | richiesta esplicita del committente |

**Confini dell'app** (vanno scritti anche nel codice): solo `SectorFiles/Include/IT/**`, `update.ini`, `changelog.md`;
mai `Aurora.exe`, DLL, suoni; mai *force push*; mai cancellare rami non nostri; mai binari; mai l'updater.

## §2 — Fatti misurati sul repo del sector

Misurati sulla copia locale `D:\Programmazione\IVAO_Test\Refactoring_ItalianSectorFile\it-aurora-sector`.

| Cosa | Misura |
|---|---|
| Fine riga | **LF dentro git, CRLF sul disco** — lo impone il `.gitattributes` del commit `5a98f2b` (40 estensioni) |
| Codifica | **UTF-8 senza BOM** (`ù` = `c3b9` in `limg.geo`, `À` = `c380` in `licr.vfi`) |
| Riga finale | **quasi tutti i file NON hanno il ritorno a capo finale**; `ITALY.isc` sì |
| Peso | 14 MB di dati IT, **412 MB di cronologia** (c'è `Aurora.exe`): mai clonare dal server |
| Confini | `GEO/itgeo.geo`, 13 835 righe, etichetta `COAST` |

Prova byte per byte su `italy.restrict`: disco `0d0a 2f2f 5234 0d0a`, git `0a 2f2f 5234 0a`, nessun a capo finale in entrambi.

⚠️ **`itgeo.geo` MESCOLA due formati nello stesso file**: 8 914 righe in DMS **puntato** (`N041.14.35.812`) e 4 658 in
DMS **compatto** (`N0434857348`). Un lettore che ne conosca uno solo perde due terzi dei confini **senza errore**.

Buchi da colmare (misurati): `.vrt` 14 file / 121 righe · `.hold` 1 / 71 · aree P/R/D 3 file / 13 789 righe
(`.prohibit` 10 479, `.restrict` 2 567, `.danger` 743) · `.mva` 28 / 10 089.

## §3 — F0, prova 1: il guscio (Blazor locale in WebView2)

Eseguibile WinForms + WebView2 + Blazor Server interattivo, `net8.0-windows`. Sorgente: `sector-lab-f0\guscio\`.
WebView2 già installata (153.0.4234.32), SDK .NET 10, runtime 8.

| Momento | ms dall'avvio |
|---|---|
| server in ascolto su `127.0.0.1:<porta a caso>` | 64 |
| WebView2 pronta | 337 |
| pagina HTTP 200 | 464 |
| **circuito interattivo vivo** | **567** |
| file del sector letto (7 ms), analizzato, spedito e disegnato | 1 435 |

Cancello: segreto nell'indirizzo → cookie `HttpOnly`/`SameSite=Strict`; senza → **403**.

**Quattro trappole, già pagate — vanno nel codice vero dal primo giorno:**
1. ⚠️ Senza **`app.UseAntiforgery()`** ogni pagina risponde **500**, senza un messaggio che lo dica.
2. ⚠️ La radice dei contenuti è la **cartella di lavoro del processo**: lanciato da un collegamento, **tutti** i file
   statici vanno a 404. Fissare `ContentRootPath = AppContext.BaseDirectory` e copiare `wwwroot` in uscita.
3. ⚠️ SignalR accetta **32 KB per messaggio ricevuto**: un poligono grosso lo supera e il circuito **muore in
   silenzio**. Alzare `HubOptions.MaximumReceiveMessageSize`.
4. Leaflet preso da CDN funziona solo con la rete: nell'app va servito da noi (il sito lo ha già in `wwwroot/vendor`).

## §4 — F0, prova 2: la mappa col sector INTERO

Caso peggiore: tutti i `.geo` (96 file, 83 186 segmenti cuciti in 6 781 polilinee) + tutti i `.pol` (93 file,
1 750 poligoni) = **8 531 oggetti, 128 709 punti**. Misurato **dentro la WebView2**, senza sfondo a mattonelle.

| Motore | Primo disegno | Gesti Italia (media / peggiore) | Gesti Fiumicino z14 |
|---|---|---|---|
| Leaflet SVG (il sito oggi) | 98 ms | 49 / 100 ms | 46 / 67 ms |
| **Leaflet canvas** | **55 ms** | **39 / 50 ms** | **39 / 50 ms** |
| MapLibre (WebGL) | 364 ms | 26 / 50 ms | 24 / 49 ms |

**Scelta proposta: Leaflet canvas.** È il motore del sito (`vipi-aor.js`, oggi SVG), servito da noi; MapLibre
vorrebbe dire due motori. Senza rete sparisce solo lo sfondo Esri; e lo sfondo migliore è il **sector stesso**
(coste e confini da `itgeo.geo`). Misure fatte sul PC del committente: su macchine più lente il margine resta ampio.

## §5 — F0, prova 3: i PDF dell'AIP

Prototipo `sector-lab-f0\pdf-aip\enr2.py` (pdfplumber, colonne ricavate **per pagina** dalle righe verticali della
tabella). ⚠️ Punta ai vecchi percorsi di `RealDOCS` (i PDF ora stanno in `RealDOCS\ENR\ENR 5\ENR 5.1\`).

| File | Aree | Complete |
|---|---|---|
| ENR 5.1.1 (P) | 280 | 280 |
| ENR 5.1.2 (R) | 152 | 152 |
| ENR 5.1.3 (D) | 48 | 48 |
| ENR 5.1.4 (TSA/TRA) | 61 | 61 |
| ENR 2.1.1.4.1 (Milano CTA) | 39 zone | 39 |

**2 690 coordinate su 2 690**, controprova con `pdftotext` indipendente. Classificazione delle 541 aree ENR 5.1:
322 poligoni «to point of origin», 185 cerchi, 29 con arco, 5 con confine/costa. Su tutti i file: **44 archi**,
**13 aree a confine o costa** (il 2,2%: lì il PDF non ha la geometria, serve `itgeo.geo`).

### La grammatica completa (colonna geometria) — base di F1

`<C>` = coordinata, sempre `DD°MM'SS"N DDD°MM'SS"E` (U+00B0, apostrofi ASCII; virgolette curve **solo** nelle Note).

```
to point of origin.
Circular area centered on <C> within a 1.0 NM radius.           (anche «300.0 M», «5.0 KM»)
<C> then arc of circle in clockwise direction radius 2.8 NM centred on <C> till point of origin.
<C> then arc of circle in clockwise direction radius 13.5 NM centred on <C> till point <C>
<C> then arc of circle in anti-clockwise direction radius 5.0 NM centred on <C> till point <C>
<C> then arc of circle in anti-clockwise direction radius 5.0 NM centred on <C> till point of origin.
<C> Italian northern geographical border till point <C>
<C> line at 500 m from coast to point of origin.
```

- `centred` all'inglese britannico **sempre**; `centered` solo nella frase del cerchio. Raggio in **NM, KM, M**.
- La frase dell'arco va **a capo tre volte** (il raggio su una riga sua).
- Un poligono può avere **più archi** (LI R503/A: 16.0 e 18.5 NM) e **due tratti di confine** (Zone '18' Monte Bianco).
- Il testo italiano («arco di cerchio in senso orario…») **non compare** nelle colonne geometria di questi PDF: va
  previsto (decisione 13), ma la forma va presa da un esempio vero quando si trova.

### Le trappole che contano

- 🔴 **Identificatori fuori standard SALDANO l'area alla precedente, senza errore**: `LI R48 A` (senza barra),
  `LI R300/A Amendola` (senza trattino), `LI/LD D35/A`, `LI TSA73 bis`, `EUC 60` ed `EUC 660` (senza `LI`), e il
  refuso **`Zona '29'`** invece di `Zone` in Milano CTA. → **Guardia**: ogni riga della colonna geometria che non è né
  coordinata né frase nota si segnala.
- 🔴 Le righe della tabella **non** delimitano le aree (in 5.1.4 una riga ne contiene due): si lavora sulla y del testo.
- Pagine pari e dispari sfalsate di ~19,4 pt; x diverse per file; cornice e disegni che imitano le righe; intestazione
  ripetuta disallineata di 2,6 pt (in 2.1.1.4.1 **sostituiva** limite e classe delle zone 26 e 32); nomi d'area che
  vanno a capo (14 casi); limiti spezzati su più righe (75 aree); **77 aree continuano nella pagina dopo**;
  identificatore duplicato (`LI TSA74` ×2: la chiave è identificativo **+ nome**).
- ENR 2.1: stessa macchina, **profilo diverso** (colonna 2 = superiore, inferiore, **classe**).
- ⚠️ **Ogni pagina ha la sua data AIRAC** (5.1.1 p.1: 10 JUL 2025; 5.1.2: 09 JUL 2026; 5.1.4: 05 OCT 2023). Con
  **GEN 0.4** (elenco delle pagine) si sa quali pagine sono cambiate in un ciclo → base del ramo `airac/`.

### Le procedure AD 2 (sondaggio)

`LI-AD 2 LIME 6` ha le tabelle di codifica in chiaro: path terminator ARINC (`IF`, `TF` con rotta, distanza, quota).
`IF/TF/DF` si risolvono coi cataloghi dei fix; ⚠️ `VA/CA/VI` **non hanno un punto finale**: si segnalano, non si inventano.

## §6 — F0, prova 4: i due progetti fermi

**A — `D:\Programmazione\Hobby\Aurora SectorFile Drawer`** (C#, un commit `5761d00` del 30 maggio 2026): **libreria
di lettura/scrittura finita, senza applicazione**. 21 parser + 20 scrittori (`src\AuroraSectorDrawer.IO\`),
**310 test verdi** (eseguiti), round-trip **byte per byte su ~620 file** veri (`RealFileIntegrationTests`).
`SectorFileReader` risolve codifica (UTF-8 stretto, ripiego Windows-1252), BOM, fine riga, riga finale;
`FileSaverOrchestrator.WriteAtomic` = `.tmp` + flush + `File.Replace`. **Fermo all'ingresso della fase «mappa»**.
Non ha: mappa, git, diff, backup, validatore; `.vrt`, aree P/R/D, `.hold`. `ParserRegistry` è codice morto; il `.mva`
è dichiarato approssimato; `App\` e `ViewModels\` mai committati.

**B — `D:\Programmazione\IVAO_Test\Aurora Sector Files Reeditor\Aurora SectorFiles Reeditor`** (TS/React, mai sotto
git, fermo al 12 giugno): **applicazione funzionante senza libreria**. Mappa MapLibre (16 425 elementi in ~540 ms),
`ChangeSet` con diff obbligatorio, backup di tutti i file prima di scrivere, `GarbageCollector` multi-master
(sui file veri: 1 riferimento rotto, 31 orfani, 214 chiavi duplicate, e un errore vero `itvor.vor:109 DMS fuori
range 47.44.75.0`). 198 test verdi. 🔴 **Legge e scrive UTF-8 a forza**: su un file Latin-1 corromperebbe anche le
righe non toccate. Fermo «sulla soglia del guscio desktop» (commenti su Tauri).

| Pezzo | Verdetto |
|---|---|
| A: `SectorFileReader`, `ParseResult`/chunk, 21 parser+scrittori, `CoordinateConverter`, `SessionService`, `WarningCollector` | **ereditare** |
| A: scrittura atomica | **ereditare**, ma ⚠️ **togliere** i marcatori `//Start`/`//End` che inietta |
| A: `IscLoader` | ereditare + innestare il **multi-master** di B |
| A: `.mva`, `ParserRegistry`, UI WPF | riscrivere / ignorare |
| A: `docs\TEST_MATRIX.md`, `docs\SRS.md` §5 | **leggere**: capitolato di test e specifica dei formati complessi |
| B: `SPECIFICA_FORMATI.md`, `CONVENZIONI_USO.md`, `FUNZIONALITA_EDITOR.md` | **leggere per primi** (25 estensioni, cartelle, trucco `MAPS`) |
| B: `ChangeSet`, backup, `GarbageCollector`, `DuplicateService`, `conventions.ts`, `fileLifecycle.ts`, `buildGeoJson` | **riscrivere in C#, copiando l'idea** |
| B: test «modifica su copia reale, cambia solo la riga toccata» | **copiarne il modello** |
| B: I/O a UTF-8 forzato | **evitare attivamente** |

## §7 — Il piano

| Fase | Dove | Cosa | Finita quando |
|---|---|---|---|
| **F0** ✅ | prove | guscio, mappa, PDF, progetti fermi | — |
| **F0-bis** | prove | inventario di **tutti** i PDF scaricati (§9): sezione AIP → file del sector → estraibile? → differenze col sector di oggi | una tabella per sezione, coi numeri |
| **F1** | motore + **sito** | archi, cerchi, `to point of origin`, confine/costa segnalati, testo IT+EN, densità regolabile; il centro non entra più fra i vertici; avviso se il raggio dichiarato non torna | l'area della foto del committente esce giusta |
| **F2** | motore | formati del sector **ereditati da A**, + `.vrt`, `.hold`, aree P/R/D, doppio DMS; validatore | round-trip a zero differenze sull'albero intero |
| **F3** | app | guscio, sfoglia, **mappa di anteprima con contesto**, diff, backup, scrittura atomica | una modifica = un diff di 3 righe e la forma sulla mappa |
| **F4** | app | git: rami, commit, PR, stato del ramo AIRAC, riporto da `main` | un giro ramo → PR → fusione |
| **F5** | app | Aurora a lotti | una ricarica sola per un lotto |
| **F6** | app | PDF ENR → aree, confine da `itgeo.geo`, confronto, ramo AIRAC proposto, `update.ini`/`changelog.md` | un ENR → elenco di differenze vere |
| **F7** | app | procedure AD 2 (SID, initial climb, transition, STAR) | LIRN, LIME, LICA, LIRF, LIMC |
| **F8** | app | geometria fine: radiale/DME, asse pista, saldatura bordi, semplifica | |
| **F9** | app | trascinare i vertici sulla mappa | |

**Sul sito resta**: `/services/coordinates` con gli archi (anche per chi non è AOD), cataloghi, import SID, confronto
col sectorfile che già gira.

## §8 — Decisioni ancora aperte (proposta tra parentesi)

1. ✅ **Metadati `//@` nei file** (committente, 18-set, contro la proposta «nessuno»): servono a portare **valori in
   più** che il formato Aurora non ha (es. l'**initial climb** delle SID), perché **sistemi esterni come vIPI** li
   estraggano e le informazioni stiano **in un posto solo**. Si adotta la convenzione già scritta in B
   (`SPECIFICA_FORMATI.md` §metadati): solo **righe intere** `//@chiave=valore` (mai in coda a una riga dati: Aurora
   in alcune sezioni le leggerebbe come dato), a livello di **file** (prime righe) o di **record** (subito sopra la
   riga). Chiavi già previste da B: `@initialclimb`, `@fix` (nome intero del fix), `@source` (ciclo AIRAC), `@note`,
   `@id`, `@locked`, `@gen`, `@extra`. ⚠️ Da decidere nella carta di F2: un `//@` resta **orfano** se un AOD cancella a
   mano la riga sotto, e si attacca alla successiva → proposta: la riga porta il nome del record
   (`//@XIBR5A initialclimb=5000`) e il validatore segnala se non combacia. Il **catalogo delle chiavi** è un contratto
   fra il Lab (scrive) e vIPI (legge).
2. ✅ Marcatori `//Start`/`//End` di A: **fuori** (li sostituiscono i `//@`; sporcherebbero ogni diff).
3. ✅ `.vrt` e `.hold`: **in F2, subito**.
4. ▶ Dove vive il codice del Lab: proposta **stesso repo di vIPI** — in discussione (il committente ha chiesto il perché).
5. ✅ Motore della mappa: **Leaflet canvas** (§4).

## §9 — Il materiale scaricato per F0-bis (`RealDOCS`, 155 file, 138 MB)

- `GEN\`: `LI-GEN 0.1` … `0.6` (**0.4 = elenco delle pagine con le date**), `LI-GEN 2.4`
- `ENR\ENR 2\`: `ENR 2.1.1\` (2.1.1.1 … e `ENR 2.1.1.4\`), `ENR 2.1.2\` (un PDF per CTR: Alghero, Catania, Crotone…),
  `LI-ENR 2.1.3`, `LI-ENR 2.2`, `LI-ENR 2.2.1` — 59 file
- `ENR\ENR 3\`: `ENR 3.2\` **solo alcune rotte** (troppe da scaricare: KY139, L12, L5, L869, L995…), `ENR 3.4\` (3.4.1, 3.4.2).
  ENR 3.1 e 3.3 sono **vuoti** nell'AIP
- `ENR\ENR 4\`: `ENR 4.1\` (4.1.1, 4.1.2), `LI-ENR 4.4`, `LI-ENR 4.5`. ENR 4.2 e 4.3 **vuoti**
- `ENR\ENR 5\`: `ENR 5.1\` (5.1.1 … **5.1.5**), `ENR 5.2\` (5.2.1, 5.2.2 — **usate** nel sector), `ENR 5.3`
  (**non usata**, scaricata per completezza; la 5.4 non c'è)
- AD 2: `LI-AD 2 LIME`, `LI-AD 2 LIRN` (i primi due), e poi `AD 2 LICA` (STAR con transition), `AD 2 LIRF`,
  `AD 2 LIMC` (i più complessi)

⚠️ I PDF **non vanno in git** (peso, copyright ENAV). ⚠️ Verificare che siano **dello stesso ciclo**: le date di pagina
servono solo così (e le prime pagine di ENR 5.1.x oggi vanno dal 2023 al 2026, il che è normale — ogni pagina ha la sua).

## §10 — F0-bis, prova 5: dalla «Cover Page» alla checklist dell'AIRAC (18 settembre)

Il committente ha aggiunto `RealDOCS\LI-Cover Page.pdf`: la copertina dell'**AIRAC AMDT A09/26** (pubblicato 23 JUL
2026, in vigore **03 SEP 2026** = ciclo **2609**). Contiene due cose: l'**elenco delle modifiche** (26 voci, IT a
sinistra, EN a destra) e l'elenco **pagine da distruggere / da inserire** (83 documenti).

Prototipo: `sector-lab-f0\airac\checklist.py` (pdfplumber + git). Legge la Cover, legge il sector **da git**
(`origin/master`, mai scritto), scrive `airac\out\checklist-A09-26.md` con: checklist per voce, pagine cambiate
**non annunciate**, bozza di `CHANGELOG/2609.txt` nel formato degli AOD, e — se c'è già — il changelog vero.

**Fatti misurati.**
- Capitoli di AD 2 (dai PDF di LIRF/LIME e dalle voci del ciclo): **1** testo AD 2.x · **2** carte a terra (ADC/APDC) ·
  **3** carte ostacoli · **4** STAR · **5** avvicinamenti **e** carte a vista (VRP) · **6** SID · **8** ATC SMAC (→ `.mva`).
  Il 7 non c'è nei PDF scaricati.
- ENR 3.4.1 = attese in rotta (→ `HOLDENR.hold`), 3.4.2 = radioassistenze. Mappe CTR/ATZ stanno nel `<icao>.str`
  (`LIRV;MAPS;LIRV ATZ`); i `.str` portano STAR **e** parte degli avvicinamenti (`ILSZ36`, `RNP04L`).
- Nome → ICAO: `OTHER/itap.ap` usa il **secondo** nome (`FERTILIA`, `COSTA SMERALDA`, `TESSERA`); FIR da `OTHER/<fir>.ap`
  (⚠️ LIMP sta sia in `limm.ap` sia in `lipp.ap`).
- ⚠️ **L'elenco delle modifiche NON è completo**: 13 aeroporti hanno pagine cambiate senza voce (LICJ **−12 pagine**
  di capitolo 5, LIPO −6, LIME/LIMG/LIPK/LIPQ/LIRI/LIRN/LIRQ −4: avvicinamenti **ritirati**), più ENR 3.4.1/3.4.2.
  Chi legge solo le voci li perde.

**Lo stato («fatto?») si deduce dai commit dopo la pubblicazione, con due regole imparate sbagliando:**
1. 🔴 Un commit **di massa** (es. `cdc3b9c` «tag RNAV», 41 `.str`) tocca i file di mezza Italia: non prova niente → 🟡.
2. 🔴 Un file **condiviso** (`itfreq.frq`, `DYNAMIC_SEC/*`, `GEO/italy.restrict`) conta solo se il **diff** contiene
   l'ICAO o il nome della voce. Prima versione: RIACI CAPO risultava «fatto» per un commit su **LIRU** che toccava
   `VFR_NASCOSTI.fix`; CEPOLISPE «fatta» per la revisione della TSA626.
   E se il nome **c'è** nel sector ma nessun diff lo tocca → ⬜ (RIACI CAPO: stesse coordinate dal 2024).

**Controprova col lavoro vero degli AOD** (il remoto ha già `2609.txt`, scritto a mano):

| | Voci |
|---|---|
| ✅ trovate fatte (4/4 delle voci AIP del changelog vero) | LIEA, LIEO, LICG SID/STAR · LIRU VRP |
| ⬜ candidate **mancanti** | LIRV ATZ e CTR (voci 3, 4, 26: `lirv.str` fermo al 12-2025) · LICR VRP RIACI CAPO · LILE piste/frequenze · ENR 3.4.1 attese (12 pagine) |
| 🟡 solo commit di massa | LIPZ STAR/SID · avvicinamenti di LIMZ, LIMJ, LIMP, LIMF, LIPX, LIRA · 10 aeroporti «silenziosi» |
| ❔ il sector non ha il nome: ci va? | aree FMC, Milano CTA zona 18 «MONTE BIANCO», zone UA/APR, laser, lanci, aeroclub |
| fuori dall'AIP (la checklist non può saperle) | LIBV/LIBA MIL gates, VFR routes LIBV/LICZ |

⚠️ ⬜ vuol dire «nessuno l'ha toccato», **non** «è sbagliato»: la nota di classificazione del CTR di Viterbo nel
sector forse non esiste. La checklist **indirizza lo sguardo**; il giudizio resta all'AOD.

**Limite accettato:** la Cover dice *dove* è cambiato, non *che cosa*. ✅ **Decisione del committente (18-set):
NON si archiviano i PDF del ciclo precedente**; la checklist dice *cosa guardare*, il *che cosa è cambiato* lo trova
l'AOD aprendo il file. ✅ **Il militare lo cura il SOD**: MIL gates, VFR routes militari e simili restano fuori dalla
checklist AIRAC.

## §11 — F0-bis: inventario dei PDF e confronto col sector di oggi (18 settembre)

Prototipi in `sector-lab-f0\airac\`: `inventario.py` (livello 1, per file) e `confronto.py` (livello 2, per oggetto).
Uscite: `out\inventario.md` (una riga per PDF) e `out\confronto.md` (+ `.json` con gli elenchi completi).
Sector letto da `origin/master` (c1de948, ciclo 2610).

**Il materiale è tutto del ciclo 2609**: GEN 0.4 e le pagine più recenti di ogni file portano 03 SEP 2026.
154 PDF (esclusa la Cover), **1 664 pagine**, letti in **16 s** con `pdftotext`. ⚠️ pdfplumber ci metteva ~1 minuto
per PDF sulle carte: va bene per le tabelle difficili (ENR 5.1), non per lo scandaglio.

**Livello 1 — cosa c'è, per gruppo.** «Solo immagine» = pagina senza testo estraibile (carta disegnata).

| Gruppo | PDF | Pagine | Solo immagine | Coordinate | File del sector |
|---|---|---|---|---|---|
| GEN | 7 | 64 | 5 | 0 | — (GEN 0.4 = indice delle pagine) |
| ENR 2.1.1 FIR/CTA/TMA | 11 | 96 | 7 | 2 258 | `LOW_/HI_AIRSPACE`, `ACC`, `DYNAMIC_SEC` |
| ENR 2.1.2 CTR | 45 | 156 | 26 | 1 037 | `<icao>.str` (MAPS «CTR») |
| ENR 2.1.3 / 2.2 ATZ, RMZ | 3 | 22 | 1 | 308 | `<icao>.str` (MAPS «ATZ») |
| ENR 3.2 rotte (29 scaricate) | 29 | 64 | 22 | 226 | `AIRWAY/*.lairway/.hairway` |
| ENR 3.4.1 attese · 3.4.2 radioassistenze | 2 | 32 | 1 | 0 | `HOLDENR.hold` · `NAVAIDS` |
| ENR 4.1 radioassistenze | 2 | 18 | 1 | 91 | `itvor.vor`, `itndb.ndb` |
| ENR 4.4 punti | 1 | 158 | 0 | 1 254 | `NAVAIDS/*.fix` |
| ENR 5.1 P/R/D/TSA | 5 | 124 | 5 | 2 215 | `GEO/italy.*` |
| ENR 5.2 militari | 10 | 72 | 20 | 674 | `GEO/italy.restrict` (militare = SOD) |
| ENR 5.3 altri pericoli, 4.5 luci | 5 | 16 | 5 | 38 | — |
| AD 2 cap. 1 testo | 5 | 180 | 4 | 240 | `itap.ap`, `itrw.rw`, `itfreq.frq`, `.vfi`, `.atis` |
| AD 2 cap. 2 carte a terra · 3 ostacoli | 10 | 106 | **94** | 0 | `GND_LAYOUT`, `.gts`, `.txi` · — |
| AD 2 cap. 4 STAR · 6 SID | 10 | 346 | 158 | 296 | `.str` · `.sid` — **140 pagine di tabelle ARINC** in chiaro |
| AD 2 cap. 5 IAC/VAC | 5 | 178 | **134** | 125 | `.str`, `.vfi` |
| AD 2 cap. 8 ATC SMAC | 5 | 32 | 13 | 1 122 | `.mva` (⚠️ LIMC/LIME/LIRF: `ENRMVA`?) |

⚠️ `itap.ap` chiama gli aeroporti col **secondo** nome: 33 CTR su 45 non si agganciano per nome (Alghero ≠
FERTILIA); l'ICAO più frequente nel testo del PDF li aggancia (Alghero → LIEA ×4).

**Livello 2 — l'AIP contro il sector, oggetto per oggetto** (numeri di `out\confronto.md`):

| Sezione | AIP | Sector | Comuni | Cosa esce | Affidabilità |
|---|---|---|---|---|---|
| ENR 4.4 punti ↔ `*.fix` | 1 116 | 2 182 | 1 116 | 0 mancanti; **TIMOV** (350 NM) e **XOPTA** (260 NM) sono **altri punti** nell'AIP | buona (1 116 su ~1 250 nomi) |
| ENR 5.1 aree ↔ `GEO/italy.*` | 540 | 550 | 526 | mancano **P343, P739, P92, R107A-D, R11, R167, R362, TSA421B**; il sector ha **P167** (R167?) e TSA74A/B (AIP: TSA74 ×2) | buona |
| ENR 3.2 rotte ↔ `AIRWAY` (29) | — | — | 18 identiche | **M616, Q985, T543, T678, Y651** assenti; 11 con punti in più nel sector (spesso i tratti esteri) | buona |
| ENR 3.4.1 attese ↔ `HOLDENR.hold` + `HLD-` dei `.str` | 162 | 239 | 113 | 49 attese AIP senza disegno; 6 con verso/rotta diversi | media (colonna fix rumorosa) |
| SID/STAR dei 5 aeroporti ↔ `.sid/.str` | 404 | 343 | 324 | LIRN: tutte le 7G/1A-1C ci sono, mancano **7H/8J/1B/1D**; LIRF: 4 STAR solo nel sector (GILI3T, LUNA3T, MOP3K, RITE4D) | buona sui nomi, **non** sui percorsi |
| ENR 4.1 radioassistenze | 30 | 131 | 30 | 6 posizioni diverse (PAN 0,3 NM, OST 0,1 NM…) | ⚠️ **parziale**: 30 su ~110 |
| VRP (cap. 1 e 5) | 14 | 51 | 14 | nessun VRP AIP mancante | ⚠️ LICA e LIRF: VRP solo nelle carte (immagine) |

**Cose imparate (vanno nel motore di F6):**
- 🔴 Nel sector i designatori hanno **tre convenzioni**: `XIBR5A` (LIRF, LIRN), `IRK7A-PEP2X` (LIMC, LIME, SID +
  transizione), `BAGIX3R` (LICA, nome intero) — e LIRF le ha **tutte e due** (`EKLO8M` ed `EKL8M`). Confronto:
  stesso suffisso + nome del sector = **inizio** del nome AIP. Prima prova a 3 lettere: SOSA e SOSI collidevano.
- 🔴 Contorni del sector che valgono per **più aree** AIP: `R18A/B`, `D35BC`, `R405AC`, `D75` (= D75A+B+C).
- 🔴 Le attese vicine agli aeroporti stanno nei `<icao>.str` (`HLD-GIKIN`), non in `HOLDENR.hold`.
- pdftotext: grado = `U+FFFD` o `°`, secondi = `''`; nelle rotte i punti hanno davanti `▲`/`∆`.
- 🔴 Le trappole della shell sui sorgenti: heredoc + Python hanno scritto `\b` e `\f` come caratteri di controllo
  VERI (backspace, form feed) dentro le regex — la regex non trovava niente, **senza errore**. Modifiche ai `.py`
  solo con l'editor.

**Cosa NON si estrae (serve l'occhio dell'AOD):** carte a terra e ostacoli (94/106 pagine immagine), avvicinamenti
(134/178), VRP di chi li ha solo in carta, confini/costa delle aree. Per queste la checklist (§10) dice *dove*
guardare, e basta.

**Dove va:** è la porta d'ingresso di F6 e del ramo `airac/AYYMM` (decisione 6): Cover → checklist → ramo → PR con
`CHANGELOG/AYYMM.txt` precompilato (decisione 12). Non richiede F1-F5: si può anticipare come comando dell'app.
