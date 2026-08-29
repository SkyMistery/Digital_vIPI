# Feature — Biblioteca allegati (PDF su Drive di divisione, linkati nei documenti)

Data: 2026-08-25 · Aggiornata: **2026-08-29** (modo *incorporato*) · Stato: **CARTA** (nessuna slice avviata) · Gate: [FEATURE-PROCESS](../FEATURE-PROCESS.md)

## Obiettivo

Gli allegati (LoA firmate, circolari, manuali, carte) si caricano **in un posto solo** e si **linkano** dai
documenti dove servono. Il sito tiene il registro: chi cita cosa, in che versione, e cosa si romperebbe
cancellando una voce.

Due modi di linkare: un **blocco «Allegato»** accanto a Paragrafo/Callout/Tabella/Immagine, e un **link
inline** dentro la prosa. Il blocco rende in **due modi**, scelti blocco per blocco: **Link** (default) e
**Incorporato** (il PDF si legge dentro la pagina). Vedi §3-bis.

## Il vincolo che decide il deposito

I byte **non stanno da noi**. Due ragioni, entrambe esterne e non negoziabili:

1. il piano di hosting **non ammette il formato PDF** — vincolo contrattuale, non tecnico: non si aggira
   mettendo i byte in MariaDB;
2. **IVAO HQ indica di tenere i documenti sul Drive di divisione**.

Quindi: deposito = **Google Drive di divisione, account IVAO**. Noi teniamo metadati, organizzazione,
versioni e registro dei link.

⚠️ Conseguenza da mettere agli atti: il file Drive è condiviso «chiunque abbia il link», quindi **tutto ciò
che entra in biblioteca è pubblico**. Allegati riservati allo staff **non sono supportati** — un controllo di
accesso davanti a un URL Drive pubblico sarebbe teatro. Confermato che non servono.

## Stato di partenza (rilevato nel codice, 2026-08-25)

| Cosa | Dove | Nota |
|---|---|---|
| Formati blocco | `Vipi.Domain/Enums.cs:46` | `Table, Prose, Image, List, AorMap, Callout` — **nessun Attachment** |
| Dispatch per-formato | 9 file citano `BlockFormat.Image` | 2 editor, `BlockRenderer`, `AeroportoPage`, rebuild aeroporto, ricerca, manutenzione, modello extra |
| Renderer markdown | `Vipi.Ui/MarkdownLite.cs` | Grassetto/corsivo/a capo. **Nessun link, di nessun tipo** |
| Scanner riferimenti | `Application/Media/MediaReferenceScanner.cs` | Cerca 64 esadecimali. Deliberatamente largo |
| Registro citazioni | `EfMediaMaintenance.ReferencedShasAsync` | Legge **4 fonti**: blocchi (bozze comprese), sezioni extra, `DocReleases.PayloadJson`, blocchi condivisi. Torna un `HashSet` piatto |
| Serve i byte | `VipiModuleExtensions.cs:335` (`/vsop/media/{sha}`) | Immagini, cache `immutable` |
| Intestazioni sicurezza | `VipiStartup.cs:187` e `:213` | `X-Frame-Options: DENY` riguarda **le nostre** pagine dentro iframe altrui: non ci vieta di incorporare. ⚠️ La CSP **non ha `frame-src`** ⇒ cade su `default-src 'self'`; ed è **Report-Only**, quindi oggi segnalerebbe senza bloccare |
| Pagine admin | `/services/vsop/admin/*` | Convenzione rotte: segmento inglese kebab |
| Pagina Cambiamenti | `Pages/ChangedPage.razor` | Esiste, mostra i cambi per AIRAC |
| Indice ricerca | `EfSearchRepository.cs:86` | Indicizza `Body` e `BodyJson` grezzo |
| Giro import giornaliero | `ImportPolicyService` | Tutti gli import girano ogni 24h |
| Audit | `AuditLogs` + enum azione (stringa) | Aggiungere un valore è additivo e sicuro |

## Pre-flight — 4 domande

**1. Modello — aggiungo un concetto o ne esiste già uno?**

Si aggiunge **un** concetto: l'`Attachment` (voce di biblioteca). Non è un gemello di `MediaAsset`:
`MediaAsset` sono **byte nostri indirizzati dal contenuto** (immagini), `Attachment` è un **riferimento a un
documento esterno con identità stabile**. Restano separati e non si somigliano.

⚠️ Il modello è a **due livelli**, ed è il punto che regge tutto:

