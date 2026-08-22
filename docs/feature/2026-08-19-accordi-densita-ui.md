# Accordi di coordinamento — densità della pagina (19 agosto 2026)

Round di sola **forma**: nessun modello, nessuna rotta, nessuna migrazione. La pagina
`/services/vsop/admin/transfers` spendeva in fasce fisse l'altezza che serve alle clausole, e alcune cose si
muovevano sotto il puntatore mentre si lavorava.

## Il perno: sopra le tre colonne, l'altezza è tabella

Il riquadro di lavoro è alto **schermo meno ciò che gli sta sopra**, e la misura la fa `vipiFitViewport`
(`vipi-ui.js`) a ogni render — non è esprimibile in CSS. Conseguenza diretta: ogni fascia tolta in testa
diventa clausole visibili senza scorrere. Erano cinque (briciole, testata, lock, ACC, filtri); ora sono tre.

## Cosa è cambiato

1. **Testata in una riga**: titolo · «?» · barra del lock. Il sottotitolo — tre righe di prosa sempre a
   schermo — è sparito perché diceva quasi parola per parola ciò che il «?» dice al clic. La frase che aveva
   in più (risalita della gerarchia fino al primo ente online, altrimenti UNICOM) è migrata in
   `Xfer_HelpBody`; la chiave `Xfer_Subtitle` è stata rimossa da **entrambi** i resx.
   I margini di fascia della `.lockbar` si azzerano nel CSS della testata, **non** nel componente: struttura e
   nuovo documento la usano ancora come fascia a sé.
2. **Una barra sola**: ACC · filtri · vista, letta da sinistra a destra come si lavora (ambito → cosa cerco →
   come guardo). I contatori «N accordi · N clausole» non hanno più una casella: in albero il navigatore conta
   già gli accordi, e i due totali stanno nel `title` del chip dell'ACC aperta.
3. **I diagnostici non si spostano**: «Da rivedere» e «Lacune» ci sono sempre, spenti a zero. Prima
   comparivano col contatore e spingevano vista e ordinamento mentre ci si mirava. Corollario: il cruscotto
   delle lacune si chiude da sé all'ultima lacuna, altrimenti resterebbe aperto con il tasto ormai spento.
4. **Anteprima frase** come tasto-icona `¶`: era una casella con etichetta, il pezzo più largo della barra
   per il peso che ha. Con il suo «?» accanto.
5. **Form della sezione**: gli aeroporti su una riga propria (in coda), le tre cose brevi in prima riga.
   Chip e comandi d'aggiunta su una riga sola.
6. **Colonne fisse** nelle tabelle di clausole (`table-layout:fixed` + colgroup, due profili: albero ed
   elenco). Si scrive **in cella**: un bersaglio che cambia posto fra due tabelle affiancate va ricercato ogni
   volta.
7. **Intestazioni che restano**: in elenco il `thead`, in albero la testata del **verso**.

## Le tre trappole di questo round

- **Specificità**: `.struct .inline-form .field{flex:1}` vale tre classi. Una regola nuova da due classi
  perde **in silenzio** — il form sembrava non essere stato toccato. Si è visto solo misurando le quote dei
  campi in pagina.
- **Colonne fisse = niente più negoziazione**: la colonna delle azioni si allargava da sé, e a 118px fissi
  in elenco il quarto tasto finiva oltre il bordo. Quanto serve **si misura** (128px in albero, 163px in
  elenco), non si stima. Stesso ragionamento per i CoP lunghi (`overflow-wrap`) e per le colonne di contesto
  (puntini + valore intero nel `title`: un callsign troncato è un altro callsign).
- **Due elementi appiccicati alla stessa quota si sovrappongono**: in albero, con la testata del verso
  fissata in cima, il `thead` della sua tabella le passa sotto mentre quel blocco è al bordo. È il prezzo
  accettato: il verso non si ricostruisce guardando la riga, i nomi di tre colonne sì. L'alternativa —
  `thead` a `top:` pari all'altezza della testata — richiederebbe una misura in JS perché quell'altezza
  cambia quando la testata va a capo.

## Verifica

Guidata su copia del DB (skill `verifica-live`, Edge+puppeteer, porta 5035): cinque tabelle di clausole nello
stesso accordo con colonne identiche `26·26·312·184·238·132`; in elenco `26·26·207·207·191·127·286·191·162·168`
e intestazione ancora visibile a `scrollTop 500`; form sezione su due righe (campi a 468/481/469, aeroporti a
544 largo 902); «?» chiuso prima del clic e aperto dopo; nessun errore di console, di circuito o HTTP ≥ 400.

