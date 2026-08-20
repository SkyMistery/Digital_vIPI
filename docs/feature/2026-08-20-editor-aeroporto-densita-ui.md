# Editor aeroporto — la tabella SID lavorabile (20 agosto 2026)

Quinto giro della famiglia, dopo [accordi](2026-08-19-accordi-densita-ui.md),
[struttura](2026-08-19-struttura-densita-ui.md), [ACC](2026-08-19-acc-admin-densita-ui.md) e
[aeroporti](2026-08-19-aeroporti-densita-ui.md): `/vsop/{acc}/airports/editor`. Le
[regole](../design/regole-ui-pagine-admin.md) la davano a 10 059px; misurata su un aeroporto vero è molto
peggio, e per una ragione sola.

## Il difetto misurato (1600×900, IT, copia del DB di sviluppo)

| Aeroporto | SID importate | Pagina | di cui sezione SID |
|---|---:|---:|---:|
| LIML | 23 | 7 135px | 3 756 |
| LIPZ | 54 | 11 033px | 7 564 |
| **LIRF** | **206** | **31 286px** | **27 085 (87%)** |

I 10 059px della ricognizione erano un aeroporto medio: **l'altezza di questa pagina è il numero di SID**.
TOC e rail erano già appiccicati (verificato: a −1 140px di scorrimento restano a schermo), quindi qui il
lavoro non era la testata — era la tabella.

Quattro difetti nella tabella delle SID importate, tre dei quali visti **solo guardando** lo screenshot:

- **Nessuna intestazione di colonna** dopo dieci righe: si scrive alla cieca in caselle identiche
  («Initial climb»? «Condition»? «Priorità»?). E aggiungere `sticky-head` non sarebbe bastato: la tabella
  stava dentro un `<div style="overflow:auto">` **senza altezza**, cioè un contenitore di scorrimento che
  non scorre mai in verticale — lì `position:sticky` non si aggancia a niente.
- **Colonne strizzate**: il nome SID andava a capo una sillaba per riga (`NE N5 A- GIL 9G`), «Type»
  diventava `CON V`, e i chip Cat./WTC si impilavano in verticale invece che in riga. Misurato:
  **128px di altezza per riga**.
- **Ultima colonna oltre il bordo** già a 1600px (`scrollWidth 984` contro `clientWidth 946`): il tasto di
  salvataggio della riga si raggiungeva solo scorrendo di lato.
- **1 415 campi e 1 714 stili in linea** in una pagina sola.

E due difetti di comportamento, trovati leggendo il codice:

- **«Toccata» e «scelta» erano lo stesso insieme** (`TouchImported` faceva `_selSid.Add`): modificare una
  cella metteva la riga fra le «selezionate», e chi trascinava per scegliere cinquanta righe da pubblicare
  le vedeva identiche a cinquanta righe modificate.
- **Il ri-prelievo scartava il lavoro in silenzio**: `ReimportSids` → `LoadAsync` rifà i buffer dal DB, e le
  righe compilate e non salvate sparivano senza un avviso.

Più uno di lingua: **19 attributi, ~10 testi visibili e 2 messaggi** scritti a mano in italiano — in inglese
la pagina era mista.

## Cosa cambia

1. **La tabella SID diventa lavorabile.** Larghezze per **classe semantica** e `table-layout:fixed` (come la
   `.sid-view` del viewer), chip in riga (`nowrap`), niente stili in linea sugli input. Misurato:
   **riga 128 → 45px**, corpo della tabella 26 353 → 9 309.
2. **Riquadro con un tetto e intestazione ferma.** `.ed-pane` con `max-height:min(64vh,660px)`: scorre il
   corpo, il `thead` resta (`top:0`, perché dentro un riquadro lo sticky è relativo al contenitore).
   Sezione SID **27 085 → 1 223px** su ogni aeroporto — l'altezza della pagina non dipende più dai dati.
3. **⤢ Larghezza piena**: indice e rail spariscono e la tabella prende tutta la pagina (a 1600: 946 → 1 484px,
   zero scorrimento orizzontale; a 1280: 637 → 1 177).
4. **Modificata ≠ scelta.** `_dirtySid` è un insieme suo: la riga toccata diventa gialla, il contatore dice
   «N modificate» e **Salva modificate** conferma tutto in un colpo — chi fallisce **resta modificato**. La
   scelta (blu) resta per «Pubblica scelte». Il salvataggio per riga sparisce: era lo stesso gesto.
