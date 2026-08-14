# Audit del database e della sua gestione — 14 agosto 2026

**Oggetto:** modello dati, mappatura EF, provider MariaDB/Pomelo, ciclo di vita dello schema, transazioni,
concorrenza, sicurezza dei dati a riposo, esercizio.
**Stato:** carta + **blocchi 1 e 2 eseguiti** (14 agosto 2026). Restano aperte solo cose che non sono
codice: la domanda sul **backup** (D4), e la consegna del `.sql`.
**Agg. 14 agosto, sera:** le quattro domande al committente hanno avuto risposta (§Domande). Tre chiuse, una
ritirata perché mal posta, **D4 aperta e grave** (nessuno sa chi faccia il backup). La risposta che cambia il
piano è D3: il cutover **non è ancora avvenuto**, quindi i lavori sullo schema costano zero fino alla
consegna del `.sql` e diventano un blocco a scadenza — vedi «Ordine di esecuzione».

---

## 0. Metodo, e il numero che cambia tutte le priorità

Prima di giudicare, ho **misurato** il database di sviluppo reale (`src/Vipi.Host/vipi.db`, 5,1 MB), che è la
copia più vicina ai dati di produzione:

| tabella | righe |     | misura | valore |
|---|---|---|---|---|
| `AirportSids` | 1481 | | totale righe (38 tabelle) | ~4 800 |
| `AirportTransitionLevels` | 368 | | `AuditLogs` | **20** righe, 1 solo `EntityType` |
| `Sectors` | 320 | | `DocReleases.PayloadJson` max | 221 KB (media 25 KB, 34 righe) |
| `DocumentSections` | 266 | | `ContentBlocks` corpo totale | **20 KB** su 199 righe |
| `SpecialAreaCenters` | 247 | | `MediaAssets` | 1 riga, 179 KB |
| `ContentBlocks` | 199 | | `AccSectors.RegionMapPolygon` max | 33 KB |

**Conseguenza da tenere ferma per tutta la lettura: a questa scala non esiste un problema di prestazioni.**
Nessuna scansione completa di questo database costa qualcosa di percepibile, su nessun motore. Quindi *non*
propongo indici, denormalizzazioni o cache: sarebbero lavoro contro un problema che non c'è, esattamente
l'errore che `dev-process-gates` chiede di non fare.

Quello che resta è **correttezza, sicurezza ed esercizio** — e lì i difetti ci sono, alcuni seri.

### Cosa ho trovato già a posto (non lo ripeto più sotto)

Il ramo MariaDB è la parte più curata del progetto: collation `as_cs` dichiarata per colonna e verificata
sulla **DDL generata** e non sui metadati; lunghezze delle colonne indicizzate con le coppie FK annotate
contro l'`errno 150`; assembly di migrazioni separato con un test che confronta modello e snapshot; dispatch
esplicito per provider in `MigrateVipiDatabase` con il ramo sconosciuto che lancia; `ServerVersion` fissata
invece che auto-rilevata; `AsNoTracking` praticamente ovunque; `QuerySplittingBehavior.SplitQuery` su tutti e
tre i provider; segreti mascherati nella diagnostica d'avvio; il dump che esclude `DataProtectionKeys`.
Nulla di tutto questo va rivisto.

---

## A. Correttezza — la concorrenza ottimistica è dichiarata ma **non funziona**

> Gravità: **alta**. È l'unico difetto che può far perdere lavoro a un editor senza dire niente a nessuno.

### A1. Sei entità su sette hanno un token di concorrenza inerte

`VipiDbContext.OnModelCreating` dichiara `IsConcurrencyToken()` su **sette** proprietà `RowVersion`
(`Document`, `DocumentSection`, `ContentBlock`, `SharedBlock`, `UnificationRule`, `TransferFlow`,
`DocumentProfile`), e il commento in cima alla classe promette «concorrenza ottimistica su entità editabili
(SPEC_Modello_Dati §6)».

