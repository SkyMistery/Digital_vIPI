# L'Indice dell'AIP espanso, rifatto (1 novembre 2026)

Filone Sector Lab. Segue le carte [«Lettura e consegna»](2026-10-22-lettura-e-consegna.md) (la prima pagina rifatta) e
[«Gli aeroporti nel Lab»](2026-10-27-aeroporti-nel-lab.md) (la pagina dello scalo). **Approvata il 1 novembre, tutte e
quattro le regole come proposte (§4); fatta lo stesso giorno (§6).**

## §0 — La domanda

Il committente, 27 ottobre: «Dobbiamo continuare con la revisione grafica, come abbiamo fatto per Lettura e consegna: ora
anche per l'Indice.» Gli aeroporti sono venuti prima (S1-S5); il 1 novembre, fra i lavori da fare mentre aspetta di
provare, ha scelto anche questa.

## §1 — Com'è oggi (misurato sul banco, 1 novembre, finestra di 1400 px)

Quattro colonne fisse, da sinistra:

| Colonna | Larghezza | Che cosa |
|---|---|---|
| Indice | ~410 px | in testa ciclo, «in vigore…», «Lavora sul 2610», la casella «Mostra l'AIP espanso sulla mappa», il menu e «Aggiungi», la ricerca; sotto l'albero ENR 2 · ENR 3 · ENR 4 · ENR 5 · AD 2 (97 scali) · AD 3 |
| Scheda | ~430 px | la voce scelta, o la pagina dello scalo con le sue sei schede, o la carta posata |
| Strati | ~310 px | Coste e confini, AIP espanso e i suoi 27 tipi (e, col ground, gli strati del sector) |
| Mappa | **~430 px** | quel che resta |

Quel che si è visto facendo le prove A75-A98:
- **La mappa è la colonna più stretta**, proprio nella pagina dove serve: la carta posata (A77, A89-A90) e il ridisegno
  (A86) si fanno sulla mappa. Per guardarle ho dovuto allargare la mappa a mano.
- **Gli Strati stanno sempre aperti** ma si toccano di rado: 27 righe dell'AIP, più «Piste» due volte quando c'è il
  ground (un tipo dentro «Disegni .geo» e lo strato dei `.rw`).
- **La pagina dello scalo** (sei schede, la carta, la posa, il ridisegno) sta nella colonna della scheda: 430 px per una
  tabella delle carte di 5 colonne o la tabella degli stand.
- **La testata dell'Indice** occupa ~200 px di altezza prima dell'albero.
- Quando si lavora su uno scalo, l'albero a sinistra serve solo per tornare indietro.

## §2 — Le regole (proposte)

- **R1 — Gli Strati sopra la mappa.** Un tasto «Strati» nell'angolo della mappa apre il pannello sopra di lei, a destra;
  si chiude da solo quando si clicca la mappa. La mappa guadagna i ~310 px. Gli strati del ground hanno un titolo loro
  («Ground del sector»), così «Piste» non compare due volte nello stesso elenco.
- **R2 — L'Indice si stringe quando lavori su una voce.** Aperta una voce o uno scalo, l'Indice diventa una striscia di
  ~3 rem con le parti (ENR 2 … AD 3) in verticale e «‹ Indice» in cima; un clic lo riapre. La scheda prende il suo
  posto: con R1 e R2 la scheda arriva a ~650 px e la mappa a ~700.
- **R3 — La testata in una riga.** «AIP espanso · ciclo 2611 · in vigore dal 29/10 · [Lavora sul 2610] · ☑ sulla mappa ·
  [Aggiungi ▾]», e sotto la ricerca. Le frasi lunghe («13524 forme (2 attese su 789 senza il punto nel sector): i tipi si
  accendono negli Strati») vanno nel titolo della casella, al passaggio del mouse.
- **R4 — Le parti come schede.** Al posto del primo livello dell'albero, sei schede in riga: ENR 2 · ENR 3 · ENR 4 · ENR 5
  · AD 2 · AD 3, ognuna col suo numero. Dentro, l'albero di oggi da lì in giù. Con una ricerca le schede mostrano quante
  voci trovano ciascuna («ELKAP»: ENR 3 1 · ENR 4 1 · AD 2 9).

Non cambia: che cosa c'è nell'Indice, le schede delle voci, la pagina dello scalo dentro, i gesti (Correggi, Togli,
Aggiungi), la mappa e i suoi colori.

## §3 — Il disegno

