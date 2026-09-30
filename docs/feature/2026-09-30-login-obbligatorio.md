# Il sito si legge solo dopo il login IVAO — carta (30 settembre 2026)

> **Stato: ✅ ESEGUITA il 30 settembre 2026** sul ramo `fix/login-obbligatorio` (filone Sito, S67), provata col login
> IVAO vero dal committente (§6). Nessuna migrazione, nessun `deploy/`. Da fondere. Nasce da una segnalazione delle Public Relations girata dal committente: «il sito aperto a tutti senza
> login si espone a furto dati da parte di bot; renderlo accessibile solo previo login e con un account IVAO attivo».
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md). Guida del login: [standalone-auth-ivao.md](../guide/standalone-auth-ivao.md).
>
> Dove sta: `CancelloDelLogin` (Host/Auth: la regola e il middleware) · `VipiAuthOptions.LoginObbligatorio` ·
> `AccessoConLogin` (Ui: che cosa disegna la porta) · `ServicesHome` · `DiagnosticaErrori.RegistraCampiDelProfilo`.

## 1. Le decisioni del committente (30 settembre 2026)

1. **Senza login si vede solo la porta d'ingresso** (`/services`) con «Entra con IVAO». Tutto il resto — vIPI, vSOP,
   vLOA, vista live, ricerca, vAWOS, statistiche, strumenti, immagini dei documenti — chiede il login.
2. **Entra qualunque account IVAO**, di qualunque divisione: le vLOA le leggono anche le divisioni confinanti.
3. **«Account attivo»: prima si misura.** Dal profilo IVAO leggiamo VID, ACC, divisione, nome e incarichi staff; un
   campo «sospeso» non l'abbiamo mai visto. Al primo login dopo ogni avvio si scrivono i soli **nomi** dei campi in
   `diagnostica/errori-richieste.txt`; il controllo si aggiunge quando si sa quale campo guardare.

## 2. Pre-flight

**1. Modello.** Niente di nuovo da salvare: il login IVAO c'è già (OIDC, cookie di 7 giorni scorrevoli). Si aggiunge
un interruttore (`VipiAuth:LoginObbligatorio`, default acceso) e un segnale per la UI (`AccessoConLogin`), che il
modulo non può dedurre da sé — non sa se il login è suo, dell'host che lo monta o di nessuno.

**2. Dispatch.** Un cancello solo, davanti a tutto, e **chiuso per default**: passa solo quello che è scritto in
`CancelloDelLogin.Liberi`. Un `[Authorize]` pagina per pagina lascerebbe aperta in silenzio la pagina dimenticata;
così una rotta nuova nasce chiusa.

**3. Ingressi e verifica.** Chi non è entrato arriva sulla porta o su un link a un documento: nel secondo caso va al
login e ne torna **sulla pagina che voleva** (`returnUrl` con la sua query; `SafeReturn` al ritorno accetta solo
indirizzi nostri). Verifica: regola pura provata caso per caso, cancello montato su un host col login acceso,
porta in bUnit, prova a schermo.

**4. Propagazione.** Additiva. Cambia che cosa vede un anonimo: la cache delle letture anonime
(`CacheDelleLettureAnonime`) resta, ma ormai tiene solo la porta — le pagine a un anonimo rispondono 302, che non si
tiene.

## 3. Che cosa resta aperto, e perché

| Indirizzo | Perché |
|---|---|
| `/` e `/services` | la porta d'ingresso («/» rimanda lì) |
| `/services/vsop/auth/…`, `/signin-oidc`, `/signout-callback-oidc` | il giro del login stesso |
| `/vsop/health`, `/vsop/health/ready`, `/vsop/ping` | sonde di monitoraggio |
| `/vsop/api/…` | API per altri programmi: hanno la loro chiave |
| `/api/rfo/…` | ponte RFO: ha le sue chiavi |
| `/Error` | la pagina d'errore deve reggere sempre |
| file statici | li serve `UseStaticFiles`, prima del cancello: sono asset, non dati |

Il resto, a chi non è entrato: **302 al login** se è una pagina chiesta da un browser (`GET`/`HEAD` che accetta HTML),
**401** in tutti gli altri casi — il circuito Blazor, le fetch (per esempio l'endpoint del vAWOS), i `POST`.

⚠️ **Il circuito (`/_blazor`) è chiuso anche lui.** Dentro un circuito si naviga fra le pagine senza nuove richieste
HTTP: chiudere le pagine e lasciarlo aperto sarebbe chiudere la porta e lasciare la finestra. Chi non è entrato non ne
apre comunque: dal 12 settembre il layout gli dà zero isole interattive, e la porta è SSR statica.

## 4. Che cosa cambia per chi usa il sito

- I link ai documenti condivisi fuori (Discord, forum) chiedono il login IVAO e poi aprono il documento.
- Le pagine spariscono dai motori di ricerca.
- Chi è già entrato non vede differenze: il cookie dura 7 giorni e si rinnova usandolo.
- Il vAWOS aperto su un secondo monitor resta aperto finché il cookie vale.
- In sviluppo con `VipiAuth:Enabled=false` (utente finto) e nel montaggio embedded il cancello non c'è.

Per riaprire il sito senza ricompilare: `VipiAuth__LoginObbligatorio=false`.

## 5. La misura del profilo (30 settembre 2026)

Al login vero del committente, `/v2/users/me` porta questi campi (solo i nomi):

```
id, firstName, lastName, centerId, countryId, createdAt, divisionId, isStaff, isSupervisor, languageId, email,
rating{isPilot,isAtc,pilotRating,atcRating,networkRating}, gcas[], hours[]{type,hours},
userStaffPositions[]{id,staffPositionId,divisionId,centerId,connectAs,onTrial,description,staffPosition},
userStaffDetails{email,note,description,remark}, prCreator, ownedVirtualAirlines[], groups[],
sub, given_name, family_name, nickname, profile, publicNickname
```

**Nessun campo dice «attivo» o «sospeso».** Resta quindi valido il login riuscito: è IVAO a decidere chi può
entrare nel suo SSO. Se un giorno servisse un criterio in più, i candidati sono in `rating` (per esempio
`isPilot`/`isAtc`), ma che cosa valgano per un account sospeso non lo sappiamo: si deciderebbe con IVAO, non
indovinando. La nota nel registro resta (una per avvio, solo nomi): dice subito se IVAO cambia il profilo.

## 6. Verifica

- Test: regola pura caso per caso e cancello montato su un host col login acceso (`CancelloDelLoginTests`), porta in
  bUnit (`ServicesHomeTests`). E2E 465 → 497, Ui 1905 → 1908.
- A schermo con un'autorità finta: porta col solo accesso (IT, EN, 375 px), documenti/ricerca/vAWOS → 302 al login
  col ritorno giusto, sonde aperte, API e ponte RFO dalla loro porta, `/_blazor` 401.
- **Col login IVAO vero** (committente, http://localhost:5034, copia del DB poi cancellata): da un link a LIRF si va
  a IVAO, si entra e si torna su LIRF; navigazione, ricerca e vista live normali; dopo il logout di nuovo al login.

## 7. Fuori da questo giro

- **Un controllo «account attivo» nostro**: IVAO non manda un campo che lo dica (§5).
- **Limitare chi è entrato** (un account IVAO che scarica tutto): è un tetto di richieste per VID, altra cosa.