5. **Il ri-prelievo chiede prima**: «C'è 1 SID modificata e non salvata: il ri-prelievo la scarta. Continuare?»
6. **Si sceglie anche da tastiera**: la cella di scelta è `role="button"`, `tabindex="0"`, `aria-pressed`, e
   Invio/Spazio fanno il clic. Prima la casella aveva `tabindex="-1"` e il gesto era solo mouse: senza mouse
   quelle righe non si potevano scegliere.
7. **Filtri e conteggi**: `.htree-search`, chip **da verificare** sempre presente e spento a zero, **chip per
   pista** con il conteggio (su LIRF: 206 righe → 3 con un clic), pill «N di TOT».
8. **Dodici paragrafi d'aiuto diventano «?»** accanto al titolo della loro sezione (testo identico, stesse
   chiavi), testata in una riga con lo stato Bozza/Pubblicata, e le due righe «da sorgente» restano come chip
   con la spiegazione nel «?».
9. **Icone e lingua**: 💾 🕒 ⚠ 🔒 → `Icon`; 🔴/🧊 restano (vocabolario di stato, come 🕒🟢 in Versioni).
   Tutte le stringhe in resx IT **ed** EN, con singolare e plurale sui contatori.
10. **Anche il pannello release** (condiviso da tutti gli editor) aveva tre righe d'aiuto scritte a mano in
    italiano: in inglese erano italiane in cinque pagine, non in una.

## Verifica

`dotnet build Vipi.slnx -c Release --no-incremental` verde su **entrambi** i TFM (0 avvisi) + `dotnet test`
verde (2 577). Poi guida live con Edge+puppeteer sulla copia del DB.

### Prima → dopo (LIRF, 1600×900, IT)

| Cosa | Prima | Dopo |
|---|---:|---:|
| Pagina | 31 286px | **4 913** (largo: 4 690) |
| Sezione SID | 27 085px | **1 223** |
| Altezza di riga | 128px | **45** (identica su tutte e 206) |
| `thead` durante lo scorrimento | via a dieci righe | **fermo** (3 000px dentro il riquadro, resta a `gap=0`) |
| Colonna finale | fuori dal bordo | **dentro** (pill a 1 270 contro un bordo a 1 279) |
| Stili in linea | 1 714 | **149** |
| Prosa sempre a schermo / «?» | 12 / 2 | **1 / 15** |
| Campi filtro `.htree-*` | 0 | **2** |

L'altezza della pagina non segue più i dati: **LIML 7 135 → 4 091**, **LIPZ 11 033 → 4 181**,
**LIRF 31 286 → 4 913**.

### Comportamento, guidato davvero

- Scritto in una cella: `1 modificate` / `0 selez.`, riga gialla. **Invio** sulla cella di scelta di un'altra
  riga: `1 modificate` / `1 selez.`, riga blu — i due stati non si confondono più.
- «Salva modificate» → «Salvata 1 SID.» (singolare giusto) e il contatore torna a `0 modificate`.
- Chip pista `07 3`: 206 righe → 3.
- Con una modifica pendente, «Re-importa SID» chiede conferma prima di scartarla.

### Assetti e zoom

1600 / 1440 / 1280 / 1024, IT ed EN, zoom 0.8 → 1.5: riga sempre 45px (38 a 0.8, 68 a 1.5), riquadro sempre
~574px, nessuno scorrimento orizzontale **di pagina** dovuto all'editor. Restano da sistemare due cose che
questo giro ha scoperto e corretto per l'editor ma che vengono da fuori:

- il **collasso a una colonna sotto i 1080px** non si applicava (`.ed-layout.with-rail` è dichiarata più in
  basso nel foglio e vinceva): a 1024 la colonna centrale restava **391px** con indice e rail ai lati. Ora
  931px;
- l'unico scorrimento orizzontale di pagina che resta a 1280/1024 è della **topbar** (`.right`, `user-chip`):
  è identico sulla home e sulle altre pagine — **non** è di questa pagina, e resta aperto.

Nel riquadro, invece, lo scorrimento orizzontale c'è ancora quando indice e rail sono a schermo (21px a 1600,
153 a 1440, 306 a 1280): è dentro un riquadro con l'intestazione ferma, e il tasto ⤢ lo azzera.
