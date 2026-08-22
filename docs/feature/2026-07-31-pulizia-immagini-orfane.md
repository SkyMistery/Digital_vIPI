# Feature — Pulizia delle immagini non più usate (spazio recuperabile)

Data: 2026-07-31 · Stato: **FATTO** (suite 814 verde, verificato live) — esteso in giornata con anteprima,
quota per documento e pulizia alla cancellazione · Gate: [FEATURE-PROCESS](../FEATURE-PROCESS.md) ·
Segue: [immagini nei blocchi](2026-07-31-immagini-nei-blocchi.md) §R2, dove la pulizia era un non-obiettivo.

## Obiettivo

Togliere un blocco immagine da un documento **non** libera lo spazio: i byte restano in `MediaAssets`, di proposito
(una release pubblicata cita lo sha, e la stessa immagine può essere usata altrove). Serve un modo **esplicito** per
recuperare lo spazio di ciò che davvero non serve più: un'azione admin che prima mostra cosa toglierebbe e quanto
si guadagna, e cancella solo su conferma.

## Pre-flight — 4 domande

**1. Modello.** Nessuna entità nuova, nessuna colonna nuova: si legge `MediaAssets` e si confrontano gli sha con
quelli citati altrove. Il conteggio non si materializza da nessuna parte — un contatore di riferimenti sulla riga
sarebbe un secondo modello della stessa verità, che va in deriva appena qualcosa scrive senza aggiornarlo.

**2. Dispatch.** Nessun `switch` per tipo. C'è un solo punto che sa *dove* possono comparire i riferimenti
(`EfMediaMaintenance`) e un solo punto che sa *come* riconoscerli in un testo (`MediaReferenceScanner`).

**3. Ingressi + verifica.** Ingresso: `/services/vsop/admin/diagnostics`, che è già la pagina delle azioni di manutenzione
ed è già riservata agli admin (`Authz.IsAdmin`). Verifica: test sui casi che contano (sotto) + verifica live
caricando un'immagine, cancellando il blocco, e controllando che compaia nell'elenco solo quando non la cita più
nessuno.

**4. Propagazione.** Nulla viene rimosso o rinominato: la feature è additiva. Da aggiornare comunque la carta
delle immagini (il non-obiettivo «GC» diventa fatto) e la memoria.

## Design

### 1. Che cosa vuol dire «non usata»

Un asset è orfano se il suo sha **non compare** in nessuno di questi quattro posti:

| Dove | Perché conta |
|---|---|
| `ContentBlock.BodyJson` di **tutte** le versioni | comprese le bozze non pubblicate: è la foto che qualcuno sta scrivendo adesso |
| `AirportExtraSection.Body` | le sezioni extra dell'aeroporto tengono i blocchi serializzati in un campo solo |
| `DocRelease.PayloadJson` | le **fotografie congelate** dei documenti: una vIPI dell'AIRAC scorso continua a citare quello sha |
| `SharedBlock.BodyJson` | contenuti condivisi: oggi nessuno li crea, ma il modello li prevede (`ContentBlock.SharedBlockId`) e portano `Format`+`BodyJson` come i blocchi normali — è il «quarto posto» del rischio, chiuso subito |

Non si guarda l'**audit log**: registra che cosa è successo, non che cosa si mostra. Se citasse uno sha
cancellato resterebbe una traccia storica con un riferimento morto, e nessun documento si rompe.

### 2. Come si riconosce un riferimento — `MediaReferenceScanner`

Funzione pura: dato un testo, restituisce tutti gli sha citati. Cerca **qualunque sequenza di 64 caratteri
esadecimali**, non solo `"mediaId":"…"`.

Sembra grossolano ed è deliberato: i due errori possibili non si equivalgono.
- Riconoscere di *più* del dovuto ⇒ un asset orfano resta lì: si spreca spazio, che è il problema che stiamo
  già tollerando.
- Riconoscere di *meno* ⇒ si cancella un'immagine ancora in uso: si rompe un documento pubblicato, e in silenzio.

