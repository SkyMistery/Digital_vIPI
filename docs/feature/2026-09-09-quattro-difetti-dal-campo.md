# §CN — Quattro difetti dal campo, e uno solo era quello segnalato

> 9 settembre 2026, sera tardi. Consegnati in **1.18.2**.
> Nasce da due segnalazioni del committente sull'editor unito di **Gioia del Colle (LIBV)**:
>
> 1. *«la ✕ per eliminare uno dei documenti dall'unione la clicco e non mi apre nemmeno la conferma»*;
> 2. *«dopo che sposto un documento verso l'alto o il basso nell'unione vengo rimandato direttamente alla
>    home senza che clicchi nulla»*.
>
> Sotto quei due sintomi c'erano **quattro** difetti distinti. Nessuno dei due sintomi aveva la causa che
> sembrava avere.

---

## 0. Il metodo, prima dei difetti — tre errori di lettura pagati per arrivarci

Vale la pena scriverli perché sono tutti e tre ripetibili.

### 🔴 L'ora prima del contenuto, e il FUSO è parte della prova

La cartella `diagnostica/` scaricata riportava il timbro `1.18.0` e `avvii.txt` finiva alle **14:13:21
UTC**, mentre il file era stato scaricato alle **16:14** ora locale. La prima lettura è stata **«sono file
vecchi»**, ed era **sbagliata**: la differenza era il **fuso** (CEST = UTC+2), e quell'ultima riga era di
**un minuto prima** del download. I file erano freschissimi.

**L'offset si ricava dai file stessi** — mtime locale contro l'ultima riga UTC — e va ricavato **prima** di
dire «stale». La conseguenza pratica era grossa: le **15:00 locali** in cui il committente aveva premuto la
✕ sono le **13:00 UTC**, e in quella finestra `avvii.txt` porta **1.17.0** ininterrotto fino alle 14:04.
🔴 La prova era stata fatta su **1.17.0**, non su 1.18.0 né su 1.18.1.

⚠️ E due volte, a richieste diverse, la cartella conteneva gli **stessi byte**: si controlla l'**impronta**
prima di leggere. «Stessa impronta» e «fuso frainteso» si distinguono solo facendo **tutt'e due** i
controlli.

### 🔴 L'ORDINE dei guasti diceva che erano due difetti, non uno

```
13:47:30 → 14:10:23   OTTO NullReferenceException di RENDER   (tre abbattono il circuito)
14:11:26 / 14:11:39   solo QUI «A second operation» + diffing:None
```

**Ventiquattro minuti di NRE prima della prima corsa sul `DbContext`.** §CM (in 1.18.1) corregge la corsa
delle 14:11; le NRE delle 13:47 restavano intere, e tre di loro abbattono il circuito **da sole** — cioè
danno lo stesso identico sintomo a schermo. Due cause, un sintomo: contare **quando** invece di leggere
**cosa** è quel che le separa.

### 🔴 Si chiede l'indirizzo, non si deduce

La prima ipotesi sul rimbalzo era il rimando all'edizione militare (§3 qui sotto). L'ha **smentita la barra
degli indirizzi**: `.../libb/airports/editor?icao=LIBV#s-4256` — la **stessa** pagina con un'ancora di
sezione, non l'editor militare. Il difetto trovato per quella strada è reale ed è stato corretto lo stesso,
ma **non era il sintomo segnalato**. Una domanda da cinque secondi ha risparmiato una diagnosi intera.

---

## 1. La NRE di render non è dell'editor APP: è di quello CONDIVISO

Tre occorrenze su `DocumentSectionsEditor.BuildToc():138`, una su `SectionsBody:147`, e sono **lo stesso
dereferenziamento**: `RootSections ?? Doc.Sections`. Quel componente lo rendono **tutte e cinque** le
famiglie, quindi non era un guasto dell'APP: era il motore comune.

⚠️ **Il `.g.cs` in `obj/` non serve a mappare le righe** — è di un'altra data e le sue mappature non
combaciano più col sorgente. La riga si legge **sul commit che girava**: `git show 30ca658:...`.

