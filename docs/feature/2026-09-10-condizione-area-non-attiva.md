# Condizione di coordinamento: «area NON attiva» 🟢

**Chiesto dal committente il 10 settembre 2026.** «Nei trasferimenti c'è modo di dire che un certo
trasferimento non si può effettuare se c'è un'area tipo la $406 attiva? Abbiamo il caso in cui un certo
trasferimento vale solo con un'area attiva ma non il contrario.»

## La domanda

La condizione di una clausola ha tre dimensioni indipendenti e additive — **pista in uso**, **area attiva**,
**personalizzata** — e la seconda ha **una polarità sola**. `ConditionAreaLabel` è una stringa, e il template
che la rende dice sempre *attiva*:

```csharp
public string Area { get; init; } = "con {label} attiva";
public string RunwayAndArea { get; init; } = "con pista {runway} in uso e {area} attiva";
```

Il rovescio — «questo trasferimento non vale se la $406 è attiva» — oggi si può scrivere **solo nella terza
colonna, come prosa libera**. Funziona, ed è quel che va fatto finché questa carta non è in produzione, ma
costa una cosa che si paga dopo: scritta a mano, `$406` **non è più l'area `$406`**, e il giorno che qualcuno
la rinomina il report di consistenza non se ne accorge — la guardia
`ConsistencyReportService` («Area «X» non presente tra le aree speciali: rinominata o rimossa») guarda
`ConditionAreaLabel`, non il testo libero.

## La decisione: una POLARITÀ, non una quarta dimensione

`ConditionAreaNegated`, un `bool` accanto all'etichetta. **Non** una quarta colonna «area non attiva».

⚠️ **Perché non la quarta colonna.** Una quarta dimensione permetterebbe di scrivere «con A attiva **e** B non
attiva» su **una** riga, che sembra un guadagno. Non lo è: quella frase si scrive già oggi, e meglio, con
l'**outline delle varianti** — la riga capofila dice «con A attiva», l'eccezione appesa sotto dice «con B non
attiva», e `BuildCondition` **cumula la catena** in AND (`Merge`). Aggiungere la colonna vorrebbe dire due
modi di dire la stessa cosa, e il secondo nasconderebbe la struttura che il documento poi stampa.

⚠️ **La polarità sta sull'AREA, non sulla condizione.** Non si nega la pista in uso («con pista 16R non in
uso» non è fraseologia, ed è la stessa cosa che dire quale pista *è* in uso), e non si nega la personalizzata,
che è già prosa e può negarsi da sé. Una polarità per dimensione sarebbe simmetria per la simmetria.

⚠️ **La bandiera senza l'etichetta non vuol dire niente**, e si azzera scrivendo — come `ConditionRefId`, che
esiste solo se c'è una pista:

```csharp
c.ConditionRefId       = c.ConditionLabel     is null ? null  : i.ConditionRefId;
c.ConditionAreaNegated = c.ConditionAreaLabel is null ? false : i.ConditionAreaNegated;   // ← nuova, stesso modo
```

> ⛔ **Superato in parte dalla carta [«PIÙ aree»](2026-09-10-condizione-piu-aree.md)** (stesso giorno):
> `RunwayAndArea` e `RunwayAndAreaInactive`, descritti qui sotto come «forme dedicate», **non esistono più** —
> erano la regola generale dei frammenti scritta a mano, e il testo che producevano non è cambiato di un
> carattere. Il resto di questa carta — la polarità, la bandiera che segue l'etichetta, il tag simbolico —
> resta in vigore.

## Le parole: DUE template nuovi per lingua, non uno

Pista e area insieme non sono l'unione delle due clausole: usano una **forma dedicata** («con pista X in uso e
Y attiva», fraseologia approvata). Quindi il rovescio ne vuole due:

