# Diagnostica — densità UI (carta, 22 agosto 2026)

> Ramo `ui-trasferimenti-densita`, pagina `/services/vsop/admin/diagnostics`. Seconda carta del giro: **la forma**.
> La sostanza sta nella gemella [`2026-08-22-diagnostica-cosa-afferma.md`](2026-08-22-diagnostica-cosa-afferma.md).
> Dodicesima pagina del giro. Regole: [regole-ui-pagine-admin](../design/regole-ui-pagine-admin.md).

## Il numero, rimisurato

La ricognizione dava **900**. ⚠️ Era la misura **a report vuoto**: nel DB di sviluppo nessun soft-ref è
rotto, quindi la pagina mostrava un callout verde e due schede. **Con otto rilievi sono 1 349px**, e la
pagina scorre già a 1600×900.

È la terza volta nel giro: Permessi diceva 1 346 a tabella vuota (erano 2 449), Audit 1 556 con 28 righe
(erano 13 293). Qui il numero non era vecchio — era **la misura di una pagina che non aveva niente da dire**.

Quanto può crescere non ha un tetto noto: otto rilievi fanno 480px di tabella (60px a riga, perché
«Categoria» va a capo e il «Dettaglio» è prosa su due righe). Cinquanta rilievi — un import andato storto,
una potatura di aree che porta via una selezione salvata in venti documenti — fanno **~3 000px di sola
tabella**. Quindi qui il riquadro misurato serve, e con lui il `thead` fermo: al contrario di Sorgenti,
il contenuto è più alto dello schermo **per mestiere**, quindi `vipiFitViewport` (`height`) e non
`vipiCapViewport` (regola 150).

## Misurato prima di toccare (1600×900, IT, otto rilievi)

| Pezzo | Quanto | Perché se ne va |
|---|---|---|
| `.wrap` a **1 100px** | ⚠️ manda la **barra admin su due righe** (75px invece di 55) | larghezza piena, come Audit: la tabella ha quattro colonne di cui due di prosa |
| Sottotitolo su **due righe** | testata 90px | nel «?» (regola 7). Ed è anche **falso**: vedi D2 nella carta della sostanza |
| Fascia `Diag_IssuesFound` | ~48px, sempre | il conteggio va nella pill del titolo; «sono solo diagnosi, nessun dato è stato modificato» è prosa: «?» |
| Colonna «Gravità» | una colonna intera per ripetere GRAVE/AVVISO otto volte | pill accanto alla **categoria**, e i chip che contano fanno il filtro |
| Riga da 60px | «Gerarchia dangling» va a capo in una colonna stretta | colonne misurate col font sui valori veri |
| Pattern admin (regex) | 2 righe sempre a schermo | nel «?» della scheda: servono quando qualcosa non torna |
| Tre blocchi in colonna | il terzo (immagini) sta **sotto** 1 349px: non lo vede nessuno | vedi sotto |

## La decisione di forma: tre domande, due colonne

La pagina risponde a **tre** domande che non si somigliano:

1. *qualcosa nei dati non torna?* — cresce senza tetto, si legge scorrendo, ha un'azione altrove;
2. *chi può editare?* — una tabellina di poche righe, si guarda **soprattutto quando va tutto bene**
   (lo dice già il commento nel markup) e non ha azioni;
3. *quanto spazio sprecano le immagini?* — un'azione a due tempi, distruttiva, che parte da un clic.

A sinistra la prima, in un riquadro misurato con il `thead` fermo. A destra le altre due, una sopra
l'altra: sono compatte e non crescono. È la stessa griglia di Versioni e Permessi (`.ver-layout` /
`.perm-layout`), che qui però **non** è elenco+dettaglio — e va detto, perché la regola 140 dice che il
pannello di destra si giustifica con l'**azione**: qui a destra non c'è il dettaglio di una riga di
sinistra, ci sono **due argomenti diversi**. Il pannello si giustifica perché sono tre domande in una
pagina, non perché una spieghi l'altra.

⚠️ Il terzo blocco oggi è **sotto la piega** e ci resta per sempre: più rilievi ci sono, più è lontano — e
«ho trovato dei problemi» è esattamente il momento in cui uno scorre meno. Portarlo a destra non è
estetica: è l'unico modo perché venga visto.

