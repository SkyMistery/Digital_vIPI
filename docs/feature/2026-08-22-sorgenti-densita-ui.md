# Sorgenti — densità UI (carta, 22 agosto 2026)

> Ramo `ui-trasferimenti-densita`, pagina `/vsop/admin/sorgenti`. Seconda carta del giro: **la forma**.
> La sostanza sta nella gemella [`2026-08-22-sorgenti-cosa-fa-la-policy.md`](2026-08-22-sorgenti-cosa-fa-la-policy.md).
> Undicesima pagina del giro. Regole: [regole-ui-pagine-admin](../design/regole-ui-pagine-admin.md).

## Il numero di partenza

**1 252px** (ricognizione del 22 agosto, dopo la barra admin). ⚠️ Va **rimisurato prima di toccare**, con la
tabella degli stati **piena**: nel DB di sviluppo può avere zero righe, e il numero a tabella vuota è la
trappola che su Permessi ha detto 1 346 dove c'erano 2 449. Lo stato pieno si costruisce **scrivendo nel DB
della copia mentre l'app gira** (4 categorie + una con `LastError`), non aspettando che gli import girino.

Bersaglio: **900** — cioè il viewport: la pagina non scorre, e qui è alla portata perché l'altezza **non
dipende dai dati** (cinque categorie sono cinque per sempre).

## Da dove vengono i 350px di troppo

| Pezzo | Oggi | Perché se ne va |
|---|---|---|
| Sottotitolo in fascia | 1 riga sempre a schermo | dice a tutti, ogni volta, ciò che il «?» dice a chi chiede (regola 7) |
| `Sorg_Intro`, paragrafo di prosa | 3 righe | è la spiegazione del meccanismo: nel «?» e nella Guida |
| Due callout in fascia (`_msg`, `_ok`) | ~64px quando appaiono, e spingono giù la tabella | `.st-msg` accanto al tasto che li ha generati |
| Titolo + aiuto del secondo blocco | h3 + 2 righe | il secondo blocco **sparisce**: vedi sotto |
| `margin-top:18px` fra due blocchi da 720px | 18px + due bordi | un pannello solo |
| Tasto «Salva» in coda al primo blocco | riga + margine | sale in testata (regola 4: il comando sta dove si guarda) |

## La decisione di forma: **una tabella, non due**

Oggi la pagina ha due tabelle che parlano **delle stesse cinque cose** con **due vocabolari**: sopra
«Settori», sotto `AirportSector`; sopra «da sorgente / manuale», sotto «ok / errore». Chi legge deve fare a
mente il join fra le due — ed è un join che sbaglia, perché sotto compare anche una riga che import non è
(`SpecialAreaForeignOptOut`, vedi la carta della sostanza).

La domanda della pagina è una sola per riga: **da dove viene questo dato, quando è arrivato l'ultima volta,
e dove lo si tocca**. Quindi una riga per categoria e quattro colonne:

```
Categoria              Provenienza          Ultimo aggiornamento        Dove si modifica
─────────────────────────────────────────────────────────────────────────────────────────
ACC (anagrafica)       🔒 da sorgente       oggi 04:12 · fra 19h        Struttura
Transition Altitude    [x] da sorgente      su richiesta                Editor aeroporto
Piste                  [ ] manuale          su richiesta                Editor aeroporto
Settori                [x] da sorgente      oggi 04:15 · fra 19h        Struttura · Editor aeroporto
SID                    [x] da sorgente      ⚠ errore: 404 su LIRF       Editor aeroporto
Aree regolamentate     [ ] ❄ congelate      esclusa                     ACC
```

Sei righe (le cinque categorie **più** l'anagrafica ACC, che oggi appare solo in basso come riga di stato
senza nessuno che la spieghi). La descrizione della categoria resta sotto il nome in `muted` 11px: **non è
prosa d'aiuto, è il dato** («Ident, lunghezza e bearing delle piste» dice *quali colonne* la sorgente possiede).

Conseguenze già verificate in altre pagine del giro:
- la colonna «Provenienza» e la spunta erano **la stessa informazione scritta due volte** (spunta accesa ⇔
  «🔒 da sorgente»): restano **una** cella, la spunta con accanto la parola;
- «importa dalla sorgente» era ripetuto **cinque volte**, una per riga: l'etichetta sale nell'intestazione
  di colonna e le celle portano solo lo stato;
