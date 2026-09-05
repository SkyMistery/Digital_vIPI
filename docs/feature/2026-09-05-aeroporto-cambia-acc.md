# Un aeroporto che cambia ACC (5 settembre 2026)

> **Domanda del committente**: «se un aeroporto nel DB di IVAO cambia ACC, cioè passa da LIBB a LIRR, che
> succede?» Risposta misurata: **non succede niente, e nessuno lo dice**. E se poi lo si sposta a mano, lo
> spostamento **non teneva**.

## 1. Automaticamente non si muove niente — ed è voluto

L'ACC di un aeroporto, dopo la nascita, lo decide **il nostro archivio**, non la sorgente:

- `AutoAssignAirportsAsync` è **additiva**: salta gli ICAO già presenti. Il `centerId` della sorgente serve
  solo a far nascere gli aeroporti **nuovi**.
- `SyncAirportSourceFieldsAsync` riallinea presenza militare, IATA, quota, variazione magnetica e il timbro
  `LastSeenAtUtc`. **`AccId` non lo tocca.**

Va bene così: spostare un aeroporto stacca i padri fuori ACC, cambia gli elenchi di due centri e sposta un
documento su cui qualcuno sta scrivendo. Non è un lavoro da giro notturno. Ma il **silenzio** non va bene: la
divergenza non aveva nessun modo di farsi vedere, mai.

**Ora si segnala**, in due posti e con una regola sola (`AirportAccDivergences.Trova`):

- nella pagina **Gestione aeroporti**, entrando — non solo dopo aver premuto «Assegna aeroporti noti», o la
  vedrebbe soltanto chi già sospetta qualcosa;
- nel **registro** del giro notturno, una riga per aeroporto.

⚠️ Un ACC **vuoto** nella sorgente non è una divergenza: è un dato che non c'è.

## 2. Lo spostamento a mano non teneva

`MoveAirportAsync` scriveva l'anagrafica e i `Sector` **proiettati**. Ma i `Sector` sono una proiezione: la
fonte è `AirportSector`, e lì restava il **vecchio** codice ACC. Alla prima riproiezione — il giro notturno, o
un qualunque salvataggio che la scatena — i settori **tornavano indietro**, e con loro il padre.

**Misurato guidando l'app** (LIBD da LIBB a LIRR), prima della correzione:

| | dopo lo spostamento | dopo una riproiezione |
|---|---|---|
| `Airport.AccId` | LIRR | LIRR |
| `AirportSector.AccCode` (**fonte**) | **LIBB** | LIBB |
| `Sector.AccId` (proiezione) | LIRR | **LIBB** |
| padre dell'APP | staccato | **riattaccato a `LIBB_ES_CTR`** |

Ora lo spostamento porta con sé **anche il catalogo**: `AccCode` su tutte le righe dell'aeroporto, e i padri
che restano fuori dal nuovo ACC si staccano **in catalogo** — non solo nella proiezione, o tornerebbero anche
loro. Un padre cross-ACC resta possibile, ma dev'essere una scelta di chi edita: un APP che continua a pendere
dal CTR del centro appena lasciato è un residuo, non una scelta.

## 3. Che cosa NON si rompe (verificato a schermo)

- **I documenti stanno all'aeroporto, non all'ACC** (`Airport.DocumentId`, `MilDocumentId`): civile e militare
  seguono lo scalo. Le **release** hanno per chiave l'ICAO: nessuna si sposta, nessuna si perde.
- **Gli elenchi seguono**: LIBD sparisce da quello di Brindisi e compare in quello di Roma.
- **Gli editor funzionano** da entrambi gli indirizzi.
- **I permessi non c'entrano**: dal 28 agosto l'Editor edita tutto, quindi nessuno resta fuori.
## 4. Le tre code, chiuse

Le tre cose annotate qui sopra sono state corrette nello stesso giro.

