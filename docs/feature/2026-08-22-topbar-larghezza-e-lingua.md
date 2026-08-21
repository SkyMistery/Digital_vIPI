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

## Come si verifica

⚠️ La topbar sta su **ogni** pagina: si guida un campione che copra le famiglie — una pubblica (`/vsop`), un
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
