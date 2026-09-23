# §A117 — La vista `v_share_atc_sessions` per l'IVAO Division Hub (23 settembre 2026)

> Stato: 🟡 **in PR, non in `main`, non in pacchetto.** Una migrazione MySQL di sola vista
> (`20260923100945_VistaCondivisaSessioniAtc`) e la copia del database che impara a portarla.
> Il contratto l'ha deciso il committente il 14 settembre 2026, nella nota dell'hub
> `docs/internal/decisions/2026-09-14-dati-condivisi-con-vipi.md` (repository dell'hub, non questo).

## 0. Che cosa serve

L'**IVAO Division Hub** è un sito separato, con un database suo sullo **stesso server MariaDB** di `itivao_atc`. Il suo
modulo dei tour (T12, «gli ATC contattati») chiede «quali posizioni erano online in questo intervallo»: la risposta
è il nostro archivio delle sessioni ATC (`AtcSessions`, anche fuori divisione dal 28 agosto 2026). La regola dei due
siti: **ogni dato ha un padrone, che è l'unico a scriverlo**; l'altro legge **solo da viste `v_share_`** create dalle
migrazioni del padrone, con un **utente MariaDB suo** che ha solo `SELECT` su quelle viste. Niente copie, niente API.

## 1. Pre-flight

1. **Modello.** Nessuna entità: la vista non è nel modello EF, legge una tabella che c'è già.
2. **Dispatch.** Nessuno. La vista esiste **solo su MySQL/MariaDB** (vedi §3).
3. **Ingressi + verifica.** Nessuna pagina: chi la usa è l'hub. Verificata su MariaDB 11.4.10 vero (§5).
4. **Propagazione.** Additiva. ⚠️ Ma toccava la **copia del database** (§A47): la copia si rifiutava davanti a
   qualunque vista — «vista v_share_atc_sessions» — e l'Admin non avrebbe più potuto scaricare la copia di sicurezza
   prima del carico successivo. Ora la porta (§4).

## 2. La vista — è il contratto

```sql
CREATE OR REPLACE SQL SECURITY DEFINER VIEW v_share_atc_sessions AS
SELECT SessionId         AS session_id,
       UserId            AS vid,
       Callsign          AS callsign,
       Position          AS position,
       Frequency         AS frequency,
       StartUtc          AS start_utc,
       EndUtc            AS end_utc,
       DurationSeconds   AS duration_seconds,
       Rating            AS rating,
       IsOutsideDivision AS is_outside_division
FROM AtcSessions;
```

- 🔴 **Cambia solo aggiungendo colonne.** I dieci nomi a destra di `AS` sono quelli che l'hub legge: una colonna nuova
  si aggiunge con **un'altra migrazione** e un altro `CREATE OR REPLACE`; un alias non si rinomina e non si toglie mai.
  Se cambia una colonna di `AtcSessions`, si riscrive il lato **sinistro**. Lo presidia
  `MySqlMigrationsTests.La_vista_per_l_hub_porta_i_nomi_del_contratto_e_legge_colonne_che_esistono`.
- `end_utc` nullo = sessione ancora in corso. Fuori di proposito: traffico, piste, `ShiftKey`, `Source`, le colonne
  di servizio.
- Verificato il 23 settembre: le dieci colonne della nota dell'hub coincidono con `AtcSession`
  (`src/Vipi.Domain/Entities/StatisticheAtc.cs`) e con la tabella come la creano le migrazioni MySQL. Nessun
  adattamento.
- `SQL SECURITY DEFINER`: si legge coi permessi di chi l'ha creata (l'utente della vIPI), quindi all'utente dell'hub
  basta il `SELECT` sulla vista e **non vede `AtcSessions`** (provato: `SELECT command denied … AtcSessions`).

## 3. Perché solo MySQL, senza gemella SQLite

