# Il pannello release (carta, 22 agosto 2026)

> Ramo `ui-trasferimenti-densita`, componente `ReleasePanel` — montato da **tutti e quattro** gli editor
> (ACC, APP, vLOA, aeroporto). Non è una pagina: è il pezzo che ogni giro di editor ha misurato e rimandato.
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md); regole: [regole-ui-pagine-admin](../design/regole-ui-pagine-admin.md).

## Il punto di partenza, misurato

**974px** sull'editor ACC con 10 release: testata 72 + **10 righe × 68px** + form in fondo 39.

⚠️ **Correzione a quello che era scritto nell'handoff**: la retention **c'è già**
(`ReleaseRetentionOptions.KeepSupersededWithinCycles = 13`, ≈ un anno AIRAC), quindi il pannello **non
cresce per sempre** — si ferma intorno alle 13-14 righe, cioè ~1 100px. È un **massimo strutturale**, non
una crescita aperta come il registro di audit. Cambia il peso del problema: è densità, non rischio.

Nel database di prova le release sono 34 in tutto, e il bersaglio più carico è `AccVipi LIBB|LIBB_ES_CTR`
con **10, di cui 9 superate**.

## Cosa ho trovato

### ⚠️ R1 — Annullare una release passa dal `confirm()` NATIVO del browser

`CancelRelease` chiama `JS.InvokeAsync<bool>("confirm", …)`. È **esattamente** quello che il giro Versioni
aveva tolto il 21 agosto, e per due ragioni che valgono qui uguali:

- il `confirm` nativo **blocca il circuito Blazor** finché non si risponde;
- il testo utile — *quale* release si sta annullando — finisce dentro una finestrella di sistema invece che
  accanto al tasto che l'ha aperta.

E qui pesa di più che altrove: annullare una release è **irreversibile** e tocca un documento **pubblicato**.
La cura è `InlineConfirm`, che il progetto ha già e che le altre pagine usano da un mese.

### ⚠️ R2 — «VID 704798» nudo, per la terza volta

La riga dice `rilascio #13 · **VID 704798** · 31 lug 2026 17:15`. È la regola 124 — «un VID non è un nome» —
già pagata su **Permessi** (dove i VID si risolvono col roster) e su **Incarichi** (idem). Il pannello release
è il terzo posto, ed è quello che si guarda quando si vuole sapere **chi ha pubblicato**.

⚠️ Il roster (`GetDisplayNamesAsync`) è già in casa e risolve in **una** chiamata per pagina: non è una query
per riga.

### R3 — La riga è alta 68px per un contenuto che sta in una

Titolo e meta vanno su due righe: `AIRAC 2607 · 🟢 In vigore` sopra, e sotto `rilascio #13 · VID · data ·
in vigore dal …` più tre tasti. Su dieci righe sono 680px per una storia che si scorre, non si legge riga
per riga.

### R4 — Dieci release tutte a schermo, e nove sono superate

Delle dieci, **nove sono `Superseded`**: storia. Quella che conta — la release **in vigore** e le eventuali
**programmate** — sono una o due. Le altre si guardano quando si va a cercare qualcosa di preciso.

## Cosa cambia

1. **`InlineConfirm` al posto del `confirm()` nativo**, con la release nominata nella domanda
   («Annullare la release AIRAC 2607?»).
2. **Il VID diventa un nome**, risolto dal roster in una chiamata per pannello. Se il roster non lo conosce
   resta il VID: meglio un numero che un trattino.
3. **Riga da 68 a ~40px**: una riga sola, e il resto (ora esatta, nota) nel `title`.
4. **Le superate oltre le prime tre** in un «altre N» che si apre — `<details>`, come tutto il resto del
   progetto. ⚠️ **La release in vigore e le programmate non si collassano mai**: sono lo stato del
   documento, non storia.

Atteso: **974 → ~350px**, su tutti e quattro gli editor.

## Fuori ambito, dichiarato

- **La retention non si tocca**: 13 cicli sono una scelta già presa e documentata, e il pannello dopo questo
  giro non è più il pezzo che detta l'altezza.
- **Il diff** (`ReleaseDiffTable`) resta com'è: si apre su richiesta e non pesa finché è chiuso.

## Com'è andata

**974 → 420px** sull'editor ACC di LIBB (10 release, il bersaglio più carico del database). La riga è passata
da **68 a 37px**, e delle dieci release ne restano quattro a schermo — la in vigore più le tre superate più
recenti — con «Altre 6 release superate» che si apre.

## Tre cose imparate sbagliando

1. ⚠️ **Il roster non si inietta.** `@inject IStaffRosterRepository` ha reso il pannello **non montabile** per
   chiunque non l'avesse registrato, e ha spento **18 test in un colpo**. Dare un nome a un VID è una
   **comodità**, non una dipendenza del pannello: si risolve dal service provider, e senza restano i VID —
   che era già il ripiego dichiarato. ⚠️ Regola generale: un componente **condiviso** non acquisisce
   dipendenze obbligatorie per una comodità, perché il costo lo pagano tutti i suoi host.
2. ⚠️ **Una `}` di troppo scarta UNA regola, e sembra un problema di specificità.** Avevo lasciato una graffa
   in più negli scaglioni della topbar: il parser CSS chiudeva il foglio in anticipo e **scartava la prima
   regola dopo** — solo quella. Le regole *figlie* funzionavano (`#p-release .rel-row .btn` sì), la madre no,
   e il valore calcolato restava quello vecchio. Ho cercato a lungo una specificità che non c'entrava niente.
   **Un conteggio delle graffe lo trova in un secondo** e va fatto prima di ogni altra ipotesi.
3. ⚠️ **`flex-shrink:0` non è il modo di dare larghezza a un campo** (vedi la carta della topbar).

## Come si verifica

Guidare **l'editor ACC di LIBB** (10 release, il bersaglio più carico del DB) e almeno un altro editor, a
1600 e 1280, IT ed EN:

- l'altezza del pannello chiuso e aperto;
- che la release **in vigore** sia sempre visibile senza aprire niente;
- ⚠️ che l'**annulla** chieda in linea e nomini la release — e che il circuito **non si blocchi**, che è la
  ragione vera per cui il `confirm` nativo se ne va;
- che il nome di chi ha pubblicato ci sia, e che dove il roster non sa resti il VID.

## Slice

1. R1: `InlineConfirm` al posto del `confirm()`.
2. R2: il VID risolto col roster.
3. R3/R4: riga compatta e superate oltre le prime tre in un «altre N».
