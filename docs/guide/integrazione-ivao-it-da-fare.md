# Integrazione nel sito Ivao.It — che cosa manca ancora

**Aggiornato:** 1 agosto 2026 · **Branch:** `integrazione/ivao-it` · **Tag consegnabile:** `embed-v1.0`

Questo documento elenca **tutto ciò che resta da fare** perché il modulo vIPI/vLOA giri davvero dentro
`Ivao.It.Website`. Non ripete come si integra (quello è `integration.md` e, lato loro,
`INTEGRAZIONE-VIPI.md` dentro `ivao-it-wiring.patch`): elenca **il lavoro aperto**, chi lo deve fare e
che cosa succede se non viene fatto.

## Stato: che cosa è già chiuso

Perché ciò che segue si legga senza rileggere la storia del branch.

| Fatto | Verificato come |
|---|---|
| Librerie multi-target `net8.0;net10.0` | build net8 0 warning; suite net10 816 test verdi |
| Migration EF10 applicabili da EF8 | sonda net8: 65 migration applicate su SQLite sotto EF Core 8.0.29 |
| Licenza Apache 2.0 + NOTICE + third-party | — |
| Job CI `build-net8` | comandi eseguiti verbatim in locale prima del commit |
| Wiring lato host (7 file) | il loro `Ivao.It.Website` **compila** col modulo agganciato |
| Persistenza in due forme (SQLite + Postgres) | i tre `appsettings` parsano; build rifatta dopo la modifica |

**Il confine di ciò che è provato è la compilazione.** Nessuno ha mai *eseguito* il modulo dentro un
host net8: vedi §2.1, che è il rischio tecnico più grosso rimasto.

---

## 1. Bloccanti — senza questi non si parte

### 1.1 Visibilità del repository del modulo
Se `github.com/SkyMistery/Digital_vIPI` è **privato**, il `git submodule add` **fallisce da loro** e
fallisce nella loro CI. Serve una scelta: invitarli come collaboratori (read) oppure rendere pubblico
il repo. **A carico nostro.** È il primo punto in cui l'integrazione si blocca e non dipende dal codice.

### 1.2 Consegna della patch
La PR non è aperta: `gh` non è installato sulla macchina di sviluppo, quindi fork e PR via API non
sono stati creati. Loro ricevono `docs/guide/ivao-it-wiring.patch` e la applicano con `git am`.
**A carico nostro** decidere se aprire la PR a mano dall'interfaccia GitHub.

### 1.3 Scelta del database — **risposto: solo MySQL**
Il modulo oggi supporta SQLite e PostgreSQL, non MySQL. La domanda posta non era «quale DB preferite» ma:
il sito gira su **una sola istanza** con **disco persistente**, e potete affiancare un **PostgreSQL** al
MySQL?

**Risposta di Ivao.It (1 agosto 2026): solo MySQL**, con il consiglio di usare il connector Pomelo.
Quindi cade sia la strada Postgres sia quella SQLite, e si apre il progetto MySQL: vedi
[`docs/design/piano-supporto-mysql.md`](../design/piano-supporto-mysql.md) e §4.1 qui sotto.

**Gate:** il piano è scritto ma non eseguibile finché non sappiamo la **versione del loro server MySQL**
(8.0+ / 5.7 / MariaDB). È l'unica risposta che blocca: decide la strategia di collation, che decide lo
schema. Le altre tre domande (database dedicato con DDL, libertà sulla collation, backup) stanno nel §1
del piano, con un messaggio già pronto da inviare in appendice.

### 1.4 Credenziali app IVAO — c'è un problema aperto **nostro**
Servono `Ivao__ClientId` / `Ivao__ClientSecret` con grant `client_credentials` e scope `tracker` +
`configuration`. **Oggi il token app fallisce con `POST /v2/oauth/token → 400`** (vedi `HANDOFF.md`):
la diagnosi è che non è codice ma il secret o i grant dell'app sul portale IVAO.

Senza token app il modulo **parte lo stesso**, ma perde **live ATC** e **verifica del roster staff** —
cioè la vista live e il popolamento del dropdown dei permessi. Va risolto prima di dire che
l'integrazione è completa, altrimenti consegniamo un modulo dimezzato e sembrerà colpa dell'host.

