# Aperture dei documenti: i più letti in cima alla pagina dell'ACC — carta (1° ottobre 2026)

> **Stato: ✅ ESEGUITA il 1° ottobre 2026** sul ramo `fix/aperture-documenti` (filone Sito, S93). **Migrazione
> additiva** `ApertureDocumenti` (SQLite e MySQL; Postgres la allinea `PostgresSchemaReconciler`). Nessun `deploy/`.
> Richiesta del committente: «per ogni documento un contatore di quante volte viene aperto (non importa da chi) e, nella
> pagina dell'ACC, per ogni gruppo i primi tre per numero di aperture; nelle pagine elenco l'ordine alfabetico».
>
> Dove sta: `AperturaDocumento` (Domain: la riga e il giorno) · `IApertureDocumenti` e `ApertureDocumenti.Primi`
> (Application: il contratto e la regola dei tre) · `EfApertureDocumenti` (Infrastructure: conta e legge) · le quattro
> pagine pubbliche (`AeroportoPage`, `MilDocumentPage`, `AppnPage`, `VloaListPage`, chi conta) · `AccLanding` (chi legge).

## 1. Com'era

Nella pagina di un ACC (`/services/vsop/<acc>`) le tre schede — Aeroporti, Avvicinamenti autonomi, vLoA — mostravano
tre voci: prima quelle messe **in evidenza** a mano (`FeaturedRank`), poi le altre **in ordine alfabetico**. Con sei
scali, LIBC, LIBD e LIBG erano in cima perché cominciano per C, D e G.

## 2. Le decisioni del committente (1° ottobre 2026)

1. **Finestra di 90 giorni** (sui tre periodi proposti: 30, 90, da sempre): la classifica segue quel che si usa adesso,
   e un documento di un evento finito non resta in cima per mesi.
2. **Prima i manuali, poi i più aperti**: la scelta «in evidenza» resta e vince sui numeri.
3. **Il numero lo vede solo lo staff** (chi può modificare), accanto alla voce: «12 aperture», col «?» che dice
   «negli ultimi 90 giorni».
4. **Le pagine elenco** (`/airports`, `/apps`, `/vloa`) **non cambiano**.

## 3. Che cosa si conta

- **Un'apertura = una richiesta della pagina PUBBLICA** del documento. Le pagine documentali sono SSR statiche: una
  richiesta, un render, un conto. Non contano la **bozza** e l'**anteprima di una release** (le apre chi scrive, e
  gonfierebbero proprio i documenti in lavorazione), né l'**editor**.
- **Chi apre non si salva**: la riga è (documento, giorno, volte). Niente VID, niente indirizzo. Per questo non c'è una
  regola di conservazione come quella del registro degli accessi; le righe vecchie restano (al massimo 365 l'anno per
  documento) e un documento cancellato si porta via le sue (`ON DELETE CASCADE`).
- **I bot non arrivano**: dal 30 settembre il sito chiede il login per tutto ([login obbligatorio](2026-09-30-login-obbligatorio.md)).
- ⚠️ **La cache delle letture anonime** (`CacheDelleLettureAnonime`, 60 secondi) servirebbe le aperture ripetute
  senza far girare la pagina, e quindi senza contarle. In produzione non tocca: chi legge è sempre entrato, e per
  chi è entrato la cache non c'è. **In locale sì** (l'identità di sviluppo è anonima): la prova a schermo va fatta
  con un cookie qualunque nel browser, che spegne la cache come il login — senza, tre aperture ne contano una.
- **Un ricarico conta di nuovo**, e anche cambiare «Tutto · Pilota · ATC» (è una richiesta nuova). È un difetto
  accettato: la classifica è fra documenti, e tutti lo subiscono allo stesso modo.
- **Una pagina unita conta il solo documento della porta**: è quello che il lettore ha chiesto.
- **La vIPI dell'ACC non si conta**: non sta in nessuna delle tre schede.

## 4. Come si mettono in fila

`ApertureDocumenti.Primi`: in evidenza (nel loro ordine) → aperture degli ultimi 90 giorni, decrescenti → nome. Lo
**scalo somma vIPI e vSOP militare**: la riga della scheda è lo scalo, e da lì si apre l'uno o l'altro. Senza numeri
(contatore appena acceso, archivio che non risponde) l'ordine è quello di prima.

## 5. Le scelte di costruzione

- **L'incremento lo fa il database** (`UPDATE … SET Volte = Volte + 1`), non «leggi, somma, scrivi»: due lettori nello
  stesso istante altrimenti farebbero un'apertura sola. La riga del giorno che nasce in due insieme urta la chiave
  primaria (documento, giorno) e il secondo ripiega sull'incremento.
- **Il contatore non lancia mai**: un guasto finisce nel registro e la pagina esce lo stesso. Lo stesso per la lettura
  sulla pagina dell'ACC, che senza numeri ricade sull'ordine di prima.
- **Si conta in fondo al caricamento**, dopo tutte le letture della pagina, sullo scope della pagina: il contesto del
  database è uno, e due operazioni insieme lo rompono.

## 6. Reti

`ApertureDocumentiTests` (Infrastructure: giorno, due contesti che si sommano, finestra di 90 giorni, chiave sconosciuta
e guasto senza eccezioni, cancellazione a cascata) · `ApertureDocumentiPrimiTests` (Application: l'ordine) ·
`ApertureDocumentiPagineTests` (Ui: ognuna delle quattro pagine conta e solo in vista pubblica; la pagina dell'ACC usa
la regola per tutti e tre i gruppi).

## 7. Non fatto, di proposito

- **Il numero nelle pagine elenco** per lo staff: si aggiunge in un'ora se serve.
- **Un'apertura per persona al giorno** invece di una per richiesta: chiederebbe di sapere chi apre, che è proprio
  quel che non si vuole salvare.