| | IT | EN |
|---|---|---|
| `Area` *(c'è già)* | con {label} attiva | with {label} active |
| **`AreaInactive`** | con {label} **non attiva** | with {label} **not active** |
| `RunwayAndArea` *(c'è già)* | con pista {runway} in uso e {area} attiva | with runway {runway} in use and {area} active |
| **`RunwayAndAreaInactive`** | con pista {runway} in uso e {area} **non attiva** | with runway {runway} in use and {area} **not active** |

## La fusione della catena si SDOPPIA

`Merge` fonde la catena dimensione per dimensione e ne fa **una** `ConditionClause`. Con la polarità non
basta più: due aree di polarità opposta non si possono unire in una stringa sola, o «A attiva e B» direbbe che
anche B è attiva.

La catena si fonde quindi in **due secchi** — aree attive e aree non attive — e `ClausesOf` ne cava fino a due
clausole:

- pista + attive → forma combinata; le **non attive** che restano si appendono con la congiunzione
- pista + **solo** non attive → forma combinata **rovescia** (`RunwayAndAreaInactive`)
- niente pista → `Area` e/o `AreaInactive`

Esempio con capofila «$406 attiva» ed eccezione «$407 non attiva»:
> «… con $406 attiva e $407 non attiva.»

## Il tag breve (pill) resta un TAG

`TransferConditionText.Display` compone l'etichetta corta del pill admin e della chip nelle viste live. Non ha
un localizzatore — è una proprietà calcolata su un record — e già oggi scrive `area {nome}` con un prefisso
non tradotto. Qui **non si peggiora e non si finge**: la negazione entra come **simbolo**, `area {nome} ⊘`,
scelto perché non compare da nessun'altra parte nell'app e perché il ✕ in questa pagina vuol già dire
«elimina».

⚠️ **La divisione è dichiarata**: il **tag** è un simbolo, la **prosa** è a parole, e le parole sono quelle che
finiscono nel documento pubblicato e nella vLOA. Chi legge il documento non vede mai il simbolo.

## Il matcher Aurora non cambia, ed è giusto

`TransferMatcher.EvaluateCondition` dichiara `unknown` per area e personalizzata: «la pista si può controllare
(Aurora dà `#CTRLRWY`); area e condizione personalizzata no». Un'area **non** attiva è ignota esattamente
quanto una attiva — la bandiera non aggiunge né toglie verificabilità, e `hasUnverifiable` resta com'è.

## Pre-flight (`docs/FEATURE-PROCESS.md`)

1. **Modello** — nessun concetto nuovo: una colonna accanto a una che c'è già, sulla stessa entità. Chi fra sei
   mesi cerca «dov'è la condizione d'area» trova **un** posto.
2. **Dispatch** — nessuno `switch` nuovo. Le due forme combinate sono due `if` dentro `ClausesOf`, che è
   l'unico posto che compone le clausole condizione.
3. **Ingressi + verifica** — la casella sta nel pannello della clausola, accanto al picker dell'area, ed è
   raggiungibile ovunque lo sia la condizione. Verifica: **dal vivo**, seminando una clausola con l'area e
   guardando la frase resa, in italiano **e** in inglese.
4. **Propagazione** — questo giro **non rimuove né rinomina** niente: è additivo. La riga della carta dei
   trasferimenti e la spec del modello dati vanno comunque aggiornate.

## Definition of Done

- [ ] Migrazione **additiva** per i due provider (`bool NOT NULL`, default `false`).
      ⚠️ `false` su tutte le righe esistenti **è** il significato giusto — «attiva» — e non un ripiego.
- [ ] `dotnet build Vipi.slnx -c Release --no-incremental` verde sui **due TFM**, 0 avvisi.
- [ ] Suite verde **contando i progetti**.
- [ ] 🔴 **`real-coordination.approved.txt` non si muove di un carattere.** Con la bandiera a `false` la frase
      e il tag devono uscire **identici** a prima: è la rete che dice che il giro è additivo davvero.
- [ ] Verifica live: frase resa in IT e in EN, pill, round-trip del salvataggio, e la bandiera che si azzera
      togliendo l'area.
- [ ] Carta, `docs/spec/modello-dati.md` §9.20 e la memoria aggiornate.