**a) I link vecchi non mentono più.** Le pagine d'aeroporto e di vSOP militare stanno sotto
`/services/vsop/{acc}/…` ma risolvono il documento per **ICAO**: l'ACC nella rotta è chrome. Un link scritto
prima dello spostamento continuava quindi a funzionare mostrando l'ACC sbagliato — una pagina che dice il
falso senza rompersi, che è il modo peggiore di sbagliare. Ora le quattro pagine (documento ed editor, civile e
militare) rimandano all'indirizzo giusto sostituendo la voce nella cronologia. La domanda la fa
`RottaAeroporto` al **catalogo delle stazioni**, che è già in cache di processo e si aggiorna da sé quando una
riga `Airport` viene salvata: nessuna query mentre la pagina si disegna. Un ICAO che il catalogo non conosce
non manda da nessuna parte — la pagina sa già dire «non trovato».

**b) I documenti dei due centri si segnalano.** Lo spostamento apre un impatto `AirportAccChanged` su tutto
ciò che raccontava quelle posizioni: la vIPI del centro che perde lo scalo, quella del centro che lo prende, il
documento dello scalo e i vicini nella catena di copertura. ⚠️ I documenti si cercano **due volte**, sotto il
vecchio codice e sotto il nuovo: il reverse-lookup risale alla vIPI ACC passando per il codice del centro, e
dopo lo spostamento quello di prima non lo porta più nessuna riga. È un **evento**, non un calcolo: non c'è
niente da ricalcolare, c'è una frase — «Bari è dentro il settore ES» — che nessun calcolo sa riscrivere. La
chiude una persona quando ha riletto. La copia già pubblicata resta com'è: è congelata, ed è giusto.

**c) «In evidenza» non segue lo scalo.** `FeaturedRank` dice quali aeroporti mette in prima pagina la landing
di **un** centro. Portandoselo dietro, uno scalo appena arrivato si presentava in evidenza a Roma perché lo era
a Brindisi — una decisione che nessuno di Roma aveva preso. Ora lo spostamento lo azzera.

### 🔴 Il difetto che la verifica live ha preso, e i test no

La frase del punto (b) ha **tre** segnaposto. La riga del banner ne componeva al massimo **due** e buttava via
il resto: `L[chiave, args[0], args[1]]`. Risultato: `FormatException` durante il render — e siccome quella riga
vive dentro il banner dell'editor, non si rompeva la riga, **non partiva la pagina**. Suite verde, editor
d'aeroporto morto. Ora gli argomenti si passano tutti, e c'è una prova con tre.

⚠️ **La vIPI ACC già pubblicata continua a citarlo** finché non si ripubblica: è una release **congelata**, ed
è giusto così — ma adesso chi la cura ha la riga che glielo dice.

## Verifiche

- Suite verde su due TFM, `dotnet build Vipi.slnx -c Release --no-incremental` 0 avvisi.
- Prove delle tre code: `RottaAeroportoTests` (si corregge / si lascia stare / ICAO sconosciuto),
  `AeroportoCambiaAccTests` (l'evidenza non segue, lo spostamento racconta che cosa ha mosso, e spostarlo dove
  già sta non è un evento), `DocReviewBarTests` (una frase con **tre** argomenti li prende tutti).
- **Live, dopo le correzioni**: `/services/vsop/libb/airports?icao=LIBD` finisce su `/lirr/…` con la briciola
  giusta (e l'editor pure); gli indirizzi già giusti non si muovono; quattro impatti aperti — vIPI Brindisi,
  vIPI Roma, vIPI LIBD e vIPI LIBR — con la frase «Airport LIBD moved from LIBB to LIRR…» a schermo nel banner;
  `FeaturedRank` azzerato.
- Prove nuove: `AeroportoCambiaAccTests` (anagrafica + catalogo + proiezione; **la riproiezione non riporta
  indietro i settori**; il padre fuori ACC resta staccato) e `AirportAccDivergencesTests` (il disaccordo, le
  maiuscole, l'ACC assente, l'aeroporto che la sorgente non nomina, l'ordine).
- **Live**: LIBD spostato davvero dalla tendina della pagina admin. Prima della correzione il catalogo restava
  su LIBB; dopo, catalogo e proiezione dicono LIRR e i padri fuori ACC sono staccati. L'avviso di divergenza
  è comparso a schermo con la sorgente vera: «LIBD Bari Palese: qui sotto LIRR, nella sorgente sotto LIBB».
