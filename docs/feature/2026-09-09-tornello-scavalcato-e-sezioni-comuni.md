# Il tornello scavalcato, e le sezioni in comune che non erano comuni (9 settembre 2026, sera)

> Due difetti segnalati insieme, unendo un **terzo** documento (`LIBV_APP`) alla vIPI e al vSOP di Gioia
> del Colle: la scheda «sezioni in comune» che si riapre e propone cose sbagliate, e — subito dopo — **la
> pagina che si blocca del tutto**: né «sciogli l'unione», né «togli un documento».

## §1 — Il blocco: non è dell'unione, è il **tornello scavalcato**

### Che cosa dice la diagnostica

⚠️ **Prima di tutto, l'era.** Il file `errori-richieste.txt` finisce alle **14:11:39** e l'avvio di 1.18.0 è
alle **14:13:21**: tutto quel che c'è dentro è dell'era **precedente**. Non è una regressione di 1.18.0, e il
codice qui sotto quel pacchetto non l'ha toccato.

Il gruppo delle 14:05–14:11 racconta sempre la stessa scena:

| ora | che cosa |
|---|---|
| 14:11:26 | `InvalidOperationException: A second operation was started on this context instance` — `AppSectionsEditor.ParametriAsync` (riga 420) mentre il `DbContext` ha aperta la query di `GetVfrAsync`, cioè di `LoadAsyncCore` |
| 14:11:26 | `NotImplementedException: Encountered unsupported frame type during diffing: None` |
| 14:11:26 | `ObjectDisposedException: 'VipiDbContext'` sulla stessa catena |

🔴 **Il blocco è la seconda riga.** Quando un render muore a metà, il diff di Blazor resta **corrotto**: da
lì in poi la pagina non risponde più a nessun comando. Non è che «sciogli l'unione» sia rotto — è che non
c'è più nessuno che ascolti il clic.

### Il meccanismo

`DocumentEditorShell.InFilaAsync` ricorda di essere «già dentro» con un **`AsyncLocal<bool>`**. Un
`AsyncLocal` **si eredita**: ogni flusso nato dentro un'operazione in fila lo trova acceso. Un render
provocato da lì dentro chiama `OnParametersSetAsync` del componente, che legge «sono già dentro» e
**salta la coda** — partendo **accanto** alla prima invece che dopo. Due catene sullo stesso `DbContext`.

⚠️ **La differenza fra il rientro giusto e questo è CHI ASPETTA**, e non si vede dal codice:

- `StartEditingAsync` è un gesto in fila che chiama il ricarico e **lo aspetta**: sono la stessa catena, e
  scavalcare è giusto — senza, si aspetterebbe se stessi e l'editor si pianterebbe.
- Il `Task` di `OnParametersSetAsync` **non lo aspetta chi lo ha provocato**: lo tiene il renderer. Sono
  **due** catene, e la seconda deve mettersi in coda.

### Perché non si vede in locale, e perché con tre membri

È una **corsa**. Con un editor solo la finestra è stretta; con tre membri l'ospite ridisegna a ogni
caricamento di membro, e su **MariaDB** le query durano abbastanza da farle sovrapporre. Su **SQLite** in
locale finiscono prima che la seconda parta. Un difetto che il banco non riproduce non è un difetto che non
c'è: è un difetto che il banco è troppo veloce per vedere.

### 🔴 La prova prima della cura

`Un_caricamento_provocato_DA_DENTRO_si_mette_in_coda` riproduce lo scavalcamento **in modo deterministico**:
un'operazione che, dal proprio flusso, ne fa partire una seconda **senza attenderla** — l'unico dettaglio
che conta. Sul codice di **prima**: `Expected: 1, Actual: 2`. Due operazioni insieme dove ne deve stare una.

⚠️ È la prova che **distingue**: se fosse stata verde sul codice di prima, la diagnosi era sbagliata e si
ricominciava. Vedi la regola in `docs/…/prova-che-non-distingue`.

### La cura

Il tornello si divide in **due porte**, e la differenza è dichiarata:

| porta | chi la usa | rientra? |
|---|---|---|
| `InFilaAsync` | i gesti e le catene annidate che si **aspettano** | sì |
| `CaricaInFilaAsync` | `OnParametersSetAsync`, in tutti e cinque gli editor | **no, si mette sempre in coda** |

Nessuno stallo, e per un motivo preciso: chi provoca quel `Task` non lo attende.

## §2 — Le sezioni in comune: la stessa chiave non è lo stesso dato

### Il fatto, misurato

Chiavi di catalogo condivise fra un **APP** e la **vIPI d'aeroporto**: `frequencies`,
`operationaltechnique`, `validity`. `validity` nasce già non spuntata (dice il ciclo di *quel* documento).
Le altre due nascevano **spuntate**.

🔴 **E non sono ripetizioni.** Le «Frequenze» di un APP sono quelle dell'**avvicinamento**; quelle
dell'aeroporto sono del **campo**. Nasconderne una non toglie un doppione: **perde contenuto vero**. Chi
premeva «applica» senza guardare lo perdeva.

Vale anche fra due APP dello stesso campo — e LIBV ne ha due: settori diversi, frequenze diverse. L'unico
asse su cui una chiave uguale significa davvero la stessa cosa è il **luogo**.

### La cura

`SezioniComuni.Confrontabili(membri con famiglia)`: partecipano al confronto **solo** i documenti che
descrivono lo stesso luogo — `Airport` e `AirportMil`. Meno di due che si confrontano = nessuna sezione in
comune.

⚠️ **La regola sta nella porta che c'è già.** `SezioniComuniAsync` e `ApplicaSezioniComuniAsync` non
prendono più degli id nudi ma i **membri con la loro famiglia**, e applicano la regola dentro: nessun
chiamante la può saltare. Scriverla nel pannello — l'unico chiamante di oggi — avrebbe fatto una regola che
il secondo chiamante non trova.

Conseguenze in pagina:

- fra le caselle «nascondi in» compaiono **solo** i documenti che si sono confrontati: per l'APP non c'è
  casella, perché non c'è niente da nascondere;
- unendo vIPI + vSOP la scheda funziona **esattamente come prima**;
- **la scheda non si apre più da sola quando non ha niente da chiedere.** Fino a oggi si apriva sempre —
  «niente da scegliere è una risposta, il silenzio è un dubbio» — e andava bene finché l'unione era
  vIPI + vSOP. Con un APP in mezzo comparirebbe a ogni unione per non proporre nulla, e una scheda che si
  apre per dire «niente» la seconda volta è rumore. Il suo tasto resta, e chi la apre a mano la trova.

## Che cosa NON è stato fatto, e si sa

- 🔴 **La cura del §1 è provata al livello del guscio, non in produzione.** Riprodurre la corsa vera vuole
  tre membri **e** un database lento: il banco locale non la fa vedere né prima né dopo. La prova che
  distingue esiste ed è quella sul tornello; la conferma sul campo sarà il prossimo
  `errori-richieste.txt` — devono sparire le `A second operation` da `AppSectionsEditor`.
- ⚠️ **Il §2 non è stato guidato a schermo**: serve un'unione a tre membri, che in locale non c'è
  (i tre documenti di LIBV nel `vipi.db` di sviluppo non hanno versione di lavoro).
- ✅ **Il pacchetto c'è**: **1.18.1** (`ba16e1c`, 6 file, zip `ed78a32d…`), provata sul pacchetto
  pubblicato — dieci controlli di smoke e i sedici dei gesti dell'editor.
