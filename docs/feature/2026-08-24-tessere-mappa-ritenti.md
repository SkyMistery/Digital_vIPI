# Mappe a scacchi alla prima apertura — le tessere che non arrivano (24 agosto 2026)

**Segnalato dalla produzione**: «a volte, quando apro una vIPI, la mappa si carica male; refresho la pagina
ed è tutto a posto». La mappa è quella dell'**AOR**, e «male» vuol dire **fondo grigio a scacchi**: la
cornice c'è, i poligoni e i numeri pure, manca la mappa sotto.

## 1. Perché succede

Due fatti misurati sul documento vero (`/services/vsop/libb/vipi`):

- quella pagina porta **77 mappe Leaflet** — l'AoR, la carta delle minime e, soprattutto, le decine di
  mappine delle aree regolamentate;
- all'apertura si accendevano tutte in fila e chiedevano **115 tessere in 19 millisecondi** a due host.

Il browser tiene sei connessioni per host e i fornitori di tessere limitano la frequenza: in una raffica
così, qualcosa cade. E **Leaflet non ritenta una tessera fallita**: quel riquadro resta grigio finché non si
ricarica la pagina — al secondo giro le tessere arrivano dalla cache del browser, ed è esattamente il
«refresho ed è tutto ok» della segnalazione.

Riprodotto in locale facendo fallire il 40% delle tessere per i primi dodici secondi: **otto secondi dopo la
fine del guasto**, 66 mappe su 77 restavano bucate e 2 completamente grigie. Nessun errore in console:
per il codice non era successo niente.

## 2. Cosa è stato fatto

- **Si ritentano le tessere cadute** (`ritentaTessere` in `vipi-aor.js`, esposto come
  `window.vipiRitentaTessere` e usato anche dai fondi di `vipi-mva.js`): cinque tentativi, attesa che
  raddoppia (0,6s → 1,2 → 2,4 → 4,8 → 9,6) e un po' di dispersione, così i ritenti di mappe diverse non
  ripartono nello stesso istante. ⚠️ Tre tentativi ravvicinati **non bastavano**: un'interruzione di dodici
  secondi se li mangiava tutti e restavano 31 mappe bucate.
- ⚠️ **La classe `leaflet-tile-loaded` la mette Leaflet solo quando il caricamento gli riesce**: dopo un
  ritento andato bene va aggiunta a mano, o la tessera resta trasparente (la dissolvenza del suo foglio
  parte da `opacity:0`). Un ritento che «funziona» ma non si vede è peggio di nessun ritento.
- **Le mappe si accendono a scaglioni**: prima quelle vicine allo schermo, poi le altre a gruppi di quattro
  ogni 300 ms. La raffica passa da 19 ms a ~5 secondi, e le tessere di chi sta guardando non fanno la coda
  dietro a settanta mappine fuori vista. ⚠️ Le altre si accendono **comunque**, non «quando si scorre»: così
  cambia il ritmo, non l'esito, e resta l'ipotesi su cui contano stampa e ricerca in pagina.

## 3. Due difetti trovati per strada

- **C'erano due basemap**, non una: la strada delle mappine (`.area-map` — ⚠️ **tolte il 27 agosto 2026**, le
  aree regolamentate hanno ora una mappa sola) creava la sua `L.tileLayer` a mano
  invece di passare da `addBasemap`, sessanta righe più su. Risultato: il ritentatore copriva l'AoR grande e
  **saltava proprio le decine di mappe che fanno la raffica**. Si è visto solo dal numero: 30 tessere rotte a
  schermo ma **28** marcate come ritentate. Quando i due conteggi non tornano, il bersaglio è sbagliato.
