# API degli aeroporti: scali, scheda, SID e STAR per gli altri programmi — carta (30 settembre 2026)

> **Stato: 🔨 in esecuzione** sul ramo `fix/api-aeroporti` (filone Sito, S64). Nessuna migrazione, nessun `deploy/`.
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
curl -H "Authorization: Bearer vipi_…" https://atc.it.ivao.aero/vsop/api/v1/airports/LIRF/sids?runway=16L
```

```json
{
  "icao": "LIRF",
  "transitionAltitudeFt": 6000,
  "count": 1,
  "sids": [
    {
      "runway": "16L", "fix": "ELKAP", "name": "ELKAP5A", "transition": null,
      "initialClimb": "FL90 (to coord with APP)", "initialClimbFt": 9000, "initialClimbByApp": true,
      "type": "RNAV", "cat": null, "wtc": null, "condition": null
    }
  ]
}
```

- `initialClimb` è scritto **come nel documento**: in piedi fino alla TA, in livello di volo sopra, con la nota
  dell'APP se c'è. `initialClimbFt` è la stessa quota in piedi, per i programmi; `initialClimbByApp` dice che va
  concordata con l'APP. Nelle STAR i tre campi sono `null`/`false`.
- `/stars` ha la stessa forma, con `stars` al posto di `sids`.

Scheda (`/vsop/api/v1/airports/LIRF`):

```json
{
  "icao": "LIRF", "name": "Roma Fiumicino", "acc": "LIRR", "document": "vipi",
  "transitionAltitudeFt": 6000,
  "transitionLevels": [ { "qnh": "1013-1031", "level": "FL70" } ],
  "runways": [
    { "ident": "16L", "lengthM": 3900, "tora": "3900", "lda": "3600", "approaches": ["ILS", "RNP"],
      "patterns": null, "circling": null, "threshold": "41°50'…N 012°13'…E", "thresholdElevationFt": 14 }
  ],
  "frequencies": [ { "name": "Tower", "callsign": "LIRF_TWR", "frequency": "118.700", "primary": true } ]
}
```

`document` dice da quale documento si è letto: `vipi` (civile) o `vsop` (militare, per i campi che hanno solo quello).

Elenco (`/vsop/api/v1/airports`): `{ "count": 2, "airports": [ { "icao": "LIBD", "name": "Bari Palese", "vipi": true,
"vsop": false }, … ] }`.

## 4. Fuori da questo giro

- **Il ciclo AIRAC entrante** (`?cycle=`): la vista pubblica è una sola, quella in vigore. Chiedere «come sarà» vuol
  dire leggere la release programmata, che è un'anteprima di staff: se servirà, è una decisione a parte.
- **Regole piste, minimi LVP, meteo**: la scheda porta oggi quello che serve per le SID (TA per l'initial climb, piste
  per il filtro) più le frequenze. Il resto si aggiunge nella stessa forma quando qualcuno lo chiede.