```
Attachment (slug stabile)  →  AttachmentVersion corrente  →  file su Drive
```

Se il link nel documento citasse il file, sostituire un PDF vorrebbe dire **riaprire tutti i documenti che lo
citano**. Citando lo slug, la sostituzione è lo spostamento di un puntatore.

Domanda di controllo: «dove si salva un allegato?» → **un** posto (`Attachments`, con `AttachmentVersions` per la
storia).

**2. Dispatch — switch per-tipo duplicato?**

Sì, ed è il momento di dirlo chiaro: il `switch (Format)` esiste in **9 file**, e questa è la **seconda**
feature che vi aggiunge un `case` — la prima furono le immagini, che decisero esplicitamente di non aprire il
registry. La regola del 2 è ampiamente superata.

**Decisione: NON si apre il registry qui.** Motivo: sarebbe un refactor trasversale a 9 file mescolato a una
feature, cioè esattamente ciò che il gate vieta. Ma va **annotato come debito** nel doc refactor: alla terza
volta si apre, e questa carta è la prova che la terza volta arriverà.

Come per le immagini, il corpo di ogni `case` è **una riga** che monta un componente condiviso
(`AttachmentBlockEditor` in editing, `AttachmentLink` in resa).

**3. Ingressi + verifica**

- Ingresso biblioteca: `/services/vsop/admin/attachments`, voce nel menù admin (livello **Editor**: è contenuto documentale, come le radioassistenze).
- Ingresso link: pulsante **«+ Allegato»** accanto agli altri, in entrambi gli editor; più la sintassi inline
  nella prosa.
- **Catch-22 da evitare**: l'elenco deve mostrare le voci **anche se non le cita nessuno**, altrimenti la
  prima voce caricata non è raggiungibile. Anzi, «non citata da nessuno» è un **filtro** dell'elenco.
- Verifica: §Verifica live.

**4. Propagazione — rimuove o rinomina qualcosa?**

Niente da rimuovere: è additiva. Da aggiornare nello stesso giro:

- guida in-app (`/services/vsop/guide#editor-blocchi`) che elenca i tipi di blocco;
- `HelpHint` di `DocumentBlocksEditor` che nomina i formati;
- la regola «in biblioteca solo materiale pubblico», che va **scritta nella guida**, non solo qui.

## Design

### 1. Le due entità

⚠️ **Nomi come stanno nel codice.** Le classi si chiamano `Attachment` e `AttachmentVersion`, non
`Allegato`/`AllegatoVersione`: qui dentro i tipi sono in inglese e la prosa che li spiega in italiano
(`MediaAsset`, `GlossaryTerm`, `Navaid`), e una coppia italiana in mezzo si leggerebbe come roba d'un'altra
casa. Il **blocco** e il **token** restano `allegato:`, che è quello che vede chi scrive un documento.

`Attachment` — la voce di biblioteca, **identità stabile**:

| Campo | Nota |
|---|---|
| `Slug` | `loa-lirr-lfmm` — indice unico, è ciò che i documenti citano. **Non si cambia mai** |
| `Title` | quel che si legge nel link |
| `Kind` | LoA · Circolare · Carta · Manuale · Altro |
| `Scope` + `ScopeKey` | Divisione · Acc (`LIRR`) · Scalo (`LIMC`). ⚠️ La chiave è una **stringa, non una FK**: una LoA resta da leggere anche il giorno che l'anagrafica non ha più quella riga |
| `Notes` | libere |
| creato/aggiornato da/quando | |

⚠️ **Qui NON c'è nessun riferimento ai byte, e la carta del 25 agosto diceva il contrario.** Metteva
`DriveFileId` sia sulla voce sia sulla versione, «di norma identici». Due posti che dicono la stessa cosa
sono due posti che un giorno diranno cose diverse — e quello sbagliato sarebbe proprio quello che serve il
link. Dove stiano i byte lo dice **solo** la versione; la corrente è quella col `Number` più alto. Di
conseguenza **una voce nasce con la v1**: non esiste lo stato «voce senza file», che sarebbe una riga che
non sa rispondere alla domanda per cui esiste.

`AttachmentVersion` — la storia, tenuta **da noi** anche se i byte non sono nostri:

