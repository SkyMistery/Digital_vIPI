# §A39 — «Il documento è saturo»: due guasti dietro un sintomo solo

> 16 settembre 2026. In `main`, **non** ancora in pacchetto. Commit `0635fdd3`, CI verde.
> Nasce da una segnalazione di un administrator che stava scrivendo il **SOD di Decimomannu (LIED)**:
>
> > *«ma ogni SOP ha qualche vincolo di grandezza nel DB? sto facendo Decimo che è un bel mattone, sono
> > arrivato ad un punto che se creo una sottosezione, non appare nemmeno. sembra "saturo". è davvero un
> > problema quando i documenti si caricano abbastanza? Oppure possiamo fare qualcosa? o è un limite dei
> > server dove è ospitato il sito?»*
>
> **Nessuna delle tre ipotesi era giusta.** Non è la dimensione, non è il database, non è l'host. Sotto quel
> sintomo c'erano **due guasti distinti**, che producono lo stesso identico comportamento: il gesto non fa
> niente, e nessuno dice perché.

---

## 0. Il metodo — tre domande invece di una prova

La diagnosi è arrivata da **tre domande al segnalante**, non da una riproduzione a mano. Sono scritte qui
perché la forma è riutilizzabile: ognuna separa **una** cosa (vedi la regola «una domanda cambia una cosa
sola»), e le tre risposte insieme dividevano i due guasti.

| Domanda | Risposta | Che cosa ha deciso |
|---|---|---|
| Su quale sezione premi «+ Sottosezione»? È già una sotto-sotto-sezione? | **sì** | il ramo è quello profondo del catalogo militare |
| Dopo il click, **in cima alla pagina**, c'è un riquadro rosso? | **no** | il rifiuto c'è ma è fuori campo — non che non ci sia |
| Compare mai «connessione persa»? E ricaricando, la sottosezione poi c'è? | **sì** | c'è **anche** un secondo guasto, indipendente dal primo |

🔴 **Senza la terza domanda ci si fermava al primo guasto**, che è il meno grave dei due.

⚠️ **La copia locale di `diagnostica/` era vecchia di tre giorni** (`errori-richieste.txt` fermo al 13-set)
mentre `avvio-diagnostica.txt` era di quella mattina. Un caso di oggi non si cerca in una cartella scaricata
ieri: si **riscarica**. È la stessa trappola di §CN, con un'altra faccia — lì era il fuso, qui l'età.

---

## 1. La profondità — il tasto era acceso e il rifiuto invisibile

`DocumentSection.MaxDepth` si conta **da zero** ed era **3**: quattro livelli. Il catalogo **militare** ci
stava appoggiato, e lo sapevamo — sta scritto nella carta del 6 settembre e in un test apposta:

```
regulated              «Aree di lavoro»            Depth 0
└ operationaltechnique «Procedure generali»        Depth 1
  └ departureprocedures «Procedure di partenza»    Depth 2
    └ :vfr / :ifr      «VFR» / «IFR»               Depth 3   ← il bordo
```

È esattamente il ramo in cui stava lavorando. Il motore rifiutava, **e rifiutava bene**
(`EfEditingRepository.AddSectionAsync`). Quel che non andava erano le altre due metà:

- 🔴 **Il tasto «+ Sottosezione» restava acceso.** `DocumentSectionsEditor` non guardava `s.Depth`: offriva
  un gesto che il motore avrebbe rifiutato. È una promessa non mantenuta, e sul fondo dell'albero è l'unica
  promessa che conta.
- 🔴 **Il rifiuto si disegna in cima alla pagina.** `_shell.Error` sta nel callout in testa
  (`AirportSectionsEditor.razor`). Su un documento corto lo vedi; su un SOD di Decimomannu premi un tasto a
  **tre schermate** dal fondo e il messaggio compare dove non stai guardando.

**Le due cose insieme fanno una diagnosi sbagliata.** Non «non si può annidare più giù», ma «il documento è
rotto», e poi — cercando una spiegazione a un programma che tace — «il documento è **saturo**».

### Che cosa si è fatto

- **`MaxDepth` 3 → 5.** Costa una costante: nessuna ricorsione — viewer (`SectionNode`), editor
  (`DocumentSectionsEditor`), sommario (`DocumentToc`), stampa — ha mai avuto un fondo scritto a mano.
