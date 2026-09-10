# Le SID nel vSOP militare 🟢

**Chiesto dal committente il 10 settembre 2026**: *«nelle vSOP andrebbero aggiunte le SID. Se ci sono delle
vIPI civili le SID sono prese da lì, come frequenze e transition altitude; se è military only devono essere
direttamente nelle vSOP.»*

## La precisazione che semplifica tutto

Le SID **non stanno «nella vIPI»**: stanno nell'**anagrafica dell'aeroporto** (`AirportSids`, importate dal
sectorfile per ICAO). La vIPI civile è soltanto la **porta di scrittura**, esattamente come per frequenze,
piste e quote di transizione.

Conseguenza: **in lettura non serve nessun ramo.** Il vSOP legge dall'aeroporto e basta, misto o solo
militare che sia. **Solo la porta di scrittura si biforca** — e quella biforcazione esiste già dal §AS
(`ScaloSenzaCivile` = solo militare **E** senza civile).

## 🔴 Quel che c'è già, e non va scritto

Cercato prima di progettare, e ha tolto tre quarti del lavoro:

| | |
|---|---|
| Il dato (`AirportSids`, import sectorfile, carry fix, lock per-ICAO) | ✅ c'è |
| **La derivazione per il bersaglio `AirportMil`** | ✅ **c'è, ed è già chiamata**: `MilMemberLoader` fa `ResolveForViewAsync(code, useFrozen, ReleaseTargetType.AirportMil)`, che torna un `AirportDerived` **con dentro le `Sids`**. Il vSOP le calcola già e le **butta via** |
| Il congelamento alla release | ✅ c'è: `AirportFrozenSectionProvider` è registrato **due volte**, e gestisce già la chiave `"sids"` |
| Il componente che le disegna (`AirportSids`) | ✅ c'è, già condiviso col vSOP per piste e frequenze |
| **La semina nei vSOP già scritti** | ✅ **c'è**: `AddMissingCatalogSectionsAsync` gira all'avvio, copre già i vSOP militari e **scende nelle sotto-sezioni**. Basta aggiungere la chiave al catalogo |

Manca **solo**: la sezione nel catalogo, il `case` nel corpo militare, e la porta di scrittura.

## Dove va: sotto Runways, in Dati generali

**Deciso dal committente.** Sorella di `runways`, subito dopo — **non** figlia (lì c'è già «Coordinate delle
soglie», e il profilo militare **tocca già `MaxDepth=3`**).

```
Dati generali
  ├── Radioassistenze · Frequenze ATC/CRC · Aeroporti alternati
  ├── Piste ──► Coordinate delle soglie
  ├── SID                                   ← nuova
  └── Quote di transizione · Nominativi · Planimetria · Parcheggi
```

⚠️ **Rinumerare le sezioni che seguono è innocuo per i documenti già scritti**, ed è stato verificato:
`SectionOrdering.OffsetsFromStandard` confronta la **sottosuccessione** delle sole sezioni di catalogo
**presenti nel documento** — «chi manca dal documento non lascia un buco». Spostare in avanti tutti i
successivi non cambia il loro ordine relativo, quindi nessuno si accende come «fuori posto».

⚠️ **`H` e non `HB`**, come la sorella civile: la sezione è **derivata pura**, senza blocchi di prosa. Le
code per campo — «Combat departure» di Gioia — restano **sezioni libere**, che il catalogo sa già fare, ed è
la decisione già scritta in testa al profilo militare.

## La porta di scrittura, e solo dove serve

Nel `MilSectionsEditor` la sezione si redige **solo se `ScaloSenzaCivile`**, come gli altri tre editor
d'aeroporto già ospitati lì (transizione, piste, frequenze). Sui campi **misti** resta la nota di rimando
all'editor civile: due porte di scrittura sullo stesso dato sono il modo in cui una delle due comincia a
mentire.

⚠️ **Nessuna seconda stesura**: `AirportSidsEditor` è già un componente estratto e indipendente dalla pagina
che lo ospita. Qui c'è un secondo **ospite**, non un secondo editor — la stessa forma del §AS.

## ✅ Le SID esistono anche sui campi solo militari

Confermato dal committente. Quindi la sezione **non nasce vuota**, e non si ripete il caso delle aree BOAT
(dove il catalogo IVAO non ne ha nessuna e la sezione nasce vuota per costruzione).

## Pre-flight (`docs/FEATURE-PROCESS.md`)

1. **Modello** — nessun concetto nuovo, nessuna tabella, **nessuna migrazione**: una chiave in più in un
   catalogo, e un dato che era già calcolato e scartato.
2. **Dispatch** — nessuno `switch` nuovo: un `case` in più nel corpo militare e uno nell'editor, cioè i due
   posti che già scelgono per chiave.
3. **Ingressi + verifica** — la sezione compare da sé nei vSOP nuovi (catalogo) e in quelli vecchi (la
   passata d'avvio). Verifica **dal vivo** su un campo **solo militare** (i campi si scrivono lì) e su uno
   **misto** (lì si rimanda), in italiano e in inglese.
4. **Propagazione** — niente si rimuove né si rinomina. Vanno aggiornati i test che **contano** le sezioni
   del profilo (43 → 44, e i figli di «Dati generali» 8 → 9), l'indice del SOD e la spec.

## Definition of Done

- [ ] `dotnet build Vipi.slnx -c Release --no-incremental` verde sui due TFM, 0 avvisi.
- [ ] Suite verde contando i progetti; i due conteggi del profilo militare aggiornati **con intenzione**,
      non per far passare il rosso.
- [ ] 🔴 La sezione compare in un vSOP **già scritto** dopo un riavvio — cioè la passata d'avvio funziona
      davvero su questa chiave, e non solo sui documenti nuovi.
- [ ] Su un campo **misto** i campi delle SID **non** si scrivono nel vSOP: c'è la nota di rimando.
- [ ] Verifica live in italiano e in inglese.
- [ ] Carta, `indice-del-sod`, spec e memoria aggiornate.
