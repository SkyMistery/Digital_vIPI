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
Blazor) con dentro **la frase del cambio**, una riga per categoria toccata, e le righe verso «da sorgente»
in rosso:

> Piste: da sorgente → manuale — non si aggiorneranno più; diventano modificabili nell'editor aeroporto.
> ⚠️ SID: manuale → da sorgente — al prossimo import le SID inserite a mano saranno sostituite da quelle del sectorfile.

Le frasi sono **le stesse** che finiscono nel registro di audit (slice 3 della carta sostanza): un
vocabolario solo, come `AuditNarrator` per gli eventi.

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

## Slice di questo pezzo (dopo le sei della sostanza)

7. **Una tabella sola** + testata in una riga + `.st-msg` al posto dei due callout + `.st-pane` misurato
   (`vipiFitViewport('.sorg-pane', 900)` in `OnAfterRenderAsync`, **a ogni render**: il «?» aperto e la
   conferma in linea cambiano ciò che sta sopra).
8. **Prosa nei «?»** + sezione Guida `#admin-sorgenti` (IT/EN) + voce nel catalogo di ricerca.
9. **Conferma in linea** col racconto del cambio, e la pill «modificate».
10. **Misura e rifiniture**: larghezze di colonna col **font calcolato** sui valori veri, quattro assetti,
    due lingue, cinque zoom; poi carta + regole + ricognizione §15 + memoria.

⚠️ Trappole già pagate che valgono qui: le regole nuove si scrivono **in coda** al foglio e con `.struct`
davanti (perdono in silenzio contro `.res-table`); una classe non può significare due cose (`.sorg-*` per
quello che è solo di questa pagina); `@bind:event="oninput"` sui campi che accendono un tasto — qui sulle
spunte serve `@bind:after` per ricalcolare la pill; **due chiavi resx con la stessa traduzione** sfuggono al
test (si confrontano le chiavi, non i valori) e le stringhe nuove si rileggono **in pagina italiana**.
