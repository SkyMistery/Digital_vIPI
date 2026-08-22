# Editor APP ed Editor vLOA — densità (carta, 22 agosto 2026)

> Ramo `ui-trasferimenti-densita`, pagine `/services/vsop/{acc}/apps/editor` e `/services/vsop/{acc}/vloa/editor`. Seconda carta
> del giro: **la forma**. La sostanza sta nella gemella
> [`2026-08-22-editori-app-vloa-cosa-fanno.md`](2026-08-22-editori-app-vloa-cosa-fanno.md).
> Regole: [regole-ui-pagine-admin](../design/regole-ui-pagine-admin.md).

## Il punto di partenza, misurato

⚠️ La ricognizione le dava **entrambe a 900**, e lo diceva già lei stessa che il numero **non era
verificato**: era una misura **in lettura**, sui dati di sviluppo. Rimisurate su documenti veri e — come
chiede la regola 69 — **come si usano**, cioè **in modifica**:

| Pagina | Bersaglio | Lettura | **In modifica** |
|---|---|---:|---:|
| Editor APP | `LIBP_APP` (Pescara, 11 sezioni) | 2 594 | **3 540** |
| Editor vLOA | `LIBB ↔ LGGG` (9 sezioni) | 3 531 | **4 351** |

Le fasce sopra il contenuto: **104px** sull'APP (briciola 20 + testata 84) e **177px** sulla vLOA, che alla
testata aggiunge un callout da **73px** sempre a schermo.

## Il punto: non c'è niente da inventare

L'editor **ACC** è stato rifatto il 20 agosto e ha già tutto — testata in riga, «?», lock in riga, larghezza
piena, espandi/comprimi. Queste due pagine montano **lo stesso** `DocumentSectionsEditor` sotto, e sopra sono
rimaste a due generazioni fa. Il giro è **portarle sulla riga già pagata**, non disegnarne una nuova.

## Cosa cambia

