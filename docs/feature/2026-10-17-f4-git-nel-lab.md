# F4 — Git nel Lab: rami, commit, pull request, il ramo dell'AIRAC (17 ottobre 2026)

> **Stato: 📝 PROPOSTA** — da approvare dal committente (§6). Nessun codice prima dell'approvazione.
> Quarta fase di Aurora Sector Lab ([carta madre](2026-09-18-aurora-sector-lab.md), §1 decisioni 4-7 e 12, §7). Usa
> l'app di F3 ([carta F3](2026-09-22-f3-l-app.md)) e il lotto «Subito»
> ([carta del lotto](2026-09-27-lotto-subito.md)); le voci «F4» dei file stanno nella
> [carta «file per file»](2026-09-24-file-per-file.md). Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md).
> 🔴 **Nessun dato di vIPI si tocca**, niente migrazioni, il sito non cambia. 🔴 Finché il committente non lo dice,
> il Lab lavora **solo sul fork di prova** (`SkyMistery/it-aurora-sector-test`) e su repo finti nei test: mai su
> `ivao-italy/it-aurora-sector`.

## §0 — La domanda

La carta madre (§7) chiede a F4: **git — rami, commit, PR, stato del ramo AIRAC, riporto da `main`**; finita quando
**un giro ramo → PR → fusione** si fa dall'app.

Oggi il Lab salva sul disco e basta: quello che succede dopo (commit, push, la pull request, il ciclo AIRAC) l'AOD lo
fa a mano, fuori dal Lab, oppure non lo fa. E una trentina di voci già decise aspettano proprio questo: si fanno «in
un ramo» (§2.3).

## §1 — Le misure (17 ottobre)

**Il repo vero** (`ivao-italy/it-aurora-sector`, pubblico; letto, mai scritto):

| cosa | misura |
|---|---|
| ramo predefinito | **`master`** (la carta madre dice `main`: vale il predefinito del repo, chiunque sia) |
| rami | `master`, `mappe_statiche` |
| pull request | **2 in tutta la storia**, nessuna fusa: oggi **si lavora direttamente su `master`** |
| commit | 1 278; nell'ultimo anno 6 autori (Matteo Marangon 139, altanico 130, Giulio 27, …), tutti su `master` |
| ultimo commit | `11b8569` di oggi, «revisione minime LIMJ»: gli aggiornamenti immediati vanno su `master` |
| ciclo in `changelog.md` | **A2610**, in vigore dal 01/10/2026; nessun ramo del ciclo dopo |
| permesso del committente | `WRITE` |

**Quanto costa prenderlo** (prova nello scratchpad, fuori repo): la cronologia intera pesa 412 MB (c'è
`Aurora.exe`, carta madre §2). Una **copia parziale** — `git clone --filter=blob:none --sparse`, poi
`git sparse-checkout add SectorFiles` — prende **3 secondi e 3,7 MB** di `.git`, 758 file, 14 MB di dati, con la
cronologia dei commit intera (i contenuti vecchi si scaricano solo se servono). È la strada per un AOD nuovo.

**Il fork di prova** (`SkyMistery/it-aurora-sector-test`): fork di GitHub del repo vero, ma con **4 commit soli**
(`8cf32c6`): non ha la storia di `master`, quindi un riporto o una PR verso il repo vero non si può provare lì così
com'è (§6, D3).

**Sul PC**: Git for Windows 2.54. I fine riga sono LF dentro git e CRLF sul disco (`.gitattributes` del repo,
carta madre §2): il Lab scrive già CRLF, e git li normalizza.

## §2 — Quel che è già deciso

### 2.1 — Dalla carta madre (17-18 settembre)

- **4** Git **locale** (con le credenziali di Windows), non l'API di GitHub: l'API fa un commit per file e non fonde.
- **5** Nessuna GitHub App; al più un'autorizzazione *device flow* per aprire le PR dall'app.
- **6** Rami: `master` = aggiornamenti immediati · `airac/AYYMM` = il ciclo dopo · `aod/<VID>/<argomento>` = lavoro.
- **7** Ruleset su `master` e `airac/*`: PR obbligatoria con 0 approvazioni (la PR è il secondo sguardo), niente
  *force push*, niente cancellazioni, nessun bypass. **È un'impostazione del repo vero: la fa il committente.**