## L'elenco dei rilievi

```
[chip: Dati 6 · Schema 0 · Server 0 · Avvio 0 · Configurazione 0]   [cerca]

GRAVITÀ+CATEGORIA        ENTITÀ                        DETTAGLIO                          DOVE
● Gerarchia dangling     Settore ACC LGGG_W_CTR        ParentCallsign «LIRR_XX_CTR»…      Struttura
● Pista orfana           Clausola #1 (LIBB, Y01-Y12)   ConditionRefId=99001 non…          Accordi
▲ Area fantasma          Clausola #5 (LIBB, ALL to GR) Area «LI R99Z» non presente…       Accordi
```

- **I chip contano** e contano la condizione che applicano (regola 107): la somma è il totale. ⚠️ Qui contano
  per **area** (dati/schema/server/avvio/configurazione), non per categoria: le categorie sono **tredici** e
  crescono con ogni controllo nuovo, le aree sono cinque e rispondono alla domanda «di chi è il problema».
- ⚠️ Un chip a **zero è neutro**, non verde (regola 108): «0 rilievi di schema» è un fatto, e su questa
  pagina lo è ancora di più — zero può voler dire «la sonda non gira su questo provider».
- **La gravità è una pill sulla categoria**, non una colonna sua: rosso `grave`, ambra `avviso`.
- **«Dove»** è il link della slice 3 della carta sostanza. Vuoto quando non c'è un posto da aprire.
- Il **dettaglio** è prosa e prende quello che avanza: colonna elastica, le altre fisse, `min-width` = somma
  delle fisse **più** il pavimento dell'elastica.

## Testata in una riga

`titolo · pill «8 · 6 gravi» · «?» · Aggiorna`, con `.doc-head.st-head` e `.st-h2` come le altre undici. La
pill è **verde a zero** — qui sì, perché «nessuna incongruenza» è davvero una buona notizia, al contrario dei
chip che contano una fetta del totale. Accanto al tasto, in `muted`, **da quando è la fotografia**.

## Prosa: «?» e Guida

- `HelpHint Href="/services/vsop/guide#admin-diagnostica"` con dentro il sottotitolo **corretto** (tutte e cinque le
  aree), la frase «sono solo diagnosi», e cosa vuol dire ogni area.
- Sezione `#admin-diagnostica` nella Guida, IT **ed** EN: le cinque **aree** con, per ognuna, **come nasce** e
  **dove si ripara** (le categorie sono tredici e crescono: raccontarle una per una invecchierebbe). È la pagina in cui la Guida serve di più: ogni riga della tabella è un termine tecnico.
- Voce in `GuideSearchCatalog` (oggi «diagnostica» compare solo dentro la voce «Aree admin»).
- Il «?» della scheda «Chi può editare» si prende i **pattern regex** e la spiegazione di cosa sono.

## Come lo verifico

Skill `verifica-live` su **copia** del DB, exe pubblicato avviato **dalla sua cartella**, porta libera,
sentinella `nav.admin-nav` prima di credere a un numero.

