# Feature — Editor trasferimenti: l'editing esce dalla riga ed entra nel pannello

Data: 2026-08-12 · Stato: **CHIUSO — suite 2197 verde, Release 0 warning su entrambi i TFM, ✅ verifica live** ·
Gate: [FEATURE-PROCESS](../FEATURE-PROCESS.md) ·
Segue [trasferimenti ACC↔APP](2026-08-11-trasferimenti-acc-app.md) e
[varianti a livelli](2026-08-12-varianti-a-livelli.md), stesso branch.

> ⚠️ **Il layout descritto qui è stato superato lo stesso giorno.** Le due colonne (`.xfe-layout`: lista a
> sinistra, pannello sticky a destra) sono diventate **tre** (`.xfe-layout3`: navigatore · riquadro di lavoro ·
> pannello, ognuna col proprio scorrimento) in
> [editor trasferimenti a tre colonne](2026-08-12-editor-trasferimenti-tre-colonne.md), che spiega perché.
> Tutto il resto di questa scheda — l'editing che esce dalla riga, le tre azioni per riga, gli stili inline
> azzerati — resta vero.

## Obiettivo

Due giri di lavoro hanno aggiunto alla riga di trasferimento la faccetta, la velocità e l'outline delle
varianti. La pagina che li scrive non era stata rifatta insieme, e guardandola a schermo si vedeva.

**Sette cose misurate, non supposte** (ricognizione con Edge sulla pagina reale):

1. La riga in modifica era **una fila di sei controlli senza etichette** — `BIRSU | ≤(-) | 150 | FL | Even |
   Level` — sotto un'intestazione «LEVEL» che ne copriva quattro.
2. Il **multi-select delle piste sfondava** sopra la riga sotto: un `<select multiple size=3>` dentro una cella.
3. **Nove bottoni-icona per riga**, 28-35px, senza etichetta: `⑂ ↳ ⤒ ▲ ▼ ⤓ ⧉ ✎ ✕`.
4. **Tre bottoni dicevano la stessa parola**: «⧉ Gruppo», «✎ Gruppo», «✕ Gruppo».
5. **Tre colonne di condizione** quasi sempre vuote (su 76 righe: pista 4 · area 2 · personalizzata 0), mentre
   la vista pubblica le unisce già in una.
6. **51 stili inline** più un `<style>` dentro il markup: l'audit dell'11 agosto ha lasciato la CSP in sola
   segnalazione proprio per questi.
7. **Il pattern del progetto non era questo.** `.xfe-layout` (lista + pannello 380px) è usato da Permessi e
   Incarichi, `.gerarchia-2col` + `.detail-sticky` da Struttura. Trasferimenti era l'unica pagina di editing
   admin che trasformava la riga in un form.

## Pre-flight — 4 domande

**1. Modello.** Nessuna entità nuova. Al contrario: i due blocchi di campi paralleli del form (`_p*` per la
riga nuova, `_ep*` per la modifica) diventano **un tipo, `PointForm`, in due istanze**. Dodici coppie da tenere
allineate a mano erano dodici occasioni di aggiornarne una sola — ed è la stessa ragione per cui la faccetta
era già diventata un oggetto.

**2. Dispatch.** Nessuno `switch` nuovo. Il pannello ha un solo markup, parametrizzato sull'istanza attiva:
prima lo stesso form era scritto due volte, in due posti che potevano divergere.

**3. Ingressi + verifica.** Nessun ingresso nuovo: la pagina è quella. Verifica: ricognizione a schermo prima,
e la stessa a valle per confrontare i numeri.

**4. Propagazione.** `EditLockBar` guadagna un callback **additivo** (`ExpiresChanged`): chi non lo aggancia
non cambia comportamento, e le altre tre pagine che montano la barra restano com'erano.

## Cosa cambia

### Struttura (A)

`xfe-layout`: tabella di sola lettura a sinistra, **pannello sticky a destra**. Il pannello ha i campi con
etichetta, raggruppati — *Ingresso e ricevente · Livello autorizzato · Trasferimento · Condizione* — l'anteprima
della frase in fondo, e sotto le azioni sulla riga. Vuoto, dice cosa fare, come il pannello di Struttura: una
colonna che non dice niente sembra rotta.

### Azioni (B) e colonne (C)

In tabella restano **tre** azioni — aggiungi alternativa, aggiungi eccezione, apri — più la maniglia di
trascinamento e la casella di selezione. Spostare, duplicare, sfilare ed eliminare stanno nel pannello, dove
c'è posto per le parole: «▲ Sposta su», non «▲».

Una colonna **Condizione** sola, riusando `ConditionDisplay` come la vista pubblica.

### Etichette (D) e stili (E)

I tre «Gruppo» diventano **Duplica · Modifica · Elimina**. Gli stili inline passano a classi `xt-*` nel tema:
da **51 a 0**, `<style>` incluso.

## QoL — tutti e dieci