- **12** `changelog.md` scritto dal Lab, rivisto a mano nella PR (`update.ini` tolto: C5).
- **Confini**: solo `SectorFiles/Include/IT/**`, `changelog.md`, `delete.upd`; mai `Aurora.exe`, DLL, suoni,
  binari, l'updater; mai *force push*; mai cancellare rami non nostri.

### 2.2 — Dalla carta «file per file» (§3, C1-C6)

- **C1** Righe di changelog **proposte** da ogni modifica (FIR dal file o dallo scalo, testo dal tipo di record), che
  l'AOD corregge, toglie, aggiunge. **C2** Ogni PR porta le sue righe nel `changelog.md` del ramo `airac/AAMM`; la
  prima riga di una sezione toglie `*NIL`, l'ultima tolta lo rimette. **C3** Fine ciclo: `changelog.md` archiviato in
  `CHANGELOG\AAMM.txt`, poi lo schema vuoto del ciclo nuovo (dal calendario AIRAC che vIPI calcola già,
  `Vipi.Domain.Services.AiracService`). **C4** `delete.upd` scritto dal Lab quando si toglie un file. **C6** Controlli:
  intestazione diversa dal ciclo del ramo, sezione mancante, riga accanto a `*NIL`.

### 2.3 — Il lavoro che aspetta un ramo

Tutto deciso, tutto «F4, in un ramo»:
- **correzioni dei dati** (R-10: una trentina, comprese le `//` dei simboli, la `]` di `default.atis`, i codici VFR);
- **pulizie** (`limw.pol`, `test.artcc` A9, `limc_star`/`lirf_star` K3, via `//Waypoint references` B10, SID ripetute
  P3b, M7, R3b);
- **adozioni** (famiglie di forme D6, blocchi E6, coordinate col punto G6);
- **rami di prova per Aurora** (A8, B9, F5, I7/K2, O4, R4, i `SYMBOLS_` dei profili): li fa il committente, il Lab li
  prepara.

## §3 — La proposta

**Come parla con git.** Il Lab chiama **`git.exe`** (Git for Windows) con argomenti esatti, mai una riga di shell:
niente librerie in più, le credenziali le tiene il Credential Manager di Windows come per chiunque usi git. Se git non
c'è, il Lab lo dice e spiega dove prenderlo. Ogni comando finisce nel Registro del Lab.

**Cosa l'AOD vede.** In alto, accanto al nome della cartella: **il ramo** e il suo stato («pulito», «3 file
salvati, non registrati», «2 commit da mandare», «master è avanti di 4: da riportare»). Un pannello **Ramo** con i
gesti qui sotto. I nomi di git (commit, push) si dicono in italiano: **Registra** (commit), **Manda** (push), **Prendi**
(fetch/pull).

**Le regole che il codice fa rispettare** (i confini di §2.1, più):
- il Lab **non scrive mai** su `master` né su `airac/*`: ci arriva solo con una PR;
- prima di cambiare ramo, niente in sospeso nel Lab (o si salva o si annulla) e niente di non registrato in git;
- un commit contiene solo file dentro i confini; un file fuori (un binario, `Aurora.exe`) ferma tutto col perché;
- mai `--force`, mai `reset --hard`, mai `stash`, mai cancellare un ramo che non sia un `aod/` dell'AOD stesso.

### Le slice

