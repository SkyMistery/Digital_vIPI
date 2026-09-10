# Condizione di coordinamento: PIÙ aree su una riga 🟢

**Chiesto dal committente il 10 settembre 2026**, subito dopo la polarità
([`2026-09-10-condizione-area-non-attiva.md`](2026-09-10-condizione-area-non-attiva.md)): «ovunque si possano
inserire delle aree, si devono poter inserire anche più di un'area, e ovviamente le frasi generate si devono
adattare».

## Le due misure che hanno deciso il disegno

Fatte sul catalogo vero (241 aree) **prima** di scrivere, e tutte e due hanno cambiato una scelta:

1. 🔴 **Il separatore delle piste non si può riusare.** `ConditionLabel` elenca le multi-pista con `" / "`, e
   **cinque aree hanno già lo `/` nel nome**: `LI/LD D35/A-CRIT`, `LI/LD D35/B-CRIT`, `LI/LD D35/C-CRIT`,
   `LI R49A/B/C/D/E/F - Zita`, `LI R21A/B - Sara`. Tagliare su `/` le farebbe a pezzi. Il separatore è **`;`**,
   che in catalogo non compare (né `;` né `|` né `·` né `,` né `+`).
2. 🔴 **La colonna da 80 non basta.** Il nome più lungo è 37 caratteri e tre nomi lunghi uniti fanno **105**.
   `ConditionAreaLabel` va a **200**, come `Cops`, che è già dimensionata «lista corta per natura».

## «E» oppure «O»: lo sceglie chi scrive (decisione del committente)

Con più aree la condizione può valere quando **tutte** sono attive o quando **una qualunque** lo è, e il senso
naturale **cambia con la polarità**: al positivo si pensa «tutte», al negativo «se una qualunque è attiva,
niente trasferimento». Sono i due lati di De Morgan, e indovinare per conto proprio vuol dire scrivere in un
documento operativo una regola che nessuno ha deciso.

Scelta: **un selettore per riga**, `ConditionAreaAll` — `false` = *una qualunque* (default), `true` = *tutte*.

- ⚠️ **`false` di default e non `true`**: è il senso che ha già la **multi-pista**, dove il matcher fa
  `wanted.Any(...)` — basta che UNA delle piste elencate sia in uso. Due liste vicine con due sensi opposti
  sono il modo in cui qualcuno legge la seconda con la testa della prima.
- ⚠️ Ed è il default CLR di `bool`, che è la condizione a cui il `PostgresSchemaReconciler` di Render regge
  (vedi il blocco dei default dichiarati in `VipiDbContext`).
- ⚠️ **Spento con UNA sola area**: la domanda non ha senso, e un selettore che si può muovere senza effetto è
  la stessa cosa di un tasto che a volte non fa niente. Col motivo accanto, come la casella «non attiva».

## 🔴 La frase: due template MUOIONO, e il testo esce identico

Oggi pista+area usano una forma dedicata:

```csharp
RunwayAndArea = "con pista {runway} in uso e {area} attiva";
```

Guardandola col problema nuovo si vede che **non è una forma dedicata**: è la regola generale scritta a mano.

```
"con pista {runway} in uso"  +  " e "  +  "{area} attiva"
      Runway                     Join        (la coda)
```

**La regola generale**: la condizione è una fila di frammenti; **il primo porta la preposizione, gli altri no**,
e si uniscono con `Join`. Con quella regola `RunwayAndArea` e `RunwayAndAreaInactive` **si cancellano** e il
testo prodotto è **carattere per carattere lo stesso** — lo dicono i test che c'erano già.

⚠️ **E la regola generale ripara un difetto che c'era da prima**, che le due forme dedicate nascondevano: due
clausole d'area nella stessa catena ripetevano la preposizione — «con $406 attiva **E CON** $407 non attiva».

### I template dell'area, per lingua

| | IT | EN |
|---|---|---|
| `Area` / `AreaMany` | con {label} **attiva** / **attive** | with {label} active |
| `AreaTail` / `AreaTailMany` | {label} **attiva** / **attive** | {label} active |
| `AreaInactive` / `…Many` | con {label} **non attiva** / **non attive** | with {label} not active |
| `AreaInactiveTail` / `…Many` | {label} **non attiva** / **non attive** | {label} not active |
| `AreaAll` · `AreaAny` | **e** · **o** | and · or |

⚠️ **Due chiavi per il plurale, non una.** «Una sola forma plurale sbaglia sempre sull'uno», ed è la regola che
il progetto ha già pagato con «1 clauses»: il progetto non ha ICU, si scelgono due stringhe. In inglese le due
forme coincidono e si dichiarano lo stesso — una coincidenza di lingua non è una regola di codice.

⚠️ **`Runway` resta al singolare** («con pista 16R / 16L in uso») e **non si tocca in questo giro**: è testo
già pubblicato in documenti in vigore, e cambiarlo muoverebbe l'approvato di caratterizzazione per una cosa
che nessuno ha chiesto. Segnalato al committente, non fatto.

