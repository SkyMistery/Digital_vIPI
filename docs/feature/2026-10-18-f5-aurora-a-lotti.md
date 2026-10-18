# F5 — Aurora a lotti: vedere in Aurora tante modifiche con una ricarica sola (18 ottobre 2026)

> **Stato: ✅ APPROVATA** dal committente il 18 ottobre 2026 («facciamo F5», con le risposte di §5: D1 la vetrina).
> Quinta fase di Aurora Sector Lab ([carta madre](2026-09-18-aurora-sector-lab.md), decisione 10 e §7). Usa l'app di
> F3 e il git di [F4](2026-10-17-f4-git-nel-lab.md). Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md).
> 🔴 Nessun dato di vIPI si tocca, il sito non cambia. 🔴 Aurora è un programma che il committente usa: il Lab non lo
> avvia, non lo chiude, non ne scrive le impostazioni, non tocca il sector pubblicato.

## §0 — La domanda

La carta madre: Aurora ricarica il sector a mano e ci mette **fino a un minuto**, quindi la mappa del Lab è l'ossatura e
Aurora si guarda **a lotti** («7 modifiche da vedere»). F5 è finita quando **un lotto = una ricarica sola**.

Oggi, per vedere in Aurora quel che si è fatto nel Lab, l'AOD deve sapere da sé quali file ha cambiato, portarli dove
Aurora li legge, ricordarsi cosa guardare, e annotare a parte com'è andata. Le prove in Aurora di questo mese (la T3
dei simboli) sono state preparate così, a mano: cartelle, file da scambiare, un `LEGGIMI`.

## §1 — Le misure (18 ottobre, sul PC del committente, in sola lettura)

- **Aurora ha un «secondo sector»**: `Settings\profile.ini` di `%APPDATA%\IVAO\AuroraWindows`, sezione `[SECTOR]`,
  chiave `SecondSector`. Il committente: il sector pubblicato resta in `%APPDATA%\IVAO\AuroraWindows\SectorFiles`, il
  secondo è quello che si edita. Oggi `SecondSector=D:\IVAO\it-aurora-sector\SectorFiles`: un clone del repo vero su
  `master`, con due file cancellati in locale.
- 🔴 Lo stesso `profile.ini` tiene **l'accesso IVAO** del controllore (token di sessione, password cifrata). Il Lab ne
  legge **solo la riga `SecondSector`** e non lo scrive mai: dove guarda Aurora lo sceglie l'AOD, in Aurora.
- `Settings\last.ini`: l'ultimo profilo aperto (`…\Profiles\LIRR_EW_CTR.cpr`).
- La ricarica: a mano, fino a un minuto (carta madre §0). Come si chiede ad Aurora di ricaricare non è scritto da
  nessuna parte; il Lab non ci prova (non pilota Aurora).

## §2 — Quel che è già deciso

