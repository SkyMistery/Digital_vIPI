# d16 — Allineamento delle sezioni fra documenti (terzo giro di revisione)

**Domanda del committente**: «verifica su quali sezioni o sottosezioni i documenti non sono allineati, se questa cosa
non è prevista dal design».

**Misura**: copia del DB di produzione `vipi_rev3` (MariaDB 11.4, porta 3399), letta solo con SELECT (query in
`query.sql`, alberi ricostruiti in Python da `dump/`: `analisi.py`, `riepilogo.py`, `lavoro_vs_pubblicata.py`,
`albero.py`). Codice: ramo `sito/lavori` @ 8247e1ea (= 1.46.5 in produzione). La copia è del 26-set pomeriggio
(ultima release #451 delle 16:15Z); il timbro `RiconcDoc:8247e1e` (26-set 16:50) dice che le passate d'avvio sono
già girate anche sulla copia, a vuoto: lo stato misurato è quello che la 1.46.5 lascia a regime.

**Perimetro misurato**: 86 documenti = 46 vIPI d'aeroporto, 17 vSOP militari, 18 vIPI APP non remotizzato,
4 vIPI ACC, 1 vLOA (nessun documento `AppMil`). Per ognuno: **versione di lavoro** (numero di versione più alto)
e **release in vigore** (`DocReleases.Status='Effective'`: 45 aeroporti, 17 militari, 11 APP, 1 ACC, 1 vLOA) più
l'unica release **programmata** (#187, LIBV_APP, 2610).

---

## 1. La regola (che cosa il design prevede)

Fonte: `SectionCatalog.cs` (registro per profilo), `DocumentBirth.cs` (nascita), `EfDocumentMaintenance.cs`
(passate d'avvio), `SectionOrdering.cs`, `SezioniComuni.cs`, `TitoliDiCatalogo.cs`, `AirportLegacySections.cs`,
carte `2026-08-26-aeroporto-a-sezioni`, `2026-09-04-sezioni-mobili`, `2026-09-06-vsop-sezioni-sod`,
`2026-09-20-star-e-altri-riferimenti`, `2026-09-03-documenti-uniti`, memorie citate nel compito.

| Aspetto | Regola |
|---|---|
| Sezioni **obbligatorie** | tutte quelle del profilo (`IsFixed`), a qualunque profondità; nascono con il documento (`DocumentBirth.Semina`, ricorsiva) e ai documenti già scritti le porta `AddMissingCatalogSectionsAsync` (solo **ultima versione**, posizione di catalogo, gruppo rinumerato). Non si rinominano né si eliminano dall'editor (vincolo solo in UI, `IsMandatory`: vedi fine §2). |
| **Facoltative / nascoste** | nessuna sezione è facoltativa; si **nascondono** (`IsHidden`) a mano o dalla scheda «sezioni comuni» delle unioni. Nascono nascoste solo `stars` (civile e militare, `BornHidden`, migrazione `StarNascosteDiDefault`). |
| **Pubblico** | default di nascita `Pilots` su 12 sezioni del vSOP (SOD); `ApplyCatalogAudienceDefaultsAsync` porta il default solo dove c'è ancora `Both`. |
| **Libere** | `custom:{guid8}`; si aggiungono ovunque fino a `MaxDepth = 5`; dal 4-set si **spostano anche in altri gruppi** (solo le libere, `MoveSectionToParentAsync`). |
| **Ordine** | il catalogo lo decide alla nascita; poi è **libero fra fratelli** (frecce/trascinamento, `SectionOrdering.OffsetsFromStandard` mostra lo scostamento). Le sezioni di catalogo **non cambiano padre** se non con una passata d'avvio dedicata. |
| **Titoli** | nel DB nella lingua che il documento aveva alla nascita; a view-time `TitoliDiCatalogo` impone il titolo di catalogo nella lingua di lettura (anche nelle release). Titolo diverso nel DB = **interno**, non visibile. |
| **RenderMode** | `weather`/`validity` sempre Live; aeroporto civile: `sids` e `stars` nascono Live (`EfAirportRepository.BornLive`, carta STAR «nasce Live come le SID»); tutto il resto Frozen. |
| **Release** | gli snapshot **non si riscrivono** (doc 13 §9): una struttura cambiata compare in pubblico solo alla ripubblicazione; la lista «Da fare» apre `ReleaseDrift` finché non si ripubblica. Reti a view-time per snapshot vecchi: `AirportLegacySections.ForView` (aeroporto) e `AccDocumentAssembler.SectionsOf` (ACC, accoda in fondo). |
| **Dipendenti dai dati** | nessuna sezione nasce o sparisce in base ai dati: STAR esiste sempre (nascosta), LVP e Regole piste esistono anche senza righe in anagrafica. Le sezioni vuote **si mostrano** (`VipiViewService.Map`: «Nessuna sezione viene scartata», doc 11 §3b). |

### Profili (ordine di catalogo; `>` = figlia; (H) nasce nascosta; (P) nasce «per i piloti»)

- **Airport**: weather · transition · frequencies · runways > runwayrules · sids · stars (H) · operationaltechnique · lvp ·
  charts > {aerodrome, iac, sid, star, vfr} · validity. (20 sezioni)
- **AirportMil**: weather · generaldata > {navaids, frequencies, diversion (P), runways > runways:thresholds (P), runwayrules,
  sids, stars (H), transition, callsigns (P), airportlayout, parkings (P) > parkings:apronflow (P)} · groundprocedures >
  {enginestart (P), taxiing, arming (P)} · flightprocedures > {takeoff (P), arrivalrestrictions (P), circuitrestrictions, sfo,
  commfail, gca, vfrjet > vfrjet:points (P), ifrsignificant (P), gat} · lvp · regulated > {operationaltechnique >
  {departureprocedures > {:vfr, :ifr}, arrivalprocedures > {:vfr, :ifr}}, lowlevel (P)} · charts > {5} · validity. (47 sezioni)
- **App** (= AppMil, = AccAppBlock): separations · configurations · aor · frequencies · minima · trafficmanagement >
  {trafficmanagement:ifr, vfr} · coordination · operatingtechnique · regulated · operationaltechnique · validity. (13)
- **AccAerovia**: separations · configurations · aor · frequencies · minima · coordination · aor-mil · regulated · aor-fss ·
  operationaltechnique · validity. (11)
- **Vloa**: purpose · aor · frequencies · operationaltechnique · coordination > {coordination:out, coordination:in} ·
  regulated · validity. (9)

---

## 2. NON PREVISTI (diventano findings)

| # | Tipo | Sezione (chiave / titolo) | Differenza | Documenti | Dove | Causa nel codice | Finding |
|---|---|---|---|---|---|---|---|
| N1 | APP | **tutto l'albero**: manca `trafficmanagement` («Gestione del traffico») con `trafficmanagement:ifr`, manca `operatingtechnique` («Tecnica operativa»), `vfr` è RADICE e visibile, mancano la sotto-sezione libera «Coordinamenti per GCA» e la tabella delle separazioni; `configurations`/`operationaltechnique`/`vfr` visibili invece che nascoste | LIBV_APP #17 | release **programmata #187** (2610, creata 9-set, in vigore dal **1-ott-2026 00:00Z**) contro la release in vigore #423 (25-set) | una release programmata al ciclo successivo non viene mai superata da un «Pubblica ora» al ciclo corrente; al rollover vince per data. `EfReleaseRepository.RecomputeStatuses` L288-302 / `GetEffectiveAsync` L144-149; `ReleaseService.PublishNowAsync` L332-383; `ImpactDriftUseCase` L263-287 (confronta solo con la in vigore); `ProssimoAiracService` L91/L116 (lo conta «già programmato») | d16-01 (S2) |
| N2 | APP | `custom:7de485cb` «Note» sotto `vfr` | `Depth=1` ma sta al livello 2 (profondità memorizzata incoerente) | LIRZ_APP #80 (Perugia Approach) | lavoro (bozza, mai pubblicata) | `ReparentAppTrafficManagementAsync` sposta `vfr` a profondità 1 ma non riscrive la `Depth` delle sue figlie (EfDocumentMaintenance L662-663; gemelli L745-746, L860-862, L922-924, L1006-1008). Conseguenza: dopo la prima pubblicazione `CreateDraftAsync` (EfEditingRepository L206-211) solleva KeyNotFoundException | d16-02 (S2) |
| N3 | APP | «Gestione del traffico» **due volte**: `custom:b5dcc6aa` (libera, con «IFR» libera > «Partenza»/«Arrivo», 10 blocchi) e `trafficmanagement` (catalogo, con `trafficmanagement:ifr` VUOTA e `vfr`) | doppione di titolo, contenuto IFR fuori dalla IFR di catalogo | LIRZ_APP #80 | lavoro (bozza) | la passata del 15-set cerca il contenitore solo per chiave (EfDocumentMaintenance L641-642) e non prevede l'IFR già scritto a mano | d16-03 (S3) |
| N4 | vIPI ACC (blocco APP) | `trafficmanagement` > `trafficmanagement:ifr` e `vfr` **entrambe vuote**, e tre sezioni libere di pari livello «Traffico IFR in Arrivo a LIBD», «TRAFFICO IFR in Arrivo a LIBR», «Traffico IFR in Partenza da LIBD e LIBR» | contenuto IFR fuori dalla IFR di catalogo; IFR/VFR vuote mostrate (le vuote non si potano) | LIBB (#13) blocco «Brindisi CS0» | **pubblicata** (#342) e lavoro | `RiparentaVfrDeiBlocchiAppAccAsync` sposta solo `vfr` (L691-764); IFR/«Tecnica operativa» nascono vuote da `AddMissing` | d16-03 (S3) |
| N5 | vIPI aeroporto | `stars` «STAR» | RenderMode **Frozen** (le SID dello stesso documento sono Live) | 45 su 46 (tutte tranne LIBG #102) | lavoro | `AddMissingCatalogSectionsAsync` L559 usa solo `IsAlwaysLive`; la nascita usa `BornLive` (EfAirportRepository L638-641) che include `stars`. La carta STAR (§4) dice «Nasce Live come le SID … due nascite diverse sarebbero due comportamenti da spiegare» | d16-04 (S4) |
| N6 | vLOA | `validity` «Validity and Revision» | RenderMode Frozen (sempre-live) | vLOA #65 | lavoro e pubblicata | `VloaStructureSeeder.AddSection` L22-31 non imposta `RenderMode` (né `Audience`/`IsHidden`); `DocumentBirth` sì | d16-05 (S4) |
| N7 | vIPI aeroporto (EN) | `runwayrules`, `charts:aerodrome`, `charts:iac` | titolo nel DB in italiano su documento inglese, mentre le radici sono state riscritte in inglese | LIRP #55, LIRS #86, LIRL #96 | lavoro (e release #397, #303, #330) | `ReconcileAirportSectionKeysAsync` riscrive il titolo di catalogo solo sulle RADICI (L1173-1175, L1220-1224); la riscrittura apre una deriva «da ripubblicare» spuria su LIRL #96 (impatto #128 aperto) perché la firma di deriva è per titolo (ReleaseService L757-764) | d16-08 (S4) |
| N8 | vSOP militare | `stars` | presente solo nella BOZZA v7, assente dalla versione pubblicata v6 (da cui `CreateDraftAsync` copierebbe dopo uno «Scarta bozza») | LIBA #69 | versioni | le passate d'avvio toccano solo l'ultima versione (EfDocumentMaintenance L418-420, e le Reparent*), `CreateDraftAsync` copia da `CurrentVersionId` (EfEditingRepository L180) e il gate per build non le rigira fino alla consegna successiva | d16-07 (S4) |

Non previsti **nel codice ma senza istanza nei dati** (latenti, stessi finding): `SeminaFiglie` della vIPI ACC
(EfEditingRepository L391-401) scrive sempre il titolo italiano e non applica `Audience`/`BornHidden` (d16-05);
`SectionMoveTargets.Per` ha ancora il tetto 3 mentre `MaxDepth` è 5 dal 16-set (d16-06); somma `cambiamenti` senza
`traffico` (d16-09). Rinomina ed eliminazione di una sezione di catalogo sono bloccate solo in UI (`IsMandatory`), non
nei repository: annotato, non è un finding perché oggi nessuna porta lo aggira.

---

## 3. PREVISTI dal design

### 3a. vIPI d'aeroporto (46 documenti)

| Sezione | Differenza | Documenti | Dove | Regola che lo prevede |
|---|---|---|---|---|
| `charts` (+ figlie) | nascosta | 42/46 (tutti tranne LIME #32, LICA #44, LICT #73, LIBG #102); figlie nascoste anche in LICD #15, LIRP #55, LIRS #86, LIRL #96 | lavoro e pubblicata, stessa scelta in tutte le versioni | scelta editoriale (nessun codice nasconde `charts`; stato identico nelle versioni archiviate) |
| `runwayrules` | nascosta | 42/46 (tutti tranne LIME #32, LIBR #50, LICT #73, LIBG #102) | idem | scelta editoriale |
| `lvp` | nascosta | LIRF, LICC, LIRA, LIRI, LIRJ, LIRN, LIRQ, LIRZ, LIRS, LIRL | idem | scelta editoriale |
| `operationaltechnique` | nascosta | LIPH, LIPK, LIPX, LICR, LIEA, LIRN, LIRS, LIRL | idem | scelta editoriale |
| weather, transition, frequencies, runways, sids, validity | nascoste | LIRP #55, LIRS #86, LIRL #96 (e `sids` LIMW #64, `transition` LIRJ #76) | idem | **unioni** (§11 documenti uniti): il civile cede le comuni al vSOP del campo; su LIRS e LIRL la vIPI civile ha **tutte** le sezioni di catalogo nascoste |
| `stars` | manca nella release | LIPR, LIPZ, LIMW, LIRA, LICT, LIRJ, LIRS, LIRL | pubblicata | release anteriore al 20-set, non si riscrive; nasce nascosta ⇒ nessuna differenza a schermo; «da ripubblicare» aperto |
| `lvp` | manca nella release | LIPR #26, LIPZ #30, LIMW #64 | pubblicata (release dell'8-set) | idem; nessun minimo LVP in anagrafica per i tre scali ⇒ niente di perso; «da ripubblicare» aperto dal 12-set (impatti 93/101/104) |
| `runwayrules` | radice invece che figlia di `runways` | LIPR #26, LIPZ #30, LIMW #64 | pubblicata | idem (passata del 12-set); nascosta in tutte e tre |
| titoli di catalogo | nella lingua di nascita | LIRP #55, LIRS #86, LIRL #96 (radici EN nel lavoro, IT nelle release) | lavoro/pubblicata | `TitoliDiCatalogo` a view-time; resta interno (vedi N7 per la parte non prevista) |
| sezioni libere | 1-35 per documento (LIRF 34, LIMC 21, LIRA 11, LICR 6…) | vedi `riep` | lavoro e pubblicata | contenuto libero, anche sotto sezioni di catalogo |
| «Nuova sezione» vuota sotto `operationaltechnique` | libera senza titolo scelto | LICC #47, LIBD #48 | solo bozza | lavoro in corso (deriva «da ripubblicare» aperta, impatti 150/158) |
| RenderMode | `runwayrules` Live in 33, `frequencies` Live in LIBC/LIBG, `lvp` Live in LIRI/LIRJ/LIRZ | lavoro | interruttore Live/Frozen dell'editor |

### 3b. vSOP militare (17 documenti)

| Sezione | Differenza | Documenti | Regola |
|---|---|---|---|
| tutte le 47 | presenti, sotto il padre giusto, profondità coerente, pubblico = default SOD | 17/17 lavoro, 17/17 pubblicata | catalogo + passate |
| `stars` | manca nella release | LIPL, LIBA, LICT, LIED, LIRS, LIPS, LIPA, LIRL, LIPC, LIRM | release anteriori al 20-set; nascosta; «da ripubblicare» aperto |
| `groundprocedures` | figlie in ordine taxiing, enginestart, arming | LIBV #9 (lavoro e pubblicata) | riordino fra fratelli (scostamento mostrato dall'editor) |
| titoli di catalogo | italiani su documenti inglesi (tutti e 17 sono `En`) | 17/17 | nati `It` (MilitaryDocuments, carta §1d) e passati a `En`; `TitoliDiCatalogo` a view-time |
| nascoste | arming (LIRE, LIRL), gca (LIED, LIRE, LIMN), lowlevel (LIPA, LIRM), regulated (LIRE), charts:star (LIPS), lvp/runwayrules/runways (LIRP #56, unione) | editoriale / unione |
| «QRA / Scramble» libera | alla **radice** | LIBV, LICT («QRA / Scrambles»), LIRS, LIPS; sotto `flightprocedures` in LIBA («QRA/Scramble») | `RemoveMilQraSectionsAsync` l'ha resa libera sotto `flightprocedures` (misurato sulle release di LIBV fino al 12-set); fra il 12 e il 18-set è stata **spostata a mano** (sezioni mobili, 4-set) |
| altre libere | Combat Departure (LIBV), HEMS/46° ICTC/Paratroopers (LIRP), LL routes (LIED), LVTO sotto lvp (LIRE), SAR Alert (LIPC), Working zones (LIBN, LIRL)… | contenuto libero (le «code per campo» della carta) |

### 3c. vIPI APP non remotizzato (18 documenti, 11 pubblicati)

| Sezione | Differenza | Documenti | Regola |
|---|---|---|---|
| tutte le 13 | presenti e al posto giusto | 18/18 lavoro, 11/11 pubblicata | catalogo + passata del 15-set |
| nascoste | configurations (11), minima (4), operationaltechnique (4), separations (4), operatingtechnique (2), regulated (2), vfr (LIBV_APP, LIBN_APP), validity (LIRE_APP) | editoriale |
| `coordination` | pubblico «Controllers» | LIBV_APP #17 | scelta a mano |
| `trafficmanagement:ifr` / `vfr` / `operatingtechnique` vuote | intestazioni senza contenuto, mostrate | IFR vuota in 11/18 (LIBV, LIPY, LIPA, LIEE, LIBN, LIBA, LIRZ, LIRS, LIRM, LIRL, LIPH; pubblicate LIBV, LIBN, LIBA, LIRL); «Tecnica operativa» vuota in 13/18; piene in LICC, LIBG, LIRP, LICJ, LIRE, LICT, LIBP | nascono vuote come «Procedure generali» (catalogo L205-219); le vuote non si potano (`VipiViewService.Map`) |
| titoli | italiani su documenti inglesi | LIRE_APP #92, LIRL_APP #95 | `TitoliDiCatalogo` |
| libere | «Coordinamenti per GCA» (LIBV_APP, LIBN_APP), «S/VFR» sotto trafficmanagement (LIRE_APP, LIBG_APP), Catania/Comiso/Sigonella sotto IFR, VFR e Tecnica operativa (LICC_APP)… | contenuto libero |
| 7 documenti mai pubblicati | 28, 29, 62, 80, 88, 93, 100 | bozze |

### 3d. vIPI ACC (4 documenti, 1 pubblicato)

| Sezione | Differenza | Documenti | Regola |
|---|---|---|---|
| blocco Aerovia (11) e blocchi APP (13) | completi e in ordine | LIBB #13, LIMM #16, LIPP #23, LIRR #46 | catalogo per blocco |
| `minima` | nascosta | LIMM blocco «Milano APP» | editoriale |
| libere | Note sotto SCCAM/FIC, «Coordinamenti Civili/Militari», «Deroghe Occasionali», «Contingenze», «Voli VFR», «Radio Avaria» | LIBB | contenuto libero (ma vedi N4) |

### 3e. vLOA (1 documento)

Struttura completa (7 + 2 direzioni) in lavoro e pubblicata; unica differenza il RenderMode di `validity` (N6).

### 3f. Unioni (5)

LIRP (55+56), LIRS (86+85), LIRL (96+94), LIBG (102+103), Pratica (92+83). Le nascoste «comuni» sono coerenti con la
scheda (§11): nel civile si nasconde, nel militare si tiene; eccezioni scelte (LIRP: `sids` e `operationaltechnique`
visibili in entrambi, `runwayrules` nascosta in entrambi). LIBG e Pratica uniscono un APP: nessuna sezione comune per
costruzione (`SezioniComuni.Confrontabili`).

---

## 4. Controlli che non hanno trovato niente

- nessuna chiave fuori catalogo (`qra`, `airportextra`, `custom` storico): 0;
- nessun doppione di chiave di catalogo nello stesso documento o blocco: 0;
- nessuna sezione di catalogo sotto il padre sbagliato nelle versioni di lavoro: 0;
- `Order` duplicati fra fratelli: 0;
- profondità memorizzata incoerente: **1** su 6225 righe (N2), 0 nelle 76 release in vigore/programmate;
- pubblico diverso dal default di catalogo: solo LIBV_APP `coordination` (a mano);
- nessuna release in vigore con `stars` visibile.
