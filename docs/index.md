# Indice della documentazione

Mappa di tutti i documenti del progetto, con scopo e stato. Entry point in root: **`../README.md`** (cos'è + build) e **`../HANDOFF.md`** (stato corrente + come riprendere).

**Stato:** 🟢 Autorevole (corrente) · 🔵 Reference (config/integrazione) · 🟣 Design · ⚪ Storico.

## Ordine di lettura consigliato (nuova chat)
0. **[lavori-aperti.md](lavori-aperti.md)** — 📋 elenco unico di **cosa manca da fare**, con il blocco di
   ciascuna voce. Se l'obiettivo è lavorare e non capire, si parte da qui.
1. `../README.md` — cos'è il progetto, architettura, build/run.
2. `../HANDOFF.md` — stato corrente e come riprendere il lavoro.
3. `history/rounds.md` — cosa è cambiato round per round.
4. `spec/modello-dati.md` + `spec/logica-aor.md` — modello dati e logica AoR (cuore testato).
5. `spec/mappa-pagine.md` — rotte del sito.
6. `adr/` — decisioni architetturali (in ordine 0001 → 0006).

## Processo (anti-vibecoding) 🟢
| File | Scopo |
|---|---|
| [FEATURE-PROCESS.md](FEATURE-PROCESS.md) | Runbook per feature nuove: pre-flight 4 domande (modello / dispatch «Regola del 2» / ingressi+verifica / **propagazione**) + DoD. |
| [refactor/REFACTOR-PROCESS.md](refactor/REFACTOR-PROCESS.md) | Runbook per refactor: ciclo Fase 0→4, gate «carta prima di codice». |

## Specifiche tecniche — `spec/` 🟢
| File | Scopo |
|---|---|
| [spec/modello-dati.md](spec/modello-dati.md) | Schema dati / entità EF Core. La **§9** è autorevole; §3–§5 sono storiche (pre-Round 5/13). |
| [spec/logica-aor.md](spec/logica-aor.md) | Logica di visibilità AoR + scenari di test S1–S10 (implementata e testata). |
| [spec/mappa-pagine.md](spec/mappa-pagine.md) | Gerarchia rotte `/vsop` + tabella rotte→file→accesso. |
| [spec/pagine-disabilitate.md](spec/pagine-disabilitate.md) | Pagine spente (rotta rimossa, codice intatto) + come riattivarle. |

## Guide operative — `guide/` 🔵
| File | Scopo |
|---|---|
| [guide/integration.md](guide/integration.md) | Come agganciare il modulo (RCL) a un sito host ASP.NET Core. |
| [guide/integrazione-ivao-it-da-fare.md](guide/integrazione-ivao-it-da-fare.md) | **Lavoro aperto** per far girare il modulo dentro Ivao.It: bloccanti, verifiche mai eseguite, decisioni loro. |
| [guide/config.md](guide/config.md) | Reference completa della configurazione runtime (Division/DataSource/Ivao/Auth/segreti/policy import). |
| [guide/dev-bootstrap.md](guide/dev-bootstrap.md) | Checklist «da DB vuoto a sito popolato» in sviluppo (sequenza import ACC→settori→aeroporti→SID→gerarchia→documenti). |
| [guide/aurora-bridge.md](guide/aurora-bridge.md) | **Guida per il controllore** al tool desktop Aurora: prerequisiti, scrittura del livello, scorciatoia, limiti, dove guardare quando non va. |

## Reference — `reference/` 🔵
| File | Scopo |
|---|---|
| [reference/sector-map.md](reference/sector-map.md) | Mappa settori della ACC pilota Roma (seed di test/fixture). |
| [reference/api-aurora-bridge.md](reference/api-aurora-bridge.md) | Contratto di `POST /vsop/api/v1/transfers/resolve` (bridge Aurora): richiesta, risposta, configurazione `AuroraBridge`, tetti. |

## Design — `design/` 🟣
| File | Scopo |
|---|---|
| [design/piano-vipi-tool.md](design/piano-vipi-tool.md) | Documento di design strategico di base (requisiti, roadmap). Parti superate dai round successivi. |
| [design/piano-editor-appn.md](design/piano-editor-appn.md) | Design editor/viewer APP non remotizzati (storage su Document dopo refactor 08). |
| [design/piano-aurora-bridge.md](design/piano-aurora-bridge.md) | **Tool desktop Aurora ↔ vIPI**: propone il livello di trasferimento al prossimo ente e lo scrive nell'etichetta quota del tag. Endpoint `/vsop/api/v1/transfers/resolve` + app Avalonia. **Codice non iniziato**: prima lo spike F0 sul protocollo Aurora. |
| [design/piano-supporto-mysql.md](design/piano-supporto-mysql.md) | **Supporto MySQL per l'embedding in Ivao.It** (solo TFM net8: Pomelo non ha build EF Core 10). Slice, rischi, stime. **Esecuzione non avviata**: attende la versione del loro server MySQL, che decide la collation. |
| [design/piano-ux-hardening.md](design/piano-ux-hardening.md) | UX hardening (audit 2026-07-22): U1 conferma delete (`InlineConfirm`), U2 icone SVG (`Icon`), U3 zoom a11y, U5–U12 refactor tema (token colori/font, dedup CSS, `.choice`/`.pill.neutral`, `LoadingState`/`EmptyState`, `.live-badge.off`, touch target). **U4 i18n IT+EN COMPLETO (2026-07-23):** chrome app tutta localizzata (nav+viewer+admin 12/12+editor 12/12), 1071 chiavi `SharedResource.resx`/`.en.resx`, switch runtime `?culture=en`. Contenuto editoriale dal DB resta IT. |
| [design/regole-ui-pagine-admin.md](design/regole-ui-pagine-admin.md) | **Regole di densità e uso delle pagine admin** (19 ago 2026): testata in una riga, prosa nei «?», altezza misurata (e le due unità dello zoom), tabelle lunghe, scritture che si accumulano, comandi che non saltano. Nate dai tre giri su accordi / struttura / ACC. In coda la **ricognizione misurata di tutte le pagine di lavoro** (chi aderisce, chi no e cosa le manca) in ordine di guadagno. |

## Feature — `feature/` 🔵
Una scheda per feature/fix non banale: stato di partenza rilevato, design, passi, **verifica live** ed esito.
Le lezioni riusabili finiscono anche nelle memorie; qui resta il perché delle scelte.
| File | Scopo |
|---|---|
| [feature/2026-07-29-toc-editor.md](feature/2026-07-29-toc-editor.md) | TOC laterale sezioni negli editor (menu di navigazione sticky, rail azioni, UX lock). |
| [feature/2026-07-30-stampa-documenti.md](feature/2026-07-30-stampa-documenti.md) | **Stampa dei documenti** (`@media print`): foglio `vipi-print.css`, `PrintMeta`, tasto Stampa, apertura dei `<details>`, scala tipografica da carta, mappe AoR ridimensionate, dati live esclusi. Include il fix delle larghezze di colonna dei coordinamenti (schermo **e** stampa). |
| [feature/2026-07-30-pill-stato-dopo-publish.md](feature/2026-07-30-pill-stato-dopo-publish.md) | «Bozza vN» dopo «Pubblica ora»: callback `Published` di `ReleasePanel` + etichetta «rilascio #N»; in coda il fix della **chiave di release ACC** che ignorava la radice dell'albero. |
| [feature/2026-08-03-aree-regolamentate-hardening.md](feature/2026-08-03-aree-regolamentate-hardening.md) | **Aree regolamentate**: categoria di import `SpecialAreas` (escludere = congelare, non «manuale»), dettaglio shape saltato quando è già in archivio, rilievo «Area regolamentata dangling» + marcatura nell'editor; poi picker scopribile e **appartenenza multi-ACC** (`SpecialAreaCenter`: la R49 «Zita» è di LIRR *e* del militare, prima vinceva l'ultimo ACC alfabetico) e **aree estere solo su richiesta** per ACC (763 legami su 993 liberati). In coda: il default dei flag bool nuovi, che nasceva `false` anche sul reconciler Postgres. |
| [feature/2026-07-31-aor3d-leggibilita.md](feature/2026-07-31-aor3d-leggibilita.md) | **AoR 3D leggibile**: selettore «Altezza» ×0.25→×2 (default ×0.5), etichette da sprite a overlay HTML con declutter, chip settore condivise col 2D. Il link «Apri pagina» è rimosso in attesa di rilavorare la pagina dedicata (rotta viva). |
| [feature/2026-08-12-editor-trasferimenti-ux.md](feature/2026-08-12-editor-trasferimenti-ux.md) | ✅ **CHIUSO, verifica live eseguita.** **Editor trasferimenti**: l'editing esce dalla riga ed entra nel **pannello a destra** (`.xfe-layout`, il pattern che Permessi/Incarichi/Struttura già usavano e questa pagina no). Nove bottoni-icona per riga → tre; tre colonne di condizione → una; tre bottoni «Gruppo» → Duplica/Modifica/Elimina; **51 stili inline → 0** (più il `<style>` nel markup: la CSP può passare in enforcement). Dieci QoL: trascinamento, duplica gruppo, modifica in blocco del ricevente, filtro «senza ricevente», avvisi di modifiche non salvate e lock in scadenza, anteprima sempre accesa, copia righe da un altro gruppo, ordinamento della vista, tasti. |
| [feature/2026-08-12-editor-trasferimenti-rifiniture.md](feature/2026-08-12-editor-trasferimenti-rifiniture.md) | ✅ **CHIUSO, verifica live eseguita.** **Il costo per gesto, la tastiera, l'annulla, il blocco.** Secondo giro sull'editor a tre colonne, con gli attriti di nuovo **contati nel sorgente**. Il più caro era un debito contratto dal giro precedente: ogni salvataggio costava **8 query** (due caricavano tutti i flussi dell'ACC), e da quando si scrive in cella si paga a ogni Invio — il contesto delle frasi ora si rifa solo sulle scritture di **gruppo**, e salvare una casella costa **1 query**. Il picker a digitazione era scritto **sei volte** e nessuna copia prendeva i tasti: estratto in `TypeaheadPicker` con frecce, Invio ed Esc. **Annulla** dopo un'eliminazione — e il punto non è il tasto ma cosa torna: ricostruire un gruppo con `AddPointAsync` appiattirebbe l'outline in silenzio, quindi il ripristino ha una via propria che porta gruppo, profondità e ordine, con gli invarianti rivalidati. **Blocco esteso** a livello (stessa sintassi della cella), condizione ed eliminazione — tre metodi distinti perché il ricevente si propaga al gruppo e gli altri no. Più ordinamento per intestazione, «+ riga» dall'elenco e pannello che si ritira. ⚠️ Tre difetti visti **solo** a schermo, tutti della stessa famiglia: `Text="form.NextText"` senza `@` passava la **stringa letterale** (la trappola del runbook, §7), la tendina di ordinamento si mostrava **vuota** con una preferenza da-solo-elenco, ed etichette e suggerimenti si toccavano. |
| [feature/2026-08-12-editor-trasferimenti-tre-colonne.md](feature/2026-08-12-editor-trasferimenti-tre-colonne.md) | ✅ **CHIUSO, verifica live eseguita.** **L'editor trasferimenti a tre colonne.** Aperto da un giudizio d'uso («ancora scomoda e poco fluida»), con gli attriti **contati nel sorgente** prima di proporre: la pagina rendeva *tutti* i gruppi dell'ACC uno sotto l'altro dietro tre livelli di collasso richiusi a ogni cambio ACC — tre clic per arrivare a una riga, ogni volta. Ora **navigatore · riquadro di lavoro · pannello**, ognuna col proprio scorrimento; il gruppo è una **foglia** dell'albero, non un livello da aprire. Vista **Albero ⇄ Elenco** (l'elenco mostra tutte le righe dell'ACC con settore/aeroporto/tipo come colonne: è il modo di *rivedere*) con **una** tabella sola per entrambe. **CoP, livello e ricevente si scrivono in cella** — Invio scende, Tab passa, Esc annulla — e il livello si rilegge con `LevelFormatting.Parse`, il cui contratto è un round-trip provato su 102 casi generati. Stato in **URL** (un gruppo si linka, un F5 non azzera), preferenze di vista in `localStorage`. ⚠️ Chiuso un difetto che c'era da prima: il filtro «senza ricevente» si accendeva, contava giusto e **non filtrava nulla**. Tre difetti visti solo a schermo: la pagina scorreva di 148 px perché l'altezza «schermo meno chrome» in CSS non è esprimibile (ora la misura `vipiFitViewport`); il navigatore si srotolava per 2174 px a schermo stretto; aprire una cella spostava le colonne di ~100 px (larghezza **intrinseca** dell'`<input>`). |
| [feature/2026-08-12-trasferimenti-gruppi-e-salva.md](feature/2026-08-12-trasferimenti-gruppi-e-salva.md) | ✅ **CHIUSO, verifica live eseguita.** Due difetti riportati guardando la pagina rifatta. **1.** In tabella non si vedeva quale condizione fosse eccezione o alternativa di quale: le righe di un gruppo si distinguevano solo per una velatura al 14% uguale per ogni gruppo. Ora il blocco si disegna (guida verticale a tinte alternate, bordi che aprono e chiudono, pill «⑂ n varianti» sulla capofila) e la colonna Condizione dice il **ruolo**: «se X», pill «negli altri casi», «↳ eccezione di: X» col rientro spostato dalla cella del CoP a quella della condizione, dov'è nella vista pubblica. Il padre lo legge `CoordinationDerivation.ParentOf`, estratto da `ConditionChain`: una risalita sola. **2.** Il Salva del pannello non compariva in **nessuna** posizione di scorrimento (1426 px di contenuto in 904 disponibili): pannello diviso in testata · corpo · piede, scorre solo il corpo. Sotto i 1080 px `vipiRevealPanel` porta il pannello in vista se il suo piede non lo è. **In coda**: la scheda intera non ci stava lo stesso (885 px di campi in 501 su un portatile) — le sezioni «Trasferimento» e «Condizione» ora partono **richiuse quando sono vuote**, con il riassunto di cosa contengono accanto al titolo, e la scheda passa a **502 px**: zero fuori dalla vista. **Seconda coda**: su una riga **esistente** il corpo arriva a 1324 px e le sei azioni sulla riga restavano fuori schermo (823 px nascosti su un portatile) — scorrere la pagina non le raggiunge, perché a scorrere è il corpo; ora sono un **menù del piede**, che non scorre. |
| [feature/2026-08-12-varianti-a-livelli.md](feature/2026-08-12-varianti-a-livelli.md) | ✅ **CHIUSO, verifica live eseguita.** **Varianti a livelli**: il gruppo di varianti diventa un **outline** — le alternative di primo livello sono **pari-grado** (pista 07 · pista 25, nessuna è lo standard dell'altra), ognuna può avere eccezioni **annidate a profondità libera** («area attiva» e, dentro, «area attiva e di notte»), più le righe che **scavalcano** le alternative («di notte, qualunque pista»). `IsOtherwise` rimosso prima del merge, quindi a dati zero. In tabella si legge il delta, nella frase la catena cumulata. ⚠️ Chi sposta una riga deve spostarne il **sottoalbero**: lasciare indietro le eccezioni le riassegna senza alcun errore. |
| [feature/2026-08-11-trasferimenti-acc-app.md](feature/2026-08-11-trasferimenti-acc-app.md) | ✅ **CHIUSO, verifica live eseguita** (la parte «varianti» è superata dalla scheda del 12 ago). **Trasferimenti ACC↔APP**: livello **autorizzato** e livello **al trasferimento** separati (oggi il modello ha un evento solo), punto di trasferimento (confine AoR / fix / libero) distinto dalle comunicazioni, restrizione di velocità, **gruppo di varianti** con riga «negli altri casi» (le alternative per condizione oggi sono righe scollegate), e la sezione estesa che mostra **tutto** ciò che entra o esce da un ente (il passo 2 della derivazione filtrava solo arrivi da CTR). |

## Decisioni architetturali — `adr/` 🟢
| File | Scopo |
|---|---|
| [adr/adr-0001-scelte-architetturali-fondanti.md](adr/adr-0001-scelte-architetturali-fondanti.md) | Fondamenti (stack, layer, persistenza, AoR, polling). Emendato da ADR-0002. |
| [adr/adr-0002-integrazione-e-autenticazione-portabile.md](adr/adr-0002-integrazione-e-autenticazione-portabile.md) | Integrazione come RCL + identità portabile (`ICurrentUserProvider`, scenari A/B/C). |
| [adr/adr-0003-trasporto-live-sse.md](adr/adr-0003-trasporto-live-sse.md) | Trasporto push live = SSE. |
| [adr/adr-0004-configurazione-divisione-e-admin.md](adr/adr-0004-configurazione-divisione-e-admin.md) | Configurazione divisione + derivazione codici admin. |
| [adr/adr-0005-superficie-modulo-e-isolamento.md](adr/adr-0005-superficie-modulo-e-isolamento.md) | Superficie del modulo + isolamento CSS/JS. |
| [adr/adr-0006-indipendenza-sorgente-dati-e-policy-import.md](adr/adr-0006-indipendenza-sorgente-dati-e-policy-import.md) | Indipendenza dalla sorgente + policy di import (+ nota Round 20: fonte unica). |
| [adr/adr-0007-produzione-persistenza-e-scala.md](adr/adr-0007-produzione-persistenza-e-scala.md) | Produzione: tampone WAL SQLite ora + cutover Postgres pianificato + scala Blazor + guardia identità dev. Aggiornamenti: 30-lug (reconciler + drift probe D1-bis), **1-ago (D4: MySQL solo su net8)**. |

## Refactor — `refactor/` 🟢
Asse di revisione strutturale post round ~23-34 (doc di area 01→10, **tutti eseguiti**). Ordine di studio bottom-up.
| File | Scopo |
|---|---|
| [refactor/00-overview.md](refactor/00-overview.md) | Piano: DAG dipendenze, principi, indice dei doc di area, ordine di studio. |
| [refactor/REFACTOR-PROCESS.md](refactor/REFACTOR-PROCESS.md) | Runbook anti-vibecoding per i refactor (ciclo Fase 0→4, gate «carta prima di codice»). |
| [refactor/01-import-infra-condivisa.md](refactor/01-import-infra-condivisa.md) | Infrastruttura di import condivisa. |
| [refactor/02-import-acc-e-settori.md](refactor/02-import-acc-e-settori.md) | Import ACC e settori. |
| [refactor/03-import-aeroporti-e-settori.md](refactor/03-import-aeroporti-e-settori.md) | Import aeroporti e settori. |
| [refactor/04-import-github.md](refactor/04-import-github.md) | Import da GitHub (sectorfile Aurora: SID, ecc.). |
| [refactor/05-import-confinanti.md](refactor/05-import-confinanti.md) | Import ACC confinanti/esteri. |
| [refactor/06-gerarchia.md](refactor/06-gerarchia.md) | Gerarchia di copertura (padri per callsign, cross-ACC). |
| [refactor/07-trasferimenti.md](refactor/07-trasferimenti.md) | Coordinamenti/trasferimenti (sorvoli, vLOA in stile ACC+EN). |
| [refactor/08-modello-documento-ed-editing.md](refactor/08-modello-documento-ed-editing.md) | Modello `Document`+`DocumentVersion` unificato per tutti e 4 i tipi + editing. |
| [refactor/09-flusso-pubblicazione.md](refactor/09-flusso-pubblicazione.md) | Flusso di pubblicazione generico (registry `IReleaseTarget`/`IDocKindRoutes`). |
| [refactor/10-snapshot-totale-e-rendermode.md](refactor/10-snapshot-totale-e-rendermode.md) | Snapshot totale al publish + `RenderMode` per sezione; visibilità pubblica = release effettiva. **Merged.** |
| [refactor/11-uniformita-tre-documenti.md](refactor/11-uniformita-tre-documenti.md) | Uniformità vIPI ACC / vIPI APP / vLOA fra editor, bozza e pubblica (audit 2026-07-30, P1→P9): chiave di sezione univoca, resa editoriale condivisa, `DocumentSection.IsHidden` e `BeforeParentBody` versionati, fallback a pubblica frozen, superficie APP = non remotizzati, stato iniziale di apertura delle sezioni. Include **§3bis: non-problemi verificati**, da non «aggiustare». |
| [refactor/12-vista-live-unificata.md](refactor/12-vista-live-unificata.md) | **Vista live unificata per callsign** (2026-07-31): una pagina sola `/vsop/live[/{callsign}]` al posto di `AccLivePage`/`AppLivePage`, descrittori per tipo di ente (`ILiveStationKind`) con **TWR/GND/DEL** finalmente coperti, postazione dalla connessione IVAO senza selettore, trasferimenti per mittente effettivo. Rimosse anche le `Ridotta*` morte. |

## Storia — `history/` ⚪
| File | Scopo |
|---|---|
| [history/rounds.md](history/rounds.md) | **Changelog cronologico** dei round (R5→R34) + asse refactor 01→10, retention/fix pubblicazione e feature 2026-07 (confinanti, QoL editor trasferimenti, condizione operativa pista/area). |
| [history/handoff-round5.md](history/handoff-round5.md) | Handoff di chiusura del Round 5 (fusione Settore/Posizione). |
| [history/handoff-round22.md](history/handoff-round22.md) | Handoff di sessione Round 22 (shape tonda TWR + coord aeroporto + rifiniture trasferimenti/AOR). |
| [history/handoff-coordinamenti-fasi-3-4.md](history/handoff-coordinamenti-fasi-3-4.md) | Handoff coordinamenti/trasferimenti (fasi 3-4, refactor 07). |
| [history/piano-round20.md](history/piano-round20.md) | Piano esecutivo del Round 20 (fonte unica cataloghi). |
| [history/review-flusso-gap.md](history/review-flusso-gap.md) | Analisi flusso vs documenti (decisioni round 4). |
| [history/audit-2026-07-14-correttezza-fonti-dati.md](history/audit-2026-07-14-correttezza-fonti-dati.md) | Audit senior correttezza + fonti-dati multiple: findings A1–A4/B1–B5, falsi positivi, fix. |
| [history/audit-2026-07-22-criticita-full-stack.md](history/audit-2026-07-22-criticita-full-stack.md) | Audit full-stack (back/front/DB): 15 criticità con severità + piano a fasi + Fase 1 (health-check migrazioni, osservabilità import, rete test bUnit/E2E). |
| [history/audit-2026-07-30-concorrenza-e-ridondanze.md](history/audit-2026-07-30-concorrenza-e-ridondanze.md) | Audit concorrenza/codice morto/ridondanze: 8 fix di race condition, import SID rotto in silenzio, 450 righe morte, 4 estrazioni + bug Razor `v@r.…` trovato in verifica live. |
| [history/audit-2026-08-11-crepe-full-stack.md](history/audit-2026-08-11-crepe-full-stack.md) | Audit full-stack **con esito**: 34 voci (2 bloccanti, 5 alte, 17 medie, 11 di debito), 23 chiuse, 3 ribaltate dalla misura, 5 rimandate con la ragione. Carta scritta prima di toccare il codice, esito in fondo. La CI era rossa sul ramo del doc 13 e su net8 girava 1 progetto di test su 7. |

---
**Nota:** i commenti nel codice sorgente (`.cs`/`.razor`) citano i documenti per **nome e sezione** in forma informale (es. «modello-dati §9.12», «ADR-0001 D5»), non come link a percorso.
