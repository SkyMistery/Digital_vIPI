# Audit — densità e leggibilità della pagina (22 agosto 2026)

> Parte B del decimo giro del ramo `ui-trasferimenti-densita`. La parte A
> ([`2026-08-22-audit-cosa-registra.md`](2026-08-22-audit-cosa-registra.md)) ha messo nel registro gli atti
> che mancavano; questa rende la pagina capace di reggerli.

## La misura, rifatta con un registro vero

La ricognizione diceva **1 166px**, poi **1 556** tre giorni dopo. Sono le misure di una tabella con 20 e 28
righe in un DB di sviluppo che non ha quasi audit. Riempita la copia con **248 righe** — la stessa scala di
un semestre di lavoro vero — la pagina è:

| | 1600 | 1440 | 1280 | 1024 |
|---|---:|---:|---:|---:|
| IT | **13 293** | 13 269 | 13 248 | 13 380 |
| EN | 13 273 | 13 248 | 13 236 | 13 360 |

⚠️ **E 13 293 è il numero col tetto**: la pagina si ferma lì solo perché `ListRecentAsync` taglia a **200
righe**, in silenzio. Senza il taglio crescerebbe per sempre. È la seconda pagina più alta del giro dopo
l'editor aeroporto, e la ricognizione la dava per ultima.

**Una misura è una fotografia.** Su una pagina che accumula va rifatta, non citata — e va rifatta **con i
dati**, come su Permessi (regola 117).

## Cosa non va, guardando la pagina

| | Difetto | Costo |
|---|---|---|
| 1 | `thead` che scorre via: alla riga 150 non si sa più quale colonna è quale | — |
| 2 | **JSON grezzo in una colonna**: `{"Id":12,"VersionNumber":18,"Reason":"publish-now-release"}` | è la colonna più larga della tabella |
| 3 | **VID crudi**: `555002` invece del nome, che il roster conosce già (come in Versioni e Permessi) | — |
| 4 | **Vocabolario da macchina**: `Update` + `Document`, `Archive` + `EditGrant`. Chi legge deve tradurre | — |
| 5 | **`EntityId` è un numero interno**: «DocumentVersion 49» non dice di quale documento | il documento è nel JSON accanto, non nella colonna |
| 6 | **Data su due righe** (`09 giu 2026` / `12:55:00`) → riga da **64px** | 64 × 200 = 12 800px |
| 7 | **Nessun filtro**: né per persona, né per tipo, né per periodo, su un registro | — |
| 8 | **Il taglio a 200 è muto**: nessuno sa che sotto c'è altro | — |
| 9 | Nessun «?», sottotitolo in fascia, nessuna sezione di Guida | ~40px + la prosa |
| 10 | `.wrap` a 1 100px → la barra admin va **a capo** (87px invece di 55) | 32px |

## Le decisioni

- **Un pannello solo, a larghezza piena, misurato.** Niente elenco+dettaglio come su Confinanti, Versioni e
  Permessi: là il dettaglio è un oggetto su cui si **agisce**, qui la riga è già tutto il fatto e un pannello
  a destra sarebbe una colonna sprecata per rileggere quello che la riga dice. Il registro non si modifica.
- **La riga si legge in italiano.** Ogni evento diventa una frase: «Pubblicata la versione 18 di *vIPI Roma
  ACC*», «Revocato il permesso su LIRR a Marco De Angelis», «Tolto il lock a Luca Rossi». Il JSON resta —
  è la verità grezza e nessuno la butta — ma nel **`title`** della cella, non nella colonna.
- **Le parole vecchie e quelle nuove dicono la stessa frase.** `Archive`+`EditGrant` (righe fino al 22
  agosto) e `Delete`+`EditGrant` (dopo) sono lo stesso atto: la pagina li rende identici. La storia non si
  riscrive, si legge.
- ⚠️ **Il formattatore è uno solo, condiviso con Versioni.** Il pannello di Versioni aveva un parser suo
  (`HistoryDetail`) che leggeva `{"Areas":…,"Saves":…}` — chiavi che **nessuno scrive** — e restituiva sempre
  stringa vuota. Non si cancella: si **sostituisce** con lo stesso formattatore, così due pagine che mostrano
  lo stesso evento non possono più divergere.
- **Il periodo al posto del tetto.** `ultimi 7 / 30 / 90 giorni / tutto`, con il conteggio di quante righe ci
  sono davvero nel periodo e quante se ne mostrano. Un tetto muto su un registro è la cosa peggiore: fa
  credere completo un elenco che non lo è.
- **I chip contano** (regola 107) e sono per **tipo di atto**, non per valore d'enum: Pubblicazioni, Bozze
  scartate, Documenti, Permessi, Gerarchia, Lock. ⚠️ Zero qui è **neutro**, non verde (regola 108): «0
  eliminazioni» non è una coda vuota da festeggiare, è un fatto.
- **La data su una riga** (`09 giu 26 · 12:55`), i secondi nel `title`: su un registro il secondo esatto
  serve una volta l'anno, la riga alta 64px si paga 200 volte.
- **Chi** è il nome dal roster col VID accanto, come in Versioni e Permessi (`Author(vid)`).
- **Larghezza piena** e la barra admin torna in una riga.
- **Nessuna azione sulla pagina**: un registro non si modifica, non si cancella, non si nasconde. L'unico
  comando è «Aggiorna» — perché il registro cresce mentre lo si guarda.

## Rete

- Test UI (bUnit) sulla resa: una riga per ogni tipo di evento dice la frase attesa, e la riga `Archive`
  vecchia dice la **stessa** frase della nuova `Delete`.
- Test sul filtro periodo (il conteggio corrisponde alla condizione, chip per chip — regola 107).
- Verifica live sulla copia riempita: 1600/1440/1280/1024, IT **ed** EN, zoom 0.8→1.5, screenshot **guardati**.