🔴 **Il sospetto misurabile**, e il motivo per cui il compilatore tace:

```csharp
[Parameter, EditorRequired] public EditableDocument Doc { get; set; } = default!;
```

`EditorRequired` è un avviso **al chiamante, in compilazione**: non è una garanzia a runtime. E `default!`
zittisce il compilatore per sempre — quindi `Doc.Sections` e `Doc.Language` sono dereferenziamenti **nudi**
su un riferimento che il tipo dichiara non nullo e che nasce nullo. Prova indiretta: `Doc is not null` su
quel parametro compila con **zero avvisi**.

⚠️ Tutti e cinque i chiamanti hanno però la guardia `_shell.Doc is null` prima di montarlo. La spiegazione
quindi **non è banale**, ed è esattamente per questo che qui non si è messa una toppa: si è messa una rete.

### Che cosa si è fatto: la rete, con lo stampo di `Testata()`

`NoInlining` + `try/catch` che **rilancia con il contesto**, su `BuildToc()` e sul nuovo
`PrologoDelCorpo()`; la frase la compone `ContestoDiRender.EditoreDiSezioni(...)` e dice documento
caricato/no, i conteggi (⚠️ `(null)` **non** è `0`: è la differenza fra «non c'era l'elenco» e «l'elenco era
vuoto», cioè la domanda), profilo, indice, radice, gruppo.

🔴 **`NoInlining` è tutto il punto.** `LinguaDelDocumento` (che è `Doc.Language`), `Titolo` e `Offset` sono
membri **corti**: in Release finiscono dentro il chiamante e si portano via la propria riga. È il motivo per
cui tre volte la diagnosi si era fermata sul «sulla riga incolpata non c'è niente che possa essere nullo» —
la riga incolpata era quella del **chiamante**. Stessa malattia già curata su `AppSectionsEditor.Testata()`,
curata in un punto solo e lasciata qui.

⚠️ **La rete non chiude il difetto**: rende diagnosticabile la **prossima** occorrenza. Se dirà
«documento=(NON caricato)», il rimedio è una guardia e la diagnosi è finita.

---

## 2. Il tornello aspettava SENZA TETTO — ed è quello che spegneva la ✕

`DocumentEditorShell.CodaAsync` faceva `_tornello.WaitAsync()` **senza tetto**, mentre `ChiudiAsync` due
metodi più sotto un tetto ce l'ha da sempre (15 s). **La stessa attesa con due regole diverse**, e si è
piantata quella senza guardia — che è il modo tipico in cui una difesa viene scavalcata.

La catena, per intero:

```
Togli(m) → UnionPanel.EseguiAsync: _busy = true
        → await Changed → UnioneCambiata → RicaricaAsync → InFilaAsync
        → attesa che non torna
        → il `finally` non gira → _busy resta acceso
```

E `InlineConfirm` porta `disabled="@Disabled"` **sul tasto d'innesco**: quindi la ✕ non era inerte, era
**spenta**, e un tasto spento non apre nemmeno la propria conferma. 🔴 La differenza fra «inerte» e
«spento» si vede a schermo ed è la prima cosa da chiedere.

### Le due porte si comportano in modo diverso, ed è voluto

| | scaduto il tetto (30 s = `DefaultCommandTimeout`) |
|---|---|
| **gesto** (`InFilaAsync`) | **solleva** → errore in pagina, badge indietro, il `finally` di chi ha premuto gira, **i tasti si riaccendono** |
| **caricamento** (`CaricaInFilaAsync`) | **rinuncia e lo scrive nel log** — il suo `Task` lo tiene il renderer, e sollevare lì abbatterebbe il circuito **per un ritardo**. Il render dopo riprova da sé |

⚠️ Il tetto è un **parametro facoltativo del costruttore**: un tetto di trenta secondi non si prova su un
banco — la prova durerebbe trenta secondi, o non ci sarebbe.

