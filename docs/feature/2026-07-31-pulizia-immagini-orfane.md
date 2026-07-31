# Feature — Pulizia delle immagini non più usate (spazio recuperabile)

Data: 2026-07-31 · Stato: **CARTA** · Gate: [FEATURE-PROCESS](../FEATURE-PROCESS.md) ·
Segue: [immagini nei blocchi](2026-07-31-immagini-nei-blocchi.md) §R2, dove la pulizia era un non-obiettivo.

## Obiettivo

Togliere un blocco immagine da un documento **non** libera lo spazio: i byte restano in `MediaAssets`, di proposito
(una release pubblicata cita lo sha, e la stessa immagine può essere usata altrove). Serve un modo **esplicito** per
recuperare lo spazio di ciò che davvero non serve più: un'azione admin che prima mostra cosa toglierebbe e quanto
si guadagna, e cancella solo su conferma.

## Pre-flight — 4 domande

**1. Modello.** Nessuna entità nuova, nessuna colonna nuova: si legge `MediaAssets` e si confrontano gli sha con
quelli citati altrove. Il conteggio non si materializza da nessuna parte — un contatore di riferimenti sulla riga
sarebbe un secondo modello della stessa verità, che va in deriva appena qualcosa scrive senza aggiornarlo.

**2. Dispatch.** Nessun `switch` per tipo. C'è un solo punto che sa *dove* possono comparire i riferimenti
(`EfMediaMaintenance`) e un solo punto che sa *come* riconoscerli in un testo (`MediaReferenceScanner`).

**3. Ingressi + verifica.** Ingresso: `/vsop/admin/diagnostica`, che è già la pagina delle azioni di manutenzione
ed è già riservata agli admin (`Authz.IsAdmin`). Verifica: test sui casi che contano (sotto) + verifica live
caricando un'immagine, cancellando il blocco, e controllando che compaia nell'elenco solo quando non la cita più
nessuno.

**4. Propagazione.** Nulla viene rimosso o rinominato: la feature è additiva. Da aggiornare comunque la carta
delle immagini (il non-obiettivo «GC» diventa fatto) e la memoria.

## Design

### 1. Che cosa vuol dire «non usata»

Un asset è orfano se il suo sha **non compare** in nessuno di questi tre posti:

| Dove | Perché conta |
|---|---|
| `ContentBlock.BodyJson` di **tutte** le versioni | comprese le bozze non pubblicate: è la foto che qualcuno sta scrivendo adesso |
| `AirportExtraSection.Body` | le sezioni extra dell'aeroporto tengono i blocchi serializzati in un campo solo |
| `DocRelease.PayloadJson` | le **fotografie congelate** dei documenti: una vIPI dell'AIRAC scorso continua a citare quello sha |

### 2. Come si riconosce un riferimento — `MediaReferenceScanner`

Funzione pura: dato un testo, restituisce tutti gli sha citati. Cerca **qualunque sequenza di 64 caratteri
esadecimali**, non solo `"mediaId":"…"`.

Sembra grossolano ed è deliberato: i due errori possibili non si equivalgono.
- Riconoscere di *più* del dovuto ⇒ un asset orfano resta lì: si spreca spazio, che è il problema che stiamo
  già tollerando.
- Riconoscere di *meno* ⇒ si cancella un'immagine ancora in uso: si rompe un documento pubblicato, e in silenzio.

Il pattern largo sopravvive anche a un formato futuro che citasse lo sha in un altro modo, o al JSON **escapato**
dentro il payload di release (`"BodyJson":"{\"mediaId\":\"…\"}"`), dove un pattern preciso rischia di non agganciare.

### 3. Porta e servizio — `IMediaMaintenance`

```csharp
Task<MediaUsageReport> AnalyzeAsync(CancellationToken ct);        // non tocca niente
Task<int> DeleteOrphansAsync(IReadOnlyList<string> sha, CancellationToken ct);
```

- `MediaUsageReport`: totale asset e byte, elenco degli orfani (sha, nome originale, byte, data, chi l'ha caricata),
  byte recuperabili.
- `DeleteOrphansAsync` **ricontrolla** l'orfanità di ogni sha al momento della cancellazione, non si fida
  dell'elenco che ha in mano: fra l'analisi e il clic possono passare minuti, e in mezzo qualcuno può aver
  pubblicato o incollato quell'immagine in una bozza.
- Impl `EfMediaMaintenance` in Infrastructure (è tutta lettura di tabelle).

### 4. UI — una card in `/vsop/admin/diagnostica`

Due tempi, mai un colpo solo:

```
Immagini dei documenti
28 immagini · 9,4 MB in tutto                              [ Analizza ]

→ dopo l'analisi:
12 immagini non citate da nessun documento né release · 4,1 MB recuperabili
  foto-torre.png     820 KB   12/06/2026   704798
  schema-hold.png    640 KB   03/07/2026   512233
  …
                                       [ Elimina definitivamente ] (conferma in linea)
```

Nessun elenco = nessun pulsante di cancellazione. La conferma usa `InlineConfirm`, come le altre azioni distruttive
del progetto.

### 5. Perché a mano e non al boot

Un lavoro automatico farebbe il danno **mentre nessuno guarda**: basta che un domani si aggiunga un quarto posto in
cui un'immagine può essere citata e ci si dimentichi di includerlo in §1. Con il pulsante, prima si legge l'elenco.
Stesso motivo per cui il probe di drift dello schema **segnala** e non corregge (ADR-0007 §D1-bis).

## Passi

1. `MediaReferenceScanner` (puro) + test. *(cuore deterministico, test-first)*
2. `IMediaMaintenance` + `MediaUsageReport` (Application) e `EfMediaMaintenance` (Infrastructure) + registrazione DI.
3. Card in `DiagnosticaPage` + stringhe it/en.
4. Test EF sui casi che contano (§sotto) e verifica live.
5. Doc: questa carta a FATTO, la carta delle immagini (§R2 «non-obiettivo» → fatto), `rounds.md`, memoria.

## Casi che i test devono fissare

- immagine citata **solo da una release** già pubblicata → **non** è orfana;
- immagine citata solo da una **bozza** non pubblicata → **non** è orfana;
- immagine citata da una **sezione extra** d'aeroporto → **non** è orfana;
- immagine usata da **due** blocchi: resta finché non spariscono entrambi;
- immagine mai citata → orfana, e la cancellazione libera solo lei (le altre righe restano);
- sha passato alla cancellazione ma nel frattempo tornato in uso → **non** viene cancellato.

## Non-obiettivi

Quota per documento; cancellazione automatica; anteprima delle immagini nell'elenco (si mostra il nome del file,
non la foto: l'elenco è una lista di candidati alla cancellazione, non una galleria).
