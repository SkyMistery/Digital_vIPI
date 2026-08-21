# HANDOFF — vIPI/vLOA Interactive

**Ultimo aggiornamento:** 22 agosto 2026 — **densità UI delle pagine admin**, sul ramo
`ui-trasferimenti-densita`, **non ancora in `main`**.
**Scopo:** dare a una nuova chat tutto il contesto per riprendere senza rileggere l'intera cronologia.

> ## 🧭 SI RIPARTE DA QUI (22 agosto 2026)
>
> **Il lavoro vivo è sul ramo `ui-trasferimenti-densita`**, allineato col remoto e **non fuso**. Per riprendere
> da freddo si leggono **due** file:
> [`docs/history/handoff-densita-ui.md`](docs/history/handoff-densita-ui.md) — dove siamo, il metodo, e da dove
> riparte la prossima pagina — e
> [`docs/design/regole-ui-pagine-admin.md`](docs/design/regole-ui-pagine-admin.md), le 124 regole già pagate più
> la ricognizione misurata di ogni pagina.
>
> In due righe: il giro riscrive **la forma** delle pagine di lavoro admin — niente cambia in modello, rotte o
> dati. **Nove pagine chiuse** (accordi, struttura, ACC, aeroporti, editor aeroporto, editor ACC, Confinanti,
> Versioni, **Permessi**). Versioni è costata due giri — prima la **sostanza** (la pagina lasciava eliminare un
> documento che un'altra persona stava editando), poi la densità: 1 664 → **900px**, il dettaglio fuori
> dall'elenco e i chip che contano. Permessi: **2 449 → 900**, le sei card di navigazione diventate una barra
> sola e completa, e l'elenco riorganizzato per **persona**.
>
> ⚠️ **La ricognizione di Permessi diceva 1 346px: era la misura a tabella VUOTA.** Le pagine che nel DB di
> sviluppo non hanno dati vanno **riempite prima di misurarle** — vale per quelle che restano.
>
> **La prossima è Sorgenti** (`/vsop/admin/sorgenti`, **1 235px**). Poi Audit, Diagnostica, Nuovo documento,
> Incarichi, editor APP/vLOA — l'ordine e il misurato stanno in `regole-ui-pagine-admin.md` §15.
>
> Cancello: `dotnet build Vipi.slnx -c Release --no-incremental` (**0 avvisi** — gli avvisi sono errori e
> `dotnet test` non li vede) e `dotnet test Vipi.slnx` verde su **entrambi** i TFM.
>
> Sotto resta lo stato del **18 agosto** (accordi di coordinamento, ramo `feature/accordi-coordinamento`) e
> quello del **15 agosto** (consegna a Ivao.It): valgono ancora per tutto ciò che non è quest'area.

> ## 🧭 DA DOVE SI RIPARTIVA IL 18 AGOSTO (accordi di coordinamento)
>
> **Il lavoro vivo è sul ramo `feature/accordi-coordinamento`**, allineato col remoto e **non fuso**. Per
> riprendere da freddo si legge **un** file:
> [`docs/history/handoff-accordi-coordinamento.md`](docs/history/handoff-accordi-coordinamento.md) — cosa c'è,
> cosa non va riscoperto a mani nude, cosa resta aperto.
>
> In due righe: `TransferFlow`/`TransferPoint` **non esistono più**, al loro posto un **accordo** fra due parti
> con due versi; e l'editor `/vsop/admin/trasferimenti` è stato rifatto sopra al modello — albero per
> **relazione** (`noi ⇄ loro`), **due versi sempre a vista**, creazione che chiede **solo i due enti**, tipo e
> aeroporti nella testata, **entrambi i lati obbligatori**.
>
> Cancello: `dotnet build Vipi.slnx -c Release --no-incremental` (0 avvisi) e `dotnet test Vipi.slnx`
> (**2581** verdi su net8 e net10).
>
> ⚠️ **Il `vipi.db` di sviluppo è il DB che va in produzione** ed è già travasato. **I suoi numeri non si
> scrivono nei documenti**: il committente lo modifica dal vivo dal proprio host sulla **5034** — mentre si
> chiudeva questo giro sono spariti due accordi — quindi si **misura** quando serve, non si cita. Tutte le
> prove di scrittura girano su una **copia** nello scratchpad; per la verifica live si usa un'altra porta.
>
> L'unica cosa che al 18 agosto era ancora vera per costruzione: **nessun accordo bilaterale**, cioè tutte le
> clausole in un verso solo. Il primo reciproco lo scrive chi usa «unisci i due versi» o «+ clausola» nel
> blocco entrante.
>
> Sotto resta lo stato del **15 agosto**, che riguarda la consegna a Ivao.It e vale ancora per tutto ciò che
> non è quest'area.

> ## 🧭 DA DOVE SI RIPARTIVA IL 15 AGOSTO (consegna a Ivao.It)
>
> **La consegna a Ivao.It è in corso, ed è lì che sta il lavoro.** Il database è stato caricato sul loro
> server; il pacchetto dell'applicazione va su via **FTP/FileZilla**, non da console — procedura in
> [`deploy/atc-ivao/LEGGIMI-FTP.md`](deploy/atc-ivao/LEGGIMI-FTP.md).
>
> **Pacchetto:** `artifacts/publish/vipi-linux-x64-mariadb-20260815.zip`, 48,1 MB, 407 file, self-contained
> net8, sha256 `28063F5E513A052C036593078FD2E3053165B174859246843CF56537B01C78EE`. Costruito da `main`
> dopo i due merge, con `Release --no-incremental` a **0 warning** e **2465 test verdi** (net8 + net10).
>
> ⚠️ **La trappola M14 si è materializzata proprio qui, ed è utile saperlo per il prossimo pacchetto.** Il
> restore di `publish -r linux-x64` **rivaluta le wildcard** `8.0.*`/`10.0.*` e riscrive i lock dei soli
> progetti `src/`: il pacchetto è nato con EF Core **8.0.30** mentre i progetti di test restavano a 8.0.29,
> e la corsa successiva della suite è morta con `CS1705` («uses a higher version than referenced
> assembly»). Rimesso in riga con `dotnet restore Vipi.slnx -p:RestoreForceEvaluate=true`, che aggiorna
> **tutti** i lock insieme, poi ricompilato, ri-testato e ripubblicato. Regola: dopo un `publish` con RID,
> guardare `git status` sui `packages.lock.json` **prima** di credere ai numeri della suite.
> ⚠️ Il pacchetto del 9 agosto e quello del 5 vanno **ritirati**: il primo non ha trasferimenti né audit
> database, il secondo non parla proprio MariaDB.
>
> ⚠️ **Chi carica non fa partire.** L'FTP non trasporta il bit di esecuzione e non installa servizi: serve
> qualcuno con shell per `chmod +x Vipi.Host`, `vipi.service` in systemd e nginx col WebSocket.
>
> ⚠️ **Se il loro database viene dal `.sql` del 9 agosto**, questa build applica da sé al primo avvio la
> migrazione `20260814092329_EnumLengthsAndDropUnusedTokens` — servono ALTER e DROP sul database.
>
> **⚠️ Un solo ramo resta pronto e non fuso.**
>
> ✅ **`feature/trasferimenti-acc-app` è stata fusa il 15 agosto** (72 commit), insieme a
> `fix/audit-database-14ago`. Nella collisione fra le due copie della stessa migrazione si è tenuta quella
> del ramo trasferimenti, l'unica il cui `Designer` descrive il modello fuso. La **PR #13 resta aperta** e
> va chiusa a mano dopo il push.
>
> ⚠️ **Resta ai colleghi, non al codice:** le righe con ricevente APP che non dicono ancora *dove* avviene
> il trasferimento vanno riviste a mano (15 nel DB di sviluppo, da rimisurare in produzione). Le elenca il
> filtro «Da rivedere» della pagina, che ora ha anche una vista a elenco fatta apposta per quel lavoro.
>
> Due cose **viste e non toccate**: `ITransferService.MovePointToEndAsync` non ha chiamanti dall'interfaccia
> (ha repository e test), e `LevelFormatting.Format` appende il suffisso di parità anche a un livello
> assente — a schermo esce «— (dispari)», che il round-trip regge ma si legge male.
>
> **Non fuso: `refactor/13-tre-documenti`** (suite **2111** verde su due TFM,
> verifica live fatta).
>
> ⚠️ **Quel ramo non compilava, e nessuno l'aveva visto.** L'audit dell'11 agosto ha trovato 14 chiavi
> duplicate nei `.resx`: il job CI che compila con `-warnaserror` dava **28 errori**, mentre la suite locale
> restava verde — *1391 test verdi e build di produzione rotta convivevano*. Corretto, con tre guardie.
> Adesso il ramo compila davvero, e la decisione di merge è di nuovo solo vostra. È il [doc 13](docs/refactor/13-audit-tre-documenti.md), l'audit dei tre documenti:
> catalogo fonte unica anche di «chi rende il corpo» e «quale sezione è obbligatoria», vLOA finalmente dal
> catalogo, gate pubblico su ricerca e «Cosa è cambiato», pannello release uguale nei quattro editor, una
> sola resa per ogni sezione comune. Dentro ci sono **due difetti che uscivano dal documento**: la pagina
> APP pubblica mostrava le configurazioni della bozza, e gli indici servivano documenti nascosti, sezioni
> nascoste e contenuto senza release. Il merge in `main` **aspetta l'ok esplicito** (come per il doc 10).
>
> Al primo avvio dopo il merge girano tre riconciliazioni one-shot (chiavi vLOA, placeholder «minima»,
> sezioni di catalogo mancanti): sul DB di sviluppo hanno toccato 15 sezioni e 18 blocchi.
>
> **Il ramo `feat/persistenza-mysql` è stato fuso in `main`**: il cutover non è più un ramo a parte. `main` è
> ora **net8 + Pomelo + MariaDB**, il Dockerfile pubblica su `aspnet:8.0`, e il deploy Render+Neon resta in
> piedi come **ambiente di prova** (decisione C3-bis: si riesamina dopo il cutover, non prima).
>
> **Le cose in mano a voi, non al codice:** consegnare `.sql` e pacchetto, le risposte di Ivao.It (A9/A10),
> la rotazione della password Neon, e quattro decisioni di contenuto — la SID `BANA8A` di LIBD, le 33 torri
> senza padre, **quali staff code valgono admin** (E4: ora i codici veri si vedono in diagnostica), e se
> pubblicare una *release* debba scrivere audit.
>
> **Metodo che ha pagato, in questa sessione più che mai:** nove difetti su undici sono usciti **guidando
> l'app**, non dai test — fra cui tre pagine che morivano su MariaDB, una direttiva nginx inesistente che
> avrebbe bloccato la consegna, e l'ATIS contato come chi controlla un aeroporto. Prima di dichiarare fatta
> una cosa, aprirla: la skill `verifica-live` esiste per questo.

> ## 🔬 AUDIT FULL-STACK — 11 agosto 2026, eseguito
>
> Carta ed esito: [`docs/history/audit-2026-08-11-crepe-full-stack.md`](docs/history/audit-2026-08-11-crepe-full-stack.md).
> 34 voci: **23 chiuse**, 3 **ribaltate dalla misura**, 5 rimandate con la ragione scritta. Sei commit.
>
> **Tre cose cambiano le regole, non solo il codice** — chi lavora qui le incontra subito:
> 1. **Gli avvisi sono errori** (`Directory.Build.props`). Un avviso nuovo ferma la build, in locale non solo
>    in CI. ⚠️ Un `--` dentro un commento XML rende quel file illeggibile e **tutte** le proprietà spariscono
>    in silenzio: c'è una guardia, ma vale saperlo.
> 2. **I test girano su net8**, che è la produzione: da **347** a **1115**. Prima ~1000 test non toccavano mai
>    il runtime del cutover.
> 3. **Le dipendenze sono bloccate** (`packages.lock.json` + restore in «locked mode»). Se la CI si ferma sul
>    restore: `dotnet restore --force-evaluate` e committa i lock.
>
> **Metodo che ha pagato, di nuovo:** tre voci sono state *ribaltate dalla misura* — i multi-poligono (zero
> casi su 1338 reali), la retention dell'audit (19 righe in tre settimane), le immagini orfane (1 riga).
> Misurare prima di toccare ha evitato tre lavori inutili. E due guardie nuove hanno **smentito affermazioni
> mie**: le chip a11y erano 8 e non 12, e tre progetti in `tools/` erano senza lock file.
>
> **Aperto dall'audit:** i 17 gestori inline che bloccano la CSP vera, `MapAll()` e il nonce OIDC (vanno con
> A10, servono un login IVAO vero), i file da 1500 righe, l'identità del circuito.