| | Cosa | Nota |
|---|---|---|
| 1 | **Trascinamento** per riordinare | riusa `.app-drag`, il gesto già in Frequenze/Regole pista/Struttura. Sposta il **sottoalbero**, e vale solo con l'ordinamento manuale |
| 2 | **Duplica gruppo di varianti** | `DuplicateVariantGroupAsync`: copia la struttura (profondità e righe trasversali), non solo le righe |
| 3 | **Modifica in blocco** del ricevente | selezione a caselle + `SetReceiverAsync`; propaga al gruppo, come fa il salvataggio singolo |
| 4 | Filtro **«senza ricevente»** | il badge UNICOM c'era, il modo di elencarle no |
| 5 | Avviso **modifiche non salvate** | acceso da una modifica **vera** (firma del form), non dal pannello aperto |
| 6 | Avviso **lock in scadenza** | soglia 60s, non 5 minuti — vedi sotto |
| 7 | **Anteprima sempre accesa** | era dietro una casella, spenta di default |
| 8 | **Copia righe da un altro gruppo** | gli accordi di aeroporti vicini si somigliano |
| 9 | **Ordinamento** per CoP o livello | cambia la vista, **mai** l'ordine salvato: nell'outline è la struttura, e per questo l'ordinamento non-manuale spegne il trascinamento |
| 10 | **Esc** chiude, **Invio** salva, **Ctrl+Invio** salva e riapre | |

## ✅ Verifica live — eseguita il 12 agosto 2026

Ricognizione a schermo prima e dopo, sulla pagina reale con i dati veri.

| Misura | Prima | Dopo |
|---|---|---|
| Stili inline nella pagina | **51** (+ un `<style>`) | **0** |
| Bottoni-icona per riga | **9** | **3** |
| Controlli senza etichetta nella riga | **6** | 0 (sono nel pannello, etichettati) |
| Colonne di condizione | 3 | 1 |

**Due difetti trovati guardando, che nessun test avrebbe visto:**

- **L'avviso di scadenza del lock era sempre acceso.** L'avevo tarato a cinque minuti, ma il TTL del lock è di
  **tre** (`ResourceLockService.LockTtlMinutes`): la condizione era vera a ogni caricamento. Portato a 60
  secondi, che con l'heartbeat vivo non scatta mai — e scatta solo quando l'heartbeat si è fermato, cioè
  l'unico momento in cui l'avviso serve.
- **L'avviso «modifiche non salvate» si accendeva aprendo una riga**, prima di toccare qualsiasi cosa. Ora
  confronta una firma del form con quella di quando si è aperto. Provato: silenzio col lock fresco, silenzio a
  riga aperta e intatta, avviso al primo carattere digitato.

**Una regressione presa al volo mentre la scrivevo**: estraendo `CopyOf` per condividere la copia di riga fra
duplicazione di gruppo e creazione di variante, la variante ha cominciato a copiare **anche la condizione** —
che è esattamente ciò che deve dire di diverso. Le due operazioni condividono venti campi e ne vogliono
diciannove: l'azzeramento è esplicito e commentato, perché la prossima persona rifarà la stessa domanda.

## Test — sei, sulle tre operazioni nuove

In `tests/Vipi.Infrastructure.Tests/TransferRepositoryTests.cs`, accanto a quelli delle varianti:

| Test | Cosa tiene fermo |
|---|---|
| `Dragging_Lands_After_The_Target_Going_Down_And_Before_It_Going_Up` | il verso: scendendo dopo, salendo prima — e su sé stessa non riscrive niente |
| `Dragging_Carries_The_Subtree_And_Refuses_To_Enter_Itself` | la capofila porta le eccezioni; il bersaglio dentro il blocco è un no-op |
| `Dragging_Between_Flows_Is_A_Noop` | un accordo non cambia gruppo di traffico per un riordino |
| `Duplicating_A_Group_Copies_The_Outline_Next_To_It` | profondità, riga trasversale, gruppo nuovo in coda — **e la condizione** |
| `Duplicating_Outside_A_Group_Does_Nothing` | fuori da un gruppo non c'è un outline da copiare |
| `Bulk_Receiver_Reaches_The_Whole_Group_Of_A_Selected_Row` | la propagazione alle sorelle non selezionate; il conteggio resta quello scelto |

Il quarto è scritto **contro** il primo dei test delle varianti
(`Alternative_Is_Peer_And_Copies_Everything_But_The_Condition`): stesso `CopyOf`, esito opposto sulla condizione.
È la regressione di questo giro messa in guardia, non una verifica di comodo.

## Fuori scopo

- **Trascinare fra flussi diversi**: un accordo appartiene al suo gruppo di traffico, e spostarlo altrove è
  un'altra operazione, non un riordino.
- **Modifica in blocco di livello e faccetta**: il ricevente è il caso che nasce davvero (un settore che cambia
  nome). Gli altri campi si scrivono uno per uno perché uno per uno vogliono essere pensati.
