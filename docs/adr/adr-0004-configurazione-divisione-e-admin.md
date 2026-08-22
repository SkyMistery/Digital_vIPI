# ADR-0004 — Configurazione divisione e codici admin

**Data:** 2026-06-23
**Stato:** Accettata
**Contesto:** estende ADR-0002 (identità portabile). Riferimento operativo: `../guide/config.md`.

## Contesto
L'identità della divisione era cablata nel codice in più punti: il prefisso degli staff code admin
(`IT-...` in una regex), il prefisso ICAO per filtrare gli ATC online (`LI`) e il path dell'API membri
divisione (`/v2/divisions/IT/members`). Riusare il tool per un'altra divisione (es. Germania) richiedeva
modifiche sparse e una ricompilazione. Serve poter cambiare divisione con il minimo intervento, e poter
aggiungere codici admin senza toccare il codice.

## Decisione
1. **`DivisionOptions` (sezione `Division`)** centralizza l'identità divisione:
   - `Code` — prefisso degli staff code admin **e** id nell'API membri divisione.
   - `IcaoPrefixes` — prefissi ICAO dei callsign ATC (filtro polling online).
   - `AdminRolePatterns` — suffissi ruolo (regex) di divisione che valgono come admin. ⚠️ **Dal 22 agosto
     2026 il default è il jolly `[A-Z0-9]+`**: lo staff di divisione è admin, tutto. L'elenco puntuale
     precedente (`DIR`, `ADIR`, `WM`, `AWM`, `AOC`, `AOAC`, `AOA\d+`) lasciava fuori quattro staffisti veri
     visti ai login (`IT-SOC`, `IT-T01`, `IT-FOC`, `IT-FOAC`), e ogni ruolo nuovo sarebbe nato escluso.
     Un codice `{Code}-{ruolo}` lo assegna il portale IVAO **solo** allo staff di divisione: il jolly non
     allarga oltre quell'insieme.
   - `AdminAccRolePatterns` — suffissi ruolo (regex) **ACC-scoped** (chief): il codice ha il prefisso ICAO
     dell'ACC, non della divisione (es. `LIRR-CH`, `LIMM-ACH`).
2. **Codici admin derivati**: `EditAuthorizationService` costruisce i pattern admin da entrambi i set:
   `^{Code}-{ruolo}$` per i ruoli di divisione (es. `IT-DIR`) **e** `^{prefissoIcao}[A-Z0-9]+-{ruolo}$` per i
   ruoli ACC-scoped (es. `LIRR-CH`), uno per ogni `IcaoPrefixes`. Cambiare `Division:Code`/`IcaoPrefixes` li sposta tutti.
3. **Override esplicito** opzionale via `AuthOptions` (`Auth:AdminStaffCodes`): pattern regex completi
   che sostituiscono i derivati, per codici non riconducibili allo schema `{Code}-{ruolo}`.
4. **`IvaoOptions` ripulito**: rimossi `Prefix` e `DivisionMembersPath` (ora derivati da `Division`);
   il path membri diventa un template `DivisionMembersPathFormat` con `{0}` = `Code`.

Tutte le opzioni sono `IOptions<T>`, sovrascrivibili da appsettings / env var / user-secrets.

## Conseguenze
- **Pro:** passare divisione = editare la sola sezione `Division` (`Code` + `IcaoPrefixes`); aggiungere
  un codice admin = una riga in config, **senza ricompilare né ridistribuire**. Fallback ai default se la
  config è assente: non si resta mai senza admin.
- **Limiti dichiarati:**
  - **Due campi, non uno**: `Code` ("IT") e `IcaoPrefixes` ("LI") sono informazioni indipendenti
    (DE → "ED"/"ET"), non derivabili l'una dall'altra. Restano nella stessa sezione.
  - Lo switch copre la **logica** (autorizzazione, API, filtro online), **non** il **contenuto**: il seed
    documentale (Roma/LIRR) è dato e va riseedato per una nuova divisione.
- **Test:** `AdminCodeTests` copre derivazione dai default, override esplicito e cambio `Division:Code`
  (IT→DE). `IvaoPollingTests` copre il filtro per prefissi ICAO della divisione.