- ⚠️ qui il `thead` appiccicato **non** serve e non si mette (sei righe, la ricognizione lo dice già):
  `.res-table` senza `sticky-head`.

## Testata in una riga

`titolo · pill «modificato» · «?» · Salva`, con `.doc-head.st-head` e `.st-h2` come le altre dieci pagine.
La pill compare **solo** quando c'è una differenza rispetto al DB e dice quante categorie cambiano
(«2 modificate»), così «Salva» non è mai un tasto che non si sa se serve. `disabled` quando nulla è cambiato.

Sotto la tabella, una riga sola in `muted`: «**deciso da** Mario Rossi **il** 2026-07-14 09:12» — oppure la
frase della slice 4 della carta sostanza quando quella policy non l'ha mai salvata nessuno.

## Il salvataggio dice cosa sta per succedere

Cambiare una spunta non è come cambiare un filtro: **decide chi vince al prossimo import**, e nelle due
direzioni le conseguenze non sono simmetriche.

- **da sorgente → manuale**: i dati smettono di aggiornarsi e diventano modificabili. Reversibile.
- ⚠️ **manuale → da sorgente**: al prossimo import **il lavoro fatto a mano viene sovrascritto**. È l'unica
  direzione distruttiva della pagina, ed è quella che oggi si prende con un clic e un tasto grigio.

Quindi `InlineConfirm` (già usato su Versioni e Confinanti — niente `confirm` nativo, che blocca il circuito
Blazor) con dentro **la frase del cambio**, e il tasto in **rosso** quando almeno una categoria va verso la
sorgente:

> Passano alla sorgente: SID — al prossimo import quanto è stato scritto a mano sarà sostituito.
> Passano a manuale: Settori — non si aggiorneranno più da sé e diventano modificabili.

⚠️ Raggruppate per **verso**, non una frase per categoria: la prima stesura scriveva la spiegazione per intero
accanto a ogni nome, e con due sole categorie era già un muro che si legge due volte identico. Le frasi sono
**le stesse** che finiscono nel registro di audit (slice 3 della carta sostanza): un vocabolario solo, come
`AuditNarrator` per gli eventi.

## Prosa: «?» e Guida

- `HelpHint Href="/vsop/guida#admin-sorgenti"` in testata, con dentro il sottotitolo e `Sorg_Intro` (i «?» si
  aprono **a clic**, mai al passaggio del mouse).
- Nuova sezione `#admin-sorgenti` nella Guida (IT **ed** EN) con: cos'è la policy opt-out, cosa vuol dire
  «congelate» per le aree, quali categorie hanno un giro automatico e quali no, e il segnaposto
  `SpecialAreaForeignOptOut` che nel report **non** si vede più.
- Voce in `GuideSearchCatalog` (oggi «sorgenti» è solo una parola dentro la voce «Aree admin»).

## Come lo verifico

Skill `verifica-live`, su **copia** del DB, `Vipi.Host` pubblicato nello scratchpad e avviato **dalla sua
cartella** (la content root è la cwd; da altrove la pagina esce senza CSS e la misura è falsa), porta libera
verificata con `Get-NetTCPConnection`, sentinella `nav.admin-nav` in pagina prima di credere a un numero.

Passi: **1600 / 1440 / 1280 / 1024**, **IT ed EN** (`Accept-Language: it-IT`, il browser headless parla la
lingua del sistema), **zoom 0.8 → 1.5** con `window.vipiSetZoom(z)` (mai `style.zoom` a mano: non scatta il
`resize`). Stati da fotografare, non solo da produrre:

1. tutte importate (default), 2. due escluse, 3. una riga di stato in errore con messaggio lungo,
4. nessuno stato (DB nuovo), 5. `InlineConfirm` aperto con tre righe di frase, 6. `.st-msg` di errore.

⚠️ E poi **guardarli**: metà dei difetti di questo giro non aveva un'asserzione che li cercasse, e il
peggiore (`.sector-pick` che significava due cose) l'ha visto un umano. Lo sforo **orizzontale** non è un
segnale utile finché la topbar non è sistemata (`div.right` 1 385px dentro 1 280 su tutte le pagine).

## Esito misurato (verifica live del 22 agosto)

**1 252 → 900**, cioè il viewport: la pagina **non scorre** a 1600×900, 1440×900, 1280×800 e 1024×768, in
italiano **e** in inglese, da zoom **0.8 a 1.5**. Il riquadro misura 505px (519 a 1024×768) — cioè quanto il
suo contenuto, non quanto lo schermo.

