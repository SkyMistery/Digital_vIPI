# ADR-0002 — Integrazione nel sito e autenticazione portabile

**Stato:** Accettato
**Data:** 16 giugno 2026
**Decisori:** Carmine + assistente
**Riferimenti:** `ADR-0001` (D1, D7, D9), `PIANO_vIPI_Tool.md` (§8, §23), `REVIEW_Flusso_e_Gap.md`
**Sostituisce/raffina:** ADR-0001 D1 (tecnologia UI) e D9 (modello di deploy); chiarisce D7 (autenticazione).

---

## Contesto

In fase di pianificazione si era ipotizzato che il sito ospitante avesse un **backend ignoto** (RF-9) e che la vIPI fosse un'**app .NET separata dietro reverse proxy** sotto `https://it.ivao.aero/sop` (ADR-0001 D9), con autenticazione propria via IVAO OIDC.

L'analisi del codice del sito esistente (`Ivao.It`) ha smentito l'assunzione di fondo:

- Il sito è **ASP.NET Core + Blazor Server + ASP.NET Core Identity**, con **IVAO OIDC come external login** (progetto `Ivao.OpenIdConnect`, `AddIvaoOidcAuth`).
- Dopo il login, l'intero profilo IVAO è nei **claim** del `ClaimsPrincipal`: `id` (vid), `centerId` (FIR), `divisionId`, `isStaff`, `userStaffPositions` (es. `IT-DIR`, `IT-WM`), ecc. Esistono già `IvaoUser`, `ClaimsPrincipalIvaoExtensions`, le `Policies` (`IsStaff`) e il merge delle staff position in **ruoli Identity** (`IvaoRolesHandler`).
- Il cookie usa un **ticket store in-memory** (`MemoryCacheTicketStore`): il cookie contiene solo un ID di sessione, i claim vivono nella **memoria del processo del sito**. Di conseguenza un'app separata, anche con le stesse chiavi di Data Protection, **non può leggere il ticket** senza uno store distribuito condiviso.
- È inoltre noto che un **secondo sito** della divisione, **stesso stack tecnologico** ma struttura diversa, è in sviluppo: la vIPI dovrà poter essere spostata lì.

Vincolo guida emerso: **portabilità** della vIPI tra host dello stesso stack, senza riscritture e senza esporre nuova superficie API sul sito.

---

## Decisione

### D1 — La vIPI è una **Razor Class Library (RCL) Blazor** integrabile, non un'app host a sé

Il layer di presentazione della vIPI è impacchettato come **RCL**: un insieme di pagine/componenti Blazor che un sito host .NET referenzia e monta su una rotta (es. `/sop`). La logica resta nei progetti **Clean Architecture** separati (`Domain`, `Application`, `Infrastructure`), referenziati dalla RCL e indipendenti dall'host.

Questo **raffina ADR-0001 D1**: l'UI passa da Razor Pages a **componenti Blazor**, per essere integrabile in un host Blazor Server. La scelta è coerente con lo stack del sito.

### D2 — L'identità è acquisita tramite l'astrazione `ICurrentUserProvider`

La RCL e la logica **non leggono mai** direttamente il cookie, l'OIDC o l'Identity dell'host. Chiedono sempre "chi è l'utente?" a un'interfaccia neutra:

```csharp
public interface ICurrentUserProvider
{
    CurrentUser? Get();   // modello utente neutro: UserId, Name, Fir, StaffPositions[], CanEdit
}
```

Le implementazioni (adapter) sono fornite dall'host in fase di wiring. La logica di autorizzazione (`CanEdit` ⇒ CH/AOD) lavora solo sul modello neutro.

### D3 — Tre scenari di deploy, stessa codebase, adapter intercambiabili

| Scenario | Host | Adapter `ICurrentUserProvider` | Login |
|---|---|---|---|
| **A — embedded sito attuale** | `Ivao.It` | legge il `ClaimsPrincipal` già presente (sessione del sito) | nessun login aggiuntivo |
| **B — embedded sito nuovo** | nuovo sito (stesso stack) | come A, adattato alla sua struttura via config | nessun login aggiuntivo |
| **C — app autonoma (futura)** | host .NET minimo dedicato | adapter **IVAO OIDC proprio** (con redirect URL) | login IVAO della vIPI |

Spostare la vIPI = referenziare RCL + progetti logici nel nuovo host e fornire l'adapter adatto. **Nessuna riscrittura** di UI/logica/DB.

### D4 — Nessuna API di configurazione né endpoint `/me` come dipendenza

