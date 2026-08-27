# HANDOFF — vIPI/vLOA Interactive

**Ultimo aggiornamento:** 27 agosto 2026, **notte** (il ramo `basemap-esri`: il fondo delle mappe e le chip morte).

## Dove siamo, prima di tutto il resto

🔷 **UN RAMO APERTO: `basemap-esri`**, spinto e **non fuso**, due commit e due lavori distinti — nati
entrambi da una segnalazione del committente, non da un piano:

| commit | cosa |
|---|---|
| `95e4227` | **Il fondo delle mappe non è più CARTO** ma Esri «Light Gray Canvas»: le tessere arrivavano stampigliate «API KEY REQUIRED», in produzione, su tutte le mappe |
| `1c15f81` | **Le chip METAR/TAF e la pista delle SID non facevano niente**: la vIPI d'aeroporto è SSR statica e lo stato dei suoi comandi era rimasto nel genitore |

I due stanno insieme solo perché sono usciti la stessa sera; **si possono scorporare** in due rami se la
fusione conviene separata.

`main` è fermo a **`4811813`** (spinto), che porta l'**audit delle prestazioni** (§O) e `riordino-e-aree`.
Punto di ritorno di quella fusione: **`main-prima-del-merge-20260827`** (`963e9aa`).

**Suite 5989** su **15** progetti con esito (erano 5981: quattro test nuovi sulle chip, contati due volte
perché `Vipi.Ui.Tests` gira su net8 e net10), build Release della soluzione intera **0 avvisi**, **nessuna
migrazione nuova** (restano diciannove).

### Il ramo aperto, in cinque righe

**Il fondo delle mappe.** CARTO ha chiuso il basemap anonimo. ⚠️ Il guasto era **invisibile alle nostre
reti**: il ritentatore e lo spazzino guardano la tessera che *non arriva*, e questa arrivava — `200`, immagine
valida — con la scritta sopra. Ora è Esri, su un host che il prodotto interrogava già (il rilievo delle
minime). 🔵 Resta la **categoria**: gratuito e senza contratto, come CARTO fino a ieri; l'unica strada che
non si ripresenta sono le tessere nostre. Carta: `docs/feature/2026-08-27-basemap-esri.md`.

**Le chip morte.** ⚠️ Due forme dello stesso difetto, e la seconda inganna: `AirportWeather` non era mai
stato promosso a **isola** (il clic non partiva nemmeno), mentre per le SID l'isola c'era e il clic
**arrivava al server** — ma lo stato viveva nel genitore statico, che non si ridisegna più. Regola che resta:
*uno stato che cambia vive **dentro** l'isola che lo cambia; un genitore statico può solo **seminarlo***.
⚠️ E **bUnit ignora i render mode**: quella classe di difetto la vede solo il browser vero. Propagazione
fatta sulle undici pagine pubbliche statiche: nessun altro comando muto. Carta:
`docs/feature/2026-08-27-chip-morte-pagina-statica.md`.

### L'audit delle prestazioni, in cinque righe

Revisione della responsività **tenendo conto dell'ambiente di produzione** — Plesk + Passenger, una sola
istanza senza backplane, MariaDB sulla stessa macchina, Cloudflare davanti, aggiornamento via FTP. Non
letta: **misurata**, con l'applicazione compilata in Release su una copia del `vipi.db` reale.

```
prima visita   336 192  ->  113 052 byte     -66%
avvio            465    ->      153 query,  e zero UPDATE inutili
```

Lo **stato stazionario era già sano** (trenta richieste concorrenti: p50 16 ms, p90 34 ms). Il costo stava
nei byte spediti, nell'avvio, e in ciò che impediva a qualunque cache di aiutare. Carta completa con tutte le
misure: [`docs/history/audit-2026-08-27-prestazioni.md`](docs/history/audit-2026-08-27-prestazioni.md);
voci aperte in `docs/lavori-aperti.md` **§O**.

⚠️ **Il filo: quattro difetti su otto sono default del framework mai scritti.** Il livello di compressione
(`Fastest`, che per Brotli è la **qualità 1**: attivarlo faceva scaricare il **24% in più** che non averlo),
il livello di log (`Information`, che per EF significa il testo di **ogni query su disco** — 1 MB ogni 210
pagine), un `@rendermode` sull'**ingresso del sito**, che non ha un solo comando, e un `DateTime.UtcNow` dove
serviva il timbro della sorgente. Nessuno somiglia a un difetto: non danno errore, e tre su quattro rendono la
configurazione *più* ricca a leggerla.

⚠️ **Due interventi pianificati sono stati SCARTATI SU MISURA**, e la misura sta **nel codice** perché nessuno
li rifaccia dalla stessa ipotesi: **ReadyToRun** (+29 MB su un deploy solo-FTP per un 2% dentro il rumore — il
cronometro d'avvio nuovo dice perché: **1 172 ms su ~1 300 sono database**, non compilazione) e la
**deduplicazione dei poligoni AoR** (guardava i byte **grezzi**: compressi, arrotondare le coordinate e
togliere i `&quot;` fa uscire **più** byte).

⚠️ **E quattro test sono passati per il motivo sbagliato prima di essere corretti** — è il rischio dominante
di questo tipo di lavoro, ed è raccontato uno per uno in fondo alla carta. Più un errore di **verifica**:
`grep -c "^Failed!"` diceva «0 falliti» mentre un progetto **non compilava**. Da qui in avanti la verifica è
`dotnet build Vipi.slnx` **prima**, e poi il conto dei **progetti con esito (15)**, non dei falliti.

🔵 **Due cose aspettano il committente, e non sono codice** (§O2, §O3):
- la **Cache Rule su Cloudflare** (`/services/` → *Eligible for cache* → *Respect origin TTL*): senza, il
  bordo non tiene l'HTML e metà del guadagno della cache resta inespressa. Istruzioni in `LEGGIMI-DEPLOY.md`;
- sul Plesk: **`passenger_min_instances ≥ 1`** (a processo spento i dodici hosted service non girano) e
  **`proxy_read_timeout ≥ 100s`** nelle direttive nginx aggiuntive — ⚠️ `nginx-vipi.conf` **su quel server
  non lo carica nessuno**.

⚠️ **Al primo avvio dopo il deploy** la proiezione allinea i timbri dei settori **una volta sola** (312
UPDATE); dal secondo in poi tace. È previsto.

### Il giro di prima (§N), fuso nello stesso commit

- **`ff8ec29`** — il **trascinamento nel menu Navigazione non ha mai funzionato col mouse**, su tutte e tre
  le famiglie, pur essendo stato dichiarato eseguito e verificato il 26. `@ondragover:preventDefault` sulla
  voce era lettera morta: **Blazor ascolta un evento solo se qualcuno vi registra un GESTORE**, e per
  `dragover` non ce n'era. Senza quel `preventDefault` il bersaglio non accetta e il browser annulla il
  gesto **senza errori e senza segni**. Cura: `wireTocDrop` in `vipi-ui.js`.
- **`6d0c309`** — le **aree regolamentate** non hanno più una mappina per area (105 su LIRR): una mappa sola
  riusata dall'AoR, una chip per area, i preset per tipo, e sotto le descrizioni delle sole accese.

- **docs** — carte, indice e questo file; e la **Guida in-app**, che mostrava a schermo i tag escapati
  (`<b>` letterale) e gli apostrofi raddoppiati in cinque sezioni, da mesi: il corpo è una `MarkupString`.
  Ora due test lo presidiano. In più una sezione nuova, «Leggere le aree regolamentate». §N5.

ℹ️ **Il fondo delle mappe e le chip morte** sono usciti dopo, la notte del 27, e stanno sul ramo
`basemap-esri`: raccontati in cima a questo file, non qui — questo blocco è la storia di `riordino-e-aree`.

⚠️ **La lezione di §N, che vale oltre il difetto**: né gli otto test bUnit né la verifica live guardavano il
pezzo rotto, perché **fabbricavano** gli eventi di trascinamento — dispacciando da sé proprio il `drop` che
nella realtà non arrivava. **Un gesto del browser si prova col browser che lo fa** (CDP
`Input.setInterceptDrags`, headful): script e lezione nella skill `verifica-live`.

---

✅ **Per il resto, sul codice non resta lavoro aperto.** Il 27 pomeriggio sono chiuse in un giro solo le ultime sei voci —
**C7a/b/c** (l'ACC estero che nasceva con le aree accese, il regime di scrittura mai deciso, le cancellazioni
strutturali mute), **C6** (la chiave di release che si sposta ora si **ripunta**), **H3** e **H1** (le due
misure sbagliate dell'interfaccia) ed **E9**, dove la corsa sul `DbContext` si è finalmente **riprodotta**:
la prima operazione è `HasAnyGrantAsync`, la domanda «hai qualcosa da modificare?» del layout. Racconto in
`docs/lavori-aperti.md` **§M**. Build Release `--no-incremental` **0 avvisi**, **nessuna migrazione nuova**
(restano diciannove). Tutto fuso in `main` (**`290b833`**, spinto) e il ramo del giro è stato **cancellato**,
locale e su origin: di nuovo un albero solo.

⚠️ **La trappola che §M lascia in eredità**: le due guardie della corsa (layout che conclude prima del render,
pagina con scope proprio) **bastano ognuna da sola**. Chi ne togliesse una non vedrebbe rompersi niente.

✅ **Un albero solo**, e stavolta senza asterischi: vedi la testata. I tre rami in
fila del 26 sono stati fusi il 27 mattina (`docs/lavori-aperti.md` **§B12**), e da lì è partito e si è chiuso
un giro nuovo: l'**audit dei quattro documenti** (**§L**, carta
[`docs/refactor/14-quattro-documenti.md`](docs/refactor/14-quattro-documenti.md)).

**Suite 5981 verdi** su `main`, contate il 27 sera tardi dopo la fusione dell'audit prestazioni (erano 5746
il pomeriggio).
⚠️ La cifra si **conta**, non si ricorda — e su net10 due progetti non girano per costruzione
(`Vipi.AuroraBridge.Tests` e `Vipi.E2E.Tests` sono net8 soli: su net10 rispondono `NETSDK1005`, ed è atteso).
⚠️ Prima di credere a un conteggio: `grep "error MSB"`. Con un `Vipi.Host` acceso i suoi DLL sono bloccati,
mezzo albero non compila e il totale cala di centinaia senza che il comando diventi rosso.
⚠️ **E non basta contare i «Failed!»**: se un progetto non compila non produce nessuna riga di esito, quindi
zero falliti può voler dire zero eseguiti. Il 27 sera è successo. Si costruisce PRIMA
(`dotnet build Vipi.slnx -c Release`), e poi si contano i **progetti con esito: devono essere 15**.

⚠️ **DICIANNOVE migrazioni in coda** al cutover MariaDB (erano diciassette: l'audit versioni & release ne ha
portate due). ⚠️ **Prima del deploy serve la SELECT dei duplicati su `DocReleases`**, o `CREATE UNIQUE INDEX`
fallisce. ⚠️ **Tre passi d'avvio** idempotenti: `LinkAirportDocumentsAsync`, `ClearVloaSeededAiracRowAsync`,
`ClearUnpublishedCurrentVersionAsync`.

🔵 **L'unica cosa che aspetta il committente sul codice: ripubblicare le quattro vLOA.** La correzione del
ciclo AIRAC doppio **non arriva al pubblico da sola** — la pagina legge lo snapshot della release, e gli
snapshot non si riscrivono. Quattro clic; la lista «Da fare» le indica dopo il primo giro notturno (§L2).

## Il giro dei quattro documenti, in cinque righe

I quattro tipi previsti da direttiva — vIPI ACC, vIPI APP non remotizzato, vLOA, vIPI d'aeroporto — condividono
ora anche il **guscio dell'editor**, il **ciclo delle sezioni del viewer**, la **nascita del documento** e un
**enum solo**. L'aeroporto è rientrato nel modello: editor **2180 → 1001** righe, e la sua pagina pubblica è
tornata **SSR statica** come le altre tre.

⚠️ **Quattro cose da sapere prima di rifare qualcosa di simile** (per esteso in §L4):

1. **«Un componente, due modi» non vale dove lettura e scrittura hanno forme diverse** — e nell'aeroporto le
   hanno *per una ragione*: la lettura dev'essere serializzabile per il congelamento della release.
2. **Per togliere il circuito a una pagina non serve separarne la rotta**: il render mode si dichiara sul
   **componente**, quindi bastano le isole e gli indirizzi pubblici non si toccano.
3. **`IReleaseTarget` non può avere un `EnsureDocumentIdAsync`**: fa **ciclo** in DI.
4. **Una scelta che sembra da fare può essere il residuo di una già fatta**: il doppio significato di
   `CurrentVersionId` lo teneva in vita **codice morto**.

⚠️ **E tre difetti trovati SPOSTANDO il codice, non leggendolo** (§L5): una conversione di regola pista
scritta due volte a quattrocento righe di distanza, un `MarkDirty("SID")` che nessuno salvava, sette frasi
mai tradotte. Le due metà stavano sempre lontane: è il motivo per cui si sono viste solo muovendole.

## Le due reti, che valgono più dei passi

- **`ParitaQuattroDocumentiTests`** — le stesse domande di comportamento ai **cinque profili**.
- **`NascitaDocumentoParitaTests`** — le stesse domande alle **quattro porte di nascita**.

Il catalogo delle sezioni aveva già invarianti provate su tutti i profili, ed è per questo che quella parte
non divergeva. Per il **comportamento** non esisteva l'equivalente, e **ogni** divergenza trovata da questo
audit era passata attraverso una suite verde. Chi aggiungerà un quinto documento le eredita.

---

<details><summary>Lo stato precedente (26 agosto), per chi rilegge</summary>

