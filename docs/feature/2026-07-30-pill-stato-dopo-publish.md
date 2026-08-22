# Fix — La pill di stato resta «Bozza vN» dopo «Pubblica ora»

Data: 2026-07-30 · Stato: FATTO (build 0 warning, suite **640** verde a fine sessione — 635 dopo questo fix, 640
col seguito sulla chiave di release in coda al documento; verificato live su `/services/vsop/libb/editor`).

## Sintomo
Su `/services/vsop/libb/editor`, dopo «Pubblica ora», la pill del rail continuava a mostrare «Bozza v13». Sembrava che la
pubblicazione non avesse funzionato.

## Cosa era davvero rotto (e cosa no)
**La pubblicazione funzionava.** Tracce nel DB dopo la segnalazione:

| Traccia | Contenuto |
|---|---|
| `DocReleases` #36 | `AccVipi` / `LIBB\|LIBB_ES_CTR`, ciclo 2607, **Effective**, creata 13:15:36 |
| payload della #36 | contiene le sezioni della v13 (es. la sezione id 437) → ha catturato la **bozza** |
| `AuditLogs` #18 | `Publish DocumentVersion 27` → `{Id:1, VersionNumber:13, Reason:"publish-now-release"}` |
| `Documents` #1 | `Published`, `CurrentVersionId=27` (= v13 **Published**), nessuna bozza residua |

Erano rotte due cose, **entrambe di presentazione**:

1. **Pill stantia.** `ReleasePanel` è autonomo: dopo il publish ricaricava solo le *proprie* release (`Reload()`)
   e non aveva alcun canale verso la pagina ospitante. La pill legge `_doc`, caricato all'apertura dell'editor —
   quando la versione era ancora bozza. Solo un refresh manuale la aggiornava. Stesso difetto in
   `AppEditorPage`, `AeroportoEditorPage` e `VloaEditor`: il componente è condiviso.
2. **Etichetta ambigua.** Nella riga della release c'era «rel. v12»: **non** è la versione del documento, è il
   progressivo della release per quel bersaglio (`SaveReleaseAsync`: `max(VersionNumber)+1`; per LIBB ne restano
   9, numerate 4→12 dopo la potatura di retention). Con la stessa notazione «v» della pill sembrava che il
   publish avesse pubblicato una versione vecchia.

## Fix
- **`EventCallback Published` su `ReleasePanel`**, invocata dopo una pubblicazione riuscita (immediata *o*
  schedulata: cambia comunque la timeline che l'host mostra) e **non** se `BeforePublishAsync` annulla. I tre
  host la agganciano al proprio `LoadAsync`. Risolto alla radice: vale per tutti gli editor, non solo l'ACC.
- **Etichetta «rilascio #N»** (chiave `Rel_VersionLabel`, EN «release #N») in `ReleasePanel` e `VersioniPage`.

⚠️ **`string.Format(L["chiave"].Value, n)` non interpola**: quell'indexer restituisce la stringa senza applicare
gli argomenti, quindi il numero non arriva mai al testo. Serve l'overload `L["chiave", n]` — la forma già usata
altrove nel progetto (`L["Common_OnlineN", n]`). Preso al primo giro: il test sull'etichetta è diventato rosso.

## Verifica live (`/services/vsop/libb/editor`, Edge+CDP)
| Passo | Pill | Etichetta release |
|---|---|---|
| apertura | `Published v13` (verde) | `release #12` |
| dopo «Modifica» | `Draft v14` (grigia) | `release #12` |
| **dopo «Pubblica ora»** | **`Published v14` (verde), senza reload** | **`release #13`** |
| dopo reload | `Published v14` — identico | `release #13` |

Prima del fix il terzo passo restava `Draft v14`. Nessun errore in pagina.

## Test
`ReleasePanelTests`: `Published_Avvisa_L_Host_Dopo_Ogni_Pubblicazione` (immediata + schedulata) e
`Published_Non_Avvisa_Se_BeforePublishAsync_Annulla`. Aggiornato
`Il_Numero_Di_Versione_Viene_Valutato_Non_Stampato_Come_Testo` alla nuova etichetta: continua a sorvegliare che
il numero sia **valutato** e non emesso come testo (la trappola `v@r.Proprietà`), ora anche attraverso il
localizer. Suite 633 → 635.

## File toccati
- `src/Vipi.Ui/Components/ReleasePanel.razor` (parametro `Published`, `Run(notify:)`, etichetta),
  `Pages/VersioniPage.razor` (etichetta), `Pages/AccEditorPage.razor`, `Pages/AppEditorPage.razor`,
  `Pages/AeroportoEditorPage.razor` (aggancio `Published="LoadAsync"`),
  `Resources/SharedResource{,.en}.resx` (`Rel_VersionLabel`), `tests/Vipi.Ui.Tests/ReleasePanelTests.cs`.

## Seguito — la chiave di release ACC ignorava la radice (corretto)
Trovato indagando, corretto subito dopo. `AccVipiReleaseTarget.ResolveDocumentIdAsync` prendeva il **primo** CTR
radice dell'ACC per `CoverageOrder` e **scartava la parte `root`** della chiave `"{acc}|{root}"` — che è invece
ciò che sceglie *quale* albero, quindi quale documento, si pubblica. Su LIBB c'è un solo CTR radice con
documento (`LIBB_ES_CTR`), quindi era innocuo oggi; con una ACC **multi-albero** «Pubblica ora» avrebbe promosso
la bozza del documento sbagliato, in silenzio, perché la chiave sembrava corretta.

Ora: se il root è indicato, si risolve **per callsign** e non esiste fallback — meglio «nessun contenuto da
pubblicare» che pubblicare un altro documento. Chiave col solo codice ACC (legacy): resta il criterio storico
(`CoverageOrder`, poi callsign). I confronti sono in maiuscolo su entrambe le parti: i callsign e i codici ACC lo
sono per convenzione e le chiavi si costruiscono già così, ma il confronto stringa di EF è sensibile al caso e
una chiave scritta a mano non deve mancare il bersaglio senza dirlo.

Test-first (`AccVipiReleaseTargetTests`, 5 casi, 3 rossi prima del fix): due alberi nella stessa ACC con
`CoverageOrder` **inverso** all'ordine alfabetico dei callsign, così un fallback su uno dei due criteri
sbaglierebbe comunque; più il caso maiuscole/minuscole, la chiave legacy e la radice inesistente o senza
documento. Suite 635 → 640.

Verifica live (`/services/vsop/libb/editor`): nessuna regressione — `Published v14` → `Draft v15` → **`Published v15`**,
release **#14** `Effective`. Catena controllata nel DB: chiave `LIBB|LIBB_ES_CTR` → settore 2 → documento 1 →
versione 31 (v15) promossa, audit `Publish` coerente.

## Non toccato (lo segnalo)
`VloaEditor` mostra la stessa pill ma **non** ospita `ReleasePanel` (le vLOA si pubblicano da `VersioniPage`),
quindi non ha il problema.

`EfAccDerivationRepository.ResolveAccDocumentIdentityAsync` — che produce il `RootCallsign` con cui l'editor
costruisce la chiave — sceglie il primo CTR radice attivo **senza** richiedere che abbia un documento, mentre il
release target considera solo le radici che ce l'hanno. Non è un problema nel flusso reale (il documento esiste
già quando si pubblica, creato entrando in modifica), ma i due criteri divergono: se un giorno la radice primaria
di un'ACC restasse senza documento, l'editor la userebbe come chiave e la pubblicazione risponderebbe «nessun
contenuto». Da unificare se si mette mano alla risoluzione della radice.
