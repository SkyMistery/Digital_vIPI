# HANDOFF — Accordi di coordinamento (16-17 agosto 2026)

> Contesto minimo per riprendere **dopo un `/clear`**. Ramo `feature/accordi-coordinamento`, **non ancora in
> `main`**.
>
> ⚠️ **Aggiornato il 17 agosto (secondo giro): l'editor.** Carta ed esito
> [`../feature/2026-08-17-editor-accordi-per-relazione.md`](../feature/2026-08-17-editor-accordi-per-relazione.md).
> L'albero non è più indicizzato sul lato B ma sull'**ACC della controparte** letta dalla lente
> `AgreementViewpoint`, e i **due versi si vedono sempre entrambi** (via l'interruttore). Chi legge qui sotto
> «selettore del verso» sta leggendo lo stato del 16.
>
> Carta ed esito completi: [`../feature/2026-08-16-accordi-di-coordinamento.md`](../feature/2026-08-16-accordi-di-coordinamento.md)
> · Schema: [`../spec/modello-dati.md`](../spec/modello-dati.md) §9.25-9.26 · Area: [`../refactor/07-trasferimenti.md`](../refactor/07-trasferimenti.md) §10
> · Voci aperte: [`../lavori-aperti.md`](../lavori-aperti.md) E6-bis, E6-ter

## In una riga

`TransferFlow` + `TransferPoint` **non esistono più** (droppate). Al loro posto un **accordo** fra due parti, con
più settori per lato, più aeroporti, più punti per clausola e fino a **due versi**.

## Il modello, dove guardare

| Cosa | Dove |
|---|---|
| Entità | `src/Vipi.Domain/Entities/CoordinationAgreement.cs` — `CoordinationAgreement` · `AgreementParty` (Side A/B) · `AgreementAirport` · `AgreementClause` |
| Enum | `src/Vipi.Domain/Enums.cs` — `AgreementSide`, `AgreementDirection` |
| Porta | `IAgreementRepository` (Abstractions) · `IAgreementService` (Content) · impl. `EfAgreementRepository` (~700 righe: outline, ordini, sottoalbero) |
| Proiezione | `src/Vipi.Application/Content/AgreementExpansion.cs` → `TransferFlowRow`/`TransferPointRow` |
| Editor | `src/Vipi.Ui/Pages/AdminTrasferimentiPage.razor` — rotta **invariata** `/vsop/admin/trasferimenti` |
| Lettura | `src/Vipi.Ui/Components/App/CoordTable.razor` |
| Ausili | `AgreementSuggestions.cs` · `ClausePaste.cs` · `AgreementGaps.cs` (tutti puri, in Application) |
| Orientamento | `AgreementViewpoint.cs` — «noi/loro» rispetto alla ACC aperta; `TrafficKinds.Reciprocal` in Domain |
| Punti spaiati | `AgreementPoints.cs` — un conto solo, letto dal cruscotto **e** dal riquadro |
| Fusione dei versi | `AgreementMerge.cs` (proposta, pura) + `IAgreementRepository.AbsorbAsReverseAsync` |
| Albero | `XferNavigator.razor` + `XferNavModel.cs` — ACC controparte ▸ ente ▸ accordo |

## Le tre cose da NON riscoprire a mani nude

**1. `TransferFlowRow`/`TransferPointRow` esistono ancora, ma NON sono storage.** Sono la **proiezione**
dell'accordo, prodotta da `AgreementExpansion`. È lo stesso schema dei settori (cataloghi = fonte unica,
`Sector` = proiezione). Per questo derivazione, frasi, vista live, stampa e matcher Aurora non sono stati
toccati: leggono tutti quella forma. Chi cerca «dove si salva un coordinamento» deve trovare **un** posto, e
quel posto è l'accordo.

**1-bis. «Noi» e «loro» sono una LENTE, non un dato.** A e B in archivio dicono chi ha scritto l'accordo per
primo, non da che parte stiamo: 13 accordi di LIBB e **10 su 11 di LIRR** hanno la ACC sul lato B. La vista li
orienta con `AgreementViewpoint`; **non** si riscrive l'archivio per raddrizzarli — cambierebbe di significato le
clausole di entrambi i versi e le release congelate, e un accordo di confine non ha un verso giusto.

