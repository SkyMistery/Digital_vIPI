# Feature — Process (anti-vibecoding) 🟢

> Gemello di [refactor/REFACTOR-PROCESS.md](refactor/REFACTOR-PROCESS.md): quello governa i **refactor**,
> questo le **feature nuove**. Nasce dopo aver ripulito 34 round di vibecoding (asse refactor 01→09):
> il debito rientra dalle feature, non solo dai refactor, quindi anche le feature hanno un gate leggero.
>
> Regola d'oro: **decisione su carta prima del codice, slice piccole, verifica sul flusso reale.**
> È ciò che ha reso i 9 doc refactor puliti mentre i 34 round no.

## Pre-flight — 3 domande (2 minuti, prima di toccare codice)

Rispondi a queste tre PRIMA di scrivere. Se una non ha risposta pulita, il design non è pronto.

### 1. Modello — «aggiungo un concetto o ne esiste già uno?»
- Mai **affiancare** un secondo modello a uno esistente per la stessa cosa. Il dolore storico era
  due modelli documento in parallelo (`Document` classic vs `profile` JSON) → risolto in doc 08
  unificando su `Document`. Ogni volta che stai per aggiungere un'entità/tabella/DTO «gemella» di
  una che c'è già: **estendi o sostituisci, non affiancare.**
- Domanda di controllo: se fra 6 mesi qualcuno cerca «dove si salva X», trova **un** posto o due?

### 2. Dispatch — «sto per switchare su un tipo che switcho già altrove?»
- **Regola del 3**: se lo stesso `switch (tipo)` / catena di `if` per-tipo compare in **≥2 punti**,
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

- [ ] Pre-flight 3 domande risposte (modello / dispatch / ingressi+verifica).
- [ ] Nessun modello «gemello» aggiunto; nessun nuovo `switch(tipo)` duplicato (o registry introdotto).
- [ ] Ingresso UI per creare **e** raggiungere il primo elemento (no catch-22).
- [ ] `dotnet test` verde (== o > baseline), test-first sul cuore complesso non coperto.
- [ ] **Verificato live** sul flusso reale, non solo test.
- [ ] 1 commit/passo, build verde; doc/spec autorevoli aggiornati se schema o rotte cambiano.

## Quando questo processo NON serve

Fix di una riga, copy, tweak di stile, tuning di un valore: applica solo la verifica live finale.
Il pre-flight è per tutto ciò che introduce un **tipo, un'entità, uno stato, o un ramo di dispatch**.
