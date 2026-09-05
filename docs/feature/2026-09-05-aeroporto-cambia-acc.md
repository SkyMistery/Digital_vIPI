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
- ⚠️ **I link vecchi restano vivi ma mentono**: `/services/vsop/libb/airports?icao=LIBD` carica lo stesso (la
  pagina risolve per ICAO) e mostra la briciola di pane dell'ACC sbagliato. Nessun redirect — annotato, non
  corretto in questo giro.
- ⚠️ **La vIPI ACC già pubblicata continua a citarlo** finché non si ripubblica: è una release **congelata**,
  ed è giusto così.
- ⚠️ `FeaturedRank` viaggia con l'aeroporto: se era in evidenza su Brindisi, si presenta in evidenza su Roma.

## Verifiche

- Suite verde su due TFM, `dotnet build Vipi.slnx -c Release --no-incremental` 0 avvisi.
- Prove nuove: `AeroportoCambiaAccTests` (anagrafica + catalogo + proiezione; **la riproiezione non riporta
  indietro i settori**; il padre fuori ACC resta staccato) e `AirportAccDivergencesTests` (il disaccordo, le
  maiuscole, l'ACC assente, l'aeroporto che la sorgente non nomina, l'ordine).
- **Live**: LIBD spostato davvero dalla tendina della pagina admin. Prima della correzione il catalogo restava
  su LIBB; dopo, catalogo e proiezione dicono LIRR e i padri fuori ACC sono staccati. L'avviso di divergenza
  è comparso a schermo con la sorgente vera: «LIBD Bari Palese: qui sotto LIRR, nella sorgente sotto LIBB».