- **Una pagina la cui unica mappa è la carta delle minime non caricava Leaflet affatto.** Il caricatore vive
  in `vipi-aor.js` e si sveglia solo se trova un `.aor-leaflet`; `vipi-mva.js` aspetta `window.L` e basta.
  Su un APP senza shape AoR la carta restava per sempre il ripiego SVG — e lì **nessun refresh salvava**.
  Ora il caricatore guarda anche `.mva-leaflet`, e `vipiInitMva` è nella lista di `vipi-boot.js` che si
  riaggancia dopo una navigazione (prima ci arrivava solo per via dell'osservatore di mutazioni).

## 4. Verifica

Stesso guasto simulato di prima — 40% delle tessere fallite per dodici secondi, 77 mappe:

| | mappe grigie | mappe bucate | tessere a posto |
|---|---|---|---|
| prima | 2 | 66 | 173 / 334 |
| dopo | **0** | **0** | **334 / 334** |

Senza guasti: le stesse 115 richieste, ora distese fra 2,3s e 7,6s, 334 tessere su 334 a schermo, nessun
errore di console. Le chip dell'AoR e delle minime continuano a funzionare (giro del driver invariato).

## 5. Cosa resta vero

Il ripiego SVG **si vede più a lungo** sulle mappine in fondo alla pagina: prima erano tutte Leaflet entro
un secondo, ora l'ultima si accende intorno al sesto. È il prezzo di non sparare 115 richieste insieme, e
riguarda mappe che stanno sotto la piega. Chi stampa nei primi secondi stampa quel ripiego — che è il disegno
nostro, non un buco.

## 6. Seguito, sera del 24 agosto: il soffitto dei ritenti

Il sintomo è rientrato, e stavolta **anche in locale**. Il ritentatore funzionava; aveva un soffitto che
non era stato misurato, perché la verifica di §4 usava un guasto (12 secondi) più corto della scala stessa.

**La scala copre ~19 secondi** — cinque tentativi da 0,6s a 9,6s. Un'interruzione più lunga se li mangia
tutti *mentre è ancora in corso*, e dopo non riprova più nessuno: il riquadro resta nero fino al refresh,
cioè di nuovo il sintomo di §1 con la soglia spostata più in là.

| guasto indotto | prima | dopo |
|---|---|---|
| 40% per 12s | rientra in 10s | uguale |
| **80% per 30s** | **25 tessere / 9 mappe nere per sempre** (immutate a +35s) | **0 in 10s** |
| **90% per 60s** | — | **0 in 10s** |

Chiuso con lo **spazzino** (`vipi-aor.js`): ogni 8s ripassa tutte le tessere di tutte le mappe in pagina —
AoR e minime insieme, perché il guasto non distingue — ripesca le rotte sfalsandole, si ferma dopo due giri
puliti e riparte da solo su `online` e quando la scheda torna davanti. Nel caso sano le richieste restano
115, identiche.

### La trappola di misura, che è la parte da ricordare

⚠️ **`complete` non dice che la tessera è arrivata.** Una tessera fallita resta `complete` con
`naturalWidth === 0`. La prima verifica di questo seguito contava `complete && opacity > 0.5` e dichiarava
«334 su 334 visibili» su una pagina che l'utente vedeva bucata. È il gemello della trappola già scritta in
§3 (28 marcate contro 30 rotte), ma peggiore: lì i conteggi *non tornavano* e l'errore si vedeva; qui
tornavano perfetti. **Quando i numeri tornano troppo bene, sospettare il metro prima del codice.**

Corollario operativo: **senza un guasto indotto il ritentatore non viene mai eseguito.** Una verifica su
rete sana misura gli scaglioni e nient'altro, e non dice niente sui ritenti. `tile-rotte.js <quota>
<secondi>` esiste per questo e va rifatto girare a ogni modifica del ritentatore, con un guasto **più lungo
della scala**.

## 7. Difetto separato trovato per strada: `maxZoom` contro `maxNativeZoom`

Nei fondi delle minime (`vipi-mva.js`) i due erano confusi. `maxZoom` è il livello oltre il quale Leaflet
**smette di mostrare il foglio**; `maxNativeZoom` è l'ultimo livello che il fornitore possiede davvero,
oltre il quale Leaflet **ingrandisce** l'ultima tessera buona. Con `maxZoom: 13` su «World Terrain Base» il
terreno spariva del tutto appena si ingrandiva a 14 per leggere la carta — 32 tessere a zoom 13, 16 a zoom
14 — e restava il solo rilievo grigio, senza terra né mare. Proprio allo zoom in cui il fondo serve di più,
dato che esiste per spiegare *perché* la minima è quella. Sfocato è meglio che assente.

## 8. Cosa resta aperto

La segnalazione che ha riaperto il caso **non è spiegata da nessuno dei due fix**: stesso `localhost`, profilo
Edge pulito, 269 tessere e zero cadute a qualunque zoom; profilo reale dell'utente, mappe bucate; finestra
InPrivate, mappe piene; estensioni disattivate, guasto ancora presente. Le piste già escluse per misura:
la rete (i tre fornitori reggono 96 richieste in parallelo, tutte 200), il JS vecchio in cache (asset con
impronta e `no-cache`), lo zoom di pagina applicato come lo applica il sito (`vipiZoom` in `localStorage`
prima del `<head>`: le mappe grandi restano coperte oltre il 130%), e le mappine con tessere a rect zero
(stanno tutte in sezioni collassate, `checkVisibility()` false: invisibili all'utente).

Serve un dato dal browser che sbaglia — conteggio richieste, stati HTTP e tessere rotte — per separare i tre
mondi: fornitore che limita (403/429 → fondo di ripiego su un altro fornitore), blocco locale a livello di
rete (richieste vuote), oppure tessere **mai chieste** (allora è geometria, e né spazzino né ripiego servono).

## 9. Il ripiego pulito: quando il fondo non arriva, si toglie

Ritenti e spazzino coprono i guasti che **passano**. Contro un blocco **stabile** non esiste ritento che
tenga: si riprova e si resta col riquadro a scacchi. Lì la cosa giusta è smettere — via il fondo, restano
lo sfondo neutro e i nostri poligoni, che sono il dato che la mappa esiste per mostrare. È deliberatamente
la stessa faccia del **ripiego SVG** di prima che Leaflet carichi (`--surface-soft`, nessuna etichetta):
una mappa senza fondo si legge come una scelta, una a scacchi come un guasto. Sparisce anche l'attribuzione
del foglio, ed è corretto — non stiamo più mostrando le tessere di nessuno.

| scenario | esito |
|---|---|
| blocco permanente | 75/75 senza fondo al ~40° secondo, **80 poligoni intatti** |
| guasto passeggero 30s | **0/75** senza fondo, 280 tessere recuperate dallo spazzino |
| blocco 60s poi rientro | spento al 40°, **tornato da solo** entro l'80° |
| rete sana | nessun cambiamento |

### Due tarature che il codice non spiega da sé

⚠️ **Il conto sta sui giri dello spazzino, non su `data-vipi-ritento`.** Quell'attributo si incrementa
quando il ritento viene *programmato*, non quando fallisce: arriva a 5 già al **nono** secondo mentre
l'ultimo tentativo è in volo fino al **diciannovesimo**. Chi lo usasse come «scala esaurita» scambierebbe
un guasto passeggero per un blocco permanente.

⚠️ **Quattro giri, non tre.** Con tre si rinuncia al 32° secondo, che cade **dentro la coda** di un guasto
di 30: il giro vede ancora rotto quel che la sua stessa richiesta sta per riparare, e nella prova **38 mappe
su 75 perdevano il fondo per una quarantina di secondi** — mappe che lo spazzino recuperava da solo. Il
blocco vero costa otto secondi in più di attesa, che è il prezzo giusto da pagare in quella direzione.

Attrezzo: `ripiego.js <blocco|passeggero|sano> <secondi>` nello scratchpad di sessione.

## 10. Epilogo della segnalazione del 24 agosto

Era **un'estensione del browser**, e l'ipotesi era stata avanzata e poi abbandonata per un dato mal letto:
l'utente le aveva disattivate e riferiva «ho ancora il problema», ma **in Edge disattivare un'estensione non
la ferma finché il browser non si riavvia**. Dopo il riavvio la sonda ha dato **286 tessere, 0 ritentate,
0 spazzate, 0 rotte**: nessuna caduta. La prossima volta «le hai disattivate» non basta — si chiede **«hai
riavviato il browser?»**, e finché la risposta non c'è l'ipotesi resta viva.

Il lavoro dei §6–§9 resta comunque giustificato, ma per un altro motivo: il soffitto dei ritenti era reale
(§6) e un utente con un bloccante attivo è un caso che si ripresenterà, perché non si può chiedere a chi usa
lo strumento di smontarsi il browser.

---

## 🔴 Aggiunta del 27 agosto 2026 — il fondo ora vuole una API key

Trovato guardando gli **screenshot** di una verifica su tutt'altro, non cercandolo: le tessere di
`basemaps.cartocdn.com/light_all` arrivano stampigliate **«API KEY REQUIRED — carto.com/basemaps/apikey»**.
CARTO ha chiuso il basemap anonimo.

⚠️ **Niente di quel che sta scritto sopra se ne accorge, ed è giusto così.** Il ritentatore reagisce alle
tessere che **cadono** e lo spazzino a quelle che restano nere: qui la tessera **arriva**, con dentro la
scritta. Non è un guasto di rete, è il fornitore che ha cambiato condizioni.

Riguarda **tutte** le mappe del prodotto — AoR 2D, visore 3D, aree regolamentate, carte delle minime — ed è
**già così in produzione**. Il posto da toccare è uno solo, `addBasemap` in `vipi-aor.js`, più la copia in
`vipi-mva.js`. Decisione del committente: chiave CARTO (c'è un piano gratuito) o altro fornitore di tessere.
Vedi `docs/lavori-aperti.md` **§N3**.