- **Il tasto si spegne sul fondo, col MOTIVO** nel `title` (`Dse_AddSubsectionMaxDepth`). Non muto: in
  un'interfaccia dove i permessi contano, un `disabled` senza spiegazione si legge «non ti è permesso» — e
  quello si rimedia chiedendo a qualcuno, mentre questo si rimedia mettendo un **blocco** invece di una
  sotto-sezione. Presidiato da `SottosezioneAlFondoTests`, provato **anche al contrario** (con la guardia
  finta a `true` i due casi cadono).

### ⚠️ Quel che NON è cresciuto: la resa

Dal livello 2 in giù viewer, editor e sommario usano **gli stessi stili**: `coord-sub2` per il corpo,
`lvl4` per il rientro del sommario, `Level=2` per la card dell'editor. Sei livelli si possono **annidare**,
non si possono **distinguere** a schermo. Era già vero a 3 e nessuno se n'era accorto, perché il quarto
livello non lo usava nessuno; a 5 diventa una scelta editoriale da fare con gli occhi. **Non è un invito**,
e se un domani serve è un lavoro di CSS a parte.

### 🔴 Quattro test pinnavano il NUMERO e non la regola

Alzando la costante sono diventati rossi **con le guardie intatte**: misuravano `3`, non «il fondo».

| Test | Come stava | Come sta |
|---|---|---|
| `EditingRepositoryTests.AddSection_Respects_MaxDepth` | srotolato a mano L0…L3 | ciclo fino a `MaxDepth`, poi il gradino dopo |
| `SezioniRiparentateTests.Rifiuta_se_il_sottoalbero_sfora_la_profondita` | «B1, profondità 1» era il bordo | scala di bersagli costruita da `MaxDepth` |
| `TocDropRulesTests.Non_si_lascia_dove_il_sottoalbero_non_ci_sta` | «A1a, profondità 2» era il bordo | bersagli sul fondo e un gradino sopra |
| `ProfiloMilitareTests.Il_profilo_TOCCA_il_limite` | `Assert.Equal(MaxDepth, …)` | `Assert.Equal(3, …)` — il fatto è il **ramo**, non il tetto |

L'ultimo merita la nota: legare i due numeri **sembrava** economia, ed erano due fatti diversi scritti con la
stessa cifra. Alzando il tetto quel test sarebbe tornato verde **per il motivo sbagliato** — il ramo non è
cresciuto, il tetto sì.

### ▶ Resta aperto

L'errore dell'editor continua a nascere **lontano da dove si preme**, in tutti e quattro gli editor. Qui si è
tolta la causa più frequente, non il difetto: qualunque altro rifiuto del motore ha ancora lo stesso
problema di visibilità.

---

## 2. Il circuito che muore — il guasto più grave, e non è nostro

`diagnostica/avvii.txt`, mattina del 16 settembre:

```
07:04:30 AVVIO 1.27.0   07:07:08 ARRESTO  acceso 00:02:38  richieste 33, ultima  7s fa · SIGTERM
07:07:16 AVVIO          07:08:12 ARRESTO  acceso 00:00:56  richieste  5, ultima  9s fa · SIGTERM
07:08:14 AVVIO          07:09:09 ARRESTO  acceso 00:00:55  richieste 15, ultima  0s fa · SIGTERM
07:09:19 AVVIO          07:15:08 ARRESTO  acceso 00:05:49  richieste 121, ultima 6s fa · SIGTERM
```

🔴 **Il numero che conta non è «acceso per», è «ultima richiesta N s fa»**: sta fra **0 e 9 secondi** su
*ogni* arresto della mattina. Passenger non spegne «dopo qualche minuto di inattività» — spegne entro
**~10 secondi** dall'ultima richiesta. E questo cambia tutto:

- **Il circuito Blazor muore di continuo.** Il gesto appena fatto non arriva mai al server: nessun errore,
  nessun badge, niente. Colpisce **tutti** gli editor e tutte le pagine interattive — il mattone lo rende
  solo più probabile, perché ci si sta dentro più a lungo.
- 🔴 **`COLPETTO_MS = 150000` in `vipi-riconnessione.js` è quindi inefficace**: il colpetto anti-spegnimento
  bussa ogni **2,5 minuti** su una finestra di **10 secondi**. Fu tarato su «Passenger spegne per inattività
  dopo qualche minuto», che è la premessa che questi dati smentiscono.