| # | Slice | Finita quando (misurato) |
|---|---|---|
| 0 | **Misure** (§1, fatte) e un repo finto per i test: un `git init --bare` nel temporaneo con la forma del sector, così la CI prova tutto senza rete | i test del giro passano su Ubuntu e Windows |
| 1 | **Il Lab sa dov'è**: la cartella aperta è un clone? ramo, stato (pulito, salvati non registrati, avanti/indietro rispetto al server), il remoto. Solo lettura | sul fork: ramo e stato giusti dopo un salvataggio, un commit a mano, un `fetch` |
| 2 | **Prendi il sector**: la copia parziale di §1 (solo `SectorFiles`) in una cartella scelta, dal repo indicato nelle Impostazioni | dal fork: 3-5 s, il Lab lo apre da solo |
| 3 | **Rami**: «Nuovo lavoro» crea `aod/<VID>/<argomento>` da `master` (immediato) o da `airac/AYYMM` (ciclo dopo); passa da un ramo all'altro; il ramo `airac/` del ciclo dopo si propone quando manca (calendario AIRAC) | sul fork: tre rami, il passaggio rifiutato con modifiche in sospeso |
| 4 | **Registra**: il commit dei file salvati, col messaggio proposto dalle modifiche (le stesse parole del changelog) e corretto dall'AOD; i confini controllati | un commit = i file del diff e nient'altro; un binario lo ferma |
| 5 | **Manda e apri la PR**: push del ramo `aod/`, poi la PR (§6, D2) verso il ramo da cui è nato, col titolo e il testo proposti | sul fork: PR aperta, fusa su GitHub dal committente |
| 6 | **Il changelog** (C1, C2, C6): le righe proposte dalle modifiche del ramo, nella sezione della FIR, `*NIL` tolto e rimesso; i controlli | sul fork: due PR nello stesso ciclo, le righe di tutte e due, nessun `*NIL` sbagliato |
| 7 | **Riporto e fine ciclo**: il riporto di `master` nel ramo `airac/` come PR (`aod/<VID>/riporto-AYYMM`), coi conflitti mostrati file per file e risolti a mano; C3 a fine ciclo; C4 `delete.upd` | sul fork: un riporto pulito e uno con un conflitto, mostrato e non risolto da solo |
| 8 | **Il primo giro vero**: le correzioni R-10 e le pulizie in un ramo, PR sul **fork**; poi, quando il committente lo dice, lo stesso sul repo vero | la PR sul fork fusa dal committente |

**Finita quando** (carta madre): un giro ramo → registra → manda → PR → fusione, dall'app, su un fork con la storia
del repo vero.

**Cosa NON è in F4**: Aurora a lotti (F5); import dai PDF (F6); risolvere da solo i conflitti di un riporto; la
GitHub App; le adozioni D6/E6/G6 (si fanno dopo, coi gesti di F4, una PR ciascuna).

## §4 — Cosa cambia per gli AOD (da dire prima)

Oggi tutti scrivono su `master`. Con la decisione 7 (ruleset) **nessuno potrà più farlo senza PR**, anche fuori dal
Lab. È una scelta di organizzazione, non del Lab: la decide il committente con gli altri AOD, e la mette lui nelle
impostazioni del repo vero quando è il momento. Il Lab funziona anche senza ruleset (la regola «mai su `master`»
è sua).

## §5 — Rischi

- **Le credenziali**: il primo push chiede l'accesso a GitHub nella finestra del Credential Manager. Il Lab non vede
  né tiene password o token (decisione 4). Se la finestra non si apre, il Lab lo dice e si ferma.
- **Un conflitto in un file del sector**: il Lab mostra le due versioni e non sceglie; l'AOD sceglie riga per riga
  (slice 7). Un riporto a metà si annulla con `git merge --abort`, mai con un reset.
- **La copia parziale**: un comando che chiede la storia di un file vecchio la scarica dal server. Il Lab non ne usa.

## §6 — Domande al committente

| # | Domanda | Proposta |
|---|---|---|
| D1 | Il **VID** nel nome del ramo (`aod/704798/…`): il Lab non ha un login IVAO. | Lo si scrive una volta nelle Impostazioni del Lab. |
| D2 | **Come si apre la PR**: (a) il Lab apre nel browser la pagina di GitHub «nuova PR» già compilata (titolo, testo, rami), l'AOD preme «Create»; (b) autorizzazione *device flow* e la PR la crea il Lab. | **(a)**: niente token nel Lab, niente app OAuth da registrare; un clic in più. |
| D3 | **Il fork di prova** ha 4 commit e non la storia del repo vero: per provare riporto e PR serve un fork con la storia. | Rifare il fork da `ivao-italy/it-aurora-sector` (lo fai tu su GitHub: è un'impostazione); fino ad allora slice 1-4 sul fork di oggi e sui repo finti. |
| D4 | **Ruleset e AOD** (§4): quando, e chi lo dice agli altri. | Dopo la slice 8 sul fork, prima del primo giro sul repo vero. |
| D5 | **Registrare**: un commit per ogni salvataggio, o quando l'AOD lo chiede? | Quando lo chiede (tasto **Registra**), col messaggio proposto: un salvataggio non è un pensiero finito. |

✅ Approvata da: — (in attesa)