> ## 📋 COSA MANCA DA FARE → [`docs/lavori-aperti.md`](docs/lavori-aperti.md)
>
> Elenco unico di **tutto** l'aperto — cutover, branch non fusi, debito noto, verifiche live pendenti,
> funzionalità. Ogni voce è presa da sola in una sessione, con il blocco segnato (🟢 subito · 🟡 dipende da
> un'altra voce · 🔴 dipende da altri). **Partire da lì**, non da questo documento, che racconta lo stato
> ma non ordina il lavoro.

> ## ✅ IL CUTOVER È IN `main` — cosa sapere su `atc.it.ivao.aero`
>
> **Il server è MariaDB 11.4.10, il provider è Pomelo, `Vipi.Host` è net8.** Decisione vigente: ADR-0007
> **§D4-ter**, che supera §D4-bis (Oracle/net10/MySQL 8) come quella aveva superato §D4. Il
> [piano MySQL](docs/design/piano-supporto-mysql.md) descrive un bersaglio cambiato: leggerlo solo per
> l'analisi dei rischi, **non** per lo stato.
>
> **Cosa è già verificato contro una MariaDB vera** (6–9 agosto): schema e collation `utf8mb4_uca1400_as_cs`
> (163 colonne su 163), `LIRF`/`lirf` che convivono, travaso dei dati veri da Neon con `.sql` **riletto** in
> un database vuoto, key-ring Data Protection che sopravvive al riavvio, un job di CI su MariaDB 11.4.10
> Linux, e i **flussi editoriali guidati sull'app** (import, SID per aeroporto, pubblicazione dei tre tipi
> di documento, lock, ricerca, vista live, blob delle immagini byte-identici).
>
> **Il `.sql` da consegnare**: `_mariadb/dump/vipi-atc-it-ivao-aero-2026-08-09.sql`, 4 MB, sha256
> `1CD77F3A…`. **Il pacchetto di deploy**: `artifacts/publish/vipi-linux-x64-mariadb-20260809.zip`, 47,8 MB,
> self-contained net8. ⚠️ Quello del 5 agosto è compilato contro un provider che non parla MariaDB: **non
> funzionerà mai**, va ritirato.
>
> **Cosa resta, tutto in [`docs/lavori-aperti.md`](docs/lavori-aperti.md) sezione A:** consegnare il dump e
> il pacchetto, le domande a Ivao.It (A9: accesso al DB, `sql_mode`, privilegi, **`max_allowed_packet` ≥ 4
> MB**, backup, WebSocket sul proxy) e i redirect OIDC (A10).
>
> ℹ️ Il bug latente di `MigrateVipiDatabase` è **chiuso**: il dispatch è esplicito per provider e un
> provider senza strategia fallisce l'avvio con un messaggio che dice cosa fare.
>
> ⚠️ **Ogni cambio di schema va emesso DUE volte** — SQLite (`Vipi.Infrastructure`) e MySQL
> (`Vipi.Infrastructure.MySqlMigrations`). Tre test guardia lo pretendono, più il job CI `mariadb-schema` su
> MariaDB vera. E lo scaffold di EF va **riletto**: sull'ultima migrazione metteva il `DropColumn` prima del
> travaso dei dati, e su un database pieno i legami sarebbero spariti in silenzio.
>
> ✅ **B4 deciso il 7 agosto 2026: in produzione va `main` + B1.** `feature/aree-speciali-hardening` è
> fusa in `main` (fast-forward, 21 commit) e si porta dentro per intero `feature/aurora-bridge`, il cui
> endpoint `POST /vsop/api/v1/transfers/resolve` **nasce spento** (`AuroraBridge:Enabled=false`): entra
> come codice, non come superficie pubblica. Conseguenze: al primo boot su Neon l'archivio aree passa da
> 993 a 230 legami (poi «Importa da sorgente»), e **il `.sql` di A3 va rifatto dopo il merge**.
>
> ⚠️ Il **token app IVAO** non è più fra i bloccanti: il 5 agosto ha risposto 200 col secret dei user-secrets
> locali (dettagli e riserve nel blocco più in basso). Manca invece `VipiAuth:ClientSecret`, che in locale
> non è mai servito perché il login è spento: in produzione serve.

