# Sorgenti — cosa promette la policy e cosa fanno davvero gli import (carta, 22 agosto 2026)

> Ramo `ui-trasferimenti-densita`, pagina `/services/vsop/admin/sources`. Prima carta del giro: **la sostanza**.
> La forma sta nella gemella [`2026-08-22-sorgenti-densita-ui.md`](2026-08-22-sorgenti-densita-ui.md).
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md); regole: [regole-ui-pagine-admin](../design/regole-ui-pagine-admin.md).

## La domanda della pagina

«**Quali dati arrivano dalla sorgente, e quali li gestiamo a mano?**» — e, subito dopo, la domanda che oggi
la pagina non lascia fare: «**quello che ho spuntato, viene rispettato?**».

La risposta, misurata leggendo tutti i punti di import, è **no per due categorie su cinque**. La pagina
scrive una promessa (`Sorg_Intro`: «l'import non la tocca più») che il codice mantiene per SID e Aree
regolamentate, e **non** mantiene per Settori, Transition Altitude e Piste. È lo stesso difetto del giro
Audit: la pagina prometteva «pubblicazioni, permessi, struttura» e la struttura non la scriveva nessuno.

## Cosa ho trovato (letto nel codice, non stimato)

### ⚠️ S1 — «Settori» non è rispettato da **nessun** import

`AirportSectorImporter.ImportAsync` è il corpo condiviso da **quattro** chiamanti e non legge la policy:

| Chiamante | Da dove | Gate policy |
|---|---|---|
| `AirportSectorImportHostedService` | automatico, ogni 24h | ❌ |
| `AirportSectorService.ImportFromSourceAsync` | bottone editor aeroporto + massivo `/services/vsop/admin/airports` | ❌ |
| `AirportImportUseCase.RunAsync` | «Assegna aeroporti noti» in Struttura | ❌ |
| `StructureEditingService.GenerateAirportDocumentCoreAsync` | «Genera documenti» | ❌ |

L'unico posto dove `Sectors` conta è `AddSectorAsync` (kind=Airport), che **vieta** l'aggiunta manuale quando
la categoria è importata. Quindi oggi il flag fa **metà** del suo mestiere: escluderlo ti lascia aggiungere
settori a mano, e poi il giro delle 24h ci ripassa sopra. È esattamente la lezione già imparata sulle Aree
regolamentate — **il gate va nel corpo condiviso auto/manual**, non nel chiamante — applicata a metà.

### ⚠️ S2 — «Genera documenti» scavalca TA e Piste

`ReimportFromSourceAsync` (editor aeroporto) rispetta la policy: se la categoria è esclusa passa `null` /
lista vuota a `MergeFromSourceAsync`, che li legge come «nessun cambio». `GenerateAirportDocumentCoreAsync`
fa la **stessa** chiamata **senza** leggere la policy: scarica TA dall'anagrafica e piste dal dettaglio e le
passa al merge. Effetto misurabile in `EfAirportRepository.MergeFromSourceAsync`:

- `airport.TransitionAltitudeFt = ta` — la TA scritta a mano viene sovrascritta;
- per ogni pista di sorgente: `ex.LengthM`/`ex.Bearing` sovrascritti, e le piste che l'utente aveva
  **tolto** rientrano;
- `RecomputeDefaultBandLevels` ricalcola i TL di fascia sulla TA appena arrivata.

Chi lo scatena: `AeroportiPage` (massivo, un aeroporto per volta) e il bottone «Genera documenti».
Due chiamanti dello stesso merge, due politiche diverse: **la regola del formattatore unico** vale anche per
i gate («un gate per categoria, non uno per chiamante»).

### ⚠️ S3 — cambiare la policy non lascia traccia

Il salvataggio è admin-only e cambia il **regime di scrittura di tutta l'applicazione**: dopo il giro Audit,
è l'ultimo atto amministrativo rimasto muto (documenti, permessi, force-unlock e struttura ora scrivono).
Nessuna riga in `AuditLog`: se domani le piste di LIRF smettono di aggiornarsi, non c'è modo di sapere
**chi** ha tolto la spunta né **quando**.

### ⚠️ S4 — `UpdatedUtc`/`UpdatedByUserId` si scrivono e non li legge nessuno

`ImportPolicy` porta autore e data dal primo giorno; `ImportPolicySnapshot` non li espone e la pagina non li
mostra. Vale il doppio qui per un motivo concreto e già scritto in memoria: **`ImportSids` è nato `false`**
(migration `AddSidImport`, 8 luglio 2026, colonna `bool` NOT NULL su tabella già popolata). In produzione
`Sids=false` può essere una decisione dell'admin **o** l'effetto della migrazione, e dal valore non si
distingue. Con autore e data a video la domanda si chiude in tre secondi: `UpdatedByUserId = 0` ⇒ quella
riga non l'ha mai salvata una persona.

### ⚠️ S5 — lo stato import dice «ok» anche quando non ha importato niente

`GatedImportLoop` marca il successo se il run **non ha lanciato eccezioni**. Ma con la categoria esclusa
`SidImporter.ImportAsync` esce subito restituendo 0, `SpecialAreaImportUseCase.RunAsync` restituisce
`Empty`, e il loop scrive comunque `MarkSuccess`. La tabella «Stato import periodici» mostra quindi
**verde e data di oggi** per una categoria che per scelta non importa nulla. Verde non è la parola giusta:
la parola è **«esclusa»**, e la sa solo l'altra tabella della stessa pagina.

Nella stessa tabella compare `SpecialAreaForeignOptOut`, che **non è un import**: è il segnaposto
«riconciliazione già fatta» delle aree degli ACC esteri. In un elenco intitolato «stato degli import
periodici» è una riga che mente. E le categorie sono scritte col nome in codice (`AirportSector`) mentre la
tabella sopra le chiama «Settori»: **due vocabolari per le stesse cinque cose, nella stessa schermata**.

### S6 — TA e Piste non hanno un import periodico, e la pagina non lo dice

Le tre righe di stato reali sono `Acc`, `AirportSector`, `SpecialArea`, `Sid`. TA e Piste arrivano **solo su
richiesta** (reimport nell'editor aeroporto, generazione documento): non c'è nessun giro automatico, e la
pagina lascia credere il contrario mettendole nello stesso elenco di categorie «importate». Va detto in
riga: *«su richiesta — Reimporta nell'editor aeroporto»*.

E c'è una categoria importata che la pagina **non nomina affatto**: l'anagrafica **ACC** (`ImportCategories.Acc`,
ogni 24h). Non ha un flag di policy — è sempre di sorgente — ma è l'unica riga di stato che oggi appare senza
che nessuna riga di policy la spieghi.

## Cosa faccio

Sei slice, un commit ciascuna, `dotnet build -c Release --no-incremental` (0 avvisi) + `dotnet test` su
**entrambi i TFM** a ogni commit.

### 1. Il gate dei Settori nel corpo condiviso
`AirportSectorImporter` prende `IImportPolicyStore`; se `Sectors` è escluso esce **prima della fetch**
restituendo `(0,0)` — come fa `SpecialAreaImportUseCase`, con lo stesso commento sul perché il gate sta qui
e non nell'hosted service.

⚠️ Un effetto va gestito, non subìto: `GenerateAirportDocumentCoreAsync` importa il catalogo **quando è
vuoto** e poi lo usa come fonte unica. Con i Settori esclusi e il catalogo vuoto il documento uscirebbe senza
settori e senza spiegazioni. Torna invece il messaggio già previsto dal tipo `AirportDocResult`:
«Settori esclusi in Sorgenti: aggiungi i settori d'aeroporto a mano in Struttura».

Test: policy esclusa ⇒ `ImportAsync` non chiama la porta sorgente e non tocca il catalogo; `GenerateAirportDocument`
su aeroporto senza catalogo ⇒ risultato non riuscito col messaggio.

### 2. Un solo punto che decide cosa passa al merge
Estraggo la decisione «TA e piste da passare a `MergeFromSourceAsync` secondo la policy» (oggi dentro
`AirportEditingService.ReimportFromSourceAsync`) e la uso **anche** in `GenerateAirportDocumentCoreAsync`.
Meccanico + comportamento in due commit distinti se l'estrazione tocca più di una firma.

Test di caratterizzazione: con TA/Piste escluse, generare il documento **non** cambia
`TransitionAltitudeFt` né lunghezza/bearing delle piste, e non ne riporta indietro una cancellata a mano.

### 3. Il cambio di policy entra nel registro
`AuditScribe.Write` dentro `EfImportPolicyStore.SaveAsync`, nella **stessa** `SaveChanges` (l'audit descrive
l'atto avvenuto): `EntityType = "ImportPolicy"`, `Action = Update`, dettagli = **solo le categorie
cambiate**, ognuna con `da → a`. ⚠️ **Il non-evento non si scrive**: salvataggio che non cambia nulla ⇒
nessuna riga (e la pagina non dirà «salvato» a vuoto).

`AuditNarrator`: nuova famiglia `Categoria.Sorgenti` (chip sulla pagina Audit) e la frase
«Sorgenti — Piste: da sorgente → manuale», con le etichette **già usate** dalla pagina Sorgenti: un
vocabolario solo. Test: il chip conta, la frase si legge, e una riga senza dettagli non rompe il narratore.

### 4. Chi ha deciso, e quando
Nuovo DTO di sola lettura `ImportPolicyInfo(ImportPolicySnapshot Policy, DateTime? UpdatedUtc, int UpdatedByUserId)`
e `GetInfoAsync` sullo store. ⚠️ **Non tocco `ImportPolicySnapshot`**: è un record posizionale usato in tre
suite di test e in cinque punti di dominio; aggiungergli campi lo trasformerebbe da «stato» a «stato + chi».

La pagina mostra «deciso da *nome* il *data*» (nome dal roster, come su Audit) e, quando
`UpdatedByUserId == 0` **e** almeno una categoria è esclusa, la frase che chiude la domanda aperta in
produzione: *«nessuna persona ha mai salvato questa policy: le esclusioni che vedi vengono dai valori di
partenza delle colonne»*.

### 5. Lo stato import dice la verità
- Le righe del report si **uniscono** alla policy: una categoria esclusa si mostra «esclusa», non «ok».
- `SpecialAreaForeignOptOut` **fuori** dal report (è un segnaposto, non un import); resta spiegato nel «?».
- Le categorie prendono le **stesse etichette** della policy (`AirportSector` → «Settori»); la chiave in
  codice resta nel `title` della cella, come il JSON su Audit.
- **Scadenza**: nuova porta `IImportSchedule` in `Vipi.Application.Abstractions` (`TimeSpan? PeriodOf(string category)`),
  implementata in Infrastructure leggendo `IvaoOptions`/`SectorfileOptions`. ⚠️ Serve la porta perché
  `Vipi.Ui` referenzia **solo** Application e Domain: leggere `IvaoOptions` dalla pagina non si può, e non si
  deve. Con la cadenza nota la riga dice «prossimo giro atteso alle HH:MM» e diventa **ambra** se
  l'ultimo successo è più vecchio di due periodi.

### 6. Le due righe che mancano
Riga **ACC (anagrafica)**: sempre di sorgente, senza spunta, con il suo stato — così l'elenco degli stati
non ha più righe orfane. Righe **TA** e **Piste**: al posto della data, «su richiesta» con il link al posto
da cui si scatena.

## Cosa NON faccio, e perché

- **Non aggiungo un «importa adesso» globale.** I trigger manuali esistono già dove l'oggetto vive (ACC e
  Aree in `/services/vsop/admin/accs`, settori e SID nell'editor aeroporto, massivi in `/services/vsop/admin/airports`): un
  sesto bottone che fa la stessa cosa da un'altra pagina è il modo in cui due elenchi della stessa cosa
  divergono. La riga di stato **porta il link** a dove il giro si lancia.
- **Non ribalto `ImportSids` in produzione.** `false` resta indistinguibile da una scelta: la pagina ora lo
  **dichiara**, la decisione è del committente.
- **Non cambio `GatedImportLoop`.** Marcare il successo quando la categoria è esclusa è corretto per il
  gate (non c'è niente da riprovare): è il *racconto* a essere sbagliato, e si corregge dove si racconta.

## Rischi

- Il gate dei Settori **spegne** un import che oggi gira sempre: se in produzione `ImportSectors` fosse
  `false` per lo stesso incidente di `ImportSids`, l'aggiornamento dei settori si fermerebbe al deploy. ⚠️
  Da guardare **prima** in `/services/vsop/admin/sources` di produzione (la colonna «Provenienza» lo dice già oggi) —
  ed è la ragione per cui la slice 4 (chi/quando) vale la pena **prima** di andare in produzione, non dopo.
- `AuditScribe` è `internal` a `Vipi.Infrastructure`: la scrittura sta nello store EF, non nel service
  Application. È già così per gli altri punti di scrittura.
