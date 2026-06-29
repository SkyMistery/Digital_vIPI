# ADR-0001 — Scelte architetturali fondanti

**Stato:** Accettato
**Data:** 16 giugno 2026
**Decisori:** Carmine + assistente
**Riferimenti:** `../design/piano-vipi-tool.md`, `../spec/modello-dati.md`, `../spec/logica-aor.md`, `../history/review-flusso-gap.md`

---

## Contesto

Il progetto trasforma le vIPI e vLOA della divisione IVAO Italia da documenti Word statici a un portale web interattivo, leggero e manutenibile, con consultazione a due livelli (Estesa/Ridotta), logica di visibilità live legata a chi è online (AoR top-down) ed editing per i ruoli staff (CH/AOD).

Questo ADR fissa le decisioni strutturali già confermate nelle fasi di pianificazione (round 1–4), così che il "perché" resti tracciato prima di scrivere codice. Le singole decisioni di dettaglio (es. parsing del sectorfile, formato shape) saranno oggetto di ADR successivi.

---

## Decisioni

### D1 — Stack: C# / ASP.NET Core con rendering server-side

> ⚠️ **Emendato da ADR-0002 (16 giu 2026):** l'UI passa da **Razor Pages** a **componenti Blazor** impacchettati come **Razor Class Library (RCL)** integrabile, per coerenza con l'host (Blazor Server) e per la portabilità. Il resto della decisione (server-side, JS minimo, no SPA pesante) resta valido.

Rendering server-side, JavaScript minimo (autocomplete, toggle tier, accordion UI), nessun framework SPA pesante.

**Perché:** footprint minimo e manutenibilità (RNF-1, RNF-2); il contenuto è prevalentemente documentale e si presta al server-side; meno superficie di build e dipendenze rispetto a una SPA.

### D2 — Clean Architecture a 4 progetti

Solution a quattro layer: `Domain` (entità e regole pure, nessuna dipendenza), `Application` (use case, interfacce/ports, DTO), `Infrastructure` (EF Core, client API IVAO, OIDC, cache), `Web` (Razor, endpoint, DI). Regola di dipendenza verso l'interno: Web → Infrastructure → Application → Domain.

**Perché:** testabilità della logica AoR (la parte più rischiosa) isolata nel Domain; sostituibilità delle dipendenze esterne (es. SQLite → PostgreSQL tocca solo Infrastructure); separazione netta dei layer (RNF-2).

### D3 — Persistenza: SQLite + EF Core con migrazioni versionate

Database SQLite, accesso via EF Core, enum salvati come stringa, concorrenza ottimistica (`RowVersion`/`ConcurrencyToken`), soft delete dove serve lo storico.

**Perché:** singola dipendenza DB, file unico facile da ospitare e da backuppare sul VPS divisionale (RNF-1). Volumi di dati modesti. Astrazione del repository per un'eventuale migrazione futura.

### D4 — Modello a contenuto strutturato (ContentBlock taggati)

I documenti non sono blob: sono alberi di `DocumentSection` (annidamento fino a 3 livelli) che contengono `ContentBlock` taggati con `Tier` (Reduced/Extended), `Visibility` (Operational/Handoff/Always), `ScopeSectorId` e formato (`Table`, `Prose`, `Image`, `List`, `AorMap`, `Callout`).

**Perché:** è ciò che abilita le due viste, il collasso per AoR, le tabelle filtrabili e la TOC dinamica. Senza struttura, RF-3 e RF-6 non sono realizzabili.

### D5 — Logica AoR e visibilità isolata e testabile

`IAorService.Resolve(P, O)` è puro (nessun I/O) e restituisce stato dei settori + ownership; `IContentService.BuildView` applica la tabella di verità della visibilità. Due collassi distinti: **live/AoR** (dominio) e **accordion UI** (presentazione), senza che il secondo sovrascriva il primo.

**Perché:** è la parte più delicata del sistema; va coperta da test (preferibilmente property-based) sugli scenari di `../spec/logica-aor.md`. Tenerla pura la rende deterministica e cacheable.

### D6 — Integrazione IVAO: polling lato server con cache condivisa