Suite: verde su entrambi i TFM. `dotnet build Vipi.slnx -c Release --no-incremental` senza avvisi.

## Densità delle tabelle (secondo giro, stesso giorno)

Misurato **prima** di scegliere, su un accordo vero a 1680×1050: una clausola costava **152 px**.

| pezzo | prima | dopo |
|---|---|---|
| riga di dati | 43 | **35** |
| anteprima frase | 55 ⟵ più alta del dato che commenta | **32** |
| intestazione colonne | 38 | **26** |
| testata del verso | 51 (gonfiata dai suoi tasti) | **31** |
| margine fra blocchi | 14 | 6 |
| **totale per clausola** | **152** | **104** |

Un terzo in meno: da 5 a 8 clausole in un riquadro da 800 px, e in elenco da 9 a 12.

Due decisioni prese e da non ribaltare per distrazione:

- **Nessun interruttore di densità.** Una pagina che serve a confrontare righe non ha una modalità «ariosa»
  che valga la barra che costerebbe — proprio ora che di barra ne è rimasta una sola.
- **L'intestazione di colonna resta in ogni blocco di verso.** Sarebbe eliminabile (le colonne sono identiche
  per costruzione), ma sta accanto alle righe che descrive: toglierla significa non averla più dopo il primo
  blocco, in accordi che arrivano a sei sezioni.

⚠️ **Il bersaglio della scrittura in cella non dimagrisce con la riga.** `.xt-cellclick` ha un padding
negativo che ricopre esattamente quello della cella: misurato 26 px su una riga di 35, dove un bersaglio
«alto quanto il testo» sarebbe sceso sotto i 20. È il gesto principale della pagina. I due numeri sono la
stessa misura scritta due volte — se cambia il padding di `.xfe-table td`, vanno cambiati con lui.
Verificato anche che aprire una cella non fa saltare la riga: 35 prima, 35 dopo.

### La trappola, di nuovo

`.xfe-table td{padding:3px 9px}` **non faceva niente**: `.res-table td{padding:7px 10px}` vive 350 righe più
in basso con la **stessa specificità** e vinceva per posizione. Invisibile perché i due valori verticali
coincidevano — 7px e 7px — quindi il difetto è nato nel momento in cui li ho resi diversi. Si è visto solo
leggendo il padding **calcolato** a schermo: la riga restava 43 px con la regola nuova già servita in pagina.

È la **seconda volta in un giorno** che una regola di questa pagina perde in silenzio contro una più generica
scritta dopo (la prima fu `.struct .inline-form .field`). Regola pratica per questo foglio: una regola che
riguarda solo l'editor si scrive con `.struct` davanti, e la si **misura**, non la si guarda.

## Coda: il guasto emerso durante il round (stesso giorno)

Rendendo visibili due ACC nascosti, la pagina restava su **titolo e riga ACC**. Non era la ripulitura: era
una parola nel registro dei servizi.

`IStationResolver` è `AddScoped`, e in **Blazor Server** «scoped» non vuol dire «per richiesta» ma **per
circuito** — la cache dell'elenco ACC dura quanto la sessione SPA, cioè ore. Il chrome (`SopLayout`) è SSR
con uno scope per richiesta e infatti si aggiornava subito: nella stessa schermata il menu in alto mostrava
sei ACC e la barra della pagina sette. Se l'ACC che si stava aprendo era proprio uno dei nuovi, non si
risolveva e la pagina restava vuota — senza dire perché.

Rimedio in tre pezzi:

1. `IStationCatalogVersion`, **singleton di processo**: chi scrive alza il contatore (`AccAdminService`:
   nascondi ACC, import), chi legge lo confronta prima di usare la cache. Singleton perché nascondere un ACC
   lo nasconde a **tutte** le sessioni aperte, non solo a chi l'ha fatto.
2. La pagina ora **distingue** «non hai scelto» da «quello che avevi non c'è più», e lo scrive.
3. `SelectAcc` confrontava il codice richiesto con `_accCode` — che in quello stato è già uguale, mentre
   `_acc` è null: il chip di quel codice era **morto**, cioè proprio l'unico che si prova a premere.

Da ricordare: **una cache scoped in Blazor Server è una cache di sessione.** Ogni `AddScoped` che memorizza
qualcosa letto dal DB va guardato con questo occhio.