| Campo | Nota |
|---|---|
| `Number` | progressivo **per allegato**, da 1. Il più alto è la versione corrente |
| `Provider` + `ExternalId` | dove stanno i byte e come si chiamano lì: `Drive` + l'id del file. ⚠️ **Non** `DriveFileId`: il §2 dice che il deposito è una colonna, e un nome che dice «Drive» la richiuderebbe. Di norma l'id resta identico fra una versione e la successiva (Drive sostituendo un file ne mantiene l'id); cambia se hanno caricato un file nuovo — o se un giorno cambia il deposito |
| `Note` | «rifirmata dopo modifica CoP» |
| chi / quando | |

**Categorie a due assi, non cartelle.** «Le LoA di Roma» = due filtri. Un albero di cartelle a 50+ file si
riempie di roba archiviata male e nessuno la ritrova.

### 2. L'identità del link è NOSTRA

```
documento → /vsop/files/{slug} → 302 → drive.google.com/file/d/<ID>/preview
```

Il documento **non contiene mai un URL Drive**. Conseguenze:

- cambiare deposito domani (Drive → GitHub → di nuovo in casa, se l'hosting cambia) **non tocca un solo
  documento**: è una colonna in una tabella;
- il conteggio dei download, la segnalazione «link morto» e ogni futuro controllo stanno dove possiamo
  scriverli;
- l'ID Drive resta un dettaglio di persistenza.

Depositi possibili, **tutti** compatibili con questa rotta senza toccare un documento:

| Deposito | Nota |
|---|---|
| **Drive di divisione** (v1) | quello indicato da HQ. Caricamento a mano, link incollato una volta sola |
| Drive via API | service account sul drive condiviso: la sostituzione è una revisione nuova, l'ID non cambia. Fuori perimetro v1 |
| **Cloudflare R2** | c'è già Cloudflare davanti al sito dal 25-ago. I byte restano fuori dall'hosting, quindi il vincolo contrattuale regge |
| GitHub, repo pubblico | ripiego: quota e retention nostre |
| In casa | solo se cambia il piano di hosting |

⚠️ La rotta `/vsop/files/{slug}` **non può essere `immutable`**: sostituisci il PDF e il browser terrebbe il
vecchio. Va `no-cache`. È la trappola che rende la sostituzione «non funzionante» in modo intermittente e
inspiegabile.

### 3. Come si cita: UN token, ovunque

Un solo formato di riferimento, in tutte e due le forme di link:

- blocco: `BodyJson` porta `{"ref":"allegato:loa-lirr-lfmm","titolo":"…"}`
- prosa: `[LoA Marseille](allegato:loa-lirr-lfmm)`

Un token solo ⇒ **una sola regex** ⇒ lo scanner esistente si estende invece di sdoppiarsi. È la stessa scelta
che ha reso `MediaReferenceScanner` deliberatamente largo.

⚠️ **`MarkdownLite` non ha link di nessun tipo.** Va aggiunto il supporto, ma **solo per lo schema
`allegato:`**: mai URL generici. Il renderer fa HTML-encode e poi regex — aprire `[testo](url)` qualunque
significherebbe far entrare `javascript:` e link esterni arbitrari nel contenuto editoriale. Un solo schema,
riconosciuto per prefisso, risolto in un `href` che costruiamo noi.

### 3-bis. Due modi di resa: Link e Incorporato

Il blocco «Allegato» porta un campo `Modo`, e la scelta è **per blocco**, non per voce di biblioteca: la
stessa LoA può stare incorporata nella vLOA che la riguarda e restare un semplice link in una vIPI che la
cita di sfuggita.

| Modo | Cosa si vede | Quando |
|---|---|---|
| **Link** (default) | titolo, tipo, segnale «porta fuori dal sito» | citazioni di passaggio, elenchi, tutto ciò che finisce anche su carta |
| **Incorporato** | `<iframe src="/vsop/files/{slug}">` col visualizzatore Drive nella pagina, **più il link sotto** | quando il documento *è* l'allegato: una LoA firmata dentro la sua vLOA |

Tre decisioni dentro il modo incorporato:

- **L'iframe punta alla NOSTRA rotta**, `src="/vsop/files/{slug}"`: il 302 vale anche dentro un iframe, e
  l'ID Drive resta fuori dai documenti esattamente come nel link. Nessuna eccezione al §2.
- **Il link sotto c'è sempre**, anche da incorporato. È il ripiego che tiene la pagina utile il giorno che
  Google chiude l'embed (R8), ed è l'unica cosa che sopravvive alla stampa.
- **Altezza a tre scaglioni** (bassa / media / alta), non un numero libero: un numero libero produce
  riquadri da 3000px e non se ne accorge nessuno finché non li apre un telefono.

⚠️ **Serve `frame-src https://drive.google.com` nella CSP.** Oggi la direttiva manca del tutto, quindi si
cade su `default-src 'self'`. Siccome l'intestazione è **Report-Only**, l'iframe funzionerebbe lo stesso e la
violazione sarebbe **solo segnalata**: il difetto resta invisibile fino al giorno in cui la CSP entra in
vigore e la resa incorporata muore in blocco, ovunque. Va aggiunta **nella stessa slice**, non «poi».

⚠️ **Non è PDF.js in casa.** Drive non serve i byte con CORS, quindi renderli noi vorrebbe dire fare da
**proxy ai byte**: è grigio sul vincolo contrattuale del §«Il vincolo che decide il deposito», e non lo
tocchiamo. Il visualizzatore è quello di Google, barra e branding compresi — non si toglie.

### 4. Il registro dei link: ricavato, non mantenuto

La tentazione è una tabella di join `Attachment ↔ Document` aggiornata a ogni salvataggio. **No**: si
desincronizza al primo percorso di scrittura che dimentica di aggiornarla, e mente proprio quando serve.

Si estende invece `EfMediaMaintenance.ReferencedShasAsync`, che già legge le **quattro** fonti giuste, da
«insieme piatto» a «chi cita cosa»: tipo sorgente, documento, sezione, URL cliccabile. Non può mentire,
perché legge le stesse righe che il viewer rende. Costo: 219 blocchi + 36 release nel `vipi.db` reale —
irrilevante.

Ne escono due funzioni:

- `DoveUsato(slug)` → l'elenco delle citazioni (per la guardia e per la conferma di sostituzione);
- «mai usata» → il filtro che tiene pulita la biblioteca.

### 5. Sostituzione — il link segue **sempre** la versione corrente

Non si congela. La regola di casa è già scritta in `DocRelease.cs`: la release congela le **scelte
editoriali**, mentre «i dati derivati NON sono nello snapshot: si renderizzano sempre coi cataloghi
correnti». Una LoA firmata è un catalogo esterno, come una frequenza.

Congelare avrebbe anche un difetto pratico grave: caricata la scansione sbagliata e pubblicata, l'unico modo
di correggerla sarebbe **ripubblicare tutti i documenti che la citano**.

Cosa succede caricando la v3:

| Chi | Vede |
|---|---|
| Bozze | v3, subito |
| Release pubblicate che la citano | **v3**, subito — nessun documento da riaprire |
| Biblioteca | `v3 · agg. 25-ago · sostituisce v2` |

Prima di confermare, la **conferma informata** — è qui che vive la tracciatura richiesta, in positivo:

```
Sostituisci "LoA Roma–Marseille" (v2 → v3)

Cambia ciò che vedono 3 documenti:
  • vLOA LIRR↔LFMM      pubblicato, AIRAC corrente   [apri]
  • vIPI ACC LIRR        pubblicato, AIRAC 2608       [apri]
  • vIPI APP LIRF        bozza                        [apri]

Nota di versione: [____________]      [Sostituisci]  [Annulla]
```

Più la riga in `AuditLogs` e una voce in **Cambiamenti**: così lo staff si accorge che un riferimento è
cambiato sotto un documento pubblicato senza che nessuno l'abbia ripubblicato.

### 6. Cancellazione — guardia, non sorpresa

Cancellare una voce citata mostra l'elenco delle citazioni con i link, e chiede conferma esplicita.
Cancellare **non** tocca il file su Drive: toglie la voce e lascia i link da correggere, elencati.

### 7. Salute dei link

Giro giornaliero, dentro `ImportPolicyService` come gli altri: il file Drive risponde ancora? Link morto →
segnalato in biblioteca e in Cambiamenti. Senza questo il link rot è **invisibile** — ed è il difetto
principale del sistema attuale, dove i link a Drive stanno sparsi nei documenti e nessuno sa quali né dove.

### 8. Contorno

- **Ricerca**: gli allegati entrano nell'indice — cercare «Marseille» deve trovare la LoA.
- **Stampa**: un link ad allegato su carta è inutile senza l'URL accanto → regola in `vipi-print.css`.
  ⚠️ E l'iframe **non esce in stampa**: nel modo incorporato la carta deve mostrare titolo + URL, cioè
  esattamente il ripiego del §3-bis. Senza quella regola una vLOA stampata perde l'allegato **in silenzio**.
- **Segnale «esterno»**: il blocco mostra che il clic porta fuori dal sito. Il lettore lo deve sapere prima.

## Slice (ordine proposto)

| # | Slice | Chiude |
|---|---|---|
| 1 | ✅ **FATTA il 29 agosto** — entità `Attachment` + `AttachmentVersion`, migrazioni **×2** (SQLite + MySQL) | modello |
| 2 | ✅ **FATTA il 29 agosto** — pagina `/services/vsop/admin/attachments`: elenco, filtri tipo×ambito, ricerca, crea voce | ingresso, catch-22 |
| 3 | ✅ **FATTA il 29 agosto** — rotta `/vsop/files/{slug}` con 302 e `no-cache` | identità del link |
| 4 | ✅ **FATTA il 29 agosto** — scanner del token `allegato:` + `DoveUsato(slug)` + filtro «mai usata» | registro |
| 5 | ✅ **FATTA il 29 agosto** — blocco `Attachment`: enum in coda, `case` nei punti che dispatchano davvero, editor e resa condivisi — **modo Link** | link 1 |
| 5-bis | ✅ **FATTA il 29 agosto** — modo Incorporato: campo `Modo` + altezza a scaglioni, iframe sulla nostra rotta, link di ripiego, **`frame-src` in CSP** | resa in pagina |
| 6 | ✅ **FATTA il 29 agosto** — link inline in `MarkdownLite`, **solo schema `allegato:`** | link 2 |
| 7 | Sostituzione con conferma informata + audit + voce in Cambiamenti | versioni |
| 8 | Cancellazione con guardia; ricerca; stampa; guida in-app | contorno |

Un commit per slice, `dotnet build` verde a ogni commit.

## Rischi

| # | Rischio | Mitigazione |
|---|---|---|
| R1 | ~~`MarkdownLite` non ha link: aprirlo a URL generici farebbe entrare `javascript:` e link esterni nel contenuto editoriale~~ | ✅ **CHIUSO il 29 agosto**: un solo schema riconosciuto (`allegato:`), lo **slug vincolato alla sua forma** e l'`href` costruito da noi. 22 test, di cui metà sono cose che **non** devono diventare link (`javascript:`, `data:`, `vbscript:`, URL assoluti e relativi, `allegato:../../qualcosa`, un apice nel testo) |
| R2 | Nono punto di dispatch per-formato; secondo `case` aggiunto senza registry | Decisione esplicita di non aprirlo qui + debito annotato nel doc refactor |
| R3 | ~~Il reconciler Postgres allinea colonne e indici, non crea tabelle~~ | ✅ **VERIFICATO IL 25-AGO: non è più un rischio.** Il commit `eac14fd` ha chiuso l'R1 del doc immagini: `PostgresSchemaReconciler.EnsureModelTables` genera la DDL **dal diff del modello EF**, quindi una tabella nuova nasce da sola senza elenchi di colonne da tenere aggiornati. Coperto da 3 test in `PostgresSchemaReconcilerTests`. Riguarda comunque solo Render/Neon (**ambiente di prova**): SQLite e MariaDB usano migrazioni versionate |
| R4 | Le revisioni Drive scadono. ✅ **29-ago: nessuna pulizia periodica** sul drive condiviso (risposta del committente), quindi resta solo la scadenza di piattaforma qui sotto | ✅ **VERIFICATO IL 25-AGO, impatto minimo.** Regola misurata sulla doc Drive API: le revisioni «purgabili» durano **~30 giorni**, e possono cadere prima se il file ha **100** revisioni non marcate e se ne carica un'altra. Fino a **200** revisioni si possono marcare «Keep Forever», e occupano quota. ⚠️ **Ma la revisione di testa non viene MAI purgata**: la versione *corrente* — l'unica che i documenti servono — è sempre al sicuro. La scadenza tocca solo i byte delle versioni passate, che erano già fuori perimetro |
| R5 | File Drive molto scaricati possono finire in throttling | Improbabile col traffico di una divisione. Il giro giornaliero lo renderebbe visibile |
| R6 | Tutto pubblico: un PDF linkato in una **bozza** è comunque già scaricabile | Regola scritta in guida. Nessun riservato in biblioteca |
| R7 | Migrazioni doppie e additive: una colonna nuova non la riempie nessuno da sola | Vale la lezione degli aeroporti militari |
| R8 | **Google può chiudere l'embed** della preview senza preavviso, come fece CARTO col fondo mappa il 27-ago | Il link di ripiego sotto l'iframe c'è **sempre**: si perde la comodità, non l'allegato. Da provare **dal vivo** prima di dire chiusa la 5-bis — nessun test qui dentro apre un browser |
| R9 | `frame-src` dimenticato: oggi **non si vedrebbe** (Report-Only) e l'incorporato morirebbe il giorno del passaggio a CSP vera | Riga aggiunta **dentro** la slice 5-bis. Vale la lezione delle tessere OpenTopoMap, mancanti per giorni proprio perché l'intestazione segnala e non blocca |

## Verifica live

Non bastano i test — le regressioni di binding Blazor sono silenziose coi test verdi. Da guidare sul flusso
reale, con traccia:

1. creo una voce, la linko con **il blocco** in una sezione e con **il link inline** in un'altra;
2. pubblico il documento e apro la pagina pubblica: il link porta al PDF giusto;
3. sostituisco la voce (v2) → **senza toccare il documento**, la pagina pubblica serve la v2;
4. la conferma di sostituzione elencava quel documento fra gli impattati;
5. provo a cancellare la voce → la guardia elenca le due citazioni;
6. il filtro «mai usata» mostra una voce nuova non ancora linkata;
7. metto lo **stesso** allegato in modo **incorporato** in una terza sezione: il PDF si legge **dentro** la
   pagina e il link di ripiego sta sotto;
8. stampo quella pagina: al posto dell'iframe compaiono titolo e URL. E la console del browser non lamenta
   `frame-src`.

## Fuori perimetro (deciso, non dimenticato)

- **Caricamento dal sito via API Drive** (service account membro del drive condiviso; la sostituzione
  diventa una revisione nuova, l'ID e quindi il link non cambiano). Nella v1 lo staff carica su Drive a mano
  e incolla il link **una volta sola**. Da valutare dopo, se il caricamento a mano dà fastidio.
- **Allegati riservati**: richiederebbero file Drive ristretti + link temporanei via API. Confermato che non
  servono.
- **Permalink a una versione passata**: le revisioni Drive non sono linkabili, e (verificato, vedi R4) i loro
  byte si purgano dopo ~30 giorni salvo marcarle a mano. `AttachmentVersion` dice quindi **chi, quando e
  perché**, non «riscarica la v1»: la storia è nostra, i byte vecchi sono archeologia. La revisione corrente
  invece non scade mai.
- **`IPI Roma ACC.pdf` (180 MB)** e gli altri monoliti: è il documento che il sito **sostituisce**, non un
  allegato.
- **Visualizzatore nostro (PDF.js)**: Drive non serve i byte con CORS, quindi vorrebbe dire fare da **proxy
  ai byte**. Grigio sul vincolo contrattuale: non si tocca. Il visualizzatore resta quello di Google.
- **Anteprima come immagine** (`thumbnailLink`): richiede l'API Drive, fuori perimetro nella v1.
- **Registry dei formati blocco**: vedi R2.

## Accesso al deposito (risposto il 29 agosto 2026)

- ✅ **Nessuna pulizia periodica** sul drive condiviso: niente scade per policy di divisione. Restano solo le
  regole di piattaforma già misurate in R4 — e la **revisione di testa non è mai purgata**, quindi la
  versione corrente, l'unica che i documenti servono, è al sicuro.
- ✅ **Accesso**: c'è, con l'**account IVAO del committente** (dominio `ivao.aero`). Basta per la v1, dove il
  caricamento è **a mano**.

⚠️ Due conseguenze da mettere agli atti, perché non si riscoprano:

1. **L'accesso è di una persona, non del sito.** Nella v1 non è un problema — il sito non tocca Drive, tiene
   solo l'ID. Ma il giorno che si fa il caricamento via API (fuori perimetro v1) **non** si usa quell'account:
   serve un **service account** aggiunto come membro del drive condiviso, altrimenti i caricamenti muoiono col
   cambio d'incarico di una persona sola.
2. ✅ **Confermato: cartella dentro un *Drive condiviso*.** I byte appartengono quindi
   all'**organizzazione**, non a una persona: sopravvivono a qualunque cambio d'incarico, e non c'è niente da
   spostare prima della slice 1. È anche la precondizione del caricamento via API: un service account si
   aggiunge come **membro** di un Drive condiviso, cosa che in un «Mio Drive» non si può fare.
