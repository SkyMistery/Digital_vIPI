# Feature — Process (anti-vibecoding) 🟢

> Gemello di [refactor/REFACTOR-PROCESS.md](refactor/REFACTOR-PROCESS.md): quello governa i **refactor**,
> questo le **feature nuove**. Nasce dopo aver ripulito 34 round di vibecoding (asse refactor 01→09):
> il debito rientra dalle feature, non solo dai refactor, quindi anche le feature hanno un gate leggero.
>
> Regola d'oro: **decisione su carta prima del codice, slice piccole, verifica sul flusso reale.**
> È ciò che ha reso i 9 doc refactor puliti mentre i 34 round no.

## Pre-flight — 4 domande (2 minuti, prima di toccare codice)

Rispondi a queste quattro PRIMA di scrivere. Se una non ha risposta pulita, il design non è pronto.
(La 4ª vale solo se la modifica rimuove o rinomina qualcosa; per feature puramente additive, salta.)

### 1. Modello — «aggiungo un concetto o ne esiste già uno?»
- Mai **affiancare** un secondo modello a uno esistente per la stessa cosa. Il dolore storico era
  due modelli documento in parallelo (`Document` classic vs `profile` JSON) → risolto in doc 08
  unificando su `Document`. Ogni volta che stai per aggiungere un'entità/tabella/DTO «gemella» di
  una che c'è già: **estendi o sostituisci, non affiancare.**
- Domanda di controllo: se fra 6 mesi qualcuno cerca «dove si salva X», trova **un** posto o due?

### 2. Dispatch — «sto per switchare su un tipo che switcho già altrove?»
- **Regola del 2**: se lo stesso `switch (tipo)` / catena di `if` per-tipo compare in **≥2 punti**,
  è il segnale per estrarre un **descrittore + registry** (una implementazione per tipo, i motori
  iterano/consultano il registry). Esempio realizzato: doc 09, `IReleaseTarget` (identità DB-side)
  e `IDocKindRoutes` (rotte UI). Aggiungere un tipo = registrare 1 descrittore, **zero switch toccati**.
- Se invece il tipo è stabile e lo switch è in **un solo** posto: lascia lo switch, il registry sarebbe
  over-engineering.

### 3. Ingressi + verifica — «come ci arriva l'utente e come lo verifico?»
- **Ingresso UI**: ogni tipo/entità nuova ha bisogno di un punto da cui l'utente lo **crea** e lo
  **raggiunge**. Attenzione al **catch-22**: se la lista mostra solo gli elementi già pubblicati,
  il PRIMO non è raggiungibile (successo reale: lista APP mostrava solo i pubblicati → nessun modo
  di creare il primo; fix = mostrare allo staff anche quelli senza documento).
- **Verifica**: decidi già ora come proverai che funziona **guidando il flusso reale** (non solo i test).

### 4. Propagazione — «questa modifica *rimuove* o *rinomina* qualcosa?»
- Se sì, chi lo cita va aggiornato **nello stesso giro**, non «dopo»: nomi di tipi/file, commenti/`<see cref>`,
  doc di area, spec autorevoli, **memorie**. Il debito peggiore non è il codice sbagliato ma il **record
  rimasto vero a metà** (dolore reale: lo storage `*Profile` droppato ma i tipi ancora chiamati `*Profile*`
  → confusione operativa; commenti con `<see cref>` a entità morte → solo warning, passano).
- Regola: **quando togli il backing/storage di qualcosa, rinomina i tipi che lo citavano** — un nome che
  descrive un meccanismo sparito mente a chi legge fra 6 mesi.

## Post-flight — esecuzione

