# Aree di lavoro — una tabella sola, con la riga che si apre (16 settembre 2026)

> Stato: ✅ **ESEGUITA il 16 settembre 2026**, 📦 **in 1.28.0**. `dotnet build -c Release` verde su net8 e net10.
> **Nessuna migrazione.** Tocca solo i vSOP militari: ACC, APP e vLOA restano come sono.
> Gemella di [2026-09-09-aree-boat.md](2026-09-09-aree-boat.md), che ha dato a «Bassa quota (BOAT)» la stessa
> terna mappa + elenco + tabella.

**La richiesta del committente:** nei vSOP le working areas sono troppo lunghe — «avere sia i dettagli di
attivazione che le tabelle è troppo». Snellire mettendo tutto nella tabella.

## 1. Che cosa c'era

Sotto la mappa, due corpi uno dopo l'altro (`MilDocumentBody`, `case SectionKeys.Regulated` e `LowLevel`):

1. **l'elenco a schede** di `RegulatedAreas` — un `<details>` per area: pallino, tipo, nome, pastiglia del
   poligono di tiro, banda di quota, «senza forma»; aperto, `ActivationDetails` e `Description`;
2. **la tabella** di `MilWorkingAreas` — nome + poligono di tiro, limiti, attività (15 gettoni), nota.

Nome, banda e pastiglia stavano **due volte**. E le due frasi della scheda, misurate sul `vipi.db` vero
(241 aree in `SpecialAreas`):

| campo | valori distinti | il più frequente |
|---|---|---|
| `ActivationDetails` | **6** | 194/241 «by connected ATC or announced by NOTAM (…48hs…)» |
| `Description` | **13** | 222/241 «Reserved and designated for exclusive use by SO flights only» (in 4 grafie) |

Lunghezza massima 154 e 182 caratteri. Cioè la stessa frase, ripetuta decine di volte nello stesso documento.

🔴 **Difetto muto già presente:** le chip della mappa accendevano e spegnevano le **schede**, non le righe
della tabella. Chi filtrava vedeva l'elenco accorciarsi e la tabella restare intera.

## 2. Le tre strade proposte, e quella scelta

- **A** — tutto in tabella a sei colonne (attivazione e descrizione come colonne). Troppo larga.
- **B** — attivazione **codificata** in una pastiglia (`ATC/NOTAM`, `H24`…) con legenda, descrizione solo per
  le eccezioni. La più compatta, ma lega il codice ai testi dell'import IVAO.
- ✅ **C** — la tabella resta com'è, più una **freccetta** che apre una riga a tutta larghezza con
  attivazione e descrizione per esteso. Le schede spariscono. La più economica: nessuna tabella di
  traduzione, nessuna perdita, nessun problema di larghezza.

Decisioni del committente: la **descrizione si tiene** («non si sa mai cosa il SOD si inventa»); le chip
**filtrano le righe**; BOAT prende la stessa freccetta (**4** colonne invece di 3; le aree di lavoro **5**).

## 3. Com'è fatta

- `RegulatedAreas` — parametro **`ShowCards`** (default `true`). I vSOP militari passano `false`: resta la
  sola mappa. Con zero aree e `ShowCards=false` non scrive nemmeno «nessuna area scelta» — lo dice la
  tabella. `ScopeOf(blockKey)` diventa **pubblico e statico**: la tabella deve comporre la stessa chiave.
- `MilWorkingAreas` —
  - parametro **`Scope`** obbligatorio (`RegulatedAreas.ScopeOf(SectionKeys.Regulated | LowLevel)`);
  - contenitore `data-areacards`, righe `data-areacard`, conteggio `data-areacount` e riga `data-areaempty`:
    **gli stessi attributi delle schede**, quindi `vipi-aor.js` filtra le righe senza sapere che sono righe;
  - colonna `c-exp` con un `<button class="milarea-exp" aria-expanded aria-controls>`; `th` con `sr-only`;
  - riga gemella `tr.milarea-more[data-areamore][hidden]`, `colspan` = `Colonne` (5 o 4), con
    «Attivazione: …» e la descrizione; senza nessuna delle due dice «—» e la freccetta **resta accesa**;
  - cella del nome: pallino (colore della mappa), tipo, nome, pastiglia, «senza forma».
- `vipi-ui.js` — `wireRigheArea()`: delega unica sul clic, commuta `aria-expanded` e `hidden`. Sta qui e non in
  `vipi-aor.js` perché `vipi-ui.js` è caricato **ovunque** (l'editor non ha mappa) e la pagina del lettore è
  SSR statica.
- `vipi-aor.js` — `setCard` chiude anche la riga di dettaglio, in **tutt'e due i versi**: un'area riaccesa
  riparte chiusa.
- CSS — freccetta ruotata da `aria-expanded`; la barretta arancione dei poligoni di tiro continua sulla riga
  di dettaglio. **Stampa**: dettaglio sempre aperto, freccetta nascosta.

⚠️ Le righe di dettaglio portano `data-areamore` e **non** `data-areacard`: `syncCount` conta i
`data-areacard` per «ne vedi N su M», e contarle raddoppierebbe il totale.

## 4. Presidi

`TabellaAreeBoatTests` (Vipi.Ui.Tests): colonne 5/4, freccetta con titolo per chi ascolta, riga di dettaglio
chiusa e a tutta larghezza, attributi per il JS e riga di dettaglio **non** contata, `id` distinti fra i due
scope, pallino/tipo/senza forma, e il contratto testuale con `vipi-ui.js` e `vipi-aor.js`.
`RegulatedAreasTests` invariato: ACC, APP e vLOA disegnano ancora le schede.

## 5. Verificato dal vivo — sul pacchetto 1.28.0

✅ Con `aree-mil-verifica.js` (skill `verifica-live`) su LIBG: tabella sola e nessuna scheda; ▸ apre il
dettaglio e `aria-expanded` lo dice; chip spenta → riga **e** dettaglio via, conteggio 3→2; riaccesa → riga
chiusa; in stampa dettagli aperti e freccette nascoste; la vIPI ACC tiene le schede.

⚠️ **La foto ha trovato due difetti che la suite non vedeva**, corretti in `4fd8ed7`:
- pallino, tipo e nome **attaccati** nella cella («TSADonald East»): Razor toglie gli spazi fra gli elementi e
  la scheda li separava col `gap` di un inline-flex che la cella non ha → margini CSS;
- nella sezione BOAT senza aree, «Aree accese: 0 di 0» sopra «nessuna area scelta» → conteggio solo con
  almeno un'area (`Senza_aree_niente_conteggio_sopra_la_riga_vuota`).

▶ Resta: la freccetta nell'**editor** militare (circuito interattivo, stessa delega) non è stata guidata.
