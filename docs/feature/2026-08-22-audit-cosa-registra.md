# Audit — cosa il registro registra davvero (22 agosto 2026)

> Decima pagina del ramo `ui-trasferimenti-densita`, e come per Versioni **la prima carta non è di densità**:
> aprendo `/services/vsop/admin/audit` per il `thead` appiccicato è venuto fuori che il registro **non registra
> l'atto più distruttivo dell'app** (l'eliminazione di un documento), che sulla **revoca di un permesso
> attribuisce l'atto alla persona sbagliata**, e che il suo sottotitolo promette una categoria di eventi
> — la struttura — che **nessuno ha mai scritto**. La densità è la parte B, dopo.

## Il fatto di partenza, misurato sul sorgente e sul DB

In tutto il codice l'audit si scrive in **quattro punti**:

| Dove | Azione | Entità |
|---|---|---|
| `EfEditingRepository.cs:858` | `Publish` | `DocumentVersion` |
| `EfReleaseRepository.cs:101` | `Publish` | `DocumentVersion` |
| `EfEditingRepository.cs:936` | `Discard` | `DocumentVersion` |
| `EfEditGrantRepository.cs:65` | `Create` / `Update` / `Archive` | `EditGrant` |

Il DB di sviluppo lo conferma: **28 righe, tre combinazioni sole** (`Publish`×20, `Create`/`Archive`
`EditGrant`×8). Un registro che ha visto una manciata di tipi di evento non è un registro: è un log delle
pubblicazioni con due righe di permessi in mezzo.

## Pre-flight (FEATURE-PROCESS)

1. **Modello** — nessun concetto nuovo: `AuditLog` esiste (`Support.cs:71`), l'enum `AuditAction` esiste, il
   lettore esiste. Si aggiungono **due valori all'enum** (`Delete`, `ForceUnlock`) e si **usa** il valore
   morto `HierarchyChange`. Nessuna tabella gemella, nessun secondo posto dove «si salva chi ha fatto cosa».
   ⚠️ Gli enum vanno **a stringa** (`VipiDbContext:114`, `varchar(32)` su MySQL): aggiungere un valore non è
   una migrazione, ma un valore più lungo di 32 caratteri sì. `ForceUnlock` = 11, `HierarchyChange` = 15.
2. **Dispatch** — nessuno `switch(tipo)` nuovo. La scrittura è la stessa riga in sei-sette punti, quindi
   diventa **un** helper (`AuditScribe`), non un `switch`: il registry qui non serve, i chiamanti non
   iterano nulla.
3. **Ingressi + verifica** — nessuna rotta nuova. Verifica: test per ogni evento nuovo (uno per atto) **più**
   la guida live del flusso vero — si elimina un documento nella copia del DB e lo si ritrova nel registro.
4. **Propagazione** — cambiano **tre firme** (`RevokeAsync`, `SetHiddenAsync`/`DeleteAsync`, le due
   `ForceUnlockAsync` dei repo): tutti i chiamanti nello stesso giro, fake dei test compresi
   (`AdminCodeTests.cs:128`). E cambia una **promessa scritta**: il sottotitolo della pagina.

## I sei buchi

| | Difetto | Dove |
|---|---|---|
| 1 | ⚠️ **Eliminare un documento non lascia traccia.** `DeleteAsync` è definitiva (cascade su versioni, sezioni, blocchi, release) e non scrive nulla. Dal 21 agosto il gesto è in mano ad admin **e** responsabili dell'ACC | `DocumentAdminService.cs:41`, `EfDocumentAdminRepository.cs:85` |
| 2 | **Nascondere/rimostrare un documento** cambia la visibilità pubblica e non lascia traccia | `EfDocumentAdminRepository.cs:75` |
| 3 | ⚠️ **La revoca di un permesso registra l'attore sbagliato**: `Audit(g.GrantedByUserId, …)` scrive **chi concesse**, non chi revoca. Un registro che attribuisce l'atto alla persona sbagliata è peggio di nessun registro | `EfEditGrantRepository.cs:59` |
| 4 | **Il force-unlock non è auditato**, né sui documenti né sulle risorse (`structure`/`newdoc`): togliere il lock a un'altra persona è un atto d'autorità, ed è esposto in UI dal 21 agosto | `EditingService.cs:236`, `ResourceLockService.cs:94` |
| 5 | **`AuditAction.HierarchyChange` non è scritto da nessuno** — valore enum morto — mentre il sottotitolo della pagina promette «pubblicazioni, permessi, **struttura**». Stessa lezione di `Ver_HistorySubtitle`: una promessa scritta che descrive un meccanismo inesistente | `Enums.cs:65`, `Audit_Subtitle` |
| 6 | **`VersioniPage.HistoryDetail` parsa `{"Areas":[…],"Saves":N}` che nessuno scrive** → ritorna sempre stringa vuota. La «storia modifiche» del pannello non è storia delle modifiche: sono publish e discard | `VersioniPage.razor:622` |

## Le decisioni

- **Una parola per un atto.** Si aggiunge `AuditAction.Delete` e la si usa **sia** per l'eliminazione di un
  documento **sia** per la revoca di un permesso: oggi la revoca dice `Archive`, che per una riga tolta dalla
  tabella è una bugia gentile. Le righe vecchie con `Archive` **restano** e la parte B le rende con la stessa
  frase — non si riscrive la storia, si smette di scriverla storta.
- **`ForceUnlock` è un valore suo**, non un `Update`: la domanda a cui il registro deve rispondere è «chi ha
  tolto il lock a chi», e i dettagli portano **chi lo teneva**.
- **L'attore lo passa il servizio, non lo indovina il repo.** `RevokeAsync`, `SetHiddenAsync`, `DeleteAsync` e
  le due `ForceUnlockAsync` prendono `actorUserId`, come fa già `AddAsync(… GrantedByUserId …)`. Il repo che
  si va a cercare l'utente corrente da sé è il modo in cui è nato il difetto 3.
- **Il non-evento non si scrive.** `SetParentAsync` chiamata con lo stesso padre che c'è già non produce una
  riga: un registro che accumula per sempre non si riempie di righe che dicono «non è cambiato niente».
- **I dettagli portano il nome, non solo l'Id.** `{"Id":10,"VersionNumber":2}` costringe chi legge a una
  seconda ricerca. Le righe nuove portano titolo/callsign/ACC accanto all'Id.
- **Cosa resta fuori, dichiarato**: gli **import** da sorgente esterna (SID, piste, settori) e i **salvataggi**
  di contenuto. I primi sono un giro loro (decisione del committente, 22-ago); i secondi
  riempirebbero il registro di rumore — la storia di un documento si legge dalle sue versioni.
- **Il difetto 6 non si cancella, si sostituisce**: il formattatore dei dettagli diventa **condiviso** fra la
  pagina Audit e il pannello di Versioni (parte B), così muore il parser morto e le due pagine dicono la
  stessa frase sullo stesso evento.

## Le slice (un commit ciascuna, build verde a ognuna)

1. **Meccanica**: `AuditScribe` (un punto di scrittura) e i quattro siti esistenti convertiti — **nessun
   cambio di comportamento**.
2. `AuditAction.Delete` + `ForceUnlock`; revoca permesso con l'**attore giusto** e la parola giusta.
3. Eliminazione e nascondi/mostra del documento nel registro, coi dettagli che portano il titolo.
4. Force-unlock (documento e risorsa) nel registro, coi dettagli che portano chi teneva il lock.
5. `HierarchyChange` scritto davvero in `SetParentAsync` (da → a), e il sottotitolo torna vero.

## Rete

- Un test per atto (Infrastructure, DB in memoria): elimina, nascondi, mostra, revoca, force-unlock ×2,
  cambio padre — ognuno lascia **una** riga con l'attore, l'entità e i dettagli attesi.
- Un test che il cambio padre **a parità di padre** non scrive niente.
- Un test che l'audit dell'eliminazione è scritto **prima** della cancellazione (come già fa `Discard`):
  dopo, il documento non esiste più e il titolo non sarebbe più leggibile.