✅ **Provato che DISTINGUE**: `Un_gesto_che_non_ottiene_il_turno_SOLLEVA_invece_di_restare_appeso` e
`Un_caricamento_che_non_ottiene_il_turno_RINUNCIA_senza_sollevare`. Sul codice di prima **falliscono
tutt'e due, piantandosi** — ed è per questo che il `WaitAsync` di sicurezza sta sul gesto e non sull'assert.

---

## 3. Una NAVIGAZIONE dentro il RICARICO

⚠️ **Difetto reale, ma non è il sintomo segnalato** (§0). Corretto lo stesso perché è dello stesso genere.

`AirportSectionsEditor.LoadAsyncCore` aveva, nudo dentro il ricarico:

```csharp
if (_milState is { IsMilitaryOnly: true, DocumentId: null } && MilEditorUrl() is { } milUrl)
{ Nav.NavigateTo(milUrl, forceLoad: true); return; }
```

E il ricarico lo fa scattare **ogni** gesto sull'unione: `Sposta` → `Changed` → `UnioneCambiata` →
`RicaricaAsync` → `LoadAsync`. `forceLoad: true` è una navigazione **vera del browser**, non un cambio di
componente.

⚠️ **`EditorUrl` non torna MAI `null`** — è un'interpolazione secca. Il pattern `is { }` accanto **sembra**
una guardia e non lo è: l'unico interruttore era `_milState`. È il genere di riga che si legge come protetta
proprio perché la forma è quella di una protezione.

✅ Due guardie indipendenti, e la decisione **estratta** in `RimandoAllEdizioneMilitare.Serve`:

- **una volta sola** — mandare qualcuno nel posto giusto è una decisione d'**ingresso**; al ricarico numero
  due chi guarda sta già lavorando lì, e la stessa risposta non è più una guida, è uno strappo;
- **`Chrome`** — senza chrome quell'editor è un **membro** montato dentro l'unione di qualcun altro, e un
  membro che naviga si porta via la pagina dell'**ospite**, cioè un documento che non è suo.

🔴 **Ha fatto cadere un presidio esistente, ed è la conferma che quel presidio funziona.**
`DatiDelloScaloMilitareTests.Chi_rimanda_e_chi_lascia_entrare_chiedono_la_stessa_cosa` cerca la domanda
**come testo** per tenere allineate le due pagine gemelle, e la domanda aveva cambiato file. Aggiornato
**dove guarda**, non indebolito — e aggiunta l'asserzione che l'editor passi davvero da
`RimandoAllEdizioneMilitare.Serve(`: una guardia estratta che nessuno chiama sarebbe scritta e non
applicata, con il presidio che resta verde.

---

## 4. 🔴 IL rimbalzo in home: `wireAnchors`, e l'ordine di quattro righe

`vipi-ui.js` aveva:

```js
var el = document.getElementById(id);
if (!el) return;            // 🔴 esce SENZA preventDefault
e.preventDefault();
e.stopImmediatePropagation();
```

Se la sezione **non è nel DOM**, la protezione **si sfila proprio nel caso in cui serve**: il clic prosegue,
lo prende l'intercettore di navigazione di Blazor, che con `<base href="/">` risolve `#s-4256` come
**`/#s-4256`**: la radice, che rimanda alla **pagina dei servizi**.

✅ **Misurato dal vivo, sul pacchetto minificato**: sul JS di 1.18.1 il clic su un'ancora senza bersaglio
porta da `/services/vsop/libb/airports/editor` a **`/services`**. ⚠️ Non a `/`: un controllo scritto come
`pathname === '/'` sarebbe passato **proprio nel caso che deve inchiodare**, ed è il primo modo in cui una
prova dà un verde bugiardo. L'asserzione giusta è «resta sulla **stessa** pagina».

🔴 **E il commento due righe più su lo diceva già, testualmente**: *«Con `<base href="/">` i link "#id"
verrebbero risolti come "/#id" (→ home)»*. Le righe sotto lo impedivano in **tutti i casi tranne quello per
cui erano state scritte**. È la forma peggiore di una difesa: presente, documentata, e scoperta nel punto
difficile.

