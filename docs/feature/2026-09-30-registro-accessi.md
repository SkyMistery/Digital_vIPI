# Registro degli accessi: chi è entrato nel sito — carta (30 settembre 2026)

> **Stato: ✅ ESEGUITA il 30 settembre 2026** sul ramo `fix/registro-accessi` (filone Sito, S68), sopra il login
> obbligatorio ([2026-09-30-login-obbligatorio.md](2026-09-30-login-obbligatorio.md)). **Migrazione additiva**
> `RegistroAccessi` (SQLite e MySQL; Postgres la allinea `PostgresSchemaReconciler`). Nessun `deploy/`.
> Richiesta del committente: «registrare il nome di chi fa almeno un accesso e mostrarlo nella pagina delle statistiche».
>
> Dove sta: `AccessoAlSito` (Domain: la riga e la regola dei giorni) · `RegistroAccessi` (Application: servizio,
> deposito, cancello admin, potatura) · `EfRegistroAccessiStore` · `StaffLoginTrackingMiddleware` (chi scrive) ·
> `StatsAccessiPage` (`/services/stats/logins`) · `CookiePage` (l'informativa).

## 1. Com'era

Al login si salvava il nome **solo dello staff IT**: `StaffRosterService` scrive nel roster chi ha una posizione `IT-…`
e scarta tutti gli altri. Di un pilota o di un controllore non staff non restava niente.

## 2. Le decisioni del committente (30 settembre 2026)

1. **Lo vedono solo gli amministratori.** Sono dati personali di chiunque entri, di qualunque divisione.
2. **Per ciascuno:** VID e nome, divisione e ACC, primo e ultimo accesso, in quanti giorni diversi è entrato.
3. **Dodici mesi dall'ultimo accesso**, poi la riga si cancella (come l'archivio ATC).
4. **Una pagina nuova sotto le statistiche**, «Accessi al sito», con la ricerca per nome o VID.

## 3. Pre-flight

**1. Modello.** Concetto nuovo: una tabella `AccessiAlSito`, **una riga per VID** (la chiave è il VID) — non una riga
per accesso: non è un registro di chi ha letto che cosa. Il roster staff resta com'è: serve ai permessi, questo
alla statistica, e unirli metterebbe i lettori nell'elenco da cui si scelgono gli editor.

**2. Dispatch.** Nessuno. Scrive lo stesso middleware che aggiorna il roster, con la stessa cadenza (al più una
volta ogni cinque minuti per VID, `StaffLoginThrottle`) e la stessa regola (se fallisce, la richiesta prosegue).

**3. Ingressi e verifica.** Dalla pagina Statistiche di divisione, voce visibile ai soli admin. Il servizio rifiuta
chi non è admin (`EnsureAdmin`), la pagina lo nasconde. Verifica: regola dei giorni (Domain), cancello e potatura
(Application), deposito su SQLite (Infrastructure), pagina (bUnit), prova a schermo.

**4. Propagazione.** La divisione si legge ora dal profilo IVAO (claim `divisionId`, `CurrentUser.Division`):
i cookie emessi prima non la portano, e la riga la prende al primo login dopo il rilascio (una divisione che manca
non cancella quella nota). L'informativa `/services/cookies` dice che cosa si registra.

## 4. Le regole

- Il giorno si conta **una volta** (UTC): dieci accessi oggi sono un giorno.
- Nome, divisione e ACC si aggiornano all'ultimo accesso; un valore che manca non cancella quello noto.
- Potatura: al più una volta al giorno per processo, dentro una registrazione (niente servizio in background per
  una `DELETE` al giorno). Cancella chi ha l'ultimo accesso più vecchio di 365 giorni.
- Elenco: i più recenti per primi, al massimo 500 righe; un numero cerca l'inizio del VID, un testo il nome senza
  badare alle maiuscole. La pagina è SSR statica e la ricerca un form in GET (`?q=`).

## 5. L'informativa (`/services/cookies`)

Il committente ha chiesto di verificare i cookie dopo il login obbligatorio. Tre modifiche:

1. **La pagina si legge senza login** (aggiunta ai liberi di `CancelloDelLogin`): l'informativa va letta prima di
   decidere se entrare.
2. Il cookie `vipi.auth` non è più «solo se accedi»: serve per leggere il sito.
3. Sezione nuova **«Che cosa registriamo quando entri»**: che cosa, perché, chi lo vede, per quanto.

Nessun cookie nuovo: il registro sta sul server. Il banner di consenso resta non necessario (cookie tecnici).

## 6. Verifica

Test: Domain 156 → 160, Application 3155 → 3158, Infrastructure 2052 → 2055, Ui 1908 → 1915, E2E 497 → 498. A
schermo su DB vuoto con l'utente di sviluppo: la pagina elenca l'accesso (VID, ACC, date, un giorno), la ricerca per
VID e per testo risponde, l'informativa ha la sezione nuova e la descrizione aggiornata del cookie.
