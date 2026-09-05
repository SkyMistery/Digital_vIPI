# Il payload della scheda non mangia più il contenuto di chi redige (5 settembre 2026)

> **Segnalazione**: «in una sezione ho fatto una tabella e subito sotto un blocco immagine. Sembrava tutto ok;
> una volta chiuso l'editing la tabella spariva. Ho creato una sotto-sezione per l'immagine e ora si vedono
> entrambi.» Non era un difetto momentaneo: era una **perdita di dati**, e si riproduce a comando.

## Che cosa succedeva

Le sezioni «scheda + blocchi» (`HostAndBlocks`: nel vSOP militare sono Radioassistenze, Frequenze ATC/CRC,
Aeroporti alternati, Piste, Nominativi, Parcheggi, Aree di lavoro) tengono **due** cose: il **payload** della
scheda che disegna la pagina, e i **blocchi editoriali** di chi redige. Dove stia il payload lo diceva la regola
**«il primo blocco che un `BodyJson` ce l'ha»** (`SectionPayload`, e il gemello in scrittura
`EfEditingRepository.SaveSectionBlockJsonAsync`).

Ma un `BodyJson` ce l'hanno anche **la tabella scritta a mano**, **l'immagine** e **l'allegato**. E i vSOP
militari nascono **senza blocco segnaposto**. Quindi, in una sezione ancora vergine, il primo blocco con un JSON
era quello di chi redige, e:

1. la scheda **leggeva** quella tabella come se fosse la propria struttura;
2. al primo salvataggio della scheda il payload le veniva scritto **sopra** — contenuto **perso**, non nascosto;
3. il JSON riscritto porta una `variant`, e un blocco tabella con variante l'editor e il documento lo saltano
   apposta: la tabella spariva **anche dall'editor**.

Combacia riga per riga con la segnalazione: la tabella era il primo blocco con JSON e l'ha mangiata la scheda;
l'immagine, arrivata dopo, è rimasta. E una sotto-sezione libera non ha nessuna scheda — per questo lì tutto si
rivede.

**Riprodotto dal vivo** (vSOP militare di Grottaglie, sezione «Radioassistenze», editor vero):

```
prima:  {"columns":["Colonna 1","Colonna 2"],"rows":[{"cells":["CELLA-MIA",""]}]}
dopo:   {"variant":"milnavaids","rows":[{"code":"TAR","kind":"VHF","channel":null}]}   ← stessa riga, id 542
```

## La correzione

La regola diventa **«il primo blocco di STRUTTURA»**, e un blocco **editoriale non è mai un payload**: non si
legge come tale e non si riscrive. Chi sono lo dice `SectionPayload.EEditoriale`, che li riconosce dalla
**forma** del JSON — e non dal formato del blocco, perché il payload di una sezione militare è anch'esso un
blocco `Table`:

| forma | chi la scrive | verdetto |
|---|---|---|
| `mediaId` | `MediaRef` (immagine) | editoriale |
| `ref` | `AttachmentRef` (allegato) | editoriale |
| `columns` **senza** `variant` | tabella generica scritta a mano | editoriale |
| tutto il resto — `variant`, `{"Key":…}`, `{"OwnAuto":…}`, un array `["1029",…]` | le schede | payload |

La stessa domanda la fanno adesso **tutti e cinque** i posti che prima la facevano a modo loro: lettura per
chiave e per sezione, scrittura per chiave e per sezione, e le due proiezioni dell'assembler ACC. In scrittura
l'ordine è: *struttura esistente → blocco VUOTO (il segnaposto delle altre famiglie) → uno nuovo **in coda***.

⚠️ Il secondo passo prima chiedeva solo «senza prosa»: un blocco **immagine** ha la prosa vuota (la didascalia)
e sarebbe stato scelto. Ora si chiede vuoto davvero — né prosa né JSON.

**Niente migrazione, niente schema**: cambia solo chi sceglie il blocco. La finestra cieca fino al 16 settembre
resta intatta.

## Verifiche

- Suite verde su due TFM, `dotnet build Vipi.slnx -c Release --no-incremental` 0 avvisi.
- Prove nuove: `SectionPayloadTests` (le tre forme editoriali, i payload veri di tutte le famiglie, la tabella
  di struttura che si distingue per la `variant`, il JSON rotto che non si riscrive) e due in
  `EditingRepositoryTests` che rifanno il difetto — tabella a mano e immagine — su un militare nato **come
  nasce davvero**, senza segnaposto.
- **Verifica live, stesso gesto di prima**: la tabella resta intatta (id 542 invariato), il payload della scheda
  va in un blocco nuovo in coda (543), e a schermo si vedono **tutte e due** — la tabella di chi redige e la
  radioassistenza della scheda — sia in lettura sia in modifica.

## Che cosa NON copre

Resta aperta l'asimmetria fra editor e viewer sulle sezioni rese dalla pagina che **non** tengono i blocchi
(`Host` puro): lì l'editor di aeroporto e APP considera «derivata» solo la sezione a `Depth == 0`, mentre il
viewer non guarda la profondità — quindi su una sotto-sezione con chiave di catalogo si possono ancora
aggiungere blocchi che il documento non stampa. Non è una perdita di dati (il blocco resta), ma è una promessa
non mantenuta: da guardare in un giro suo.