Un `BackgroundService` interroga l'ATC summary ogni ~60 s e aggiorna una cache in memoria; tutti i client leggono dalla cache. Aggiornamenti al browser via **Server-Sent Events**.

**Perché:** una sola chiamata al minuto a IVAO indipendentemente dagli utenti; il token non è mai esposto al browser (RNF-4). SSE è più leggero di WebSocket per aggiornamenti unidirezionali server→client.

### D7 — Autenticazione IVAO SSO (OIDC); ruoli da userStaffPositions

Login via OpenID Connect IVAO; permessi `CanEdit` derivati dalle staff position (CH/AOD ⇒ editor). Token gestito con flusso client_credentials, segreti fuori dal repository.

> ⚠️ **Chiarito da ADR-0002 (16 giu 2026):** negli scenari embedded (A/B) l'autenticazione è **ereditata dal sito host** tramite `ICurrentUserProvider`; le staff position arrivano già nei **claim di sessione**, quindi **non serve** la chiamata `GET /v2/users/{vid}/userStaffPositions` (resta utile solo per lo scenario autonomo C o come fallback). Il client OIDC proprio + redirect URL servono **solo** nello scenario C.

**Perché:** identità nativa della community, nessuna gestione password locale, ruoli sempre allineati allo staff reale (RF-7, RF-8).

### D8 — Navigazione a 4 ACC e ricerca/live coesistenti

Homepage con i 4 ACC (LIRR, LIMM, LIPP, LIBB) come ingresso documentale, con barra di navigazione persistente e breadcrumb; in parallelo restano la ricerca per callsign e il rilevamento postazione → modalità live/AoR.

**Perché:** la navigazione a 4 ACC è la consultazione naturale (decisione round 4), mentre la logica AoR agganciata alla postazione resta il valore operativo del tool. Le due non si escludono.

### D9 — Deploy: integrazione nel sito host (montaggio della RCL)

> ⚠️ **Emendato da ADR-0002 (16 giu 2026):** il modello "app separata dietro reverse proxy" è sostituito dall'**integrazione in-process** come RCL montata su una rotta del sito host (es. `/sop`). L'app autonoma dietro proxy resta possibile come **scenario futuro C** (host .NET minimo dedicato), senza riscrittura.

Il portale è una RCL Blazor montata dal sito host .NET; la logica vive in progetti separati referenziati.

**Perché:** stesso stack dell'host (e del sito futuro), autenticazione ereditata senza glue code, nessuna nuova superficie API, portabilità tra host. Vedi ADR-0002.

### D10 — Brand IVAO obbligatorio

Palette e tipografia ufficiali (Nunito Sans / Poppins), colori come CSS custom properties, font self-hosted. I colori semantici sono estesi deliberatamente anche ai blocchi `Callout` (da annotare nella guida di stile).

**Perché:** coerenza con l'identità della divisione; self-hosting per leggerezza e indipendenza da CDN.

---

## Conseguenze

**Positive:** logica critica testabile e isolata; footprint contenuto; sostituibilità delle dipendenze; sicurezza del token; un modello dei contenuti che abilita viste, collasso AoR e TOC dinamica.

**Costi / impegni:** la gerarchia top-down, l'ownership dei settori e le regole di unificazione sono **dato manuale** curato dagli editor (non derivabile dalle API) → serve una UI dedicata. Il modello a contenuto strutturato richiede l'inserimento manuale dei contenuti (nessun import dai docx). La correttezza del nascondimento per AoR dipende dalla qualità di questo data-entry.

**Dipendenze esterne:** registrazione dell'app su IVAO Developers (tempi di approvazione lunghi → avviata in parallelo a questo ADR).

---

## Decisioni rinviate (futuri ADR)

- Minime di vettoramento: parsing del sectorfile della divisione su GitHub (formato, schedulazione, mapping aree→quote) — *implementazione future*.
- Formato di storage delle shape AoR (GeoJSON vs WKT) e resa dell'interazione/overlap.
- API pubblica read-only (predisposta, non implementata).
- PWA offline per la vista ridotta.