**Quando manca il bersaglio:** riordinando i membri dell'unione il DOM si ricostruisce, e la sezione di un
membro non è ancora resa. ⚠️ **Da 1.18.1 quella finestra è più LARGA, non più stretta**: i caricamenti dei
membri passano **uno per volta** dal tornello (§CM). È lo stesso motivo per cui il committente ha notato che
«il caricamento quando viene unito non è fluido» — non è un difetto, è il prezzo pagato per togliere le
corse sul `DbContext`.

✅ `preventDefault` + `stopImmediatePropagation` **sempre**, `getElementById` **dopo**. Un'ancora che non
trova il bersaglio deve **non fare niente**: o il bersaglio arriva col render successivo, o quel link è
morto — e in nessuno dei due casi la risposta è portare via chi stava lavorando.

✅ Presidio sul **testo** del JS (stessa scelta di `EditorTocDragTests`): `AncoraCheNonTrovaIlBersaglioTests`
fissa **l'ordine** delle due istruzioni. Quel che va difeso è l'ordine di due righe dentro un gestore
`capture` su `document`: bUnit non ha un intercettore di navigazione di Blazor contro cui farlo correre, e
montare il componente direbbe solo che il markup ha gli anchor giusti — **che era vero anche col difetto**.
Sul JS di prima **falliscono tutt'e due**.

⚠️ Il presidio conta i `return` **togliendo i commenti**: la prosa accanto nomina `return` a parole, e un
presidio che diventa rosso per una spiegazione scritta bene è un presidio che si fa **cancellare** invece
che leggere.

✅ **E provata sul PACCHETTO, non sul sorgente**, che qui non era una formalità: il minificatore riscrive
quel gestore in **espressioni-virgola** (`a&&(id=...,id)&&(e.preventDefault(),e.stopImmediatePropagation(),
...,el=document.getElementById(id),el)&&(...)`), quindi «l'ordine regge anche dopo la minificazione» è un
fatto che solo una prova a schermo può stabilire. Driver: `.claude/skills/verifica-live/ancora-verifica.js`.
Guidati **due** pacchetti win-x64 sulla stessa pagina, con lo stesso banco:

| pacchetto | ancora senza bersaglio |
|---|---|
| **1.18.1** | 🔴 `PORTATA VIA: /services/vsop/libb/airports/editor -> /services` |
| **1.18.2** | ✅ `/services/vsop/libb/airports/editor` — resta dov'è |

✅ Più i **dieci** controlli di smoke del pacchetto (`pacchetto-verifica.js`), tutti verdi, e il timbro
`1.18.2 · 578300a` letto in `avvio-diagnostica.txt`.

🔴 **Questa correzione tocca `wwwroot`**: il pacchetto deve portare `vipi-ui.js` (coi suoi `.br`/`.gz`) e
`Vipi.Host.staticwebassets.endpoints.json`. 1.18.1 non aveva asset — se si consegna senza, la correzione
del rimbalzo semplicemente **non arriva**.

---

## 5. Una trappola dell'attrezzatura, non del prodotto

Passando `\b` attraverso heredoc + Python, in un `.cs` è finito un **carattere di controllo** (`\x08`).
⚠️ Quello non fa fallire un test: fa **cadere l'host dei test**. Spazzato via e ricontrollati **tutti** i
file toccati, uno per uno.

---

## Che cosa resta aperto

- ⚠️ La **NRE di render** è **strumentata, non chiusa**. La prossima occorrenza deve portare il contesto:
  se dice «documento=(NON caricato)», il rimedio è una guardia su `Doc`.
- ▶ Il gesto che decide tutto: **rifare l'unione a tre di Gioia** con 1.18.2 in barra, poi mandare
  `errori-richieste.txt`. È l'unico modo di sapere **quale firma resta**.
- ⚠️ I **ventuno** componenti con lo scope proprio che non aspettano, e le ventuno pagine che prendono un
  servizio dal circuito, restano il debito misurato di §CF.