Il pattern largo sopravvive anche a un formato futuro che citasse lo sha in un campo con un altro nome.

**Preso in fase di test, non dal vivo:** dentro `AirportExtraSection.Body` il JSON dell'immagine è una stringa
*annidata*, e `System.Text.Json` ne scrive le virgolette con una sequenza di escape che finisce per `22` — due cifre
esadecimali incollate allo sha da entrambe le parti. Cercarlo delimitato non lo trovava; cercarne 64 qualsiasi lo
leggeva spostato di due. Entrambi gli errori portavano allo stesso posto: la foto di una sezione extra dichiarata
orfana mentre era in uso. Gli escape si neutralizzano **prima** di cercare.

### 3. Porta e servizio — `IMediaMaintenance`

```csharp
Task<MediaUsageReport> AnalyzeAsync(CancellationToken ct);        // non tocca niente
Task<int> DeleteOrphansAsync(IReadOnlyList<string> sha, CancellationToken ct);
```

- `MediaUsageReport`: totale asset e byte, elenco degli orfani (sha, nome originale, byte, data, chi l'ha caricata),
  byte recuperabili.
- `DeleteOrphansAsync` **ricontrolla** l'orfanità di ogni sha al momento della cancellazione, non si fida
  dell'elenco che ha in mano: fra l'analisi e il clic possono passare minuti, e in mezzo qualcuno può aver
  pubblicato o incollato quell'immagine in una bozza.
- Impl `EfMediaMaintenance` in Infrastructure (è tutta lettura di tabelle).

### 4. UI — una card in `/services/vsop/admin/diagnostics`

Due tempi, mai un colpo solo:

```
Immagini dei documenti
28 immagini · 9,4 MB in tutto                              [ Analizza ]

→ dopo l'analisi:
12 immagini non citate da nessun documento né release · 4,1 MB recuperabili
  foto-torre.png     820 KB   12/06/2026   704798
  schema-hold.png    640 KB   03/07/2026   512233
  …
                                       [ Elimina definitivamente ] (conferma in linea)
```

Nessun elenco = nessun pulsante di cancellazione. La conferma usa `InlineConfirm`, come le altre azioni distruttive
del progetto.

### 5. Perché a mano e non al boot

Un lavoro automatico farebbe il danno **mentre nessuno guarda**: basta che un domani nasca un posto in più in
cui un'immagine può essere citata e ci si dimentichi di includerlo in §1 — è già successo con `SharedBlock`, trovato
solo rileggendo il modello a feature finita. Con il pulsante, prima si legge l'elenco.
Stesso motivo per cui il probe di drift dello schema **segnala** e non corregge (ADR-0007 §D1-bis).

## Passi

1. `MediaReferenceScanner` (puro) + test. *(cuore deterministico, test-first)*
2. `IMediaMaintenance` + `MediaUsageReport` (Application) e `EfMediaMaintenance` (Infrastructure) + registrazione DI.
3. Card in `DiagnosticaPage` + stringhe it/en.
4. Test EF sui casi che contano (§sotto) e verifica live.
5. Doc: questa carta a FATTO, la carta delle immagini (§R2 «non-obiettivo» → fatto), `rounds.md`, memoria.

## Casi che i test devono fissare

- immagine citata **solo da una release** già pubblicata → **non** è orfana;
- immagine citata solo da una **bozza** non pubblicata → **non** è orfana;
- immagine citata da una **sezione extra** d'aeroporto → **non** è orfana;
- immagine usata da **due** blocchi: resta finché non spariscono entrambi;
- immagine mai citata → orfana, e la cancellazione libera solo lei (le altre righe restano);
- immagine citata da un **blocco condiviso** (`SharedBlock`) → **non** è orfana;
- sha passato alla cancellazione ma nel frattempo tornato in uso → **non** viene cancellato;
- lo **snapshot vero** di una release porta lo sha in una forma che lo scanner sa leggere
  (`ReleaseRepositoryTests.Snapshot_Carries_The_Image_Sha...`): è l'anello fra pubblicazione e pulizia, e se
  si spezzasse la pulizia cancellerebbe le foto delle vIPI già pubblicate.

## Estensione: anteprima, quota, pulizia alla cancellazione (stesso giorno)

I tre non-obiettivi della prima stesura sono stati chiesti subito dopo e implementati.

### Anteprima nell'elenco
Ogni riga mostra la miniatura (`.img-thumb`, `object-fit: contain` per non ritagliare le verticali proprio dove si
riconoscono). È il motivo per cui l'elenco si guarda: davanti a un nome come «immagine1.png» nessuno sa se quella
foto serviva.

### Quota per documento
`Media:MaxBytesPerDocument` (25 MB, `0` = nessun limite). Controllata **prima** di salvare: accettare i byte per poi
scartarli lascerebbe nel deposito un asset che nessuno cita. Conta le **righe**, non i riferimenti — la stessa foto
usata in due blocchi occupa lo spazio una volta sola. Il documento lo passa l'editor ospite
(`Doc.DocumentId`, o `_docDbId` per l'aeroporto); dove non c'è un documento la quota non si applica.

Costo accettato: una foto **già presente** nel documento viene contata due volta dalla stima, quindi a ridosso del
tetto può esserci un rifiuto di troppo. Il prezzo di un rifiuto in più è minore di quello di un asset orfano.

### Pulizia alla cancellazione
Quando una foto perde il suo blocco, si guarda subito se la cita ancora qualcuno; se no, la riga sparisce. Coperti
**tutti e quattro** i modi in cui questo può succedere:

| Percorso | Come si riconosce che cosa liberare |
|---|---|
| `DeleteBlockAsync` | lo sha del blocco, letto **prima** di cancellarlo |
| `DeleteSectionAsync` | gli sha di tutto il sottoalbero (sotto-sezioni comprese) |
| `SaveExtraSectionsAsync` (aeroporto) | lì si toglie **non rispedendo**: si confronta il prima col dopo |
| `PruneArchivedVersionsAsync` (retention) | gli sha delle versioni potate, raccolti e valutati **alla fine** — una foto può essere citata da due delle versioni in potatura |

**Non decide nulla da sé**: ripassa da `DeleteOrphansAsync`, lo stesso controllo su tutte e quattro le sorgenti che
governa la pulizia manuale. Quindi una foto ancora citata da un altro blocco, da un'altra versione o da una
**release pubblicata** resta dov'è — e i test lo fissano caso per caso.

La pulizia manuale resta, e serve: le foto rimaste indietro da prima, quelle liberate dalla retention, e quelle
caricate senza mai salvare il riferimento (succede negli extra d'aeroporto, che sono in memoria fino al salvataggio).

### Verifica live dell'estensione

- **quota**: seconda foto rifiutata con «Le immagini di questo documento occupano già 658.2 KB sui 683.6 KB
  disponibili…» — i due numeri vengono dall'opzione, non da un letterale;
- **cancellazione**: tolto il blocco con la foto, il deposito passa da «2 immagini, 658,2 KB» a «1 immagine,
  175,1 KB» senza toccare la pulizia manuale;
- **anteprima**: caricata una foto negli extra senza salvare, la diagnostica la elenca con la miniatura
  effettivamente scaricata (`naturalWidth` 1200 × 800, non un'icona rotta).

Un falso allarme lungo la strada, annotato perché può ripetersi: al primo giro la cancellazione sembrava non
liberare nulla. Era il driver, che cancellava il blocco immagine **vuoto** lasciato dal caricamento rifiutato,
invece di quello con la foto.

## Non-obiettivi (rimasti)

Galleria per riusare un'immagine già caricata; crop/rotazione; immagini dentro le celle di tabella;
cancellazione automatica **a tempo** (un lavoro periodico che gira mentre nessuno guarda).
