# API degli aeroporti: scali, scheda, SID e STAR per gli altri programmi — carta (30 settembre 2026)

> **Stato: ✅ ESEGUITA il 30 settembre 2026** sul ramo `fix/api-aeroporti` (filone Sito, S64), provata dal vivo su una
> copia del DB (§5). Nessuna migrazione, nessun `deploy/`. Da fondere.
> Nasce da una richiesta del committente (30 settembre 2026): «un'API mediante la quale si possono richiedere le info su
> un aeroporto (nel caso specifico tutte le info sulle SID, ma potrebbe tornare utile anche per altro)».
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md). Regole delle chiavi: [2026-09-13-chiavi-api.md](2026-09-13-chiavi-api.md).
>
> Dove sta: `ApiAeroporti` (Hosting: rotte e proiezione in JSON) · `ApiEndpoints.Aeroporti` (Domain: il permesso
> `aeroporti` delle chiavi) · test in `Vipi.Hosting.Tests/ApiAeroportiTests` e `Vipi.E2E.Tests/ChiaviApiTests`.

## 1. Le decisioni del committente (30 settembre 2026)

1. **Si espone la vista pubblica**, non l'archivio: chi chiama legge quello che legge un pilota nel documento
   pubblicato. Niente righe nascoste, niente procedure in attesa del loro ciclo AIRAC, i punti corretti a mano già
   applicati.
2. **Quattro letture nel primo giro**: elenco degli scali, scheda dello scalo, SID, STAR.
3. **Chi la usa: un programma di un altro reparto o di un'altra divisione IVAO**, quindi una chiave API come quelle
   dell'archivio, con un permesso suo.

## 2. Pre-flight

**1. Modello.** Nessun concetto nuovo. Le SID escono da `IAirportViewDerivationService.ResolveForViewAsync(icao,
useFrozen: true, edizione)`, cioè **la stessa chiamata della pagina pubblica** (`AirportMemberLoader`): release in
vigore dove la sezione è congelata, derivazione al ciclo AIRAC corrente dove è viva. Una seconda strada verso la
tabella `AirportProcedures` avrebbe divergito dal documento alla prima correzione a mano. Il cancello degli scali è
`AwosGate` (documento con release effettiva e non nascosto), lo stesso del vAWOS: «che cosa vede il pubblico» ha una
risposta sola.

**2. Dispatch.** Uno solo, e nuovo: quale documento leggere (`ApiAeroporti.Edizione`). La vIPI civile se è
pubblicata, altrimenti il vSOP militare. ⚠️ Non è un dettaglio: l'edizione dice da quale **release** si leggono le
sezioni congelate, e su un campo solo militare chiedere la civile ricadrebbe sempre sul vivo.

**3. Ingressi e verifica.** Le chiavi si emettono dalla pagina che c'è già (`/services/vsop/admin/api-keys`), che
elenca da sé il permesso nuovo (`ApiEndpoints.Tutti`). Verifica: test puri della proiezione, E2E della porta
(401/403/200/404) e prova dal vivo con curl su una copia del DB.

**4. Propagazione.** Additiva: niente si toglie o si rinomina.

## 3. Come si usa

Header: `Authorization: Bearer vipi_…` oppure `X-Api-Key: vipi_…`. La chiave deve avere il permesso **Aeroporti**.
La chiave è **sempre** obbligatoria, anche dove l'archivio ha ancora `Api:RichiediChiave=false`: queste API nascono
dopo la regola e non hanno client di prima da non fermare.

| Richiesta | Risposta |
|---|---|
| `GET /vsop/api/v1/airports` | gli scali con un documento pubblicato |
| `GET /vsop/api/v1/airports/{icao}` | la scheda: TA, fasce TL, piste, frequenze |
| `GET /vsop/api/v1/airports/{icao}/sids[?runway=16L]` | le SID, nell'ordine del documento |
| `GET /vsop/api/v1/airports/{icao}/stars[?runway=16L]` | le STAR, stessa forma |

Codici: **401** senza chiave o con una chiave sconosciuta/revocata · **403** chiave buona senza il permesso
Aeroporti · **404** scalo sconosciuto **o senza documento pubblicato** (le SID che lo staff non ha ancora dato al
pubblico non escono da una porta laterale) · **429** oltre 60 richieste al minuto per chiave (600 in tutto).

Il filtro `?runway=` segue la regola del documento: le procedure di quella pista **e** quelle che non ne indicano
nessuna, perché valgono per tutte. Senza corrispondenze esce un elenco vuoto, non un errore.

I campi vuoti del documento (il «—» delle celle) sono `null`.

### Esempi

```bash
curl -H "Authorization: Bearer vipi_…" "https://atc.it.ivao.aero/vsop/api/v1/airports/LIBD/sids?runway=07"
```

