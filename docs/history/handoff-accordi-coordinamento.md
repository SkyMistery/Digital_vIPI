# HANDOFF — Accordi di coordinamento (16-18 agosto 2026)

> Contesto minimo per riprendere **dopo un `/clear`**. Ramo `feature/accordi-coordinamento`, allineato col
> remoto, **non ancora in `main`** (serve l'ok esplicito).
>
> **Stato al 18 agosto 2026, sera.** Il modello a sezioni è **fatto e in esercizio**: codice, migrazioni nei due
> provider, conversione **eseguita sul `vipi.db` di sviluppo**, verifica live guidata a schermo, documenti e
> memoria allineati. Cinque commit, dal più vecchio: il modello · i tre difetti della verifica live · la
> conversione · colonna e sezioni chiuse · il chip del verso. **Il prossimo passo è del committente**: sta
> facendo aggiustamenti di UI — vedi «Dove mettere le mani per un aggiustamento di UI».
>
> **Tre carte, e la terza è quella in vigore:**
> [`../feature/2026-08-18-accordi-a-sezioni.md`](../feature/2026-08-18-accordi-a-sezioni.md) — **il modello di
> adesso**: un accordo per coppia, il traffico nelle sezioni ·
> [`../feature/2026-08-16-accordi-di-coordinamento.md`](../feature/2026-08-16-accordi-di-coordinamento.md) —
> l'accordo al posto del flusso (storia, ma le sue decisioni valgono) ·
> [`../feature/2026-08-17-editor-accordi-per-relazione.md`](../feature/2026-08-17-editor-accordi-per-relazione.md) —
> l'editor a tre colonne, cinque giri fra il 17 e il 18.
>
> Schema: [`../spec/modello-dati.md`](../spec/modello-dati.md) **§9.25-bis** (§9.25 è storia) · Area:
> [`../refactor/07-trasferimenti.md`](../refactor/07-trasferimenti.md) §11 · Voci aperte:
> [`../lavori-aperti.md`](../lavori-aperti.md)

> ## ⚠️ Il `vipi.db` di sviluppo è CONVERTITO — e adesso vuole questo ramo
>
> La conversione è girata sull'archivio vero il **18 agosto 2026**: 40 accordi / 60 clausole →
> **16 accordi / 38 sezioni / 60 clausole**, `integrity_check` ok, zero orfani, zero violazioni di FK.
>
> ⚠️ **Da qui in poi `src/Vipi.Host/vipi.db` gira solo con `feature/accordi-coordinamento`.** Il codice di
> `main` cerca ancora `AgreementParties`, che non esiste più: tornare indietro **senza rimettere il backup**
> fa crashare l'avvio. Il backup è **fuori dal repo**, in `../vipi.db.bak-pre-sezioni-20260818` (5,1 MB) — e il
> `vipi.db` **non è tracciato in git**, quindi quel file è l'unica copia dello stato precedente.


## In una riga

Un **accordo per coppia di enti**, sempre bidirezionale, con **un solo ente per lato**. Il traffico — arrivi,
partenze, sorvoli nei due versi — sta nelle **sezioni** dentro l'accordo, una per tabella. Le clausole stanno
nelle sezioni.

```
CoordinationAgreement   OwnerAcc · SideASectorId ⇄ SideBSectorId (canonici) · Note
└── AgreementSection    Kind · Direction · Description
    ├── AgreementAirport
    └── AgreementClause  (invariata, meno Direction, più SectionId)
```

## Le tre cose da NON riscoprire a mani nude

**1. `TransferFlowRow`/`TransferPointRow` esistono ancora, ma NON sono storage.** Sono la **proiezione** delle
sezioni, prodotta da `AgreementExpansion`. È lo stesso schema dei settori (cataloghi = fonte unica, `Sector` =
proiezione). Per questo derivazione, frasi, vista live, stampa e matcher Aurora non sono mai stati toccati:
leggono tutti quella forma. Chi cerca «dove si salva un coordinamento» deve trovare **un** posto.

**2. A e B non significano niente per chi legge — e il verso NON è più sulla clausola.** I due lati stanno in
**forma canonica** (id minore = A) perché l'unicità della coppia è un indice e in SQL non esiste «insieme di
due». Girare i lati è **senza perdita** solo perché `Direction` vive sulla **sezione** e si ribalta con loro
(`UpdateAgreementAsync`). Fino a ferragosto era vietato, e a ragione: col verso sulla clausola, scambiare i lati
capovolgeva il significato di tutto. «Noi/loro» resta una **lente** (`AgreementViewpoint`), non un dato.

**3. L'outline vive dentro la SEZIONE.** Spostare, annidare, sciogliere: tutto ragiona su una sezione sola. Le
clausole di un'altra sezione non sono alternative di queste, sono **un'altra tabella** (EUROCONTROL Annex D.2 ne
ha due). L'ordine **è** struttura: una riga appartiene all'ultima meno profonda che la precede, quindi spostarla
deve spostare il suo sottoalbero.

**3-bis. Vincolo snapshot.** Le release congelate serializzano `AccCoordination`/`AppCoordination` in JSON.
`AppCoordRow` si tocca **solo in modo additivo** — mai rinominare o cambiare tipo a un campo esistente, o le
release vecchie non si rileggono.

## Dove guardare

| Cosa | Dove |
|---|---|
| Entità | `src/Vipi.Domain/Entities/CoordinationAgreement.cs` — `CoordinationAgreement` · `AgreementSection` · `AgreementAirport` · `AgreementClause` |
| Enum | `src/Vipi.Domain/Enums.cs` — `AgreementSide`, `AgreementDirection`, `TransferFlowKind` |
| Porta | `IAgreementRepository` (Abstractions) · `IAgreementService` (Content) · impl. `EfAgreementRepository` |
| Proiezione | `src/Vipi.Application/Content/AgreementExpansion.cs` → `TransferFlowRow`/`TransferPointRow` |
| Conversione | `src/Vipi.Application/Content/AgreementsToSections.cs` (**pura**) + `tools/Vipi.AgreementsToSections` (il comando) |
| Verso proposto | `src/Vipi.Application/Content/SectionDirection.cs` (puro) |
| Ordine sezioni | `src/Vipi.Application/Content/AgreementSectionOrder.cs` (puro, imposto) |
| Editor | `src/Vipi.Ui/Pages/AdminTrasferimentiPage.razor` — rotta **invariata** `/vsop/admin/trasferimenti` |
| Lettura | `src/Vipi.Ui/Components/App/CoordTable.razor` |
| Ausili | `AgreementSuggestions.cs` · `ClausePaste.cs` · `AgreementGaps.cs` · `AgreementPoints.cs` (tutti puri) |
| Albero | `XferNavigator.razor` + `XferNavModel.cs` — **due livelli**: ACC controparte ▸ accordo |

## L'editor com'è adesso (18 agosto) — leggere prima di toccarlo

`/vsop/admin/trasferimenti?acc=LIBB`. Sei giri fra il 16 e il 18, e ognuno ha tolto un'abitudine del modello
precedente.

| | Com'è | Perché, in una riga |
|---|---|---|
| **Albero** | ACC controparte ▸ **accordo** (foglia) | il livello «relazione» esisteva perché una coppia poteva avere più accordi: adesso non può |
| **Foglia** | i due capi **INTERI** + «N sezioni ▤ M clausole»; la nota a capo | un callsign troncato non è un'abbreviazione, è un altro callsign — e `LIBB_ES_CTR`/`LIBD_CS0_APP` convivono nello stesso elenco |
| **Riquadro** | testata coi due capi, poi **le sezioni**, ognuna con la sua tabella | un accordo con arrivi, partenze e due versi di sorvoli non ha «un» tipo |
| **All'apertura** | sezioni tutte **chiuse**, coi tasti ⊞/⊟ in testata | otto tabelle aperte sono duemila pixel: chi apre un accordo sta cercando UNA sezione, e gliela fa trovare l'elenco dei titoli. Il blocco del **reciproco mancante** non è collassabile, quindi il vuoto resta a vista |
| **Ordine sezioni** | **imposto**: aeroporto (arrivi poi partenze) ▸ sorvoli (due versi) ▸ VFR ▸ Altro | non è struttura come l'ordine delle clausole; a mano si potrebbe nascondere una partenza lontano dai suoi arrivi |
| **Verso** | **proposto dall'aeroporto**, salvato; nel form **il chip stesso si preme** per invertirlo | «arrivi verso LIRF» va verso chi ha LIRF; non si ricalcola a ogni lettura — l'AoR cambia, l'accordo scritto no. Il chip è il comando: si preme ciò che si legge, invece di un tasto accanto più piccolo del testo |
| **Reciproco mancante** | blocco **vuoto** sotto la sezione, coi tasti «copia l'altro verso» e «+ sezione» | il vuoto **è** l'informazione: l'interruttore di ferragosto nascondeva ciò che mancava, e per questo il reciproco non si scriveva mai |
| **Gemelle** | **avviso** + tasto «unisci», non errore | due arrivi a LIRF a condizioni diverse si scrivono con le **varianti**; vietare la seconda sezione non lo insegnerebbe a nessuno |
| **Creazione** | accordo = **due enti**; sezione = tipo (+ aeroporti) | creare è dire *chi*; il traffico è il *cosa*, e sta dentro |
| **Coppia già scritta** | il form **apre quella che c'è**, non dà errore | un doppione è una domanda a cui esiste una risposta migliore di «no» |

⚠️ **Dieci trappole pagate qui, quasi tutte invisibili ai test** — le ultime tre le ha trovate la verifica live, non il DOM:

1. **Un `<select>` non torna indietro da solo.** Se il salvataggio è rifiutato il valore memorizzato non cambia,
   quindi l'albero di render non cambia, quindi Blazor **non riscrive l'attributo**: la tendina resta sulla
   scelta rifiutata e mente. Serve una **chiave con un'epoca** che avanza a ogni tentativo (`_kindEpoch`).
2. **`return` dentro un elemento aperto** in un `RenderFragment` lascia il `RenderTreeBuilder` con l'elemento non
   chiuso: si **avvolge** in un `@if`, non si esce.
3. **Un campo nascosto che tiene dati** è il modo più rapido di perderli: il blocco aeroporti resta a vista se ce
   ne sono, anche col tipo che non li vuole.
4. **Catch-22 sugli aeroporti** (ferragosto): la regola è «dove **non sono esclusi**» — tutto tranne i sorvoli —
   non «dove servono».
5. **Attributo componente `string` senza `@` = letterale** (`Key="x"` ≠ `Key="@x"`) → render vuoto senza errore.
6. **`InlineConfirm.ConfirmLabel` ha per default «Sì, elimina»**, italiano e cablato: chi non passa l'etichetta
   la mostra così anche nella pagina inglese, e anche per azioni che non eliminano niente (il tasto «unisci»).
7. ⚠️ **Un avviso che scatta sul caso normale non è un avviso.** «Lo scalo è coperto da un ente che non è fra i
   riceventi» urlava su **tre sezioni su otto**, e tutte e tre erano scritte bene: un'**area** che riceve arrivi e
   poi li gira all'avvicinamento è la norma. Vale solo se a ricevere è un **APP o una torre**. Seconda volta che
   questa pagina lo impara, dopo il cruscotto delle lacune.
8. **La testata di una sezione è piena**: traffico, coppia, freccia, scali, conteggio e quattro tasti. Una pill
   lunga in più manda «Paste table ✎ ✕» a capo sotto «+ Clause». Gli avvisi stanno nel **corpo**.
9. **Le etichette sopravvivono all'operazione che descrivevano**: il tasto delle gemelle diceva ancora «unisci i
   due **versi**» dove si uniscono due **sezioni**. Le stringhe vanno rilette quando cambia ciò che fanno.
10. ⚠️ **`aria-expanded="@bool"` NON funziona in Blazor.** È trattato come attributo *booleano HTML*: con `true`
    rende `aria-expanded=""` — che per ARIA non è un valore valido — e con `false` **omette l'attributo**, il
    che per uno screen reader significa «non espandibile», non «chiuso». Serve
    `@open.ToString().ToLowerInvariant()`. Tre occorrenze in quest'area lo sbagliavano; `StrutturaPage` lo
    faceva già giusto. Visto **ispezionando il DOM**, non dai test.

## Dove mettere le mani per un aggiustamento di UI

Tutta la pagina è **un file**: `src/Vipi.Ui/Pages/AdminTrasferimentiPage.razor` (~2900 righe), diviso in
regioni commentate (`// ---- ...`). Il CSS dell'area sta in `src/Vipi.Ui/wwwroot/vipi-theme.css`, cercabile per
prefisso **`.xt-`**.

| Cosa si vede | Dove si tocca |
|---|---|
| Albero a sinistra (rami ACC, foglie) | `Components/App/XferNavigator.razor` + `XferNavModel.cs`; CSS `.xt-nav*` |
| Come si costruisce l'albero | `BuildNav` / `NavAgreement` / `NavKey` nella pagina |
| Testata dell'accordo (capi, ⊞/⊟, + Section, Edit, Delete) | `WorkPane`, primo blocco `<div class="xt-pane-h">`; CSS `.xt-pane-h` |
| Una sezione (intestazione, avvisi, tabella) | `SectionBlock`; CSS `.xt-dirblock`, `.xt-dirhead`, `.xt-dirway`, `.xt-dirpair` |
| Il blocco del reciproco che manca | `MissingReverseBlock`; CSS `.xt-ghost` |
| Form «nuova sezione» / «modifica sezione» | `SectionFields`; CSS `.xt-dirfield`, `.xt-dirchip`, `.xt-aptpick-narrow` |
| Form dell'accordo (i due capi + nota) | `PairFields` / `SidePicker` |
| Chip degli aeroporti in testata sezione | `AirportChips`; CSS `.xt-hdr-apts`, `.xt-apt` |
| Tabella delle clausole | `Components/App/XferRowsTable.razor` + `XferTableRow.cs` |
| Pannello di destra (la clausola) | `ClausePanel` e seguenti; CSS `.xt-panel*` |
| Cruscotto delle lacune | `Gaps()` + `GapLabel`/`GapPill`/`GapDetail`; la logica pura è `AgreementGaps` |
| Etichette | `src/Vipi.Ui/Resources/SharedResource.resx` **e** `.en.resx`, prefisso `Xfer_` |

⚠️ **Le due lingue si toccano insieme.** Una chiave che manca non dà errore: esce a schermo il nome della
chiave. Il controllo veloce è questo:

```
python - <<'PY'
import io,re,glob
k=set()
for f in glob.glob('src/Vipi.Ui/**/*.razor',recursive=True)+glob.glob('src/Vipi.Ui/**/*.cs',recursive=True):
    t=io.open(f,encoding='utf-8').read()
    k|=set(re.findall(r'L\["(Xfer_[A-Za-z0-9_]+)"',t))|set(re.findall(r'localizer\["(Xfer_[A-Za-z0-9_]+)"',t))
for r in ['src/Vipi.Ui/Resources/SharedResource.resx','src/Vipi.Ui/Resources/SharedResource.en.resx']:
    h=set(re.findall(r'<data name="(Xfer_[A-Za-z0-9_]+)"',io.open(r,encoding='utf-8').read()))
    print(r,'mancanti:',sorted(k-h))
PY
```

⚠️ **Le regressioni Blazor sono silenziose coi test verdi.** Le cinque già pagate in quest'area:
attributo `string` senza `@` (= letterale), `aria-expanded="@bool"` (= attributo booleano HTML),
`<select>` che non torna indietro dopo un rifiuto (serve l'epoca), `return` dentro un elemento aperto in un
`RenderFragment`, `v@r.Prop` letto come indirizzo email. **Si guarda a schermo**, non si deduce.

## La rete che protegge tutto

`tests/Vipi.Application.Tests/CoordinationCharacterizationTests.cs` + `RealCoordinationFixture.cs` +
`Fixtures/real-flows.tsv` · `real-maps.tsv` · `real-coordination.approved.txt`.

> **Invariante:** frasi composte e righe derivate identiche, carattere per carattere, sui dati veri. Finché è
> verde, vIPI ACC, vIPI APP, vLOA, vista live, stampa e matcher Aurora non possono essersi rotti.

⚠️ **`real-flows.tsv` è l'ultima copia dei dati vecchi nella loro forma originale.** Non cancellarlo, non
«rigenerarlo». Il file approvato **non si riapprova da sé**: a differenza fallita scrive un `.received.txt`
accanto e dice dov'è, così la differenza si guarda prima di accettarla.

⚠️ **Il 18 agosto l'approvato non si è mosso di un carattere**, ed è la prova che la conversione è corretta: se
si muovesse per un riordino, la tentazione sarebbe riapprovare.

## La conversione — com'è stata eseguita, e come si rifà altrove

✅ **Fatta sul `vipi.db` di sviluppo il 18 agosto 2026.** Quello che segue serve a rifarla su un altro archivio
— la **MariaDB di produzione**, o una copia — con `--mysql "<conn>"` al posto di `--sqlite` e le due migrazioni
gemelle di `Vipi.Infrastructure.MySqlMigrations`.

⚠️ **Il percorso del DB va ASSOLUTO.** `--connection "Data Source=src/Vipi.Host/vipi.db"` fallisce con
`SQLite Error 14: unable to open database file`: il relativo si risolve dalla cartella del progetto di
startup, non da dove si è lanciato il comando. Fallisce senza toccare niente, ma fa perdere un giro.

```
# 0. backup, FUORI dal repo
cp src/Vipi.Host/vipi.db ../vipi.db.bak-pre-sezioni-20260818

# 1. schema nuovo, tutto nullable: non tocca niente di ciò che c'è
dotnet ef database update 20260818115830_AgreementSectionsAdditive \
  --project src/Vipi.Infrastructure --startup-project src/Vipi.Infrastructure --framework net8.0

# 2. i dati. SENZA --apply stampa il piano e non scrive: si guarda PRIMA.
dotnet run --project tools/Vipi.AgreementsToSections -- --sqlite src/Vipi.Host/vipi.db
dotnet run --project tools/Vipi.AgreementsToSections -- --sqlite src/Vipi.Host/vipi.db --apply

# 3. NOT NULL, indice unico, via il vecchio
dotnet ef database update 20260818115838_AgreementSectionsFinalize \
  --project src/Vipi.Infrastructure --startup-project src/Vipi.Infrastructure --framework net8.0
```

Su MariaDB: `--mysql "<conn>"` e le due migrazioni gemelle di `Vipi.Infrastructure.MySqlMigrations`.

**Cosa deve dire il rapporto** (misurato sulla copia del `vipi.db` del 18 agosto):
40 accordi / 60 clausole → **16 accordi, 38 sezioni, 60 clausole**, una fusione di gemelle (`#26`+`#27`, arrivi
LIBD), un guscio scartato (`#41`), 35 aeroporti. E in fondo: *«Clausole: tutte e 60 ritrovate, nessuna persa e
nessuna inventata»* — che è la riga da leggere davvero.

⚠️ **Il passo 3 fallisce se il passo 2 non è girato**, ed è la protezione: `NOT NULL` su colonne ancora nulle,
indice unico su coppie ancora doppie. Un fallimento rumoroso è l'unica difesa che vale — la trappola di
ferragosto era una passata che «non trova niente, scrive zero, e i dati spariscono senza un errore».

⚠️ **Il tool si rifiuta di girare due volte.** Una seconda passata rileggerebbe le righe già convertite come se
fossero ancora vecchie, rifondendo accordi già fusi e mescolandone gli aeroporti.

## Cosa resta aperto

1. ✅ **Verifica live fatta** (18 agosto, porta 5035, copia del DB convertita): albero a due livelli, le otto
   schede di LGGG diventate una foglia «8 ▤ 13», ordine imposto con arrivi e partenze dello stesso scalo
   accostati, verso proposto dall'aeroporto che **gira digitando l'ICAO**, tasto `⇄`, blocco fantasma del
   reciproco + «copia l'altro verso», gemelle create e unite, deep-link `?sezione=`. Ha trovato **tre difetti**
   invisibili al DOM, tutti corretti: l'avviso «scalo non coperto» che urlava su 3 sezioni su 8 (ora solo se a
   ricevere è un APP o una torre — un'**area** che gira gli arrivi all'avvicinamento è il caso normale), lo
   stesso avviso che dalla testata mandava i tasti a capo (sceso nel corpo), e cinque etichette che dicevano
   ancora «unisci i due **versi**» dove si uniscono due **sezioni**.
2. ✅ **`Vipi.Host` e `Vipi.E2E.Tests` ricompilati** a host spento: suite completa **2094 verdi**.
3. **Le due asimmetrie note NON sono state toccate** — `LGGG ⇄ LIBB` (BELIX di qua, OLGAT di là) e
   `LDZO ⇄ LIBB` (sei punti da un lato solo). Adesso stanno nello **stesso accordo**, una sezione sotto l'altra,
   quindi finalmente si **vedono**; sceglierne una è una decisione dei colleghi, non una migrazione.
4. **Merge in `main`**: serve l'ok esplicito, come per il doc 10 e per B6.
5. ⚠️ **Tre difetti di `LevelFormatting`**, congelati nell'approvato apposta e tutti visti a schermo: L10
   (`FL260 (pari)` nella resa inglese — `LevelFormatting` non conosce la lingua); `— (dispari)`, cioè la parità
   appesa a un livello **assente**; e la parità appesa anche a un livello **speciale** che la dice già a parole
   (`Pari (Nord) - Dispari (Sud) (dispari)`). Un giro loro, insieme, con la riapprovazione guardata riga per riga.
6. ⚠️ **`InlineConfirm.ConfirmLabel`** con default «Sì, elimina», italiano e cablato. Fuori da quest'area.
7. **`AuroraClientTests.Richieste_in_sequenza_non_si_mescolano` è instabile** — due passate su tre in
   isolamento, su codice che questo lavoro non tocca. Da rendere deterministico prima che qualcuno impari a
   rilanciare la suite finché diventa verde.

## Comandi

```
dotnet build Vipi.slnx -c Release --no-incremental     # il cancello vero: avvisi = errori, DUE TFM
dotnet test Vipi.slnx                                  # 2094 verdi al 18 agosto (E2E inclusi)
dotnet ef migrations add NOME --project src/Vipi.Infrastructure \
  --startup-project src/Vipi.Infrastructure --output-dir Persistence/Migrations --framework net8.0
dotnet ef migrations add NOME --project src/Vipi.Infrastructure.MySqlMigrations \
  --startup-project src/Vipi.Infrastructure.MySqlMigrations --output-dir Migrations --framework net8.0
```

⚠️ `--framework net8.0` è **obbligatorio** (progetti multi-target, altrimenti `MSB4057`). Le migrazioni si
emettono **due volte** e **si leggono**: su quest'area lo scaffolding ha già proposto, due volte, un
`RenameColumn` che avrebbe lasciato dati validi ma **sbagliati** — a ferragosto un rename diverso nei due
provider, il 18 agosto un `AgreementId` spacciato per `SectionId`.

⚠️ **Fermare `Vipi.Host` prima di compilare**, o è `MSB3021` sui DLL bloccati. ⚠️ **Ma se l'host acceso è quello
del committente (5034, con connessioni attive) NON si spegne**: si compila in una cartella separata —
`dotnet build src/Vipi.Host -c Debug -f net8.0 -o <scratchpad>/app` — e si lancia da lì `dotnet app/Vipi.Host.dll`.
L'`OutDir` diverso non tocca `src/Vipi.Host/bin`, che è ciò che l'host blocca. Resta impossibile il `dotnet test`
completo: `Vipi.E2E.Tests` referenzia `Vipi.Host`. E prima di credere a ciò che si
vede a schermo, controllare l'ora di `src/Vipi.Host/bin/Debug/net8.0/Vipi.Application.dll`:
`dotnet build src/Vipi.Ui` **non** aggiorna la copia dentro `bin` dell'host, e `dotnet run --no-build` parte da lì.

⚠️ **Il committente tiene il suo host sulla 5034**: per la verifica live usare **un'altra porta** (5035). Ogni
prova di scrittura va su una **copia** nello scratchpad.

Verifica a schermo: skill di progetto `.claude/skills/verifica-live/`. La rotta di una vIPI ACC è
`/vsop/{Acc}/vipi` (**non** `/vsop/acc/...`), l'editor è `/vsop/admin/trasferimenti?acc=LIBB`.
