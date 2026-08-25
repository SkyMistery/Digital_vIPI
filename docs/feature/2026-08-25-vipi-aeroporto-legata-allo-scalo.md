# La vIPI di un aeroporto è dell'AEROPORTO, non di un suo settore (25 agosto 2026)

**Segnalato provando a pubblicare LIBG**: «mi dice *Nessun contenuto da pubblicare: crea prima il documento
(bozza)*, ma il documento l'ho creato da Nuovo documento».

Il documento c'era. Ce n'erano **quattro**.

```
19 | vIPI — LIBG Taranto Grottaglie | Draft |  12:37:17
20 | vIPI — LIBG Taranto Grottaglie | Draft |  12:37:29
21 | vIPI — LIBG Taranto Grottaglie | Draft |  12:37:36
22 | vIPI — LIBG Taranto Grottaglie | Draft |  12:38:08
```

Tutti e quattro **orfani**: nessun settore vi puntava, nessuna release li citava, nessuna pagina li raggiungeva.

## 1. Perché, e perché proprio LIBG

Il documento d'aeroporto si agganciava ai **settori** dello scalo:

```csharp
var sectors = await _db.Sectors.Where(s => s.AirportId == airport.Id && s.Type != SectorType.App)…
var primary = sectors.FirstOrDefault(s => IsTower(s.Type)) ?? sectors.FirstOrDefault();
foreach (var s in sectors) { s.DocumentId = doc.Id; … }   // ciclo su ZERO elementi
```

Regge finché lo scalo ha una torre. Su IVAO, **Taranto Grottaglie ha una sola postazione: `LIBG_APP`**, e non è
remotizzata — un APP standalone, che ha un **documento tutto suo** e per regola (`SectorDocumentRules`) non può
portare la vIPI dell'aeroporto. Niente TWR, niente GND, niente DEL.

Quindi: il documento nasceva legato a nessuno; il ramo «documento esistente», che lo cercava **attraverso gli
stessi settori**, non poteva più ritrovarlo; ogni riapertura dell'editor ne creava un altro. E la pubblicazione,
non trovando contenuto, rispondeva *«crea prima il documento»* a chi l'aveva appena creato quattro volte.

| aeroporto | settori | prima |
|---|---|---|
| LIBD | `LIBD_TWR` + APP remotizzato | ✅ |
| LIBA | `LIBA_TWR` + `LIBA_APP` standalone | ✅ |
| **LIBG** | **solo `LIBG_APP` standalone** | ❌ quattro orfani |

ⓘ Il segnale era già a schermo da prima: sulla riga LIBG della pagina Aeroporti c'è il badge **«no TWR»**, quello
che il chip conta come **11 aeroporti**. Nessuno aveva collegato quel badge a questo difetto.

## 2. La decisione

Del committente, e coglie il punto: **un documento d'aeroporto descrive un AEROPORTO**, indipendentemente dai
settori. APP e vLOA restano legati ai **settori**, perché è ciò che descrivono davvero.

| documento | legame |
|---|---|
| vIPI d'aeroporto | **`Airport.DocumentId`** |
| vIPI di APP | settore (`LIBG_APP`) |
| vLOA | i due settori parti |

**Uno a uno, con indice UNICO.** Non è pignoleria: due scali che puntano allo stesso documento sarebbero un
difetto visibile solo mesi dopo, a schermo, come un aeroporto che mostra le piste di un altro. I NULL restano
molti (gli scali senza documento) e tutti e tre i provider ne ammettono più d'uno in un indice unico.

**I settori restano legati** allo stesso documento (`Sector.DocumentId`), su richiesta esplicita: serve a chi
parte da un callsign. È una **proiezione** del legame di sopra, non una seconda verità — e il rebuild li
riallinea, compresi quelli comparsi **dopo** la prima generazione (una torre che IVAO aggiunge più tardi
restava altrimenti scollegata per sempre).

## 3. I documenti già scritti

`IDocumentMaintenance.LinkAirportDocumentsAsync`, che gira **a ogni avvio prima delle altre riconciliazioni** ed
è idempotente. Legge dove il dato viveva prima — i settori — e scrive sull'aeroporto.

⚠️ **Deve escludere l'APP non remotizzato.** Collegando lo scalo a *quel* documento, l'aeroporto mostrerebbe la
documentazione del proprio avvicinamento. È l'unico lavoro per cui `SectorDocumentRules` sopravvive: il commento
in testa a quel file ora lo dice, e la sua variante in memoria — che non serviva più a nessuno — è stata tolta.

Misurato sul `vipi.db` di sviluppo: **5 aeroporti collegati**, e LIBA è andato al proprio documento (**18**) e non
ad «Amendola Approach» (**3**).

## 4. Le sette letture

Passavano tutte dalla regola sui settori; ora chiedono all'aeroporto: `GetDocumentIdAsync`, la sezione SID della
versione corrente, documento→ICAO per il bersaglio della release, il filtro del viewer, `ResolveDocumentIdAsync`
e `TryDescribe` del release target, e la colonna «documento» della pagina Aeroporti (che era rimasta indietro,
nascosta dietro un commento che descriveva la vecchia strada).

⚠️ `TryDescribe` lavora su un `Document` **già caricato**: i suoi tre chiamanti devono portarsi dietro
`.Include(d => d.Airport).ThenInclude(a => a.Acc)`, o l'ICAO esce vuoto e il documento diventa irraggiungibile
**senza dare errore**.

## 5. Verificato

Guidando l'applicazione vera su una copia del `vipi.db`:

- avvio: «**Collegati 5 aeroporti alla loro vIPI**» nei log, e i documenti giusti (§3);
- LIBG: selezionato in Aeroporti → «Genera documenti» → **1 documento** (non quattro) → editor → **«Release
  published now»** — l'errore è sparito;
- la sua pagina pubblica mostra METAR & TAF, Transition levels, Frequenze, Piste, SID;
- in archivio: `Airports.DocumentId = 23`, release `Airport/LIBG` effettiva 2608, **nessun aeroporto che
  condivide un documento**, e `LIBG_APP` resta **senza** documento — giusto, il suo è un'altra cosa.

**2013 test su net8 e 2004 su net10**, tutti verdi, Release verde su entrambi i TFM.

Nel giro anche cinque test nuovi (`LinkAirportDocumentsTests`) che presidiano il ponte: collega chi ha la torre,
**non** sceglie il documento dell'APP, lascia scollegato chi ha solo l'APP, è idempotente, e non riscrive una
scelta già fatta.

## 6. Cosa resta

- Il **documento APP di LIBG** non esiste ancora: si crea da `/services/vsop/libb/apps/editor?app=LIBG_APP`. La
  sua testata mostra già l'etichetta militare; il viewer la mostrerà appena il documento c'è.
- I quattro orfani sono stati rimossi dal `vipi.db` di sviluppo (backup
  `vipi.db.bak-pre-pulizia-orfani-libg-20260825`). In produzione non ce ne sono: il difetto si manifesta solo
  aprendo l'editor di uno scalo senza torre.
