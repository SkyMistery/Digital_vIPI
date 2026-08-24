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

- **C'erano due basemap**, non una: la strada delle mappine (`.area-map`) creava la sua `L.tileLayer` a mano
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
