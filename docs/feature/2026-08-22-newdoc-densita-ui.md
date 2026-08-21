# Nuovo documento — densità UI (carta, 22 agosto 2026)

> Ramo `ui-trasferimenti-densita`, pagina `/vsop/editor/newdoc`. Seconda carta del giro: **la forma**.
> La sostanza sta nella gemella [`2026-08-22-newdoc-cosa-crea.md`](2026-08-22-newdoc-cosa-crea.md).
> Tredicesima pagina del giro. Regole: [regole-ui-pagine-admin](../design/regole-ui-pagine-admin.md).

## Il numero, e perché stavolta la ricognizione aveva ragione

**957px** con la scheda vLOA — quella che si apre per prima — e **900** con le altre tre. Confermato a
1600/1440/1280/1024 e in entrambe le lingue: la pagina scorre **solo** nella scheda vLOA, di 57px.

⚠️ Ma il numero non è la misura giusta: **la pagina ha quattro schede e ognuna ha un'altezza sua**. Misurare
«la pagina» vuol dire misurare la scheda che si apre per prima, e non è detto che sia quella che pesa. Qui
per fortuna coincidono; la lezione resta: su una pagina a schede si misura **ogni scheda**, e quella che
conta è la più alta.

E l'altezza **non dipende dai dati**: 7 ACC italiani, 21 esteri, 92 aeroporti — tutti dentro menu a tendina,
che non crescono. È il caso opposto a Diagnostica.

## Il difetto più grave non è un'altezza

⚠️ **Il tasto che conclude sta PRIMA di quattro dei cinque campi che gli servono.** L'ordine a schermo, oggi:

```
TITOLO             [_________________________]
                   [ Crea e apri editor ]        ← qui
ACC ITALIANO (HOME)[ ACC…            ▾]
SETTORE HOME       [ settore…        ▾]
ACC ESTERO         [ ACC…            ▾]
SETTORE NEIGHBOUR  [ settore…        ▾]
```

Si legge come se «titolo + Crea» bastasse. E infatti chi lo preme ottiene *«Seleziona il settore Home e il
settore Neighbour»* — un errore che la pagina ha causato con la sua disposizione. Viene dal markup: il primo
`inline-form` contiene titolo **e** bottone, i due successivi contengono i quattro menu. Nessuna misura lo
trova: si vede guardando la schermata.

## Misurato prima di toccare (1600×900, IT)

| Pezzo | Quanto | Perché se ne va |
|---|---|---|
| Sottotitolo in fascia | testata 65px | nel «?» (regola 7) |
| Cinque campi a **larghezza piena** (940px) per contenere un menu da 22 voci | il blocco vLOA fa **542px** | i campi si misurano sul contenuto: una griglia, non uno stack |
| Due paragrafi di prosa nel blocco vLOA | ~40px | «?» della scheda |
| Prosa in **ognuna** delle altre tre schede | 1 riga ciascuna | idem |
| Etichetta scheda «vIPI APP (non remotizzato)» | manda la fila di schede a 4 elementi larghi | «APP», e il «non remotizzato» nel «?» |
| Barra del lock sopra le schede | 44px | **resta**: la pagina è corta e la fascia è la forma giusta (§15). Ma deve dire **a cosa** si riferisce |
| `.wrap` a 1 100px, niente barra admin | — | vedi sotto |

## Le due decisioni di forma

### 1. I campi si dispongono, non si impilano

Cinque campi larghi 940px l'uno sotto l'altro per contenere «LIRR · Roma» sono la ragione dei 542px. Una
griglia a due colonne — *ACC Home | Settore Home* sopra, *ACC estero | Settore Neighbour* sotto — dice anche
qual è la **coppia**, che è la cosa che la vLOA è. E il tasto va **in fondo**, dove finisce quello che gli
serve.

### 2. La barra admin: sì, e la briciola va via

⚠️ Decisione lasciata aperta dalla ricognizione (§15) e da chiudere qui. `AdminNav` **non** ha questa voce
e la pagina ha ancora la briciola `Home › Bozze & versioni › Nuovo documento`.

**La barra si mette, la voce no.** Sono due cose diverse e vanno decise separatamente:

- **la barra sì**, perché è una pagina di lavoro admin come le altre dodici, e da qui si va spesso altrove
  (Struttura per creare un settore che manca, Aeroporti per assegnarne uno, Confinanti per le vLOA in
  blocco): oggi ci si arriva solo tornando indietro;
- **la voce no**, perché `AdminNav` è già a undici voci e a 1 100/1 200px va su due righe. «Nuovo documento»
  non è una destinazione che si cerca: ci si arriva **da Documenti**, che nella barra c'è già, e col tasto
  che sta lì. Aggiungerla renderebbe la barra peggiore per tutte e tredici le pagine per servirne una.

⚠️ È il primo caso del giro in cui una pagina prende la barra **senza** entrare nell'elenco, e va scritto
nella regola: `AdminNav` mostra **dove si può andare**, non **dove si è stati**. La voce accesa non è un
requisito per rendere la barra.

E la briciola va via come sulle altre dodici: la barra ne fa il lavoro meglio. Resta il tasto «← Documenti»
in testata, che è l'unico salto che la briciola faceva e che la barra copre con la voce «Documenti».