Poiché negli scenari A/B la RCL gira **in-process** nell'host, legge l'identità dal contesto già autenticato: **non si espone alcun nuovo endpoint** sul sito (né di configurazione né di identità). L'opzione `/api/me` o ticket store distribuito resta documentata solo come ripiego per un'eventuale vIPI **out-of-process** sotto un host che non possa referenziare la RCL — caso non previsto oggi.

### D5 — Confine di dipendenza invariante (regola di portabilità)

RCL e progetti logici **non devono dipendere da tipi specifici dell'host** (es. `ApplicationUser`, le sue `Policies`, il suo `DbContext`). Dipendono solo da `ICurrentUserProvider` e dal modello utente neutro. È questo confine a rendere A → B → C un'aggiunta di host, non una riscrittura.

---

## Conseguenze

**Positive**
- Integrazione a costo quasi nullo sul sito attuale: l'auth è **ereditata**, niente doppio login, niente glue code.
- Portabilità reale verso il sito nuovo (stesso stack): basta referenziare e configurare.
- Nessuna nuova superficie d'attacco sul sito (nessun endpoint esposto).
- Identità CH/AOD dai **claim già presenti** ⇒ **non serve** la chiamata API `GET /v2/users/{vid}/userStaffPositions` prevista in PIANO §14.4 per lo scenario embedded (resta utile solo per lo scenario C o come fallback).
- Clean Architecture preservata: solo il guscio di presentazione cambia forma.

**Costi / impegni**
- Negli scenari A/B la vIPI **non è deployabile da sola**: richiede un host .NET. Accettabile, perché lo scenario reale è "embedded in host dello stesso stack". Lo scenario C (host minimo dedicato) resta possibile in futuro senza riscrittura (D3).
- L'UI è vincolata a **Blazor** (non più Razor Pages): coerente con l'host, ma rivede ADR-0001 D1.
- Disciplina necessaria sul confine di dipendenza (D5): da verificare in code review.
- L'integrazione **live/SSE** (ADR-0001 D6) va realizzata in modo compatibile con il modello di rendering Blazor dell'host (Blazor Server gestisce nativamente gli aggiornamenti push via circuito; valutare se serve ancora SSE o se basta il circuito Blazor — da decidere in un ADR successivo).

**Dipendenze esterne**
- Redirect URL e client OIDC propri servono **solo** per lo scenario C (app autonoma). Per A/B non servono.

---

## Alternative scartate

- **Modulo copiato dentro la solution del sito** — massimo accoppiamento, zero portabilità: scartata appena emersa la priorità di spostare la vIPI altrove.
- **App separata + cookie condiviso via ticket store distribuito (Redis/SQL)** — richiede modifiche infrastrutturali all'host (sostituire `MemoryCacheTicketStore`) e condivisione delle chiavi Data Protection: più parti mobili, nessun vantaggio rispetto alla RCL su host dello stesso stack.
- **App separata + endpoint `/api/me`** — introduce superficie API e una chiamata HTTP per sessione; tenuta solo come ripiego per host non-.NET (non previsto).
- **iframe** — problemi di cookie cross-origin/terze parti e di autenticazione: scartata.

---

## Note di implementazione

- Struttura prevista: `Vipi.Ui` (RCL Blazor) → referenzia `Vipi.Application`/`Vipi.Domain`; `Vipi.Infrastructure` (EF Core/SQLite, client IVAO) iniettata dall'host.
- Adapter forniti: `HostIdentityCurrentUserProvider` (scenari A/B, legge `ClaimsPrincipal`) e `OidcCurrentUserProvider` (scenario C).
- Mapping staff position → `CanEdit`: riusa la logica già presente nel sito (`userStaffPositions` filtrate per `divisionId == "IT"` e ruolo CH/AOD), ma applicata sul modello neutro.
- Il `DbContext` SQLite della vIPI è **separato** da quello del sito; convivono nello stesso processo senza condividere schema.

---

## Aggiornamento (27 giugno 2026)

- **`Vid` → `UserId`** nel modello neutro: `CurrentUser.UserId`, `HostIdentityOptions.UserIdClaim` (valore default `"id"`), e tutte le colonne DB correlate (migrazione `Rename_Vid_To_UserId`). Coerente con D5 (il modello utente non porta più un nome legato a una rete specifica). Le **label a video** restano "VID".
- Il singolo utente esterno si legge ora via la porta neutra **`IUserDirectory.GetUserAsync`** (non più un nome IVAO-specifico). Il decoupling completo dalla sorgente dati è in **`ADR-0006`**.
