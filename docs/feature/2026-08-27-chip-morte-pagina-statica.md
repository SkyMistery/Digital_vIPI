# Le chip che non facevano niente (METAR/TAF e pista delle SID)

**27 agosto 2026, sera.** Segnalato dal committente: «quando clicco sulle chip per passare da METAR a TAF, o
per cambiare pista nella schermata SID di un aeroporto, non funziona».

Riprodotto e chiuso. Due sintomi, **una radice**: la vIPI d'aeroporto è diventata **SSR statica** (doc 14
§3g) e lo stato dei suoi comandi è rimasto dov'era.

## Il meteo: l'isola che non è mai stata promossa

Quando la pagina è passata da `InteractiveServer` a statica, le parti interattive sono state promosse a
**isole**: `AirportListPanel` e `AirportSids`. `AirportWeather` no.

Il risultato è che i suoi `@onclick` non erano agganciati a niente. Nell'HTML servito i due bottoni escono
così — **senza un solo attributo Blazor**:

```html
<div class="wx-tabs"><button class="wx-tab on">METAR</button><button class="wx-tab ">TAF</button></div>
```

⚠️ Il clic **non partiva nemmeno**: niente sul WebSocket, niente nei log del server, niente in console. Un
comando morto non è un errore, è un silenzio.

Cura: `@rendermode InteractiveServer` sul componente. ℹ️ **Non aggiunge un circuito**: `<LiveBadge>` nella
barra è già un'isola su **ogni** pagina, quindi il circuito c'è comunque. Costo misurato: **+2 806 byte** di
descrittore sulla pagina di `LIBD` (42 997 → 45 803 non compressi; 16,7 KB compressi in tutto), perché
`WeatherReport`, `ParsedMetar` e `ParsedTaf` attraversano il confine SSR→interattivo e vengono serializzati.

## Le SID: l'isola c'era, lo stato no

Qui il difetto è più insidioso, perché **l'isola esiste** e funziona: il clic arriva davvero al server
(`DispatchEventAsync` sul WebSocket) e torna anche un `RenderBatch`. Semplicemente non cambia niente.

```razor
// AeroportoPage.razor — pagina STATICA
<AirportSids Selected="@_sidRwy" SelectedChanged="SelectSidRwy" … />
private void SelectSidRwy(string rw) { _sidRwy = rw; _sidRwyUserPicked = true; }
```

⚠️ **Lo stato della scelta viveva nel genitore statico.** Il gestore aggiornava il campo di una pagina che
**non si ridisegnerà mai più**, e l'isola continuava a ricevere il valore congelato nel suo descrittore. Il
commento del componente diceva *«l'host la possiede perché sopravvive al cambio sezione»*: era vero quando la
pagina era interattiva, ed è sopravvissuto al cambio che l'ha resa statica.

**La regola generale**: *uno stato che cambia deve vivere dentro l'isola che lo cambia*. Un genitore statico
può solo **seminarlo**. Quindi `Selected`/`SelectedChanged` diventano un solo parametro `InitialRunway`, letto
**una volta** in `OnParametersSet`; e nella pagina sparisce `_sidRwyUserPicked`, che fingeva di sapere una
cosa — la scelta del lettore — che da lì non passa più.

## Perché i test non l'hanno visto, e cosa si è aggiunto

Le cinque prove su `<AirportSids>` montavano il componente con la pista **già scelta** e guardavano il
risultato. Il filtro era giusto e lo è sempre stato: a non funzionare era **il modo in cui la scelta
arrivava**. Chip premute da un test: **zero**.

Ora si preme: `La_chip_di_pista_cambia_davvero_le_SID_mostrate` (e il rovescio,
`Il_seme_non_torna_a_riprendersi_la_scelta_del_lettore`, che presidia il difetto scritto al contrario: un
seme ripreso a ogni giro di parametri riporterebbe il lettore alla pista in uso). Più due sul meteo, che non
aveva **nessun** test.

⚠️ **E va detto quel che i test ancora non possono fare**: bUnit **ignora i render mode**. La prova del
meteo sarebbe passata anche prima della cura, perché bUnit monta il componente come se fosse interattivo. Il
render mode è un fatto dell'**hosting**, non del componente: l'unica rete che lo vede è il browser vero.

## Verifica live

Browser vero (Edge headless), DB copiato, `LIBD`:

| | prima | dopo |
|---|---|---|
| chip meteo dopo il clic su TAF | `METAR* · TAF` — vista invariata | `METAR · TAF*` — «TAF LIBD 271700Z…» |
| chip pista dopo il clic su 25 | `07🛫* · 25`, 18 righe, prima riga `07 BANAV` | `07🛫 · 25*`, 17 righe, prima riga `25 BANAV` |

Provati anche `LIBR` (chip seminata su `31🛫`, andata e ritorno METAR→TAF→METAR) e `LIBG`, che non ha né
meteo né SID: nessun errore, in console o nei log.

## Propagazione: chi altro è rimasto indietro

La domanda giusta dopo un difetto così non è «ho corretto?» ma «**quanti altri comandi sono morti nello
stesso modo?**». Chiusura transitiva dei componenti raggiunti dalle undici pagine pubbliche statiche,
fermandosi alle isole, cercando `@onclick`/`@onchange`/`@bind` senza `@rendermode`:

- **Quattro candidati**, tutti falsi: `AppConfigurations`, `AppFrequencies`, `AppSeparations`, `AppVfr` hanno
  i gestori dentro il ramo `Editing`, che nel viewer è `false` — i comandi non vengono proprio resi.
- Su tutti i bottoni **fuori** dalle isole nelle pagine pubbliche: tema, zoom, stampa e le chip dell'AoR/3D,
  tutti serviti da `onclick` inline o dalla delega in `vipi-aor.js`. Nessun altro comando muto.

ℹ️ Sulle chip di pista e su quelle di configurazione dell'ACC cade la **stessa classe** `.cfg-btn` con due
meccanismi diversi (Blazor qui, JS là), ma non si pestano i piedi: la delega di `vipi-aor.js` esce subito se
il bersaglio non sta in un `.aor-block`, e `wireAor` lavora sullo stesso scope.