---

## 2. Verifiche tecniche mai eseguite

### 2.1 Eseguire il modulo dentro un host net8 — il gap più grosso
Abbiamo **compilato**, mai **avviato**. Build verde non dice nulla su:
- comportamenti runtime di **EF Core 8** rispetto al 10 sulle stesse query (il modulo è sviluppato e
  testato solo su EF 10);
- rendering dei componenti della RCL sotto **ASP.NET Core 8** (differenze di render mode, streaming
  rendering, `enhancedload`);
- lo **stream SSE** `/vsop/live/atc` dietro la loro pipeline;
- la collisione di rotta nota: `/vsop/live/{callsign}` contro il prefisso SSE `/vsop/live/atc` — vince
  il segmento letterale, ma è una proprietà del routing, e la pipeline dell'host è diversa dalla nostra.

**Come chiuderlo senza aspettare loro:** montare un host net8 minimo di prova (o riusare l'albero di
`Ivao.It-master` già usato per verificare il build) e guidare le pagine reali con la skill
`verifica-live`. È lavoro nostro e si può fare subito.

### 2.2 Doppia localizzazione — conflitto probabile, mai provato
Il modulo registra **`AddLocalization` + `UseRequestLocalization`** (culture `it`/`en`) dentro
`AddVipiModule`/`UseVipiModule`. Il loro sito registra **`AddIvaoItLocalization()` + `UseIvaoItLocalization()`**,
con un cookie di cultura scritto in `App.razor`. Nella pipeline patchata ci sono quindi **due middleware
di request localization**, e `UseIvaoItLocalization()` gira **dopo** `UseVipiModule()`.

Chi vince decide la lingua delle pagine `/vsop`. Sintomo atteso se va storto: il modulo ignora la
lingua scelta col loro `CultureSelector`, o viceversa il sito cambia lingua entrando in `/vsop`.
**Da provare a runtime**, ed è probabile che serva un flag per non registrare la localizzazione del
modulo quando l'host ne ha già una.

### 2.3 Endpoint di health anonimi
`/vsop/health` e `/vsop/health/ready` sono mappati **senza autorizzazione**. Sul nostro host standalone
va bene; su un sito pubblico `/vsop/health` espone il **report di consistenza dei dati**. Decidere se
proteggerlo (staff-only) o lasciare pubblica solo la sonda `ready`. **Modifica al modulo**, non alla patch.

### 2.4 Convivenza CSS con Bootstrap
Gli stili del modulo sono confinati sotto `.vipi-root` e non toccano `body`/reset. Ma il loro sito
carica **Bootstrap 5.3.3 e animate.css globalmente**: sono loro a poter sbavare dentro `.vipi-root`,
non il contrario. Mai verificato visivamente. **Da guardare con gli occhi**, non con i test.

---

## 3. Lavoro di prodotto, non di codice

### 3.1 Punto d'ingresso nel sito
Con `Vipi:RenderTopbar=false` il modulo non ha una propria barra: **serve una voce nel menu del sito**
che porti a `/vsop`. Il loro menu è dati nel CMS (tabella menu items), non codice — quindi è una riga
da inserire a DB, non nella patch. Senza, le pagine esistono ma **non sono raggiungibili**.

### 3.2 Dove vivono i dati veri
Oggi il modulo gira su Render+Neon con un DB di test. All'integrazione va deciso **quale DB è quello
buono** e travasati i contenuti reali (`tools/Vipi.DbSeed`). Va anche deciso se il deploy Render resta
in vita come ambiente di prova o si spegne: due istanze che scrivono documenti diversi sono la
premessa di un disallineamento silenzioso.

### 3.3 Backup e retention
Il DB del modulo **non è nel dump del MySQL**. Va aggiunto al loro piano di backup, qualunque provider
si scelga. La retention delle pubblicazioni è già automatica lato modulo.

### 3.4 Processo di aggiornamento versione
Loro pinnano `embed-v1.0`. Serve concordare chi tagga il nuovo rilascio, con che cadenza, e che cosa
garantisce un tag (oggi: build net8 + suite net10 + migration verificate). Senza processo, o restano
fermi per sempre o inseguono un branch che si muove.

---

