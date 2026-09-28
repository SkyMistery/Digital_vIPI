# Revisione totale del codice, terzo giro — 26-27 settembre 2026

**Commit:** `e24557e` (1.46.5, in produzione; `sito/lavori` @ `8247e1ea` ha lo stesso `src/`) · **Stato:** registro
chiuso, correzioni da fare per lotti · **256 findings** `U-001`…`U-256` · **0 S1** · 18 S2 · 90 S3 · 148 S4 ·
5 confutati · chiesta dal committente il 26-set sera, filone Sito (voce S9 di `docs/filoni/sito.md`).

Terzo giro integrale, dopo quelli del 6 e del 13 settembre (`audit-2026-09-06-revisione-totale.md`,
`audit-2026-09-13-revisione-totale-2.md`). Fra il secondo giro (1.25.2) e questo (1.46.5) sono passati 388 commit
mai riletti da una revisione intera. Diciassette dimensioni lette in parallelo da agenti, ciascuna passata a un
verificatore avversariale; in più la base misurata, le prove dal vivo su due copie del DB di produzione e due
richieste esplicite del committente: **che cosa può rompere il sito un utente** (dimensione d15 e prove di doppio
clic) e **dove i documenti non sono allineati fuori dal design** (dimensione d16, §7).

Perimetro: Host, Hosting, Ui, Application, Infrastructure (+ MySqlMigrations), Domain, la libreria Sectorfile come
ingresso del sito, l'endpoint `transfers/resolve`. Fuori: Sector Lab, AuroraBridge, AuroraProfiles.

> **Come è stato condotto, e dove è debole.** Il workflow si è fermato tre volte sul limite di sessione; i risultati
> sono stati recuperati da file e il lavoro è stato completato a pezzi. Sei dimensioni (d04, d10, d11, d12, d14,
> d16) **non hanno avuto il verificatore**: i loro findings sono marcati **NV**, tranne gli S2, che la chat
> principale ha riletto sul codice o sui dati a mano. Tre dimensioni (d09b, d13, d15) sono state lette due volte:
> le due letture sono fuse (ids con suffisso `-g1` = primo giro). La deduplica fra dimensioni è fatta a mano sugli
> S2 e sui doppioni noti; fra gli S3/S4 possono restare due righe per lo stesso difetto.

---

## 1. La risposta che conta

**Nessun S1.** Nessuno, da fuori, può oggi scalare di livello, leggere pagine riservate o scrivere: il giro delle 58
rotte con quattro identità (anonimo, senza livello, Editor, Admin) regge, la copia del DB è chiusa agli altri,
il ponte RFO senza chiave risponde 401, in produzione i file sensibili danno 404/403.

