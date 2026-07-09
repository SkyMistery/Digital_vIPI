# Refactor — Process (runbook) 🟢

> Runbook operativo del refactor strutturale. Definisce **come** eseguire in sicurezza
> un singolo refactor. Complementa [00-overview.md](00-overview.md), che definisce
> **cosa** e **in che ordine**.
>
> **Un ciclo (Fase 0→4) per ogni doc di area**, ripetuto in ordine `01 → 02 → … → 09`
> (bottom-up, DAG dipendenze). 9 doc = 9 giri. Non in parallelo: lo strato sotto va
> chiuso (✅) prima di salire.

## Cosa è per-giro e cosa è globale

| | Vale |
|---|---|
| **Globale** (una volta sola) | Questo file, i principi in `00 §Principi guida`, il glossario. |
| **Per-giro, sempre uguale** | La spina: branch, build/test baseline, commit per passo, verify, chiusura doc. |
| **Per-giro, dal doc specifico** | Il contenuto di Fase 2 (dai «Passi di migrazione» sez. 4) e le verifiche di Fase 3 (da «Impatto/Verifica» sez. 5). |

## Regole invarianti (valgono in ogni fase, ogni giro)

1. **Carta prima di codice.** Nessuna riga toccata finché Fase 0 non è chiusa. No eccezioni.
2. **Ogni commit compila.** `dotnet build` verde a ogni commit.
3. **Meccanico separato da logica.** Estrazione tipi/DTO/rename in commit distinti dai
   commit che cambiano comportamento.
4. **ValidationException convention.** Service Application usano
   `Vipi.Application.*.ValidationException`, mai DataAnnotations (altrimenti la UI non
   cattura → crash circuito). Vedi `00 §Principi` #2.
5. **Niente scope creep.** 1 doc = 1 area = 1 branch = 1 PR. Se emerge lavoro di un'altra
   area → annotalo nel suo doc, non farlo qui.
6. **Agnosticismo dal provider.** Ogni sorgente/servizio esterno sta dietro una porta
   Application (interfaccia); l'implementazione concreta vive in Infrastructure e si
   sostituisce con un nuovo adapter senza toccare Application/UI. Un refactor non può
   accoppiare un consumatore a un provider concreto né esporre tipi dell'adapter oltre la
   porta. Vale anche per i servizi interni Application: interfaccia nello stesso file (per
   coerenza + mockabilità), come da convenzione repo.

---

## Fase 0 — Carta (gate: blocca il codice)

Scopo: non toccare codice finché il piano scritto è approvato e congelato.

- [ ] **Rileggi sez. 1 (Stato) e 2 (Problemi)** del doc N. Sono già 🟢; conferma che
      riflettono ancora il codice reale (può esser cambiato dopo la mappatura iniziale).
- [ ] **Promuovi sez. 3 (Architettura target) e sez. 4 (Passi di migrazione)** da 🟡 bozza
      ad approvate: decisioni aperte chiuse, nomi di tipi/file decisi, nessun «da discutere»
      residuo.
- [ ] **Owner approva il target.** Punto umano esplicito. Senza ok → **STOP**, si resta su carta.

**Output:** doc N con sez. 3+4 approvate. Zero righe di codice toccate.

**Perché:** refactor senza target congelato = deriva. Il gate forza la decisione quando
cambiarla costa zero.

---

## Fase 1 — Baseline (rete di sicurezza)

Scopo: fotografia dello stato verde attuale, per provare a fine giro che hai cambiato
*forma* e non *comportamento*.

- [ ] **Branch dedicato:** `refactor/NN-nome-area` (es. `refactor/01-import-infra`).
- [ ] **Build + test verdi ORA:** `dotnet build` poi `dotnet test`. **Registra il conteggio**
      test passati (es. «142 verde») — è il numero che Fase 3 deve eguagliare o superare.
      Se la baseline è già rossa → aggiusta *prima* di partire.
      - ⚠ Gotcha noto: se i bin dei test danno *Access denied*, risolvi qui, non a metà refactor.
- [ ] **Snapshot comportamento** — *solo se* il doc N tocca schema DB o rotte pagine:
      annota lo stato di riferimento nei doc autorevoli `spec/modello-dati.md` e
      `spec/mappa-pagine.md`, così a fine giro il diff è verificabile. Refactor puro-codice
      interno (nessun cambio schema/rotte) → salta questo passo.

**Output:** branch pronto, numero-baseline annotato, (eventuale) snapshot pre-cambio.

**Perché:** senza baseline verde + conteggio, «funziona ancora?» è opinione; con baseline è misura.

---

## Fase 2 — Esecuzione (segue «Passi di migrazione» del doc)

Scopo: applicare i passi della sez. 4 del doc N, in ordine, ciascuno verificabile.

- [ ] **Prima i passi meccanici** (estrai DTO/tipi, split file, rename) — commit separato,
      nessuna logica cambiata.
- [ ] **Poi i passi con logica** — 1 commit per passo, messaggio che cita il doc:
      `refactor(NN): <cosa> — doc NN §4.x`.
- [ ] **Ogni commit: `dotnet build` verde.**

**Output:** i passi sez. 4 applicati, storia di commit pulita e bisezionabile.

---

## Fase 3 — Verifica (usa «Impatto/Verifica», sez. 5 del doc)

Scopo: provare che il comportamento è invariato.

- [ ] **`dotnet test`** → stesso conteggio verde della baseline, o più. Mai meno.
- [ ] **Verifica il flusso reale** (skill `/verify` o `/run`): guida il flusso toccato, non
      solo i test. Osserva il comportamento end-to-end.
- [ ] **Check specifici del doc N** (sez. 5). Esempi:
      - doc 01: import manuale e auto producono lo stesso stato DB; `SyncFromCatalogsAsync`
        gira una sola volta per import.

**Output:** prova che forma cambiata, comportamento no.

---

## Fase 4 — Chiusura

Scopo: tracciare e propagare (vedi `00 §Principi` #6).

- [ ] **Doc area N:** stato → ✅ + note «cosa fatto».
- [ ] **Spec autorevoli** se schema/rotte cambiati: `spec/modello-dati.md`, `spec/mappa-pagine.md`.
- [ ] **`history/rounds.md`:** riga del round.
- [ ] **`00-overview.md`:** tabella indice → ✅ per il doc N.
- [ ] Merge del branch.

**Output:** doc chiuso, tracciamento propagato. Si passa al doc N+1 da Fase 0.

---

## Riepilogo ciclo

```
per doc N in 01..09 (in ordine):
  Fase 0  Carta       → sez. 3+4 approvate, owner ok        [gate: no codice prima]
  Fase 1  Baseline    → branch + test verdi + conteggio
  Fase 2  Esecuzione  → passi sez. 4, 1 commit/passo, build verde
  Fase 3  Verifica    → test == baseline, /verify, check sez. 5
  Fase 4  Chiusura    → ✅ doc + spec + rounds + overview + merge
```