### Come si compone, in ordine

1. la pista, se c'è → `Runway`
2. **un frammento per GRUPPO d'area**, dove un gruppo sono le aree di **una** clausola della catena — con il
   suo conteggio, la sua polarità e il suo «tutte/una qualunque»
3. la personalizzata, se c'è → `Custom` (ha una preposizione sua, «in condizione», e dopo `e` si legge)

🔴 **I gruppi NON si fondono fra clausole della catena.** Fonderli vorrebbe dire scegliere un connettivo solo
per aree che vengono da righe con flag diversi: «con A o B attiva **e** C attiva» diventerebbe «con A o B e C
attive», che dice un'altra cosa. La pista e la personalizzata si fondono ancora, perché non hanno flag.

## Il tag breve resta un tag

`TransferConditionText.Display` non ha un localizzatore. Le aree si uniscono con un **simbolo**, non con una
parola: `area A + B` (tutte) · `area A / B` (una qualunque), più `⊘` se la polarità è rovescia. È la stessa
divisione dichiarata nella carta della polarità — **tag simbolico, prosa a parole** — e la stessa convenzione
compatta di `LevelFormatting` (`≤ ≥ + − ↑ ↓`).

## Le due cose che si perdono se non si guardano

- 🔴 **Il report di consistenza controlla l'area SINGOLA.** `ConsistencyReportService` fa
  `!d.AreaNames.Contains(t.ConditionAreaLabel.Trim())`: su una lista non troverebbe mai niente e direbbe
  «area fantasma» su **ogni** riga multi-area, cioè un avviso che scatta sul caso normale — che è il difetto
  già imparato due volte da questa pagina. Va **spezzato e controllato nome per nome**, e il messaggio deve
  dire **quale** dei nomi manca.
- ⚠️ **La scrittura in blocco** (`SetConditionAsync`) prende oggi una `string? areaLabel`: deve prendere la
  lista, la polarità e il «tutte/una qualunque», o dalla barra si scriverebbe una condizione che dalla riga
  non si può scrivere.

## ⚠️ L'avviso «loss of data» dello scaffolding, e perché è benigno

`dotnet ef migrations add` sul progetto **MySQL** stampa *«An operation was scaffolded that may result in the
loss of data»*. Letta, la migrazione dice:

- **`Up`**: `AlterColumn ConditionAreaLabel varchar(80) → varchar(200)` — un **allargamento**, da cui non si
  perde niente — più l'`AddColumn` della bandiera.
- **`Down`**: la stessa colonna **stretta** 200 → 80. È lì che qualcosa potrebbe troncarsi, ed è per la `Down`
  che EF avvisa.

Sul provider **SQLite** l'`AlterColumn` non compare affatto: le lunghezze `varchar(n)` lì non esistono
(affinità `TEXT`), quindi resta il solo `AddColumn`. Due migrazioni diverse per la stessa modifica del
modello, ed è il motivo per cui si emettono due volte **e si leggono**.

## Pre-flight (`docs/FEATURE-PROCESS.md`)

1. **Modello** — nessun concetto nuovo: la stessa colonna diventa una lista, come `ConditionLabel` è già una
   lista di piste. Nessuna tabella gemella: l'etichetta resta **verità denormalizzata per il display**, che
   sopravvive al rename di un'area e agli snapshot pubblicati.
2. **Dispatch** — nessuno `switch` nuovo; anzi **due template in meno** e un solo posto che compone i
   frammenti (`ClausesOf`).
3. **Ingressi + verifica** — chip con la ✕ più typeahead e «+», il pattern che gli **aeroporti della sezione**
   usano già dieci righe più su nella stessa pagina (`.xt-aptline`). Verifica **dal vivo**, in IT e in EN.
4. **Propagazione** — spariscono `RunwayAndArea` e `RunwayAndAreaInactive`: vanno tolti dal template EN, dai
   commenti che li citano come «forma dedicata» e dalla carta della polarità, che li descrive.

## Definition of Done

- [ ] Migrazione additiva: `ConditionAreaAll` (`bool NOT NULL` default `false`) + `ConditionAreaLabel` 80→200,
      per i **due** provider, **letta** prima di accettarla.
- [ ] `dotnet build Vipi.slnx -c Release --no-incremental` verde sui due TFM, 0 avvisi.
- [ ] Suite verde contando i progetti.
- [ ] 🔴 **`real-coordination.approved.txt` non si muove di un carattere**: con una sola area il testo deve
      uscire identico, e la morte di `RunwayAndArea` non deve vedersi da nessuna parte.
- [ ] Il report di consistenza prova a **trovare** un nome fantasma dentro una lista di tre, e a **non**
      lamentarsi quando ci sono tutti.
- [ ] Verifica live: due aree, i due sensi, le due polarità, IT e EN, e il selettore spento con una sola area.
- [ ] Carta, `docs/spec/modello-dati.md` §9.20, la carta della polarità e la memoria aggiornate.