## Testata in una riga

`titolo · «?» · Documenti`, con `.doc-head.st-head` come le altre dodici. Il «?» della pagina spiega la
differenza che il nome nasconde: **la vLOA la crei qui, gli altri tre li apri** — e l'editor li crea alla
prima apertura. È la frase che chiude il difetto N5 della carta della sostanza dal lato della prosa; dal lato
del comportamento la chiude il tasto che cambia etichetta.

⚠️ Il «?» che oggi la pagina ha (contato: 1) **non è suo**: è quello di `EditLockBar`.

## Le quattro schede

- L'ordine cambia: **ACC · APP · Aeroporto · vLOA**. Oggi la vLOA è la prima e apre il form più lungo, ma è
  il documento più raro — se ne crea uno per coppia, e per le coppie confinanti li genera Confinanti in
  blocco. Le tre vIPI sono il lavoro di tutti i giorni.
- Le etichette si accorciano: `vIPI ACC` → **ACC**, `vIPI APP (non remotizzato)` → **APP**,
  `vIPI Aeroporto` → **Aeroporto**, `vLOA` resta. Il prefisso «vIPI» è vero per tre schede su quattro:
  distingue niente e paga larghezza su ognuna.
- La barra del lock dice **a cosa serve**: si vede solo sulla scheda vLOA, che è l'unica che crea. Sulle
  altre non c'è niente da serializzare (aprire un editor prende il lock **di quel documento**, che è un'altra
  cosa e la gestisce l'editor).

## Prosa: «?» e Guida

- `HelpHint Href="/vsop/guida#nuovo-documento"` in testata con: cosa fa davvero la pagina (crea la vLOA,
  apre gli altri), e che i tre vIPI sono **uno per bersaglio**.
- Un «?» per scheda, con la prosa che oggi sta nel blocco: cos'è una vLOA e che per le confinanti c'è
  Confinanti; che la vIPI ACC è **una per ACC**; che gli APP sono solo i **non remotizzati** e da dove viene
  quella distinzione (Struttura); che l'aeroporto si **genera dalle entità strutturate**.
- Sezione `#nuovo-documento` nella Guida, IT **ed** EN, e voce in `GuideSearchCatalog`.
- ⚠️ I messaggi «non c'è niente qui» (`Nd_NoForeignAcc`, `Nd_NoStandalone`, `Nd_NoAirports`) **restano in
  pagina**: non sono prosa d'aiuto, sono la **risposta** a una tendina vuota, e dicono dove si rimedia. È la
  distinzione già fatta su Sorgenti fra «descrizione del dato» e «prosa che spiega il meccanismo».

## Come lo verifico

Skill `verifica-live` su copia del DB, exe pubblicato avviato **dalla sua cartella**, porta libera,
sentinella in pagina prima di credere a un numero.

⚠️ **Quattro schede = quattro misure**, e ognuna in due stati: vuota e **con un ACC scelto** (che è quando
compaiono le tendine dipendenti e i messaggi di elenco vuoto). Più:

1. la vLOA **compilata** per intero, che è l'altezza vera della scheda;
2. il caso «documento già esistente» per ACC/APP/Aeroporto — il tasto deve dire «Apri», non «Crea»;
3. il caso **senza lock** (un'altra sessione lo tiene): il tasto vLOA spento e il perché accanto, non solo in
   cima;
4. un ACC senza APP standalone e uno senza aeroporti (i due messaggi di elenco vuoto);
5. ⚠️ un utente con **grant ma non admin**, che è la slice 3 della carta sostanza: deve vedere la pagina e
   **solo i suoi ACC**. Lo stato si costruisce scrivendo un grant nella copia del DB.

Passi: **1600 / 1440 / 1280 / 1024**, **IT ed EN**, zoom **0.8 → 1.5** con `window.vipiSetZoom`. E poi
**guardare** gli screenshot: il difetto peggiore di questa pagina — il tasto sopra i suoi campi — nessuna
misura lo trova.

## Slice di questo pezzo (dopo le quattro della sostanza)

5. **Testata in una riga** + barra admin (senza voce) al posto della briciola + prosa nei «?» + schede
   riordinate e rinominate.
6. **La griglia dei campi** e il tasto in fondo; la barra del lock solo sulla scheda che crea.
7. **Guida** `#nuovo-documento` (IT/EN) + voce nel catalogo di ricerca.
8. **Misura e rifiniture**: quattro schede × due stati, quattro assetti, due lingue, cinque zoom; poi carta +
   regole + ricognizione §15 + memoria.

⚠️ Trappole già pagate che valgono qui: le regole CSS nuove in **coda** al foglio e con `.struct` davanti;
una classe non può significare due cose; `.se-row input{flex:1}` è la regola dei **campi di testo** e colpisce
anche le checkbox (trappola di Sorgenti, e qui ci sono cinque campi); `@bind` a un valore che non è fra le
opzioni **non ne sceglie nessuna** (trappola di Permessi, e qui ci sono quattro tendine dipendenti); e le
stringhe nuove si rileggono **in pagina italiana**.