1. **Testata in UNA riga** (`.st-head` + `.st-h2`, come l'editor ACC): titolo · «?» · pill di versione ·
   **lock** · ⤢ larghezza piena. Oggi sono un `h2` grande più un sottotitolo in prosa, su due righe.
2. **Il sottotitolo va nel «?»** (regole 7-8), col suo testo riusato identico e la sua chiave.
3. **Il lock sale dal rail alla testata**: è uno **stato**, non una sezione (regola 1) — e nel rail restava
   in fondo a una colonna, lontano dal titolo del documento a cui si riferisce.
4. ⚠️ **vLOA: il callout «Documento bilaterale» va nel «?»** — 73px sempre a schermo, su ogni caricamento,
   per una frase che si impara una volta sola («solo lo staff italiano modifica la parte Home»).
5. **Espandi/comprimi tutto** nel piede del TOC, con le stesse chiavi degli altri due editor (regola 68: quel
   che si fa in un editor si fa anche nell'altro). Le sezioni sono **già** `<details>` con persistenza — il
   comando semplicemente non c'era, e con 9-11 sezioni aperte l'unica cosa da fare è scorrere.
6. **Larghezza piena** (⤢, classe `.sid-wide`): qui serve alle tabelle larghe — frequenze e coordinamenti.
7. **Una sezione nascosta non paga l'altezza piena**: oggi «Configurazioni» nascosta occupa la card intera,
   solo attenuata. Nasce **chiusa**: è già esclusa dal documento, non è lì che si lavora.
8. **«+ Blocco» con menu** al posto dei quattro tasti «+ Paragrafo / + Callout / + Tabella / + Immagine» che
   compaiono sotto **ogni** sezione in modifica. ⚠️ Sta in `DocumentSectionsEditor`, **condiviso**: il
   guadagno (~40px per sezione, ~440px su undici) arriva anche all'editor ACC e all'aeroporto, e **quei due
   vanno riguardati** prima di chiudere (decisione del committente).

## Com'è andata: i numeri, misurati

Guidati con `verifica-live` su documenti veri — APP `LIBP_APP` (11 sezioni), vLOA `LIBB ↔ LGGG` (9) — a
**1600 / 1440 / 1280 / 1024**, **IT ed EN**, zoom **0.8 → 1.5**, e **in modifica**.

| Editor | Lettura | In modifica | **Compresso** |
|---|---:|---:|---:|
| APP | 2 594 → **2 403** | 3 540 → **3 350** | **1 654** |
| vLOA | 3 531 → **3 333** | 4 351 → **4 242** | **1 359** |
| ACC (già chiuso) | 4 077 → **3 988** | 5 595 → **5 144** | 1 468 |
| Aeroporto (già chiuso) | **5 173** | — | 900 |

⚠️ **Il guadagno vero è il «compresso», e su queste due il comando prima non c'era.** Un editor scorre per
mestiere: la domanda giusta non è «quanto è alto tutto aperto» ma «quanto costa arrivare alla sezione che
serve», e la risposta è 1 654 e 1 359 invece di 3 350 e 4 242. La testata resta **38px, una riga**, a tutti e
quattro gli assetti e in tutte e due le lingue.

⚠️ **E il «+ Blocco» condiviso ha restituito ~450px all'editor ACC** (5 595 → 5 144) senza che quel giro
venisse riaperto: è il segno che la scelta di toccare il componente era quella giusta, e la ragione per cui
andava rimisurato.

## Tre cose che la misura ha trovato e la carta non prevedeva

1. ⚠️ **Una sezione CHIUSA era alta 92px invece di ~50.** La riga-titolo andava a capo — titolo più fino a
   cinque comandi — e su dieci sezioni sono **900px di sole intestazioni**, pagati anche **dopo** aver
   premuto «Comprimi tutto», cioè proprio quando si è chiesto di non vederle. Da qui `.dse-head`: il titolo
   tronca (è lui la prosa), i comandi restano nomi interi.
2. ⚠️ **A 1024 la testata andava a capo per NOVE pixel.** Misurati i pezzi (regola 34), il più largo era il
   **chip del lock**: 266px per «Stai modificando · lock fino alle 21:06». Ora il chip dice l'**ora** e la
   frase intera sta nel `title` — è la stessa cura già pagata sul giro Versioni (647 → 289), applicata a
   tutti e tre gli editor.
3. **«Bozza v2» compariva due volte**, in testata e nel rail: la pill è salita, la copia nel rail è rimasta.

## Quello che ha visto solo l'occhio

- Nell'elenco AoR nuovo **quattro settori si chiamavano tutti «Athinai Radar»**: quattro righe identiche, e
  chi spuntava non sapeva quale stava togliendo. Il **callsign** accanto al nome li distingue
  (`LGGG_O_CTR`, `LGGG_CTR`, `LGGG_LO_CTR`, `LGGG_UO_CTR`). ⚠️ Il difetto c'era anche prima, coi chip — ma
  nessuno leggeva quei chip come un elenco di scelte, ed è per questo che nessuno l'aveva visto.

## E la rete che ha fermato un errore mio

⚠️ Le ancore `#editor-app` e `#editor-vloa` **esistevano già** in `GuidaPage`, puntate a sezioni che
descrivevano il **viewer**: le mie erano un secondo blocco con lo stesso `id`, e
`GuideSearchTests.Catalog_anchors_are_unique` è diventato rosso. Sostituite, non affiancate — e quelle vecchie
erano anche **datate**: «6 sezioni fisse» dove ne ho misurate undici, cioè prosa che promette il falso, lo
stesso difetto trovato nei sottotitoli degli altri sei giri.

## Cosa resta aperto

⚠️ Lo **sforo orizzontale a 1024** c'è, ed è il difetto della **topbar** già dichiarato: `div.right` arriva a
1 396px, che è **esattamente** lo `scrollWidth` della pagina, **niente dentro il `.wrap` sfora**, e il numero
è identico sulla home. Non è di queste pagine.

## Come si verifica

Guidare **entrambe in modifica** — non in lettura — a **1600 / 1440 / 1280 / 1024**, **IT ed EN**, zoom
**0.8 → 1.5**, con la skill `verifica-live`, su `LIBP_APP` e su `LIBB ↔ LGGG`:

- l'altezza in modifica, che è il numero che conta;
- l'altezza **a sezioni compresse**, che è il vero guadagno del comando nuovo;
- ⚠️ **l'editor ACC e l'editor aeroporto vanno rimisurati**: il tasto «+ Blocco» è condiviso, e i due giri
  che li hanno chiusi avevano misurato con i quattro tasti dentro;
- ⚠️ **guardare gli screenshot**: sette giri su sette, metà dei difetti non aveva un'asserzione che li
  cercasse — e su questa pagina il difetto peggiore (E1) è proprio una cosa che si *vede*, due file di chip
  identiche una sopra l'altra.

## Slice

1. `DocumentSectionsEditor`: «+ Blocco» con menu, e i due parametri che servono agli host (`TocFooter`,
   `Wide`). Riguardare editor ACC e aeroporto.
2. Testata in riga su APP e vLOA: `.st-head`, «?», pill, lock, ⤢; sottotitolo e callout bilaterale nel «?».
3. Espandi/comprimi nel piede del TOC + larghezza piena.
4. Sezione nascosta che nasce chiusa.
5. Guida (`#editor-app`, `#editor-vloa`) e `GuideSearchCatalog`.