Risposta vera (copia del DB, 30 settembre 2026; tre righe delle venti):

```json
{
  "icao": "LIBD",
  "transitionAltitudeFt": 5000,
  "count": 20,
  "sids": [
    { "runway": "07", "fix": "BANAV", "name": "BANA6W", "transition": null,
      "initialClimb": "5000 ft", "initialClimbFt": 5000, "initialClimbByApp": false,
      "type": "RNAV", "cat": "A, B, C, D, E", "wtc": "L, M, H, S", "condition": null },
    { "runway": "07", "fix": "BANAV", "name": "BANA8A", "transition": null,
      "initialClimb": "FL90", "initialClimbFt": 9000, "initialClimbByApp": false,
      "type": "CONV", "cat": "A, B, C, D, E", "wtc": "L, M, H, S", "condition": null },
    { "runway": "07", "fix": "EKMUR", "name": "EKMU5W", "transition": null,
      "initialClimb": "to coord with APP", "initialClimbFt": null, "initialClimbByApp": true,
      "type": "RNAV", "cat": "A, B, C, D, E", "wtc": "L, M, H, S", "condition": null }
  ]
}
```

- `initialClimb` è scritto **come nel documento**: in piedi fino alla TA, in livello di volo sopra, con la nota
  dell'APP se c'è. `initialClimbFt` è la stessa quota in piedi, per i programmi (`null` se il documento dice solo
  «to coord with APP»); `initialClimbByApp` dice che va concordata con l'APP. Nelle STAR i tre campi sono
  `null`/`false`.
- `/stars` ha la stessa forma, con `stars` al posto di `sids`.

Scheda (`/vsop/api/v1/airports/LIBD`, accorciata):

```json
{
  "icao": "LIBD", "name": "Bari Palese", "acc": "LIBB", "document": "vipi",
  "transitionAltitudeFt": 5000,
  "transitionLevels": [ { "qnh": "≤ 976", "level": "FL75" }, { "qnh": "977 – 994", "level": "FL70" }, … ],
  "runways": [
    { "ident": "07", "lengthM": 3000, "tora": "3000", "lda": "3000", "approaches": ["ILS", "LOC", "VOR", "RNAV"],
      "patterns": "L", "circling": "N", "threshold": "N41°07'58.31''E016°44'26.26''", "thresholdElevationFt": 193 },
    …
  ],
  "frequencies": [ { "name": "Bari ATIS", "callsign": "LIBD_ATIS", "frequency": "…", "primary": … }, … ]
}
```

`document` dice da quale documento si è letto: `vipi` (civile) o `vsop` (militare, per i campi che hanno solo quello).

Elenco (`/vsop/api/v1/airports`): `{ "count": 10, "airports": [ { "icao": "LIBD", "name": "Bari Palese", "vipi": true,
"vsop": false }, { "icao": "LIBG", "name": "Taranto Grottaglie", "vipi": false, "vsop": true }, … ] }`.

## 4. Fuori da questo giro

- **Il ciclo AIRAC entrante** (`?cycle=`): la vista pubblica è una sola, quella in vigore. Chiedere «come sarà» vuol
  dire leggere la release programmata, che è un'anteprima di staff: se servirà, è una decisione a parte.
- **Regole piste, minimi LVP, meteo**: la scheda porta oggi quello che serve per le SID (TA per l'initial climb, piste
  per il filtro) più le frequenze. Il resto si aggiunge nella stessa forma quando qualcuno lo chiede.

## 5. Verifica dal vivo (30 settembre 2026)

Su una copia del `vipi.db` di sviluppo (cancellata dopo), app locale con due chiavi di prova scritte nella copia:

- porta: senza chiave **401**, chiave dell'archivio **403**, chiave Aeroporti in `X-Api-Key` **200**, `ZZZZ` **404**;
- elenco: **10 scali**, sei vIPI e quattro solo vSOP, letti ognuno dal documento giusto (`document`);
- **LIBD `?runway=07`: le 20 righe dell'API sono identiche, cella per cella, alle 20 della tabella della pagina pubblica**
  (che si apre già filtrata sulla pista in uso); **LIRS** (solo vSOP) `?runway=03L`: 10 su 10 come la pagina del vSOP;
- STAR: zero ovunque, ed è giusto: nella copia la tabella delle procedure ha solo SID (1 469 righe);
- UTF-8 corretto nel JSON («≤ 976», «N41°07'…»).

Un difetto visto lì e corretto: l'elenco diceva «MIL — LIBG Taranto Grottaglie». I vSOP militari nascono col titolo
«vSOP MIL — …», e `AwosGate.NomeDalTitolo` toglieva «vSOP» ma non «MIL». Ora toglie anche «MIL», solo come parola
intera («Milano» resta). Lo stesso nome lo usa la tendina del vAWOS, che si corregge insieme.
