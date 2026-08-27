# Il fondo delle mappe non è più CARTO

**27 agosto 2026.** Eseguita. Suite verde, build Release 0 avvisi.

## Il fatto

Le tessere di `basemaps.cartocdn.com/light_all` arrivavano stampigliate **«API KEY REQUIRED —
carto.com/basemaps/apikey»**: CARTO ha chiuso il basemap anonimo. Riguardava **tutte** le mappe del
prodotto — AoR 2D, pavimento del visore 3D, aree regolamentate — ed era già così **in produzione**.

⚠️ **Il guasto era invisibile alle nostre reti, e non per una loro mancanza.** Il ritentatore, lo
spazzino e la rinuncia al fondo (carta del 24 agosto) guardano tutti la stessa cosa: la tessera che
**non arriva**. Qui la tessera arriva, `200`, immagine valida, `naturalWidth > 0` — con la scritta
sopra. *Un fornitore che chiude il rubinetto non si vede dal codice della risposta*, e nessun ritento
lo rimedia. L'ha trovato una persona guardando gli screenshot della verifica.

## La scelta

Quattro strade erano aperte: chiave CARTO, OpenStreetMap standard, un altro fornitore senza chiave,
tessere nostre. Si è presa **Esri «Light Gray Canvas»**, che è la porta accanto:
`server.arcgisonline.com` **il prodotto lo interroga già** — è il fondo a rilievo delle carte delle
minime di vettoramento (`vipi-mva.js`) — quindi non entra un host nuovo, entra un foglio nuovo su un
host già nostro; e il grigio chiaro è lo stesso registro di Positron, cioè un fondo che **si fa
dimenticare** sotto i poligoni, che sono il dato.

⚠️ **Questo chiude il problema di oggi, non la sua categoria.** Esri, come CARTO fino a ieri, ci
serve a titolo gratuito e senza contratto: il giorno in cui decidesse diversamente saremmo qui a
riscrivere le stesse righe. La strada che *non* si ripresenta sono le **tessere nostre** (un file
PMTiles della sola Italia, servito da noi, CSP di nuovo `'self'`), ed è la sola che valga la pena di
mettere in conto come lavoro vero. Vedi `lavori-aperti.md` §N3.

## Cosa è cambiato

**Due fogli, non uno** (`addBasemap` in `vipi-aor.js`). Positron `light_all` portava fondo e nomi
nella stessa tessera; in Esri il fondo (`World_Light_Gray_Base`) è **muto** e le etichette stanno in
`World_Light_Gray_Reference`. Prendere solo il primo avrebbe dato una mappa d'Italia senza il nome di
una città sopra — una perdita che nessun test avrebbe segnalato, perché nessun test guarda un fondo.

⚠️ **L'indirizzo è `{z}/{y}/{x}`, non `{z}/{x}/{y}`**: ArcGIS numera per riga/colonna, il contrario
dello schema slippy. Invertirli **non dà errore**: dà un altro pezzo di mondo sotto i settori. È la
stessa forma già scritta in `vipi-mva.js`, ed è da lì che è stata copiata.

⚠️ **`maxNativeZoom: 16`, non `maxZoom: 16`.** Il fornitore possiede i livelli fino al 16; oltre,
`maxNativeZoom` fa **ingrandire a Leaflet** l'ultima tessera buona, mentre `maxZoom` gli farebbe
togliere il foglio. La differenza si era già vista a schermo sulle minime, dove il fondo spariva
appena si ingrandiva: sfocato è meglio che assente.

ℹ️ **Sparisce `{r}`, cioè `@2x`.** Esri non ha la variante a doppia densità, che Leaflet chiedeva da
solo sugli schermi HiDPI (`r: retina ? '@2x' : ''`, **anche senza** `detectRetina`). Sugli schermi
fini il fondo è appena più morbido; i poligoni sono vettoriali e restano nitidi. Non si perde
struttura, si perde una rifinitura dello sfondo.

**Il pavimento del 3D** (`vipi-aor3d.js`) cuce le tessere a mano su un canvas, quindi non ha layer da
impilare: le etichette si dipingono **sopra** il fondo della stessa casella, e si chiedono **dopo**
che quel fondo è arrivato — partendo insieme, un `Reference` veloce finirebbe coperto dal `Base` che
arriva dopo. Il conto del «pronto» resta sui soli fondi: il pavimento appare con la geografia e i
nomi compaiono poco dopo, invece di far aspettare il primo render per uno strato che non porta forma.

ℹ️ Qui l'`@2x` non costa quasi nulla, e vale la pena scriverlo perché sembrava il contrario: la
texture è **256 px per casella** (`canvas.width = cols * 256`), quindi la tessera doppia veniva già
rimpicciolita in fase di disegno. Era supersampling, non risoluzione. Per un pavimento più fine si
alza il canvas, non si cerca un fornitore che abbia l'`@2x`.

## Trovato per strada: la CSP parlava di un mondo vecchio

`img-src` elencava **solo** `*.basemaps.cartocdn.com`. Le carte delle minime esistono da giorni e
chiedono tessere a `server.arcgisonline.com` e `*.tile.opentopomap.org`: **due host fuori dalla
politica**. Non se n'era accorto nessuno perché l'intestazione è **`Content-Security-Policy-Report-Only`**
— segnala e non blocca, e i segnali non li leggeva nessuno. Ora la riga nomina i due host veri e il
commento dice quale mappa usa quale.

⚠️ Vale come promemoria per il giorno in cui la Report-Only diventerà una CSP vera: quel giorno la
riga sbagliata **spegne le mappe**, non le segnala.

## Attribuzione

Dovuta: **© Esri, HERE, Garmin, © OpenStreetMap contributors** (stringa presa dal `copyrightText` del
servizio). La mostra il controllo di Leaflet, scritta **una volta sola** perché è la stessa per i due
fogli. `THIRD-PARTY-NOTICES.md` aggiornato.

🔵 **Resta scoperto, ed è di prima**: il pavimento del **3D** non mostra attribuzione — non è una
mappa Leaflet, è una texture su un piano Three.js, e non c'era neanche con CARTO. Non è stato aperto
in questo giro perché è una scelta di interfaccia (dove si scrive, in un visore che si ruota), non
una riga di codice.

## Verifica

Endpoint provati dal vivo prima di scrivere una riga: `Base` e `Reference` rispondono `200`
(`image/jpeg` e `image/png`), `Access-Control-Allow-Origin: *` — che è ciò che tiene il canvas del 3D
non «tainted» —, `Cache-Control: max-age=86400`, e il livello 16 esiste sull'Italia.

Suite: tutti gli assembly verdi, net8 e net10. ⚠️ **Nessun test copre questo cambio**, ed è onesto
dirlo: le mappe sono JavaScript non esercitato, e un fondo giusto non si distingue da uno sbagliato
se non guardandolo.