- **Slice piccole verticali**, 1 commit per passo, `dotnet build` verde a ogni commit. Storia bisezionabile.
- **Meccanico separato da logica** (estrazioni/rename in commit distinti da cambi di comportamento).
- **Test-first di caratterizzazione** sul cuore deterministico (logica senza IO) PRIMA di ristrutturare
  codice complesso non coperto (invariante #8 del runbook refactor).
- **Verifica live**: guida il flusso end-to-end (skill `/verify` o `/run`, o CDP/puppeteer come i round
  di verifica). Le regressioni UI/binding Blazor sono **silenziose coi test verdi**:
  - attributo componente di tipo `string` senza `@` = **letterale**, non variabile (`Key="x"` ≠ `Key="@x"`)
    → render vuoto senza errore;
  - flussi `EnsureAsync`/lock/bozza si vedono solo guidando l'editor reale.
- **ValidationException**: i service Application usano `Vipi.Application.*.ValidationException`, mai
  DataAnnotations (altrimenti la UI non cattura → crash circuito).

## Definition of Done (checklist)

- [ ] Pre-flight 4 domande risposte (modello / dispatch / ingressi+verifica / propagazione).
- [ ] Nessun modello «gemello» aggiunto; nessun nuovo `switch(tipo)` duplicato (o registry introdotto).
- [ ] Ingresso UI per creare **e** raggiungere il primo elemento (no catch-22).
- [ ] `dotnet test` verde (== o > baseline), test-first sul cuore complesso non coperto.
- [ ] ⚠️ **`dotnet build` verde vale su ENTRAMBI i TFM.** Dall'11 agosto 2026 `Directory.Build.props` rende
      gli avvisi errori, e `dotnet test` **non** usa quel flag: la suite verde e la build di produzione rotta
      possono convivere — è già successo, 1391 test verdi e 28 errori in CI. Il comando che chiude la
      questione è `dotnet build Vipi.slnx -c Release --no-incremental`.
- [ ] **Verificato live** sul flusso reale, non solo test — con **traccia** (nota «verificato X guidando Y», o log CDP).
- [ ] 1 commit/passo, build verde; doc/spec autorevoli aggiornati se schema o rotte cambiano.
- [ ] **Tracciamento coerente**: header == tabella indice == `rounds`; nessuno stato contraddittorio (`✅` da una parte, `🟡` dall'altra).
- [ ] **Nessun nome/commento morto**: la slice non lascia tipi/file/`<see cref>`/doc/memorie che citano qualcosa rimosso o rinominato in questo giro.
- [ ] **Se hai aggiunto un pacchetto o un progetto**: `packages.lock.json` rigenerato e committato (la CI
      restora in «locked mode» e si ferma senza). Un progetto nuovo fuori da `Vipi.slnx` va restaurato a mano.

## Un rosso intermittente

- **Catturare il NOME, non il conteggio.** Nel ciclo di caccia si filtra `[FAIL]`, non solo `Failed!`: un
  riepilogo «Failed: 1» senza il nome del test non si può analizzare, e il giro dopo è verde. Costato due
  volte il 2 settembre 2026.
- **Riprodurre nella condizione in cui si è visto.** Se compare solo a **soluzione intera**, isolare il
  progetto non prova niente: quel verde è il verde della condizione sbagliata. (Misurato: diciotto giri di
  `Vipi.Ui.Tests` in parallelo a sei vie, tutti verdi, su un rosso che a soluzione intera si era visto due
  volte.)
- ⚠️ **Finché non ha un nome non si tocca niente.** Una correzione a un difetto che non si sa nominare è
  solo un altro cambiamento — e il verde che segue non è una prova.
- ✅ **Quando il nome c'è, la strada si MISURA — non si sceglie.** Un test che compra tempo di orologio a
  credito può cadere per più vie, e a occhio se ne indovina quella sbagliata. Il modo: una misura
  **temporanea** nel progetto di test che ripete il corpo qualche centinaio di volte **sotto zavorra
  artificiale del thread pool** e stampa la distribuzione degli esiti. Poi si toglie.
  (3 settembre 2026, `DelayedUiActionTests.La_nuova_annulla_la_precedente`: 300 giri × 2, l'esito sbagliato
  era `0` in **109 e 106 giri su 300** e quello che avevo dato per più probabile — `2` — **non è mai
  comparso in 600 giri**. Il ragionamento a priori aveva scelto la via sbagliata; la misura no.)
- ⚠️ **E la forma nuova si prova con una MUTAZIONE**, o si è solo tolto il rosso: si rompe di proposito
  la cosa che il test difende e si controlla che diventi rosso. 🔴 Nello stesso giro la mutazione ha
  rivelato che il test — vecchio **e** nuovo — ha un punto cieco inerente: provare che una cosa <i>non</i> è
  successa vuole un'attesa, e sotto fame di thread nessuna attesa basta sempre. Va **scritto nel test**,
  non scoperto dal prossimo.
- **Guardare fuori dal processo.** Un caso non era né nel codice né nei test: era il **registro eventi di
  Windows**, un provider di log che `WebApplication.CreateBuilder` aggiunge da sé e che nessuno aveva
  scelto. Si è visto con `Get-WinEvent` — **535 voci in tre ore di suite** — non leggendo sorgenti. La
  cronaca sta in `history/rounds.md`.

> **Prima di dire «non serve, misuriamo»: misura davvero.** Nell'audit dell'11 agosto tre voci su
> trentaquattro sono state *ribaltate dal dato* — un difetto reale nel parser dei poligoni non toccava
> nessuno dei 1338 poligoni veri, e due retention servivano per tabelle da 19 e 1 riga. Una query sul
> `vipi.db` reale costa cinque minuti e ha risparmiato tre lavori inutili. Vale anche al contrario: due
> guardie nuove hanno **smentito affermazioni già scritte nei commit**.

## Quando questo processo NON serve

Fix di una riga, copy, tweak di stile, tuning di un valore: applica solo la verifica live finale.
Il pre-flight è per tutto ciò che introduce un **tipo, un'entità, uno stato, o un ramo di dispatch** —
o che **rimuove/rinomina** uno di questi (→ domanda 4).