**Una cosa ha una scadenza: il 1° ottobre alle 00:00Z (U-009).** La release programmata **#187 di LIBV_APP**
(Gioia Approach), creata il 9 settembre per il ciclo 2610, entra in vigore e **prende il posto della #423** del
25 settembre: `GetEffectiveAsync` ordina per data efficace prima che per versione. La pagina pubblica tornerebbe al
9 settembre (spariscono «Gestione del traffico» con l'IFR e «Tecnica operativa», il VFR torna radice). È l'unica
release programmata nella copia. **Prima del 1° ottobre, dal pannello Versioni di LIBV_APP: annullare la #187 o
ripubblicare al 2610.** È un gesto da fare in produzione, non codice.

Gli altri S2 sono di tre famiglie.

- **Lavoro che si perde o torna indietro in silenzio**, tutti riprodotti dal vivo sulla copia:
  - U-006: annullare l'unica release e riavviare ⇒ il sito ripubblica da solo **la bozza**, firmata «sistema» (Passenger riavvia più volte al giorno).
  - U-011: due schede o un lock appena preso ⇒ la seconda scrittura riporta indietro la prima (Trasferimenti: FL150 → FL140).
  - U-010: una riga incompleta in una tabella dell'aeroporto ⇒ le correzioni valide alle altre righe non si salvano, e «Fine modifica» le mostra come salvate.
  - U-016: un testo incollato oltre il tetto SignalR ⇒ non si salva, nessun avviso.
  - U-013, U-014: le passate d'avvio cancellano i blocchi di «Validità e revisione» e lasciano una profondità incoerente che fa fallire «Crea bozza» (Perugia Approach).
- **La pagina che muore per un doppio clic**, riprodotti col doppio clic vero del mouse: U-012 Struttura (clic su un nodo), U-017 «Pubblica ora» (e la pubblicazione non avviene).
- **Dati operativi sbagliati mostrati al controllore o al pubblico**: U-015 vista rapida (TA «N/A», SID «10000 ft» dove il documento dice FL100); U-003/U-004/U-005 procedure che spariscono o si scambiano le decisioni fra import (14 chiavi doppie vere: XIB5A-OKU6A manca oggi da LIRF); U-007/U-008 unioni che nascondono sezioni non comuni o le lasciano nascoste dopo lo scioglimento; U-002 un import IVAO che timbra riuscito un elenco vuoto (gemello di T-006, NV).

Due S2 non sono difetti di codice ma di configurazione o di porta aperta: **U-018**, l'archivio API è ancora aperto
a chiunque in produzione (T-017, `Api:RichiediChiave` mai acceso: 46 522 sessioni con VID); **U-001**, il Profile
Swapper è anonimo e il suo confronto alloca memoria quadratica senza tetto complessivo (esaurimento non provato di
proposito).

### Bilancio

| Gravità | Totale | C-vivo | C | P | A | NV |
|---|---|---|---|---|---|---|
| S1 | 0 | – | – | – | – | – |
| **S2** | 18 | 9 | 7 | 0 | 1 | 1 |
| **S3** | 90 | vedi §3 | | | | |
| **S4** | 148 | vedi §3 | | | | |

Legenda V: **C-vivo** riprodotto dal vivo · **C** confermato su codice o dati · **P** plausibile · **A** aggiunto dal
verificatore · **NV** dimensione senza verificatore.

---

## 2. Base misurata e prove dal vivo

### Base (`e24557e`)

| Controllo | Esito |
|---|---|
| Build Release `--no-incremental` di `Vipi.slnx` | 0 avvisi, 0 errori |
| Test Release | **14 874 verdi**, 0 falliti, 0 saltati; `conta-test.sh`: conteggi identici all'atteso (18 righe progetto×TFM) |
| Pacchetti vulnerabili (anche transitivi) | nessuno |
| Deprecati | solo xunit 2.9.3 (R-004, noto) |
| Modello/migrazioni | coperti da `SqliteMigrationsTests` e `MySqlMigrationsTests`, verdi |
| **`sql_mode` di produzione** | la Diagnostica di produzione mostra «Server 0»: `ServerSettingsAnalyzer` (ServerSettings.cs:70/87) non segnala, quindi c'è `STRICT_TRANS_TABLES` e `max_allowed_packet` ≥ 4 MiB. **T-053/T-054: in produzione un valore troppo lungo fallisce, non tronca** |

### Come sono state fatte le prove

- Copia del DB scaricata dalla Diagnostica il 26-set 16:29Z (1.46.5, 65 tabelle, 109 633 righe), verificata INTERA
  con `tools/Vipi.DbBackup`, ripristinata due volte sul MariaDB 11.4 locale: una copia in sola lettura per gli
  agenti, una copia di scrittura per le prove che scrivono. Nessun dato della copia entra nel repository.
- La 1.46.5 pubblicata in Release net10, fino a cinque istanze (Admin fondatore, secondo Admin, Editor, senza
  livello, anonimo in Production), **tutte le uscite spente**: IVAO (anche il tracker pubblico, che il polling leggeva
  senza credenziali), GitHub, traduzione.
- Produzione: **solo lettura** (regola §5): la Diagnostica vista da Admin, poche GET/HEAD anonime. Nessun gesto che
  scrive, nessun «Start editing», nessuno scarico del DB.
- Le prove XSS per iniezione diretta nel DB **non sono state fatte**: l'XSS resta alla lettura del codice (d02, d12).

### Esiti principali

- **Accessi**: 58 rotte × 4 identità. Pagine riservate chiuse ad anonimo e senza livello; `database-backup` 404
  fuori dall'Admin; ponte RFO senza chiave 401; anteprime `?as=draft` da anonimo = pagina pubblica. L'Editor vede
  per intero alcune pagine admin (ACC, aeroporti, spazi aerei, struttura, navaid, allegati, confinanti,
  trasferimenti, versioni): la matrice delle scritture (allegato A) dice che le scritture restano cancellate nel servizio.
- **Lock conteso**: l'Editor non ruba il lock dell'Admin (UPDATE condizionato); resta il difetto di UI U-256.
- **Riprodotti**: U-006, U-009 (sui dati), U-004 (sui dati), U-010, U-011, U-012, U-015, U-016, U-017, U-018, U-051
  (vLOA senza lock), U-108 (pannello traduzioni, con clic nello stesso istante).
- **Smentiti dal vivo**: il CTR di Brindisi «troncato» (le forme salvate hanno i 52 vertici) → §5.
- **Non riprodotto**: il ciclo dell'editor unito col lock di un collega (U-052): 0 tentativi in 20 s.
- **Doppio clic su 16 pagine staff**: crollano Struttura, «Pubblica ora» e il pannello Traduzioni; reggono Versioni,
  Glossario, Audit, Navaid, Spazi aerei, Confinanti, ACC, Aeroporti, Trasferimenti, Da sistemare, Diagnostica,
  editor MIL, statistiche.
- **Design su telefono e tablet** (375, 768, 1024 px, misurato dal DOM): nessuna pagina pubblica sfora tranne le
  statistiche (U-107); ma sotto i 900 px **le tabelle spezzano a metà frequenze e callsign** invece di scorrere
  (U-106: 149 parole spezzate sulla vIPI LIBB a 375 px, 84 nella tabella SID di LIBD a 768). Più tour in italiano
  con UI inglese, bersagli al tocco piccoli, colonna delle versioni che sfora negli editor a 768 (U-251…U-255).

---

## 3. Registro

Per gli S1/S2 la scheda intera; per S3/S4 una riga (meccanismo, scenario e prova stanno in `revisione-totale-3/registro.json`).

### S1 (0)

Nessuno.


### S2 (18)

#### U-001 · ProfileSwapper anonimo: con file .cpr costruiti si esauriscono memoria e CPU dell'unico processo

- **G** S2 · **V** C · **Mig.** no · **Dove** `src/Vipi.Ui/Pages/ProfileSwapperPage.razor:374` · **Da** d01-01 (d01-identita-accesso), d15-01-g1 (d15-abuso-robustezza-giro1)
- **Meccanismo.** La pagina `/services/profile-swapper` è InteractiveServer e non ha nessun cancello: la apre anche l'anonimo. `LoadDests` accetta fino a 50 file per selezione (`GetMultipleFiles(maximumFileCount: 50)`, riga 374), ciascuno fino a 4 MB (`OpenReadStream(MaxByteFile)`), e `_dests` si accumula a ogni nuova selezione senza tetto complessivo. `CprProfile.Load` spezza il testo con `SplitKeepEnds`, che tratta ogni '\r' come fine riga: un file di 4 MB fatto solo di '\r' diventa 4 milioni di stringhe da un…
- **Scenario.** Un anonimo apre /services/profile-swapper e carica come sorgente un file da 40 kB («[A]» più 20.000 righe «x»). Come destinazioni carica 5-10 copie di un file da 40 kB con 20.000 righe «y», poi spunta A. Ogni render (spunta, tasto nel filtro) alloca 1,6 GB per destinazione e macina 4·10^8 confronti ciascuna. Il processo unico viene ucciso dall'hosting o va in OOM: cadono tutti i circuiti, editor …
- **Dal vivo.** Meccanismo confermato nel codice (LineDiff.cs:26); /services/profile-swapper 200 da anonimo; esaurimento NON provato di proposito.
- **Prova rossa.** Test xUnit in tests/Vipi.Ui.Tests (InternalsVisibleTo presente, Vipi.Ui.csproj:48), nome `LineDiff_non_cresce_col_quadrato_delle_righe`. Due array da 5.000 righe distinte per lato; misura `GC.GetAllocatedBytesForCurrentThread()` prima e dopo `LineDiff.Diff`; asserzione: meno di 10 MB. Oggi ne alloc…
- **Correzione.** Mettere un cancello (almeno utente loggato) e comunque dei tetti lato server: numero totale di destinazioni (es. 10) e dimensione (es. 512 kB, un .cpr reale sta sotto i 60 kB), numero massimo di righe per profilo e per sezione; il diff si calcola un…

#### U-002 · Gemello di T-006 non coperto: postazioni d'aeroporto (e subcenter di un ACC) che rispondono 401/403/429/5xx diventano «elenco vuoto», il giro dei settori timbra RIUSCITO e dopo due notti cade la D8 dell'eliminazione

- **G** S2 · **V** NV · **Mig.** no · **Dove** `src/Vipi.Infrastructure/Ivao/IvaoAirportDetailClient.cs:29` · **Da** d04-01 (d04-import-ivao)
- **Meccanismo.** La correzione di T-006 fa risalire le ECCEZIONI dei giri, ma i client anagrafici non ne sollevano: IvaoHttp.GetJsonAsync/GetStringAsync trasformano 401, 403, 429 (dopo i 3 tentativi) e 5xx in null, e GetAtcPositionsAsync/GetSubcentersAsync in un elenco vuoto. AirportSectorImporter esce con (0,0) senza timbrare ImportedAtUtc; AirportSectorImportHostedService arriva in fondo e ritorna true, GatedImportLoop timbra AirportSector riuscito (LastError azzerato, PrevSuccessUtc scorre). Dopo due giri ogn
- **Scenario.** Il token perde lo scope configuration, oppure IVAO revoca il token in corso di validità (401 fino alla scadenza dichiarata: IvaoTokenProvider non lo butta su 401), oppure /v2/airports/{icao}/ATCPositions risponde 500 per due notti. Il giro ACC fallisce (rosso, /v2/centers solleva), ma il giro Settori resta VERDE in Sorgenti e timbra ogni notte. Dopo la seconda notte un Admin che apre l'eliminazion
- **Prova rossa.** Test unitario: IvaoAirportDetailClient con HttpMessageHandler finto che risponde 403 su ATCPositions → oggi GetAtcPositionsAsync ritorna lista vuota invece di sollevare. Test d'integrazione (SQLite): un AirportSector con ImportedAtUtc = T0; due esecuzioni di ImportOnceAsync di AirportSectorImportHos
- **Correzione.** Nei client distinguere 404 (vuoto legittimo) da ogni altro non-2xx: sollevare HttpRequestException con lo status per le LISTE (ATCPositions, subcenter, runways). Nel giro dei settori contare gli aeroporti la cui lista non si è letta e non timbrare la

#### U-003 · Fra il changelog del ciclo nuovo e la sua entrata in vigore, una procedura rivista sparisce dalla tabella SID/STAR: la vecchia è cancellata, la nuova aspetta il ciclo

- **G** S2 · **V** C · **Mig.** sì · **Dove** `src/Vipi.Infrastructure/Persistence/EfAirportRepository.cs:357` · **Da** d05-02 (d05-sectorfile-procedure)
- **Meccanismo.** ReplaceImportedProceduresAsync cancella tutte le importate del verso. La riga rivista, con contenuto o chiave nuova, prende il ciclo DICHIARATO (spesso il prossimo), e IsPublicAt la nasconde fino a quel ciclo. Nessuna copia «in vigore» resta, al contrario delle shape di settore. Una release che capta la sezione Frozen in quei giorni la congela senza la procedura.
- **Scenario.** Già successo il 25-set a LIMF: le TOP1B-* sulla 36 sono state cancellate, le 5 sulla 18 timbrate 2610 e senza WTC/Cat/IC, e la TOP1B LAG2L è sparita. Fino al 1° ottobre il vSOP pubblico di LIMF non ha le TOP1B con transizione. Succede a ogni ciclo, per ogni procedura rivista.
- **Dal vivo.** non eseguito; confermato dai dati della copia (LIMF TOP1B)
- **Prova rossa.** SidImportRepositoryTests: import TOP1B AST8L pista 36 a '2609', reimport sulla 18 a '2610', DeriveAsync('LIRF', Sid, atCycle '2609') → Assert.NotEmpty (oggi vuoto). SQL: le TOP1B % di LIMF sono solo 2610.
- **Correzione.** Come per le shape: quando una riga cambia con un ciclo d'entrata futuro, tenere la versione in vigore e mostrarla ai cicli precedenti; le righe tolte dalla sorgente restano fino al ciclo dichiarato.

#### U-004 · Procedure diverse con la stessa StableKey (XIB5A-OKU5R/OKU6A a LIRF, ILF1x-VOG1K/VOG1S a LIME, ROBO1H/ROBO5H a LIBG): la seconda viene ritimbrata a ogni giro ed eredita le decisioni della prima

- **G** S2 · **V** C-vivo · **Mig.** no · **Dove** `src/Vipi.Infrastructure/Persistence/EfAirportRepository.cs:357` · **Da** d05-03 (d05-sectorfile-procedure)
- **Meccanismo.** La prior è first-wins per chiave. ContentUnchanged confronta il nome della seconda riga con quello della prima: è sempre falso, e la seconda prende il ciclo appena calcolato a ogni giro. Col ciclo dichiarato avanti resta fuori dalla pubblica. L'eredità delle decisioni (WTC, IC, forzatura, nascosta) è voluta e ha un test, ma la premessa «revisioni della stessa SID» non vale per queste coppie. Il ritimbro non ha nessun test.
- **Scenario.** Oggi le 14 seconde righe sono 2610 e le prime 2608. Sulla pagina pubblica di LIRF manca XIB5A-OKU6A (e le altre *6A), a LIME le VOG1S, a LIBG ROBO5H. Si ripete per circa 10-12 giorni a ogni ciclo. Forzare solo la seconda riga viene disfatto la notte dopo.
- **Dal vivo.** ✅ sui dati: 14 chiavi doppie; XIB5A-OKU6A assente dalla pagina pubblica di LIRF.
- **Prova rossa.** SidImportRepositoryTests.Reimport_Sopravvive_A_Due_Revisioni_Con_La_Stessa_StableKey: dopo il secondo import a '2607', Assert.Equal('2606', ROBO2H.SourceAiracCycle) → oggi '2607'.
- **Correzione.** Riagganciare per (StableKey, Name) prima di ripiegare sulla sola chiave; mai first-wins sul ciclo e sulle decisioni per riga.

#### U-005 · La StableKey contiene il fix RISOLTO: creare o togliere un alias, un catalogo che cambia, una riga assente per un giro fanno perdere priorità, forzatura, «nascosta», correzioni, Cat/WTC/IC e ritimbrano il ciclo

- **G** S2 · **V** C · **Mig.** sì · **Dove** `src/Vipi.Infrastructure/Sectorfile/AuroraSectorfileParser.cs:224` · **Da** d05-05 (d05-sectorfile-procedure)
- **Meccanismo.** La chiave è ICAO\|fix risolto\|lettera\|transition\|pista, e il fix risolto dipende da catalogo e alias. Il riaggancio del lavoro editoriale avviene solo per chiave: se la chiave cambia, la riga rinasce nuda e col ciclo nuovo. Sulla copia 356 SID e 86 STAR risolte a mano hanno la chiave sul prefisso grezzo, e 1165 SID hanno WTC e 1093 IC. Due commenti del codice promettono il contrario.
- **Scenario.** L'editor di LIPI corregge ROSK1C («da verificare») in ROSKA e spunta «alias», come la pagina propone. La notte dopo le 14 ROSK di LIPQ/LIPZ perdono WTC e IC 4000 e restano fuori dalla pubblica fino al 1° ottobre. Stessa sorte per PIMO, KAPP, MARE. Una riga malformata per un giro (TOP1B LAG2L) cancella gli arricchimenti.
- **Dal vivo.** non eseguito; innesco verificato sui dati (ROSK a LIPI da verificare, 14 ROSK arricchite a LIPQ/LIPZ)
- **Prova rossa.** Test del parser: ParseSids SOSA5A con catalogo {SOSAK,SOSAM} senza e con alias SOSA→SOSAK: Assert.Equal delle StableKey (oggi diverse). Repository: import con chiave grezza, UpdateImportedSidAsync(priority 1), reimport con chiave risolta → Priority null.
- **Correzione.** Chiave indipendente dalla risoluzione (prefisso grezzo del codice) con migrazione che ricalcola le chiavi, o riaggancio in due passi; arricchimenti in una tabella a parte per chiave.

#### U-006 · Il «backfill» d'avvio pubblica da solo, a ogni riavvio, la versione di lavoro (bozza compresa) dei documenti «Published» senza release in vigore

- **G** S2 · **V** C-vivo · **Mig.** no · **Dove** `src/Vipi.Application/Content/ReleaseService.cs:494` · **Da** d07-01 (d07-motore-lock-release)
- **Meccanismo.** BackfillMissingReleasesAsync gira a OGNI avvio (Isolata, fuori dal gate del timbro): per ogni documento Published, non nascosto e senza release effettiva ADESSO crea una release Effective al ciclo corrente, autore 0. Lo snapshot passa da WorkingVersionIdAsync, che sceglie la BOZZA. Nessun controllo di lock, programmate o unioni. CancelReleaseAsync non cambia Document.Status: annullare l'unica release crea proprio l'ingresso del backfill.
- **Scenario.** Editor annulla l'unica release di LIRA (pubblicata per errore) mentre ha una bozza aperta: al primo riavvio di Passenger la pagina torna pubblica con la BOZZA, firmata «sistema». Stesso effetto per un documento con sola release programmata (esce subito al ciclo corrente) e, in produzione, per la vLOA 65 generata e pubblicata dal backfill l'8-set.
- **Dal vivo.** ✅ riprodotto: release 452 «backfill», autore 0, con la bozza «PROVABOZZAU» dopo un riavvio (LIRA).
- **Prova rossa.** ReleaseGenericFlowTests «Backfill_non_ripubblica_un_documento_con_la_release_annullata»: PublishNowAsync, bozza con «PROVA-BOZZA», CancelReleaseAsync, BackfillMissingReleasesAsync → n=0 e GetEffectiveAsync nullo (oggi 1, payload con la bozza).
- **Correzione.** Togliere il backfill dal giro d'avvio (migrazione A fatta a luglio) o metterlo sotto il gate del timbro; se resta: solo documenti senza NESSUNA release, fotografare CurrentVersionId e mai la bozza, saltare lock attivi e programmate.

#### U-007 · «Sezioni in comune»: il confronto per chiave nasconde anche contenuti NON ripetuti fra vIPI civile e vSOP militare (sotto-alberi solo militari, blocchi propri, sezioni editoriali)

- **G** S2 · **V** C · **Mig.** no · **Dove** `src/Vipi.Application/Content/SezioniComuni.cs:101` · **Da** d07-02 (d07-motore-lock-release)
- **Meccanismo.** SezioniComuni.Di dichiara «in comune» ogni chiave di catalogo presente in due documenti a ogni profondità (16 fra Airport e AirportMil, 15 spuntate); Piano mette IsHidden sulla sezione, che in pubblica si porta via il sottoalbero. Nel vSOP «operationaltechnique» ha 6 discendenti solo militari, «runways» ha le soglie, «frequencies» è HB, carte editoriali. L'invitante è il primo membro e DoveNascondere propone tutti gli altri: unendo dal civile si nasconde nel vSOP. La scheda mostra solo i titoli.
- **Scenario.** Editor, dall'editor della vIPI civile in modifica, unisce il vSOP: la scheda si apre con 15 voci spuntate sul vSOP; «Nascondi» → spariscono dal vSOP procedure di partenza/arrivo VFR/IFR (69 blocchi nei 17 vSOP), soglie, carte militari (101 blocchi), blocchi CRC/AEW. Su LIRP la release corrente del vSOP ha già piste+soglie nascoste.
- **Dal vivo.** non provato dal vivo; dati della copia: LIRP vSOP 56 con runways (blocco + soglie) nascoste nella versione pubblicata
- **Prova rossa.** Vipi.Application.Tests «Piano_non_fa_sparire_sottoalberi_che_il_civile_non_ha»: alberi da SectionCatalog Airport/AirportMil, Di()+Piano(tutte, nascondiIn=[mil]) → chiavi invisibili nel militare ⊆ chiavi del civile; oggi include departureprocedures:*, arrivalprocedures:*, runways:thresholds.
- **Correzione.** Non proporre sezioni editoriali e HB; per una comune con figli nascondere solo se il sottoalbero è comune per chiavi, altrimenti solo le figlie comuni; mostrare e contare i discendenti che sparirebbero.

#### U-008 · Sciogliere l'unione (o togliere un membro) lascia nascoste le sezioni «in comune»: la pagina singola esce subito monca, mentre il prompt dice «non si perde niente»

- **G** S2 · **V** C · **Mig.** no · **Dove** `src/Vipi.Infrastructure/Persistence/EfDocumentUnionRepository.cs:104` · **Da** d07-03 (d07-motore-lock-release)
- **Meccanismo.** La scheda delle comuni nasconde con IsHidden e la pubblicazione congiunta congela quelle sezioni nello snapshot di ogni membro. DissolveAsync e RemoveMemberAsync cancellano solo le righe di appartenenza: nessun passo rimette visibili le sezioni nascoste per l'unione, nessun avviso. Il prompt dello scioglimento dice «Non si perde niente di quel che c'è scritto».
- **Scenario.** Nella copia le vIPI civili LIRS (doc 86) e LIRL (doc 96) hanno TUTTE e dieci le sezioni radice nascoste nella release corrente. Un editor scioglie l'unione 7 o 8: da quel momento la pagina pubblica della vIPI LIRS/LIRL è vuota. Le unioni 3-6 risultano già sciolte in passato.
- **Dal vivo.** non provato dal vivo; dati della copia: doc 86 e 96 con tutte le radici nascoste
- **Prova rossa.** Dal vivo sulla copia: Editor apre la vIPI LIRS, «Sciogli l'unione»; anonimo apre la pagina pubblica della vIPI LIRS → nessuna sezione. Test Infrastructure: comuni nascoste nel civile, PublishNowAsync, SciogliAsync → weather deve tornare visibile (oggi IsHidden=true).
- **Correzione.** Allo scioglimento/rimozione rimostrare in bozza le sezioni nascoste dal piano delle comuni (ricordarle: colonna o riga dedicata) e dire che serve ripubblicare; in subordine avviso con l'elenco delle sezioni ancora nascoste.

#### U-009 · Release programmata vecchia sostituisce al rollover quella più recente: il 1-ott la vIPI APP LIBV torna al 9-set, senza avviso

- **G** S2 · **V** A · **Mig.** no · **Dove** `src/Vipi.Application/Content/ImpactDriftUseCase.cs:273` · **Da** d08-16-v (d08-derivazioni), d16-01 (d16-allineamento-documenti)
- **Meccanismo.** RecomputeStatuses sceglie la vincitrice di ogni ciclo e mette in vigore quella con la data più recente già passata; GetEffectiveAsync ordina per data. PublishNowAsync non tocca le programmate. Una programmata al ciclo entrante fatta PRIMA di pubblicazioni immediate più recenti le sostituisce al rollover. La deriva (ValutaAsync:273) confronta il ciclo entrante con la release in vigore, non con la programmata che lo diventerà.
- **Scenario.** Copia del 26-set: LIBV_APP ha la 187 (v2, al 2610, in vigore dal 1-ott 00:00Z, creata il 9-set) e la 423 (v10, 25-set, in vigore). La query di GetEffectiveAsync al 1-ott restituisce la 187: il pubblico perde i Coordinamenti (4936→77 byte), i link a LIBB_MIL, Barca, Pioppo e Legion, le Separazioni e «Coordinamenti per GCA». Nessuna voce in «Da fare».
- **Dal vivo.** non provato a schermo; confermato con la query sulla copia vipi_rev3 (risultato: Id 187)
- **Prova rossa.** Test Infrastructure «Pubblica_ora_dopo_una_programmata_resta_in_vigore_al_rollover»: SaveRelease 2610 a +4 giorni, poi 2609 subito; GetEffectiveAsync(+5 giorni) deve dare la seconda (oggi dà la prima). SQL sulla copia: già Id 187.
- **Correzione.** Subito, in produzione: annullare la 187 o ripubblicare LIBV_APP al 2610 prima del 1-ott. Nel codice: «Pubblica ora» chiede cosa fare delle programmate più vecchie, e la deriva al ciclo entrante confronta con la programmata.

#### U-010 · Una riga incompleta blocca il salvataggio dell'INTERA tabella (livelli, piste, regole, SID/STAR manuali): le modifiche valide alle altre righe si perdono in silenzio all'uscita o al ricarico

- **G** S2 · **V** C-vivo · **Mig.** no · **Dove** `src/Vipi.Ui/Components/App/AirportEditModels.cs:565` · **Da** d09a-02 (d09a-editor-documenti)
- **Meccanismo.** SaveTls/SaveRwys/SaveRules/SalvaManuali (AirportSectionsEditor 771-810) salvano solo se AirportSaveGate (AirportEditModels:565-579) trova complete TUTTE le righe; il servizio sostituisce la tabella intera. Con una riga a metà nessun gesto arriva al DB. «Fine modifica» (603) molla il lock senza chiedere, la vista in sola lettura mostra i buffer come salvati, e ogni ricarico (638-652) li butta.
- **Scenario.** Editor su LIRA, Quote di transizione: «+ riga» con il solo QNH, poi corregge FL80→FL90 su un'altra riga e preme «Fine modifica»: vede FL90 e la riga nuova; al ricarico FL80 e la riga sparita, nessun avviso. Stesso copione con una regola piste o una SID manuale a metà.
- **Dal vivo.** ✅ riprodotto: FL90 perso in silenzio dopo «Finish editing» (LIRA).
- **Prova rossa.** Unitario: AirportSaveGate.Tls([riga completa modificata, riga nuova senza Level]) == false. Rossa vera, bUnit su AirportSectionsEditor con IAirportEditingService finto: riga incompleta + Level cambiato + FineModifica → Assert salvataggio della correzione o rifiuto con avviso (oggi zero chiamate).
- **Correzione.** Salvare subito le righe complete escludendo solo le NUOVE incomplete; oppure FineModifica e i ricarichi rifiutano/chiedono quando un cancello è chiuso, e il cartello dice «la tabella non si salva finché…».

#### U-011 · Prendere il lock non rilegge i dati: la prima scrittura riporta indietro il lavoro fatto nel frattempo (anche dallo stesso admin in un'altra scheda)

- **G** S2 · **V** C-vivo · **Mig.** no · **Dove** `src/Vipi.Ui/Pages/AdminTrasferimentiPage.razor:528` · **Da** d09b-01 (d09b-pagine-admin)
- **Meccanismo.** OnLockChanged fa solo _canEdit=mine: i dati a schermo restano la fotografia dell'apertura. Cella, aeroporti e verso rimandano la clausola o la sezione INTERA (EditableCopy). UpdateClauseAsync non ha token di versione e propaga i Cops vecchi a tutte le varianti del gruppo. Il lock è per uid (due schede passano) e ForceUnlock è concesso a ogni Editor. Stesso schema per i ripieghi in Struttura (ReplaceAsync) e per il campo gemello dei limiti in ACC.
- **Scenario.** A lascia Trasferimenti aperto in sola lettura; B prende il lock (o lo forza), corregge punti e condizione della clausola X, «Fine modifica». A preme «Inizia modifica» e cambia il livello di X in cella: il lavoro di B sparisce in silenzio, e i punti vecchi tornano anche sulle varianti del gruppo. Stesso esito con un solo Admin e due schede.
- **Dal vivo.** ✅ riprodotto: seconda scheda ha riportato LevelValue 150 → 140 (clausola 2, LIBB).
- **Prova rossa.** Già rossa dal vivo (quaderno §d09b-01). Test: tests/Vipi.Ui.Tests AdminTrasferimentiPageTests.PresaDelLock_RileggeGliAccordi — service finto che conta le letture, LockChanged(true), asserire una seconda lettura prima di riaccendere le celle.
- **Correzione.** Ricaricare al passaggio a mine=true prima di riabilitare i comandi; scritture per campo (solo il campo toccato) o token di versione confrontato dal repository; lock per circuito invece che per utente.

#### U-012 · Struttura: un clic su un nodo mentre un'altra operazione è in volo fa cadere il circuito (o lascia la proiezione vecchia)

- **G** S2 · **V** C-vivo · **Mig.** no · **Dove** `src/Vipi.Ui/Pages/StrutturaPage.razor:1065` · **Da** d09b-02 (d09b-pagine-admin), d11-01 (d11-concorrenza-circuito)
- **Meccanismo.** Servizi presi dal circuito, tutti sullo stesso VipiDbContext scoped. Select (riga dell'albero) non guarda _busy e non ha catch; Guarded non ha sentinella; OnDrop non guarda _busy. SetParentAsync salva e poi riproietta (sei tabelle intere): la finestra è di centinaia di ms. Nell'app non c'è ErrorBoundary. Se parte per seconda la query di Select, l'IOE chiude il circuito; se parte per seconda la riproiezione, Guarded la mostra ma il padre resta salvato con la proiezione vecchia.
- **Scenario.** Un Editor col lock trascina un APP sotto un nuovo padre e subito clicca un altro nodo per controllarlo: la pagina si stacca («A second operation…»), oppure compare il messaggio e i Sector proiettati restano vecchi fino alla riproiezione successiva.
- **Dal vivo.** ✅ riprodotto: un VERO doppio clic su un nodo dell'albero fa cadere la pagina (ObjectDisposedException, StrutturaPage.razor:1069/1115).
- **Prova rossa.** bUnit: IHierarchyEditingService finto che attende un TaskCompletionSource in SetParentAsync; ISectorFallbackService finto che lancia IOE se chiamato in parallelo. Trascinare, cliccare un'altra riga, asserire nessuna Renderer.UnhandledException (oggi rosso).
- **Correzione.** Fila di pagina (InFilaAsync) per Select, CaricaRipieghi, Proponi e Guarded; sentinella in Guarded; righe inerti mentre _busy; oppure ScopeProprioCheAspetta.

#### U-013 · A ogni consegna la manutenzione d'avvio cancella i blocchi scritti a mano in «Validità e revisione» delle vIPI d'aeroporto

- **G** S2 · **V** C · **Mig.** no · **Dove** `src/Vipi.Infrastructure/Persistence/EfDocumentMaintenance.cs:1229` · **Da** d10-01 (d10-dati)
- **Meccanismo.** ReconcileCookedSections, passo 2, toglie tutti i blocchi di ogni radice per cui SectionCatalog.IsHostRendered(Airport, chiave) è vero. Il passo è del 26-ago; il giorno dopo (8b749af6) IsHostRendered è stato allargato a Host OR HostAndBlocks per far nascere «validity» con scheda + blocchi editoriali, e questo consumatore non è stato aggiornato. Per il profilo Airport l'unica HB è «validity»: i suoi blocchi propri (quelli che l'editor fa aggiungere) vengono rimossi dalla versione più recente (bozz
- **Scenario.** Un Editor apre la vIPI di LIRF, sotto «Validità e revisione» aggiunge un blocco di prosa (es. storico revisioni, firmatari: nelle vIPI ACC succede davvero, vIPI Brindisi ha due blocchi di prosa lì). Salva, magari pubblica. Al pacchetto successivo (1.46.6) il primo avvio cancella il blocco dalla versione di lavoro; la release in vigore lo mostra ancora, ma la pubblicazione seguente (fatta da una bo
- **Dal vivo.** EfDocumentMaintenance.cs:1229-1235 rimuove i blocchi se IsHostRendered, che dal 27-ago comprende HostAndBlocks («validity»). Oggi latente: nessuna validity d'aeroporto ha blocchi.
- **Prova rossa.** Test (SQLite in memoria): DocumentBirth.Crea con SectionProfile.Airport, aggiungere un ContentBlock Prose alla sezione «validity», chiamare EfDocumentMaintenance.ReconcileAirportSectionKeysAsync, asserire che il blocco esiste ancora (oggi sparisce). Misura sulla copia: nessuna sezione validity d'aer
- **Correzione.** Nel passo 2 usare «corpo reso solo dalla pagina»: IsHostRendered && !KeepsOwnBlocks (o BodySource == Host). Aggiungere il test sopra. Controllare anche DocumentBirth.Semina, che per lo stesso allargamento semina un blocco tabella vuoto nelle sezioni

#### U-014 · I riparenti d'avvio non aggiornano la Depth dei discendenti: dopo la pubblicazione «Crea bozza» esplode (Perugia Approach già colpito)

- **G** S2 · **V** C · **Mig.** no · **Dove** `src/Vipi.Infrastructure/Persistence/EfDocumentMaintenance.cs:663` · **Da** d10-02 (d10-dati), d16-02 (d16-allineamento-documenti)
- **Meccanismo.** ReparentAppTrafficManagementAsync e RiparentaVfrDeiBlocchiAppAccAsync spostano il VFR sotto «Gestione del traffico» impostando vfr.Depth = padre.Depth + 1, ma le sotto-sezioni del VFR restano con la Depth vecchia (uguale a quella del VFR spostato). Lo spostamento fatto dall'editor (EfEditingRepository ~1003/1101) ricalcola la profondità del sottoalbero; le passate d'avvio no. CreateDraftAsync copia le sezioni in ordine Depth, Order e cerca il padre in un dizionario riempito man mano: una figlia
- **Scenario.** Sulla copia di produzione: documento 80 «Perugia Approach», versione 242 (bozza v1): la sezione 5720 «Note» (figlia del VFR 5709) ha Depth 1 come il VFR, e Order 1 < Order 2 del VFR. Oggi l'editor riusa la bozza e non se ne accorge. Il giorno che qualcuno pubblica Perugia Approach e poi apre di nuovo l'editor (CreateDraftAsync dalla pubblicata), la creazione della bozza fallisce con «The given key
- **Dal vivo.** Stessa riga 5720 misurata sulla copia.
- **Prova rossa.** Misura: SELECT sulle sezioni con Depth <> Depth del padre + 1 → una riga (5720). Test: documento APP con VFR radice e una sotto-sezione; ReparentAppTrafficManagementAsync; PublishAsync; CreateDraftAsync → KeyNotFoundException. Dal vivo sulla copia locale: scenario «Perugia».
- **Correzione.** Nelle quattro passate ricalcolare la Depth di tutto il sottoalbero spostato (riusare la routine dell'editor). Una passata d'avvio fuori gate che riallinea Depth = padre.Depth+1 sistema la riga già guasta. In CreateDraftAsync ordinare per albero (padr

#### U-015 · Vista rapida aeroporto: TA sempre «N/A», TL sempre «—» e initial climb sopra la TA scritta in piedi invece che in FL

- **G** S2 · **V** C-vivo · **Mig.** no · **Dove** `src/Vipi.Ui/Components/App/AirportQuickPanel.razor:256` · **Da** d13-06-g1 (d13-dominio-aeronautico-giro1)
- **Meccanismo.** ParseTransition cerca la TA (regex sulla prosa) e le righe TL (tabella JSON) nei BLOCCHI della sezione «transi…» del documento pubblicato. Dalla carta 2026-08-26 «transition» è una sezione Host, col corpo dalla pagina e i dati in Derived/FrozenSections, e nello snapshot non ha blocchi. Così _ta resta null e _tlRows vuota: TA «N/A», TL «—», e InitialClimb(raw, null) scrive sempre «N ft». La causa non è ForView, che qui non viene chiamata: è lo snapshot stesso.
- **Scenario.** Sulla copia hanno blocchi nella sezione transition 0 release effettive su 45 Airport e 0 su 17 AirportMil. Una TWR apre la sua vista live, dove il pannello è aperto di base, e legge TA N/A e TL — sul proprio scalo. A LIBR (TA 5000) 16 SID con initial climb 8000 o 10000 escono come «10000 ft», mentre il documento scrive FL100. Idem LICR 17, LIPB 26, LICD 12.
- **Dal vivo.** ✅ riprodotto: vista rapida LIBR_TWR «TA N/A», «TL —», DOLO6J «10000 ft» contro FL100 del documento.
- **Prova rossa.** bUnit su AirportQuickPanel: sezione transition senza blocchi, come in tutte le release vere, e TA 5000 nel profilo. Oggi TA «N/A», atteso «5000 ft». Dal vivo: Admin su /services/vsop/live/libr_twr.
- **Correzione.** Leggere TA e righe TL dalla fonte del documento (AirportTransitionView derivata o congelata, come AirportTransition). Calcolare il TL con la funzione del vAWOS, senza ripiegare sulla prima riga.

#### U-016 · Tetto SignalR lasciato al default: oltre ~16 KB (il valore viaggia due volte) un campo stacca il circuito e il testo non arriva al server

- **G** S2 · **V** C-vivo · **Mig.** no · **Dove** `src/Vipi.Host/VipiStartup.cs:118` · **Da** d15-01 (d15-abuso-robustezza), d12-02 (d12-pagine-js-css-lingue)
- **Meccanismo.** AddHubOptions (VipiStartup.cs:118-136) non imposta MaximumReceiveMessageSize: resta il default di 32 KB. Il Lab lo alza (ServerDelLab.cs:64, trappola già misurata). In blazor.web.js 10.0.12 ogni evento di input/textarea spedisce il valore DUE volte (eventFieldInfo.fieldValue e ChangeEventArgs.value): la soglia reale è ~16 KB di testo. Oltre, il server chiude la connessione e l'evento è perso: il DOM mostra il testo, il server no.
- **Scenario.** Un Admin in Confinanti incolla il poligono di LAAA, DTTC, LYBA o LSAM (16,5-31 KB, 4 coppie su 33). Al primo oninput il circuito si stacca e si riaggancia; il conteggio resta a 0, salvare resta spento, ogni tasto ristacca. Stesso esito per import tabella da PDF e convertitore. Prosa oltre ~16 KB: mai salvata, sparisce a «Fine modifica» (massimo reale oggi 5,4 KB).
- **Dal vivo.** ✅ riprodotto: 44 KB incollati in un campo di prosa, nessun errore, niente salvato, testo perso al ricarico.
- **Prova rossa.** Vipi.E2E.Tests (VipiAppFactory): risolvere via riflessione IOptions<HubOptions<ComponentHub>> e asserire MaximumReceiveMessageSize >= 256 KB (oggi 32768). Dal vivo, Admin 5199 su /services/vsop/admin/neighbours: il poligono LAAA (16 561 B) stacca il circuito, LFXV (8 931 B) passa.
- **Correzione.** In AddHubOptions impostare MaximumReceiveMessageSize a 512 KB, come il Lab, con un test che lo fissi. Aggiungere maxlength ai campi. Per le caselle d'incolla valutare lo streaming JS→.NET.

#### U-017 · ReleasePanel: doppio clic o due schede su «Pubblica ora» possono far cadere il circuito (eccezioni non catturate)

- **G** S2 · **V** C-vivo · **Mig.** no · **Dove** `src/Vipi.Ui/Components/ReleasePanel.razor:630` · **Da** d15-10-g1 (d15-abuso-robustezza-giro1)
- **Meccanismo.** Run alza _busy senza controllarlo in testa: la sola difesa è disabled, che arriva dopo un giro di rete. Releases è @inject (DbContext del circuito). Un secondo clic in volo → «A second operation was started» (InvalidOperationException) sullo stesso context; due schede → urto sull'indice unico (DbUpdateException). Run cattura solo EditNotAllowedException/ValidationException, quindi entrambe escono dal gestore e, senza ErrorBoundary, il circuito cade.
- **Scenario.** Un Editor su rete lenta fa doppio clic su «Pubblica ora», o due editor dell'unione pubblicano insieme: barra rossa, pagina da ricaricare, pubblicazione in volo eventualmente annullata col circuito.
- **Dal vivo.** ✅ riprodotto: un VERO doppio clic su «Publish now» fa cadere la pagina e la pubblicazione non avviene (EfUnitOfWork ← ReleasePanel.razor:605).
- **Prova rossa.** bUnit su ReleasePanel: due invocazioni ravvicinate di PublishNow devono restare gestite (nessuna eccezione fuori dal componente). Fix: `if(_busy) return;` in testa a Run, catch generale di DbUpdateException/InvalidOperationException, Releases da uno scope proprio.
- **Correzione.** Guardia di rientro + catch generale con messaggio (come DocumentEditorShell) + scope proprio per Releases.

#### U-018 · L'archivio /vsop/api/v1/atc/sessions risponde a chiunque, anche in produzione: 46 522 sessioni con VID, callsign e orari

- **G** S2 · **V** C-vivo · **Mig.** no · **Dove** `src/Vipi.Hosting/VipiModuleExtensions.cs:459` · **Da** v-01 (dal-vivo)
- **Meccanismo.** Api:RichiediChiave è ancora false: l'archivio accetta chi non porta chiave (T-017). La regola del committente dice che le API non sono mai anonime.
- **Scenario.** Una GET anonima in produzione restituisce 200, 44 KB, total 46 522.
- **Dal vivo.** ✅ locale e produzione
- **Prova rossa.** GET anonima → 200 (atteso 401).
- **Correzione.** Dare la chiave al validatore dei tour, guardare UltimoUsoUtc, poi Api:RichiediChiave=true in produzione (configurazione, niente codice).


### S3 (90)

| U | V | Titolo | Dove | Da |
|---|---|---|---|---|
| U-019 | C | Qualunque membro IVAO loggato crea incarichi «liberi» senza limite, e finiscono nell'elenco admin | `src/Vipi.Application/Content/EditorTaskService.cs:90` | d01-05 (d01-identita-accesso) |
| U-020 | C | XSS memorizzato nel tooltip 2D dell'AoR: il nome del volume (KMZ) finisce in innerHTML via Leaflet | `src/Vipi.Ui/wwwroot/vipi-aor.js:370` | d02-01 (d02-input-superficie), d12-01 (d12-pagine-js-css-lingue) |
| U-021 | P | Tetto per IP e log «da dove» usano RemoteIpAddress: dietro Cloudflare/Plesk/Passenger l'IP vero del client non è garantito | `src/Vipi.Hosting/VipiModuleExtensions.cs:392` | d02-02 (d02-input-superficie) |
| U-022 | C | Un anonimo svuota errori-richieste.txt con GET /Error e zittisce per il giorno richieste-*.tsv e log-*.txt | `src/Vipi.Host/VipiStartup.cs:657` | d03-02 (d03-segreti-dati-infra) |
| U-023 | A | Login IVAO fallito: «error» ed «error_description» della query finiscono crudi, a capo compresi, in errori-richieste.txt (ogni volta) e in avvisi-log.txt (la prima del giorno) | `src/Vipi.Host/Auth/VipiStandaloneAuthExtensions.cs:282` | d03-09-v (d03-segreti-dati-infra) |
| U-024 | NV | Categoria «Settori» esclusa in Sorgenti: il giro dei settori timbra riuscito ogni notte senza timbrare nessuna riga, e dopo due notti la D8 cade su tutti i settori d'aeroporto | `src/Vipi.Infrastructure/Ivao/AirportSectorImportHostedService.cs:136` | d04-02 (d04-import-ivao) |
| U-025 | NV | Il whazzup è pubblico ma il poller passa dal token: credenziali IVAO rotte (rotazione A13, revoca, token endpoint giù) spengono vista live e statistiche | `src/Vipi.Infrastructure/Ivao/IvaoHttp.cs:36` | d04-03 (d04-import-ivao) |
| U-026 | NV | Una sessione ATC che ricompare nel whazzup dopo più di 15 minuti viene reinserita come nuova: chiave primaria duplicata, e ogni minuto l'intero piano delle sessioni (di tutti) va perso finché quel controllore resta conn… | `src/Vipi.Application/Stats/AtcSessionSync.cs:86` | d04-04 (d04-import-ivao) |
| U-027 | NV | Traffico d'aeroporto: un blocco che la sorgente rifiuta butta l'intero giro di consolidamento; uno scalo che IVAO non conosce (404) lo blocca per sempre, ritentando ogni ora, e nessuna riga di Sorgenti lo mostra | `src/Vipi.Application/Stats/AirportTrafficRollupUseCase.cs:92` | d04-05 (d04-import-ivao) |
| U-028 | NV | Una sola notte in cui IVAO manda military falso o assente riporta a Civile ogni campo militare, e la categoria scelta dall'amministratore (Solo militare, Militare con presenza civile) non torna più | `src/Vipi.Infrastructure/Persistence/EfStructureEditingRepository.cs:364` | d04-06 (d04-import-ivao) |
| U-029 | NV | Import manuale ACC e «Assegna aeroporti noti»: un timeout IVAO, un token rifiutato, un JSON non valido o una collisione col giro notturno fanno cadere il circuito dell'Editor; il doppio clic lancia due import sullo stes… | `src/Vipi.Ui/Pages/AccAdminPage.razor:576` | d04-07 (d04-import-ivao) |
| U-030 | NV | Gemello di T-007 nell'import dei confinanti: un dettaglio subcenter estero che non si legge azzera la frequenza in catalogo e nei Sector | `src/Vipi.Infrastructure/Persistence/EfNeighbourRepository.cs:139` | d04-08 (d04-import-ivao) |
| U-031 | P | L'alias fix è globale: la radice risolta in uno scalo riscrive, senza il segno «da verificare», il fix delle procedure di un altro scalo (LUMA: LUMAR a LIBD, LUMAV a LIPE) | `src/Vipi.Infrastructure/Sectorfile/AuroraSectorfileParser.cs:256` | d05-04 (d05-sectorfile-procedure) |
| U-032 | P | Il «Reimporta» dal tasto usa il ciclo dichiarato rimasto in cache dal giro precedente: le procedure del ciclo nuovo prendono il ciclo vecchio ed escono in anticipo, per sempre | `src/Vipi.Application/Content/ProcedureImporter.cs:72` | d05-06 (d05-sectorfile-procedure) |
| U-033 | P | ITALY.isc che risponde con un non-2xx (o spostata): il catalogo punti ripiega in silenzio su 3 file su 8, e l'import delle procedure gira su quello (circa 107 righe cambiano chiave) | `src/Vipi.Infrastructure/Sectorfile/AuroraNavaidSource.cs:197` | d05-07 (d05-sectorfile-procedure) |
| U-034 | C | SID+transizione separate da SPAZIO: il parser divide solo sul «-» e scrive fix spazzatura («LAT1E PEM» a LIRL, «FRASCA R» a LIED); a LIML e LIMF l'hanno corretto gli editor a mano | `src/Vipi.Infrastructure/Sectorfile/AuroraSectorfileParser.cs:201` | d05-08 (d05-sectorfile-procedure) |
| U-035 | C | Ogni giro d'import cancella e ricrea le procedure importate: le modifiche fatte dopo dall'editor già aperto vanno su Id spariti e si perdono in silenzio | `src/Vipi.Infrastructure/Persistence/EfAirportRepository.cs:431` | d05-09 (d05-sectorfile-procedure) |
| U-036 | C | Radioassistenze: il canale sta nell'identità, e una riga che la sorgente non manda più resta congelata, citata dai documenti, non correggibile né cancellabile (oggi TRP 25X) | `src/Vipi.Infrastructure/Persistence/EfNavaidCatalog.cs:279` | d05-10 (d05-sectorfile-procedure) |
| U-037 | C | Allineamento: aree TWR write-once (66 su 70 vengono da twrs.tfl e non si aggiornano mai), MRVA e radioassistenze senza gate AIRAC, a differenza delle aree di settore | `src/Vipi.Application/Content/GithubTowerShapeService.cs:51` | d05-11 (d05-sectorfile-procedure) |
| U-038 | C | Il giro delle procedure si dichiara riuscito anche quando falliscono tutti gli scali o la sorgente non risponde: niente ritentativo dopo un'ora, Sorgenti «aggiornata» | `src/Vipi.Infrastructure/Sectorfile/ProcedureImportHostedService.cs:74` | d05-12 (d05-sectorfile-procedure) |
| U-039 | P | GitHub giù o lento: nessun ricordo del fallimento e un solo semaforo per tutte le carte MRVA; bozza, editor e anteprima delle vIPI ACC/APP aspettano circa 15 s per carta, in fila, e cadono | `src/Vipi.Infrastructure/Sectorfile/SectorfileCache.cs:97` | d05-13 (d05-sectorfile-procedure) |
| U-040 | A | Radioassistenze: il canale scritto a mano su una riga della sorgente che non lo porta la stacca dalla sorgente; il giro dopo la ricrea, e quella citata resta congelata (TRP 25X) | `src/Vipi.Infrastructure/Persistence/EfNavaidCatalog.cs:180` | d05-16-v (d05-sectorfile-procedure) |
| U-041 | P | La rinomina di un callsign lascia indietro gli agganci AIP: il settore torna alla forma di IVAO in silenzio (e, latenti, le scelte per callsign del DocumentProfile) | `src/Vipi.Infrastructure/Persistence/EfCallsignRenameService.cs:142` | d06-02 (d06-altri-ingressi) |
| U-042 | C | Import in coda con «la prima riga è l'intestazione»: le colonne della tabella vengono sostituite e le righe esistenti troncate | `src/Vipi.Ui/Components/DocumentSectionsEditor.razor:1156` | d06-04 (d06-altri-ingressi) |
| U-043 | C | Import da testo senza tetto su colonne e celle, e la proposta moltiplica righe × colonne massime: una riga lunga basta a esaurire la memoria | `src/Vipi.Application/Import/Griglia.cs:82` | d06-05 (d06-altri-ingressi) |
| U-044 | C | La regola delle ancore «Aeroporti alternati» è una regex senza timeout a costo circa cubico sugli spazi Unicode non ridotti: gemello di T-023 | `src/Vipi.Application/Import/SpecTabelle.cs:79` | d06-06 (d06-altri-ingressi) |
| U-045 | C | Import XLSX: foglio e stringhe condivise fino a 32 MB passano per un DOM XDocument prima di ogni tetto; un file da 80 KB vale ~380 MB | `src/Vipi.Application/Import/LettoreXlsx.cs:51` | d06-07 (d06-altri-ingressi) |
| U-046 | C | Casella d'import e prosa degli editor spediscono il testo intero con @onchange, e il sito lascia il tetto SignalR a 32 KB | `src/Vipi.Ui/Components/ImportaTabella.razor:27` | d06-08 (d06-altri-ingressi) |
| U-047 | P | Traduzione: il lotto verso Azure si taglia a 50 testi e non a 50 000 caratteri; un lotto troppo lungo farebbe fallire ogni giro ripagando i lotti prima | `src/Vipi.Infrastructure/Translation/AzureTranslationEngine.cs:83` | d06-09 (d06-altri-ingressi) |
| U-048 | P | Biblioteca allegati: titolo oltre 200 o nota oltre 500 caratteri falliscono in DB e lasciano righe Added nel DbContext del circuito, e ogni salvataggio dopo fallisce | `src/Vipi.Infrastructure/Persistence/EfAttachmentLibrary.cs:46` | d06-10 (d06-altri-ingressi) |
| U-049 | C | Pagina spazi aerei: gestori senza guardia di rientro sul DbContext del circuito; un doppio clic su «Aggancia» abbatte il circuito | `src/Vipi.Ui/Pages/AdminAirspacePage.razor:488` | d06-11 (d06-altri-ingressi) |
| U-050 | A | Il tetto T-022 sull'XLSX si aggira a valle: la proposta porta ogni riga a 16 384 colonne, 2000 × 16 384 celle da un file di pochi KB | `src/Vipi.Application/Import/CostruttoreProposta.cs:47` | d06-16-v (d06-altri-ingressi) |
| U-051 | C-vivo | vLOA: nascondi AoR/frequenze e ordine delle frequenze si salvano senza controllare il lock | `src/Vipi.Application/Content/VloaDerivationService.cs:291` | d07-04 (d07-motore-lock-release), d01-02 (d01-identita-accesso) |
| U-052 | P | Editor unito: un membro col lock di un collega innesca un ciclo continuo di tentativi di lock, ricarichi e ridisegni | `src/Vipi.Ui/Components/Doc/UnionMembersEditor.razor:249` | d07-05 (d07-motore-lock-release), d09a-01 (d09a-editor-documenti) |
| U-053 | C | L'anteprima di una release (?as=rel:N) mostra le derivate di OGGI, non quelle congelate in quella release | `src/Vipi.Ui/Components/Doc/AirportMemberLoader.cs:174` | d07-06 (d07-motore-lock-release) |
| U-054 | C | vLOA: lingua e blocco lingua dal pannello release non si salvano mai («Documento inesistente») | `src/Vipi.Infrastructure/Persistence/EfDocumentAdminRepository.cs:124` | d07-08 (d07-motore-lock-release) |
| U-055 | C | Unire, togliere, spostare e sciogliere un'unione non guardano nessun lock | `src/Vipi.Application/Content/DocumentUnions.cs:206` | d07-09 (d07-motore-lock-release) |
| U-056 | C | AirportLockGuard non rinnova il lock: chi lavora solo sulle tabelle dello scalo lo perde 30 minuti dopo la presa, mentre salva | `src/Vipi.Application/Content/AirportLockGuard.cs:127` | d07-11 (d07-motore-lock-release) |
| U-057 | C | La ricerca e «Cosa è cambiato» saltano i documenti in vigore che non hanno mai avuto una «Pubblica versione» | `src/Vipi.Infrastructure/Persistence/EfSearchRepository.cs:37` | d07-20 (d07-motore-lock-release) |
| U-058 | A | La vLOA generata da una coppia confinante nasce «Published» senza che nessuno la pubblichi: al primo riavvio il backfill la mette in vigore col testo segnaposto, firmata «sistema» | `src/Vipi.Infrastructure/Persistence/EfNeighbourRepository.cs:385` | d07-21-v (d07-motore-lock-release) |
| U-059 | C | vLOA: nascondi settore/frequenza e ordine frequenze senza lock del documento, lettura-modifica-scrittura non versionata | `src/Vipi.Application/Content/VloaDerivationService.cs:301` | d08-01 (d08-derivazioni) |
| U-060 | P | Accordo verso un ente sparito: la vIPI continua a trasferirgli traffico e nessuna voce «Da rivedere» arriva alla controparte | `src/Vipi.Infrastructure/Persistence/EfDocumentImpactRepository.cs:40` | d08-04 (d08-derivazioni) |
| U-061 | C | Annulla dopo aver eliminato una variante: il gruppo non si ricostituisce (eccezione orfana salvata e poi rifiutata, o alternative separate) | `src/Vipi.Infrastructure/Persistence/EfAgreementRepository.cs:610` | d08-06 (d08-derivazioni) |
| U-062 | P | Riconciliazione d'avvio: una sezione LIBERA di primo livello intitolata «Configurazioni pista», «Regole piste» o «Runway rules» diventa una seconda «Regole piste» di catalogo e perde i suoi blocchi | `src/Vipi.Infrastructure/Persistence/EfDocumentMaintenance.cs:1207` | d09a-03 (d09a-editor-documenti) |
| U-063 | P | Gesti ravvicinati su tabelle e selezioni calcolate sullo stato «visto» al momento del gesto: il secondo salvataggio cancella il primo | `src/Vipi.Ui/Components/App/RegulatedAreasEditor.razor:232` | d09a-04 (d09a-editor-documenti) |
| U-064 | C | SID/STAR importate: dopo un reimport (giro notturno o altra scheda) le modifiche della tabella aperta non scrivono niente e dicono «Salvato» | `src/Vipi.Infrastructure/Persistence/EfAirportRepository.cs:431` | d09a-05 (d09a-editor-documenti) |
| U-065 | P | Pannello release: «Pubblica ora», «Pubblica al ciclo», «Annulla» e «Differenze» senza sentinella di rientro sul DbContext del circuito, e il gestore cattura solo due tipi d'eccezione | `src/Vipi.Ui/Components/ReleasePanel.razor:630` | d09a-07 (d09a-editor-documenti) |
| U-066 | C | Radioassistenze del vSOP: doppio clic su ✕ toglie due righe diverse | `src/Vipi.Ui/Components/Doc/MilSectionsEditor.razor:1216` | d09a-10 (d09a-editor-documenti) |
| U-067 | C | «Per tutti» su una sezione marcata dal SOD viene ribaltato a «Piloti» a OGNI consegna, non «una volta sola» come dichiarato | `src/Vipi.Infrastructure/Persistence/EfDocumentMaintenance.cs:1135` | d09a-11 (d09a-editor-documenti) |
| U-068 | C | vIPI d'aeroporto: l'ordine scelto a mano per «LVP» viene disfatto a ogni consegna | `src/Vipi.Infrastructure/Persistence/EfDocumentMaintenance.cs:933` | d09a-12 (d09a-editor-documenti) |
| U-069 | C | Pannello traduzioni: «apri» legge fuori dalla porta del componente e senza sentinella | `src/Vipi.Ui/Components/TranslationReviewPanel.razor:414` | d09a-20 (d09a-editor-documenti) |
| U-070 | A | Pannello dell'unione: se IsEditing cambia due volte di fila partono due ricarichi sullo stesso DbContext, e il circuito cade («Modifica» respinta su un'unione) | `src/Vipi.Ui/Components/Doc/UnionPanel.razor:301` | d09a-23-v (d09a-editor-documenti) |
| U-071 | A | Viewer d'aeroporto: una sottosezione libera di «Piste» intitolata «Configurazioni pista» o «Regole piste» diventa, sulla pagina pubblica, una seconda tabella delle regole e perde il suo testo | `src/Vipi.Application/Content/AirportLegacySections.cs:103` | d09a-25-v (d09a-editor-documenti) |
| U-072 | C | Glossario: con una ricerca attiva «Aggiungi» sovrascrive in silenzio una voce esistente | `src/Vipi.Ui/Pages/GlossarioPage.razor:1013` | d09b-08 (d09b-pagine-admin) |
| U-073 | C | Spazi aerei: TMA, TMZ, R e P non si possono agganciare mai — il tetto di 500 righe ordinate per NOME della famiglia le taglia fuori | `src/Vipi.Ui/Pages/AdminAirspacePage.razor:463` | d09b-10 (d09b-pagine-admin) |
| U-074 | C | Eliminazione multipla delle clausole: le eccezioni della capofila restano orfane e cambiano significato, e l'annulla non le riattacca | `src/Vipi.Ui/Pages/AdminTrasferimentiPage.razor:1116` | d09b-02-g1 (d09b-pagine-admin-giro1) |
| U-075 | C | Spazi aerei: «Aggancia» sostituisce tutti gli agganci del settore, ma la pagina non li precarica e non avvisa: chi aggiunge un volume toglie gli altri dalla carta pubblica | `src/Vipi.Ui/Pages/AdminAirspacePage.razor:488` | d09b-12-g1 (d09b-pagine-admin-giro1) |
| U-076 | NV | A ogni consegna le LVP vengono rimesse subito dopo «Procedure generali», disfacendo il riordino fatto dall'editor | `src/Vipi.Infrastructure/Persistence/EfDocumentMaintenance.cs:933` | d10-03 (d10-dati) |
| U-077 | NV | Il pubblico «per tutti» scelto a mano su una sezione del SOD torna «piloti» a ogni consegna, anche nelle versioni archiviate | `src/Vipi.Infrastructure/Persistence/EfDocumentMaintenance.cs:1135` | d10-04 (d10-dati) |
| U-078 | NV | Bool con HasDefaultValue(true): un false scritto in INSERT diventa true (ACC esteri che nascono con le aree accese, prima policy d'import ribaltata) | `src/Vipi.Infrastructure/Persistence/VipiDbContext.cs:288` | d10-05 (d10-dati) |
| U-079 | NV | Un ATIS con molte piste supera varchar(32) di AtcSessionRunways e, in strict mode, avvelena lo scope: si perde il traffico di tutta la divisione a ogni giro | `src/Vipi.Infrastructure/Persistence/EfAtcSessionStore.cs:197` | d10-06 (d10-dati) |
| U-080 | NV | vLOA generate da «ACC confinanti» nascono Published senza CurrentVersionId: spariscono da ricerca e «Cosa è cambiato», e alla prossima pubblicazione restano due versioni Published | `src/Vipi.Infrastructure/Persistence/EfSearchRepository.cs:37` | d10-07 (d10-dati) |
| U-081 | NV | Pannello release: pubblicare mentre il pannello sta leggendo (differenze, ricarico dopo un'unione, doppio clic) abbatte il circuito | `src/Vipi.Ui/Components/ReleasePanel.razor:630` | d11-02 (d11-concorrenza-circuito) |
| U-082 | NV | Accordi (Trasferimenti): dopo che chiunque scrive un ACC o un aeroporto, il primo disegno della pagina legge il catalogo dal database dentro il render e può abbattere il circuito | `src/Vipi.Ui/Pages/AdminTrasferimentiPage.razor:131` | d11-03 (d11-concorrenza-circuito) |
| U-083 | NV | Revisione traduzioni: aprire due righe di fila fa cadere il circuito (DocumentiToccatiAsync fuori dalla porta e senza _busy) | `src/Vipi.Ui/Components/TranslationReviewPanel.razor:409` | d11-04 (d11-concorrenza-circuito) |
| U-084 | NV | La trappola `value="@x" @oninput` è ancora in 20 campi: caratteri cancellati che ricompaiono, anche su quote e poligoni che poi si salvano | `src/Vipi.Ui/Components/App/TypeaheadPicker.razor:26` | d12-03 (d12-pagine-js-css-lingue) |
| U-085 | NV | Zoom di pagina irraggiungibile allo scaglione tb-4: chi ingrandisce su un portatile resta bloccato, anche alle visite successive | `src/Vipi.Ui/wwwroot/vipi-theme.css:4505` | d12-04 (d12-pagine-js-css-lingue) |
| U-086 | NV | Pagine statistiche più larghe di qualunque telefono: `.stats-cols` pretende colonne da 420px | `src/Vipi.Ui/wwwroot/vipi-theme.css:613` | d12-05 (d12-pagine-js-css-lingue) |
| U-087 | NV | Vista live sul telefono: la testata appiccicata va a capo su 5-6 righe e resta incollata sotto la barra | `src/Vipi.Ui/wwwroot/vipi-theme.css:1844` | d12-07 (d12-pagine-js-css-lingue) |
| U-088 | NV | Mappe AoR/MRVA e stage 3D intrappolano il dito sul telefono; il 3D non si può zoomare al tocco | `src/Vipi.Ui/wwwroot/vipi-aor.js:340` | d12-08 (d12-pagine-js-css-lingue) |
| U-089 | C | VV/// e strati con base /// letti come «cielo sgombro»: il quadro propone di cancellare le LVP (o le sottostima) col cielo oscurato | `src/Vipi.Application/Weather/MetarParser.cs:73` | d13-01 (d13-dominio-aeronautico) |
| U-090 | C | RVR tutti «P» (sopra scala): la valutazione LVP ricade sulla visibilità e scrive «no RVR reported» | `src/Vipi.Application/Content/Lvp.cs:195` | d13-02 (d13-dominio-aeronautico) |
| U-091 | C | Regole piste valutate sul vento medio: la raffica non entra nella coda e nel traverso massimi | `src/Vipi.Application/Awos/AwosComposition.cs:143` | d13-03 (d13-dominio-aeronautico) |
| U-092 | C | vAWOS: l'«età del bollettino» misura l'ultima risposta HTTP, non il METAR; un METAR vecchio di ore sembra fresco e guida LVP e pista | `src/Vipi.Ui/wwwroot/vipi-awos.js:153` | d13-05 (d13-dominio-aeronautico) |
| U-093 | A | RVR «P2000» su tutte le celle quando il METAR non ha RVR anche con visibilità di poche centinaia di metri | `src/Vipi.Ui/Shared/AwosTesto.cs:181` | d13-18-v (d13-dominio-aeronautico) |
| U-094 | C | Riempimento a posteriori: una sovrapposizione anche breve con una posizione più titolata azzera i movimenti dell'intera sessione | `src/Vipi.Application/Stats/AirportBackfillPlanner.cs:50` | d13-01-g1 (d13-dominio-aeronautico-giro1) |
| U-095 | PLAUSIBLE | vAWOS: senza gruppi RVR il quadro scrive P2000 su ogni testata anche con visibilità di poche centinaia di metri | `src/Vipi.Ui/Shared/AwosTesto.cs:181` | d13-03-g1 (d13-dominio-aeronautico-giro1) |
| U-096 | NV | Migrazioni MySQL all'avvio non atomiche, non serializzate e non riprendibili: un'interruzione a metà lascia il sito giù a ogni riavvio | `src/Vipi.Hosting/VipiModuleExtensions.cs:745` | d14-01 (d14-esercizio-qualita) |
| U-097 | NV | Migrazioni distruttive consegnate come MINOR: il processo vecchio ancora vivo e il rollback «a due rinomine» girano su uno schema che non conoscono, mentre il foglio promette «esattamente la situazione di prima» | `deploy/atc-ivao/LEGGIMI-AGGIORNARE-VIA-FTP.md:219` | d14-02 (d14-esercizio-qualita) |
| U-098 | NV | Un anonimo svuota il registro degli errori (errori-richieste.txt e la sua copia precedente) con qualche decina di GET /Error | `src/Vipi.Host/VipiStartup.cs:658` | d14-03 (d14-esercizio-qualita) |
| U-099 | NV | Un anonimo zittisce fino a mezzanotte UTC il log del giorno e il registro delle richieste (tetto di 5 MB) con circa 35 000 richieste che prendono 401 | `src/Vipi.Host/RegistroGiornaliero.cs:89` | d14-04 (d14-esercizio-qualita) |
| U-100 | NV | /vsop/health è «Degraded» per costruzione in produzione: il canale pensato per segnalare manutenzioni d'avvio fallite e sonde rotte non distingue più niente | `src/Vipi.Hosting/VipiHealthCheck.cs:43` | d14-05 (d14-esercizio-qualita) |
| U-101 | C | Stream SSE con solo tetto globale (300): un qualunque utente IVAO entrato lo esaurisce per tutta la divisione | `src/Vipi.Hosting/VipiModuleExtensions.cs:294` | d15-03-g1 (d15-abuso-robustezza-giro1) |
| U-102 | P | La cache delle letture anonime si aggira con un parametro di query o un cookie qualsiasi, e nessuna pagina ha un tetto per chiamante | `src/Vipi.Host/VipiStartup.cs:171` | d15-04-g1 (d15-abuso-robustezza-giro1) |
| U-103 | P | Un cookie di sessione più vecchio di 4 ore, se ri-mandato sempre uguale, fa chiamare l'API IVAO a ogni richiesta, senza cache né tetto lato server | `src/Vipi.Host/Auth/RiconvalidaPosizioniStaff.cs:51` | d15-05-g1 (d15-abuso-robustezza-giro1) |
| U-104 | C | Ponte RFO: nessun tetto di frequenza sul PUT e storia che conserva una copia intera (fino a 1 MB) a ogni scrittura, senza potatura | `src/Vipi.Infrastructure/Persistence/EfRfoSharedStateStore.cs:108` | d15-06-g1 (d15-abuso-robustezza-giro1) |
| U-105 | NV | La migrazione «Gestione del traffico» ha spostato solo il VFR: dove l'IFR era già scritto a mano restano una IFR di catalogo vuota e il contenuto IFR fuori dal contenitore (LIBB pubblicata), oppure due «Gestione del tra… | `src/Vipi.Infrastructure/Persistence/EfDocumentMaintenance.cs:641` | d16-03 (d16-allineamento-documenti) |
| U-106 | C-vivo | Sotto i 900 px le tabelle spezzano a metà frequenze, callsign e numeri invece di scorrere | `src/Vipi.Ui/wwwroot/vipi-theme.css:4632` | v-02 (dal-vivo) |
| U-107 | C-vivo | Statistiche personali e di divisione: la pagina scorre in orizzontale di 61 px sul telefono | `src/Vipi.Ui/wwwroot/vipi-theme.css:613` | v-03 (dal-vivo) |
| U-108 | C-vivo | Pannello «Translation» degli editor: due clic ravvicinati su una riga fanno cadere il circuito | `src/Vipi.Ui/Components/TranslationReviewPanel.razor` | v-04 (dal-vivo) |

### S4 (148)

| U | V | Titolo | Dove | Da |
|---|---|---|---|---|
| U-109 | C | Intro di pagina: il deposito salva senza verificare il lock `editor:page-intro:*` che la pagina tiene | `src/Vipi.Infrastructure/Persistence/EfPageIntroStore.cs:43` | d01-03 (d01-identita-accesso) |
| U-110 | C | Cinque scritture della UI vanno dritte all'Infrastructure senza cancello nel metodo | `src/Vipi.Ui/Pages/GlossarioPage.razor:1206` | d01-04 (d01-identita-accesso) |
| U-111 | P | Reimport SID/STAR dall'editor d'aeroporto senza nessun controllo di lock | `src/Vipi.Application/Content/ProcedureImporter.cs:49` | d01-06 (d01-identita-accesso) |
| U-112 | C | ProductionIdentityGuard non può mai scattare: la guardia dell'audit D1 è tautologica | `src/Vipi.Host/VipiStartup.cs:276` | d01-07 (d01-identita-accesso) |
| U-113 | C | Commenti che dicono «solo admin» su sblocco forzato, lock di struttura e registro traduzioni: il codice dice Editor | `src/Vipi.Application/Content/ResourceLockService.cs:18` | d01-08 (d01-identita-accesso) |
| U-114 | P | Doppio clic su Permessi e Chiavi API: due operazioni sullo stesso DbContext, il circuito cade | `src/Vipi.Ui/Pages/AdminApiKeysPage.razor:188` | d01-09 (d01-identita-accesso) |
| U-115 | C | Riconvalida delle posizioni: durante un guasto IVAO una richiesta per utente ogni 15 minuti aspetta il timeout | `src/Vipi.Host/Auth/RiconvalidaPosizioniStaff.cs:61` | d01-10 (d01-identita-accesso) |
| U-116 | C | Sessioni altrui viste dallo staff senza la riga di audit prescritta (dettaglio turno, archivio per VID) | `src/Vipi.Ui/Pages/StatsSessionPage.razor:167` | d01-11 (d01-identita-accesso) |
| U-117 | A | Intro di pagina: chi prende il lock scrive sopra un testo letto all'apertura della pagina e perde in silenzio il salvataggio di chi aveva il lock prima | `src/Vipi.Ui/Components/PageIntroZone.razor:225` | d01-12-v (d01-identita-accesso) |
| U-118 | A | ProfileSwapper: con più di 50 file trascinati la pagina cade (eccezione fuori dal try) | `src/Vipi.Ui/Pages/ProfileSwapperPage.razor:374` | d01-13-v (d01-identita-accesso) |
| U-119 | A | Nomi AIP con entità letterali: 32 volumi su 1536 si leggono «VAL D&apos;AOSTA» | `src/Vipi.Application/Airspace/AirspaceKmlReader.cs:58` | d02-03-v (d02-input-superficie) |
| U-120 | P | Righe false nei registri di diagnostica: un percorso con %0A va a capo in avvisi-log.txt (e in avvii.txt ed errori-richieste.txt) e può zittire un avviso per tutto il giorno | `src/Vipi.Host/RegistroAvvisi.cs:193` | d03-01 (d03-segreti-dati-infra) |
| U-121 | P | Ponte RFO: la storia copia l'intero documento (fino a 1 MB) a ogni PUT, senza tetto, senza limitatore e senza potatura | `src/Vipi.Infrastructure/Persistence/EfRfoSharedStateStore.cs:108` | d03-03 (d03-segreti-dati-infra) |
| U-122 | P | Anteprima Render: dal 24 agosto l'app in Production non si fida più del proxy, quindi redirect_uri OIDC in http e un solo IP per tutti; runbook e commento dicono il contrario | `src/Vipi.Host/VipiStartup.cs:319` | d03-04 (d03-segreti-dati-infra) |
| U-123 | P | prepara-pacchetto.ps1: la seconda rete non riconosce le chiavi del ponte RFO, non guarda docs/ e blocca per sempre appsettings.json | `tools/prepara-pacchetto.ps1:65` | d03-05 (d03-segreti-dati-infra) |
| U-124 | C | I nomi che l'oscurità protegge finiscono nella diagnostica: il file dei segreti malformato in avvio-errore.txt, il GUID del key-ring in avvisi-log.txt | `src/Vipi.Host/SegretiFuoriDalWeb.cs:66` | d03-06 (d03-segreti-dati-infra) |
| U-125 | P | avvio-diagnostica.txt stampa in chiaro la parte di password dopo un «;» fra virgolette | `src/Vipi.Host/StartupDiagnostics.cs:175` | d03-07 (d03-segreti-dati-infra) |
| U-126 | C | Due carte dicono il falso: la vista condivisa «in PR, non in pacchetto» (è online da 1.43.0) e LEGGIMI-DEPLOY che per aggiornare manda a un foglio-storia che comincia con DROP DATABASE | `docs/feature/2026-09-23-vista-condivisa-sessioni-atc.md:3` | d03-08 (d03-segreti-dati-infra) |
| U-127 | A | Le righe di contesto di avvisi-log.txt non troncano il percorso: una voce può portare circa 80 kB di percorsi scelti da un anonimo | `src/Vipi.Host/RegistroAvvisi.cs:193` | d03-10-v (d03-segreti-dati-infra) |
| U-128 | NV | «Verde regalato» ancora possibile: TA/piste e aree speciali timbrano riuscito anche quando TUTTI gli enti hanno fallito; il catch «non configurato» del giro TA/piste è codice morto e il commento del caso d'uso dice il f… | `src/Vipi.Application/Content/AirportDataImportUseCase.cs:102` | d04-09 (d04-import-ivao) |
| U-129 | NV | «Chiedi alla sorgente» su un ACC estero risponde sempre «non c'è più», e gli ACC esteri non vengono mai ritimbrati dal giro notturno | `src/Vipi.Infrastructure/Ivao/IvaoSourcePresenceProbe.cs:170` | d04-10 (d04-import-ivao) |
| U-130 | NV | «Escludi aree» pota legami e cancella aree orfane senza aprire l'impatto AreaGone che l'import apre per la stessa sparizione | `src/Vipi.Infrastructure/Persistence/EfAccAdminRepository.cs:53` | d04-11 (d04-import-ivao) |
| U-131 | NV | Whazzup parziale o fermo preso per fresco: AsOf è l'ora di arrivo, updatedAt è ignorato e clients assente vale «nessuno online» | `src/Vipi.Infrastructure/Ivao/IvaoWhazzupClient.cs:86` | d04-12 (d04-import-ivao) |
| U-132 | P | Pagina Radioassistenze: «Rileggi dal sectorfile» (che riparte fuori dal dispatcher) e le celle scrivibili usano insieme il DbContext del circuito; nessun catch in Scrivi/Aggiungi/Elimina | `src/Vipi.Ui/Pages/AdminNavaidsPage.razor:450` | d05-14 (d05-sectorfile-procedure) |
| U-133 | P | Latenti nei parser .tfl: un commento a riga intera dentro un blocco tronca l'anello (ParseTowerShapes e ParseSectorShapes), e nessuno controlla che la coppia sia N/S poi E/W | `src/Vipi.Infrastructure/Sectorfile/AuroraSectorfileParser.cs:416` | d05-15 (d05-sectorfile-procedure) |
| U-134 | A | Timbro «appiccicoso»: se il ciclo dichiarato torna indietro (changelog in anticipo o con un refuso), le righe timbrate col ciclo lontano restano nascoste anche dopo la correzione | `src/Vipi.Infrastructure/Persistence/EfAirportRepository.cs:357` | d05-17-v (d05-sectorfile-procedure) |
| U-135 | P | Eliminare un settore porta via o lascia appesi legami che il piano non nomina (frequenze collegate in cascata, ripieghi, agganci AIP), senza marcare i documenti | `src/Vipi.Infrastructure/Persistence/EfDeletionRepository.cs:249` | d06-03 (d06-altri-ingressi) |
| U-136 | C | «Confronta con l'AIP» legge sempre il file in vigore come KMZ: dopo un caricamento .kml dice che nessuna radioassistenza è nell'AIP | `src/Vipi.Ui/Pages/AdminAirspacePage.razor:528` | d06-12 (d06-altri-ingressi) |
| U-137 | C | La quota immagini per documento si aggira: ogni sostituzione lascia l'asset precedente nel deposito, non contato e non ripulito | `src/Vipi.Ui/Components/ImageBlockEditor.razor:187` | d06-13 (d06-altri-ingressi) |
| U-138 | P | Pulizia immagini orfane non atomica col deposito che deduplica per sha: un file ricaricato durante la pulizia finisce citato ma cancellato | `src/Vipi.Infrastructure/Persistence/EfMediaMaintenance.cs:45` | d06-14 (d06-altri-ingressi) |
| U-139 | C | Lingua e blocco lingua cambiati dal pannello release saltano il controllo del lock altrui | `src/Vipi.Application/Content/DocumentAdminService.cs:99` | d07-07 (d07-motore-lock-release) |
| U-140 | P | Intro di pagina: il lock esiste solo nella pagina, il deposito salva senza chiederlo | `src/Vipi.Infrastructure/Persistence/EfPageIntroStore.cs:40` | d07-10 (d07-motore-lock-release) |
| U-141 | C | Elenco pubblico dei vSOP militari e ponte civile↔militare ignorano il documento nascosto | `src/Vipi.Infrastructure/Persistence/EfMilitaryDocumentService.cs:71` | d07-12 (d07-motore-lock-release) |
| U-142 | C | Ricerca e «Cosa è cambiato»: il cancello pubblico ignora l'aeroporto nascosto e l'APP disattivato | `src/Vipi.Infrastructure/Persistence/PublicDocumentGate.cs:26` | d07-13 (d07-motore-lock-release) |
| U-143 | C | «Scarta bozza» su un documento unito è impossibile se un membro non è mai stato pubblicato, e l'errore sembra parlare del documento sbagliato | `src/Vipi.Application/Content/EditingService.cs:310` | d07-14 (d07-motore-lock-release) |
| U-144 | P | Pannello release e pagina Versioni: una DbUpdateException (due pubblicazioni simultanee sullo stesso bersaglio) non è gestita e abbatte il circuito | `src/Vipi.Ui/Components/ReleasePanel.razor:630` | d07-15 (d07-motore-lock-release) |
| U-145 | P | «Pubblica versione» e «Scarta bozza» su un'unione non sono atomici | `src/Vipi.Application/Content/EditingService.cs:284` | d07-16 (d07-motore-lock-release) |
| U-146 | C | Commenti e Guida dicono «sblocco solo admin»: il codice lo permette a ogni Editor | `src/Vipi.Application/Content/ResourceLockService.cs:31` | d07-17 (d07-motore-lock-release) |
| U-147 | P | Le sezioni di catalogo si possono eliminare dal servizio: la guardia sta solo nel tasto | `src/Vipi.Application/Content/EditingService.cs:234` | d07-19 (d07-motore-lock-release) |
| U-148 | C | vIPI ACC con snapshot anteriore a una sezione di catalogo: SCCAM/FIC accodate in fondo con «Nessun settore» | `src/Vipi.Application/Content/AccDocumentAssembler.cs:166` | d08-02 (d08-derivazioni) |
| U-149 | C | Frase capofila presa dalla prima riga di tabelle miste: nomina un solo ricevente o scalo | `src/Vipi.Ui/Components/App/CoordTable.razor:221` | d08-03 (d08-derivazioni) |
| U-150 | P | Frequenze collegate a settori disattivati: la frequenza resta ferma a quella della disattivazione, senza voce | `src/Vipi.Infrastructure/Persistence/EfAccDerivationRepository.cs:218` | d08-05 (d08-derivazioni) |
| U-151 | C | SID/STAR fra i punti degli accordi: nome del ciclo di oggi anche nelle release programmate al ciclo entrante | `src/Vipi.Application/Content/ProcedureReferenceResolver.cs:117` | d08-07 (d08-derivazioni) |
| U-152 | P | Ponte Aurora: la condizione della capofila non pesa sulle eccezioni | `src/Vipi.Application/Content/TransferMatcher.cs:333` | d08-08 (d08-derivazioni) |
| U-153 | C | Frase: il codice del mittente sparisce se le sue lettere stanno nel nome («TUNIS Radar» per DTTC_N_CTR) | `src/Vipi.Application/Content/CoordinationSentenceComposer.cs:552` | d08-09 (d08-derivazioni) |
| U-154 | C | Condizione e livello in blocco: niente tetto di lunghezza né regola «in ogni caso» | `src/Vipi.Infrastructure/Persistence/EfAgreementRepository.cs:531` | d08-10 (d08-derivazioni) |
| U-155 | C | La vLOA legge due volte gli accordi di confine: doppioni nascosti dal collasso, «, » nella colonna CoP per le clausole senza punto | `src/Vipi.Application/Content/VloaDerivationService.cs:236` | d08-11 (d08-derivazioni) |
| U-156 | P | Giri di deriva concorrenti riconciliano senza fila | `src/Vipi.Application/Content/ImpactDriftUseCase.cs:160` | d08-12 (d08-derivazioni) |
| U-157 | C | Cambiare il canale di una radioassistenza citata ne cambia l'identità: le tabelle militari la perdono in silenzio | `src/Vipi.Infrastructure/Persistence/EfNavaidCatalog.cs:175` | d08-13 (d08-derivazioni) |
| U-158 | C | vLOA: tre definizioni di «settori confinanti» e due campi calcolati che nessuno legge | `src/Vipi.Infrastructure/Persistence/EfVloaDerivationRepository.cs:31` | d08-14 (d08-derivazioni) |
| U-159 | C | vLOA generata dai confinanti nasce «Published» mentre il commento dice «bozza» | `src/Vipi.Infrastructure/Persistence/EfNeighbourRepository.cs:385` | d08-15 (d08-derivazioni) |
| U-160 | A | Canale scritto a mano su una radioassistenza importata senza canale: esce dall'import, che crea un doppione, e i documenti restano sulla copia congelata | `src/Vipi.Infrastructure/Persistence/EfNavaidCatalog.cs:180` | d08-17-v (d08-derivazioni) |
| U-161 | C | «Reimporta SID» dell'editor non controlla il lock del documento dello scalo (gemello di T-004) | `src/Vipi.Application/Content/ProcedureImporter.cs:55` | d09a-06 (d09a-editor-documenti) |
| U-162 | P | Tabella generica: due celle scritte in fila → la seconda è rifiutata come conflitto e il campo mostra un valore mai salvato | `src/Vipi.Ui/Components/DocumentSectionsEditor.razor:1040` | d09a-08 (d09a-editor-documenti) |
| U-163 | P | vSOP militare, «+ Alternato»: la ricerca del nome gira fuori dal tornello e fuori dal guardiano; un timeout di IVAO o una lettura in corso abbattono il circuito | `src/Vipi.Ui/Components/Doc/MilSectionsEditor.razor:1299` | d09a-09 (d09a-editor-documenti) |
| U-164 | P | Scrittura di un campo di radioassistenza: in fila ma fuori dal guardiano — un'eccezione del servizio abbatte il circuito | `src/Vipi.Ui/Components/Doc/MilSectionsEditor.razor:1250` | d09a-13 (d09a-editor-documenti) |
| U-165 | P | «Crea vSOP militare» dall'editor d'aeroporto: fuori dal tornello, senza controllo di _busy e con due soli catch | `src/Vipi.Ui/Components/Doc/AirportSectionsEditor.razor:734` | d09a-14 (d09a-editor-documenti) |
| U-166 | P | DocumentSectionsEditor: la lettura dei riferimenti citati nel ciclo di vita non ha rete | `src/Vipi.Ui/Components/DocumentSectionsEditor.razor:325` | d09a-15 (d09a-editor-documenti) |
| U-167 | C | «Prosa capofila/distesa» sui Coordinamenti non ricarica: l'etichetta non cambia e il secondo clic non torna indietro | `src/Vipi.Ui/Components/DocumentSectionsEditor.razor:509` | d09a-16 (d09a-editor-documenti) |
| U-168 | C | Editor APP, ACC e vLOA restano «in modifica» dopo un ricarico anche se il lock è ormai di un altro | `src/Vipi.Ui/Components/Doc/AppSectionsEditor.razor:474` | d09a-17 (d09a-editor-documenti) |
| U-169 | C | Pannello dell'unione: il ricarico dell'host dopo un gesto può abbattere il circuito (anche il timeout del tornello pensato proprio per questo pannello) | `src/Vipi.Ui/Components/Doc/UnionPanel.razor:456` | d09a-18 (d09a-editor-documenti) |
| U-170 | C | Editor unito: trascinare nell'indice una sezione di un MEMBRO la sposta davvero, ma a schermo non cambia niente | `src/Vipi.Ui/Components/Doc/UnionMembersEditor.razor:240` | d09a-19 (d09a-editor-documenti) |
| U-171 | C | Alias del punto SID scritto prima del controllo del lock e senza guardia di servizio | `src/Vipi.Ui/Components/Doc/AirportSectionsEditor.razor:888` | d09a-21 (d09a-editor-documenti) |
| U-172 | C | Commento falso nella riconciliazione del VFR: dice «non la vIPI ACC» e due righe sotto la tratta | `src/Vipi.Infrastructure/Persistence/EfDocumentMaintenance.cs:611` | d09a-22 (d09a-editor-documenti) |
| U-173 | A | UpdateImportedSidAsync cerca la riga solo per Id, senza lo scalo: il lock controllato è quello di un ICAO, la riga scritta può essere di un altro | `src/Vipi.Infrastructure/Persistence/EfAirportRepository.cs:428` | d09a-24-v (d09a-editor-documenti) |
| U-174 | P | ACC e Radioassistenze: gesti non spenti durante import sullo stesso DbContext del circuito | `src/Vipi.Ui/Pages/AdminNavaidsPage.razor:450` | d09b-03 (d09b-pagine-admin) |
| U-175 | C | ACC: import con IVAO irraggiungibile o risposta non JSON abbatte il circuito (il timeout invece lascia la pagina muta) | `src/Vipi.Ui/Pages/AccAdminPage.razor:576` | d09b-04 (d09b-pagine-admin) |
| U-176 | C | Trasferimenti: «Applica condizione» con i campi vuoti cancella le condizioni delle clausole scelte, senza conferma né annulla e senza validazione | `src/Vipi.Ui/Pages/AdminTrasferimentiPage.razor:386` | d09b-05 (d09b-pagine-admin) |
| U-177 | C | Trasferimenti: «Applica livello» in blocco cancella lo stato «livellato» (e le frecce non scritte) | `src/Vipi.Infrastructure/Persistence/EfAgreementRepository.cs:522` | d09b-06 (d09b-pagine-admin) |
| U-178 | P | Incolla tabella: se una riga viene rifiutata, le precedenti restano salvate ma non a schermo, e al nuovo invio si duplicano | `src/Vipi.Ui/Pages/AdminTrasferimentiPage.razor:1991` | d09b-07 (d09b-pagine-admin) |
| U-179 | C | Glossario: il cestino cancella la voce al primo clic, senza conferma e senza traccia | `src/Vipi.Ui/Pages/GlossarioPage.razor:261` | d09b-09 (d09b-pagine-admin) |
| U-180 | P | Tetto SignalR di 32 KB mai alzato nel sito: un poligono o una tabella incollati oltre 32 KB staccano il circuito | `src/Vipi.Host/VipiStartup.cs:118` | d09b-11 (d09b-pagine-admin) |
| U-181 | P | Permessi: salvataggio senza sentinella e fuori dalla fila, con due soli catch — nota oltre 500 caratteri o doppio clic abbattono il circuito | `src/Vipi.Ui/Pages/AdminRolesPage.razor:371` | d09b-12 (d09b-pagine-admin) |
| U-182 | C | «Da fare»: qualunque utente IVAO loggato, anche senza livello, crea incarichi senza limite che finiscono nella pagina admin | `src/Vipi.Ui/Pages/TasksPage.razor:263` | d09b-13 (d09b-pagine-admin) |
| U-183 | C | Confinanti: una «Verifica adiacenza» interrotta cambiando coppia consegna il dettaglio della coppia vecchia nel pannello della nuova | `src/Vipi.Ui/Pages/ConfinantiAdminPage.razor:715` | d09b-14 (d09b-pagine-admin) |
| U-184 | P | Sorgenti: «Salva» riscrive l'intera politica letta all'apertura e rovescia in silenzio la decisione di un altro admin | `src/Vipi.Ui/Pages/SorgentiAdminPage.razor:361` | d09b-15 (d09b-pagine-admin) |
| U-185 | C | Trasferimenti e Permessi: la conferma inline resta aperta quando cambia ciò che ha sotto (accordo, sezione, lacuna, persona) | `src/Vipi.Ui/Pages/AdminTrasferimentiPage.razor:1490` | d09b-16 (d09b-pagine-admin) |
| U-186 | C | Cella del livello: la freccia ↑/↓ si vede ma non si può cambiare né togliere | `src/Vipi.Ui/Pages/AdminTrasferimentiPage.razor:2671` | d09b-17 (d09b-pagine-admin) |
| U-187 | P | Condizione di pista senza tetto nel servizio: la scelta multipla con ICAO può superare varchar(80) | `src/Vipi.Application/Content/AgreementEditingService.cs:349` | d09b-18 (d09b-pagine-admin) |
| U-188 | C | Diagnostica: «Aggiorna» resta acceso durante «Rilancia la deriva» e i due girano insieme sullo stesso contesto | `src/Vipi.Ui/Pages/DiagnosticaPage.razor:56` | d09b-19 (d09b-pagine-admin) |
| U-189 | C | ACC: quote dei settori — il testo non numerico diventa GND/UNL, e inferiore ≥ superiore passa | `src/Vipi.Ui/Pages/AccAdminPage.razor:566` | d09b-21 (d09b-pagine-admin) |
| U-190 | P | Altri gestori che lasciano uscire le eccezioni (circuito morto su un errore del database) | `src/Vipi.Ui/Pages/GlossarioPage.razor:1049` | d09b-22 (d09b-pagine-admin) |
| U-191 | C | Codice morto che descrive un meccanismo inesistente: ChangeKind/_kindEpoch | `src/Vipi.Ui/Pages/AdminTrasferimentiPage.razor:1850` | d09b-23 (d09b-pagine-admin) |
| U-192 | A | Struttura: cliccare un nodo dell'albero (anche lo stesso) butta via in silenzio la catena di ripiego che si stava scrivendo | `src/Vipi.Ui/Pages/StrutturaPage.razor:1065` | d09b-v01 (d09b-pagine-admin) |
| U-193 | P | Trasferimenti: l'annulla si consuma prima che il ripristino riesca, e un'eliminazione che Guarded salta perché la pagina è occupata arma lo stesso l'annulla | `src/Vipi.Ui/Pages/AdminTrasferimentiPage.razor:3832` | d09b-13-g1 (d09b-pagine-admin-giro1) |
| U-194 | NV | Note senza tetto su colonne varchar: nota LVP (2000) e nota della promozione a mano (500) | `src/Vipi.Infrastructure/Persistence/EfAirportRepository.cs:281` | d10-08 (d10-dati) |
| U-195 | NV | La riconciliazione «Purpose» delle vLOA può dare la chiave di catalogo a una seconda sezione libera, a ogni consegna | `src/Vipi.Infrastructure/Persistence/EfDocumentMaintenance.cs:116` | d10-09 (d10-dati) |
| U-196 | NV | SegnalaModificheInterceptor: un salvataggio fallito dentro una transazione butta anche le famiglie dei salvataggi già riusciti | `src/Vipi.Infrastructure/Persistence/SegnalaModificheInterceptor.cs:60` | d10-10 (d10-dati) |
| U-197 | NV | EditLockBar non chiude mai il proprio scope: il DisposeAsync ridichiarato nasconde quello di OwningComponentBase | `src/Vipi.Ui/Components/EditLockBar.razor:235` | d11-05 (d11-concorrenza-circuito) |
| U-198 | NV | Il presidio dello scope proprio considera «sicuro» IProssimoAiracService, che legge e scrive il database; VersioniPage lo usa dal circuito fuori dalla terza porta | `tests/Vipi.Ui.Tests/ScopeProprioDellePagineTests.cs:82` | d11-06 (d11-concorrenza-circuito) |
| U-199 | NV | Chiavi API: un doppio clic su «Crea» apre due scritture sullo stesso contesto (nessuna guardia di rientro, CreaAsync fuori porta) | `src/Vipi.Ui/Pages/AdminApiKeysPage.razor:188` | d11-07 (d11-concorrenza-circuito) |
| U-200 | NV | Il giro della deriva (ora due minuti dopo ogni modifica) può riaprire «da ripubblicare» su un documento appena pubblicato | `src/Vipi.Application/Content/ImpactDriftUseCase.cs:139` | d11-08 (d11-concorrenza-circuito) |
| U-201 | NV | Hub e landing sfondano a 375 e 360: `.choice-grid` con minimo 340px | `src/Vipi.Ui/wwwroot/vipi-theme.css:1052` | d12-06 (d12-pagine-js-css-lingue) |
| U-202 | NV | Vista rapida d'aeroporto (live) con testi fissi in italiano, e intestazioni inglesi in pagina italiana | `src/Vipi.Ui/Components/App/AirportQuickPanel.razor:24` | d12-09 (d12-pagine-js-css-lingue) |
| U-203 | NV | Tour dell'editor solo in italiano | `src/Vipi.Ui/wwwroot/vipi-tour.js:14` | d12-10 (d12-pagine-js-css-lingue) |
| U-204 | NV | Intestazioni delle tabelle pubbliche scritte a mano in inglese (SID/STAR, piste, frequenze, TA/TL, configurazioni) | `src/Vipi.Ui/Components/App/AirportSids.razor:34` | d12-11 (d12-pagine-js-css-lingue) |
| U-205 | NV | Elenco vSOP militari: l'introduzione nel riquadro blu è grigio su blu (contrasto ~2:1) nel tema chiaro | `src/Vipi.Ui/Pages/MilListPage.razor:30` | d12-12 (d12-pagine-js-css-lingue) |
| U-206 | NV | `var(--accent)` usato fuori da `.nav-card`: sparisce l'anello di fuoco su due tasti pubblici e lo stato «scelto» del Profile Swapper | `src/Vipi.Ui/wwwroot/vipi-theme.css:3819` | d12-13 (d12-pagine-js-css-lingue) |
| U-207 | NV | Maniglia delle immagini: sotto zoom di pagina la percentuale salvata è sbagliata del fattore di zoom | `src/Vipi.Ui/wwwroot/vipi-media.js:151` | d12-14 (d12-pagine-js-css-lingue) |
| U-208 | NV | Vista live: il collegamento «apri la vIPI» senza documento punta a «#» e porta fuori dalla pagina | `src/Vipi.Ui/Pages/LivePage.razor:143` | d12-15 (d12-pagine-js-css-lingue) |
| U-209 | NV | vipi-boot.js segna un modulo come caricato prima che arrivi: un caricamento fallito non si ritenta più nella scheda | `src/Vipi.Ui/wwwroot/vipi-boot.js:59` | d12-16 (d12-pagine-js-css-lingue) |
| U-210 | NV | Landing dell'ACC: aeroporti, APP e vLoA in evidenza sono `<li onclick>` e non collegamenti | `src/Vipi.Ui/Pages/AccLanding.razor:82` | d12-17 (d12-pagine-js-css-lingue) |
| U-211 | NV | Home vSOP sul telefono: la pastiglia AIRAC copre il titolo, e «offline/N online» copre il codice ACC | `src/Vipi.Ui/wwwroot/vipi-theme.css:941` | d12-18 (d12-pagine-js-css-lingue) |
| U-212 | NV | Campi sotto i 16px su pagine da telefono: iOS ingrandisce al fuoco (la regola a 16px ne copre solo sette) | `src/Vipi.Ui/wwwroot/vipi-theme.css:4656` | d12-19 (d12-pagine-js-css-lingue) |
| U-213 | NV | «Cosa è cambiato»: orario di pubblicazione in UTC senza «Z» | `src/Vipi.Ui/Pages/ChangedPage.razor:47` | d12-20 (d12-pagine-js-css-lingue) |
| U-214 | C | Senza METAR (o con NIL, o con «/////KT») la regola pista si valuta come vento calmo e pista asciutta, e la pista «in uso» si mostra lo stesso | `src/Vipi.Application/Awos/AwosComposition.cs:145` | d13-04 (d13-dominio-aeronautico) |
| U-215 | C | TAF: «PROB30 TEMPO …» produce una riga PROB vuota «dall'inizio della validità» e un TEMPO senza probabilità | `src/Vipi.Application/Weather/MetarParser.cs:117` | d13-06 (d13-dominio-aeronautico) |
| U-216 | C | Vista rapida: senza QNH il TL mostrato è quello della prima riga della tabella (la fascia ≤976, il più alto) | `src/Vipi.Ui/Components/App/AirportQuickPanel.razor:283` | d13-07 (d13-dominio-aeronautico) |
| U-217 | C | Quote AGL delle ATZ trattate come AMSL anche nell'attribuzione del traffico: le I_TWR degli scali in quota perdono il circuito | `src/Vipi.Application/Stats/SectorVolumeMap.cs:117` | d13-08 (d13-dominio-aeronautico) |
| U-218 | C | Turni: le riconnessioni con sovrapposizione di pochi secondi restano due turni per sempre (e dal vivo nessuna riconnessione rapida si unisce fino al giro notturno) | `src/Vipi.Application/Stats/AtcShiftGrouper.cs:59` | d13-09 (d13-dominio-aeronautico) |
| U-219 | C | «Aeroporti gestiti»: per gli APP conta il solo ICAO del callsign, e un APP che copre più campi ne accredita uno sbagliato | `src/Vipi.Infrastructure/Persistence/EfAtcStatsQueries.cs:293` | d13-10 (d13-dominio-aeronautico) |
| U-220 | P | Convertitore: «N-41.99» o «-41.99N» diventano latitudine SUD senza segnalazione | `src/Vipi.Application/Coordinates/CoordinateParser.cs:504` | d13-11 (d13-dominio-aeronautico) |
| U-221 | C | Convertitore: virgola decimale all'italiana separata da «, » («45,4642, 9,1900») si spezza in vertici validi e sbagliati | `src/Vipi.Application/Coordinates/CoordinateParser.cs:450` | d13-12 (d13-dominio-aeronautico) |
| U-222 | C | Convertitore: GeoJSON Feature/FeatureCollection non riconosciuto (va al parser a righe, lat/lon invertite o angoli spaiati); MultiPolygon e buchi scartati in silenzio | `src/Vipi.Application/Coordinates/CoordinateParser.cs:100` | d13-13 (d13-dominio-aeronautico) |
| U-223 | C | Regole e ripiego usano rotta = ident×10, il pannello vento del vAWOS la rotta dell'anagrafica: coda e «regola applicabile» si contraddicono | `src/Vipi.Application/Weather/RunwaySuggestion.cs:325` | d13-14 (d13-dominio-aeronautico) |
| U-224 | P | Ripiego sul vento con tre parallele (L/C/R): gli arrivi vanno sulla «C», non sulla sinistra | `src/Vipi.Application/Weather/RunwaySuggestion.cs:191` | d13-15 (d13-dominio-aeronautico) |
| U-225 | C | vAWOS: con vento VRB o calmo le caselle CROSS/TAIL dicono «00» al primo disegno, il JS «--» | `src/Vipi.Ui/Pages/AwosPage.razor:534` | d13-16 (d13-dominio-aeronautico) |
| U-226 | A | QFE del vAWOS con la retta dei 27 ft/hPa: 2–4 hPa di errore sugli scali pubblicati in quota, su una premessa scritta falsa | `src/Vipi.Application/Awos/AwosComposition.cs:114` | d13-17-v (d13-dominio-aeronautico) |
| U-227 | A | vAWOS: TA e tabella TL dall'anagrafica viva, mentre documento e vista rapida mostrano la sezione congelata della release | `src/Vipi.Application/Awos/AwosService.cs:126` | d13-19-v (d13-dominio-aeronautico) |
| U-228 | C | Consolidamento giornaliero degli aeroporti: un pilota che riconnette con un piano di volo nuovo conta due movimenti | `src/Vipi.Application/Stats/AirportCoverage.cs:97` | d13-07-g1 (d13-dominio-aeronautico-giro1) |
| U-229 | C | Convertitore di coordinate: il segnaposto «⟦R…⟧» scritto nel testo e un numero JSON oltre il double fanno cadere il circuito | `src/Vipi.Application/Coordinates/AipGeometryReader.cs:315` | d13-10-g1 (d13-dominio-aeronautico-giro1) |
| U-230 | PLAUSIBLE | Testo AIP: un arco senza verso dichiarato viene disegnato in senso orario, senza nessuna segnalazione | `src/Vipi.Application/Coordinates/AipGeometryReader.cs:538` | d13-12-g1 (d13-dominio-aeronautico-giro1) |
| U-231 | NV | Nessun test prova che l'avvio vero non lasci segnalazioni: il test che lo dice nel nome istanzia un report vuoto, e gli smoke accettano Degraded | `tests/Vipi.Hosting.Tests/StartupMaintenanceTests.cs:91` | d14-06 (d14-esercizio-qualita) |
| U-232 | NV | Mutazioni che sopravvivono nelle suite di lock e di cancelli: le porte si provano a campione | `tests/Vipi.Infrastructure.Tests/LockDellaStrutturaTests.cs:84` | d14-07 (d14-esercizio-qualita) |
| U-233 | NV | README, HANDOFF e ci.yml dicono il falso sulla rete di sicurezza | `README.md:54` | d14-08 (d14-esercizio-qualita) |
| U-234 | NV | /Error è mappato solo in GET: un'eccezione in POST /vsop/api/v1/transfers/resolve o nel PUT del ponte RFO esce come 405 vuoto, non come 500 | `src/Vipi.Host/VipiStartup.cs:649` | d14-09 (d14-esercizio-qualita) |
| U-235 | NV | Program.cs: il filtro «StopTheHostException» non scatta mai, e il commento descrive un comportamento che WebApplicationFactory non ha | `src/Vipi.Host/Program.cs:29` | d14-10 (d14-esercizio-qualita) |
| U-236 | NV | La finestra di modifiche per «Da fare» vive solo in memoria: se il processo si spegne entro 2 minuti dal salvataggio, la causa si perde e le righe arrivano solo col giro notturno | `src/Vipi.Application/Content/ModificheInAttesa.cs:46` | d14-11 (d14-esercizio-qualita) |
| U-237 | P | Circuiti Blazor anonimi senza tetto (smentita la parte sulla «ricerca a scansione piena») | `src/Vipi.Ui/Pages/SearchPage.razor:3` | d15-02 (d15-abuso-robustezza) |
| U-238 | A | I 25 posti per i circuiti staccati sono comuni a tutti: i lettori anonimi (o uno script) li riempiono, e l'editor che perde la rete viene ricaricato | `src/Vipi.Host/VipiStartup.cs:97` | d15-03-v (d15-abuso-robustezza) |
| U-239 | C | I registri del giorno (log e richieste, tetto 5 MB) si possono saturare con richieste anonime al ponte RFO: il resto del giorno la diagnostica tace | `src/Vipi.Host/RegistroGiornaliero.cs:89` | d15-07-g1 (d15-abuso-robustezza-giro1) |
| U-240 | P | MarkdownLite: le regex del sottolineato e del link allegato non hanno MatchTimeout e possono degenerare su una riga lunga, nel render pubblico | `src/Vipi.Ui/MarkdownLite.cs:79` | d15-08-g1 (d15-abuso-robustezza-giro1) |
| U-241 | C | «Pubblica ora» ripetuto crea ogni volta una release completa (payload intero, tenuto 13 cicli) e ricarica in memoria tutti i payload del bersaglio | `src/Vipi.Infrastructure/Persistence/EfReleaseRepository.cs:63` | d15-09-g1 (d15-abuso-robustezza-giro1) |
| U-242 | C | /vsop/media/{sha}: anche una risposta 304 legge dal database tutti i byte dell'immagine | `src/Vipi.Hosting/VipiModuleExtensions.cs:574` | d15-11-g1 (d15-abuso-robustezza-giro1) |
| U-243 | C | Porta delle API: una chiave ben formata ma falsa costa una query (SHA-256 + lookup per impronta) anche oltre il tetto per IP | `src/Vipi.Hosting/PortaDelleApi.cs:71` | d15-12-g1 (d15-abuso-robustezza-giro1) |
| U-244 | C | Quota immagini per documento aggirabile: sostituire un'immagine lascia l'originale orfano nel deposito, fuori dal conto | `src/Vipi.Ui/Components/ImageBlockEditor.razor:187` | d15-13-g1 (d15-abuso-robustezza-giro1) |
| U-245 | NV | Le sezioni STAR aggiunte dalla manutenzione d'avvio sono Frozen, quelle nate con il documento sono Live come le SID: 45 vIPI civili su 46 hanno SID Live e STAR Frozen | `src/Vipi.Infrastructure/Persistence/EfDocumentMaintenance.cs:559` | d16-04 (d16-allineamento-documenti) |
| U-246 | NV | Due nascite ancora fuori dalla regola del catalogo: la vLOA nasce con «Validity and Revision» Frozen, e i blocchi della vIPI ACC nascono con titoli sempre italiani e senza pubblico e nascosta di catalogo | `src/Vipi.Infrastructure/Persistence/Seed/VloaStructureSeeder.cs:22` | d16-05 (d16-allineamento-documenti) |
| U-247 | NV | «Sposta in…» ha ancora il tetto di profondità 3, mentre MaxDepth è 5 dal 16-set: le destinazioni valide ai livelli 3-4 non vengono offerte | `src/Vipi.Application/Content/SectionMoveTargets.cs:51` | d16-06 (d16-allineamento-documenti) |
| U-248 | NV | Le passate d'avvio toccano solo l'ultima versione: con una bozza aperta la versione pubblicata resta vecchia, e dopo «Scarta bozza» la nuova bozza torna alla struttura di prima fino alla consegna successiva | `src/Vipi.Infrastructure/Persistence/EfDocumentMaintenance.cs:418` | d16-07 (d16-allineamento-documenti) |
| U-249 | NV | La riconciliazione dei titoli d'aeroporto riscrive solo le radici: nelle vIPI civili in inglese le figlie di catalogo restano in italiano, e la riscrittura apre una riga «da ripubblicare» che nessun lettore può vedere | `src/Vipi.Infrastructure/Persistence/EfDocumentMaintenance.cs:1220` | d16-08 (d16-allineamento-documenti) |
| U-250 | NV | Il timbro delle riconciliazioni non conta lo spostamento della «Gestione del traffico», contro quanto dice il commento | `src/Vipi.Hosting/VipiModuleExtensions.cs:1102` | d16-09 (d16-allineamento-documenti) |
| U-251 | C-vivo | Editor aeroporto e MIL a 768 px: la colonna delle versioni sfora di 24 px | `src/Vipi.Ui/wwwroot/vipi-theme.css` | v-05 (dal-vivo) |
| U-252 | C-vivo | Tour guidato scritto solo in italiano, anche con l'interfaccia in inglese | `src/Vipi.Ui/wwwroot/vipi-tour.js:14` | v-06 (dal-vivo) |
| U-253 | C-vivo | Intestazioni e titoli rimasti in italiano con l'interfaccia inglese | `src/Vipi.Ui` | v-07 (dal-vivo) |
| U-254 | C-vivo | «All screens» dice «tutte le pagine» ma ne elenca 14 su 58 (indice della mockup v2) | `src/Vipi.Ui/Pages/ScreensIndex.razor` | v-08 (dal-vivo) |
| U-255 | C-vivo | Bersagli al tocco sotto 24×24 px su statistiche, «Cosa è cambiato», archivio e aiuti | `src/Vipi.Ui/wwwroot/vipi-theme.css` | v-09 (dal-vivo) |
| U-256 | C-vivo | Lock tenuto da un altro: il tasto «✎ Edit» resta acceso e, premuto, sparisce senza dire perché | `src/Vipi.Ui` | v-10 (dal-vivo) |


---

## 4. Lotti di correzione

Nessun lotto richiede una migrazione di schema. Serve una **correzione di dati** (U-014: la riga 5720 di Perugia
Approach) e una **azione in produzione** senza codice (U-009 prima del 1-ott; U-018 accendere `Api:RichiediChiave`).

| # | Lotto | Findings | Note e dipendenze |
|---|---|---|---|
| **L0** | **Prima del 1° ottobre, in produzione** | U-009 (gesto), U-018 (configurazione) | Nessun codice. U-009: annullare la #187 di LIBV_APP o ripubblicare al 2610. U-018: dare la chiave al validatore dei tour, poi `Api:RichiediChiave=true` |
| **L1** ✅ | **Pubblicazione e release** — corretto il 27-set (sito S10, `docs/filoni/sito.md`) | U-009 (codice), U-006, U-017, U-013, U-014 | U-009 nel codice: una «Pubblica ora» deve superare le programmate più vecchie, o avvisare. U-006: il backfill solo una volta, mai la bozza. U-017: sentinella + porta in `ReleasePanel.Run`. U-014: le passate d'avvio riscrivono la Depth del sottoalbero, e `CreateDraftAsync` non si fida dell'ordine |
| **L2** ✅ | **Scritture che si perdono o si sovrascrivono** — corretto il 27-set (sito S11) | U-011, U-010, U-016, U-051, U-109 | U-011: rileggere al passaggio del lock e scrivere delta, o un token di concorrenza. U-010: salvare le righe valide e dire quale riga blocca. U-016: alzare `MaximumReceiveMessageSize` e mandare i testi lunghi a pezzi; comunque dire all'utente che non si è salvato. U-051/U-109: lock nel servizio (T-004/T-025 rinati) |
| **L3** ✅ | **Circuito che cade** — corretto il 27-set (sito S12; U-237, tetto nel codice su scelta del committente: sito S13) | U-012, U-017, U-108, U-237 | Stessa regola già pagata: ogni gesto dentro la porta (`InFilaAsync`) con sentinella. Controllo dopo la correzione: `doppio-clic` su tutte le pagine staff |
| **L4** ✅ | **Procedure e sectorfile** — corretto il 27-set (sito S14, S15, S16; U-005 senza migrazione) | U-003, U-004, U-005, U-031, U-032, U-033 | U-004/U-005 prima: la StableKey non deve contenere né il fix risolto né perdere la transizione dopo il «-». Test con i casi veri (LIRF XIB5A-*5R/*6A, LIME ILF1x-VOG1K/VOG1S, LIBG ROBO1H/ROBO5H) |
| **L5** ✅ | **Documenti uniti** — corretto il 27-set (sito S17; U-008 senza colonna nuova) | U-007, U-008, U-052 | Il confronto «in comune» per contenuto, non per chiave; lo scioglimento rimette visibili le sezioni cedute (o lo chiede) |
| **L6** ✅ | **Superficie pubblica** — corretto il 27-set (sito S18; U-001 senza login e U-018 default `true` nel codice, scelte del committente) | U-001, U-020, U-018 (codice: la regola), U-237 (✅ in L3, sito S13) | U-001: login o tetto complessivo + diff lineare. U-020: encode del nome nel tooltip Leaflet (la 3D lo fa già) |
| **L7** ✅ | **Import IVAO** — corretto il 27-set (sito S19, S20, S21; U-028 senza declassamento automatico, scelta del committente) | U-002 e gli S3 di d04 | U-002 è NV: prima un test rosso (client finto che risponde 403 → oggi elenco vuoto) |
| **L8** ✅ | **Dominio e vista rapida** — corretto il 27-set (sito S22; U-091 resta il vento medio e U-093 «///» sotto 1500 m, scelte del committente) | U-015, U-094 e gli S3 di d13 | U-015: TA/TL dal pubblicato come il documento; FL sopra la TA |
| **L9** ✅ | **Design su telefono e tablet** — corretto il 28-set (sito S23; con gli NV confermati della carta d12: U-085…U-088, U-201…U-203, U-205, U-206, U-211, U-212) | U-106, U-107, U-251…U-255 | CSS e JS: il pacchetto porta wwwroot. Misura prima/dopo con la stessa sonda (parole spezzate, scrollWidth) |
| **L10** ✅ | **Allineamento dei documenti** — corretto il 28-set (sito S24; U-245 anche sulle STAR esistenti e U-105 a mano, scelte del committente) | U-105, U-245, U-246, U-248, U-249 (+ U-009, U-014) | Vedi §7 |
| **L11** | **Il resto** — a fette per area, decise col committente il 28-set: **A** import SID/sectorfile ✅ (sito S25; U-037 in S26, con migrazione), **B** lock/release/unioni ✅ (sito S27; U-059 = U-051 e U-140 = U-109, già chiusi in S11), C log e superficie anonima, D circuito e doppio clic, E import da testo/XLSX, F derivazioni e dati, G dominio aeronautico, H carte e commenti falsi, I migrazioni all'avvio | S3/S4 per dimensione | Da prendere per area di codice quando si tocca quell'area; gli NV partono da una prova rossa |


---

## 5. Confutati

| Id | Dimensione | Titolo | Perché cade |
|---|---|---|---|
| d05-01 | d05-sectorfile-procedure | Commento a riga intera dentro DYNAMIC_SEC: CTR di Brindisi troncato, LIEE_MIL_APP senza area | Le aree vengono da IVAO (ShapeSource='Source' ovunque): LIBB_* 52 vertici, LIEE_MIL_APP 34. Il ripiego tocca solo settori senza area e la shape IVAO resta (EfAccAdminRepository:303-313). Parser latente, in d05-15. Dal vivo ❌. |
| d06-01 | d06-altri-ingressi | La rinomina non riscrive le chiavi AppMil | AppMil non esiste: nessuna porta lo crea (MilDocRoutes.cs:42-47) e MilDocumentId si scrive solo su Airports (EfMilitaryDocumentService.cs:151). Copia: 0 Sectors con MilDocumentId, 0 release/incarichi AppMil. Latente: va aggiunto con le pagine. |
| d06-15 | d06-altri-ingressi | L'editor della tabella generica perde tableId/unified/primary/star/group/r | Su 982 blocchi Table: 0 con star/primary/tableId/group/r, 1 solo con unified=false, il default (TableBlock.razor:53). TableBlock scrive «nessun corpo salvato oggi li usa». Quelle chiavi le scrivono solo i semi Roma di sviluppo. Latente. |
| d07-18 | d07-motore-lock-release | Anteprima bozza: le sezioni radice si ordinano senza spareggio sull'Id | OrderBy LINQ stabile e righe già in ordine di Id dall'indice InnoDB (DocumentVersionId, ParentSectionId, Order, PK), come da EXPLAIN sulla copia: stesso risultato di ThenBy(Id). Nella copia 0 radici con Order doppio. |
| d09b-20 | d09b-pagine-admin | Nuovo documento: il lock «nuovo documento» è solo a schermo; il servizio non lo verifica | Solo NewDocumentPage chiama il servizio, dietro il lock editor:newdoc (un detentore): il secondo Editor trova la vLOA (EditingService 76-83) ed è rifiutato. Link «apri» errato irraggiungibile: Home/Neighbour disgiunti (312/315), titolo obbligatorio. |

## 6. Copertura

| Dimensione | Non coperto (dichiarato) |
|---|---|
| d01-identita-accesso | Le pagine grandi lette solo nei punti di chiamata e nei cancelli, non gesto per gesto: AdminTrasferimentiPage (3.739 righe), StrutturaPage, AeroportiPage, AccAdminPage, ConfinantiAdminPage, VersioniPage, MilSectionsEditor, AirportSectionsEditor. · La logica interna dei repository di scrittura oltre alla presenza della guardia (correttezza dei dati, transazioni): è di altre dimensioni. · Nessuna prova dal vivo né build/test (regola del giro): il comportamento di IHttpContextAccessor dentro il circuito e dopo una riconnessione è ragionato, non misurato. · Il meccanismo di Blazor che tiene validi gli id dei gestori fino alla conferma del render (d01-09) è dedotto, non provato a schermo. · Limi… |
| d02-input-superficie | Prova a schermo/HTTP di tutti e tre gli scenari dal vivo (revisione di sola lettura): d02-01, d02-02 e l'Origin del WebSocket /_blazor non chiusi dalla lettura · Contenuto per intero dei .resx (SharedResource it/en, ~2500 stringhe): controllata la difesa (FraseHtml guard + resx trusted) ma non ogni singola stringa con markup+argomenti · src/Vipi.Application/Content/{RiferimentiResolver.cs} e i costruttori dei ValoriDato/NomiProcedura dalle sorgenti IVAO/sectorfile: letto il rendering (encoda) ma non l'intera catena di risoluzione · La maggior parte dei ~128 Components/*.razor oltre a quelli citati (AirportSectionsEditor, MilSectionsEditor, UnionPanel, ReleasePanel, AdminTrasferimentiPage 37… |
| d03-segreti-dati-infra | Il resto dei ~90 fogli LEGGIMI-* di deploy/atc-ivao: visti solo con grep mirati, non letti riga per riga · deploy/cloudflare/atc-archiver/index.js per intero: è il Worker del validatore dei tour, non la vIPI · La catena reale Cloudflare → nginx di Plesk → Passenger → Kestrel, le intestazioni che arrivano e il pannello Cloudflare (WAF, Authenticated Origin Pulls): da qui non si leggono, e T-020 resta non misurato · L'appsettings.json davvero presente sul server: la rete dei segreti lo tiene fuori dai pacchetti dal 1.5.0 e da qui non si verifica (per esempio se ha Auth:FounderVids) · Lo stato reale di Render (variabili nella dashboard, se il servizio è ancora vivo): d03-04 dipende da quello ·… |
| d04-import-ivao | EfSectorProjectionService.SyncFromCatalogsAsync: la concorrenza fra proiezioni lanciate insieme (giro anagrafica + giro settori + import manuale) non è verificata · EfCallsignRenameService (rinomine dentro gli import): T-029 non riverificato · ForeignAccFetcher, NeighbourAdjacencyComputer, ForeignSectorResolver (solo le chiamate al client) · StaffRosterService.VerifyAllAsync (il corpo della verifica del roster) e IvaoUserClient oltre la lettura · ImpactDriftHostedService, DerivaDopoLeModificheHostedService, ReleaseSweepHostedService, TranslationFillHostedService: registrati in AddVipiIvao ma non sono import · EfAirportRepository oltre le parti di merge (righe 1-160 e 550-798 solo scorse) · … |
| d05-sectorfile-procedure | src/Vipi.Sectorfile/** (non è un ingresso del sito) · AuroraSectorShapeProvider, AuroraTowerShapeProvider, AuroraSectorfileFactsProvider, SectorfileComparison*: non riletti dal verificatore · Durata dei processi Passenger in produzione (serve a d05-06) · Nessuna app avviata, nessun test eseguito; il repo della divisione (ivao-italy) non confrontato col fork |
| d06-altri-ingressi | EfTranslationMemory, EfStatoTraduzione, DeepLTranslationEngine (limite 128 KiB per richiesta non verificato) · Meteo (NOAA/IVAO/VATSIM): non riverificato, il lettore non ha trovato difetti · Nessuna prova dal vivo sulle istanze locali per i findings d06; il tetto di memoria del processo su Plesk non è verificato · Limite Azure di 50 000 caratteri per richiesta preso dalla documentazione, non da una chiamata |
| d07-motore-lock-release | IEditingService.cs, EditingModels.cs, SectionCatalogBridge.cs, SectionDescriptor.cs, SectionKeys.cs, SectionProfile.cs, SectionOrdering.cs, SectionMoveTargets.cs, SectionDirection.cs, TitoliDiCatalogo.cs, Outline.cs, MilitaryDocuments.cs: modelli e funzioni pure, non letti riga per riga (usati solo attraverso i chiamanti) · FrozenTranslation.cs (corretto in T-065, non riletto), AppFrozenSectionProvider.cs, AccFrozenSectionProvider.cs · EfStructureEditingRepository.cs ed EfDocLinkStructureSource.cs: non letti (visti solo i servizi che li chiamano) · ReleaseTargetRegistry.cs (21 righe) non aperto · DocumentSectionsEditor e il resto dei grandi editor (AirportSectionsEditor/MilSectionsEditor/Ap… |
| d08-derivazioni | Test ed esecuzione (regola): nessuna prova rossa eseguita · Stato di produzione dopo il 26-set 16:29Z (la release 187 di LIBV_APP potrebbe essere stata annullata nel frattempo) · AppDocumentAssembler/AppFrozenSectionProvider: se l'APP accoda sezioni di catalogo come l'ACC (utile per d08-16-v e d08-02) · Resa di ComposeLead e Outline.ParentOf per l'eccezione orfana (d08-06) guardata solo dai chiamanti |
| d09a-editor-documenti | AccEditorPage, VloaEditor, AppEditorPage/MilEditorPage oltre ai punti citati · InlineConfirm, ConversioneSidPanel, ImportaTabella, AirportSidsEditor (solo i richiami) · Nessuna prova eseguita dal verificatore: né build, né test, né app (regola del giro); tempi delle corse di d09a-04/07/08 non misurati |
| d09b-pagine-admin | Nessuna prova dal vivo eseguita da questo verificatore (niente app): le prove rosse proposte sono da scrivere; d09b-01 è l'unica già rossa (quaderno). · Durata reale di SyncFromCatalogsAsync in produzione (ampiezza della finestra di d09b-02): stimata dal codice, non misurata. · Pagine lette dal lettore e non riaperte qui: AuditPage, AdminTasksPage (tranne il cancello), DatabaseBackupCard, DeleteDialog, TypeaheadPicker, EditLockBar (solo parametri). · WorkItemList/WorkItemRow, OrphanSectorService, DeletionService, ImpactDriftUseCase (restano come nel lettore). |
| d09b-pagine-admin-giro1 | I 14 finding del primo giro che coincidono col secondo: non riverificati (lo fa l'altro agente); i «punti in più» segnati come non riverificati restano da controllare. · d09b-13-g1: la finestra di corsa (blur → click prima del ridisegno) non è misurata, e non è verificato se Blazor Server consegna il click a un gestore il cui tasto è stato appena spento. · d09b-02-g1: non ho letto come CoordinationDerivation compone una riga a profondità 1 senza capofila (caso del gruppo che resta con più righe); ho verificato solo il caso del gruppo sciolto. · Nessuna build, nessun test, nessuna app: solo lettura e SELECT sulla copia. |
| d10-dati | Nessuna compilazione né test eseguito (regola del giro): d10-05 si regge sul comportamento documentato di EF 8.0.31 e contraddice un test esistente; va chiuso eseguendo quel test isolato · PonteDelleForme.cs solo scorso: il ritentativo del ponte dentro una transazione esplicita dopo un deadlock InnoDB (che annulla l'intera transazione) non è stato verificato · Concorrenza fra due processi Passenger che eseguono insieme le manutenzioni d'avvio dopo un carico: nessun lock fra processi (il reconciler Postgres ce l'ha, MySQL no); ragionato, non provato. I token di concorrenza delle sezioni fermano quasi tutti i doppioni tranne l'aggiunta in coda a un gruppo · Snapshot SQLite e Designer delle mi… |
| d11-concorrenza-circuito | BackgroundService non letti riga per riga (solo registrazione e cadenza): AccImport, SpecialAreaImport, AtcHistoryImport, AirportTrafficBackfill, AirportTrafficRollup, TrafficRetention, StaffRosterVerification, ProcedureImport, NavaidImport, SectorfileComparison, TranslationFillHostedService, MemoriaDelProcesso. La scrittura concorrente di AtcSessions fra poller e storico e la proiezione dei settori senza lucchetto sono state ragionate ma non chiuse (EF aggiorna solo le colonne cambiate, finestre di millisecondi): nessun finding. · Componenti OwningComponentBase non letti gesto per gesto: AirportListPanel, AirportQuickPanel, AttachmentBlockEditor, ImageBlockEditor, ImportaTabella, MediaClea… |
| d12-pagine-js-css-lingue | Nessuna verifica a schermo: tutte le affermazioni responsive (d12-04…d12-08, d12-18, d12-19 e responsive.md) sono dedotte dal CSS e vanno misurate dal vivo. · GuidaPage.razor (1923 righe): non letta; i corpi sono testo statico bilingue in linea (eccezione dichiarata). · Markup completo di StatsHome, StatsDivisionPage, StatsSessionPage, AtcWorldArchivePage, AeroportoPage, AppnPage, VloaListPage, MilDocumentPage, AirspacePage, AppsListPage, ScreensIndex: letti solo per cancelli, isole e classi di layout. · Componenti montati dalle pagine-documento non aperti: AccSectionBody, AccCoordinationView, RegulatedAreas, MilWorkingAreas, VloaDocumentView, AppDocumentBody, MilDocumentBody, PrintMeta, Co… |
| d13-dominio-aeronautico | Nessuna prova dal vivo sul tema d13 nel quaderno; nessun test eseguito (regola del giro) · Frequenza reale di VV///, RVR tutti P, PROBnn TEMPO e /////KT nei bollettini italiani: non misurata (niente rete) · Comportamento delle scorte IVAO/VATSIM sui METAR vecchi (restituiscono o no un bollettino di ore fa): non verificato · Stats: TrafficTimeline, AirportCoverage, AirportBackfillPlanner, AirportTrafficBackfillUseCase, AirportTrafficRollupUseCase · Live: LiveModels, LiveViewService, TransferOnlineResolver · Airspace: AirspaceKmlReader, AirspaceNavaidReader, ISectorShapeResolver (implementazione) · AuroraSectorfileParser (lato Infrastructure) |
| d13-dominio-aeronautico-giro1 | I sei finding già nel secondo giro non sono stati riverificati, come da consegna · Nessun programma eseguito: che GetDouble lanci su «1e999» è ragionato su System.Text.Json (Utf8Parser dà infinito oppure false, e in tutti e due i casi TryGetDouble torna false), non provato · Nessuna prova dal vivo sulle istanze locali, per la regola del giro · Non è misurata la frequenza reale dei METAR con visibilità sotto i 1500 m senza RVR sugli scali pubblicati (d13-03-g1) · Il comportamento della sorgente IVAO alla riconnessione (piano nuovo) viene solo dal commento misurato in EfAtcTrafficStore e dal proxy dei FlightPlanId distinti; la sorgente non è stata interrogata |
| d14-esercizio-qualita | Nessuna build, nessun test e nessuna mutazione eseguiti (vincolo di sola lettura): le mutazioni di d14-06 e d14-07 sono ragionate e verificate per grep, non lanciate · docs/filoni/coordinamenti-aeroporti.md, da-fare.md, lab.md, lista-da-fare.md, lock-uniti.md (filoni chiusi o del Lab): non letti · HANDOFF.md oltre le prime 120 righe (letto solo per grep) e le note di rilascio in Directory.Build.props: non verificate voce per voce · Suite di import (ImportCheNonPerdonoDatiTests, AccImportTests, AirportDataImportTests…), di release (ReleaseGenericFlowTests, ReleaseRepositoryTests…), AuthLockTests, ResourceLockTests, DocumentAdminLockGuardTests: non lette; RegistroAvvisiTests e RegistroDelGior… |
| d15-abuso-robustezza | sorgente del framework (CircuitRegistry, MemoryCache con SizeLimit): comportamento al riaggancio preso dalla documentazione · prove dal vivo di d15-01, d15-02 e d15-03-v: non eseguite (solo lettura) · voci del primo giro del lettore non ripresentate (tetto SSE per utente, cache aggirabile, cookie vecchio e API IVAO, tetto dei registri, 304 del media): non verificate a fondo |
| d15-abuso-robustezza-giro1 | Verifica dal vivo delle soglie e degli esaurimenti (fatta solo per lettura del codice) · Vipi.Sectorfile come ingresso (parser): non riletto qui · Numero di circuiti per IP e pool dei circuiti staccati (25): solo ragionato · d15-02-g1 (SignalR 32 KB): non ri-verificato, è lo stesso difetto del 2° giro (d15-01), in verifica su altro agente · Effetto a valle sul throttling del token app IVAO (d15-05-g1): non dimostrabile senza IVAO |
| d16-allineamento-documenti | Seed di sviluppo Roma* (RomaAirportSeed, RomaContentSeed, RomaStructureSeed, RomaVloaSeed): non letti, non girano in produzione · Percorsi d'import (AirportImportUseCase, ProcedureImporter, Vipi.Sectorfile) per quanto riguarda la creazione di sezioni: non letti. Nei dati non ho trovato sezioni fuori catalogo che li chiamino in causa · Corpi razor per famiglia (AppDocumentBody, AirportDocumentBody, MilDocumentBody, AccSectionBody, VloaDocumentView): non letti per intero. La resa delle sezioni vuote e delle profondità è dedotta da VipiViewService e SectionNode, e va confermata dal vivo (scenari 2 e 3) · I lettori che mostrano i titoli senza passare da TitoliDiCatalogo (ricerca, «Cosa è cambia… |

---

## 7. Allineamento dei documenti (richiesta del committente)

Misurati 86 documenti sulla copia di produzione (46 vIPI d'aeroporto, 17 vSOP militari, 18 APP, 4 vIPI ACC,
1 vLOA): versione di lavoro, release in vigore e l'unica release programmata. La regola del design (catalogo per
profilo, sezioni obbligatorie, nascoste di nascita, libere, ordine libero fra fratelli, titoli a view-time) e la
tabella completa stanno nell'allegato C.

**Non previsti dal design** (diventano findings):

| # | Dove | Differenza | Finding |
|---|---|---|---|
| N1 | LIBV_APP | la release programmata #187 rimette dal 1-ott un albero vecchio (niente «Gestione del traffico», VFR radice, sezioni nascoste visibili) | U-009 |
| N2 | Perugia Approach (LIRZ_APP) | «Note» sotto VFR con profondità incoerente: «Crea bozza» fallirà dopo la prima pubblicazione | U-014 |
| N3 | Perugia Approach | «Gestione del traffico» due volte (una libera con l'IFR scritto a mano, una di catalogo vuota) | U-105 |
| N4 | vIPI Brindisi, blocco CS0 | IFR/VFR di catalogo vuote accanto a tre sezioni libere con l'IFR vero | U-105 |
| N5 | 45 vIPI d'aeroporto su 46 | STAR Frozen mentre le SID sono Live (la carta dice «nasce Live come le SID») | U-245 |
| N6 | vLOA | «Validity» Frozen (dovrebbe essere sempre Live) | U-246 |
| N7 | LIRP, LIRS, LIRL (inglesi) | titoli di tre sotto-sezioni ancora in italiano; apre una deriva spuria su LIRL | U-249 |
| N8 | vSOP LIBA | STAR solo nella bozza, non nella versione da cui «Scarta bozza» ricopierebbe | U-248 |

**Previsti dal design** (nessuna azione): sezioni nascoste a mano o dalle unioni, ordine diverso fra fratelli,
sezioni libere, titoli nel DB nella lingua di nascita (resi a view-time), release vecchie con la struttura di allora
(si allineano alla ripubblicazione).

---

## 8. Allegati

In `docs/history/revisione-totale-3/`:

- **A — `matrice-scritture.md`** (d01): tutti i metodi che scrivono, con il livello e il lock controllati **nel
  servizio**. Da lì vengono U-051 e U-109 (lock solo a schermo).
- **B — `prove-di-rottura.md`** (d15): l'elenco delle prove di abuso proposte, da ripetere dopo le correzioni di L3/L6.
- **C — `allineamento-documenti.md`** (d16): regola del design per profilo, non previsti e previsti, controlli vuoti.
- **D — `design-responsive.md`** (d12): pagine e componenti a rischio su telefono e tablet, con la regola CSS.
- **`registro.json`**: tutti i findings con meccanismo, scenario, prova, correzione e origine.

- **`giro-rotte.mjs`**: le 58 rotte con quattro istanze (una per identità), stato, lunghezza, intestazioni. Da
  rilanciare dopo L6 e prima di un pacchetto che tocca i cancelli.
- **`doppio-clic.js`**: da incollare nel browser su una pagina staff; doppio clic nello stesso istante su ogni
  elemento cliccabile che non scrive, si ferma al primo circuito caduto. È il controllo di chiusura di L3.
