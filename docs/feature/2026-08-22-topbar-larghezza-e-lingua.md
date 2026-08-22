# La topbar: larghezza e lingua (carta, 22 agosto 2026)

> Ramo `ui-trasferimenti-densita`, componente `SopLayout` — il **chrome**, cioè ogni pagina dell'applicazione.
> Non è una pagina della ricognizione: è la cosa che la ricognizione ha continuato a incontrare e a rimandare.
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md); regole: [regole-ui-pagine-admin](../design/regole-ui-pagine-admin.md).

## Perché adesso

È l'**ultimo difetto aperto che si vede a schermo**, ed è quello che ha reso inutile una misura per tutto il
ramo: da sette giri lo sforo orizzontale sulle singole pagine **non è più un segnale utile**, perché sotto
c'è sempre questo. Chiuderlo restituisce anche quello.

## Cosa ho trovato, misurato

### ⚠️ T1 — La barra ha una larghezza minima incomprimibile di 1385px

| Larghezza | `scrollWidth` | Eccesso |
|---|---:|---:|
| 1600 | 1600 | **0** — sta |
| 1280 | 1385 | 105px |
| 1024 | **1385** | **361px** |

⚠️ Il numero **non cambia** fra 1280 e 1024: la barra ha smesso di comprimersi. Da lì in giù la pagina
scorre in orizzontale e basta — e non è la pagina a sforare: **niente dentro il `.wrap` esce dal bordo**,
verificato su editor, home e vIPI ACC. In inglese sono 1395.

I pezzi a 1024: `brand` 81 · `acc-nav` 262 · `top-search` 140 · **`.right` 810**.

Dentro `.right`, misurato pezzo per pezzo:

| px | Pezzo | Cosa dice |
|---:|---|---|
| 148 | `staff-badge` | «IT-AOA1 · IT-T03» |
| 116 | `zoom-ctrl` | «− 100% +» |
| 105 | `live-badge` | «Live · non connesso» |
| 101 | `editor-btn` | «Incarichi» |
| 87 | `editor-btn` | «Editor» |
| 79 | `user-chip` | «U7» |
| 38 ×3 | icone | guida, permessi, logout |

### ⚠️ T2 — Non c'è NESSUNA media query sulla topbar

Zero. In tutto il foglio non esiste una sola regola che adatti la barra a una larghezza. Il `brand` si
accorcia da 202 a 81 solo perché il flex lo comprime, non perché qualcuno l'abbia deciso. **Non è un difetto
da limare: è una cosa che non è mai stata fatta.**

### ⚠️ T3 — Quindici stringhe cablate in italiano, su OGNI pagina

`title`, `aria-label` e `placeholder` della topbar sono scritti a mano in italiano:

> «Guida» · «Posizione staff» · «Riduci zoom» · «Aumenta zoom» · «Zoom pagina» · «Reimposta a 100%» ·
> «Cerca» · «Cerca CoP, FIX, callsign, frequenze…» · «Logout» · «Login IVAO» · «Home vIPI / vLOA»

È il difetto E2 del giro editor (il «?» cablato tre volte), ma qui sta sul **chrome**: non su tre pagine, su
**tutte** — comprese quelle pubbliche, che un pilota straniero legge in inglese. E il `placeholder` della
ricerca è la stringa più visibile dell'applicazione.

⚠️ La parte peggiore sono gli **`aria-label`**: chi usa un lettore di schermo in inglese si sente leggere
etichette italiane, e lì non c'è un contesto visivo che compensi.

## Cosa cambia

**T3 — le stringhe.** Quindici chiavi IT+EN. Il testo si riusa identico dove esiste già una chiave buona
(`Common_Home`, `Nav_Tasks`…), il resto nasce nuovo.

**T1/T2 — tre scaglioni, per priorità** (decisione del committente). Ogni scaglione toglie **ciò che serve
meno**, e ⚠️ **niente sparisce**: quello che si comprime resta raggiungibile.

| Sotto | Cosa cede | Perché quello | Guadagno |
|---|---|---|---:|
| ~1400 | il **badge staff** perde il testo, resta l'icona | «chi sei» lo sai già: è l'unico pezzo che non è né un comando né uno stato che cambia | −110px |
| ~1200 | «Editor» e «Incarichi» diventano **sole icone** | sono comandi frequenti, quindi restano — ma il loro nome si impara al primo uso, e il `title` lo ridà | −120px |
| ~1000 | la **ricerca** diventa un'icona che apre il campo | è il pezzo più largo che non serve a ogni pagina; il campo si apre a piena riga quando lo si chiede | −105px |