> ## ⏸️ RIMANDATO — embedding nel sito `Ivao.It.Website` (non è più la strada del sito definitivo)
>
> **Dal 5 agosto 2026 questo non è più il prossimo passo.** Il sito definitivo sarà servito dal nostro
> host standalone (blocco 🟢 qui sopra), non dalla RCL montata nel loro sito. Il lavoro qui sotto resta
> **valido e non buttato** — l'embedding è rimandato, non cancellato, e il multi-target `net8.0;net10.0`
> delle cinque librerie resta in piedi proprio per questo — ma non è ciò su cui si lavora ora.
>
> **Eseguire il modulo dentro un host net8 e guidarlo.** È il punto 3 del piano in
> [`docs/guide/integrazione-ivao-it-da-fare.md`](docs/guide/integrazione-ivao-it-da-fare.md) §5, e chiude
> **tre** voci aperte in una sessione sola:
> - **§2.1** — il modulo su net8 è stato solo **compilato, mai eseguito**. Restano non verificati i
>   comportamenti runtime di **EF Core 8** (sviluppato e testato solo su EF 10), il rendering della RCL
>   sotto **ASP.NET Core 8**, lo **stream SSE** `/vsop/live/atc` dietro la pipeline dell'host, e la
>   collisione di rotta fra `/vsop/live/{callsign}` e il prefisso SSE.
> - **§2.2** — **doppia localizzazione**: il modulo registra `AddLocalization` + `UseRequestLocalization`
>   dentro `AddVipiModule`/`UseVipiModule`, il sito registra `AddIvaoItLocalization` + `UseIvaoItLocalization`
>   che gira **dopo**. Chi vince decide la lingua di `/vsop`. Sintomo atteso: il `CultureSelector` del
>   sito non ha effetto sulle pagine del modulo, o entrare in `/vsop` cambia lingua al sito. Probabile
>   esito: un flag per non registrare la localizzazione del modulo quando l'host ne ha già una.
> - **§2.4** — **CSS**: il sito carica Bootstrap 5.3.3 e animate.css **globalmente**. Gli stili del modulo
>   sono confinati sotto `.vipi-root`, quindi il rischio è il contrario del solito: sono i loro a poter
>   sbavare dentro il nostro contenitore. Va guardato con gli occhi, non con i test.
>
> **Punto di partenza già pronto:** l'albero `Ivao.It-master` col modulo montato **compila** (0 warning,
> 0 errori). Per rifarlo da zero: copiare l'albero, `git am` di `docs/guide/ivao-it-wiring.patch`,
> materializzare il modulo in `external/vipi`, e — solo per compilare in locale — sostituire il
> `PackageReference` `Ivao.It.Logging` (feed privato loro, non su nuget.org) con il `ProjectReference` a
> `src/Common/Logging/Ivao.It.Logging.csproj`, che è già nel loro albero. Poi guidare con la skill
> `verifica-live`.
>
> ⚠️ Serve `VipiAuth`/identità: in embedded l'identità viene dall'host, quindi per la prova o si monta un
> `ClaimsPrincipal` finto sull'host di test, o si usa `useDevIdentity: true` in `AddVipiModule`.