«Ogni cambio di schema va emesso due volte» vale per il **modello**: le due guardie di allineamento confrontano
modello e snapshot, e una vista non è nel modello. La vista serve solo in produzione, dove l'hub la legge; in locale
(SQLite) e su Postgres (`EnsureCreated` + `PostgresSchemaReconciler`) non c'è nessuno che la legga. Precedenti di
migrazioni solo MySQL: `SpecialAreasHardening`, `EnumLengthsAndDropUnusedTokens`. Il `Designer` è quello che scaffolda
EF (snapshot invariato).

## 4. La copia del database porta le viste condivise

- `MySqlDumpSource` accetta le viste col prefisso **`v_share_`** (`MySqlSchema.SharedViewPrefix`); ogni altra vista, i
  trigger e le procedure continuano a **fermare** la copia.
- La `CREATE` viene da `SHOW CREATE VIEW` **senza la clausola `DEFINER`**. Non è pulizia: ripristinata da un utente che
  non è quel definer, la `CREATE` chiede il privilegio `SET USER` — provato con un utente che ha tutto sul suo database,
  «Access denied; you need … SET USER». Senza, il definer è chi ripristina.
- Esce **dopo le tabelle** (`DROP VIEW IF EXISTS` + `CREATE`), non conta come tabella nella riga di chiusura, e il
  formato resta **1**: il verificatore la attraversa come ogni istruzione che non è un `INSERT`, e l'impronta la copre.
- `SHOW CREATE VIEW` non qualifica le tabelle col nome del database: ripristinata in un altro database, la vista legge
  **quello** (lo riprova l'andata e ritorno della CI, passo 5b-bis).

## 5. Verifica

- Test: `MySqlMigrationsTests` (+1: contratto e colonne), `CopiaDelDatabaseTests` (+7: `DEFINER` tolto, solo le
  `v_share_`, vista dopo le tabelle e copia INTERA). **Controprova**: rinominato `UserId` in `UserIdd` nella migrazione,
  il test cade con «la vista legge colonne che AtcSessions non ha più: UserIdd».
- MariaDB 11.4.10 in Docker, stesse condizioni della CI: migrazioni applicate fino a `VistaCondivisaSessioniAtc`; la
  domanda dell'hub (`start_utc <= @to AND start_utc >= @from - 2 giorni AND (end_utc IS NULL OR end_utc >= @from)`, e
  `MIN(start_utc) GROUP BY is_outside_division`) risponde; un utente con il solo `GRANT SELECT` sulla vista la legge e
  non legge `AtcSessions`; `andata-e-ritorno.sh` verde con la vista (66 tabelle, 1 vista tornata).
- CI `mariadb-schema`: verifica **4** (le dieci colonne nell'ordine, `GRANT` a un utente di sola lettura, la domanda
  dell'hub, `AtcSessions` negata) e passo **5b-bis** dell'andata e ritorno (la vista torna, legge il database
  ripristinato, stesse righe).

## 6. In produzione — tre cose, e nessuna la fa il sito

1. 🔴 **L'utente della vIPI deve poter creare viste** (`CREATE VIEW` su `itivao_atc`). Sul pannello Plesk **non è
   ancora verificato** (lo sta controllando il committente). Senza, la migrazione fallisce **all'avvio** e il sito non
   parte: prima del carico del pacchetto che la porta, va confermato — o la migrazione resta fuori dal pacchetto.
2. **Il `GRANT` all'utente dell'hub lo fa chi amministra il server**, a mano, dopo che la migrazione è passata. Non
   sta nella migrazione di proposito (l'utente della vIPI non deve poter dare permessi):
   ```sql
   GRANT SELECT ON itivao_atc.v_share_atc_sessions TO '<utente dell'hub>'@'<host>';
   ```
   L'utente dell'hub è suo e dedicato, con un tetto di connessioni proprio (il pool del server è condiviso).
3. Come ogni migrazione: **copia di sicurezza del database prima del carico**, e `Vipi.Infrastructure.MySqlMigrations.dll`
   nel pacchetto.
