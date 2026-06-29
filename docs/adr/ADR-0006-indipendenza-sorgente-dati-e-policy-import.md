# ADR-0006 — Indipendenza dalla sorgente dati e policy di import

**Stato:** Accettato
**Data:** 27 giugno 2026
**Decisori:** Carmine + assistente
**Riferimenti:** `ADR-0002` (D2/D5, astrazione identità), `SPEC_Modello_Dati.md` (§8), `docs/CONFIG.md` (§1b, §10), piano `atomic-stirring-nygaard.md`, memoria `source-decoupling-and-import-policy`.

---

## Contesto

Il sito è funzionalmente legato a IVAO: anagrafica aeroporti, postazioni/piste, utenti e ATC online arrivano dalle API IVAO v2. Due esigenze del gestore:

1. **Indipendenza dalla rete.** Se un domani la divisione cambia network — o si vuole alimentare i dati da un **DB interno** — deve bastare cambiare la fonte, non riscrivere l'app.
2. **Autorità del dato.** Tutto ciò che la sorgente può fornire deve essere **preso da lì e non modificabile dall'utente**; il gestore deve poter decidere **cosa** si importa e cosa resta manuale.

Stato di partenza: le chiamate HTTP IVAO erano già isolate in `Infrastructure/Ivao/*`, ma i nomi (interfacce `IIvao*`, DTO `Ivao*`, metodi `*FromIvaoAsync`, campo `Vid`) **trapelavano** il network nell'Application/UI, e non esisteva alcuna policy di provenienza: un utente poteva editare TA e ident pista che il re-import poi sovrascriveva.

---

## Decisione

### D1 — Porte dati esterne **sorgente-neutre**
Le interfacce in `Application/Abstractions` sono neutre: `IAirportDirectory`, `IAirportDetailProvider`, `IUserDirectory`, `IOnlineAtcProvider`, con DTO `Source*` (`SourceAirport`, `SourceRunway`, `SourceAtcPosition`, `SourceUserStaff`). Nessun nome IVAO trapela in Application/UI. I metodi di import sono `ReimportFromSourceAsync`/`MergeFromSourceAsync`.

### D2 — Adapter IVAO concreto, selezionato da config
L'implementazione IVAO resta in `Infrastructure/Ivao/*` (mantiene i nomi `Ivao*`: è UNA implementazione). Il provider attivo si sceglie con **`DataSource:Provider`** (`DataSourceOptions`); `VipiModuleExtensions.AddVipiModule` fa branch sul valore (oggi solo `"Ivao"`; sconosciuto → eccezione di avvio). Aggiungere un network diverso o un adapter **DB interno** = nuova implementazione + un branch, **senza toccare Application/UI** (coerente con ADR-0002 D5).

### D3 — Identità neutra: `Vid` → `UserId`
Il modello utente e le colonne DB non portano più un nome legato a IVAO: `CurrentUser.UserId`, `HostIdentityOptions.UserIdClaim`, e i campi `*UserId` (migrazione `Rename_Vid_To_UserId`). Le **label a video restano "VID"** — è il termine che i controllori usano — ma il codice è neutro.

### D4 — Policy di import globale **opt-out**
Una entità `ImportPolicy` (riga singola, default tutto importato) decide, per categoria (`ImportCategory { TransitionAltitude, Runways, Sectors }`), se il dato è **di sorgente** (autorevole, sola lettura) o **manuale**. Default opt-out: tutto importato; il gestore esclude singole categorie in **`/sop/admin/sorgenti`**. Granularità **globale** (predisposta per un futuro override per-aeroporto senza cambiare i punti di enforcement).

> 📝 **Aggiornamento round 16.** La categoria **`Atis`** è stata **rimossa**. Con la semplificazione del modello la **frequenza è un attributo del settore** (`Sector.DefaultFrequency`, una per settore) e l'ATIS è un `AirportSector` come gli altri (non più `Airport.AtisFrequency`, eliminato): la sua frequenza ricade quindi nella categoria `Sectors`. Migrazione `SimplifyDataModel`. L'enum attuale è `ImportCategory { TransitionAltitude, Runways, Sectors }`.

### D5 — Enforcement a difesa in profondità
Per le categorie importate: (a) **editor read-only** con badge 🔒 (`AeroportoEditorPage`), (b) **guard nei service** che rifiutano la scrittura (`ValidationException`), (c) **import policy-aware** — `ReimportFromSourceAsync` non passa al merge le categorie escluse, quindi i dati manuali dell'utente non vengono mai toccati. I campi **editoriali** (regole pista, SID, livelli TL, link frequenze, gerarchia settori) non sono categorie: sempre dell'utente.

---

## Conseguenze

**Positive**
- Cambiare fonte dati (altro network, DB interno, dataset statico) non tocca Application/UI: solo un nuovo adapter + config.
- I dati autorevoli di sorgente non sono più corrompibili dagli utenti; risolti i conflitti reali (TA + ident pista sovrascritti dal re-import).
- Il gestore ha il controllo esplicito su cosa importare, con default sicuro (tutto importato).

**Costi / impegni**
- Doppio livello di nomi (porte neutre vs adapter `Ivao*`): un piccolo overhead concettuale, ripagato dalla sostituibilità.
- Rename `Vid`→`UserId` esteso (codice + DB): una migrazione con `RENAME COLUMN` (verificata, zero perdita dati). Nota cosmetica: alcuni parametri/locali sono diventati PascalCase.
- La policy «Settori» è applicata in modo mirato (blocca i settori d'**aeroporto** manuali; i settori d'**area ACC**, che la sorgente non fornisce, restano liberi).

---

## Alternative scartate

- **Mantenere i nomi `Ivao*` ovunque** — più semplice ma lascia il leaking del network in Application/UI: incompatibile con l'esigenza di indipendenza.
- **Policy opt-in (manuale di default)** — non rispecchia «tutto dal DB se posso»: avrebbe lasciato i dati di sorgente editabili per default.
- **Provenienza per-campo sull'entità** — massima flessibilità, ma complessità sproporzionata: scelta la policy globale per categoria.
- **Selezione provider via DI hard-coded** — meno trasparente di un valore di config esplicito (`DataSource:Provider`).

---

## Note di implementazione

- Config: `DataSource:Provider` (default `Ivao`); `Ivao` resta la sezione dell'adapter concreto.
- Storage policy: `ImportPolicy` (migrazione `AddImportPolicy`), `IImportPolicyStore`/`EfImportPolicyStore` (get-or-create riga 1), servizio admin `IImportPolicyService` (gate `EnsureAdmin`).
- Enforcement: `AirportProfileService.SetTransitionAltitudeAsync`/`SaveRunwaysAsync`, `StructureEditingService.AddSectorAsync`, `ReimportFromSourceAsync`; UI `AeroportoEditorPage` + `SorgentiAdminPage`.
- Test: `ImportPolicyTests` (store, guard, reimport policy-aware). Suite a 118 test verde.