- `DisconnectedCircuitRetentionPeriod = 5 min` non aiuta e lo dice già il suo commento: vale per i buchi di
  **rete**, non per un **processo** ucciso.

### Che cosa si è fatto (e che cosa no)

Fatto, lato app: dopo una ricarica causata da un circuito morto compare la striscia **«L'ultimo comando
potrebbe non essere arrivato»** — `#vipi-gesto-perso` in `App.razor`, accesa da `vipi-riconnessione.js` con
una bandierina in `sessionStorage` piantata **prima** di `location.reload()`.

- Markup in `App.razor` e non in un componente: si accende **prima** che Blazor parta — è il caso in cui un
  circuito, per definizione, non c'è. Per la stessa ragione i testi stanno nei resx: tradotti dal server.
- Ci passa **anche** il tasto «Ricarica la pagina», ma **senza** il conteggio anti-ciclo: quel conteggio
  esiste per non ricaricare all'infinito da soli, e applicato a un tasto lo spegnerebbe proprio quando è
  l'unica cosa rimasta da premere. Un tasto che non fa niente è il difetto da cui è nato quel file.
- ⚠️ **`.vipi-perso[hidden]{display:none}` serve.** Un `display:flex` scritto da noi **vince** su `[hidden]`
  della UA stylesheet, e senza quella riga l'avviso sarebbe comparso su **ogni pagina del sito**. È la stessa
  trappola già pagata in `vipi-awos.css`, dove sta scritta a chiare lettere — e ci si è cascati lo stesso.

**Non** fatto, e deliberatamente: il colpetto non è stato abbassato. Il numero giusto dipende da come è
configurato l'host, e sceglierlo senza quella informazione è indovinare.

### ▶ Al committente

`passenger_min_instances ≥ 1` e l'**idle timeout** in Plesk. È la stessa voce che ricorre da agosto (§O3, e
il Q5 di §CZ con il «processo fermato ogni ~46 s»): finora il costo sembrava una pagina lenta ogni tanto,
adesso si sa che è anche **il lavoro che sparisce sotto le mani a chi scrive**.

---

## 3. Le immagini — non c'entravano, ma un tetto per documento esiste

La domanda era esplicita («il documento contiene molte immagini»), e la risposta è **no**, con un però che
vale la pena sapere.

- 🔴 **`MediaOptions.MaxBytesPerDocument = 25 MB` è l'unico tetto per-documento di tutto il sistema**, ed è
  per le immagini. Si cambia senza toccare il codice: `Media__MaxBytesPerDocument`. Accanto: upload singolo
  **3 MB**, lato lungo ridotto a **2000 px** dal browser prima di partire, `MaxImagePixels` 12000.
- Quando scatta **lo dice** («occupano già X sui 25 MB: rimuovi un'immagine…») e blocca **solo** il
  caricamento di un'immagine. Non tocca le sezioni.
- ⚠️ **I byte non pesano sull'editor**: tabella `MediaAssets` a parte, il blocco porta solo lo **sha**,
  servite da `/vsop/media/{sha}` come `immutable`. Ricaricare il documento dopo ogni gesto non rilegge un
  byte di immagine.
- Tutto il resto del testo è **`longtext`** su MariaDB (`MySqlStringLengths` dimensiona solo le colonne
  indicizzate e gli enum): nessun limite di sezioni, di blocchi o di caratteri. E `max_allowed_packet` è già
  sorvegliato dalla Diagnostica (`MySqlServerSettingsProbe`, minimo 4 MiB per le immagini).

---

## Che cosa resta aperto

1. ▶ **L'errore dell'editor nasce lontano dal gesto** (tutti e quattro gli editor). Qui si è tolta la causa
   più frequente, non il difetto.
2. ▶ **`passenger_min_instances` e idle timeout** — al committente. Finché non sono a posto, i gesti persi
   continuano: la striscia li **dichiara**, non li salva.
3. ▶ **`COLPETTO_MS`** va rideciso **insieme** a quella configurazione, non prima.
4. ℹ️ Se un domani servono davvero sei livelli leggibili, serve una **resa** per i livelli oltre il secondo:
   oggi `coord-sub2` e `lvl4` li appiattiscono tutti.