Il `RowVersion` è un `byte[]?` **gestito dall'applicazione**: MariaDB non ha un `rowversion` automatico, e
nessuna delle tre configurazioni chiede a EF di generarlo (`ValueGeneratedOnAddOrUpdate` non c'è). Deve
quindi assegnarlo il codice. Cercando **tutte** le assegnazioni nel solution:

| entità | valore alla creazione | ruotato all'aggiornamento | token efficace? |
|---|---|---|---|
| `ContentBlock` | sì | **sì** (`EfEditingRepository.cs:630`, con `OriginalValue` dal client alla riga 623) | **sì** |
| `DocumentSection` | sì | **no** (es. rinomina titolo, `EfEditingRepository.cs:681`) | no |
| `Document` | **mai assegnato** | mai | no (colonna sempre `NULL`) |
| `DocumentProfile` | **mai assegnato** | mai | no |
| `SharedBlock` | **mai assegnato** | mai | no |
| `UnificationRule` | **mai assegnato** | mai | no |
| `TransferFlow` | **mai assegnato** | mai | no |

Un token che non cambia mai non è un controllo: EF emette `UPDATE … WHERE Id=@id AND RowVersion=@old`, e con
`@old` costante (o `IS NULL`) la clausola è **sempre vera**. Due editor che aprono lo stesso documento e
salvano in sequenza vincono entrambi: il secondo sovrascrive il primo, `SaveChangesAsync` ritorna 1 riga, e
non viene sollevata nessuna `DbUpdateConcurrencyException` — che infatti nel progetto è gestita in **un solo
punto** (`EfEditingRepository.cs:633`, il salvataggio del corpo di un blocco).

Non è teoria: il lock di editing (`EditResourceLock`) copre le pagine admin **senza** `Document`
(`structure`, `newdoc`); le pagine con documento si affidano proprio a questo token.

**Prova che propongo prima di correggere.** Un test d'integrazione che carica la stessa entità da due
`DbContext` distinti, salva su entrambi e pretende una `DbUpdateConcurrencyException` sul secondo. Scritto
**per tutte e sette** è la dimostrazione del difetto: oggi passerebbe solo su `ContentBlock`.

Dopo la correzione il test si assesta su **tre** entità (`ContentBlock`, `Document`, `DocumentSection`) e va
affiancato da una **guardia complementare**: nessun'altra entità del modello dichiara `IsConcurrencyToken()`.
Serve perché la decisione qui sotto è «per queste quattro il last-write-wins è voluto» — e senza guardia, la
prossima entità nasce con un token decorativo e il difetto ricomincia da capo.

**Correzione — decisa il 14 agosto 2026 (risposta a D1), due mosse su insiemi disgiunti.**

1. **Rotazione centralizzata per `Document` e `DocumentSection`.** Un override di `SaveChangesAsync` sul
   `VipiDbContext` che, per ogni entry `Added`/`Modified` la cui entità dichiara un token `RowVersion`,
   riscrive `Guid.NewGuid().ToByteArray()`. Toglie il difetto alla radice — nessun percorso di scrittura
   futuro può dimenticarsene — e rende superflue le 20 assegnazioni a mano già sparse (che restano innocue).
   Sono le due entità che due editor aprono davvero insieme, quindi qui il controllo serve.
   ⚠️ L'override va scritto in modo da **non** toccare `OriginalValue`: la riga 623, che imposta il valore
   originale con il token arrivato dal client, deve continuare a funzionare.
2. **Rimozione del token da `UnificationRule`, `TransferFlow`, `SharedBlock`, `DocumentProfile`.** Il
   committente ha confermato che lì il **last-write-wins è voluto**: sono modificate da un editor alla volta,
   sotto lock. Via `IsConcurrencyToken()` e via la colonna. Una dichiarazione che non protegge niente è
   peggio dell'assenza: fa credere che ci sia una difesa.
   ⚠️ Quattro `DropColumn`, quindi **cambio di schema**: da emettere due volte (SQLite + MySQL) e — visto che
   il cutover non è ancora avvenuto (D3) — da far confluire nella rigenerazione di `InitialCreate` MySQL
   insieme a D1, non come migrazione a sé.

`ContentBlock` resta com'è: è l'unico che già funziona.

### A2. Il conflitto rilevato non ha una via d'uscita per l'utente

`EfEditingRepository.cs:633` cattura la `DbUpdateConcurrencyException` per l'unico caso funzionante. Vale la
pena guardare **cosa vede l'editor** quando scatta: se il risultato è la perdita del testo appena scritto, il
controllo esiste ma il danno resta. Da verificare guidando l'app (skill `verifica-live`), non per ispezione.

---

## B. Sicurezza dei dati a riposo

### B1. Le chiavi Data Protection sono in chiaro in un database che non è nostro

> Gravità: **alta in senso organizzativo**, bassa in senso tecnico-locale.

`VipiDataProtection.AddVipiDataProtection` registra `PersistKeysToDbContext<DataProtectionKeysDbContext>()`
**senza** `ProtectKeysWith…`. Su Linux non esiste DPAPI, quindi il key-ring finisce nella tabella
`DataProtectionKeys` come **XML in chiaro**. Quelle chiavi firmano e cifrano il cookie di autenticazione, il
token antiforgery e lo state OIDC.

Il punto non è il rischio di intrusione: è che il database di produzione sta su `atc.it.ivao.aero`, che è
**infrastruttura del committente**. Chiunque abbia `SELECT` su `itivao_atc` — l'amministratore di sistema di
Ivao.It, un backup, un pannello di hosting — può **fabbricare un cookie di sessione valido per qualunque
VID**, admin compresi. Questo aggira l'intero modello di autorizzazione, che poggia sui claim del login IVAO.

Il progetto ha già visto metà del problema: il dump di consegna esclude la tabella con la motivazione giusta
(«quelle chiavi decifrano i cookie di sessione: sono un segreto della *nostra* installazione»). La stessa
frase vale per la tabella viva sul loro server.

**Correzioni possibili, in ordine di preferenza.**

1. **Cifrare il key-ring a riposo** con `ProtectKeysWithCertificate(cert)`, dove il certificato (o la sua
   password) sta in un file letto dal servizio e **non** nel database: chi legge il DB non ha le chiavi.
   Costo: un file in più da consegnare e da non perdere — se sparisce, tutti vengono sloggati una volta.
2. **Riportare il key-ring su disco** (`PersistKeysToFileSystem`) in una directory di `/opt/vipi` che
   `vipi.service` possiede. Il motivo per cui è nel DB — disco effimero — vale per Render, **non** per un
   server con systemd e una cartella stabile. Costo: zero; la si perde solo se la macchina viene
   riprovisionata, ed è esattamente lo scenario che il commento in `DataProtectionSchema` cita ma che su
   `atc.it.ivao.aero` non è quello di tutti i giorni.
3. **Accettare e dichiarare**: scriverlo in `docs/adr` e nella consegna, così la decisione è di chi ospita.

Raccomando la **2** per il deploy `atc-ivao` (mantenendo il DB su Postgres/Render, dove il disco è davvero
effimero): è l'unica che toglie il segreto dal database senza aggiungere un file da custodire.
`DataProtectionSchema.UsesDatabaseKeyRing` è già il punto unico dove si decide — la modifica è di una riga
più un ramo di configurazione.

### B2. `AuditLog` non è protetto da cancellazione né firmato

Nessuna difesa impedisce a chi ha accesso al DB di riscrivere la storia. È accettabile — l'audit qui serve a
ricostruire una modifica, non a reggere in giudizio — ma va **detto**, perché il nome «audit» promette altro.
Nessuna correzione proposta; solo una riga in `docs/spec`.

---

## C. Esercizio e cutover — dove il ramo MariaDB è più scoperto

### C1. Nessuna configurazione di pool, timeout e resilienza sul ramo MySQL

`DependencyInjection.cs:59-65` configura Pomelo con `QuerySplittingBehavior` e `MigrationsAssembly`. Non c'è
altro. La stringa di connessione di `deploy/atc-ivao/appsettings.Production.json` è:

```
Server=localhost;Port=3306;Database=itivao_atc;User Id=itivao_atc;Password=…
```

Quindi valgono tutti i default di MySqlConnector, e nessuno di essi è stato scelto:

| impostazione | default che ci ritroviamo | perché è un rischio **qui** |
|---|---|---|
| `MaximumPoolSize` | **100** | un server condiviso di divisione applica quasi sempre `max_user_connections` (25÷50). Superato il tetto, l'errore è `has more than 'max_user_connections' active connections` — e arriva sotto carico, cioè il giorno della pubblicazione AIRAC |
| `EnableRetryOnFailure` | **assente** | scelta *motivata* in `VipiDataProtection` («server dedicato, non serverless»), ma copre solo il risveglio del compute. Non copre il riavvio di `mariadb.service`, il `wait_timeout` del server, un `KILL` da pannello |
| `DefaultCommandTimeout` | 30 s | accettabile |
| `ConnectionIdleTimeout` | 180 s | ragionevole, ma non verificato contro il loro `wait_timeout` |

**Correzione proposta.** Portare le tre impostazioni che contano nella stringa di connessione di produzione —
dove le può cambiare chi amministra, senza ricompilare — e aggiungere `EnableRetryOnFailure` al ramo MySQL
come già c'è su Npgsql:

```
…;MaximumPoolSize=20;ConnectionIdleTimeout=60;DefaultCommandTimeout=30
```

⚠️ **`EnableRetryOnFailure` non è gratis**: rende obbligatorio l'uso dell'execution strategy attorno a ogni
transazione esplicita. `EfUnitOfWork` lo fa già (e azzera pure il change-tracker a ogni tentativo, che è la
parte che di solito si dimentica), quindi il costo è già pagato — ma i **tre** soli chiamanti di
`ExecuteInTransactionAsync` vanno ricontrollati, e con essi `UseVipiDataProtection`, che esegue un
`ExecuteSqlRaw` all'avvio.

**Il numero: 20, e la domanda al committente si chiude senza risposta.** Avevo posto `MaximumPoolSize` come
dipendente dal loro `max_user_connections` (D2). È un errore di impostazione: quel valore serve a sapere dove
sta il *tetto*, ma il numero giusto lo detta il **traffico**, non il tetto. Il traffico atteso è di decine di
controllori — la stessa scala su cui sono tarati `DisconnectedCircuitMaxRetained = 25` e i 300 SSE. **20 sta
sotto qualunque limite di hosting plausibile** (i valori tipici sono 25÷50) ed è abbondante per quel carico.

Se un giorno servisse leggere il loro tetto: `SELECT @@max_user_connections, @@max_connections;`, da eseguire
**sul loro server** — il 3306 di `atc.it.ivao.aero` è su `localhost` e da qui non si raggiunge. Non vale la
pena farne una dipendenza.

### C2. Sei passate di manutenzione a **ogni** avvio, bloccanti, senza isolamento dei guasti

`Program.cs:145-154` esegue in sequenza, prima che l'app risponda a qualunque richiesta:

```
MigrateVipiDatabase → ReconcileVipiDocuments (6 riconciliazioni) → ProjectVipiSectors
  → BackfillVipiReleases → PruneVipiReleases
```

Tutte sono idempotenti e tutte sono `.GetAwaiter().GetResult()` su thread di avvio. Tre osservazioni:

1. **Sono migrazioni di dati travestite da avvio.** `ReconcileCustomSectionKeys`, `MigrateHiddenSections`,
   `ReconcileVloaSectionKeys`, `BackfillAreaCenters` sono riconciliazioni **one-shot** di problemi storici,
   riferite a documenti che oggi non esistono più. Rigirano per sempre, a ogni riavvio, su tutte le righe.
   A questa scala non costano nulla (§0) — ma sono codice vivo che nessuno rilegge, e ognuna ha diritto di
   **scrivere** nel database di produzione a ogni riavvio del servizio.
2. **Un guasto qualsiasi impedisce l'avvio.** Con `Restart=always` + `RestartSec=10` in `vipi.service`, un
   difetto in una di queste passate diventa un ciclo di riavvii, non un degrado. Non hanno né `try/catch` né
   un timeout.
3. **Nessuna gira in transazione.** `ReconcileVipiDocuments` fa 7 `SaveChanges` indipendenti su servizi
   diversi. Interrotta a metà (riavvio, `OOM`, DB che chiude), lascia uno stato intermedio che la passata
   successiva deve saper riprendere — cosa vera per costruzione (sono idempotenti) ma mai provata
   interrompendole davvero.

**Correzione proposta, in due tempi.**

- **Ora, a costo quasi zero:** avvolgere il blocco `ReconcileVipiDocuments`/`Backfill`/`Prune` (non
  `MigrateVipiDatabase`, che deve restare fatale) in un `try/catch` che **logga e prosegue**. Il sito che
  parte con una riconciliazione saltata è meglio del sito che non parte; il difetto si legge dal log e dalla
  diagnostica.
- **Poi, quando c'è tempo:** promuovere le quattro riconciliazioni storiche a **migrazioni** vere — righe in
  `__EFMigrationsHistory`, quindi eseguite una volta e mai più. È il posto dove EF le tiene, e cancella la
  domanda «questa serve ancora?» per sempre. Va emessa due volte (SQLite + MySQL), come ogni cambio di schema.

### C3. Gli import multi-passo non sono transazionali

L'import da sorgente (`/vsop/admin/acc`, bottone «Importa da sorgente») attraversa
`ImportAsync` → `ImportSubcentersAsync` → `ImportSpecialAreasAsync` → `PruneSpecialAreasNotInAsync`, ognuna
con il **proprio** `SaveChangesAsync`; `PruneSpecialAreasNotInAsync` ne ha addirittura **due**
(`EfAccAdminRepository.cs:185` e `:194`: prima toglie i legami, poi le aree rimaste orfane). Nessun chiamante
apre una transazione: `ExecuteInTransactionAsync` ha **tre** soli call site in tutto il progetto
(`NeighbourImportService` ×2, `ReleaseService` ×1).

Una caduta di rete verso IVAO fra la seconda e la terza chiamata lascia i cataloghi **parzialmente**
aggiornati; subito dopo `ProjectVipiSectors` proietta i settori su quello stato parziale, e il risultato è
una gerarchia coerente con dati che non sono mai esistiti tutti insieme.

**Correzione proposta.** Avvolgere l'orchestrazione dell'import (il servizio applicativo, non i repository)
in `ExecuteInTransactionAsync`. È già la forma usata da `NeighbourImportService`: c'è un precedente da
copiare, non un pattern da inventare. Costo: basso. ⚠️ Da fare **dopo** C1, se si adotta
`EnableRetryOnFailure`, perché l'azione dev'essere rigiocabile da capo — e il `ChangeTracker.Clear()` che
`EfUnitOfWork` già fa la rende tale solo se le operazioni interne rileggono da zero (vale per gli upsert
dell'import; da verificare per il prune).

### C4. Due proprietà del server che l'applicazione **assume** senza verificarle

`docs/deploy/mariadb/README.md` le elenca già come incognite del loro server, e restano incognite:

- **`sql_mode`.** Se lo strict mode è spento — comune sugli hosting condivisi — una stringa troppo lunga
  **tronca in silenzio** invece di lanciare. Tutte le lunghezze di `MySqlStringLengths` sono dimensionate con
  margine sui dati veri, quindi il caso è remoto; ma se accade non lo scopre nessuno.
- **`max_allowed_packet`.** L'app assume **≥ 4 MB** (le immagini sono `longblob` e il taglio applicativo è a
  3 MB). Misurato oggi: l'unico `MediaAsset` pesa 179 KB e il `PayloadJson` più grosso 221 KB — quindi siamo
  a un ordine di grandezza di distanza. Ma il tetto **si supera in un colpo solo**, il giorno in cui qualcuno
  carica una carta aeroportuale da 3 MB, e l'errore che esce (`Got a packet bigger than…`) non somiglia a
  «l'immagine è troppo grande».

**Correzione proposta, la più economica di tutto il documento.** Aggiungere a `VipiHealthCheck` (il taglio
`full`, quello che apre un umano — **non** la sonda `ready`) due letture, solo su provider MySQL:

```sql
SELECT @@sql_mode, @@max_allowed_packet;
```

`Degraded` con il valore letto se `sql_mode` non contiene `STRICT_TRANS_TABLES` o se
`max_allowed_packet < 4194304`. Costo: ~20 righe più un test. In cambio, due domande aperte al committente
diventano **una schermata da guardare** dopo il cutover, e smettono di dipendere da una risposta via mail.

### C5. `vipi.service` dipende da `mysql.service`, che su Debian è un alias

`After=network.target mysql.service` funziona su Debian (il pacchetto MariaDB fornisce l'alias
`mysql.service`), ma è fragile e comunque `After=` **non** garantisce che il server accetti connessioni,
solo che l'unità sia partita. Con `Restart=always` il ciclo si chiude da sé in 10 secondi, quindi non è un
guasto — è rumore nei log al riavvio della macchina. Con C1 (retry) sparisce anche quello.

---

## D. Debito di schema — vero, ma **non** da pagare adesso

### D1. 128 colonne `longtext`, di cui una trentina sono enum a valori corti

La regola oggi in vigore è: si dimensiona una colonna stringa **solo se indicizzata** (o se ha un `DEFAULT`,
perché MySQL non ammette default su `TEXT`). Tutto il resto nasce `longtext`. Contate nella DDL generata:
**128** colonne. Fra queste, enum salvati come stringa il cui insieme di valori è chiuso e corto:

`AuditLogs.Action`, `DocReleases.Status`, `EditorTasks.Priority`, `CoordinationPoints.Kind`,
`ContentBlocks.Tier|Format|Visibility|CalloutKind`, `DocumentParties.Role`, `DocumentVersions.Status`,
`Sectors.Type|Kind|ApproachKind`, `TransferFlows.Kind`,
`TransferPoints.LevelUnit|LevelConstraint|Parity|VerticalState`, `SpecialAreas.Type`,
`NeighbourCandidates.Status`, `SharedBlocks.Format`, `AirportRunwayRules.DateParity`, `AirportSids.Type`…

**Perché non è un problema di prestazioni:** a 4 800 righe, no. Lo dichiaro per non tornarci.

**Perché è comunque debito:**

1. **Nessuna di queste colonne è indicizzabile senza una migrazione di tipo.** Il giorno in cui una query
   avrà bisogno di un indice su `DocumentVersions.Status`, non si aggiunge un indice: si cambia il tipo di
   colonna su un database di produzione altrui. Su MySQL è un `ALTER TABLE` che riscrive la tabella, in DDL
   non transazionale (il `README` di `deploy/mariadb` avverte già: «se fallisce a metà, il database resta
   sporco»).
2. **La regola scritta è più stretta della regola vera.** `MySqlStringLengths` documenta «lunghezze delle
   colonne stringa **indicizzate**», e il commento su `RenderMode` mostra che la regola era già stata
   allargata una volta per necessità (i `DEFAULT`). Una terza eccezione arriverà.
3. **In non-strict mode** (§C4) un `longtext` non tronca mai, ma nemmeno protegge: nessuna delle due metà è
   quella voluta.

**Correzione proposta, e il momento giusto per farla.** Estendere `MySqlStringLengths` a **tutti gli enum
salvati come stringa** — 32 caratteri, come già fanno le cinque voci di `TransferPoint` e le due di
`Document` — con una regola dedotta invece che una mappa a mano: in `MySqlStringLengths.Apply`, per ogni
proprietà il cui tipo CLR è un `enum` (prima della conversione a stringa) e che non è già in `Map`,
`SetMaxLength(32)`. È lo stesso criterio che `MySqlCollation.Apply` usa già per riconoscere gli enum via
`GetProviderClrType()`.

⚠️ **Va accompagnato da una guardia**: nessun nome di valore di enum del dominio deve superare 32 caratteri —
un test che li enumera per riflessione, come già fa `IndexedStringLengthTests` per la copertura.

**Quando: ADESSO — la finestra è aperta e si chiude alla consegna.** Il committente ha confermato (D3) che
il cutover sul loro server **non è ancora avvenuto**. Quindi non c'è nessun `ALTER TABLE` da fare su un
database vivo: si **rigenera `InitialCreate`** del set MySQL, che ancora nessuno ha eseguito, e le colonne
nascono già `varchar(32)`. Costo **zero**.

Questo ribalta la priorità della voce: da «debito da rimandare» a «lavoro da fare prima di consegnare il
`.sql`». Dal momento in cui il dump gira sul loro server, la stessa modifica torna a costare un `ALTER TABLE`
su trenta colonne in DDL non transazionale.

⚠️ **Rigenerare `InitialCreate` significa buttare e rifare le quattro migrazioni MySQL**, non aggiungerne una
quinta. È legittimo **solo** perché nessun database le ha mai applicate — e smette di esserlo nell'istante in
cui una `__EFMigrationsHistory` reale le contiene. Vale la pena verificare che nemmeno la MariaDB **locale**
di prova venga tenuta viva fra una sessione e l'altra: lì la ricetta è già `DROP DATABASE` + rifai, quindi
non è un ostacolo.

**Da far confluire nella stessa rigenerazione:** i quattro `DropColumn` di `RowVersion` decisi in §A1. Una
sola riscrittura dell'`InitialCreate`, non due.

### D2. `AuditLogs` senza indici e senza retention — **misurato e archiviato**

La tabella ha PK e nient'altro; `EfAuditLogReader.ListForEntityAsync` filtra su
`EntityType`+`EntityId`, che sono **`longtext`** e come tali non indicizzabili senza D1.

**Misura: 20 righe, un solo `EntityType` distinto.** Non c'è niente da correggere. Coerente con quanto già
rilevato nell'audit dell'11 agosto (retention audit: 19 righe → voce ribaltata dal dato).

**Condizione di riapertura**, da scrivere e dimenticare: se `AuditLogs` supera **50 000 righe**, servono
insieme la lunghezza sulle due colonne (D1) e un indice `(EntityType, EntityId, Id DESC)`. Prima di allora,
qualunque lavoro qui è speculativo.

---

## E. Cose viste, valutate e **non** proposte

Le elenco perché il prossimo che guarda non le riscopra da capo credendole nuove.

| cosa | perché non la propongo |
|---|---|
| Indici su `Sectors.AirportIcao`, `Sectors.Kind`, ecc. | 320 righe. §0 |
| Ricerca globale: `LOWER(col) LIKE '%…%'` su `ContentBlocks` | non indicizzabile per costruzione, ma il corpo **totale** dei blocchi è 20 KB. Il filtro è già stato spostato nel DB e il commento spiega perché resta case-insensitive nonostante `as_cs`: è giusto così |
| N+1 in `EfTransferRepository` (query dentro `foreach`, righe 264-265 e 319) | percorsi di editing su gruppi di varianti, decine di righe, azione manuale di un editor. Reale, irrilevante |
| Immagini servite leggendo il `longblob` intero anche con ETag valido | l'URL **è** lo sha, quindi si potrebbe rispondere `304` prima di toccare il DB. Elegante, ma oggi c'è **1** immagine. Da riprendere se `MediaAssets` cresce |
| `AddDbContextFactory` al posto di `AddDbContext` scoped | è la cura strutturale al «second operation» di Blazor, già mitigato con `OwningComponentBase` su 6 componenti. È un refactor ampio con un beneficio oggi teorico: merita una scheda propria, non una riga in un audit del DB |
| Wildcard `%`/`_` non escapati nella ricerca | chi digita `%` vede tutto invece di niente. Nessun rischio, comportamento bizzarro |
| `ToLocalTime()` in `EditLockBar` e affini | rende nel fuso **del server**, non del browser. È un difetto di UI, non di DB: fuori perimetro, ma va segnalato a chi tiene la UI |

---

## Ordine di esecuzione — rivisto il 14 agosto dopo le risposte

Le risposte del committente hanno spostato una cosa sola, ma di molto: **il cutover sul loro server non è
ancora avvenuto**, quindi tutto ciò che tocca lo schema costa zero **finché il `.sql` non è consegnato**.
Quel gruppo di lavori sale in cima e diventa una scadenza, non una priorità.

### Blocco 1 — ✅ ESEGUITO il 14 agosto 2026

1. ✅ **A1 — il test che dimostra il difetto.** `ConcorrenzaOttimisticaTests`, scritto prima della cura:
   **8 rossi su 8**, uno più dei sei previsti. Il di più è `ContentBlock`, e sposta la diagnosi — vedi
   «Cosa ha detto la prova» qui sotto.
2. ✅ **A1 — rotazione centralizzata** in `VipiDbContext.SaveChanges/SaveChangesAsync` per `Document`,
   `DocumentSection`, `ContentBlock`; **token e colonna rimossi** dalle altre quattro. Guardia
   `Solo_le_entita_decise_dichiarano_un_token_di_concorrenza` sull'elenco esatto, più il test speculare che
   pretende che dove il last-write-wins è voluto il secondo salvataggio **passi**.
3. ✅ **D1 — regola sugli enum** in `MySqlStringLengths.Apply` (`EnumChars = 32`), con due guardie: nessun
   nome di valore supera i 32 caratteri, e ogni enum esce da `Apply` con una lunghezza.
4. ✅ **Migrazioni**, una per serie: `20260814092312_DropConcurrencyTokensUnused` (SQLite) e
   `20260814092329_EnumLengthsAndDropUnusedTokens` (MySQL, **48 `AlterColumn` + 4 `DropColumn`**).
   ⚠️ **Non** una rigenerazione di `InitialCreate`, come questa carta proponeva: vedi «La deviazione».
5. ✅ **Provate su database veri**, non solo generate — esiti in «Cosa ha detto la prova».

**Suite: 2 423 test verdi** su entrambi i target (net8 1 227, net10 1 196), `dotnet build -c Release
--no-incremental` **0 warning**.

### Cosa ha detto la prova (e che l'ispezione non aveva detto)

**1. `ContentBlock` era rosso anche lui, e questo cambia la diagnosi.** La carta lo dava come «l'unico che
funziona». È vero solo attraverso `EfEditingRepository.UpdateBlockAsync`: la rotazione viveva **in quel
metodo**, non nel modello. Qualunque altro percorso che salvasse un blocco passando dal context non
proteggeva niente. La conclusione giusta non era «sei entità su sette sono rotte» ma **«la garanzia era una
proprietà di un metodo invece che del modello»** — che è esattamente ciò che la rotazione centralizzata
sistema, e la ragione per cui metterla nel context era la mossa giusta e non solo la più comoda.

**2. La migrazione SQLite provata su una copia del `vipi.db` reale** (5,1 MB, 40 tabelle): conteggi
**identici tabella per tabella**, `PRAGMA foreign_key_check` senza violazioni, `PRAGMA integrity_check` = ok.
L'unica differenza è `__EFMigrationsHistory`, 70 → 71 righe. Il `DropColumn` su SQLite ricostruisce la
tabella, quindi non era scontato.

**3. Le migrazioni MySQL applicate a una MariaDB 11.4.10 vera**, con utente ristretto come il loro, in
entrambi i percorsi che contano:
- **da zero** (il cutover): 5 migrazioni applicate, e sul database risultante — letto da
  `information_schema`, non dai metadati EF — **171 colonne** con `utf8mb4_uca1400_as_cs` + 2
  `utf8mb4_general_ci` (`__EFMigrationsHistory`, che crea EF e non governiamo), **42 colonne `varchar(32)`**,
  **esattamente 3 `RowVersion`** superstiti, 104 `longtext` rimasti — tutti testo libero vero (`Body`,
  `RegionMapPolygon`, nomi, note), cioè fuori dalla regola per progetto;
- **in aggiornamento su dati veri**: copia di `itivao_atc` (4,5 MB), 3 migrazioni pendenti applicate.
  Conteggi identici prima e dopo su tutte le tabelle campione (Accs 28, AirportSids 1481, ContentBlocks 298,
  DocumentSections 347, SpecialAreaCenters 247, TransferFlows 40), i valori enum **si rileggono tutti**
  (`Draft`/`Published`, `Callout`/`Image`/`Prose`/`Table`, `Acc`/`Airport`), **zero avvisi di troncamento**.

I due database di prova e il loro utente sono stati rimossi; `itivao_atc` e `itivao_verifica` sono stati solo
**letti**. Il server locale è stato fermato: non era in esecuzione prima.

### La deviazione dalla carta, e perché il dato le ha dato ragione

Questa carta proponeva di **rigenerare `InitialCreate`** invece di aggiungere una migrazione, «perché nessuno
l'ha ancora eseguita». Ho scelto la migrazione in più, per il motivo scritto nel suo commento: la premessa
si può credere ma non verificare da qui.

**E infatti era falsa in casa nostra.** La MariaDB locale ha un `itivao_atc` la cui
`__EFMigrationsHistory` contiene già `20260805213003_InitialCreate` e `20260807125819_SpecialAreasHardening`
(è ferma al travaso di inizio agosto: le due dell'11 non le aveva). Rigenerando, quel database — e qualunque
altro che avesse visto quelle migrazioni, incluso un eventuale tentativo dalla loro parte — sarebbe diventato
non aggiornabile, con un errore che parla di migrazioni sconosciute invece che della causa. Con una
migrazione in più il percorso di aggiornamento **è stato provato e funziona**, e su un database vuoto
l'`ALTER TABLE` in più costa quanto non farlo.

Regola che ne esce, e vale oltre questo caso: **«nessuno l'ha ancora applicata» è un'affermazione sul mondo,
non sul repository.** Se non la si può interrogare, non la si assume.

### Blocco 2 — ✅ ESEGUITO il 14 agosto 2026 (tranne la voce 11, vedi sotto)

6. ✅ **C2 primo tempo.** `RunVipiStartupMaintenance` esegue le quattro passate non critiche ognuna isolata:
   un guasto viene registrato e l'avvio prosegue. `MigrateVipiDatabase` resta **fuori** e resta fatale.
   Il guasto non si ferma al log: passa da `IStartupMaintenanceReport` al report di consistenza, quindi si
   vede in `/vsop/admin/diagnostica` e manda `/vsop/health` in Degraded.
7. ✅ **C4.** `MySqlServerSettingsProbe` legge `@@sql_mode` e `@@max_allowed_packet` e li giudica in
   `ServerSettingsAnalyzer` (funzione pura). No-op fuori da MySQL, come la sonda di drift.
8. ✅ **C1.** `EnableRetryOnFailure` sul ramo MySQL + `MaximumPoolSize=20;ConnectionIdleTimeout=60;
   DefaultCommandTimeout=30` nella stringa di connessione di produzione.
9. ✅ **B1.** `DataProtection:KeyRingPath` = `/var/lib/vipi/keys` con `StateDirectory=vipi` in
   `vipi.service`: le chiavi escono dal database del committente.
10. ⚠️ **C3 — ridotto di proposito.** Vedi «C3: perché la transazione larga non s'è fatta».
11. ⛔ **C2 secondo tempo — non fatto, e propongo di non farlo.** Vedi «La voce 11».

**Suite: 2 495 test verdi** su entrambi i target, `Release --no-incremental` **0 warning**.

#### Le sonde nuove non hanno un posto nuovo

Né C2 né C4 hanno toccato `VipiHealthCheck`. Il progetto aveva già la forma giusta: sonde opzionali
iniettate in `ConsistencyReportService`, i cui esiti confluiscono nell'**unico** punto letto sia da
`/vsop/admin/diagnostica` sia da `/vsop/health`. Le due nuove si agganciano lì come già facevano il drift di
schema e la copertura admin — nessuna logica a valle da modificare, e la diagnostica le mostra da sé.

#### C3: perché la transazione larga non s'è fatta

La carta chiedeva di avvolgere l'orchestrazione dell'import in `ExecuteInTransactionAsync`. Guardando il
codice per scriverla, due fatti l'hanno smontata:

- **Ogni passo è già atomico.** `ImportAsync`, `ImportSubcentersAsync` e `SyncFromCatalogsAsync` fanno **un**
  `SaveChanges` ciascuno, quindi una transazione implicita a testa. Quel che resta scoperto è lo spazio
  *fra* i passi, e lì l'esito di un guasto non è corruzione ma uno stato **degradato che si sana da sé**: i
  cataloghi aggiornati e i settori non riproiettati, che il prossimo import — o il prossimo avvio, visto che
  `ProjectVipiSectors` gira lì — rimette a posto.
- **La transazione larga avrebbe contenuto le chiamate di rete a IVAO.** `RunAsync` alterna fetch e
  scritture, e la lista degli ACC da interrogare si legge dal database *dopo* il primo import. Avvolgere
  tutto significa tenere una transazione aperta per la durata di più HTTP da 10÷15 secondi — e con
  `EnableRetryOnFailure` appena acceso (voce 8), un retry rigiocherebbe anche i fetch. Si scambierebbe un
  rischio piccolo e auto-sanante con uno nuovo e reale.

**Fatto invece il pezzo stretto che serviva davvero:** `PruneSpecialAreasNotInAsync` aveva **due**
`SaveChanges` adiacenti, senza rete in mezzo — legami prima, aree orfane poi — e un guasto fra i due lasciava
aree che nessun ente elenca più e che nessuna passata sarebbe tornata a guardare. Ora è **un** `SaveChanges`
solo. Per riuscirci, le orfane si calcolano *prima* di cancellare: la vecchia versione chiedeva al database
«quali aree non hanno più legami», domanda che ha senso solo dopo aver scritto la cancellazione — ed era
esattamente il motivo per cui i salvataggi dovevano essere due.

#### La voce 11: perché propongo di lasciarla stare

«Promuovere le riconciliazioni storiche a migrazioni» non regge alla prova dei fatti, per tre ragioni:

1. **Non sono esprimibili come migrazioni.** Generano chiavi `custom:{guid8}`, leggono e riscrivono JSON:
   è logica C#, non DDL, e andrebbe scritta due volte in SQL portabile.
2. **Metà non sono storiche affatto.** `AddMissingCatalogSections` serve quando il catalogo *guadagna* una
   sezione fissa; `ProjectVipiSectors` è documentata come «serve a far entrare in vigore i cambi alla regola
   di derivazione»; `PruneVipiReleases` è retention corrente. Renderle one-shot sarebbe un difetto.
3. **L'idioma per il vero one-shot esiste già e nessuno l'ha ignorato.** `OptOutForeignAreasAsync` ha un
   marcatore in `ImportState` (`SpecialAreaForeignOptOut`), messo lì perché ripeterla era **dannoso** —
   rispegneva le aree di un ACC estero appena riabilitato dall'admin. Le altre quel marcatore non ce l'hanno
   perché ripeterle non fa niente: su un database già riconciliato sono **sole letture**, e a 266 sezioni
   costano un battito.

Aggiungere marcatori dove non servono introdurrebbe un modo nuovo di sbagliare: un marcatore scritto
significa che una futura correzione di quella riconciliazione non verrebbe mai applicata ai dati esistenti.

**Condizione di riapertura:** se una di quelle passate comincia a *scrivere* a ogni avvio su un database già
convergente — cioè se smette di essere una lettura — allora ha un difetto, e va marcata o corretta.

Ogni voce che tocca lo schema va emessa **due volte**, SQLite e MySQL. Ogni voce va provata guidando l'app,
non solo con la suite: l'audit dell'11 agosto ha trovato nove difetti su undici aprendo le pagine.

---

## Domande al committente — chiuse il 14 agosto 2026

- **D1 — chiusa.** Per `UnificationRule`, `TransferFlow`, `SharedBlock`, `DocumentProfile` il last-write-wins
  è **voluto**: token e colonne vanno rimossi (§A1). `Document` e `DocumentSection` non erano nella domanda e
  restano da correggere con la rotazione — se anche lì si volesse il last-write-wins, va detto prima del
  blocco 1.
- **D2 — ritirata.** Era mal posta: `MaximumPoolSize` lo detta il traffico atteso, non il tetto del server.
  Fissato a **20** (§C1). Nessuna risposta necessaria.
- **D3 — chiusa: cutover non ancora avvenuto.** È ciò che rende gratuiti A1-rimozione e D1, e li trasforma in
  una scadenza legata alla consegna del `.sql` (§D1, «Quando»).
- **D4 — APERTA, e resta il rischio più grave di questo documento.** Il committente **non sa chi faccia il
  backup** di `itivao_atc`. Non è una lacuna di documentazione: finché la risposta è «non lo so», l'unica
  affermazione difendibile è che **il backup non esiste**. Su un database che ospita l'intero stato
  dell'applicazione — immagini comprese, per scelta esplicita di ADR-0007 — significa che una perdita del
  volume è una perdita totale e definitiva del lavoro editoriale della divisione.

  **Non è un compito di codice, ma non va lasciato in questo stato.** Due mosse, entrambe nostre:

  1. **Domanda esplicita a Ivao.It**, da mandare insieme al `.sql` e non dopo: chi esegue il backup di
     `itivao_atc`, con quale frequenza, con quale ritenzione, e **quando è stato provato un ripristino**.
     Un backup mai ripristinato è un'ipotesi, non un backup. Va aggiunta all'elenco A9.
  2. **Il paracadute, così la risposta «nessuno» ha un rimedio pronto:** uno script `mariadb-dump` in
     `deploy/atc-ivao/` — le stesse opzioni già validate per la consegna (`--single-transaction`,
     `--hex-blob`, `--no-tablespaces`, senza `--databases`) — con rotazione a N giorni e una riga di `cron`,
     da consegnare a chi amministra la macchina. ⚠️ Va scritto in modo che il dump **non** finisca sullo
     stesso volume del datadir, o non protegge dallo scenario che conta.

  ⚠️ La ritenzione della pubblicazione (`ReleaseRetention`) **non** è un backup: pota di proposito le release
  superate. Non va confusa con una copia di sicurezza.