⚠️ **Con i rilievi veri, e non ce ne sono**: nel DB di sviluppo il report trova zero. Lo stato si costruisce
**scrivendo nella copia mentre l'app gira** — le incongruenze che il report esiste per trovare si producono
sporcando i soft-ref: un `ConditionRefId` che punta a una pista inesistente, una `ConditionLabel` che cita un
altro ident, una `ConditionAreaLabel` soppressa, un `ParentCallsign` che non risolve. Vanno provati **due**
volumi: otto rilievi (com'è ora) e cinquanta, perché è l'unico modo di sapere se il riquadro misurato regge.

Passi: **1600 / 1440 / 1280 / 1024**, **IT ed EN**, zoom **0.8 → 1.5** con `window.vipiSetZoom`. Stati da
fotografare **e guardare**:

1. zero rilievi (il caso buono, che è anche il più frequente);
2. otto rilievi, misti fra le aree;
3. cinquanta rilievi (il riquadro tiene? il `thead` resta?);
4. una **sonda rotta** — è la slice 1. Il modo economico di provarla è **rinominare via** la tabella che il
   report interroga: la lettura dei dati fallisce e tutto il resto della pagina deve arrivare lo stesso;
5. la scheda immagini dopo «Analizza», con e senza orfane;
6. il caso «nessuno è admin», che è il rilievo più grave che l'applicazione sappia produrre.

⚠️ Lo sforo **orizzontale** non è un segnale utile finché la topbar non è sistemata (`div.right` 1 411px
dentro 1 280, identico su ogni pagina).

## Esito misurato (verifica live del 22 agosto)

**1 349 → 900**, cioè il viewport: la pagina **non scorre** a 1600×900, 1440×900, 1280×800 e 1024×768, in
italiano **e** in inglese, da zoom **0.8 a 1.5** — e **con 76 rilievi come con zero**. Il riquadro misura
682px (582 e 519 sugli assetti bassi) e dentro scorre solo la tabella: il `thead` resta fermo a 269px dopo
**4 024px su 4 618** di scorrimento interno.

Guidata con la skill `verifica-live` su copia del DB, con **quattro stati che il DB di sviluppo non ha** e
che sono stati scritti nella copia mentre l'app girava:

| Stato | Come si è costruito | Cosa ha detto |
|---|---|---|
| zero rilievi | il DB di sviluppo così com'è | pill **verde** «nessun rilievo», `/vsop/health` = `Healthy` |
| otto | tre clausole sporcate + cinque padri inesistenti | 900px, i link portano a Struttura e Accordi |
| settantasei | 25 clausole + 50 nodi | 900px lo stesso; scorre il riquadro, non la pagina |
| **sonda rotta** | la tabella che il report interroga **rinominata via** | la pagina **vive**, mostra il rilievo del guasto, la colonna di destra arriva intatta, e `/vsop/health` risponde **`Degraded`** |

L'ultima riga è la slice 1 verificata dove conta: non nei test, ma nella pagina.

### Quello che ha visto l'occhio e non i numeri

- ⚠️ **La localizzazione era a metà.** Tradotti categoria e dettaglio, il **bersaglio** restava italiano:
  «severe | Broken hierarchy | *Settore ACC* LGGG_W_CTR». Metà dei bersagli non è un identificatore ma una
  frase. Regola pratica emersa: si traduce ciò che è **prosa**; ciò che è un identificatore (`sql_mode`,
  `Documents.Title`) resta, perché tradurlo è inventargli un secondo nome.
- ⚠️ E la stessa trappola in piccolo: il nome del controllo dentro il messaggio della sonda rotta era un
  **argomento**, quindi grezzo — «The check *«incongruenze dei dati»* did not complete». Un argomento è un
  valore, non una chiave. Lì non ci andava affatto: sta già nella colonna del bersaglio, tradotto.
- **Le due schede di destra avevano due pesi tipografici diversi** e si leggevano come due livelli:
  `MediaCleanupCard` teneva la testata che aveva quando stava da sola in fondo alla pagina.
- `piu'` invece di `più` nel testo della card immagini (difetto preesistente, ben visibile una volta
  portata a destra).
- La colonna «Entità» a 190px mandava a capo **alcune** righe e non altre, e l'elenco sembrava sghembo:
  rimisurata sul valore più lungo che il produttore sa generare.
- Il messaggio di «tutto a posto» prometteva ancora i **soli soft-ref** — la stessa promessa ridotta del
  sottotitolo, sopravvissuta in un altro punto della pagina.

### Rimasto aperto, e non è di questa pagina

⚠️ A **1024×768** la barra admin torna su due righe (75px): lì undici voci non ci stanno comunque, e non è
più il difetto del `.wrap` a 1 100px — a 1280 ora sono 43px. E lo sforo **orizzontale** resta quello della
topbar (`div.right` 1 411px dentro 1 280, identico su ogni pagina).

## Slice, come sono andate

Nove sulla carta, nove fatte — cinque di sostanza e quattro di forma — più il giro di correzioni che la
verifica live ha fatto nascere, ed è quello che ha prodotto le regole 160 e 162.