**2. L'outline vive dentro `(accordo, verso)`.** Spostare, annidare, sciogliere: tutto ragiona su una direzione
sola. Le clausole del verso opposto non sono alternative delle prime, sono **un'altra tabella** (EUROCONTROL
Annex D.2 ne ha due). L'ordine **è** struttura: una riga appartiene all'ultima meno profonda che la precede,
quindi spostarla deve spostare il suo sottoalbero.

**3. Vincolo snapshot.** Le release congelate serializzano `AccCoordination`/`AppCoordination` in JSON.
`AppCoordRow` si tocca **solo in modo additivo** — mai rinominare o cambiare tipo a un campo esistente, o le
release vecchie non si rileggono.

## La rete che protegge tutto

`tests/Vipi.Application.Tests/CoordinationCharacterizationTests.cs` + `RealCoordinationFixture.cs` +
`Fixtures/real-flows.tsv` · `real-maps.tsv` · `real-coordination.approved.txt`.

> **Invariante:** frasi composte e righe derivate identiche, carattere per carattere, sui dati veri. Finché è
> verde, vIPI ACC, vIPI APP, vLOA, vista live, stampa e matcher Aurora non possono essersi rotti.

⚠️ **`real-flows.tsv` è l'ultima copia dei dati vecchi nella loro forma originale.** Le tabelle da cui vengono
non esistono più: quel file è il solo motivo per cui la rete può ancora dire «la derivazione non è cambiata».
Non cancellarlo, non «rigenerarlo».

Il file approvato **non si riapprova da sé**: a differenza fallita scrive un `.received.txt` accanto e dice
dov'è, così la differenza si guarda prima di accettarla.

## Il travaso non c'è più, e va saputo

