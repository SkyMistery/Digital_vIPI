# Editor APP ed Editor vLOA — densità (carta, 22 agosto 2026)

> Ramo `ui-trasferimenti-densita`, pagine `/vsop/{acc}/apps/editor` e `/vsop/{acc}/vloa/editor`. Seconda carta
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