Oggi:

    ┌ Indice ──────────────┬ Scheda ──────────────┬ Strati ────────┬ Mappa ──────┐
    │ AIP espanso · 2611   │ LIRN · NAPOLI        │ ☑ Coste    129 │             │
    │ in vigore dal 29/10  │ ARP … · 294 FT       │ ☑ AIP   13 524 │             │
    │ [Lavora sul 2610]    │ Piste · Procedure ·  │  ☑ ATZ      94 │             │
    │ ☑ Mostra l'AIP … (2  │ Carte · Frequenze ·  │  ☑ Altre    24 │             │
    │   righe di testo)    │ Ground · Testo AD 2  │  ☑ Attese  789 │             │
    │ [un'area ▾][Aggiungi]│ ─────────────────────│  …27 righe…    │             │
    │ [Cerca una voce…]    │ RWY 24               │                │             │
    │ ▾ ENR 2 …        330 │  ☑ SID 4             │                │             │
    │ ▾ ENR 3 …        773 │  ☑ STAR 10           │                │             │
    │ …                    │ …                    │                │             │
    └──── ~410 px ─────────┴──── ~430 px ─────────┴─── ~310 px ────┴── ~430 px ──┘

Proposta, con uno scalo aperto:

    ┌──┬ LIRN · NAPOLI Capodichino ─────────────────┬ Mappa ─────────────────────────── [Strati ▾] ┐
    │‹ │ ARP 40°53'04"N 014°17'27"E · 294 FT · 4° E │                                ┌ Strati ────┐│
    │  │ [Apri il PDF] [Solo lo scalo sulla mappa]  │                                │☑ Coste     ││
    │E2│ Piste · Procedure 78 · Carte · IAP 7/7 ·   │                                │☑ AIP       ││
    │E3│ Frequenze · Ground · Testo AD 2            │                                │ ☑ ATZ   94 ││
    │E4│ ────────────────────────────────────────── │                                │ …          ││
    │E5│ Carta              Pagina   Data   Il Lab  │                                │Ground del  ││
    │A2│ AERODROME CHART    2-1      10/07  posata  │                                │sector      ││
    │A3│ …                                          │                                └────────────┘│
    └──┴──────────── ~650 px ────────────────────┴───────────────── ~700 px ─────────────────────┘

Proposta, senza una voce aperta (l'Indice largo, la mappa sempre ~700 px):

    ┌ AIP espanso · 2611 · in vigore dal 29/10 · [Lavora sul 2610] · ☑ sulla mappa · [Aggiungi ▾] ┬ Mappa ── [Strati ▾] ┐
    │ [Cerca una voce…]                                                                          │                     │
    │ [ENR 2 330] [ENR 3 773] [ENR 4 1299] [ENR 5 7067] [AD 2 97 scali] [AD 3 10]               │                     │
    │ ▾ ENR 2.1 FIR, UIR, TMA e CTA 290                                                         │                     │
    │   ▸ UIR 6 · ▸ TMA 27 · …                                                                   │                     │
    └────────────────────────────────────── ~650 px ─────────────────────────────────────────┴───── ~700 px ───────┘

## §4 — Domande per il committente

1. **R1** Gli Strati sopra la mappa, a scomparsa: va bene, o li vuoi sempre aperti (magari più stretti)?
2. **R2** L'Indice che si stringe quando apri una voce: va bene, o preferisci che resti sempre largo?
3. **R4** Le parti come schede in riga al posto del primo livello dell'albero?
4. Altro che ti dà fastidio nell'Indice e che qui non c'è?

**Risposte del committente (1 novembre)**: R1 sopra la mappa; R2 si stringe; R4 sì, schede in riga; R3 come scritta,
nient'altro da aggiungere.

## §5 — Il lavoro (dopo il sì)

Una fetta per regola, nell'ordine R1, R3, R2, R4; ognuna provata a schermo sul banco a 1400 px e coi test bUnit della
pagina (le colonne, il tasto Strati, l'Indice stretto e «‹ Indice»). Le prove a mano nuove in `SectorLab-prova\PROVE.md`.
Tocca solo il Lab (`Vipi.SectorLab.Ui`: la pagina e il suo CSS).

## §6 — Fatto (1 novembre)

- **R1** (`Home.razor`): nella pagina Indice la colonna degli Strati diventa un pannello sopra la mappa, in alto a destra
  (Leaflet lì non ha controlli), aperto dal tasto «Strati ▾» e chiuso da un clic sulla mappa. Senza divisore: la sua
  larghezza resta quella della colonna (`--lab-l-strati`). Nelle altre pagine con la mappa (Sector, Confronti) gli Strati
  restano una colonna: la carta parla dell'Indice. Gli strati del ground (S5) stanno sotto il titolo «Ground del sector»,
  e lo strato dei `.rw` si chiama «Piste (.rw)», per non confondersi col genere «Piste» dei `.geo`.
- **R3** (`IndiceDellAipVista`): la testata è una riga che va a capo solo se la colonna è stretta. La frase sullo strato
  («ciclo 2611, 13 524 forme (…): i tipi si accendono negli Strati») è il titolo della casella «sulla mappa». «Aggiungi» è
  un menu solo («Aggiungi ▾»): scelto il tipo, si apre il suo modulo (prima un menu e un tasto).
- **R2** (`PannelliDellaPagina`): senza una voce l'Indice è largo (`--lab-l-indice-largo`, 40rem) e la scheda non c'è; con
  una voce o uno scalo è una striscia di 3rem — «‹ Indice» e le parti (E2 … A3, quella della voce in evidenza) — e la
  scheda prende il suo posto (`--lab-l-scheda-aip`, 40rem, la stessa per una voce e per uno scalo: prima lo scalo aveva
  una larghezza che il divisore non toccava). Riaperto accanto alla scheda, «‹» in testata lo stringe di nuovo; una voce
  nuova, dall'Indice o dalla mappa, lo stringe da sé. Nella finestra dei pannelli (due schermi) la scheda resta sempre.
  L'Indice intero resta nella pagina, nascosto: riaprendolo rami aperti e ricerca sono quelli di prima.
- **R4**: le parti sono schede in riga col loro numero («AD 2 97 scali»); cercando ci sono solo le parti che trovano
  qualcosa, ognuna col suo numero, e si apre la scelta se c'è ancora, se no la prima. Scegliendo una voce (anche dalla
  mappa) la scheda aperta diventa la sua parte. Le parti non scelte restano nella pagina con `hidden`: cambiare scheda non
  ridisegna migliaia di righe.

Test: `AipNellaBarraTests` (l'Indice largo, le schede, la striscia, «‹ Indice», «‹», la voce dalla mappa; cercando le
schede contano), `TreMappeTests` (gli Strati sopra la mappa, il clic che li chiude, il gruppo del ground). Prove a mano
A103-A106.