Il travaso è **girato** sul `vipi.db` di sviluppo (37 flussi / 78 punti → 41 accordi / 63 clausole) e poi è stato
**rimosso col suo macchinario** (`ILegacyFlowReader`, `IAgreementMaintenance`, `FlowsToAgreements`, le due impl.
EF, la passata d'avvio, la categoria `ImportStates`).

⚠️ **La trappola di sequenza, se mai si ripresentasse su un'altra area:** le migrazioni girano **prima** della
manutenzione d'avvio (`src/Vipi.Host/Program.cs`, righe 147 e 153). Una migrazione che droppa + una passata che
legge quella tabella nella **stessa release** = la passata non trova niente, scrive zero, e i dati spariscono
**senza un errore**. E tenere la passata *dopo* il drop è peggio che inutile: su un DB non ancora convertito
legge una tabella inesistente e fa **crashare l'avvio**.

Qui si è potuto fare in un colpo solo per una ragione precisa: **il DB di produzione viene sostituito con quello
di sviluppo, già convertito.** Non è una regola generale.

Backup pre-travaso, **fuori dal repo**: `../../vipi.db.bak-pre-travaso-20260817` (cioè in
`D:\Programmazione\IVAO_Test\vIPI Ivao Italy\`). Il `Down` delle migrazioni ricrea le tabelle **vuote** — fa
tornare lo schema, non l'archivio.

## Stato del DB di sviluppo

`src/Vipi.Host/vipi.db` è **già travasato e già droppato**: 41 accordi · 80 parti · 35 aeroporti · 63 clausole,
zero `TransferFlows`/`TransferPoints`. È il DB che va in produzione. ⚠️ **Non contiene nessun accordo bilaterale**:
le 63 clausole sono tutte in un verso. Le fusioni provate il 17 agosto sono girate su una **copia**.

⚠️ **Prima di credere a ciò che si vede guidando l'app**, controllare l'ora di
`src/Vipi.Host/bin/Debug/net8.0/Vipi.Application.dll`: `dotnet build src/Vipi.Ui` **non** aggiorna la copia dentro
`bin` dell'host, e `dotnet run --no-build` parte da lì. Il 17 agosto questo ha fatto misurare 27 lacune invece di
28 e concludere che una voce nuova non funzionasse.

Il 41° accordo non ha clausole ed **è corretto**: viene dal flusso #10 (`LIRR_NE_CTR`, sorvolo) che era già
vuoto prima del travaso. Il cruscotto delle lacune lo segnala.

## Cosa resta aperto

0. **Tre reciproci ancora in accordi separati** (`#13/#32` LGGG · `#17/#28` LDZO · `#23/#38` LAAA): il comando
   «unisci i due versi» c'è ed è stato provato **su una copia**, non sul `vipi.db` del progetto — che resta a 41
   accordi. Va fatto in produzione, guardando le due tabelle prima di premere. Il cruscotto li elenca sotto
   «reciproco a parte».
1. **Due asimmetrie fra i versi**, da decidere dai colleghi e non dal codice: `LGGG ⇄ LIBB` (BELIX di qua, OLGAT
   di là) e `LDZO ⇄ LIBB` (sei punti scritti da un lato solo, **che nessuno aveva notato**). Il travaso non le ha
   risolte apposta: accoppiare i due versi vorrebbe dire scegliere quale valga. Dopo la fusione compaiono
   **dentro il riquadro**, sopra le due tabelle, non solo nel cruscotto.
2. **Merge in `main`**: serve l'ok esplicito, come per il doc 10 e per B6.
3. **`AuroraClientTests.Richieste_in_sequenza_non_si_mescolano` è instabile** (E6-ter) — due passate su tre in
   isolamento, su codice che questo lavoro non tocca. Test su socket TCP di loopback. Slegato, ma da rendere
   deterministico prima che qualcuno impari a rilanciare la suite finché diventa verde.
4. **Il secondo giro dei campi**, già progettato nella carta §5 con il posto dove ognuno atterra (quindi
   additivo, non una seconda chirurgia): rotta separata dal punto, *Release* (climb/descent/turn), modo di
   coordinamento, nota per clausola, default in testa all'accordo, condizione come intestazione, clausole in
   prosa e spaziatura, voce «riceve da», etichetta del gruppo di aeroporti.
5. ⚠️ **Tre difetti di `LevelFormatting`**, congelati nell'approvato apposta e ora tutti **visti a schermo**:
   L10 (nella resa inglese la colonna livello esce `FL260 (pari)` — `LevelFormatting` non conosce la lingua);
   `— (dispari)`, cioè la parità appesa a un livello **assente**; e la parità appesa anche a un livello
   **speciale** che la dice già a parole (`Pari (Nord) - Dispari (Sud) (dispari)`). Un giro loro, insieme, con la
   riapprovazione guardata riga per riga.
6. ⚠️ **`InlineConfirm.ConfirmLabel` ha per default «Sì, elimina», italiano e cablato**: nella pagina inglese ogni
   conferma in linea che non passa l'etichetta lo dice in italiano. Fuori da quest'area, ma trovato qui.

## Comandi

```
dotnet build Vipi.slnx -c Release --no-incremental     # il cancello vero: avvisi = errori
dotnet test Vipi.slnx                                  # 2485 verdi su net8 e net10
dotnet ef migrations add NOME --project src/Vipi.Infrastructure \
  --startup-project src/Vipi.Infrastructure --output-dir Persistence/Migrations --framework net8.0
dotnet ef migrations add NOME --project src/Vipi.Infrastructure.MySqlMigrations \
  --startup-project src/Vipi.Infrastructure.MySqlMigrations --output-dir Migrations --framework net8.0
```

⚠️ `--framework net8.0` è **obbligatorio** (i progetti sono multi-target, altrimenti `MSB4057`). Le migrazioni
si emettono **due volte** e **si leggono**: su quest'area lo scaffolding ha già proposto un `RenameColumn`
*diverso nei due provider*. Fermare `Vipi.Host` prima di compilare, o è `MSB3021` sui DLL bloccati.

Verifica a schermo: skill di progetto `.claude/skills/verifica-live/`. La rotta di una vIPI ACC è
`/vsop/{Acc}/vipi` (**non** `/vsop/acc/...`), l'editor è `/vsop/admin/trasferimenti?acc=LIBB`.
