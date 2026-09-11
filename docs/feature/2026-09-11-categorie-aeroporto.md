# Le quattro categorie d'aeroporto 🟢

**Chiesto dal committente l'11 settembre 2026.** Oggi un aeroporto è *niente*, *presenza militare* o *solo
militare*. Diventa una di **quattro categorie**, ognuna con i suoi documenti:

| # | Categoria | Chi la decide | vIPI civile | vSOP militare |
|---|---|---|---|---|
| 1 | **Civile** | la sorgente: nessuna presenza militare | ✅ | ❌ |
| 2 | **Solo militare** | un amministratore, dalla pagina Aeroporti | ❌ | ✅ |
| 3 | **Civile con presenza militare** | un amministratore | ✅ | ❌ |
| 4 | **Militare con presenza civile** | un amministratore | ✅ | ✅ |

Le categorie 2, 3 e 4 si possono scegliere **solo** dove la sorgente dice che c'è presenza militare
(`HasMilitaryPresence`). E in **tutti** i documenti, per **tutti** gli aeroporti, compare la categoria —
anche «Civile», che oggi non ha etichetta.

## Che cosa cambia rispetto a oggi, misurato

Sul `vipi.db` di sviluppo: 93 aeroporti, **34** con presenza militare.

| oggi | quanti | diventa |
|---|---|---|
| senza presenza | 59 | **1** Civile |
| solo militare (LIBG LIBN LIBV LIMN LIBA LIPA) | 6 | **2** Solo militare |
| presenza, **con** vSOP militare (LIML LIMS) | 2 | **4** Militare con presenza civile |
| presenza, senza vSOP (Linate, Ciampino, Pisa…) | 26 | **3** Civile con presenza militare |

⚠️ La novità vera è una sola: **la categoria 3 non ammette il vSOP militare**. Oggi un campo con presenza
militare ne può avere uno; domani solo se è in categoria 4. La regola di travaso qui sopra mette in 4 **chi un
vSOP ce l'ha già**, quindi nessun documento esistente finisce fuori categoria per effetto del cambio.

## Decisioni del committente

1. **Default = 3.** I campi con presenza e senza vSOP partono «Civile con presenza militare», e così quelli che
   la sorgente scoprirà in futuro. Chi va in 4 (Pisa, per esempio) lo sposta un amministratore.
2. **Via la regola «prima la vIPI civile».** Oggi su un campo misto il vSOP nasce solo dopo la vIPI. In
   categoria 4 i due documenti nascono in qualunque ordine — LIML ha già il vSOP senza vIPI.
3. **Cambiare categoria non tocca i documenti** (stessa decisione del 10 settembre, carta
   `2026-09-10-solo-militare-con-vipi-civile.md`). Se la categoria nuova esclude un documento che c'è già, il
   documento resta; il gesto **chiede conferma** e la **Diagnostica** lo segnala finché qualcuno non lo
   nasconde o lo elimina.

## Il modello

**Un campo solo, `Airport.Category`** (enum `AirportCategory`, salvato come stringa, `Civil` = zero), al posto
di `IsMilitaryOnly`. ⚠️ **Invariante**: senza presenza militare la categoria è `Civil`; con presenza **non** è
mai `Civil`. La mantengono tre porte, e servono tutt'e tre:

- il **giro dell'anagrafica** (`SyncAirportSourceFieldsAsync`): la presenza cade ⇒ `Civil`; la presenza
  compare ⇒ il default 3;
- il **comando** della pagina Aeroporti, che rifiuta 2/3/4 senza presenza e `Civil` con presenza;
- una **passata d'avvio** idempotente, che ripara lo stato che nessuno digita (e che fa il travaso).

**Le regole «quale documento» stanno in un posto solo**, `AirportCategories` nel Dominio (puro, senza I/O):
`AmmetteCivile`, `AmmetteMilitare`. Oggi la stessa domanda è scritta in sei punti — nascita della vIPI,
nascita del vSOP, «Nuovo documento», elenco vSOP, rimando all'editor militare, Diagnostica — ed è la **regola
del 2** del `FEATURE-PROCESS`.

## ⚠️ La finestra cieca decide la forma della migrazione

Fino al 16 settembre 2026 le migrazioni MySQL non possono togliere colonne né eseguire SQL
(`MigrazioniDellaFinestraCiecaTests`). Quindi:

- la migrazione **aggiunge** `Airports.Category` (NOT NULL, default `'Civil'`) e **non** tocca `IsMilitaryOnly`;
- il **travaso** lo fa la passata d'avvio in C#, che vale per tutti e tre i provider;
- `IsMilitaryOnly` resta in archivio **come specchio** (`Category == MilitaryOnly`), scritto a ogni cambio e
  letto **solo** dal travaso: se si dovesse tornare a 1.21.x, quella versione troverebbe il dato giusto.
- ▶ **Dopo il 16 settembre**: una migrazione toglie `IsMilitaryOnly`, e il travaso smette di leggerlo.

## Pre-flight (`docs/FEATURE-PROCESS.md`)

1. **Modello** — un concetto che **sostituisce** un booleano, non uno che gli si affianca. Il booleano resta
   solo in archivio, per la finestra cieca, e ha scritto in faccia che è in pensione.
2. **Dispatch** — la domanda «quali documenti ammette?» era in sei punti: ora è `AirportCategories`.
3. **Ingressi + verifica** — il comando sta dove sta oggi (la pastiglia nella pagina Aeroporti), l'etichetta
   dove sta oggi più il vSOP militare. Verifica dal vivo sui quattro stati, e sui due documenti fuori categoria.
4. **Propagazione** — `IsMilitaryOnly` sparisce da record, parametri, test, testi e memorie nello stesso giro.

