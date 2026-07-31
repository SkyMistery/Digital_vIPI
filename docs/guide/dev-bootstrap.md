# Bootstrap sviluppo — da DB vuoto a sito popolato

> **Perché questo doc (audit B3).** Il progetto **non semina** il DB all'avvio: è una scelta (i dati reali
> arrivano dalla sorgente/editor, non da fixture). Il rovescio è che un ambiente nuovo (o dopo un reset di
> `vipi.db`) va **ripopolato a mano** in una sequenza precisa. Questa è la checklist unica; prima era sparsa in
> più note dell'HANDOFF.

## Prerequisiti
- Credenziali IVAO in **user-secrets** (`Ivao:ClientId` / `Ivao:ClientSecret`), scope `tracker` sufficiente per
  ACC/settori/dettaglio postazione. `appsettings.json` le tiene **vuote** (vedi `config.md` §5).
  Senza credenziali l'import non parte, ma l'app sì (fallback statici dove previsto).
- In sviluppo l'utente è `DevCurrentUserProvider` (VID 704798, staff `IT-AOC` → **admin**, può tutto).

## Avvio
```bash
cd "vIPI Ivao Italy"
dotnet run --project src/Vipi.Host --urls http://localhost:5034   # crea/migra vipi.db, poi apri /vsop
```
Il DB SQLite viene creato e migrato all'avvio. Gli import periodici sono **gated** (`ImportState`): al primo
avvio popolano da zero, ai riavvii successivi **saltano** i fetch finché non scadono le 24h (o via bottoni
manuali). Lo stato/errore di ogni import è visibile in **`/vsop/admin/sorgenti`**.

## Sequenza di popolamento (ordine obbligatorio)
1. **ACC + settori** — `/vsop/admin/acc` → «Importa da sorgente». La sync proietta i `Sector` dai cataloghi
   (`AccSector`/`AirportSector`). I settori **non si creano a mano** (sono proiezione, Round 20).
2. **Aeroporti** — piste + Transition Altitude si importano da IVAO; bottone «Re-importa da IVAO (tutti)» su
   `/vsop/admin/airports`. Le shape tonde 5 NM delle TWR vuote si generano al job d'avvio (~30s).
3. **SID** — dal sectorfile Aurora GitHub (config `Sectorfile`); pubbliche solo dal ciclo AIRAC successivo.
4. **Gerarchia di copertura** — `/vsop/admin/sectorstructure`: imposta i padri per callsign (cross-ACC).
   Verificabile a valle in **`/vsop/admin/diagnostica`** (nessuna gerarchia dangling).
5. **Documenti** — «Crea nuovo documento» (vIPI ACC = N settori di scope, uno primario) → editor.

## Reset pulito
Cancella `vipi.db*` (inclusi `-wal`/`-shm`, vedi WAL in ADR-0007) nella cartella `src/Vipi.Host/` e riavvia:
riparte da DB vuoto e ripercorre la sequenza sopra.

## Verificare una modifica a schermo
Per **guidare** l'app (non solo avviarla) usa la skill **`.claude/skills/verifica-live/`**: avvio su una **copia**
del `vipi.db` (la verifica pubblica e annulla release, quindi scriverebbe sui dati di sviluppo), driver
Edge+puppeteer-core, bersagli utili nel DB e trappole già pagate. Due cose che sorprendono:
- serve **`VipiAuth__Enabled=false`**, altrimenti in Development l'app pretende il login OIDC IVAO reale
  (`appsettings.Development.json` porta `Enabled=true`, e `useDevIdentity = IsDevelopment && !authEnabled`);
- le pagine sono `@rendermode InteractiveServer`, quindi la prima risposta HTTP è il **prerender**: un `200` non
  prova che la pagina funzioni, bisogna attendere l'aggancio del circuito Blazor.

## Note
- Le fixture Roma (`*Seed.cs`) esistono **solo come dati dei test**, non vengono seminate all'avvio.
- Salute dell'istanza, due tagli:
  - **`/vsop/health`** — quadro completo, da aprire a mano: Unhealthy se il DB non risponde o ci sono migrazioni
    pendenti; Degraded se incongruenze dati o cache ATC stantia (vedi audit Fasi 1–2).
  - **`/vsop/health/ready`** — sonda economica (due query), è quella che interroga Render (`healthCheckPath`) e
    lo smoke del container in CI. Non tocca il report di consistenza, che fa scansioni complete: va tenuta fuori
    da ciò che viene sondato di continuo.
  - Su Postgres il probe sulle migrazioni è saltato di proposito: lì lo schema lo fa `PostgresSchemaReconciler`
    (EnsureCreated), che non scrive in `__EFMigrationsHistory` — le riporterebbe tutte come pendenti.