> **🚧 Sessione 2026-08-03 — aree regolamentate: interruttore, import incrementale, dangling, appartenenza
> multi-ACC.** Branch `feature/aree-speciali-hardening`, 8 commit, suite **951 verde** (+24), build 0 warning. Carta completa:
> `docs/feature/2026-08-03-aree-regolamentate-hardening.md`. Le cose da sapere subito:
> - ⚠️ **`ImportSids` può essere spento in produzione senza che nessuno l'abbia deciso.** La migration dell'8 lug
>   aggiunse la colonna con `defaultValue: false`, e su Postgres `PostgresSchemaReconciler` backfillava a `false`
>   ogni bool NOT NULL nuovo: su un DB dove la riga `ImportPolicies` esisteva già, la categoria è nata spenta.
>   **Da guardare in `/vsop/admin/sorgenti`.** Non è ribaltabile da codice: `false` è indistinguibile da una scelta
>   dell'admin. Per il futuro il default sta nel modello (`HasDefaultValue`) e il reconciler lo legge.
> - **Le aree regolamentate ora hanno un interruttore** (categoria `SpecialAreas`): escluderle **congela** quelle in
>   archivio — l'import non le aggiorna e soprattutto non le pota. Gate in `SpecialAreaImportUseCase`, non
>   nell'hosted service, sennò il bottone di `/vsop/admin/accs` lo scavalca.
> - **L'import non riscarica più la shape** delle aree che ce l'hanno già (rinfresco a 30 giorni): era una chiamata
>   per area per ACC a ogni giro, solo per rileggere lo stesso poligono.
> - **Le aree selezionate in un documento possono sparire in silenzio**: gli id sono soft-ref senza FK e il prune li
>   può cancellare. Ora la diagnostica le segnala («Area regolamentata dangling», sola versione di lavoro) e
>   l'editor le marca «⚠ non più disponibile». Il prune resta libero di potare: si rileva, non si vincola.
> - **Un'area regolamentata può appartenere a PIÙ ACC** e prima ne tenevamo uno solo: `IvaoId` è unico e
>   `CenterId` era una colonna, quindi vinceva l'ultimo ACC in ordine alfabetico. La R49 «Zita» (id 8870), che su
>   IVAO è di LIRR e del militare LIZZ, risultava solo di LIZZ — ente nascosto — e spariva dalle aree proprie di
>   Roma. Ora c'è l'entità di legame `SpecialAreaCenter` (SPEC §9.23): import additivo, prune per legame, area
>   cancellata solo quando resta senza enti.
> - ⚠️ **Dopo il deploy premere «Importa da sorgente»**: il backfill recupera una sola appartenenza per area (era
>   l'unica che il vecchio modello sapeva); le altre le riporta il primo import. Su Postgres il travaso e il drop
>   della colonna storica li fa `ISpecialAreaMaintenance` al boot, non la migration — che lì non gira.
> - ⚠️ **Le aree estere spariscono dall'archivio al primo avvio** (763 legami su 993): `Acc.SpecialAreasEnabled`
>   nasce spento per gli `IsForeign`, e una riconciliazione one-shot al boot le libera. Restano le 230 italiane.
>   Se ne serve una, si riaccende quell'ACC con «Importa aree» in `/vsop/admin/accs` e torna. I documenti che ne
>   citavano una la vedono come dangling (diagnostica + marcatura nell'editor).
> - ✅ **Verifica live eseguita il 6 agosto 2026** (esito per esteso nella carta e in `docs/lavori-aperti.md` B1):
>   interruttore, dangling e aree estere confermati; la R49 «Zita» non è più elencata sotto LIRR dalla sorgente —
>   la meccanica multi-ACC funziona lo stesso, è l'esempio a essere invecchiato.

> **📄 Sessione 2026-07-30 (3) — uniformità dei tre documenti (vIPI ACC · vIPI APP · vLOA).** Branch
> `fix/uniformita-tre-documenti`, 17 commit, suite **640 → 663 verde**, verifica live confermata dall'owner.
> Carta completa: `docs/refactor/11-uniformita-tre-documenti.md`. Le cose da sapere subito:
> - **Il modello era unico, la rilettura no.** Ogni famiglia interpretava lo stesso `Document` a modo suo:
>   chiave di sezione, resa del contenuto editoriale, stato «nascosta», fallback della vista pubblica.
>   Sei difetti alti, tutti **invisibili ai test verdi** e trovati guidando l'app reale.
> - **Stato per-sezione ⇒ colonna su `DocumentSection`.** `IsHidden` (migrazione `AddSectionIsHidden`) e
>   `BeforeParentBody` (`AddSectionBeforeParentBody`) si aggiungono a `RenderMode` di doc 10: versionati e dentro
>   lo snapshot. Prima «nascondi» viveva in tre storage, due non versionati → **cambiava la pagina pubblica senza
>   pubblicare**. ⚠️ `CreateDraftAsync` non copiava i flag: aprire una bozza resettava `RenderMode` a `Frozen`.
> - **Chiavi di sezione univoche** (`custom:{guid8}`): la costante `"custom"` faceva collidere le sezioni libere.
>   Migrazione dati al boot (`IDocumentMaintenance`), non EF: le migration del repo sono SQLite-flavored.
> - **`?as=` non valido ⇒ pubblica CON derivate frozen.** Prima il fallback lasciava `_useFrozen=false`: il
>   congelamento AIRAC era bypassabile dall'URL.
> - **P7–P9 chiesti dall'owner in verifica live**: sotto-sezioni collocabili **prima** del corpo; coordinamenti
>   con il solo primo livello espanso; «Aree regolamentate» che nasce collassata (viewer **ed** editor).
> - ⚠️ **Viewer ed editor possono avere sequenze opposte per la stessa sezione** (vLOA/coordinamenti: il viewer
>   rende le direzioni nel padre, l'editor nelle figlie). Toccarne una sola ha prodotto un albero duplicato.
> - **§3bis del doc 11: «non-problemi verificati»** — due apparenti duplicazioni nei coordinamenti che sono dato
>   corretto. Leggerlo prima di «aggiustarle».

> **🖨️ Sessione 2026-07-30 (2) — stampa dei documenti + fix pubblicazione.** Branch
> `fix/audit-race-deadcode-redundancy`, 14 commit, suite **631 → 640 verde**, build 0 warning. Schede complete:
> `docs/feature/2026-07-30-stampa-documenti.md` e `docs/feature/2026-07-30-pill-stato-dopo-publish.md`.
> Le cose da sapere subito:
> - **La stampa era rotta da sempre e in silenzio**: il blocco `@media print` in `vipi-theme.css` nascondeva
>   tutto e mostrava solo `.printable`, classe che **nessun markup applicava** → Ctrl+P dava un foglio bianco su
>   qualunque pagina. Ora c'è il foglio dedicato **`vipi-print.css`** (nasconde il chrome, contenuto nel flusso
>   normale, A4 verticale, `thead` ripetuto, colori informativi preservati, scala tipografica da carta) +
>   `PrintMeta` + tasto «Stampa» sui quattro viewer. Nessun endpoint di export: la stampa del browser copre
>   RNF-6 (piano §10, §22.7 aggiornati). **Dati live fuori dalla carta** per decisione: METAR/TAF e Ridotta.
> - **Tre trappole del browser, tutte invisibili ai test.** Un `<details>` chiuso **non si apre col solo CSS**
>   (Chrome lo nasconde da user-agent con `content-visibility` su `::details-content`) → serve l'hook
>   `beforeprint` (`wirePrint` in `vipi-ui.js`). **Chrome segnala la stampa due volte** (`beforeprint` + cambio
>   media `print`) → gli handler di stampa vanno resi **idempotenti**, o il ripristino post-stampa non avviene.
>   **Leaflet** tiene la propria dimensione in memoria: ridurre l'altezza da CSS **ritaglia** la mappa invece di
>   riadattarla (serve `invalidateSize` + refit).
> - **«Bozza vN» dopo «Pubblica ora» era solo la pill**, non la pubblicazione (release `Effective`, audit e
>   documento promosso erano corretti): `ReleasePanel` ricaricava solo le proprie release senza avvisare l'host.
>   Ora ha un `EventCallback Published` che i tre editor agganciano al proprio `LoadAsync`. ⚠️
>   `string.Format(L["chiave"].Value, n)` **non interpola** — serve l'overload `L["chiave", n]`.
> - **⚠️ Chiave di release ACC**: `"{acc}|{root}"` — la parte `root` sceglie *quale* albero/documento si
>   pubblica e **va rispettata**. `AccVipiReleaseTarget` la scartava (primo CTR radice per `CoverageOrder`): su
>   una ACC multi-albero avrebbe promosso la bozza del documento sbagliato, in silenzio. Corretto.
> - **Razor scarta il testo di sola spaziatura che precede un blocco di codice**, anche dentro `<text>`: la
>   legenda piste usciva «recommended**from** the METAR wind». Lo spazio va scritto come entità `&#32;`.
>   Stessa famiglia della trappola `v@r.Proprietà` (sessione precedente).

> **⚠️ Sessione 2026-07-30 — audit concorrenza / codice morto / ridondanze.** Branch
> `fix/audit-race-deadcode-redundancy`, 14 commit, suite **505 → 631 verde**, build 0 warning. Documento completo:
> `docs/history/audit-2026-07-30-concorrenza-e-ridondanze.md`. Le tre cose da sapere subito:
> - **Import SID era rotto in silenzio** su LIRF/LIMC/LIME/LIBG/LIED/LIEO/LIPQ (ogni *reimport* falliva: snapshot
>   costruito con `ToDictionaryAsync(StableKey)` su chiave legittimamente ripetuta; il job logga a `LogDebug`).
>   Fixato. ⚠️ **La `StableKey` NON è unica per design** — non aggiungere un indice unico, fallisce sui dati veri.
> - **Le migration si provano su una copia di `src/Vipi.Host/vipi.db`**, non solo su DB vuoti da `EnsureCreated`:
>   i test partono sempre da vuoto e non vedono questa classe di problemi.
> - **Nuova skill `.claude/skills/verifica-live/`** per lanciare e guidare l'app in locale (la procedura non era
>   scritta: `dev-bootstrap.md` si fermava a `dotnet run`, e serve `VipiAuth__Enabled=false` per entrare).
>   Guidandola è uscito `rel. v@r.VersionNumber` **letterale** a schermo: in Razor una `@` fra due caratteri
>   non-spazio è letta come **indirizzo email** e non apre un'espressione, senza alcun warning → usare `v@(...)`.
>
> Aperto, **non di codice**: la SID `BANA8A` di LIBD (pista 07) ha `InitialClimb = "90"` → resa «90 ft», quota
> implausibile (le altre BANAV hanno `9000` → «FL90»). Da correggere nell'editor.

> **⚠️ Sessione 2026-07-29 — hardening deploy Render+Neon (leggere se si lavora sul deploy hostato).** Il sito test gira su Render+Neon Postgres (vedi `deploy/render/README.md` e memoria [[deploy-hosting-options]]). Fix di questa sessione, tutti su branch `fix/airport-weather-tl-draft-preview`:
> - **Login IVAO ricordato 7 giorni** (`VipiStandaloneAuthExtensions.cs`): cookie `ExpireTimeSpan=7gg` sliding + `IsPersistent=true` sul challenge → un solo login, sopravvive a chiusura browser.
> - **Retry-on-failure Neon** (`Infrastructure/DependencyInjection.cs`, ramo Postgres): `EnableRetryOnFailure` — Neon serverless chiude le connessioni idle, la prima query dava 500 `transient failure`. ⚠️ **Corretto il 30 lug:** questa nota diceva «retry-safe perché `EfUnitOfWork` avvolge già le transazioni in `CreateExecutionStrategy()`» — **necessario ma non sufficiente.** Al retry la strategy rigira la lambda sullo stesso context scoped e il rollback non ripulisce il change-tracker, quindi le entità del tentativo fallito venivano riemesse (doppi insert). Ora `EfUnitOfWork` azzera il tracker a ogni tentativo.
> - **DataProtection su Postgres** (`src/Vipi.Host/VipiDataProtection.cs`, modulo staccabile): su Render il container è effimero → il key-ring di default si perdeva a ogni redeploy (antiforgery rotto + logout). Ora le chiavi vanno su un `DbContext` dedicato (tabella `DataProtectionKeys` su Neon). ⚠️ **NON** `EnsureCreated()` (verifica il *database*, non la tabella → non creava nulla sul DB esistente): la tabella si crea con `CREATE TABLE IF NOT EXISTS`. Attivo solo se `Persistence:Provider=Postgres`; in dev SQLite resta il file-store.
> - **StationResolver.Prewarm()** (fix crash `A second operation was started`, memoria [[blazor-dbcontext-concurrency]]): `OnlineCount()` faceva lazy-load DB **durante il render** su `AccVipiPage`/`SopHome`/`VloaListPage`. Nuovo `IStationResolver.Prewarm()` scalda le cache nel ciclo di vita async. **Regola: nessuna I/O DB durante il render, nemmeno lazy via service scoped.**
> - **Tool `Vipi.DbSeed`** (copia SQLite locale→Neon): fix ciclo `Document↔DocumentVersion` (insert a 2 fasi con `CurrentVersionId=null`). Uso: `dotnet run --project tools/Vipi.DbSeed -- <vipi.db> "<connstring-postgres>"` (fa TRUNCATE+reseed).
> - **`IvaoTokenProvider`**: logga il body d'errore sui token 400 (prima `EnsureSuccessStatusCode()` lo scartava).
>
> **✅ RIENTRATO (5 agosto 2026) — token app IVAO.** Avviando l'host sul MySQL locale, `POST /v2/oauth/token`
> ha risposto **200** e il polling ha trovato 2 ATC di divisione online. Il secret nei user-secrets locali
> funziona: quello stale era su Render, non qui. ⚠️ Verificato solo il percorso con scope **`tracker`** (il
> polling): l'**import** ACC/settori, che potrebbe volere anche `configuration`, non è stato riprovato — il
> database di prova era vuoto. Da confermare guidando l'import. La diagnosi storica resta sotto perché il
> ragionamento serve se il 400 tornasse.
>
> **⏳ ex-APERTO — token app IVAO (400):** il polling tracker + import ACC falliscono con `POST /v2/oauth/token → 400`. Diagnosi: **NON è codice** (endpoint/grant/scope validati col discovery OIDC IVAO). È il **secret/app sul portale**: o `Ivao:ClientSecret` stale nei user-secrets, o l'app `fc95c992…` non ha grant `client_credentials`/scope `tracker`+`configuration` abilitati. Il nuovo log mostra l'`error` esatto nel body. Nota: `Ivao:ClientId == VipiAuth:ClientId` (stessa app IVAO per login utente + token app). Aggiornare il secret sia in user-secrets locali sia in `Ivao__ClientSecret` su Render.
>
> **NB dev locale:** per testare login/logout in locale serve `VipiAuth:Enabled=true` in `appsettings.Development.json` (spegne l'utente dev fittizio → login IVAO vero) + redirect `http://localhost:5034/signin-oidc` e `/signout-callback-oidc` registrati sul portale IVAO. Questo flag è tenuto **fuori dai commit** (preferenza locale).

> **⚠️ Stato corrente (2026-07-21) — leggere prima.** Dopo il Round 34 il progetto è passato per l'**asse di refactor strutturale `docs/refactor/01→10` (tutti eseguiti)**: modello **`Document`+`DocumentVersion` unificato** per tutti e 4 i tipi (vIPI ACC / APP / Airport / vLOA), editing e storage su documento (doc 08); **flusso di pubblicazione generico** via registry `IReleaseTarget`/`IDocKindRoutes` (doc 09); **snapshot totale al publish + `RenderMode` per sezione** con **visibilità pubblica = release effettiva** (doc 10, merged). Aggiunta **retention pubblicazione** (anti-bloat: pota release `Superseded` oltre 13 cicli e versioni `Archived` oltre 3/documento; per-publish + boot sweep `PruneVipiReleases`). **Fix 2026-07-21:** off-by-one del cap `Archived` su **entrambi** i path publish (release-publish `ReleaseService.PublishNowAsync` e version-publish `EditingService.PublishAsync`) — ora il prune gira dopo l'archiviazione. Suite **358 verde**. Dettagli in `docs/history/rounds.md` (in coda), `docs/refactor/00-overview.md` e memoria `publication-retention-plan`. **NB:** le sezioni §4→§8 qui sotto descrivono lo stato a Round 34 e NON riflettono ancora l'asse 08→10 (modello/pubblicazione): in caso di conflitto valgono i doc `refactor/` + `spec/modello-dati.md`.
**Stato:** progetto **in sviluppo attivo**. Solution .NET 10 a 4 layer + Host Blazor Server, consultazione+editing+sicurezza dal DB. **Import SID da GitHub** (sectorfile Aurora `ivao-italy/it-aurora-sector`): parser + completion fix/VOR + alias, merge preserva-manuali, priorità per punto persistente (StableKey), pubblicazione differita al ciclo AIRAC N+1 (round 34, `AddSidImport`). **Import periodici gated** (`ImportState`, `AddImportState`): niente più fetch-all a ogni riavvio (round 34). **Vista live UNIFICATA** (`/vsop/live[/{callsign}]`, doc refactor 12): una pagina per callsign, descrittori per tipo di ente (CTR/APP/**TWR/GND/DEL**), postazione dalla connessione IVAO senza selettore, **non richiede una vIPI pubblicata** (è legata all'ente, non al documento) + vista rapida aeroporto inline (`AirportQuickPanel`); QoL admin `sectorstructure`/`trasferimenti` (round 34). **Versioning AIRAC**: release schedulate per ciclo su TUTTI i tipi (`DocRelease`; round 29, §9.17) + **task management editor**. **Anteprime unificate `?as=`** nei viewer tipizzati (round 33). **vLOA data-driven** + **ACC esteri confinanti** (round 27-28, §9.16). **vIPI ACC/APP data-driven a blocchi** (round 21/23). **Live IVAO** (polling + cache + SSE). **Sorgente dati disaccoppiata** + **policy di import opt-out** (categorie: TA/Runways/Sectors/**Sids**). Pagine su prefisso **`/vsop`**. **Fonte unica = cataloghi**: i `Sector` sono una proiezione, gerarchia per callsign cross-ACC (Round 20).

> **📡 Sessione 2026-07-31 — vista live.** Branch `feat/vista-live`, 23 commit, suite **631 → 718 verde**,
> verifica live guidata su copia del DB reale. Carta: `docs/refactor/12-vista-live-unificata.md`. Da sapere subito:
> - **Una pagina sola, keyed sul callsign**: `/vsop/live` (la tua postazione, dalla connessione IVAO —
>   **nessun selettore**) e `/vsop/live/{callsign}` (consultazione). Via `AccLivePage`/`AppLivePage` e le due
>   `Ridotta*` morte. Le rotte storiche fanno **301 a un salto solo**.
> - **La vista è legata all'ENTE, non al documento**: senza vIPI pubblicata degrada a banner e continua a
>   rendere trasferimenti, AoR e frequenze dai cataloghi. Non reintrodurre early-return sul documento.
> - **Descrittori per tipo** (`ILiveStationKind`, come `IReleaseTarget`): **torri, ground e delivery hanno una
>   vista live** che prima non esisteva. Un test verifica che ogni `SectorType` abbia un descrittore.
> - ⚠️ `/vsop/live/{callsign}` ricade sul prefisso dello stream SSE `/vsop/live/atc`: vince il segmento
>   letterale, ma è una proprietà del routing che si rompe cambiando le rotte → smoke dedicato.
> - **L'avvicinamento è reso come l'area**: chip degli aeroporti (un APP ne copre spesso più d'uno), frequenze,
>   trasferimenti. Pannello fisso solo per torri/ground/delivery, che sono di un aeroporto solo.
> - **Un punto verso un proprio discendente si mostra solo se quel settore è APERTO**: se è chiuso lo stai
>   coprendo tu, e il punto diceva «passa a te stesso». Vale solo per i discendenti — verso l'esterno la
>   risalita fino a UNICOM resta informazione utile.
> - ⚠️ In verifica: `innerText` su un `<details>` **chiuso** torna stringa vuota — un'asserzione ingenua la
>   legge come «elemento assente».
> - ⚠️ In verifica: un `dotnet run` che fallisce per **DLL bloccate** da un'istanza precedente lascia in ascolto
>   il binario VECCHIO, e si finisce per misurare la build sbagliata. Killare, `dotnet build`, poi `--no-build`.
>
> - **Il padre dell'aeroporto non arrivava alle sue posizioni** (segnalato dall'owner, fixato): la proiezione
>   leggeva solo `AirportSector.ParentCallsign` (solo APP) e ignorava `Airport.ParentCallsign`, che è il campo
>   che l'admin compila in Struttura → torri/ground/delivery orfani. Ora scaletta **DEL→GND→TWR→APP** + uscita
>   sul padre dell'aeroporto, riproiettata all'avvio (`ProjectVipiSectors`). Reggeva anche la risalita dei
>   trasferimenti: un punto verso una torre offline finiva su UNICOM invece che all'APP.
>
>   Fra pari grado si sceglie **coi dati**: la radice del sottoalbero APP (gerarchia scritta dall'admin, es. le
>   sei APP di LIRF pendono da `LIRF_TW1_APP`), poi il callsign senza infisso (`LIRF_TWR` vs `LIRF_E_TWR`), e se
>   resta ambiguo si **sale** invece di tirare a sorte.
>
> - **Torri, ground e delivery sono nodi editabili** in `/vsop/admin/sectorstructure` (§8 del doc 12): erano
>   esclusi da un filtro `Position == "APP"`, non da una scelta di modello. La scaletta è un servizio di dominio
>   condiviso (`AirportPositionLadder`) e i nodi senza padre scritto mostrano quello **ereditato** invece di un
>   «da assegnare» che contraddirebbe la vista live. Guardia: nessun padre più in basso nella scaletta.
>
> Aperto, **di dato**: 33 torri di aeroporti senza APP e senza padre configurato in Struttura, più LIRF stesso
> (senza padre l'aeroporto non compare fra i chip di nessuno). Ora si sistemano dalla pagina: il filtro «solo da
> agganciare» li raccoglie.

> **Storia dei round:** `docs/history/rounds.md` (changelog R5→R34). **Indice doc:** `docs/index.md`. Ultimo round: **34** — vista operativa + QoL admin + import SID GitHub + gating import; modello in `docs/spec/modello-dati.md` §9.8 (migrazioni). (R33: anteprime `?as=`; R30: QoL Bozze & versioni §9.18; R29: versioning AIRAC + task §9.17.)

---

## 1. In una frase
Portale web interattivo che trasforma le **vIPI** (istruzioni operative ATC) e le **vLOA** (lettere di accordo) della divisione IVAO Italia da Word statici a contenuto strutturato, con due livelli (Estesa/Ridotta), logica di visibilità live legata a chi è online (AoR top-down) ed editing per lo staff.

## 2. Come far girare il progetto
```bash
cd "vIPI Ivao Italy"            # cartella interna con la solution
dotnet build Vipi.slnx
dotnet test  Vipi.slnx          # 631 test (Domain 23 · App 273 · Infra 228 · Hosting 18 · Ui/bUnit 85 · E2E 4)
dotnet run --project src/Vipi.Host --urls http://localhost:5034   # poi apri /vsop
```
- 🔎 **Per verificare una modifica UI a schermo** (non solo coi test): skill **`.claude/skills/verifica-live/`** —
  avvio su una copia del DB, driver Edge+puppeteer-core, bersagli e trappole già mappate. Le regressioni Blazor
  sono silenziose coi test verdi, quindi il runbook chiede di guidare il flusso reale.
- ⚠️ **AZIONE PENDENTE (2026-07-22, audit Fase 1):** **RIAVVIARE il Host** per applicare `AddImportStateLastError` (additiva: `ImportState.LastAttemptUtc`/`LastError`). Poi `/vsop/admin/sorgenti` mostra il **report stato import** (ultimo successo/tentativo/errore per categoria). Nota: da questa sessione `/vsop/health` è **Unhealthy (503)** se ci sono migrazioni pendenti (schema drift). Audit completo: `docs/history/audit-2026-07-22-criticita-full-stack.md`. Nuova rete di test: `Vipi.Ui.Tests` (bUnit) + `Vipi.E2E.Tests` (WebApplicationFactory in-process).
- ℹ️ **FASE 2 audit ESEGUITA (2026-07-22, nessun cambio schema):** **B1** report consistenza soft-ref in **`/vsop/admin/diagnostica`** (pista orfana · label pista divergente · area fantasma · gerarchia `ParentCallsign` dangling) — solo diagnosi, nessun auto-fix; `IConsistencyReportService`/`Analyze` (logica pura) + `IConsistencyReportRepository` (EF read-only); se ci sono finding, `/vsop/health` → **Degraded**. **C1** XSS: `HtmlEncode` dei valori dinamici in `StrutturaPage`/`AeroportoPage` (pattern gemello `SearchPage`/`MarkdownLite`).
- ℹ️ **FASE 3 audit ESEGUITA (2026-07-22) — parte code, resto pianificato in ADR-0007:** **A1** tampone concorrenza SQLite `SqliteTuningInterceptor` (WAL + `busy_timeout`) nel path `UseSqlite`; **D1** `ProductionIdentityGuard.EnsureSafe` in `Program` fa **hard-fail** all'avvio se l'identità dev è attiva fuori da Development (no admin-onnipotente in prod); test path prod `HostIdentityCurrentUserProvider` (nuovo progetto `Vipi.Hosting.Tests`). **A1 cutover Postgres + A2 scala Blazor = pianificati in `docs/adr/adr-0007-produzione-persistenza-e-scala.md`** (non attuati: servono migrations Postgres dedicate + istanza di validazione + backplane). **ESTERNI residui:** montare la RCL nel sito host + configurare `HostIdentity` coi claim/staff-code IVAO reali; eseguire il cutover Postgres; provisioning backplane.
- ℹ️ **MINORI audit ESEGUITI (2026-07-22):** **C4** `StrutturaPage` — estratti i `RenderFragment` HTML-a-mano in componenti dichiarativi `StructureCoverage`/`StructureFallbackChain` (chiude C1 alla radice, +6 bUnit con regressione XSS). **B4** spec §3 marcata `[SUPERATO]` (usa §9). **B3** nuova checklist `docs/guide/dev-bootstrap.md` (coerente «Nessun seed»). **C3** chiuso come non-issue (aor3d già off; AoR block = editoriale, non stub). Onboarding dev: vedi `docs/guide/dev-bootstrap.md`.
- ⚠️ **AZIONE PENDENTE (2026-07-22):** **RIAVVIARE il Host** per applicare le migrazioni pendenti dei trasferimenti — `AddTransferPointConditionArea` poi **`SplitTransferConditionColumns`** (backfilla e droppa `ConditionKind`). Sessione 22 lug: condizione trasferimenti = **tre colonne indipendenti** (pista multi-select · area con **ricerca a digitazione** · personalizzata), enum `TransferConditionKind` **rimosso**; fix condizione «Pista» che legge le **piste reali** `AirportRunways` (non le config); bottone **«Re-importa da IVAO (tutti)»** su `/vsop/admin/airports`. Verifica live su LIBD. Suite **19 dom + 205 app + 174 infra** verde. Dettaglio: `spec/modello-dati.md` §9.20, `refactor/07-trasferimenti.md` §7-7.2, memorie `transfer-condition-model` / `airport-runway-import`.
- ⚠️ **NOTA (Round 34):** il **`vipi.db` dev è stato resettato** a fine sessione (testando il gating import). Al primo avvio ripopola da zero (ACC → settori → aree → SID) e stampa lo stato in `ImportStates`; i riavvii successivi **saltano** i fetch finché non scadono i 24h (o via bottoni manuali). Le SID importate sono pubbliche solo dal ciclo AIRAC successivo.
- ⚠️ **AZIONE PENDENTE (Round 22):** **fermare e RIAVVIARE il Host** per applicare la migrazione **`AddAirportCoordsAndTwrSyntheticShape`** (additiva) e far girare il job che (a) popola `Airport.Latitude/Longitude` dal dettaglio ATCPositions e (b) genera le **shape tonde 5 NM** per le TWR vuote (`/v2/ATCPositions/{compose}.regionMapPolygon = "[]"`). Il job parte ~30s dopo l'avvio. Poi su `/vsop/{acc}/apps/vipi?app={APP}` l'AOR mostra il cerchio della torre col toggle «Shape torre». ⚠️ Credenziali IVAO in **user secrets** (`Ivao:ClientId/ClientSecret`), scope `tracker` basta per il dettaglio postazione. Il Host viene **fermato** a fine sessione (blocca le DLL in build).
- ⚠️ **AZIONE PENDENTE (Round 20):** se il DB è ancora pre-round-20: **reset `src/Vipi.Host/vipi.db`** in dev (o applica `AddHierarchyParentCallsign`) → riavvia. Poi `/vsop/admin/acc` → «Importa da sorgente»: la **sync** popola i `Sector` dai cataloghi; in `/vsop/admin/sectorstructure` compare l'**albero di copertura globale** (cross-ACC).
- DB **SQLite** creato/migrato all'avvio (`src/Vipi.Host/vipi.db`). **Nessun seed**: si parte da DB **vuoto**. Flusso dati reale: `/vsop/admin/acc` importa ACC+settori dalla sorgente → la sync proietta i `Sector` → la **gerarchia** (padri per callsign) si imposta in `/vsop/admin/sectorstructure` → «Crea nuovo documento» (vIPI = N settori di scope, uno primario) → editor. **I settori NON si creano più a mano** (sono proiezione dei cataloghi, Round 20). Cancella `vipi.db*` per ripartire da zero. I `*Seed.cs` di Roma restano solo come fixture nei test.
- In dev l'utente è `DevCurrentUserProvider` (VID 704798, staff `IT-AOC` → **admin**, può tutto).
- Migrazioni: `dotnet ef migrations add <Nome> --project src/Vipi.Infrastructure --startup-project src/Vipi.Infrastructure -o Persistence/Migrations`. ⚠️ Per i **rename** di proprietà/colonna EF scaffolda `RENAME COLUMN` solo se i campi combaciano: **verificare a mano** la migrazione generata (no Drop+Add che perde dati).

## 3. Mappa documenti
Indice completo con scopo e stato di ogni documento: **`docs/index.md`**. In sintesi:
- `README.md` (cos'è + architettura + build) · **questo `HANDOFF.md`** (leggere per primo per riprendere).
- `docs/history/rounds.md` (changelog dei round) · `docs/spec/` (modello dati, logica AoR, mappa pagine) · `docs/guide/` (config, integrazione, **guida utente del bridge Aurora**) · `docs/adr/` (decisioni) · `docs/design/` (piano, **piano+verbali del bridge Aurora**) · `docs/reference/` (`sector-map.md`, **`api-aurora-bridge.md`**).

---

## 4. STATO CODICE — cosa è implementato (e dove)

**Solution (Clean Architecture, net10.0):** `Vipi.Domain` · `Vipi.Application` · `Vipi.Infrastructure` (EF Core + SQLite) · `Vipi.Ui` (RCL Blazor) · `Vipi.Host` (Blazor Server dev) + 3 progetti test.

**Cuore AoR/visibilità (✅ testato S1–S10):** `Application/Aor/AorService.cs` (ownership/stato settori, top-down, unificazioni), `Topology.cs`, `Infrastructure/Aor/TopologyBuilder.cs` (implementa la porta `ITopologyProvider`). Tabella di verità visibilità in `Application/Content/ContentService.cs`.

**Consultazione dal DB (✅):** pipeline `IContentRepository` → `IVipiViewService` → `SectionNode`/`BlockRenderer`. Rotte sotto `/vsop`:
- `/{acc}/vipi` (Estesa ACC) · `/{acc}/ridotta` (proiezione tier Reduced + sezione Trasferimenti) · `/{acc}/airports?icao=` (vIPI aeroporto) · `/{acc}/vloa`.
- `/search` (ricerca full-text reale), `/changed` (cosa è cambiato nel ciclo AIRAC), `/{acc}/export` (Estesa → stampa/PDF browser).
- **SID ✅ reali** (round 34): importate dal sectorfile Aurora GitHub, editor aeroporto + `AirportQuickPanel`. Stub residui: mappe AoR (SVG statico), `/{acc}/aor3d` (SVG statico). METAR/TAF = reale (NOAA).

**Editing persistente (✅):** `Application/Content/EditingService.cs` + `Infrastructure/Persistence/EfEditingRepository.cs`:
- Workflow **bozza→pubblicato** (clona versione, audit, archivia precedente). CRUD **blocchi e sezioni** (aggiungi/elimina/sposta, vincolo max 3 livelli). `EditorPage` (`/{acc}/editor`, anche `?doc={id}`), `VersioniPage`.
- Editor specializzati: `AdminTrasferimentiPage` (trasferimenti, pagina admin globale `/vsop/admin/trasferimenti`: selettore ACC + flussi/punti, Next cross-ACC; ex per-ACC `XferEditorPage` rimosso) — **round 22:** flussi e punti **editabili in-place** via `ITransferService.UpdateFlowAsync`/`UpdatePointAsync`. **12 ago 2026 — la pagina è a TRE COLONNE**: navigatore (`XferNavigator`, albero Settore ▸ Aeroporto ▸ gruppo, dove il gruppo è una **foglia** e non un livello di collasso) · riquadro di lavoro (il gruppo scelto) · pannello riga; ognuna scorre per conto proprio, e l'altezza la misura `vipiFitViewport` perché in CSS non è esprimibile. Interruttore **Albero ⇄ Elenco** (`XferRowsTable` è **una** tabella per entrambe le viste, con le colonne di contesto solo in elenco). **CoP, livello e ricevente si scrivono in cella**; il livello si rilegge con `LevelFormatting.Parse` (round-trip provato). Stato in URL (`?acc=&vista=&gruppo=&riga=&q=&tipo=&rev=&norx=`), preferenze di vista in `localStorage`. Secondo giro: **annulla** dopo un'eliminazione — con `RestoreFlowAsync`/`RestorePointsAsync`, che rimettono anche l'outline (ricostruire con `AddPointAsync` lo appiattirebbe in silenzio) — **modifica in blocco** su ricevente/livello/condizione/eliminazione, ordinamento per intestazione in elenco, e i sei picker a digitazione ridotti a un componente solo (`TypeaheadPicker`, con frecce/Invio/Esc). ⚠️ Salvare una cella costava **8 query**: il contesto delle frasi ora si rifa solo sulle scritture di gruppo. Carte: [`docs/feature/2026-08-12-editor-trasferimenti-tre-colonne.md`](docs/feature/2026-08-12-editor-trasferimenti-tre-colonne.md) e [`docs/feature/2026-08-12-editor-trasferimenti-rifiniture.md`](docs/feature/2026-08-12-editor-trasferimenti-rifiniture.md). `VloaEditorPage` (redirect all'editor generico). Gerarchia di copertura in `StrutturaPage` (`/vsop/admin/sectorstructure`).
- **Editor APP non remotizzati (✅ round 21):** `AppEditorPage` (`/vsop/{acc}/apps/editor?app=`) WYSIWYG con 6 sezioni fisse (Separazioni · AOR · Frequenze · VFR · Minime · Coordinamenti) + custom, riordino drag-and-drop+tasti, nascondi sezioni; viewer `AppnPage` data-driven. Entità `AppProfile`/`AppFrequencyLink` (modello §9.13), service `IAppProfileService` (freq/coord/AOR **derivate live**), `AorPolygonProjector`, registry `AppSections`, componenti `Vipi.Ui/Components/App/*`, mappa AOR Leaflet (`vipi-aor.js`). Instradamento via `DocumentSummary.IsStandaloneApp`. **Round 22:** «Trasferimenti verso ACC» suddiviso in sottosezioni **Partenze/Arrivi** (`AppCoordinationView`, split per `Kind`); **AOR** mostra anche le **shape delle TWR** dello stesso aeroporto come overlay Leaflet con toggle «Shape torre» (`GetTowerPolygonsAsync`). ⚠️ **`TopologiaPage` rimossa** (`/vsop/{acc}/topologia`): gerarchia → `sectorstructure`; le regole di unificazione + simulatore AoR erano legacy e non hanno più UI (motore `IAorService` + `UnificationRule` + test S1–S10 **restano**).

**Sicurezza/permessi (✅):** `Application/Auth/EditAuthorizationService.cs`:
- **Admin** = staff position da due set: **ruoli di divisione** (`DivisionOptions.Code` + `AdminRolePatterns` → `^{Code}-{ruolo}$`, es. IT-DIR/IT-WM/IT-AOC) **e ruoli ACC-scoped/chief** (`AdminAccRolePatterns` → `^{prefissoIcao}[A-Z0-9]+-{ruolo}$`, es. `LIRR-CH`/`LIMM-ACH`) → edita tutto + gestisce permessi. Override esplicito opzionale via `Auth:AdminStaffCodes`. **Divisione configurabile** (sezione `Division`): vedi §7.
- **Multi-divisione:** tutto ciò che cambia passando divisione è in `DivisionOptions` (Application): `Code`, `IcaoPrefixes`, `AdminRolePatterns`, `AdminAccRolePatterns`. Il **contenuto seed** (Roma/LIRR) resta dato separato.
- **Grant per-ACC** (`EditGrant`, VID→ACC): chi non è admin edita una ACC solo con grant. Schermata `/vsop/admin/permessi` (solo admin).
- **Lock** documento esclusivo (30 min sliding, atomico via `ExecuteUpdateAsync`, **force admin**) → `EditConflictException`. **Concorrenza ottimistica** (`RowVersion` su `ContentBlock`/`DocumentSection`).
- **Lock risorsa** per le pagine admin senza documento (`EditResourceLock`, `IResourceLockService`): le 4 pagine di struttura condividono `admin:structure`, newdoc ha `editor:newdoc`; una persona alla volta (barra `EditLockBar`, TTL 3min + heartbeat 60s + force admin).
- **Validazione**: `UnificationRule` hard, trasferimenti soft. Verifiche **sempre server-side**. Security review: XSS in `AorBlock` corretto.

**Persistenza:** `VipiDbContext` mappa tutte le entità; enum→stringa; **lista migrazioni autoritativa = `docs/spec/modello-dati.md` §9.8** (fino a **`AddAirportCoordsAndTwrSyntheticShape`**, round 22). Seed (solo fixture di test, **non** seminato all'avvio): `RomaStructureSeed`, `RomaContentSeed`, `RomaAirportSeed`, `RomaVloaSeed`, `RomaTransferSeed`. ⚠️ **In produzione i `Sector` sono una proiezione dei cataloghi** (round 20): non si creano a mano, vedi `docs/spec/modello-dati.md` §9.12.

**Modello dati — aggiunte rispetto a `docs/spec/modello-dati.md` §3:** **`TransferFlow`** (settore mittente + tipo + aeroporto) → **`TransferPoint`** (CoP/livello strutturato/settore ricevente `NextSector`); risoluzione live **risale la gerarchia globale** (`ParentCallsign`/`ParentSectorId`), terminale **UNICOM** (no enum fallback). `EditGrant`; campi **lock** su `Document`; `RowVersion` su `ContentBlock`/`DocumentSection`.

**Live IVAO (✅):** `src/Vipi.Infrastructure/Ivao/` — `OnlineAtcCache` (singleton, `IOnlineAtcProvider`), `IvaoApiClient` (`/v2/tracker/now/atc/summary`, filtro prefisso `LI`), `IvaoTokenProvider` (client_credentials, solo per i membri divisione: tracker pubblico), `AtcPollingHostedService` (60s), `IvaoOptions`. Transport **SSE** `/vsop/live/atc` + `vipi-live.js`. `VipiViewService` calcola AoR reale quando `live=true`; `RidottaPage` `InteractiveServer`. Decisione in **ADR-0003**.

**Indipendenza dalla sorgente (✅, ADR-0006):** porte dati esterne **neutre** (`IAirportDirectory`/`IAirportDetailProvider`/`IUserDirectory`/`IOnlineAtcProvider`, DTO `Source*`); adapter IVAO selezionato da **`DataSource:Provider`**. `Vid`→`UserId` ovunque (a video resta "VID"). **Policy di import** (`ImportPolicy`, categorie `{TransitionAltitude, Runways, Sectors}`, pagina `/vsop/admin/sorgenti`): dati di sorgente in sola lettura, enforcement a difesa in profondità.

**Fonte unica settori (✅ Round 20):** cataloghi `AccSector`/`AirportSector` = fonte autoritativa; `Sector` = proiezione (`ISectorProjectionService.SyncFromCatalogsAsync`). Gerarchia per callsign (`ParentCallsign`, cross-ACC) editata in `/vsop/admin/sectorstructure` (`IHierarchyEditingService`). Dettagli: `docs/spec/modello-dati.md` §9.12.

**Shape tonda TWR + coord aeroporto (✅ Round 22):** le TWR senza poligono reale (IVAO le espone come `"[]"`) ricevono una **shape circolare 5 NM** sintetica così da poterle disegnare. `CircleShapeBuilder` (puro, formato `[[lng,lat],…]`), `TowerShapeFallbackService` (genera solo sulle vuote — decise col `AorPolygonProjector` —, marca `IsShapeSynthetic=true`, mai sovrascrive shape reali). Centro = `Airport.Latitude/Longitude`, popolate all'import dal blocco `airport` del dettaglio `/v2/ATCPositions/{compose}` (`SourceAtcPosition.AirportLatitude/Longitude`); ripiego = centro del poligono di un settore fratello. Job in `AirportSectorImportHostedService` (import isolato in try: il fallback gira anche senza credenziali). **TODO futuro:** shape reali TWR dal **sectorfile GitHub** via `DataSource:Provider` → rimpiazzano solo le sintetiche. Dettagli: `docs/spec/modello-dati.md` §9.14.

**Bridge Aurora (✅ 3 ago 2026, branch `feature/aurora-bridge`, NON ancora in `main`):** tool desktop che
scrive nel tag di Aurora il livello a cui cedere il traffico al prossimo ente.
- **Lato sito:** `TransferMatcher` (puro, `Application/Content/`) + `ITransferMatchService` + endpoint
  **`POST /vsop/api/v1/transfers/resolve`** (in `MapVipiModule`, anonimo e read-only, tetto per IP via
  `RequestRateLimiter`, sezione di config `AuroraBridge`). Il matching valuta CoP (fix da `#TRPATHL`, poi
  rotta; jolly `ALL`/`ALL to GR`, range aerovie `Y01-Y12`), parità semicircolare, condizione pista contro
  `#CTRLRWY`, next ATC già impostato — e restituisce candidati **motivati in italiano**.
- **Lato tool:** `Vipi.AuroraBridge.Contracts` (contratto), `.Core` (protocollo TCP 1130, client HTTP con
  cache su disco, orchestratore, ViewModel), `Vipi.AuroraBridge` (shell Avalonia), `tools/Vipi.AuroraBridge.Cli`
  (verifica end-to-end), `tools/Vipi.AuroraProbe` (sonda del protocollo).
- **Vincoli di Aurora accertati sul campo:** l'**XFL non è scrivibile** (nessun comando esiste, si scrive
  l'etichetta quota con `#LBALT`), si scrive **solo sul traffico assunto**, e la porta 1130 si apre solo
  riapplicando *3rd Party Software Access* **nella sessione in corso**. Cinque inesattezze della wiki IVAO
  documentate in `docs/design/piano-aurora-bridge.md` §11.
- Guida utente: `docs/guide/aurora-bridge.md`. Contratto: `docs/reference/api-aurora-bridge.md`.

---

## 5. PROSSIMI PASSI (ordinati per valore)

0. **Bridge Aurora — portarlo in produzione:** il branch `feature/aurora-bridge` va rivisto e unito; finché
   l'endpoint non è rilasciato su `it.ivao.aero`, il tool funziona **solo** contro un host locale.
   Chiuse per decisione: i sorvoli LIBB senza livello sono lacuna redazionale (il tool non deve indovinare),
   e il pacchetto macOS lo farà chi ha una macchina Apple.

1. **Live IVAO — rifiniture aperte:**
   - **Identità "P"** legata al callsign connesso del CH loggato (oggi selettore manuale in Ridotta).
   - **Mapping token-handler → callsign** trasferimenti (oggi euristica match-segmento). Valutare tabella esplicita.
   - **Endpoint membri divisione** (`/v2/divisions/IT/members`) da confermare.
   - Estendere `live=true` a **vIPI aeroporto / vLOA** (oggi solo ACC Ridotta).
2. **Dati reali:** METAR/TAF ✅ (NOAA). Shape AoR ✅ (poligono IVAO). **SID ✅** (sectorfile Aurora GitHub, round 34, sez. config `Sectorfile`). **AoR 3D ✅** (Three.js r128 vendorizzato: tab 2D/3D nel blocco AoR + pagina `/vsop/aor3d/{Kind}/{Key}`; settori estrusi per banda FL, con **basemap geografica CartoDB come pavimento** — proiezione Web Mercator, toggle «Mappa base» — e rendering leggibile: selettore «Altezza» ×0.25→×2 con default ×0.5, etichette come overlay HTML con declutter, chip settore condivise col 2D — vedi `docs/feature/2026-07-31-aor3d-leggibilita.md`; il link «Apri pagina» è **rimosso** in attesa di rilavorare la pagina dedicata, che resta raggiungibile a URL diretto). **Shape reali TWR ✅** (dal sectorfile GitHub, `GithubTowerShapeService`: 68 TWR su 84 hanno il poligono vero, i 16 cerchi sintetici restanti sono torri che nemmeno `twrs.tfl` contiene — verificato il 9 agosto 2026). **Minime MVA ❌ scartate** (9 agosto 2026): nel sectorfile la struttura dei file MVA non dice a quale settore appartiene un'area, e un import dovrebbe indovinarla — una minima attribuita al settore sbagliato è peggio di una minima assente. Se serviranno, saranno **editoriali**, non importate. Vedi `docs/lavori-aperti.md` §E2. Nota AoR 3D: i settori senza limiti admin estrudono GND→UNL (banda piatta) → il rilievo 3D emerge solo coi `LowerLimit`/`UpperLimit` valorizzati.
3. **Fonte unica (Round 20) — follow-up:** doc+AoR girano ancora sui `Sector` (proiezione), non direttamente sui cataloghi. Eliminazione totale di `Sector` + **risoluzione live** "chi controlla l'aeroporto adesso" (presidiato se DEL/GND/TWR online, altrimenti primo antenato online risalendo `ParentCallsign`) = fase live. ✅ **Fatto per i trasferimenti:** `ITransferService.ResolveForAccAsync` + `ITopologyProvider.BuildGlobalAsync` risolvono mittente e ricevente risalendo la gerarchia globale (terminale UNICOM); Ridotta li mostra nidificati Settore ▸ Aeroporto ▸ Tipo. Resta da estendere la stessa risalita alla "presidenza aeroporto" generale.
4. **Auth di produzione:** adapter reali `ICurrentUserProvider` — `HostIdentity` (A/B, claim `Ivao.It`) e OIDC (C); mappare gli **staff code reali** (§6). Montare la RCL nel sito host.
5. **Copertura/rifiniture:** viewer **audit log**, "scarta bozza", editor visuale mappe AoR, test property-based AoR, rifinitura UI.

---

## 6. Nodi aperti / decisioni
**Ancora aperte:**
- **Staff code esatti IVAO:** admin derivati da `Division.Code` + ruoli di divisione (`IT-DIR/ADIR/WM/AWM/AOC/AOAC/AOA<n>`) **e** ruoli chief ACC-scoped (`{ACC}-CH`/`{ACC}-ACH`, es. `LIRR-CH`), da confermare col sito host. I chief (CH/ACH) ora **sono** admin completi (`AdminAccRolePatterns`); l'auto-elenco per il dropdown grant resta via `IDivisionMembersProvider` (path `DivisionMembersPathFormat` = `/v2/divisions/{Code}/members`, da confermare).
- Identità **P** = callsign connesso del CH (oggi selettore manuale); mapping token-handler trasferimenti (oggi euristica); GeoJSON vs WKT (shape); formato/schedulazione parsing sectorfile (SID + minime).

**Risolte (storico):** modello editing persistente; autorizzazione (admin via staff code + grant per-ACC); lock 30 min + force admin; validazione hard/soft; export = stampa browser; trasporto live = **SSE** (ADR-0003); polling cache singleton 60s.

**Fix collaterali round 21:** `NewDocumentPage` naviga all'editor con **`forceLoad:true`** dopo la creazione (evitava lo stale read «documento non esiste»). `AdminTrasferimentiPage` — i dropdown sector-pick selezionano su **`@onmousedown`** (non `@onclick`): in Blazor Server il `@onblur` chiudeva il dropdown prima del click.

**Nota tecnica round 22 (importante per il debug):** la sorgente IVAO espone le TWR con **`regionMapPolygon = "[]"`** (array vuoto), **non** null — il «vuoto» NON si rileva in SQL (`null`/`''`) ma **provando a proiettare** col `AorPolygonProjector` (`Project(raw) is null` ⇒ vuoto/degenere). Il centro del cerchio viene dal blocco **`airport`** del dettaglio **`/v2/ATCPositions/{compose}`** (NON da `/v2/airports`, che richiede scope `configuration`). Credenziali IVAO reali in **user secrets** (id `79756a9b-…`), `appsettings.json` le ha **vuote**. Le coordinate si popolano solo all'**import** (job all'avvio), quindi serve **riavviare il Host** per vederle.

## 7. Note operative per la nuova chat
- **Configurazione:** riferimento completo in `docs/guide/config.md` (sezioni `Division`/`Ivao`/`Auth`, secrets, env var). Divisione/admin: ADR-0004.
- **Caveman mode** spesso attivo in queste chat (comunicazione compressa) — non è parte del prodotto.
- **Divisione pilota:** Italia (`Division:Code=IT`), **ACC pilota:** Roma (LIRR). Validare su una sola ACC prima di estendere.
- **Brand:** palette §15.1 di `docs/design/piano-vipi-tool.md` (blu `#0D2C99`…), font Nunito Sans + Poppins; tema in `Vipi.Ui/wwwroot/vipi-theme.css` (include `@media print`).
- **Parte più rischiosa:** logica AoR/visibilità → coperta da test S1–S10; mantenerla testata ad ogni modifica.
- **Pagine interattive** usano `@rendermode InteractiveServer` (editor, trasferimenti, ricerca, changed, admin).
- **Sicurezza:** ogni nuova operazione di scrittura deve passare per i service Application (guardia authz + lock), mai bypassare dal repo/UI.
- **Sorgente dati (ADR-0006):** non reintrodurre nomi IVAO in Application/UI — usa le porte neutre; l'adapter IVAO resta in `Infrastructure/Ivao/*`, selezionato da `DataSource:Provider`.
- **VID vs UserId:** nel **codice** è `UserId`; a **video** resta "VID". Non rinominare le label.
- **Dati di sorgente = sola lettura:** se aggiungi un campo che la sorgente può fornire, trattalo come categoria `ImportPolicy` (vedi `source-decoupling-and-import-policy` in memoria). I settori sono proiezione dei cataloghi (Round 20).

---

## 8. Mockup v2 — storico UI
🗑️ **`mockups/vipi-ui-mockup-v2.html` ELIMINATO il 2026-08-01**, insieme alla cartella `Esempi documenti/` (i .docx di partenza). Le 17 schermate del mockup sono ormai **tutte** derivate in componenti Blazor reali (vedi §4) e il prodotto ha superato il prototipo: il riferimento visivo oggi è l'app. Chi cerca l'originale lo trova nella storia git (`git show 8d661c4:mockups/vipi-ui-mockup-v2.html`); i doc più vecchi che lo citano per path sono record di sessioni passate, non istruzioni.

Note ereditate dal mockup, ancora valide: SCCAM e Aree regolamentate sono sezioni top-level; la vLOA ha due AoR e due tabelle frequenze; gli APP non remotizzati separano i trasferimenti verso ACC e verso torre.