## Definition of Done

- [x] `dotnet build Vipi.slnx -c Release --no-incremental` verde, 0 avvisi; suite verde, tutti i progetti.
- [x] Travaso provato su una copia del `vipi.db` vero: **59 / 6 / 26 / 2**, esattamente la tabella qui sopra
      (log d'avvio: «Portati 34 aeroporti alla loro categoria»). Mutazione sul travaso: due test rossi.
- [x] Le guardie di nascita rispondono per categoria, provate su tutte e quattro — col vSOP creato **prima**
      della vIPI, l'ordine che fino all'11 settembre era vietato (`EdizioneGiustaPerCampoTests`).
- [x] 🔴 Il caso «tutto a posto» in Diagnostica **tace**, per tutte e due le edizioni.
- [x] Verifica live: tre voci e niente «Civile»; comandi spenti senza la modifica presa; LICA 3→4 scrive
      senza chiedere; LIML 4→3 (vSOP pubblicato) chiede conferma, «Chiudi» lascia l'archivio e la tendina com'erano,
      «Sì» scrive; la Diagnostica dice «vSOP militare fuori categoria» per LIML e tiene il caso di LIPA; etichette in
      vIPI (LIBD «Civile»), APP (LIBP_APP, LIBA_APP, LICC_APP), vSOP (LIBG, LIML) ed editor (LIMS «Militare con
      presenza civile»), in italiano e in inglese.
- [x] Spec `modello-dati.md` §9.3, memorie e questa carta aggiornate.

## Trovato strada facendo

- 🔴 **La domanda di conferma usciva tagliata** al bordo della cella (`td.col-state` è `nowrap`): ora va a capo.
- 🔴 **La tendina accanto allo stato allargava la tabella**: «Militare con presenza civile» sono 196px, e lo sforo
  orizzontale già aperto (§H3) misurava **85px a 1600 e 239px a 1280**. Messa **sotto** lo stato, stessa misura
  (`scrollWidth` della tabella meno `clientWidth` del contenitore): **0 a 1600 e 77px a 1280**. Per confronto, la
  memoria del 25 agosto dava 17 e 171px con la vecchia pastiglia, misurati in un altro modo.
- ⚠️ **Il dato da sistemare a mano dopo il caricamento**: sul `vipi.db` di sviluppo **Ghedi (LIPL)** esce
  «civile con presenza militare», perché non era mai stato marcato «solo militare». In produzione i 26 campi in
  categoria 3 vanno riguardati una volta: quelli che un vSOP lo devono avere (Pisa, per esempio) vanno in 4.

## Gli aeroporti dell'ACC (11 settembre 2026, sera)

**Chiesto dal committente:** su `/services/vsop/{acc}` niente più riquadro «vSOP militari»; su
`/services/vsop/{acc}/airports` **tutti** gli aeroporti, filtrabili per le quattro categorie, e dove ci sono
vIPI **e** vSOP la scheda ha **due voci**, una per documento.

- **Chi compare** — `AeroportiDellAcc.Elenco` (Ui), una regola sola per la landing (conteggio e tre in
  evidenza) e per l'elenco: lo scalo c'è se ha **almeno un** documento pubblico (release effettiva, non
  nascosto). La vIPI pretende `IsPublic` (almeno un settore), il vSOP solo lo scalo non nascosto — LIMS ha
  zero settori. «Tutti» vuol dire tutti quelli con qualcosa da leggere: uno scalo senza documenti porterebbe a
  «documento non disponibile».
- ⚠️ **La categoria non filtra i documenti**: un documento pubblicato fuori categoria resta raggiungibile
  finché qualcuno non lo nasconde (decisione 3 qui sopra). Toglierlo dall'elenco lo renderebbe irraggiungibile
  senza spegnerlo.
- **La scheda** — con un documento solo resta un link intero (al vSOP se è l'unico); con due diventa un
  riquadro con «vIPI civile» e «vSOP militare» **accanto al nome** (a capo se non c'è posto), perché un `<a>`
  dentro un `<a>` non è HTML. La riga della landing ha posto per un link: la vIPI se c'è, altrimenti il vSOP.
- **Il vSOP si apre in vista ATC** (`&vista=atc`), da ogni collegamento di questo elenco e della landing: dall'ACC
  arriva un controllore, come dall'elenco nazionale arriva un pilota (`vista=pilota`). La chip in testata
  riporta a «Tutto»; su un vSOP senza sezioni marcate non filtra niente (e la chip non compare).
- **I filtri** — «Tutti» più le quattro categorie, dal più civile al più militare; la barra c'è solo con
  almeno due categorie presenti, e dentro ci sono sempre tutte e quattro (a zero spente, **neutre**: il verde
  di `sh-chip:disabled` vuol dire «coda vuota, bene»).
- L'elenco **nazionale** `/services/vsop/mil` resta, raggiunto da `/services` e `/services/vsop`.

Verificato dal vivo su una copia del `vipi.db` (con una release vIPI seminata su LIMN per avere uno scalo con
tutti e due i documenti): LIBB 4 schede (LIBG solo vSOP), LIMM 3 (LIML e LIMS solo vSOP, LIMN due voci che
aprono i due documenti), ogni filtro mostra solo la sua categoria e ricliccato torna a tutti, italiano e
inglese, tema scuro, 400px senza scorrimento orizzontale, zero errori in console.

## ▶ Dopo il 16 settembre 2026

Una migrazione toglie `Airports.IsMilitaryOnly`; `AirportCategoryTransfer` perde il ramo del travaso e resta
`AirportCategories.Normalize`; il setter di `Airport.Category` torna un'auto-proprietà.