## 4. Opzionali — da aprire solo se lo chiedono

### 4.1 Supporto MySQL — **non più opzionale: è la strada scelta** (§1.3)
Non è una configurazione, è un progetto. Piano completo, slice e stime:
[`docs/design/piano-supporto-mysql.md`](../design/piano-supporto-mysql.md). In sintesi:

- **MySQL sarà supportato solo sul TFM net8**, quello dell'embedding. Verificato l'1 agosto 2026:
  `Pomelo.EntityFrameworkCore.MySql` è fermo alla **9.0.0** (EF Core 9, pubblicata ago-2025), il repo non
  ha commit su `main` da allora, e i quattro tentativi di porting a EF Core 10 sono o aperti da mesi
  (#2007, #2019) o **chiusi senza merge** (#2031, #2032, #2042). Per net8 serve la **8.0.3**, stabile.
  Il ramo net10 (deploy Render+Neon) resta Postgres + SQLite.
- Il lavoro vero non è il provider: è **collation** (MySQL è case-insensitive di default e il modulo ha
  ~10 indici unici su stringa, più hash content-addressed), **`HasMaxLength`** su tutte le colonne
  indicizzate (oggi 6 in tutto il modello; senza, InnoDB non indicizza `longtext`) e un **set di migration
  dedicato** (le 65 esistenti sono SQLite-flavored).
- Due slice sono **indipendenti dal provider e fattibili subito** (lunghezze + test guardia sul modello):
  migliorano il modello anche per SQLite e Postgres e non si buttano se MySQL cambiasse.
- Stima: **4-5 sessioni**, di cui l'ultima è la verifica live, non stimabile con precisione.

### 4.2 Pacchetti NuGet al posto del submodule
Più pulito per loro (niente `git submodule update --init` in CI), ma richiede una pipeline di publish
su GitHub Packages per cinque pacchetti. Da fare quando l'integrazione è stabile, non prima.

### 4.3 Voci in `Ivao.It.Frontend.sln`
Il build passa dai `ProjectReference`, ma i progetti del modulo **non compaiono in Visual Studio**
finché non vengono aggiunti alla solution. Cosmetico finché non devono metterci mano loro.

### 4.4 ~~Self-hosting di Leaflet~~ — ✅ non serve più, fatto da noi l'11 agosto 2026
Leaflet è **vendorizzato** in `src/Vipi.Ui/wwwroot/vendor/leaflet/` e servito dalle rotte statiche del
modulo, come three.js e i font. La pagina non contatta più `unpkg`, quindi non c'è niente da chiedere alla
loro CSP e l'obbligo di notice BSD-2-Clause **è già nostro** (`THIRD-PARTY-NOTICES.md`), non passa a loro.

⚠️ Restano esterne le **tessere** della mappa (`basemaps.cartocdn.com`): quelle non si vendorizzano. Se la
loro CSP le blocca, i poligoni — cioè il dato nostro — si disegnano lo stesso, su sfondo vuoto. È l'unico
host di terzi che la pagina contatti, e va detto nella `img-src` della loro policy.

ℹ️ `ivao-it-wiring.patch` è un artefatto **congelato al 1° agosto 2026** e su questo punto dice ancora
«unpkg»: chi lo applica deve sostituire quelle due righe con i riferimenti locali. Non è stato riscritto
perché è un patch con un hash, non un documento.

---

## 5. Ordine consigliato

1. Sbloccare l'accesso al repo (§1.1) — altrimenti tutto il resto è teorico.
2. Risolvere il token app IVAO (§1.4) — è nostro, è aperto, e dimezza il prodotto.
3. **Eseguire** il modulo su un host net8 (§2.1) e guardare localizzazione (§2.2) e CSS (§2.4) mentre
   gira. Tre voci chiuse in una sessione sola.
4. Consegnare (patch + tag) e porre le domande su **versione MySQL** (§1.3 — è il gate del piano
   `design/piano-supporto-mysql.md`) e menu (§3.1).
5. In parallelo, senza aspettarli: le due slice indipendenti dal provider del piano MySQL (`HasMaxLength`
   sulle colonne indicizzate + test guardia sul modello). Valgono per tutti e tre i provider.
6. Il resto quando risponde il loro lato.