Guidata con la skill `verifica-live` su copia del DB (Edge headless, `Accept-Language: it-IT`, sentinella
`nav.admin-nav` prima di credere a un numero), con gli stati d'import **scritti nella copia mentre l'app
girava**: un import in errore con messaggio lungo, uno fermo da tre giorni, una policy già decisa da una
persona e una mai salvata da nessuno.

### La lezione riusabile: `max-height`, non `height`

Il primo tentativo usava `vipiFitViewport` come tutte le altre pagine del giro. Risultato misurato **e
guardato**: riquadro 682px per un contenuto di ~450 — **mezzo pannello di bianco**. Tolta la misura del tutto,
il riquadro tornava alto quanto il contenuto ma a 1024×768 la pagina scorreva di 52px e da zoom 1.25 di più.

Da qui **`vipiCapViewport`** (`vipi-ui.js`), che scrive `max-height`: alto quanto il contenuto quando ci sta,
e dentro scorre solo quando non ci starebbe. ⚠️ Quale delle due serve **dipende da cosa c'è dentro**:
`height` è giusto dove il contenuto è più alto dello schermo per mestiere (Audit, Aeroporti), `max-height`
dove è corto e fisso. E «la pagina non scorre» non è l'obiettivo: l'obiettivo è che **ciò che si guarda stia
a schermo**.

⚠️ E i 52px avevano un colpevole preciso, già noto: il `.wrap` porta **70px di padding sotto il riquadro**, e
il tetto misura fin dove arriva il riquadro, **non cosa gli sta sotto**. Serve
`.wrap.struct:has(.sorg-pane){padding-bottom:18px}` — su Audit erano 52, qui 70.

### Quello che hanno visto gli occhi e non i numeri

Metà dei difetti non aveva un'asserzione che li cercasse:

- ⚠️ **due tasti «Annulla» affiancati** con la conferma aperta — quello di `InlineConfirm` chiude la domanda,
  quello della pagina butta le modifiche. Il secondo è diventato «Ripristina»;
- ⚠️ **le sei caselle non erano incolonnate**: `.se-row input{flex:1}` è la regola dei **campi di testo** e si
  applicava anche alle checkbox, che si allargavano a riempire la cella — lo scostamento dipendeva dalla
  lunghezza della parola accanto. `.se-row` è la riga di un form; una cella di tabella non è un form;
- i link «Dove si modifica», a `display:block`, **sembravano campi di testo** (`.lnk-mini` è una pillola col
  bordo, stirata per tutta la colonna);
- un errore di rete lungo quattro righe faceva la **riga SID alta il doppio** delle altre: clamp a due righe,
  testo intero nel `title`;
- la frase di conferma **ripeteva la stessa spiegazione** per ogni categoria: ora raggruppata per verso, come
  la riga di audit;
- ⚠️ **`e'` e `piu'`** al posto di `è` e `più` in tre stringhe nuove. Le stringhe si rileggono **in pagina
  italiana**, non nel file resx.

### Le bugie residue della colonna di stato

Tutte della stessa famiglia del verde regalato, e tutte trovate **dopo** averlo corretto — perché una bugia
tolta dalla pill si ripresenta nella cella accanto:

- una categoria **esclusa** annunciava il **prossimo giro** (che non ci sarà) e mostrava l'**errore** di un
  giro che non la riguarda più;
- una categoria **ferma** annunciava un «prossimo» che stava **nel passato** («prossimo 19 agosto» col 21 sul
  calendario): ora dice «atteso il …, non arrivato»;
- «su richiesta» era scritto **due volte** nella stessa cella (pill e riga sotto), e vinceva sull'errore
  quando la sorgente veniva sconfigurata — la pill smentiva il testo che le stava accanto.

### Rimasto aperto, e non è di questa pagina

⚠️ Lo **sforo orizzontale** a 1280 e 1024: `div.right` della topbar misura **1 411px dentro 1 280**, ed è
identico su `/vsop`, su Audit e su Sorgenti — verificato elencando gli elementi oltre il bordo: **niente
dentro il `.wrap` sfora**. È del chrome, come già scritto nell'handoff.

## Slice, come sono andate

Dieci sulla carta, dieci fatte — sei di sostanza e quattro di forma — più una undicesima non prevista: il giro
di correzioni che la verifica live ha fatto nascere, ed è quello che ha prodotto `vipiCapViewport` e le
regole 150-152.