**1385 → ~1010px**: sta a 1024 con dieci pixel di margine.

⚠️ **Gli `aria-label` restano interi anche quando il testo sparisce**: un tasto che diventa un'icona non
diventa muto — è la stessa regola del `title` sui tasti accorciati (regola 33), qui applicata
all'accessibilità.

## Fuori ambito, dichiarato

- **`acc-nav` non si tocca** (262px, quattro ACC): sono la navigazione primaria della divisione, e il numero
  di ACC non cresce. Comprimerli sarebbe togliere l'unica cosa per cui la barra esiste.
- **Sotto i 1000px** non si va: non è un assetto che il progetto supporta (la ricognizione si ferma a 1024) e
  fingere un layout telefono sarebbe inventare un requisito.
- Il **`.wrap`** delle pagine non c'entra: è già verificato che non sfora.

## Com'è andata

`scrollWidth == clientWidth` su **32 combinazioni**: 4 famiglie di pagina (pubblica, viewer, editor, admin)
× 4 assetti × 2 lingue. Barra 62px ovunque, 13 comandi su 13 raggiungibili, nessun `aria-label` mancante,
nessuna stringa italiana in pagina inglese.

Gli scaglioni sono finiti a **1500** (spazio più stretto + badge staff a icona) e **1300** (marchio senza
sottotitolo, «Editor»/«Incarichi» a icone, ricerca a icona, badge live a pallino). 1385 → **sta a 1024**.

## Tre lezioni, pagate una per volta

1. ⚠️ **Una media query si scrive sopra l'assetto da far stare, non sotto.** La prima soglia era 1000: a
   **1024 non scattava affatto**, e la barra restava a 1161. Sembra ovvio scritto qui; non lo era mentre
   guardavo il numero sbagliato.
2. ⚠️ **Il `nowrap` non ha creato un difetto: l'ha rivelato.** A 1440 la barra sembrava stare, e ci stava
   andando a capo **dentro i suoi pezzi** — marchio e badge live spezzati su due righe. `scrollWidth` misura
   il bordo, non l'interno: **una barra che sta perché il suo contenuto si spezza non sta**. Vietato il wrap,
   il difetto vero è venuto fuori (1513px minimi contro 1440) e la soglia è salita a 1500.
3. ⚠️ **Uno spazio libero non è spazio disponibile finché non ci si è messo dentro quello che dovrebbe
   starci.** Misurati **306px liberi** a 1280, avevo abbassato la soglia a 1200 per tenere la ricerca aperta:
   quel numero era preso con la ricerca **chiusa**, e riaprendola si tornava a sforare di 31px. Tornato a 1300.

## E un difetto preesistente, trovato guardando

Il **segnaposto della ricerca era troncato a OGNI assetto**, anche a 1600 («Cerca Co…»): `.right` ha
`margin-left:auto`, e in flexbox **i margini `auto` assorbono lo spazio libero prima che `flex-grow` lo
distribuisca**. Il campo restava quindi al suo minimo per sempre. Serve una `flex-basis` dichiarata —
⚠️ ma **non** `flex-shrink:0`: provato, e a 1600 e 1440 faceva sforare di 80-104px. Il campo deve poter
cedere; è il **testo** che si accorcia.

## Come si verifica

⚠️ La topbar sta su **ogni** pagina: si guida un campione che copra le famiglie — una pubblica (`/services/vsop`), un
viewer, un editor, una admin — a **1600 / 1440 / 1280 / 1024**, **IT ed EN**, e si controlla:

- `scrollWidth == clientWidth` a ogni assetto (è **questa** la misura del giro, non un'altezza);
- che a ogni scaglione la barra resti su **una riga** e i comandi restino cliccabili;
- che gli **`aria-label`** ci siano ancora dove il testo è sparito;
- ⚠️ che nessuna pagina abbia **perso** il suo comando: il rischio di un layout a scaglioni è nascondere
  qualcosa a chi ne ha bisogno, e questo non lo trova una misura — si guarda.

## Slice

1. T3: le quindici stringhe in chiavi IT+EN.
2. T1/T2: i tre scaglioni, e gli `aria-label` che restano.
3. Verifica guidata sulle quattro famiglie di pagina, IT ed EN.