> **Il ramo `identita-settori`, in tre righe.** Nasce da una domanda del committente — *svincolare il settore
> dal suo nome* — e porta tre lavori (più le chiusure della notte del 26, qui sotto). **(1)** L'identità dei cataloghi passa dal callsign all'**id numerico che
> IVAO manda già**: una rinomina diventa un `UPDATE` e `Sector.Id` le sopravvive, con documento, accordi, vLOA
> e figli. **(2)** Un `regionMapPolygon` vuoto non cancella più la shape che abbiamo: l'assenza non è un ordine
> di cancellare — misurato, **83 poligoni su 83** azzerati da un solo import. **(3)** Le shape di CTR/APP/MIL/FSS
> arrivano dal **sectorfile Aurora** come ripiego, con un **gate AIRAC**, perché quel file lo scriviamo prima
> che il ciclo esca.
>
> Carte: [identità dei settori](docs/feature/2026-08-26-identita-dei-settori.md),
> [l'assenza non cancella](docs/feature/2026-08-26-lassenza-non-cancella.md),
> [le shape dal sectorfile](docs/feature/2026-08-26-shape-dal-sectorfile.md).
>
> **E un quarto lavoro, che coi settori non c'entra**: arrivato dal committente il 26 sera, mentre il ramo era
> aperto. **L'ordine delle sezioni è una scelta editoriale** — anche le sezioni di **catalogo** si spostano su e
> giù dentro il loro gruppo (i fratelli: il blocco per la vIPI ACC, la radice per APP e vLOA), restando non
> rinominabili e non eliminabili, e ognuna dice di quanti posti si è allontanata dall'ordine standard (`↑2`,
> `↓1`). Il motore c'era già — `Order` versionato, `MoveSectionAsync` che scambia fra fratelli: mancava il
> tasto. ⚠️ Strada facendo è saltato fuori che il viewer della **vLOA** rendeva le due direzioni dei
> coordinamenti in una sequenza **scritta nel codice**: spostarle avrebbe cambiato l'editor e non il documento
> pubblicato. Carta: [l'ordine delle sezioni](docs/feature/2026-08-26-ordine-sezioni-personalizzato.md)
> (**J6** e **J7** chiuse: si riordinano anche i **blocchi** della vIPI ACC, con i settori di aerovia fissi in
> testa). **Nessuna migrazione.**
> **E la notte del 26 la sezione J si è chiusa tutta** (`docs/lavori-aperti.md` **§J**, nove voci, **nessuna
> aperta**). Le tre della notte:
>
> - **J1 — l'avviso a chi pubblica una shape non ancora in vigore.** Il gate AIRAC faceva già la cosa giusta,
>   ma **in silenzio**: chi pubblica vedeva a schermo il confine nuovo e nel documento ne trovava un altro. Ora
>   il **pannello release** elenca le aree che resterebbero indietro e offre «Pubblica comunque le aree nuove».
>   ⚠️ Nessuna regola nuova: la domanda la fa `ShapeAiracGate.IsDeferredAt`, la stessa del congelamento.
>   ⚠️ I cicli guardati sono **due**, perché i tasti sono due — si avvisa per l'unione.
>   ⚠️ Forzare **non** scrive «è in vigore»: la promozione notturna spegne la forzatura da sé.
> - **J2 — i ripieghi valgono solo per gli enti della divisione** (decisione del committente): *le aree degli
>   ATC esteri le dà IVAO, se ce le dà*. Sectorfile, GitHub `twrs.tfl` e cerchio 5 NM guardano solo la
>   divisione; regola in **un posto solo** (`ShapeFallbackScope`). Vale per i **ripieghi**: quel che IVAO manda
>   si scrive per tutti.
> - **J8 — il catalogo dei punti leggeva TRE file su OTTO.** `GODRA` e `GIGUS` non mancavano: stavano in
>   `NAVAIDS/ESTERNI.fix`, che non scaricavamo perché la configurazione elencava tre file **scritti a mano**
>   mentre `ITALY.isc` ne cita otto. Ora l'elenco lo dà l'indice, come già per i file di settore: **1385 →
>   3732** nomi in catalogo, e i blocchi di Milano chiudono l'anello. ⚠️ Gli irrisolti erano **tre**, non due
>   (c'era anche `GEMLA`): il difetto era la lista scritta a mano, non i nomi. ⚠️ Tocca anche i **suggerimenti
>   dei CoP** e la completion delle SID, che leggono lo stesso catalogo.
>
> **J3 chiusa da una risposta, non da codice**: gli undici settori senza area vanno bene così. Non hanno un
> volume proprio — sono **postazioni operative in più** sullo stesso cielo di qualcun altro (guidacaccia,
> planner, coordinamento) — e un poligono per loro sarebbe una finzione. ⚠️ Il dato non manca: **non esiste**.
> Non trattarlo come un buco da riempire in un giro futuro.
>
> ⚠️ Le tre chiusure **non aggiungono migrazioni**: restano diciassette.
>
> ✅ **IVAO ha confermato** (26 agosto, su richiesta del committente) che l'assenza dei poligoni dall'API è un
> **guasto loro** e che lo sistemeranno: il ripiego dal sectorfile è una rete, non una sostituzione, e il
> rientro dell'anagrafica è già provato.
>
> **Dove sta il lavoro:** ramo **`identita-settori`**, **28 commit** oltre `statistiche-atc` (110 oltre `main`),
> spinto su origin (testa `12c6c93`). Build **0 avvisi** su entrambi i TFM; contati il 26 agosto a notte:
> **2761 test su net8** e **2523 su net10**, tutti verdi (⚠️ la cifra si **conta**, non si ricorda — e su
> net10 due progetti di test non girano per costruzione: `Vipi.AuroraBridge.Tests` e `Vipi.E2E.Tests` sono
> net8 soli).

> **Il giro del 25 agosto pomeriggio, in due righe.** La sorgente IVAO sapeva già quali aeroporti hanno una base
> militare e lo scartavamo (34 su 221) — ⚠️ ma `military` **non** vuol dire «aeroporto militare»: Linate, Pisa e
> Ciampino sono nell'elenco. E provando a pubblicare LIBG è venuto fuori che la **vIPI d'aeroporto era legata a
> un suo settore** invece che allo scalo: chi non ha una torre — LIBG ha solo un APP non remotizzato — produceva
> un documento orfano a ogni apertura dell'editor, e non si pubblicava.
> Carte: [`docs/feature/2026-08-25-aeroporti-militari.md`](docs/feature/2026-08-25-aeroporti-militari.md) e
> [`docs/feature/2026-08-25-vipi-aeroporto-legata-allo-scalo.md`](docs/feature/2026-08-25-vipi-aeroporto-legata-allo-scalo.md).
**Scopo:** dare a una nuova chat tutto il contesto per riprendere senza rileggere l'intera cronologia.

> ## 🧭 SI RIPARTE DA QUI (25 agosto 2026)
>
> Per riprendere da freddo si legge **un** file:
> [`docs/feature/2026-08-24-servizio-statistiche-atc.md`](docs/feature/2026-08-24-servizio-statistiche-atc.md)
> — **§12** dice cosa resta, **§13** cosa è stato fatto il 25.
>
> **Dove sta il lavoro:** ramo **`statistiche-atc`**, una trentina di commit oltre `main`, spinto su origin
> (⚠️ la cifra si **conta** — `git rev-list --count main..statistiche-atc` — perché qui era rimasta «24»
> per due giri mentre il ramo era già a 27). Release **0 avvisi** su entrambi i TFM; **2254 test verdi su
> net8** e **2016 su net10**, tutti verdi (il rosso di §H2 è stato chiuso la sera del 25). **Non è fuso**:
> vedi §B12.
>
> **Cos'è.** Il **terzo servizio** dell'hub `/services`: le statistiche da ATC. ⚠️ Il fatto che decide tutto
> il resto — **IVAO dà le connessioni, non il traffico**: chi hai gestito lo costruiamo noi campionando l'AoR
> a ogni giro del poller che c'era già (stessa cadenza, stesse chiamate, in più i piloti). L'AoR è
> **orizzontale E verticale**, un aereo va a **una sessione sola**, e i numeri sono **due** — movimenti e
> presenze — perché i volumi ACC partono da terra e conterrebbero ogni aereo posteggiato della FIR.
>
> **Il 25 agosto (§13), su richiesta del committente:** la veste, e le targhette che dicono **chi hai visto
> atterrare e chi no**.
>
> 1. ⚠️ **La regola, e vale per tutto quel che ci si costruisce sopra: una targhetta dice quel che si è
>    VISTO.** Un volo diretto al tuo campo che esce dall'area ancora in volo **non è «atterrato»** — è
>    «uscito in volo», o «consegnato a LIRR_NE1_CTR» se sappiamo chi l'ha preso. Sta in `TrafficStory`
>    (puro, con test), e la usa **anche il filtro** della pagina: una seconda copia nel markup si
>    scollerebbe dalla prima al primo cambiamento.
> 2. **Otto colonne nuove** su `AtcSessionTraffic` (fasi, quote, consegne) e **nessuna chiamata in più**: la
>    fase la calcolava già il recorder a ogni giro e la buttava. ⚠️ La consegna si scrive solo fra **due giri
>    consecutivi**: a poller fermo, «prima era tuo e ora è suo» è un buco, non un passaggio.
> 3. **La striscia del turno** nel dettaglio sessione, con la punta di traffico dichiarata **stimata** — la
>    barra è la finestra fra primo e ultimo avvistamento, non la presenza.
> 4. **Periodo e filtro nell'indirizzo** (`?p=30`, `?f=arr`): è ciò che tiene le pagine **SSR statiche**.
>    E sotto i 700px di **contenitore** (`@container`, non `@media`: lo zoom è `zoom` e le media query non lo
>    vedono) le tabelle diventano schede.
>
> ⚠️ **La lezione del giro**, ed è la stessa di sempre scritta in modo nuovo: suite verde e Release pulita
> **prima** di guardare le pagine, e poi la verifica live ha trovato **sette difetti** — due dei quali erano
> **conti sbagliati**, non estetica (la ciambella calcolata sulle prime venti postazioni invece che su tutte;
> un riquadro che contava le righe della classifica tagliata a cinquanta e le chiamava «controllori»).
>
> ⚠️ **Non ancora visto dal vivo**: le targhette di fase e le consegne. Le colonne nascono adesso e si
> riempiono dal **primo turno campionato dopo il deploy**; sulle righe già in archivio restano vuote, e in
> quel caso la pagina non scrive targhette di fase.

> ## 🆕 ANCHE, la sera del 25 agosto — due cose fuori dalle statistiche
>
> Stesso ramo, perché è dove si stava lavorando. Nessuna delle due tocca il modello o le migrazioni.
>
> **1. Il VID è una porta sul profilo IVAO** — chiesto dal committente: cliccando un VID, in qualsiasi
> pagina, si apre `https://ivao.aero/Member.aspx?Id=<VID>`. Quindici punti in dieci file, **un componente
> solo** (`Components/VidLink.razor`), che è anche l'unico posto dove quell'indirizzo è scritto.
> Carta: [`docs/feature/2026-08-25-vid-porta-sul-profilo-ivao.md`](docs/feature/2026-08-25-vid-porta-sul-profilo-ivao.md).
> ✅ **Verificato dal vivo**, e la verifica ha trovato **un buco vero**: nel Registro nove VID erano a
> schermo e **zero** erano link, perché lì il VID sta dentro le frasi del narratore — non è un campo, è una
> **parola**. ⚠️ Nessuna prova sbagliava: nessuna guardava quella colonna. Chiuso con un secondo componente,
> **`VidText`**, che taglia la frase già composta sulla forma «VID 1234567» ed emette il resto come testo
> (niente `MarkupString`: quelle frasi portano dentro titoli e note scritti da persone). Aggancia anche il
> «Deciso da …» di Sorgenti e l'«Assegnato da …» di Incarichi, che erano dati per irrisolvibili.
>
> ⚠️ La regola che ne esce, per chi ci aggancia il prossimo punto: **il link sta dove il VID si vede già**,
> non dove lo si potrebbe ricavare. Dove a schermo c'è il nome (Registro, `ReleasePanel`, Incarichi) resta
> il nome: la colonna `.c-who` è larga quanto un nome, e «(VID 123456)» su 500 righe la taglia.
>
> **2. ✅ Il rosso «intermittente» di `Vipi.Application.Tests` è chiuso** (§H2, aperta dal 23 agosto) — ed
> erano **due** difetti, non uno.
> Il primo: `Il_rapporto_fra_i_lati_e_quello_vero` ricalcolava `cos(latitudine media)` sui punti
> **generati** mentre il proiettore lo calcola su quelli **parsati**, e dal 25 agosto `ParsePoints` toglie i
> gemelli consecutivi (374,008 contro 371,701: esattamente i due numeri dell'asserzione).
> Il secondo l'ha trovato il **martello**: rilanciate a 200 000 giri invece dei 100 di default, è caduta
> subito un'altra proprietà — i punti del path confrontati con `Assert.Equal(…, 0)`, che non è una
> tolleranza ma un secondo arrotondamento con un secondo mezzo su cui cadere. Il file quel difetto lo aveva
> già curato sul **viewBox** dimenticando il **path**.
>
> ⚠️ **La lezione, generale:** una proprietà CsCheck non è ballerina — cade **per certi sorteggi**, e il modo
> di trovarli è **alzare le iterazioni**, non rilanciare finché passa. Se una di queste torna rossa, il primo
> gesto è `CsCheck_Iter=2000000`.
>
> Verificato a **2 milioni di giri su entrambi i TFM**, controesempio congelato in
> `AorPolygonProjectorTests`, e suite completa di nuovo **tutta verde**: **2254 net8 / 2016 net10**, Release
> `--no-incremental` **0 avvisi**.

> ## ⚪ STORIA — quattro difetti chiusi (23 agosto 2026)
>
> Per riprendere da freddo si legge **un** file:
> [`docs/feature/2026-08-23-quattro-difetti-e-le-proprieta.md`](docs/feature/2026-08-23-quattro-difetti-e-le-proprieta.md).
>
> Quattro cose chiuse, tutte in `main`, tutte con la loro voce in
> [`docs/lavori-aperti.md`](docs/lavori-aperti.md). In ordine di quanto sono costate a capirle:
>
> 1. **Aurora, il test ballerino era un difetto vero** (E6-ter). `AuroraClient.SendAsync` si connetteva
>    *prima* di prendere il turno: due invii insieme aprivano un socket a testa, e `stream` e canale si
>    leggevano in due istruzioni separate — si poteva scrivere su un socket e aspettare la risposta sul
>    canale dell'altro. Inseguito per undici giorni come «lentezza del thread-pool». Visto fallire: **200
>    giri su 200** col client vecchio.
> 2. **Le tabelle del viewer sforavano a zoom alto**, e il colpevole non era quello scritto ieri
>    (`.rwy-table`, non `.sid-table`). ⚠️ La regola generale che ne esce: **lo zoom di questa applicazione è
>    `zoom` sull'`<html>`, e le media query non lo vedono**. Una soglia in `@media` è cieca allo zoom — vale
>    per la topbar, per le tabelle e per la prossima. 144 combinazioni verificate guidando Edge.
> 3. **La lingua non arrivava al circuito**: in Blazor Server le richieste sono due, e `/_blazor` non porta
>    `?culture=it`. Cookie scritto **solo** su richiesta esplicita.
> 4. **Admin = tutto lo staff di divisione** (E4), decisione del committente: il default è il jolly
>    `^IT-[A-Z0-9]+$`, e quattro staffisti veri smettono di restare fuori.
>
> Più i **test property-based sull'AoR** (E5, CsCheck), che hanno trovato un commento che diceva il falso
> sull'ordine delle coordinate.
>
> Cancello: `dotnet build Vipi.slnx -c Release --no-incremental` (**0 avvisi**), suite **verde su net8 e
> net10** (E2E compresi, eseguiti in Release: i `bin/Debug` erano bloccati dall'app in esecuzione).
>
> ⚠️ **Il blocco al deploy non è cambiato**: la MariaDB di produzione va convertita agli accordi a sezioni
> prima di pubblicare (E6-bis §9), o `AgreementSectionsFinalize` fallisce all'avvio.

> ## ⚪ STORIA — il giro degli import (22 agosto 2026, sera)
>
> **L'ultimo lavoro è in `main`**: il ramo `sorgenti-giro-ta-piste` è stato fuso e cancellato (merge
> `9be2200`). **Non resta nessun ramo con lavoro fuori.** Per riprendere da freddo si legge **un** file:
> [`docs/feature/2026-08-22-sorgenti-giro-automatico-ta-piste.md`](docs/feature/2026-08-22-sorgenti-giro-automatico-ta-piste.md).
>
> In due righe: delle sei righe di `/services/vsop/admin/sources` quattro giravano da sole ogni giorno e due
> no — **Transition Altitude** e **Piste** arrivavano solo premendo un bottone, quindi una TA cambiata in
> AIRAC poteva restare vecchia a tempo indefinito mentre la pill diceva «su richiesta», che è vero e non dice
> **quanto** è vecchio il dato. Ora girano **tutte**, e con loro l'**anagrafica aeroporti**, che nell'elenco
> non compariva affatto. **Nessuna riga resta «su richiesta»**, e un test lo pretende.
>
> I due motori nuovi passano per lo **stesso** corpo dei bottoni (`SourceMergeInputs` + `MergeFromSourceAsync`
> per TA/piste, `IAirportImportUseCase` per l'anagrafica): niente secondo percorso che possa divergere sulla
> policy. Nessuna entità nuova e **nessuna migrazione**: non allunga la coda ferma per il cutover MariaDB.
>
> ⚠️ **L'anagrafica aeroporti è l'unico giro che CREA entità** (aeroporto + catalogo settori). Era stata
> lasciata a mano di proposito; è stata automatizzata su decisione del committente. È **additiva**: uno scalo
> tolto dalla sorgente resta in archivio e si toglie a mano.
>
> ⚠️ **L'ordine dei `bootDelay` non è estetica**: ACC 15s → aeroporti 25s → SID 30s → settori 40s → TA/piste
> 50s. I tre giri in coda iterano gli aeroporti che il secondo crea; invertirli lascerebbe uno scalo nuovo
> senza settori e senza piste fino al giorno dopo. Verificato live: il giro ha assegnato **LIDS (Parco
> Livenza)**, che IVAO aveva aggiunto, e quello dopo ha aggiornato **93** aeroporti, non 92.
>
> ⚠️ **Due cose che il deploy deve sapere** (anche in [`docs/lavori-aperti.md`](docs/lavori-aperti.md) §B9):
> in produzione comparirà **LIDS** al primo giro; e i **21 aeroporti senza TA** si popoleranno da soli,
> ricalcolando i TL delle fasce *default*. La policy vera va guardata **prima** in
> `/services/vsop/admin/sources`: in sviluppo `ImportPolicies` è **vuota**, quindi i valori a video vengono dai
> default delle colonne e non da una decisione di qualcuno.
>
> ⚠️ **Nessun giro rigenera i documenti**: import e generazione restano scollegati (doc 03 §4.3), quindi il
> dato nuovo entra nel sito al prossimo «Genera documenti».
>
> ⚠️ **Trappola di verifica pagata qui**: la pagina sembrava non aggiornata perché l'app girava da un
> `dotnet run` avviato **dodici minuti prima** del commit che accendeva il giro. Il `.dll` in `bin/Debug`
> portava una data più recente (l'avevano riscritto i `dotnet test`), ma il processo tiene in memoria quello
> caricato all'avvio. Prima di dare la colpa al codice: **guardare l'ora di avvio del processo**, non la data
> del file.
>
> Cancello: `dotnet build Vipi.slnx -c Release --no-incremental` (**0 avvisi**); test **591 + 450 + 255 + 57**
> verdi su net8 **e** net10 (E2E non eseguiti: i `bin/` erano bloccati dall'app in esecuzione).

> ## 🧭 ⚪ STORIA — catalogo dei punti (22 agosto 2026), **FUSO IN `main`**
>
> ⚠️ Questo blocco diceva «si riparte da qui»: **non è più l'ultimo lavoro** — dopo è arrivato il giro degli
> import (blocco sopra). Resta perché quanto racconta è tutto ancora vero.
>
> Il ramo `catalogo-punti-suggerimenti` è stato fuso e cancellato la sera del
> 22 agosto (merge `2b4480d`). Per riprendere da freddo si
> legge **un** file:
> [`docs/feature/2026-08-22-catalogo-punti-suggerimenti.md`](docs/feature/2026-08-22-catalogo-punti-suggerimenti.md).
>
> In due righe: i nomi di fix/VOR/NDB che il parser SID già scaricava da GitHub **escono da Infrastructure** e
> diventano una porta (`INavaidSource` + `NavaidCatalog`), così i campi dove si scrive un punto a mano —
> FIX e Transition delle SID manuali, correzione fix delle importate, CoP delle clausole — **suggeriscono** i
> nomi veri e **segnano** quelli che non esistono. Nessuna tabella nuova in database: il catalogo vive in
> memoria, quindi **non aggiunge niente alla coda di migrazioni ferma per il cutover MariaDB**.
>
> Nello stesso giro gli **alias dei fix** smettono di essere invisibili: si vedono e si tolgono da
> `/services/vsop/admin/sources`, il loro bersaglio passa per lo stesso controllo, e ogni ciclo d'import
> elenca nei log quelli che puntano a nomi inesistenti.
>
> ⚠️ **Trovato un typo vero in archivio e NON corretto:** il CoP `BESIV` dell'accordo `LIBB_ES_CTR ⇄ LDZO_CTR`
> non esiste nel sectorfile (a una lettera c'è `BEKIV`). Correggerlo è una decisione editoriale e la prende chi
> conosce l'accordo — sta in [`docs/lavori-aperti.md`](docs/lavori-aperti.md) §E2.
>
> ⚠️ **Verifica live: due deroghe alla skill `verifica-live`.** `Sectorfile__RawBaseUrl` va lasciato **acceso**
> (spento, il catalogo è vuoto e non si verifica niente); e nella pagina accordi bisogna prima **prendere il
> lock** di struttura, o i tasti di riga restano spenti e il pannello non si apre mai. Sono scritte nella skill.
>
> Cancello: `dotnet build Vipi.slnx -c Release --no-incremental` (**0 avvisi**) e `dotnet test Vipi.slnx`
> (**1 677** verdi su net8).

> ## 🧭 ⚪ STORIA — densità UI delle pagine admin (22 agosto 2026), **FUSA IN `main`**
>
> ⚠️ Questo blocco diceva «il lavoro vivo è sul ramo `ui-trasferimenti-densita`, non fuso»: quel ramo **non
> esiste più**, è in `main` dal 22 agosto. Resta qui perché il METODO e le misure valgono ancora.
>
> Per riprendere
> da freddo si leggono **due** file:
> [`docs/history/handoff-densita-ui.md`](docs/history/handoff-densita-ui.md) — dove siamo, il metodo, e da dove
> riparte la prossima pagina — e
> [`docs/design/regole-ui-pagine-admin.md`](docs/design/regole-ui-pagine-admin.md), le 124 regole già pagate più
> la ricognizione misurata di ogni pagina.
>
> In due righe: il giro riscrive **la forma** delle pagine di lavoro admin — niente cambia in modello, rotte o
> dati. **Nove pagine chiuse** (accordi, struttura, ACC, aeroporti, editor aeroporto, editor ACC, Confinanti,
> Versioni, **Permessi**). Versioni è costata due giri — prima la **sostanza** (la pagina lasciava eliminare un
> documento che un'altra persona stava editando), poi la densità: 1 664 → **900px**, il dettaglio fuori
> dall'elenco e i chip che contano. Permessi: **2 449 → 900**, le sei card di navigazione diventate una barra
> sola e completa, e l'elenco riorganizzato per **persona**.
>
> ⚠️ **La ricognizione di Permessi diceva 1 346px: era la misura a tabella VUOTA.** Le pagine che nel DB di
> sviluppo non hanno dati vanno **riempite prima di misurarle** — vale per quelle che restano.
>
> Poi, il **22 agosto**, un giro che non è di una pagina ma della **testa di tutte e undici**: la barra
> `AdminNav` sta ora **sopra il titolo** di ogni pagina admin, **al posto della briciola di pane**, e ogni sua
> voce si porta dietro la propria regola d'accesso — cambiare chi entra in una pagina è **una riga**, non
> undici `@if` (regole 125-132, §21).
>
> ⚠️ **La prossima è AUDIT, non più Sorgenti.** `/vsop/admin/audit` è passata da **1 166 a 1 556px** senza che
> nessuno la toccasse: le righe erano 20 alla ricognizione, sono 28 adesso. È l'unica pagina dell'elenco la cui
> altezza **cresce da sola per sempre** — un registro non si accorcia — quindi il `thead` appiccicato lì si
> ripaga, al contrario di quanto diceva la riga vecchia della ricognizione. **Una misura è una fotografia: su
> una pagina che accumula va rifatta, non citata.** Poi **Sorgenti** (**1 252px**), Diagnostica, Nuovo
> documento, Incarichi, editor APP/vLOA — l'ordine e il misurato stanno in `regole-ui-pagine-admin.md` §15.
>
> Cancello: `dotnet build Vipi.slnx -c Release --no-incremental` (**0 avvisi** — gli avvisi sono errori e
> `dotnet test` non li vede) e `dotnet test Vipi.slnx` verde su **entrambi** i TFM.
>
> Sotto resta lo stato del **18 agosto** (accordi di coordinamento, ramo `feature/accordi-coordinamento`) e
> quello del **15 agosto** (consegna a Ivao.It): valgono ancora per tutto ciò che non è quest'area.

> ## 🧭 ⚪ STORIA — accordi di coordinamento (18 agosto 2026), **FUSI IN `main`**
>
> ⚠️ Anche qui: `feature/accordi-coordinamento` **non esiste più**, è in `main` dal 22 agosto. Il contenuto del
> blocco resta valido come descrizione del modello.
>
> Per riprendere da freddo si legge **un** file:
> [`docs/history/handoff-accordi-coordinamento.md`](docs/history/handoff-accordi-coordinamento.md) — cosa c'è,
> cosa non va riscoperto a mani nude, cosa resta aperto.
>
> In due righe: `TransferFlow`/`TransferPoint` **non esistono più**, al loro posto un **accordo** fra due parti
> con due versi; e l'editor `/vsop/admin/trasferimenti` è stato rifatto sopra al modello — albero per
> **relazione** (`noi ⇄ loro`), **due versi sempre a vista**, creazione che chiede **solo i due enti**, tipo e
> aeroporti nella testata, **entrambi i lati obbligatori**.
>
> Cancello: `dotnet build Vipi.slnx -c Release --no-incremental` (0 avvisi) e `dotnet test Vipi.slnx`
> (**2581** verdi su net8 e net10).
>
> ⚠️ **Il `vipi.db` di sviluppo è il DB che va in produzione** ed è già travasato. **I suoi numeri non si
> scrivono nei documenti**: il committente lo modifica dal vivo dal proprio host sulla **5034** — mentre si
> chiudeva questo giro sono spariti due accordi — quindi si **misura** quando serve, non si cita. Tutte le
> prove di scrittura girano su una **copia** nello scratchpad; per la verifica live si usa un'altra porta.
>
> L'unica cosa che al 18 agosto era ancora vera per costruzione: **nessun accordo bilaterale**, cioè tutte le
> clausole in un verso solo. Il primo reciproco lo scrive chi usa «unisci i due versi» o «+ clausola» nel
> blocco entrante.
>
> Sotto resta lo stato del **15 agosto**, che riguarda la consegna a Ivao.It e vale ancora per tutto ciò che
> non è quest'area.

> ## 🧭 DA DOVE SI RIPARTIVA IL 15 AGOSTO (consegna a Ivao.It)
>
> **La consegna a Ivao.It è in corso, ed è lì che sta il lavoro.** Il database è stato caricato sul loro
> server; il pacchetto dell'applicazione va su via **FTP/FileZilla**, non da console — procedura in
> [`deploy/atc-ivao/LEGGIMI-FTP.md`](deploy/atc-ivao/LEGGIMI-FTP.md).
>
> **Pacchetto:** `artifacts/publish/vipi-linux-x64-mariadb-20260815.zip`, 48,1 MB, 407 file, self-contained
> net8, sha256 `28063F5E513A052C036593078FD2E3053165B174859246843CF56537B01C78EE`. Costruito da `main`
> dopo i due merge, con `Release --no-incremental` a **0 warning** e **2465 test verdi** (net8 + net10).
>
> ⚠️ **La trappola M14 si è materializzata proprio qui, ed è utile saperlo per il prossimo pacchetto.** Il
> restore di `publish -r linux-x64` **rivaluta le wildcard** `8.0.*`/`10.0.*` e riscrive i lock dei soli
> progetti `src/`: il pacchetto è nato con EF Core **8.0.30** mentre i progetti di test restavano a 8.0.29,
> e la corsa successiva della suite è morta con `CS1705` («uses a higher version than referenced
> assembly»). Rimesso in riga con `dotnet restore Vipi.slnx -p:RestoreForceEvaluate=true`, che aggiorna
> **tutti** i lock insieme, poi ricompilato, ri-testato e ripubblicato. Regola: dopo un `publish` con RID,
> guardare `git status` sui `packages.lock.json` **prima** di credere ai numeri della suite.
> ⚠️ Il pacchetto del 9 agosto e quello del 5 vanno **ritirati**: il primo non ha trasferimenti né audit
> database, il secondo non parla proprio MariaDB.
>
> ⚠️ **Chi carica non fa partire.** L'FTP non trasporta il bit di esecuzione e non installa servizi: serve
> qualcuno con shell per `chmod +x Vipi.Host`, `vipi.service` in systemd e nginx col WebSocket.
>
> ⚠️ **Se il loro database viene dal `.sql` del 9 agosto**, questa build applica da sé al primo avvio la
> migrazione `20260814092329_EnumLengthsAndDropUnusedTokens` — servono ALTER e DROP sul database.
>
> **⚠️ Un solo ramo resta pronto e non fuso.**
>
> ✅ **`feature/trasferimenti-acc-app` è stata fusa il 15 agosto** (72 commit), insieme a
> `fix/audit-database-14ago`. Nella collisione fra le due copie della stessa migrazione si è tenuta quella
> del ramo trasferimenti, l'unica il cui `Designer` descrive il modello fuso. La **PR #13 resta aperta** e
> va chiusa a mano dopo il push.
>
> ⚠️ **Resta ai colleghi, non al codice:** le righe con ricevente APP che non dicono ancora *dove* avviene
> il trasferimento vanno riviste a mano (15 nel DB di sviluppo, da rimisurare in produzione). Le elenca il
> filtro «Da rivedere» della pagina, che ora ha anche una vista a elenco fatta apposta per quel lavoro.
>
> Due cose **viste e non toccate**: `ITransferService.MovePointToEndAsync` non ha chiamanti dall'interfaccia
> (ha repository e test), e `LevelFormatting.Format` appende il suffisso di parità anche a un livello
> assente — a schermo esce «— (dispari)», che il round-trip regge ma si legge male.
>
> **Non fuso: `refactor/13-tre-documenti`** (suite **2111** verde su due TFM,
> verifica live fatta).
>
> ⚠️ **Quel ramo non compilava, e nessuno l'aveva visto.** L'audit dell'11 agosto ha trovato 14 chiavi
> duplicate nei `.resx`: il job CI che compila con `-warnaserror` dava **28 errori**, mentre la suite locale
> restava verde — *1391 test verdi e build di produzione rotta convivevano*. Corretto, con tre guardie.
> Adesso il ramo compila davvero, e la decisione di merge è di nuovo solo vostra. È il [doc 13](docs/refactor/13-audit-tre-documenti.md), l'audit dei tre documenti:
> catalogo fonte unica anche di «chi rende il corpo» e «quale sezione è obbligatoria», vLOA finalmente dal
> catalogo, gate pubblico su ricerca e «Cosa è cambiato», pannello release uguale nei quattro editor, una
> sola resa per ogni sezione comune. Dentro ci sono **due difetti che uscivano dal documento**: la pagina
> APP pubblica mostrava le configurazioni della bozza, e gli indici servivano documenti nascosti, sezioni
> nascoste e contenuto senza release. Il merge in `main` **aspetta l'ok esplicito** (come per il doc 10).
>
> Al primo avvio dopo il merge girano tre riconciliazioni one-shot (chiavi vLOA, placeholder «minima»,
> sezioni di catalogo mancanti): sul DB di sviluppo hanno toccato 15 sezioni e 18 blocchi.
>
> **Il ramo `feat/persistenza-mysql` è stato fuso in `main`**: il cutover non è più un ramo a parte. `main` è
> ora **net8 + Pomelo + MariaDB**, il Dockerfile pubblica su `aspnet:8.0`, e il deploy Render+Neon resta in
> piedi come **ambiente di prova** (decisione C3-bis: si riesamina dopo il cutover, non prima).
>
> **Le cose in mano a voi, non al codice:** consegnare `.sql` e pacchetto, le risposte di Ivao.It (A9/A10),
> la rotazione della password Neon, e quattro decisioni di contenuto — la SID `BANA8A` di LIBD, le 33 torri
> senza padre, **quali staff code valgono admin** (E4: ora i codici veri si vedono in diagnostica), e se
> pubblicare una *release* debba scrivere audit.
>
> **Metodo che ha pagato, in questa sessione più che mai:** nove difetti su undici sono usciti **guidando
> l'app**, non dai test — fra cui tre pagine che morivano su MariaDB, una direttiva nginx inesistente che
> avrebbe bloccato la consegna, e l'ATIS contato come chi controlla un aeroporto. Prima di dichiarare fatta
> una cosa, aprirla: la skill `verifica-live` esiste per questo.

> ## 🔬 AUDIT FULL-STACK — 11 agosto 2026, eseguito
>
> Carta ed esito: [`docs/history/audit-2026-08-11-crepe-full-stack.md`](docs/history/audit-2026-08-11-crepe-full-stack.md).
> 34 voci: **23 chiuse**, 3 **ribaltate dalla misura**, 5 rimandate con la ragione scritta. Sei commit.
>
> **Tre cose cambiano le regole, non solo il codice** — chi lavora qui le incontra subito:
> 1. **Gli avvisi sono errori** (`Directory.Build.props`). Un avviso nuovo ferma la build, in locale non solo
>    in CI. ⚠️ Un `--` dentro un commento XML rende quel file illeggibile e **tutte** le proprietà spariscono
>    in silenzio: c'è una guardia, ma vale saperlo.
> 2. **I test girano su net8**, che è la produzione: da **347** a **1115**. Prima ~1000 test non toccavano mai
>    il runtime del cutover.
> 3. **Le dipendenze sono bloccate** (`packages.lock.json` + restore in «locked mode»). Se la CI si ferma sul
>    restore: `dotnet restore --force-evaluate` e committa i lock.
>
> **Metodo che ha pagato, di nuovo:** tre voci sono state *ribaltate dalla misura* — i multi-poligono (zero
> casi su 1338 reali), la retention dell'audit (19 righe in tre settimane), le immagini orfane (1 riga).
> Misurare prima di toccare ha evitato tre lavori inutili. E due guardie nuove hanno **smentito affermazioni
> mie**: le chip a11y erano 8 e non 12, e tre progetti in `tools/` erano senza lock file.
>
> **Aperto dall'audit:** i 17 gestori inline che bloccano la CSP vera, `MapAll()` e il nonce OIDC (vanno con
> A10, servono un login IVAO vero), i file da 1500 righe, l'identità del circuito.

> ## 📋 COSA MANCA DA FARE → [`docs/lavori-aperti.md`](docs/lavori-aperti.md)
>
> Elenco unico di **tutto** l'aperto — cutover, branch non fusi, debito noto, verifiche live pendenti,
> funzionalità. Ogni voce è presa da sola in una sessione, con il blocco segnato (🟢 subito · 🟡 dipende da
> un'altra voce · 🔴 dipende da altri). **Partire da lì**, non da questo documento, che racconta lo stato
> ma non ordina il lavoro.

> ## ✅ IL CUTOVER È IN `main` — cosa sapere su `atc.it.ivao.aero`
>
> **Il server è MariaDB 11.4.10, il provider è Pomelo, `Vipi.Host` è net8.** Decisione vigente: ADR-0007
> **§D4-ter**, che supera §D4-bis (Oracle/net10/MySQL 8) come quella aveva superato §D4. Il
> [piano MySQL](docs/design/piano-supporto-mysql.md) descrive un bersaglio cambiato: leggerlo solo per
> l'analisi dei rischi, **non** per lo stato.
>
> **Cosa è già verificato contro una MariaDB vera** (6–9 agosto): schema e collation `utf8mb4_uca1400_as_cs`
> (163 colonne su 163), `LIRF`/`lirf` che convivono, travaso dei dati veri da Neon con `.sql` **riletto** in
> un database vuoto, key-ring Data Protection che sopravvive al riavvio, un job di CI su MariaDB 11.4.10
> Linux, e i **flussi editoriali guidati sull'app** (import, SID per aeroporto, pubblicazione dei tre tipi
> di documento, lock, ricerca, vista live, blob delle immagini byte-identici).
>
> **Il `.sql` da consegnare**: `_mariadb/dump/vipi-atc-it-ivao-aero-2026-08-09.sql`, 4 MB, sha256
> `1CD77F3A…`. **Il pacchetto di deploy**: `artifacts/publish/vipi-linux-x64-mariadb-20260809.zip`, 47,8 MB,
> self-contained net8. ⚠️ Quello del 5 agosto è compilato contro un provider che non parla MariaDB: **non
> funzionerà mai**, va ritirato.
>
> **Cosa resta, tutto in [`docs/lavori-aperti.md`](docs/lavori-aperti.md) sezione A:** consegnare il dump e
> il pacchetto, le domande a Ivao.It (A9: accesso al DB, `sql_mode`, privilegi, **`max_allowed_packet` ≥ 4
> MB**, backup, WebSocket sul proxy) e i redirect OIDC (A10).
>
> ℹ️ Il bug latente di `MigrateVipiDatabase` è **chiuso**: il dispatch è esplicito per provider e un
> provider senza strategia fallisce l'avvio con un messaggio che dice cosa fare.
>
> ⚠️ **Ogni cambio di schema va emesso DUE volte** — SQLite (`Vipi.Infrastructure`) e MySQL
> (`Vipi.Infrastructure.MySqlMigrations`). Tre test guardia lo pretendono, più il job CI `mariadb-schema` su
> MariaDB vera. E lo scaffold di EF va **riletto**: sull'ultima migrazione metteva il `DropColumn` prima del
> travaso dei dati, e su un database pieno i legami sarebbero spariti in silenzio.
>
> ✅ **B4 deciso il 7 agosto 2026: in produzione va `main` + B1.** `feature/aree-speciali-hardening` è
> fusa in `main` (fast-forward, 21 commit) e si porta dentro per intero `feature/aurora-bridge`, il cui
> endpoint `POST /vsop/api/v1/transfers/resolve` **nasce spento** (`AuroraBridge:Enabled=false`): entra
> come codice, non come superficie pubblica. Conseguenze: al primo boot su Neon l'archivio aree passa da
> 993 a 230 legami (poi «Importa da sorgente»), e **il `.sql` di A3 va rifatto dopo il merge**.
>
> ⚠️ Il **token app IVAO** non è più fra i bloccanti: il 5 agosto ha risposto 200 col secret dei user-secrets
> locali (dettagli e riserve nel blocco più in basso). Manca invece `VipiAuth:ClientSecret`, che in locale
> non è mai servito perché il login è spento: in produzione serve.

> ## ⏸️ RIMANDATO — embedding nel sito `Ivao.It.Website` (non è più la strada del sito definitivo)
>
> **Dal 5 agosto 2026 questo non è più il prossimo passo.** Il sito definitivo sarà servito dal nostro
> host standalone (blocco 🟢 qui sopra), non dalla RCL montata nel loro sito. Il lavoro qui sotto resta
> **valido e non buttato** — l'embedding è rimandato, non cancellato, e il multi-target `net8.0;net10.0`
> delle cinque librerie resta in piedi proprio per questo — ma non è ciò su cui si lavora ora.
>
> **Eseguire il modulo dentro un host net8 e guidarlo.** È il punto 3 del piano in
> [`docs/guide/integrazione-ivao-it-da-fare.md`](docs/guide/integrazione-ivao-it-da-fare.md) §5, e chiude
> **tre** voci aperte in una sessione sola:
> - **§2.1** — il modulo su net8 è stato solo **compilato, mai eseguito**. Restano non verificati i
>   comportamenti runtime di **EF Core 8** (sviluppato e testato solo su EF 10), il rendering della RCL
>   sotto **ASP.NET Core 8**, lo **stream SSE** `/vsop/live/atc` dietro la pipeline dell'host, e la
>   collisione di rotta fra `/vsop/live/{callsign}` e il prefisso SSE.
> - **§2.2** — **doppia localizzazione**: il modulo registra `AddLocalization` + `UseRequestLocalization`
>   dentro `AddVipiModule`/`UseVipiModule`, il sito registra `AddIvaoItLocalization` + `UseIvaoItLocalization`
>   che gira **dopo**. Chi vince decide la lingua di `/vsop`. Sintomo atteso: il `CultureSelector` del
>   sito non ha effetto sulle pagine del modulo, o entrare in `/vsop` cambia lingua al sito. Probabile
>   esito: un flag per non registrare la localizzazione del modulo quando l'host ne ha già una.
> - **§2.4** — **CSS**: il sito carica Bootstrap 5.3.3 e animate.css **globalmente**. Gli stili del modulo
>   sono confinati sotto `.vipi-root`, quindi il rischio è il contrario del solito: sono i loro a poter
>   sbavare dentro il nostro contenitore. Va guardato con gli occhi, non con i test.
>
> **Punto di partenza già pronto:** l'albero `Ivao.It-master` col modulo montato **compila** (0 warning,
> 0 errori). Per rifarlo da zero: copiare l'albero, `git am` di `docs/guide/ivao-it-wiring.patch`,
> materializzare il modulo in `external/vipi`, e — solo per compilare in locale — sostituire il
> `PackageReference` `Ivao.It.Logging` (feed privato loro, non su nuget.org) con il `ProjectReference` a
> `src/Common/Logging/Ivao.It.Logging.csproj`, che è già nel loro albero. Poi guidare con la skill
> `verifica-live`.
>
> ⚠️ Serve `VipiAuth`/identità: in embedded l'identità viene dall'host, quindi per la prova o si monta un
> `ClaimsPrincipal` finto sull'host di test, o si usa `useDevIdentity: true` in `AddVipiModule`.

> **🚧 Sessione 2026-08-03 — aree regolamentate: interruttore, import incrementale, dangling, appartenenza
> multi-ACC.** Branch `feature/aree-speciali-hardening`, 8 commit, suite **951 verde** (+24), build 0 warning. Carta completa:
> `docs/feature/2026-08-03-aree-regolamentate-hardening.md`. Le cose da sapere subito:
> - ⚠️ **`ImportSids` può essere spento in produzione senza che nessuno l'abbia deciso.** La migration dell'8 lug
>   aggiunse la colonna con `defaultValue: false`, e su Postgres `PostgresSchemaReconciler` backfillava a `false`
>   ogni bool NOT NULL nuovo: su un DB dove la riga `ImportPolicies` esisteva già, la categoria è nata spenta.
>   **Da guardare in `/vsop/admin/sorgenti`.** Non è ribaltabile da codice: `false` è indistinguibile da una scelta
>   dell'admin. Per il futuro il default sta nel modello (`HasDefaultValue`) e il reconciler lo legge.
> - **Le aree regolamentate ora hanno un interruttore** (categoria `SpecialAreas`): escluderle **congela** quelle in
>   archivio — l'import non le aggiorna e soprattutto non le pota. Gate in `SpecialAreaImportUseCase`, non
>   nell'hosted service, sennò il bottone di `/vsop/admin/accs` lo scavalca.
> - **L'import non riscarica più la shape** delle aree che ce l'hanno già (rinfresco a 30 giorni): era una chiamata
>   per area per ACC a ogni giro, solo per rileggere lo stesso poligono.
> - **Le aree selezionate in un documento possono sparire in silenzio**: gli id sono soft-ref senza FK e il prune li
>   può cancellare. Ora la diagnostica le segnala («Area regolamentata dangling», sola versione di lavoro) e
>   l'editor le marca «⚠ non più disponibile». Il prune resta libero di potare: si rileva, non si vincola.
> - **Un'area regolamentata può appartenere a PIÙ ACC** e prima ne tenevamo uno solo: `IvaoId` è unico e
>   `CenterId` era una colonna, quindi vinceva l'ultimo ACC in ordine alfabetico. La R49 «Zita» (id 8870), che su
>   IVAO è di LIRR e del militare LIZZ, risultava solo di LIZZ — ente nascosto — e spariva dalle aree proprie di
>   Roma. Ora c'è l'entità di legame `SpecialAreaCenter` (SPEC §9.23): import additivo, prune per legame, area
>   cancellata solo quando resta senza enti.
> - ⚠️ **Dopo il deploy premere «Importa da sorgente»**: il backfill recupera una sola appartenenza per area (era
>   l'unica che il vecchio modello sapeva); le altre le riporta il primo import. Su Postgres il travaso e il drop
>   della colonna storica li fa `ISpecialAreaMaintenance` al boot, non la migration — che lì non gira.
> - ⚠️ **Le aree estere spariscono dall'archivio al primo avvio** (763 legami su 993): `Acc.SpecialAreasEnabled`
>   nasce spento per gli `IsForeign`, e una riconciliazione one-shot al boot le libera. Restano le 230 italiane.
>   Se ne serve una, si riaccende quell'ACC con «Importa aree» in `/vsop/admin/accs` e torna. I documenti che ne
>   citavano una la vedono come dangling (diagnostica + marcatura nell'editor).
> - ✅ **Verifica live eseguita il 6 agosto 2026** (esito per esteso nella carta e in `docs/lavori-aperti.md` B1):
>   interruttore, dangling e aree estere confermati; la R49 «Zita» non è più elencata sotto LIRR dalla sorgente —
>   la meccanica multi-ACC funziona lo stesso, è l'esempio a essere invecchiato.

> **📄 Sessione 2026-07-30 (3) — uniformità dei tre documenti (vIPI ACC · vIPI APP · vLOA).** Branch
> `fix/uniformita-tre-documenti`, 17 commit, suite **640 → 663 verde**, verifica live confermata dall'owner.
> Carta completa: `docs/refactor/11-uniformita-tre-documenti.md`. Le cose da sapere subito:
> - **Il modello era unico, la rilettura no.** Ogni famiglia interpretava lo stesso `Document` a modo suo:
>   chiave di sezione, resa del contenuto editoriale, stato «nascosta», fallback della vista pubblica.
>   Sei difetti alti, tutti **invisibili ai test verdi** e trovati guidando l'app reale.
> - **Stato per-sezione ⇒ colonna su `DocumentSection`.** `IsHidden` (migrazione `AddSectionIsHidden`) e
>   `BeforeParentBody` (`AddSectionBeforeParentBody`) si aggiungono a `RenderMode` di doc 10: versionati e dentro
>   lo snapshot. Prima «nascondi» viveva in tre storage, due non versionati → **cambiava la pagina pubblica senza
>   pubblicare**. ⚠️ `CreateDraftAsync` non copiava i flag: aprire una bozza resettava `RenderMode` a `Frozen`.
> - **Chiavi di sezione univoche** (`custom:{guid8}`): la costante `"custom"` faceva collidere le sezioni libere.
>   Migrazione dati al boot (`IDocumentMaintenance`), non EF: le migration del repo sono SQLite-flavored.
> - **`?as=` non valido ⇒ pubblica CON derivate frozen.** Prima il fallback lasciava `_useFrozen=false`: il
>   congelamento AIRAC era bypassabile dall'URL.
> - **P7–P9 chiesti dall'owner in verifica live**: sotto-sezioni collocabili **prima** del corpo; coordinamenti
>   con il solo primo livello espanso; «Aree regolamentate» che nasce collassata (viewer **ed** editor).
> - ⚠️ **Viewer ed editor possono avere sequenze opposte per la stessa sezione** (vLOA/coordinamenti: il viewer
>   rende le direzioni nel padre, l'editor nelle figlie). Toccarne una sola ha prodotto un albero duplicato.
> - **§3bis del doc 11: «non-problemi verificati»** — due apparenti duplicazioni nei coordinamenti che sono dato
>   corretto. Leggerlo prima di «aggiustarle».

> **🖨️ Sessione 2026-07-30 (2) — stampa dei documenti + fix pubblicazione.** Branch
> `fix/audit-race-deadcode-redundancy`, 14 commit, suite **631 → 640 verde**, build 0 warning. Schede complete:
> `docs/feature/2026-07-30-stampa-documenti.md` e `docs/feature/2026-07-30-pill-stato-dopo-publish.md`.
> Le cose da sapere subito:
> - **La stampa era rotta da sempre e in silenzio**: il blocco `@media print` in `vipi-theme.css` nascondeva
>   tutto e mostrava solo `.printable`, classe che **nessun markup applicava** → Ctrl+P dava un foglio bianco su
>   qualunque pagina. Ora c'è il foglio dedicato **`vipi-print.css`** (nasconde il chrome, contenuto nel flusso
>   normale, A4 verticale, `thead` ripetuto, colori informativi preservati, scala tipografica da carta) +
>   `PrintMeta` + tasto «Stampa» sui quattro viewer. Nessun endpoint di export: la stampa del browser copre
>   RNF-6 (piano §10, §22.7 aggiornati). **Dati live fuori dalla carta** per decisione: METAR/TAF e Ridotta.
> - **Tre trappole del browser, tutte invisibili ai test.** Un `<details>` chiuso **non si apre col solo CSS**
>   (Chrome lo nasconde da user-agent con `content-visibility` su `::details-content`) → serve l'hook
>   `beforeprint` (`wirePrint` in `vipi-ui.js`). **Chrome segnala la stampa due volte** (`beforeprint` + cambio
>   media `print`) → gli handler di stampa vanno resi **idempotenti**, o il ripristino post-stampa non avviene.
>   **Leaflet** tiene la propria dimensione in memoria: ridurre l'altezza da CSS **ritaglia** la mappa invece di
>   riadattarla (serve `invalidateSize` + refit).
> - **«Bozza vN» dopo «Pubblica ora» era solo la pill**, non la pubblicazione (release `Effective`, audit e
>   documento promosso erano corretti): `ReleasePanel` ricaricava solo le proprie release senza avvisare l'host.
>   Ora ha un `EventCallback Published` che i tre editor agganciano al proprio `LoadAsync`. ⚠️
>   `string.Format(L["chiave"].Value, n)` **non interpola** — serve l'overload `L["chiave", n]`.
> - **⚠️ Chiave di release ACC**: `"{acc}|{root}"` — la parte `root` sceglie *quale* albero/documento si
>   pubblica e **va rispettata**. `AccVipiReleaseTarget` la scartava (primo CTR radice per `CoverageOrder`): su
>   una ACC multi-albero avrebbe promosso la bozza del documento sbagliato, in silenzio. Corretto.
> - **Razor scarta il testo di sola spaziatura che precede un blocco di codice**, anche dentro `<text>`: la
>   legenda piste usciva «recommended**from** the METAR wind». Lo spazio va scritto come entità `&#32;`.
>   Stessa famiglia della trappola `v@r.Proprietà` (sessione precedente).

> **⚠️ Sessione 2026-07-30 — audit concorrenza / codice morto / ridondanze.** Branch
> `fix/audit-race-deadcode-redundancy`, 14 commit, suite **505 → 631 verde**, build 0 warning. Documento completo:
> `docs/history/audit-2026-07-30-concorrenza-e-ridondanze.md`. Le tre cose da sapere subito:
> - **Import SID era rotto in silenzio** su LIRF/LIMC/LIME/LIBG/LIED/LIEO/LIPQ (ogni *reimport* falliva: snapshot
>   costruito con `ToDictionaryAsync(StableKey)` su chiave legittimamente ripetuta; il job logga a `LogDebug`).
>   Fixato. ⚠️ **La `StableKey` NON è unica per design** — non aggiungere un indice unico, fallisce sui dati veri.
> - **Le migration si provano su una copia di `src/Vipi.Host/vipi.db`**, non solo su DB vuoti da `EnsureCreated`:
>   i test partono sempre da vuoto e non vedono questa classe di problemi.
> - **Nuova skill `.claude/skills/verifica-live/`** per lanciare e guidare l'app in locale (la procedura non era
>   scritta: `dev-bootstrap.md` si fermava a `dotnet run`, e serve `VipiAuth__Enabled=false` per entrare).
>   Guidandola è uscito `rel. v@r.VersionNumber` **letterale** a schermo: in Razor una `@` fra due caratteri
>   non-spazio è letta come **indirizzo email** e non apre un'espressione, senza alcun warning → usare `v@(...)`.
>
> Aperto, **non di codice**: la SID `BANA8A` di LIBD (pista 07) ha `InitialClimb = "90"` → resa «90 ft», quota
> implausibile (le altre BANAV hanno `9000` → «FL90»). Da correggere nell'editor.

> **⚠️ Sessione 2026-07-29 — hardening deploy Render+Neon (leggere se si lavora sul deploy hostato).** Il sito test gira su Render+Neon Postgres (vedi `deploy/render/README.md` e memoria [[deploy-hosting-options]]). Fix di questa sessione, tutti su branch `fix/airport-weather-tl-draft-preview`:
> - **Login IVAO ricordato 7 giorni** (`VipiStandaloneAuthExtensions.cs`): cookie `ExpireTimeSpan=7gg` sliding + `IsPersistent=true` sul challenge → un solo login, sopravvive a chiusura browser.
> - **Retry-on-failure Neon** (`Infrastructure/DependencyInjection.cs`, ramo Postgres): `EnableRetryOnFailure` — Neon serverless chiude le connessioni idle, la prima query dava 500 `transient failure`. ⚠️ **Corretto il 30 lug:** questa nota diceva «retry-safe perché `EfUnitOfWork` avvolge già le transazioni in `CreateExecutionStrategy()`» — **necessario ma non sufficiente.** Al retry la strategy rigira la lambda sullo stesso context scoped e il rollback non ripulisce il change-tracker, quindi le entità del tentativo fallito venivano riemesse (doppi insert). Ora `EfUnitOfWork` azzera il tracker a ogni tentativo.
> - **DataProtection su Postgres** (`src/Vipi.Host/VipiDataProtection.cs`, modulo staccabile): su Render il container è effimero → il key-ring di default si perdeva a ogni redeploy (antiforgery rotto + logout). Ora le chiavi vanno su un `DbContext` dedicato (tabella `DataProtectionKeys` su Neon). ⚠️ **NON** `EnsureCreated()` (verifica il *database*, non la tabella → non creava nulla sul DB esistente): la tabella si crea con `CREATE TABLE IF NOT EXISTS`. Attivo solo se `Persistence:Provider=Postgres`; in dev SQLite resta il file-store.
> - **StationResolver.Prewarm()** (fix crash `A second operation was started`, memoria [[blazor-dbcontext-concurrency]]): `OnlineCount()` faceva lazy-load DB **durante il render** su `AccVipiPage`/`SopHome`/`VloaListPage`. Nuovo `IStationResolver.Prewarm()` scalda le cache nel ciclo di vita async. **Regola: nessuna I/O DB durante il render, nemmeno lazy via service scoped.**
> - **Tool `Vipi.DbSeed`** (copia SQLite locale→Neon): fix ciclo `Document↔DocumentVersion` (insert a 2 fasi con `CurrentVersionId=null`). Uso: `dotnet run --project tools/Vipi.DbSeed -- <vipi.db> "<connstring-postgres>"` (fa TRUNCATE+reseed).
> - **`IvaoTokenProvider`**: logga il body d'errore sui token 400 (prima `EnsureSuccessStatusCode()` lo scartava).
>
> **✅ RIENTRATO (5 agosto 2026) — token app IVAO.** Avviando l'host sul MySQL locale, `POST /v2/oauth/token`
> ha risposto **200** e il polling ha trovato 2 ATC di divisione online. Il secret nei user-secrets locali
> funziona: quello stale era su Render, non qui. ⚠️ Verificato solo il percorso con scope **`tracker`** (il
> polling): l'**import** ACC/settori, che potrebbe volere anche `configuration`, non è stato riprovato — il
> database di prova era vuoto. Da confermare guidando l'import. La diagnosi storica resta sotto perché il
> ragionamento serve se il 400 tornasse.
>
> **⏳ ex-APERTO — token app IVAO (400):** il polling tracker + import ACC falliscono con `POST /v2/oauth/token → 400`. Diagnosi: **NON è codice** (endpoint/grant/scope validati col discovery OIDC IVAO). È il **secret/app sul portale**: o `Ivao:ClientSecret` stale nei user-secrets, o l'app `fc95c992…` non ha grant `client_credentials`/scope `tracker`+`configuration` abilitati. Il nuovo log mostra l'`error` esatto nel body. Nota: `Ivao:ClientId == VipiAuth:ClientId` (stessa app IVAO per login utente + token app). Aggiornare il secret sia in user-secrets locali sia in `Ivao__ClientSecret` su Render.
>
> **NB dev locale:** per testare login/logout in locale serve `VipiAuth:Enabled=true` in `appsettings.Development.json` (spegne l'utente dev fittizio → login IVAO vero) + redirect `http://localhost:5034/signin-oidc` e `/signout-callback-oidc` registrati sul portale IVAO. Questo flag è tenuto **fuori dai commit** (preferenza locale).

> **⚠️ Stato corrente (2026-07-21) — leggere prima.** Dopo il Round 34 il progetto è passato per l'**asse di refactor strutturale `docs/refactor/01→10` (tutti eseguiti)**: modello **`Document`+`DocumentVersion` unificato** per tutti e 4 i tipi (vIPI ACC / APP / Airport / vLOA), editing e storage su documento (doc 08); **flusso di pubblicazione generico** via registry `IReleaseTarget`/`IDocKindRoutes` (doc 09); **snapshot totale al publish + `RenderMode` per sezione** con **visibilità pubblica = release effettiva** (doc 10, merged). Aggiunta **retention pubblicazione** (anti-bloat: pota release `Superseded` oltre 13 cicli e versioni `Archived` oltre 3/documento; per-publish + boot sweep `PruneVipiReleases`). **Fix 2026-07-21:** off-by-one del cap `Archived` su **entrambi** i path publish (release-publish `ReleaseService.PublishNowAsync` e version-publish `EditingService.PublishAsync`) — ora il prune gira dopo l'archiviazione. Suite **358 verde**. Dettagli in `docs/history/rounds.md` (in coda), `docs/refactor/00-overview.md` e memoria `publication-retention-plan`. **NB:** le sezioni §4→§8 qui sotto descrivono lo stato a Round 34 e NON riflettono ancora l'asse 08→10 (modello/pubblicazione): in caso di conflitto valgono i doc `refactor/` + `spec/modello-dati.md`.
**Stato:** progetto **in sviluppo attivo**. Solution .NET 10 a 4 layer + Host Blazor Server, consultazione+editing+sicurezza dal DB. **Import SID da GitHub** (sectorfile Aurora `ivao-italy/it-aurora-sector`): parser + completion fix/VOR + alias, merge preserva-manuali, priorità per punto persistente (StableKey), pubblicazione differita al ciclo AIRAC N+1 (round 34, `AddSidImport`). **Import periodici gated** (`ImportState`, `AddImportState`): niente più fetch-all a ogni riavvio (round 34). **Vista live UNIFICATA** (`/vsop/live[/{callsign}]`, doc refactor 12): una pagina per callsign, descrittori per tipo di ente (CTR/APP/**TWR/GND/DEL**), postazione dalla connessione IVAO senza selettore, **non richiede una vIPI pubblicata** (è legata all'ente, non al documento) + vista rapida aeroporto inline (`AirportQuickPanel`); QoL admin `sectorstructure`/`trasferimenti` (round 34). **Versioning AIRAC**: release schedulate per ciclo su TUTTI i tipi (`DocRelease`; round 29, §9.17) + **task management editor**. **Anteprime unificate `?as=`** nei viewer tipizzati (round 33). **vLOA data-driven** + **ACC esteri confinanti** (round 27-28, §9.16). **vIPI ACC/APP data-driven a blocchi** (round 21/23). **Live IVAO** (polling + cache + SSE). **Sorgente dati disaccoppiata** + **policy di import opt-out** (categorie: TA/Runways/Sectors/**Sids**). Pagine su prefisso **`/vsop`**. **Fonte unica = cataloghi**: i `Sector` sono una proiezione, gerarchia per callsign cross-ACC (Round 20).

> **📡 Sessione 2026-07-31 — vista live.** Branch `feat/vista-live`, 23 commit, suite **631 → 718 verde**,
> verifica live guidata su copia del DB reale. Carta: `docs/refactor/12-vista-live-unificata.md`. Da sapere subito:
> - **Una pagina sola, keyed sul callsign**: `/vsop/live` (la tua postazione, dalla connessione IVAO —
>   **nessun selettore**) e `/vsop/live/{callsign}` (consultazione). Via `AccLivePage`/`AppLivePage` e le due
>   `Ridotta*` morte. Le rotte storiche fanno **301 a un salto solo**.
> - **La vista è legata all'ENTE, non al documento**: senza vIPI pubblicata degrada a banner e continua a
>   rendere trasferimenti, AoR e frequenze dai cataloghi. Non reintrodurre early-return sul documento.
> - **Descrittori per tipo** (`ILiveStationKind`, come `IReleaseTarget`): **torri, ground e delivery hanno una
>   vista live** che prima non esisteva. Un test verifica che ogni `SectorType` abbia un descrittore.
> - ⚠️ `/vsop/live/{callsign}` ricade sul prefisso dello stream SSE `/vsop/live/atc`: vince il segmento
>   letterale, ma è una proprietà del routing che si rompe cambiando le rotte → smoke dedicato.
> - **L'avvicinamento è reso come l'area**: chip degli aeroporti (un APP ne copre spesso più d'uno), frequenze,
>   trasferimenti. Pannello fisso solo per torri/ground/delivery, che sono di un aeroporto solo.
> - **Un punto verso un proprio discendente si mostra solo se quel settore è APERTO**: se è chiuso lo stai
>   coprendo tu, e il punto diceva «passa a te stesso». Vale solo per i discendenti — verso l'esterno la
>   risalita fino a UNICOM resta informazione utile.
> - ⚠️ In verifica: `innerText` su un `<details>` **chiuso** torna stringa vuota — un'asserzione ingenua la
>   legge come «elemento assente».
> - ⚠️ In verifica: un `dotnet run` che fallisce per **DLL bloccate** da un'istanza precedente lascia in ascolto
>   il binario VECCHIO, e si finisce per misurare la build sbagliata. Killare, `dotnet build`, poi `--no-build`.
>
> - **Il padre dell'aeroporto non arrivava alle sue posizioni** (segnalato dall'owner, fixato): la proiezione
>   leggeva solo `AirportSector.ParentCallsign` (solo APP) e ignorava `Airport.ParentCallsign`, che è il campo
>   che l'admin compila in Struttura → torri/ground/delivery orfani. Ora scaletta **DEL→GND→TWR→APP** + uscita
>   sul padre dell'aeroporto, riproiettata all'avvio (`ProjectVipiSectors`). Reggeva anche la risalita dei
>   trasferimenti: un punto verso una torre offline finiva su UNICOM invece che all'APP.
>
>   Fra pari grado si sceglie **coi dati**: la radice del sottoalbero APP (gerarchia scritta dall'admin, es. le
>   sei APP di LIRF pendono da `LIRF_TW1_APP`), poi il callsign senza infisso (`LIRF_TWR` vs `LIRF_E_TWR`), e se
>   resta ambiguo si **sale** invece di tirare a sorte.
>
> - **Torri, ground e delivery sono nodi editabili** in `/vsop/admin/sectorstructure` (§8 del doc 12): erano
>   esclusi da un filtro `Position == "APP"`, non da una scelta di modello. La scaletta è un servizio di dominio
>   condiviso (`AirportPositionLadder`) e i nodi senza padre scritto mostrano quello **ereditato** invece di un
>   «da assegnare» che contraddirebbe la vista live. Guardia: nessun padre più in basso nella scaletta.
>
> Aperto, **di dato**: 33 torri di aeroporti senza APP e senza padre configurato in Struttura, più LIRF stesso
> (senza padre l'aeroporto non compare fra i chip di nessuno). Ora si sistemano dalla pagina: il filtro «solo da
> agganciare» li raccoglie.

> **Storia dei round:** `docs/history/rounds.md` (changelog R5→R34). **Indice doc:** `docs/index.md`. Ultimo round: **34** — vista operativa + QoL admin + import SID GitHub + gating import; modello in `docs/spec/modello-dati.md` §9.8 (migrazioni). (R33: anteprime `?as=`; R30: QoL Bozze & versioni §9.18; R29: versioning AIRAC + task §9.17.)

---


</details>

## 1. In una frase
Portale web interattivo che trasforma le **vIPI** (istruzioni operative ATC) e le **vLOA** (lettere di accordo) della divisione IVAO Italia da Word statici a contenuto strutturato, con due livelli (Estesa/Ridotta), logica di visibilità live legata a chi è online (AoR top-down) ed editing per lo staff.

## 2. Come far girare il progetto
```bash
cd "vIPI Ivao Italy"            # cartella interna con la solution
dotnet build Vipi.slnx
dotnet test  Vipi.slnx          # 5714 casi verdi sui due TFM (27-ago-2026) — ⚠️ la cifra si CONTA, non si ricorda
dotnet run --project src/Vipi.Host --urls http://localhost:5034   # poi apri /services/vsop
```
- 🔎 **Per verificare una modifica UI a schermo** (non solo coi test): skill **`.claude/skills/verifica-live/`** —
  avvio su una copia del DB, driver Edge+puppeteer-core, bersagli e trappole già mappate. Le regressioni Blazor
  sono silenziose coi test verdi, quindi il runbook chiede di guidare il flusso reale.
- ⚠️ **AZIONE PENDENTE (2026-07-22, audit Fase 1):** **RIAVVIARE il Host** per applicare `AddImportStateLastError` (additiva: `ImportState.LastAttemptUtc`/`LastError`). Poi `/vsop/admin/sorgenti` mostra il **report stato import** (ultimo successo/tentativo/errore per categoria). Nota: da questa sessione `/vsop/health` è **Unhealthy (503)** se ci sono migrazioni pendenti (schema drift). Audit completo: `docs/history/audit-2026-07-22-criticita-full-stack.md`. Nuova rete di test: `Vipi.Ui.Tests` (bUnit) + `Vipi.E2E.Tests` (WebApplicationFactory in-process).
- ℹ️ **FASE 2 audit ESEGUITA (2026-07-22, nessun cambio schema):** **B1** report consistenza soft-ref in **`/vsop/admin/diagnostica`** (pista orfana · label pista divergente · area fantasma · gerarchia `ParentCallsign` dangling) — solo diagnosi, nessun auto-fix; `IConsistencyReportService`/`Analyze` (logica pura) + `IConsistencyReportRepository` (EF read-only); se ci sono finding, `/vsop/health` → **Degraded**. **C1** XSS: `HtmlEncode` dei valori dinamici in `StrutturaPage`/`AeroportoPage` (pattern gemello `SearchPage`/`MarkdownLite`).
- ℹ️ **FASE 3 audit ESEGUITA (2026-07-22) — parte code, resto pianificato in ADR-0007:** **A1** tampone concorrenza SQLite `SqliteTuningInterceptor` (WAL + `busy_timeout`) nel path `UseSqlite`; **D1** `ProductionIdentityGuard.EnsureSafe` in `Program` fa **hard-fail** all'avvio se l'identità dev è attiva fuori da Development (no admin-onnipotente in prod); test path prod `HostIdentityCurrentUserProvider` (nuovo progetto `Vipi.Hosting.Tests`). **A1 cutover Postgres + A2 scala Blazor = pianificati in `docs/adr/adr-0007-produzione-persistenza-e-scala.md`** (non attuati: servono migrations Postgres dedicate + istanza di validazione + backplane). **ESTERNI residui:** montare la RCL nel sito host + configurare `HostIdentity` coi claim/staff-code IVAO reali; eseguire il cutover Postgres; provisioning backplane.
- ℹ️ **MINORI audit ESEGUITI (2026-07-22):** **C4** `StrutturaPage` — estratti i `RenderFragment` HTML-a-mano in componenti dichiarativi `StructureCoverage`/`StructureFallbackChain` (chiude C1 alla radice, +6 bUnit con regressione XSS). **B4** spec §3 marcata `[SUPERATO]` (usa §9). **B3** nuova checklist `docs/guide/dev-bootstrap.md` (coerente «Nessun seed»). **C3** chiuso come non-issue (aor3d già off; AoR block = editoriale, non stub). Onboarding dev: vedi `docs/guide/dev-bootstrap.md`.
- ⚠️ **AZIONE PENDENTE (2026-07-22):** **RIAVVIARE il Host** per applicare le migrazioni pendenti dei trasferimenti — `AddTransferPointConditionArea` poi **`SplitTransferConditionColumns`** (backfilla e droppa `ConditionKind`). Sessione 22 lug: condizione trasferimenti = **tre colonne indipendenti** (pista multi-select · area con **ricerca a digitazione** · personalizzata), enum `TransferConditionKind` **rimosso**; fix condizione «Pista» che legge le **piste reali** `AirportRunways` (non le config); bottone **«Re-importa da IVAO (tutti)»** su `/vsop/admin/airports`. Verifica live su LIBD. Suite **19 dom + 205 app + 174 infra** verde. Dettaglio: `spec/modello-dati.md` §9.20, `refactor/07-trasferimenti.md` §7-7.2, memorie `transfer-condition-model` / `airport-runway-import`.
- ⚠️ **NOTA (Round 34):** il **`vipi.db` dev è stato resettato** a fine sessione (testando il gating import). Al primo avvio ripopola da zero (ACC → settori → aree → SID) e stampa lo stato in `ImportStates`; i riavvii successivi **saltano** i fetch finché non scadono i 24h (o via bottoni manuali). Le SID importate sono pubbliche solo dal ciclo AIRAC successivo.
- ⚠️ **AZIONE PENDENTE (Round 22):** **fermare e RIAVVIARE il Host** per applicare la migrazione **`AddAirportCoordsAndTwrSyntheticShape`** (additiva) e far girare il job che (a) popola `Airport.Latitude/Longitude` dal dettaglio ATCPositions e (b) genera le **shape tonde 5 NM** per le TWR vuote (`/v2/ATCPositions/{compose}.regionMapPolygon = "[]"`). Il job parte ~30s dopo l'avvio. Poi su `/vsop/{acc}/apps/vipi?app={APP}` l'AOR mostra il cerchio della torre col toggle «Shape torre». ⚠️ Credenziali IVAO in **user secrets** (`Ivao:ClientId/ClientSecret`), scope `tracker` basta per il dettaglio postazione. Il Host viene **fermato** a fine sessione (blocca le DLL in build).
- ⚠️ **AZIONE PENDENTE (Round 20):** se il DB è ancora pre-round-20: **reset `src/Vipi.Host/vipi.db`** in dev (o applica `AddHierarchyParentCallsign`) → riavvia. Poi `/vsop/admin/acc` → «Importa da sorgente»: la **sync** popola i `Sector` dai cataloghi; in `/vsop/admin/sectorstructure` compare l'**albero di copertura globale** (cross-ACC).
- DB **SQLite** creato/migrato all'avvio (`src/Vipi.Host/vipi.db`). **Nessun seed**: si parte da DB **vuoto**. Flusso dati reale: `/vsop/admin/acc` importa ACC+settori dalla sorgente → la sync proietta i `Sector` → la **gerarchia** (padri per callsign) si imposta in `/vsop/admin/sectorstructure` → «Crea nuovo documento» (vIPI = N settori di scope, uno primario) → editor. **I settori NON si creano più a mano** (sono proiezione dei cataloghi, Round 20). Cancella `vipi.db*` per ripartire da zero. I `*Seed.cs` di Roma restano solo come fixture nei test.
- In dev l'utente è `DevCurrentUserProvider` (VID 704798, staff `IT-AOC` → **admin**, può tutto).
- Migrazioni: `dotnet ef migrations add <Nome> --project src/Vipi.Infrastructure --startup-project src/Vipi.Infrastructure -o Persistence/Migrations`. ⚠️ Per i **rename** di proprietà/colonna EF scaffolda `RENAME COLUMN` solo se i campi combaciano: **verificare a mano** la migrazione generata (no Drop+Add che perde dati).

## 3. Mappa documenti
Indice completo con scopo e stato di ogni documento: **`docs/index.md`**. In sintesi:
- `README.md` (cos'è + architettura + build) · **questo `HANDOFF.md`** (leggere per primo per riprendere).
- `docs/history/rounds.md` (changelog dei round) · `docs/spec/` (modello dati, logica AoR, mappa pagine) · `docs/guide/` (config, integrazione, **guida utente del bridge Aurora**) · `docs/adr/` (decisioni) · `docs/design/` (piano, **piano+verbali del bridge Aurora**) · `docs/reference/` (`sector-map.md`, **`api-aurora-bridge.md`**).

---

## 4. STATO CODICE — cosa è implementato (e dove)

**Solution (Clean Architecture, net10.0):** `Vipi.Domain` · `Vipi.Application` · `Vipi.Infrastructure` (EF Core + SQLite) · `Vipi.Ui` (RCL Blazor) · `Vipi.Host` (Blazor Server dev) + 3 progetti test.

**Cuore AoR/visibilità (✅ testato S1–S10):** `Application/Aor/AorService.cs` (ownership/stato settori, top-down, unificazioni), `Topology.cs`, `Infrastructure/Aor/TopologyBuilder.cs` (implementa la porta `ITopologyProvider`). Tabella di verità visibilità in `Application/Content/ContentService.cs`.

**Consultazione dal DB (✅):** pipeline `IContentRepository` → `IVipiViewService` → `SectionNode`/`BlockRenderer`. Rotte sotto `/vsop`:
- `/{acc}/vipi` (Estesa ACC) · `/{acc}/ridotta` (proiezione tier Reduced + sezione Trasferimenti) · `/{acc}/airports?icao=` (vIPI aeroporto) · `/{acc}/vloa`.
- `/search` (ricerca full-text reale), `/changed` (cosa è cambiato nel ciclo AIRAC), `/{acc}/export` (Estesa → stampa/PDF browser).
- **SID ✅ reali** (round 34): importate dal sectorfile Aurora GitHub, editor aeroporto + `AirportQuickPanel`. Stub residui: mappe AoR (SVG statico), `/{acc}/aor3d` (SVG statico). METAR/TAF = reale (NOAA).

**Le QUATTRO famiglie documentali sono uniformi (✅ 26 ago 2026):** vIPI ACC · vIPI APP · vLOA · **vIPI d'aeroporto**.
L'aeroporto era l'ultimo fuori: il suo documento era una **proiezione cotta** (sezioni riconosciute per titolo,
cancellate e ricreate a ogni rigenerazione, con chiavi casuali), e per questo l'unico senza riordino, «nascondi»
e sotto-sezioni. Ora ha `SectionProfile.Airport` nel catalogo, l'editor monta `DocumentSectionsEditor` (**con
bozza + lock**, obbligati dal motore condiviso) e il viewer itera l'ordine del documento. Le sezioni fisse sono
**ancore senza corpo**: il contenuto si deriva a view-time (`AirportSectionProjection`, pura) e si **congela**
alla release. Porte nuove nel catalogo: `IsAlwaysLive` (il meteo e la validità non si congelano mai) e
`KeepsOwnBlocks` (una sezione può avere una **scheda dalla pagina E i suoi blocchi**: è «Validità e revisione»,
che porta ciclo AIRAC, data e chi ha premuto Pubblica). Carta:
`docs/feature/2026-08-26-aeroporto-a-sezioni.md`; schema: `docs/spec/modello-dati.md` §9.31, §9.31-bis, §9.32.
⚠️ **Nessuna migrazione**, ma **un passo d'avvio nuovo**: `ReconcileAirportSectionKeysAsync`, idempotente, che
porta i documenti già scritti sulle chiavi del catalogo e **trasloca** le sezioni libere dalla tabella
`AirportExtraSection` dentro il documento — tabella che si droppa **un rilascio dopo**, perché le migrazioni
girano all'avvio *prima* delle riconciliazioni.

**Editing persistente (✅):** `Application/Content/EditingService.cs` + `Infrastructure/Persistence/EfEditingRepository.cs`:
- Workflow **bozza→pubblicato** (clona versione, audit, archivia precedente). CRUD **blocchi e sezioni** (aggiungi/elimina/sposta, vincolo max 3 livelli). `EditorPage` (`/{acc}/editor`, anche `?doc={id}`), `VersioniPage`.
- Editor specializzati: `AdminTrasferimentiPage` (trasferimenti, pagina admin globale `/vsop/admin/trasferimenti`: selettore ACC + flussi/punti, Next cross-ACC; ex per-ACC `XferEditorPage` rimosso) — **round 22:** flussi e punti **editabili in-place** via `ITransferService.UpdateFlowAsync`/`UpdatePointAsync`. **12 ago 2026 — la pagina è a TRE COLONNE**: navigatore (`XferNavigator`, albero Settore ▸ Aeroporto ▸ gruppo, dove il gruppo è una **foglia** e non un livello di collasso) · riquadro di lavoro (il gruppo scelto) · pannello riga; ognuna scorre per conto proprio, e l'altezza la misura `vipiFitViewport` perché in CSS non è esprimibile. Interruttore **Albero ⇄ Elenco** (`XferRowsTable` è **una** tabella per entrambe le viste, con le colonne di contesto solo in elenco). **CoP, livello e ricevente si scrivono in cella**; il livello si rilegge con `LevelFormatting.Parse` (round-trip provato). Stato in URL (`?acc=&vista=&gruppo=&riga=&q=&tipo=&rev=&norx=`), preferenze di vista in `localStorage`. Secondo giro: **annulla** dopo un'eliminazione — con `RestoreFlowAsync`/`RestorePointsAsync`, che rimettono anche l'outline (ricostruire con `AddPointAsync` lo appiattirebbe in silenzio) — **modifica in blocco** su ricevente/livello/condizione/eliminazione, ordinamento per intestazione in elenco, e i sei picker a digitazione ridotti a un componente solo (`TypeaheadPicker`, con frecce/Invio/Esc). ⚠️ Salvare una cella costava **8 query**: il contesto delle frasi ora si rifa solo sulle scritture di gruppo. Carte: [`docs/feature/2026-08-12-editor-trasferimenti-tre-colonne.md`](docs/feature/2026-08-12-editor-trasferimenti-tre-colonne.md) e [`docs/feature/2026-08-12-editor-trasferimenti-rifiniture.md`](docs/feature/2026-08-12-editor-trasferimenti-rifiniture.md). `VloaEditorPage` (redirect all'editor generico). Gerarchia di copertura in `StrutturaPage` (`/vsop/admin/sectorstructure`).
- **Editor APP non remotizzati (✅ round 21):** `AppEditorPage` (`/vsop/{acc}/apps/editor?app=`) WYSIWYG con 6 sezioni fisse (Separazioni · AOR · Frequenze · VFR · Minime · Coordinamenti) + custom, riordino drag-and-drop+tasti, nascondi sezioni; viewer `AppnPage` data-driven. Entità `AppProfile`/`AppFrequencyLink` (modello §9.13), service `IAppProfileService` (freq/coord/AOR **derivate live**), `AorPolygonProjector`, registry `AppSections`, componenti `Vipi.Ui/Components/App/*`, mappa AOR Leaflet (`vipi-aor.js`). Instradamento via `DocumentSummary.IsStandaloneApp`. **Round 22:** «Trasferimenti verso ACC» suddiviso in sottosezioni **Partenze/Arrivi** (`AppCoordinationView`, split per `Kind`); **AOR** mostra anche le **shape delle TWR** dello stesso aeroporto come overlay Leaflet con toggle «Shape torre» (`GetTowerPolygonsAsync`). ⚠️ **`TopologiaPage` rimossa** (`/vsop/{acc}/topologia`): gerarchia → `sectorstructure`; le regole di unificazione + simulatore AoR erano legacy e non hanno più UI (motore `IAorService` + `UnificationRule` + test S1–S10 **restano**).

**Sicurezza/permessi (✅):** `Application/Auth/EditAuthorizationService.cs`:
- **Admin** = staff position da due set: **ruoli di divisione** (`DivisionOptions.Code` + `AdminRolePatterns` → `^{Code}-{ruolo}$`; dal 22 agosto 2026 il default è il jolly `[A-Z0-9]+`, cioè **tutto** lo staff di divisione) **e ruoli ACC-scoped/chief** (`AdminAccRolePatterns` → `^{prefissoIcao}[A-Z0-9]+-{ruolo}$`, es. `LIRR-CH`/`LIMM-ACH`) → edita tutto + gestisce permessi. Override esplicito opzionale via `Auth:AdminStaffCodes`. **Divisione configurabile** (sezione `Division`): vedi §7.
- **Multi-divisione:** tutto ciò che cambia passando divisione è in `DivisionOptions` (Application): `Code`, `IcaoPrefixes`, `AdminRolePatterns`, `AdminAccRolePatterns`. Il **contenuto seed** (Roma/LIRR) resta dato separato.
- **Grant per-ACC** (`EditGrant`, VID→ACC): chi non è admin edita una ACC solo con grant. Schermata `/vsop/admin/permessi` (solo admin).
- **Lock** documento esclusivo (30 min sliding, atomico via `ExecuteUpdateAsync`, **force admin**) → `EditConflictException`. **Concorrenza ottimistica** (`RowVersion` su `ContentBlock`/`DocumentSection`).
- **Lock risorsa** per le pagine admin senza documento (`EditResourceLock`, `IResourceLockService`): le 4 pagine di struttura condividono `admin:structure`, newdoc ha `editor:newdoc`; una persona alla volta (barra `EditLockBar`, TTL 3min + heartbeat 60s + force admin).
- **Validazione**: `UnificationRule` hard, trasferimenti soft. Verifiche **sempre server-side**. Security review: XSS in `AorBlock` corretto.

**Persistenza:** `VipiDbContext` mappa tutte le entità; enum→stringa; **lista migrazioni autoritativa = `docs/spec/modello-dati.md` §9.8** (fino a **`AddAirportCoordsAndTwrSyntheticShape`**, round 22). Seed (solo fixture di test, **non** seminato all'avvio): `RomaStructureSeed`, `RomaContentSeed`, `RomaAirportSeed`, `RomaVloaSeed`, `RomaTransferSeed`. ⚠️ **In produzione i `Sector` sono una proiezione dei cataloghi** (round 20): non si creano a mano, vedi `docs/spec/modello-dati.md` §9.12.

**Modello dati — aggiunte rispetto a `docs/spec/modello-dati.md` §3:** **`TransferFlow`** (settore mittente + tipo + aeroporto) → **`TransferPoint`** (CoP/livello strutturato/settore ricevente `NextSector`); risoluzione live **risale la gerarchia globale** (`ParentCallsign`/`ParentSectorId`), terminale **UNICOM** (no enum fallback). `EditGrant`; campi **lock** su `Document`; `RowVersion` su `ContentBlock`/`DocumentSection`.

**Live IVAO (✅):** `src/Vipi.Infrastructure/Ivao/` — `OnlineAtcCache` (singleton, `IOnlineAtcProvider`), `IvaoApiClient` (`/v2/tracker/now/atc/summary`, filtro prefisso `LI`), `IvaoTokenProvider` (client_credentials, solo per i membri divisione: tracker pubblico), `AtcPollingHostedService` (60s), `IvaoOptions`. Transport **SSE** `/vsop/live/atc` + `vipi-live.js`. `VipiViewService` calcola AoR reale quando `live=true`; `RidottaPage` `InteractiveServer`. Decisione in **ADR-0003**.

**Indipendenza dalla sorgente (✅, ADR-0006):** porte dati esterne **neutre** (`IAirportDirectory`/`IAirportDetailProvider`/`IUserDirectory`/`IOnlineAtcProvider`, DTO `Source*`); adapter IVAO selezionato da **`DataSource:Provider`**. `Vid`→`UserId` ovunque (a video resta "VID"). **Policy di import** (`ImportPolicy`, categorie `{TransitionAltitude, Runways, Sectors}`, pagina `/vsop/admin/sorgenti`): dati di sorgente in sola lettura, enforcement a difesa in profondità.

**Fonte unica settori (✅ Round 20):** cataloghi `AccSector`/`AirportSector` = fonte autoritativa; `Sector` = proiezione (`ISectorProjectionService.SyncFromCatalogsAsync`). Gerarchia per callsign (`ParentCallsign`, cross-ACC) editata in `/vsop/admin/sectorstructure` (`IHierarchyEditingService`). Dettagli: `docs/spec/modello-dati.md` §9.12.

**Shape tonda TWR + coord aeroporto (✅ Round 22):** le TWR senza poligono reale (IVAO le espone come `"[]"`) ricevono una **shape circolare 5 NM** sintetica così da poterle disegnare. `CircleShapeBuilder` (puro, formato `[[lng,lat],…]`), `TowerShapeFallbackService` (genera solo sulle vuote — decise col `AorPolygonProjector` —, marca `IsShapeSynthetic=true`, mai sovrascrive shape reali). Centro = `Airport.Latitude/Longitude`, popolate all'import dal blocco `airport` del dettaglio `/v2/ATCPositions/{compose}` (`SourceAtcPosition.AirportLatitude/Longitude`); ripiego = centro del poligono di un settore fratello. Job in `AirportSectorImportHostedService` (import isolato in try: il fallback gira anche senza credenziali). **TODO futuro:** shape reali TWR dal **sectorfile GitHub** via `DataSource:Provider` → rimpiazzano solo le sintetiche. Dettagli: `docs/spec/modello-dati.md` §9.14.

**Bridge Aurora (✅ 3 ago 2026, branch `feature/aurora-bridge`, NON ancora in `main`):** tool desktop che
scrive nel tag di Aurora il livello a cui cedere il traffico al prossimo ente.
- **Lato sito:** `TransferMatcher` (puro, `Application/Content/`) + `ITransferMatchService` + endpoint
  **`POST /vsop/api/v1/transfers/resolve`** (in `MapVipiModule`, anonimo e read-only, tetto per IP via
  `RequestRateLimiter`, sezione di config `AuroraBridge`). Il matching valuta CoP (fix da `#TRPATHL`, poi
  rotta; jolly `ALL`/`ALL to GR`, range aerovie `Y01-Y12`), parità semicircolare, condizione pista contro
  `#CTRLRWY`, next ATC già impostato — e restituisce candidati **motivati in italiano**.
- **Lato tool:** `Vipi.AuroraBridge.Contracts` (contratto), `.Core` (protocollo TCP 1130, client HTTP con
  cache su disco, orchestratore, ViewModel), `Vipi.AuroraBridge` (shell Avalonia), `tools/Vipi.AuroraBridge.Cli`
  (verifica end-to-end), `tools/Vipi.AuroraProbe` (sonda del protocollo).
- **Vincoli di Aurora accertati sul campo:** l'**XFL non è scrivibile** (nessun comando esiste, si scrive
  l'etichetta quota con `#LBALT`), si scrive **solo sul traffico assunto**, e la porta 1130 si apre solo
  riapplicando *3rd Party Software Access* **nella sessione in corso**. Cinque inesattezze della wiki IVAO
  documentate in `docs/design/piano-aurora-bridge.md` §11.
- Guida utente: `docs/guide/aurora-bridge.md`. Contratto: `docs/reference/api-aurora-bridge.md`.

---

**Prestazioni e forma del pacchetto (✅ 27 ago 2026, §O):** quattro pezzi nuovi, tutti fuori dal dominio.
- **`tools/Vipi.Assets`** (nella soluzione, non fra gli attrezzi da lanciare a mano: è un **passo del
  publish**). Minifica CSS/JS della `wwwroot` pubblicata — NUglify, **senza rinominare i locali** — e lascia
  accanto a ogni file di testo la variante `.br`/`.gz` alla qualità 11. ⚠️ Se un file non è minificabile il
  **publish si ferma**: JavaScript e CSS non li compila nessun altro passo della build.
- **`AssetPrecompressi`** (host): consegna quelle varianti al posto della compressione al volo, e le
  **ignora se più vecchie del file** — la rete contro il `.br` dimenticato in un aggiornamento via FTP.
- **`CacheDelleLettureAnonime`** (host): le letture anonime dei documenti pubblici escono con
  `public, max-age=60` e senza cookie antiforgery. **Sette clausole**, ognuna col suo test; il resto continua
  a rispondere `no-store`.
- **`StartupDiagnostics.CronometroAvvio`**: le fasi dell'avvio in coda a `diagnostica/avvio-diagnostica.txt`.
  Su un host senza shell è l'unico modo di rispondere a «ci mette tanto a ripartire, ma tanto **dove**?».

⚠️ **Il caricamento dei moduli JS non è più tutto nel `<body>`**: `vipi-boot.js` tira dentro mappe, minime,
3D e tour **solo se la pagina mostra il loro bersaglio**. `vipi-editor.js` e `vipi-media.js` restano sempre
caricati, ed è una scelta: quelli il codice C# li chiama **per nome**.

## 5. PROSSIMI PASSI (ordinati per valore)

0. **Bridge Aurora — portarlo in produzione:** il branch `feature/aurora-bridge` va rivisto e unito; finché
   l'endpoint non è rilasciato su `it.ivao.aero`, il tool funziona **solo** contro un host locale.
   Chiuse per decisione: i sorvoli LIBB senza livello sono lacuna redazionale (il tool non deve indovinare),
   e il pacchetto macOS lo farà chi ha una macchina Apple.

1. **Live IVAO — rifiniture aperte:**
   - **Identità "P"** legata al callsign connesso del CH loggato (oggi selettore manuale in Ridotta).
   - **Mapping token-handler → callsign** trasferimenti (oggi euristica match-segmento). Valutare tabella esplicita.
   - **Endpoint membri divisione** (`/v2/divisions/IT/members`) da confermare.
   - Estendere `live=true` a **vIPI aeroporto / vLOA** (oggi solo ACC Ridotta).
2. **Dati reali:** METAR/TAF ✅ (NOAA). Shape AoR ✅ (poligono IVAO). **SID ✅** (sectorfile Aurora GitHub, round 34, sez. config `Sectorfile`). **AoR 3D ✅** (Three.js r128 vendorizzato: tab 2D/3D nel blocco AoR + pagina `/vsop/aor3d/{Kind}/{Key}`; settori estrusi per banda FL, con **basemap geografica CartoDB come pavimento** — proiezione Web Mercator, toggle «Mappa base» — e rendering leggibile: selettore «Altezza» ×0.25→×2 con default ×0.5, etichette come overlay HTML con declutter, chip settore condivise col 2D — vedi `docs/feature/2026-07-31-aor3d-leggibilita.md`; il link «Apri pagina» è **rimosso** in attesa di rilavorare la pagina dedicata, che resta raggiungibile a URL diretto). **Shape reali TWR ✅** (dal sectorfile GitHub, `GithubTowerShapeService`: 68 TWR su 84 hanno il poligono vero, i 16 cerchi sintetici restanti sono torri che nemmeno `twrs.tfl` contiene — verificato il 9 agosto 2026). **Minime MVA ✅** (22 agosto 2026, verificate live): non come tabella — `area → quota` dal formato `.mva` non si ricostruisce (etichetta indipendente dai poligoni, 70 casi ambigui su 345, testo che non è un numero, 92 tracciati aperti su 315) — ma come **carta**, una per file, disegnata verbatim su fondo topografico (`IVectoringMinimaSource` → `AuroraMvaProvider`, composizione in `MinimaCharts`, resa in `MinimaSection`/`vipi-mva.js`). L'attribuzione non si indovina: la dichiara il nome del file, `ENRMVA/{acc}.mva` per l'enroute di un ACC e `{icao}.mva` per un aeroporto. Sezione `minima` ora **Derived** (toggle Live/Congelata, cattura nello snapshot); tabelle `VectoringMinima*` droppate. Vedi `docs/lavori-aperti.md` §E2. Nota AoR 3D: i settori senza limiti admin estrudono GND→UNL (banda piatta) → il rilievo 3D emerge solo coi `LowerLimit`/`UpperLimit` valorizzati.
3. **Fonte unica (Round 20) — follow-up:** doc+AoR girano ancora sui `Sector` (proiezione), non direttamente sui cataloghi. Eliminazione totale di `Sector` + **risoluzione live** "chi controlla l'aeroporto adesso" (presidiato se DEL/GND/TWR online, altrimenti primo antenato online risalendo `ParentCallsign`) = fase live. ✅ **Fatto per i trasferimenti:** `ITransferService.ResolveForAccAsync` + `ITopologyProvider.BuildGlobalAsync` risolvono mittente e ricevente risalendo la gerarchia globale (terminale UNICOM); Ridotta li mostra nidificati Settore ▸ Aeroporto ▸ Tipo. Resta da estendere la stessa risalita alla "presidenza aeroporto" generale.
4. **Auth di produzione:** adapter reali `ICurrentUserProvider` — `HostIdentity` (A/B, claim `Ivao.It`) e OIDC (C); mappare gli **staff code reali** (§6). Montare la RCL nel sito host.
5. **Copertura/rifiniture:** viewer **audit log**, "scarta bozza", editor visuale mappe AoR, test property-based AoR, rifinitura UI.

---

## 6. Nodi aperti / decisioni
**Ancora aperte:**
- **Staff code esatti IVAO:** ✅ il lato divisione **non è più aperto** — dal 22 agosto 2026 admin = **tutto** lo staff di divisione (`^{Division.Code}-[A-Z0-9]+$`, default `AdminRolePatterns = ["[A-Z0-9]+"]`), decisione del committente. Resta da confermare il solo lato **chief ACC-scoped** (`{ACC}-CH`/`{ACC}-ACH`, es. `LIRR-CH`): nessun codice del genere è mai comparso in un login vero. I chief (CH/ACH) ora **sono** admin completi (`AdminAccRolePatterns`); l'auto-elenco per il dropdown grant resta via `IDivisionMembersProvider` (path `DivisionMembersPathFormat` = `/v2/divisions/{Code}/members`, da confermare).
- Identità **P** = callsign connesso del CH (oggi selettore manuale); mapping token-handler trasferimenti (oggi euristica); GeoJSON vs WKT (shape); formato/schedulazione parsing sectorfile (SID + minime).

**Risolte (storico):** modello editing persistente; autorizzazione (admin via staff code + grant per-ACC); lock 30 min + force admin; validazione hard/soft; export = stampa browser; trasporto live = **SSE** (ADR-0003); polling cache singleton 60s.

**Fix collaterali round 21:** `NewDocumentPage` naviga all'editor con **`forceLoad:true`** dopo la creazione (evitava lo stale read «documento non esiste»). `AdminTrasferimentiPage` — i dropdown sector-pick selezionano su **`@onmousedown`** (non `@onclick`): in Blazor Server il `@onblur` chiudeva il dropdown prima del click.

**Nota tecnica round 22 (importante per il debug):** la sorgente IVAO espone le TWR con **`regionMapPolygon = "[]"`** (array vuoto), **non** null — il «vuoto» NON si rileva in SQL (`null`/`''`) ma **provando a proiettare** col `AorPolygonProjector` (`Project(raw) is null` ⇒ vuoto/degenere). Il centro del cerchio viene dal blocco **`airport`** del dettaglio **`/v2/ATCPositions/{compose}`** (NON da `/v2/airports`, che richiede scope `configuration`). Credenziali IVAO reali in **user secrets** (id `79756a9b-…`), `appsettings.json` le ha **vuote**. Le coordinate si popolano solo all'**import** (job all'avvio), quindi serve **riavviare il Host** per vederle.

## 7. Note operative per la nuova chat
- **Configurazione:** riferimento completo in `docs/guide/config.md` (sezioni `Division`/`Ivao`/`Auth`, secrets, env var). Divisione/admin: ADR-0004.
- **Caveman mode** spesso attivo in queste chat (comunicazione compressa) — non è parte del prodotto.
- **Divisione pilota:** Italia (`Division:Code=IT`), **ACC pilota:** Roma (LIRR). Validare su una sola ACC prima di estendere.
- **Brand:** la fonte è [ivaoaero/atmosphere](https://github.com/ivaoaero/atmosphere) → `brand/src/tokens.json` (solo colori e font). Regole operative in **`docs/design/regole-brand.md`** — leggerle prima di scrivere un colore. Font: **Poppins** (titoli), **Nunito Sans** (corpo), **IBM Plex Mono**. ⚠️ Fino al 2026-08-22 i primi due erano **scambiati**, e `piano-vipi-tool.md` §15.2 diceva l'opposto: se una carta vecchia dice «Nunito Sans → titoli», è vecchia. Tema in `Vipi.Ui/wwwroot/vipi-theme.css`, a tre livelli, **senza colori letterali fuori dal primo**; include `@media print` e un **tema scuro** che l'utente sceglie (automatico/chiaro/scuro).
- **Parte più rischiosa:** logica AoR/visibilità → coperta da test S1–S10; mantenerla testata ad ogni modifica.
- **Pagine interattive** usano `@rendermode InteractiveServer` (editor, trasferimenti, ricerca, changed, admin).
- **Sicurezza:** ogni nuova operazione di scrittura deve passare per i service Application (guardia authz + lock), mai bypassare dal repo/UI.
- **Sorgente dati (ADR-0006):** non reintrodurre nomi IVAO in Application/UI — usa le porte neutre; l'adapter IVAO resta in `Infrastructure/Ivao/*`, selezionato da `DataSource:Provider`.
- **VID vs UserId:** nel **codice** è `UserId`; a **video** resta "VID". Non rinominare le label.
- **Sezioni di un documento:** chi decide cosa è il `SectionCatalog`, **per profilo** — chi rende il corpo
  (`IsHostRendered`), se la sezione tiene anche i suoi blocchi (`KeepsOwnBlocks`), se è obbligatoria (`IsFixed`),
  se si può congelare (`IsRenderModeToggleable`, e il suo opposto `IsAlwaysLive`). Mai un insieme scritto in una
  pagina: è il debito che il doc 13 ha chiuso, e ci sono test che lo presidiano.
- **Snapshot di release = fotografie, non si riscrivono** (doc 13 §9). Chi cambia la forma di una chiave deve
  chiedersi come si comporta il viewer davanti a uno snapshot vecchio — e ricordare la regola del 26 agosto:
  *una sezione **sempre live** non è mai parte della verità di uno snapshot*, quindi si mostra anche se lì non c'è.
- **Dati di sorgente = sola lettura:** se aggiungi un campo che la sorgente può fornire, trattalo come categoria `ImportPolicy` (vedi `source-decoupling-and-import-policy` in memoria). I settori sono proiezione dei cataloghi (Round 20).

---

## 8. Mockup v2 — storico UI
🗑️ **`mockups/vipi-ui-mockup-v2.html` ELIMINATO il 2026-08-01**, insieme alla cartella `Esempi documenti/` (i .docx di partenza). Le 17 schermate del mockup sono ormai **tutte** derivate in componenti Blazor reali (vedi §4) e il prodotto ha superato il prototipo: il riferimento visivo oggi è l'app. Chi cerca l'originale lo trova nella storia git (`git show 8d661c4:mockups/vipi-ui-mockup-v2.html`); i doc più vecchi che lo citano per path sono record di sessioni passate, non istruzioni.

Note ereditate dal mockup, ancora valide: SCCAM e Aree regolamentate sono sezioni top-level; la vLOA ha due AoR e due tabelle frequenze; gli APP non remotizzati separano i trasferimenti verso ACC e verso torre.