- **Decisione 10** della carta madre: la mappa nostra è l'ossatura, Aurora si guarda a lotti.
- Dalle prove di settembre-ottobre (F3, lotto «Subito»): le cose da guardare in Aurora sono **domande precise** («il
  `//discontinuity` di L613 spezza l'aerovia?», «DUMMY sparisce dalla *MVA Selection*?»), con una risposta che cambia
  il lavoro. Oggi stanno in `PROVE.md` e nella carta «file per file» (le voci «da provare in Aurora»: A8, B9, F5, I7,
  K2, O4, R4, T3 e i `SYMBOLS_` dei profili).

## §3 — La proposta

**La vetrina** (D1, risposta del committente: «così possiamo lavorare su più sector — uno per l'AIRAC successivo, uno
per dei test — e switchare nel Lab quale vedere in Aurora, senza cambiare nessuna impostazione in Aurora»). Una
cartella fissa, scelta una volta nelle Impostazioni del Lab, a cui l'AOD punta il `SecondSector` di Aurora **una volta
sola**. Il Lab ci mette, quando l'AOD preme **«Porta in Aurora»**, la `SectorFiles` del sector aperto adesso — il
clone del ciclo dopo, quello di una prova, un altro — come specchio: i file uguali restano, i cambiati si copiano, quelli
che nel sector non ci sono più si tolgono. Nella barra: «In Aurora: `<cartella>` · `<ramo>` @ `<commit>` · alle 18:40».
- La vetrina è **del Lab**: la crea lui e ci lascia un file segno (`.vetrina-del-lab`). Non specchia mai in una
  cartella senza il segno, in un clone git, né nella `SectorFiles` pubblicata di Aurora.
- Il Lab legge la sola riga `SecondSector` per dire se Aurora guarda davvero la vetrina («Aurora legge un'altra
  cartella: puntala alla vetrina, in Aurora, una volta»).
- **Più sector**: il Lab già apre qualunque cartella; si aggiunge l'elenco dei sector aperti di recente per passare
  dall'uno all'altro con un clic. Quale sta in vetrina lo decide «Porta in Aurora».

**Il lotto.** Da quando l'AOD ha detto l'ultima volta «visto in Aurora», il Lab tiene l'elenco di quel che è cambiato
nella vetrina: i gesti salvati (le stesse voci della storia che fanno il messaggio di
«Registra», F4 slice 4) e, per ognuno, **dove guardarlo in Aurora** quando il Lab lo sa (la regola o il tipo di
record: «una MVA → la *MVA Selection*», «una SID → la finestra delle procedure di LIRF»). Un'etichetta nella barra:
«7 modifiche da vedere in Aurora».

**Una ricarica, poi l'esito.** L'AOD ricarica Aurora una volta e scorre il lotto: per ogni voce ✅ «come doveva» o ❌
con due parole («l'etichetta copre il fix»). «Visto in Aurora» chiude il lotto: le ✅ spariscono, le ❌ restano come
note sul loro record (nella scheda e nel pannello dei problemi), finché l'AOD non le toglie.

**Le prove in Aurora diventano lotti preparati.** Una prova (come la T3) = un ramo `prova/<argomento>` fatto dal Lab
con le varianti da provare, e un lotto già scritto con le domande, al posto del `LEGGIMI` a mano. L'esito si scrive nel
lotto, e il ramo di prova si butta (non si manda mai).

### Le slice

| # | Slice | Finita quando (misurato) |
|---|---|---|
| 0 | Misure (§1, fatte) e il formato del lotto sul disco (dati del Lab, per cartella del sector: sopravvive a un riavvio) | — |
| 1 | **La vetrina**: la cartella nelle Impostazioni (col segno), «Porta in Aurora» come specchio della `SectorFiles` aperta, la barra dice cosa c'è in vetrina e se Aurora la guarda (la sola riga `SecondSector`); l'elenco dei sector recenti | sul PC del committente: due sector (ciclo dopo e prova), portati in vetrina a turno, Aurora sempre sulla stessa cartella |
| 2 | **Il lotto**: i gesti salvati da «visto», dove guardarli, il conteggio nella barra; sopravvive al riavvio del Lab | tre salvataggi → «N modifiche da vedere», riaperto il Lab sono ancora lì |
| 3 | **Gli esiti**: ✅/❌ con una nota, «Visto in Aurora», le ❌ come note sui record | una ❌ si vede nella scheda del record e nei problemi, e se ne va solo quando l'AOD la toglie |
| 4 | **Le prove come lotti**: ramo `prova/…` con le varianti, il lotto con le domande; il ramo non si manda | la prossima prova del committente (una delle voci «da provare in Aurora») fatta così, senza `LEGGIMI` |
| 5 | **Il giro vero**: un lotto di modifiche del secondo giro R-10 guardato in Aurora con una ricarica | il committente: una ricarica, gli esiti scritti nel Lab |

**Cosa NON è in F5**: ricaricare Aurora dal Lab (non lo pilotiamo); scrivere le impostazioni di Aurora; i `SYMBOLS_`
dei profili (si decidono con una prova, che la slice 4 rende più facile).

## §4 — Rischi

- **Aurora legge un sector a metà**: se l'AOD ricarica mentre il Lab sta salvando, Aurora può leggere un file mezzo
  scritto. Il salvataggio del Lab è atomico file per file (F3), non l'insieme: il lotto dice «salvato alle …», e se
  l'AOD ricarica durante un salvataggio rifà la ricarica.
- **Il ramo cambia sotto Aurora**: passare a un altro ramo (F4) cambia i file che Aurora legge. Il lotto lo dice
  («cambiato ramo: tutto da rivedere») invece di fingere che sia una modifica piccola.

## §5 — Domande al committente

| # | Domanda | Proposta |
|---|---|---|
| D1 ✅ vetrina | **Dove legge Aurora**: (a) `SecondSector` punta **direttamente alla cartella di lavoro del Lab** (il clone): quel che salvi è quel che Aurora vede alla ricarica, nessuna copia; (b) una **vetrina**: «Porta in Aurora» copia lo stato scelto in una cartella fissa a cui Aurora punta, e intanto nel Lab si continua a lavorare. | **(a)**: più semplice, nessuna copia da tenere allineata; (b) solo se ti serve guardare una cosa mentre ne modifichi un'altra. |
| D2 ✅ solo nel Lab | **Le ❌**: note solo nel Lab (sul PC dell'AOD), o anche un tag `//@` nel record (finisce nel sector e nella PR)? | Solo nel Lab: un esito in Aurora è un appunto di lavoro, non un dato del sector. |
| D3 ✅ la scrivo io | **Dove guardare**: la tabella «tipo di record → dove si vede in Aurora» la scrivo io dalle prove fatte finora e la correggi tu, o la scrivi tu? | La scrivo io come proposta, segnata «da confermare», e la correggi tu alla prima prova. |

**Risposte del committente (18 ottobre)**: D1 **la vetrina** (per lavorare su più sector e scegliere nel Lab quale
vedere, senza toccare Aurora); D2 gli esiti ❌ **solo nel Lab**; D3 la tabella «dove guardare» **la scrive l'agente**,
segnata «da confermare», e la corregge il committente.

✅ Approvata dal committente, 18 ottobre 2026.

## §6 — Traccia

- **Slice 1 (18 ottobre)** — la vetrina. Solo il Lab: `Core/Aurora/Vetrina.cs`, `AuroraNellaBarra.razor`, la vetrina
  nelle Impostazioni, i sector recenti nella schermata d'apertura, `SessioneDelLab` (`CartellaDellaVetrina`,
  `PortaInAuroraAsync`, `SecondoSectorDiAurora`, `SectorRecenti`).
  - **Porta in Aurora**: la `SectorFiles` del sector aperto nella vetrina come specchio (uguali restano, cambiati si
    copiano, quelli che non ci sono più si tolgono, cartelle vuote via), con le modifiche non salvate rifiutate. Il file
    segno `.vetrina-del-lab` dice da quale sector, ramo e commit, e quando.
  - **La vetrina è del Lab**: rifiutata se è dentro un clone git, se è (o contiene, o sta dentro) la `SectorFiles`
    pubblicata di Aurora, se è piena e non ha il segno.
  - **La barra** («◫ Aurora»): «senza vetrina», «vetrina vuota», «questo sector · ramo», o il nome del sector che c'è,
    più «Aurora guarda altrove» se il secondo sector di Aurora non è la vetrina. Il riquadro dice dove puntarlo (si fa
    in Aurora, una volta). Del `profile.ini` di Aurora il Lab legge **solo** la riga `SecondSector` (nei test, un
    `profile.ini` finto con dentro credenziali finte, che non si leggono).
  - **I sector recenti** (fino a otto) nella schermata d'apertura, a un clic: si passa dal clone del ciclo a quello di una
    prova, poi «Porta in Aurora».
  Test Lab 1 014 → **1 019**, due giri verdi. **Provata dal committente il 18 ottobre sera, sul suo PC: funziona**
  (vetrina nelle Impostazioni, Aurora puntata una volta, «Porta in Aurora» coi sector a turno): slice chiusa.
- **Fuori slice (18 ottobre, chiesto dal committente)** — la schermata d'apertura. Il campo «Dal repo» largo e con lo
  stile degli altri; **«Sfoglia…»** accanto alla cartella da aprire, alla cartella dove prendere il sector e alla vetrina
  nelle Impostazioni: la finestra di Windows per scegliere (o creare) una cartella, che apre il guscio dell'app
  (`Program.ScegliCartella`, `FolderBrowserDialog` sul filo della finestra) e che la pagina chiama da
  `SessioneDelLab.ScegliCartella` — senza guscio (test, banco) i tasti non ci sono. I sector aperti di recente come voci
  cliccabili («Aperti di recente», nome e percorso). Visto a schermo sul banco, con una scelta finta.
- **Slice 2 (18 ottobre)** — il lotto. Solo il Lab: `Core/Aurora/Lotto.cs` (`Lotto`, `VoceDelLotto`, `DoveGuardare`),
  `SessioneDelLab` (`Lotto`, `InVetrinaCeQuesto`, `VistoInAurora`; i gesti della storia ricordano i loro file),
  `AuroraNellaBarra.razor`.
  - **Le voci**: a ogni salvataggio, i gesti salvati (le voci della storia, le stesse del messaggio di «Registra»), ognuno
    coi file che ha toccato — quelli le cui modifiche in sospeso sono cambiate col gesto. Un gesto i cui file non sono
    stati salvati (salvataggio interrotto) resta fuori; salvato senza storia (Lab riaperto) = una voce per file.
  - **Due stati**: salvata = «da portare»; dopo «Porta in Aurora» = «da vedere» (solo se in vetrina c'è questo sector).
    La barra: «◫ Aurora … · 4 modifiche da vedere · 1 da portare»; il riquadro elenca le voci con dove guardarle e
    quando. **«Visto in Aurora»** toglie le voci in vetrina (le ✅/❌ prima del «visto» sono la slice 3).
  - **Tutto da rivedere** (carta §4): se «Porta in Aurora» trova in vetrina un altro sector, un altro ramo, o niente, il
    lotto aggiunge una voce sola, «tutto il sector», invece di fingere che sia una modifica piccola.
  - **Sul disco**: nei dati del Lab, `lotti\<impronta del percorso>.json`, uno per cartella del sector, scritto tutto o
    niente (file a parte, poi lo scambio). Illeggibile = si ricomincia (è un appunto di lavoro).
  - **Dove guardare** (D3): una riga per tipo di file (SID → «le SID di LIRF: finestra delle procedure, per pista»,
    `.mva` di scalo → «la MVA Selection, le zone di LIRF», `ENRMVA` → «voce dell'ACC», NAVAIDS → «i punti sulla mappa»…).
    🟡 **Proposta dell'agente, da confermare** dal committente alla prima prova: si corregge in `DoveGuardare.Di`.
  - **Visto a schermo** sul banco (copia del sector): una nota salvata → «1 da portare»; «Porta in Aurora» → «2 modifiche
    da vedere» (la nota e «tutto il sector», prima volta in vetrina); «Visto in Aurora» → il conto sparisce. Dalla prova,
    il riquadro di Aurora ora va a capo (i percorsi lunghi uscivano dal riquadro, già dalla slice 1).
  Test Lab 1 019 → **1 030**, tre giri verdi. Da provare sul PC del committente: tre salvataggi, «N da portare», Lab
  chiuso e riaperto, le voci ancora lì; poi «Porta in Aurora» e il conto «da vedere».
- **Slice 3 (18 ottobre)** — gli esiti. Solo il Lab: `Core/Aurora/Lotto.cs` (`EsitoInAurora`, `RecordDelLotto`,
  `NotaDaAurora`; `Lotto.Segna`, `TogliLaNota`), `SessioneDelLab` (`SegnaNelLotto`, `NoteDaAurora`, `NoteDaAuroraDi`,
  `TogliLaNotaDaAurora`, `VaiAllaNota`), `NoteDaAurora.razor` (nella scheda e in cima ai Problemi), `AuroraNellaBarra.razor`.
  - **✅/❌**: accanto a ogni voce in vetrina (solo se in vetrina c'è questo sector); il ❌ apre un campo per le due parole;
    un secondo clic toglie l'esito.
  - **«Visto in Aurora»**: le ✅ e le **non segnate** escono (guardate, nessun problema detto: scelta dell'agente, perché
    obbligare a segnare tutto rallenta il giro); le ❌ diventano **note** sul loro record.
  - **I record**: ogni gesto ricorda i record che ha toccato col **nome di dopo il gesto** (una rinomina porta il nome
    nuovo), così la nota ritrova il record anche se dopo i record si spostano. Un gesto senza record (su tutto il file)
    lascia una nota sul file.
  - **Le note** (D2: solo nel Lab, nel file del lotto): nella scheda del record e in cima al pannello dei Problemi, col clic
    che porta al record; se ne vanno solo con «Togli».
  - **Visto a schermo** sul banco: una nota salvata, portata, ❌ «etichetta sopra il VOR», «Visto» → «1 nota da Aurora»
    nel riquadro, la nota nella scheda di BC406 e in cima ai Problemi col link al record; «Togli» la leva da tutti e due.
    Il lotto della slice 2 era ancora lì dopo il riavvio del banco.
  Test Lab 1 030 → **1 033**, tre giri verdi. Da provare sul PC del committente: una ❌ con due parole, «Visto», la nota
  nella scheda e nei Problemi, il Lab chiuso e riaperto, «Togli».
- **Slice 4 (18 ottobre)** — le prove come lotti; la prima, scelta dal committente: **l'ordine a blocchi delle aerovie**
  («file per file» B9). Solo il Lab: `Core/Aurora/Prove.cs` (`ProvaInAurora`, `ProveInAurora`,
  `OrdineABlocchiDelleAerovie`), `Lotto` (`Domande`, `TogliLaProva`, `EsitiDelleProve`), `SessioneDelLab`
  (`ProvaInCorso`, `PreparaLaProvaAsync`, `ButtaLaProvaAsync`), il riquadro «◫ Aurora».
  - **Prepara la prova**: dal ramo di adesso un ramo `prova/<argomento>` (il ramo di partenza nello stesso segno dei rami
    di lavoro, `branch.<ramo>.vipibase`), la variante scritta e registrata **solo qui** (un commit locale, «Prova in
    Aurora: …», «non si manda»), il Sector Lab si rilegge, le **domande** entrano nel lotto, da portare in Aurora, ognuna
    col suo «dove guardare». Il ramo `prova/…` non si manda mai: «Manda» accetta solo gli `aod/`.
  - **Le risposte**: ✅/❌ come le altre voci; al «visto» non diventano note su un record ma **esiti della prova**, che
    restano nel lotto (e nel riquadro) anche dopo che il ramo è buttato: sono quel che decide la voce della carta.
  - **Butta la prova** (con conferma): torna al ramo di partenza e cancella il ramo `prova/…`; le domande non ancora viste
    escono dal lotto. Portando una prova in vetrina il lotto non aggiunge «cambiato ramo: tutto da rivedere»: le domande
    dicono già che cosa guardare.
  - **La variante B9** (`itawlow.lairway`): un blocco per aerovia, `//@"NOME"`, `//@START`, le sue etichette, il suo
    tracciato (coi `//@@` dei tratti), `//@END "NOME"`, nell'ordine dei tracciati. Le righe `T` e `L` si **spostano, non
    cambiano** (verificato sul fork: 246 aerovie, 1 385 `T`, 896 `L`, 28 `BREAK`, tutte uguali prima e dopo). Restano
    com'erano l'intestazione di KY139 e la lista `//Waypoint references` in fondo (si toglie a parte, B10); fra i dati si
    tolgono i commenti di sezione e le righe vuote (7 sul fork). Un `BREAK` resta fra i due pezzi che spezza, o in coda
    all'aerovia prima; un'etichetta condivisa va nel blocco della prima in ordine alfabetico (B4); una dichiarazione
    `//@"NOME" …` che c'è già resta. Un'aerovia che tornasse più avanti dopo altre (sul fork non capita) ferma la prova:
    metterne i pezzi di seguito potrebbe unirli.
  - **Le domande della B9**, calcolate dal file: tutte le aerovie come prima (246); le etichette dove stavano (896); L81
    spezzata dove c'è il BREAK; un'etichetta condivisa vista una volta sola; nessun errore nuovo al caricamento per le
    righe `//@`.
  - **Visto a schermo** sul banco (la copia del sector fatta clone git): «Prepara la prova» → ramo
    `prova/aerovie-a-blocchi`, «5 da portare», AIRWAY letto in 352 record (erano 568: un blocco è un record); «Porta in
    Aurora» → «5 modifiche da vedere», senza «cambiato ramo»; ✅, ❌ con la nota, «Visto» → le risposte nel riquadro;
    «Butta la prova» (con conferma) → di nuovo `master`, 568 record, il ramo non c'è più. Dalla prova: preparare e
    buttare rileggono il ramo prima di decidere (la barra era ferma a una lettura di prima e rifiutava per file che non
    c'erano più).
  Test Lab 1 033 → **1 039**, tre giri verdi. La «finita quando» della slice è del committente: la prova B9 guardata in
  Aurora, sul suo PC, senza `LEGGIMI`.
  - **La prova B9 del committente (18 ottobre)**, sul suo PC (clone `it-aurora-sector-test`, prima rimesso pulito: due
    modifiche vecchie, un BREAK di L613 riscritto e `changelog.md` cancellato, fermavano «Prepara la prova», giustamente):
    domande 1, 2 e 5 ✅ (le aerovie si vedono tutte come prima, le etichette al loro posto, nessun errore per le `//@`).
    La 3 e la 4 non si potevano guardare: **in Aurora un'aerovia non si cerca per nome**. Le domande ora dicono i fix:
    «fra GEMVI e MATED e fra DOGUS e NUKNI: lì la linea di L81 non deve esserci», «il tratto comune di L53 e P873,
    OLGAT–OKIMO–ENOXA» (`Conti.BreakFra`, `Conti.CondivisaFra`). Lezione: il «dove guardare» di una prova nomina una
    cosa che in Aurora **si trova** (un fix), non una che si vede solo se sai già dov'è.
    Coi fix nominati il committente ha guardato anche la 3 e la 4: ✅ («ora va»). **La B9 è provata del tutto, senza
    `LEGGIMI`: la «finita quando» della slice 4 è raggiunta.** L'ordine a blocchi si può adottare nel file vero (B1).
- **Slice 5 (18 ottobre)** — il giro vero. Scelte del committente: le correzioni le fa l'agente sul banco, in un ramo
  `aod/704798/…` del fork mandato come PR, e il committente le guarda nel suo Lab; il lotto del giro sono **le etichette
  delle aerovie** (R-10: 53 da aggiungere, 24 da rinominare, 25 lontane; B4, B12).
  - **Il lotto dalle registrazioni** (`Core/Aurora/Registrazioni.cs`, `Lotto.Registrate`): il lavoro che arriva da git
    (un altro AOD, un altro PC, salvato prima di riaprire il Lab) il lotto non l'ha fra i gesti salvati qui. «Porta in
    Aurora», se in vetrina c'era lo stesso sector a un commit che sta nella storia di quello di adesso, legge le
    registrazioni fra i due (`git log --no-merges`): un gesto per riga `- …` del messaggio (come lo scrive «Registra»),
    o il titolo. Entrano **solo per i file che la vetrina ha cambiato davvero** (`EsitoDellaVetrina.Cambiati`): una
    registrazione già vista prima di registrarla non torna, e un gesto già fra le voci portate adesso nemmeno. Al posto
    del «cambiato ramo: tutto da rivedere», che resta quando la storia non si ripercorre (un altro ramo da `master`).
  - **Sul server, da guardare** (`RamiDelSector.DaGuardareAsync`, `SessioneDelLab.RamiDaGuardare`): nel riquadro del ramo
    i rami `aod/…` del server che qui non ci sono; un clic ci passa (`git switch` crea il ramo che segue quello del server),
    poi «Porta in Aurora», e il lotto ha i suoi gesti — prima di fondere la PR.
  - **Il lavoro, dal Lab sul banco** (clone del fork nello scratchpad, `master` `f305e6d`): «Nuovo lavoro» →
    `aod/704798/r-10-etichette-aerovie`; su `itawlow.lairway` «Sistema le etichette» (54 nuove, 24 rinominate, 25 tolte
    e rifatte; tratti ≥ 10 NM), «Salva», «Registra» col messaggio corretto a mano come un AOD: **una riga `- …` per tipo
    di correzione, coi fix dove guardarle** (L81 fra TETUZ e LORNO, L153 vicino a PIS, L869 vicino a TRP, L613 vicino a
    VIC) — sono le voci del lotto del committente. Push del ramo e PR #9 sul fork fatti da riga di comando: sul banco
    «Manda» aprirebbe il browser di Windows del committente. Avvisi 1 119 → 1 076, errori 140; restano 7 aerovie senza
    etichetta, coi tratti tutti sotto i 10 NM (Q482, T345, T369, Y138, Y480, Y498, Y526).
  - Dal banco: «Sul server, da guardare» mostrava anche i rami delle PR già fuse. Ora solo quelli non ancora fusi nel
    ramo predefinito (`for-each-ref --no-merged origin/master`).
  - **Trovato sul PC del committente** (18 ottobre): il suo clone `it-aurora-sector-test` scarica solo `master`
    (`remote.origin.fetch = +refs/heads/master:…`, un clone a un ramo solo). «Chiedi le novità» non vedeva né il ramo da
    guardare né `airac/2611` («sul server non c'è ancora», falso). Ora ogni `fetch` del Lab chiede **tutti i rami** per
    esteso (`RamiDelSector.ChiediAlServer`, `+refs/heads/*:refs/remotes/origin/*`), e il ramo del server da guardare si
    crea da `origin/…` per esteso (`RamiDelSector.GuardaAsync`: in quel clone `git switch` non sa indovinarlo). Il test
    del giro gira ora su un clone a un ramo solo.
  - **Il giro del committente (18 ottobre, 11:51)**: `master` del clone portato al passo, «Porta» e «Visto» per
    azzerare; «Chiedi le novità» → «Sul server, da guardare» → `aod/704798/r-10-etichette-aerovie`; «Porta in Aurora»:
    1 file copiato, **3 voci dalle registrazioni** (1 commit dopo `f305e6d`); una ricarica; **3 ✅**, «Visto». **Slice 5
    chiusa: la F5 è fatta.** La PR #9 la fonde il committente.

## §7 — Dopo la F5: le altre prove in Aurora, preparate dal Lab (18 ottobre)

Decisione del committente: il sector vero si tocca dopo l'uscita del ciclo 2611 (29 ottobre); intanto le prove ancora
aperte di «file per file» si preparano come lotti (slice 4), sul fork. Sei prove nuove nel riquadro «◫ Aurora → Prove
in Aurora», ognuna un ramo `prova/…` con le domande:

| Prova | Voce | Variante (sul fork) |
|---|---|---|
| `vfr-di-fir-come-fix` | F5 | i tre file di `ENRVFI` da `[VFRENR]` a `[VFRFIX]` in `ITALY.isc` e negli `.isc` di LIMM, LIPP, LIRR |
| `etichette-per-riferimento` | A8 | le 104 etichette `L;` di `FRA.artcc` per riferimento al fix omonimo; e un fix (il primo che sta in un file solo, ADEXI) 3′ più a nord, per vedere che l'etichetta lo segue |
| `punti-di-startup` | R4 | `LIRF;MAPS;STARTUP; ; ;2;` dopo l'ultima voce del MAPS di `lirf.str`: due trattini (S1, S2) sugli stand 101 e 102 — le posizioni vere non ci sono, conta dove compare la mappa |
| `file-di-scalo-cartella-per-scalo` | I7-C | tutto LIRF in `IT\AD\LIRF` (`.sid .str .gts .txi .vfi`, `lirf.geo`, `lirf.tfl` dal `.pol`), la cartella nella riga 6, via le 6 righe di LIRF dagli `.isc` |
| `file-di-scalo-cartella-per-tipo` | I7-B, O4 | la terra di LIRF in `IT\GND`, un file per tipo dal colore della riga (`.rwy .txo .txc .stp .apr .pier .bld`) e `lirf.tfl`, la cartella nella riga 6, via le righe dagli `.isc` |
| `spazi-bassi-un-file-per-acc` | K2 | `limm.lartcc`, `lipp.lartcc`, `lirr.lartcc` coi file di settore uniti (col nome del file sopra), una riga per ACC negli `.isc`; le STAR di scalo restano |

- Dal manuale IVAO («SectorFile Definition», letto col browser del pannello: al recupero automatico risponde 403):
  `ICAO.geo` e `ICAO.tfl` si caricano da soli se lo scalo è fra gli aeroporti e il file sta in una cartella della riga 6;
  così i file per tipo `ICAO.rwy/.txo/.txc/.stp/.apr/.pier/.bld`, con le righe del `.geo` (`LAT1;LON1;LAT2;LON2;COLORE;`).
  Le I7 si fondano su questo, non su un formato immaginato.
- Le proposte A (nomi uniformi, righe scritte dal Lab: nessun rischio in Aurora) e D (radice, sconsigliata dal
  committente) non hanno una prova. La O4 (dove stanno le marcature) la risponde la B: colore RUNWAY → `.rwy`; LIRF non
  ha un file di marcature, quindi la domanda è sulle piste.
- Una prova ora può **togliere** file (testo nullo) e crearne in cartelle nuove; il giro git le registra tutte (test).
- Verificate a vuoto sul clone del fork: le righe si spostano, non cambiano (la B: 5 296 righe del `.geo` nei sette file
  per tipo; una riga di dati commentata resta commento). Trovato facendolo: l'a capo dei file uniti della K2 letto dal
  testo già accorciato (un file di una riga diventava `\n`): corretto.
  Test Lab 1 044 → **1 059**, due giri verdi. Da provare dal committente, una alla volta: «Prepara la prova», «Porta in
  Aurora», una ricarica, le risposte, «Visto», «Butta la prova».

### §7-bis — I piccoli resti del Lab (18 ottobre)

- **C2 della F4, «l'ultima riga tolta rimette `*NIL`»**: nel riquadro del ramo, sotto il changelog, «Righe già nel
  changelog» con un «togli» per riga (`Changelog.Scritte`, `Changelog.Togli`, `SessioneDelLab.TogliDalChangelogAsync`);
  se era l'ultima della sua sezione torna `*NIL`, le altre righe restano byte per byte. La lettura e la scrittura del
  file (UTF-8, BOM, a capo) sono ora un giro solo per «Scrivi» e «togli».
- **I `SYMBOLS_*` dei profili** (T1, seconda metà): prima la prova, ora preparata — `simboli-nel-profilo` mette in
  `PREFS\TWR.cpr` `SYMBOLS_AIRPORT=` col disegno del simbolo «VOR» di `symbols.sym` (la stessa forma dei profili
  installati, sezione `[PREFS]`, letta da un profilo di Aurora senza scriverlo). Domande: con una posizione di torre
  gli aeroporti diventano VOR? La linguetta SYMBOLS lo mostra? Passando a un'altra posizione, torna com'era? L'editor
  dei `SYMBOLS_*` di un `.cpr` si fa dopo l'esito.
- **Nomi con spazi nelle composte**: erano già chiusi dalla §M (virgolette nell'elenco); corretta la nota nel filone.
- La tabella «dove guardare» (D3) resta da confermare dal committente: non è codice.
  - Visto a schermo sul banco: «Righe già nel changelog (20)» nel riquadro del ramo, ognuna col «togli»; le righe lunghe ora vanno a capo.
