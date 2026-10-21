# API dei settori: gerarchia e coordinamenti fra settori per gli altri programmi — carta (21 ottobre 2026)

**S119**, filone Sito. Il committente: «vorrei aggiungere una API che fornisce i coordinamenti tra settori e le
gerarchie dei settori ad altre app». Stesso stampo dell'API degli aeroporti
([`2026-09-30-api-aeroporti.md`](2026-09-30-api-aeroporti.md)) e stesse regole delle chiavi
([`2026-09-13-chiavi-api.md`](2026-09-13-chiavi-api.md)).

## 1. Le decisioni del committente (21 ottobre 2026)

| Bivio | Deciso |
|---|---|
| Da dove si legge | **La Struttura viva**: quello che leggono la pagina Struttura, Trasferimenti e il banco di prova, aggiornato al primo salvataggio. Non i documenti pubblicati (l'API degli aeroporti invece legge la vista pubblica) |
| Che forma hanno i coordinamenti | **Le righe come sono scritte** negli accordi e, con `?online=`, **anche risolte** sulla gerarchia: chi cede e chi riceve davvero con quegli enti aperti |
| Chi la usa per primo | Un'app della divisione: JSON semplice e documentato |

Le scelte del filone:

- **Un permesso nuovo**, `settori` («Settori (gerarchia e coordinamenti)» nella pagina delle chiavi): una chiave
  degli aeroporti non apre i settori, e viceversa. Chiave **sempre** obbligatoria, come per gli aeroporti.
- **I centri**: solo quelli **italiani e visibili** (`Acc.IsHidden` e `Acc.IsForeign` falsi); i militari ci sono,
  segnati. Un centro nascosto o estero è un 404. I settori esteri compaiono solo dove la struttura li usa: come padre
  di un settore italiano, come bersaglio di un ripiego, come chi riceve un trasferimento.
- **I settori**: quelli **attivi** della proiezione `Sectors`, come per Struttura e coordinamenti (un settore
  nascosto nel catalogo è spento). Il padre è quello dell'albero effettivo (scritto, o derivato dalla scala delle
  posizioni) e **può stare in un altro centro**; un padre spento non si dà.
- **«Online»** con `?online=` vuol dire **solo quelli dati**, come nella vista live: tutti gli altri sono chiusi. Non
  come nel banco di prova, dove chi sta fuori dallo scenario conta aperto. Elenco vuoto (`?online=`) = nessuno online:
  tutto va a UNICOM, ed è una domanda legittima. Al massimo 200 nominativi.
- **Le enumerazioni escono come parole** (`arrival`, `atOrBelow`, `callsign`), non come numeri: un numero cambierebbe
  significato al primo valore aggiunto in mezzo. Il tipo del settore come lo scrive il nominativo (`CTR`, `APP`, `TWR`).
- **Tetti**: 60 richieste al minuto per chiave, 600 in tutto. Una richiesta risolta costruisce la topologia di tutta la
  divisione: si chiede quando cambiano gli online, non a ogni secondo.

## 2. Come è fatto

| Pezzo | Dove |
|---|---|
| Permesso e rotte | `ApiEndpoints.Settori`, `ApiRotte.Acc` e tre voci in `ApiRotte.Tutte` (`Vipi.Domain/Entities/ApiClient.cs`): la pagina delle chiavi le elenca da lì |
| La lettura | `ISettoriPerApi` (`Vipi.Application/Content/SettoriPerApi.cs`), su database `EfSettoriPerApi`: **una porta apposta**, perché i servizi della Struttura che elencano i settori sono degli admin, e allentarli per un programma con la chiave aprirebbe anche le pagine |
| Gerarchia, bande, ripieghi | Le righe dei settori dalla porta (coi padri anche fuori dal centro: `TopologyBuilder.BuildAsync` li scarta, perché la topologia di un centro guarda dentro il centro); bande e ripieghi dalla topologia **globale** (`ITopologyProvider.BuildGlobalAsync`), perché una riga di ripiego può portare il cielo a un altro centro |
| Coordinamenti | `IAgreementService.ListFlowsByAccAsync` (scritti) e `ResolveForAccAsync` (risolti): le due letture non chiedono permessi. **Non** il servizio del banco di prova, che vuole un Editor |
| Le rotte | `Vipi.Hosting/ApiSettori.cs`, con la proiezione pura `ApiSettori.Proiezione` |

## 3. Come si usa

La chiave nell'header `Authorization: Bearer <chiave>` (o `X-Api-Key`), come per le altre API.

| Richiesta | Risposta |
|---|---|
| `GET /vsop/api/v1/accs` | i centri |
| `GET /vsop/api/v1/accs/{acc}/sectors` | i settori del centro con padre, banda e ripiego |
| `GET /vsop/api/v1/accs/{acc}/coordinations` | le righe degli accordi come sono scritte |
| `GET /vsop/api/v1/accs/{acc}/coordinations?online=A,B,C` | le stesse, risolte con solo A, B e C online |

Errori: 401 senza chiave o con una chiave sbagliata, 403 con una chiave che non ha il permesso `settori`, 404 per un
centro sconosciuto, nascosto o estero, 400 con più di 200 nominativi online, 429 oltre i tetti.

### Esempi

`GET /vsop/api/v1/accs`

```json
{ "count": 4, "accs": [ { "code": "LIBB", "name": "Brindisi", "military": false }, … ] }
```

`GET /vsop/api/v1/accs/LIRR/sectors`: un settore per voce, in ordine di copertura (dall'alto) e di nominativo.

```json
{
  "acc": "LIRR",
  "count": 42,
  "sectors": [
    {
      "callsign": "LIRR_NE_CTR", "name": "Roma Nord-Est", "type": "CTR", "kind": "acc",
      "airport": null, "frequency": "125.500", "parent": null, "coverageOrder": 1,
      "band": { "baseFt": null, "topFt": 66000 },
      "fallbacks": [ { "target": "LIRR_SU_CTR", "kind": "callsign", "baseFt": 24500, "topFt": null, "automatic": false } ]
    },
    {
      "callsign": "LIRF_APP", "name": "Roma Avvicinamento", "type": "APP", "kind": "airport",
      "airport": "LIRF", "frequency": "119.200", "parent": "LIRR_NE_CTR", "coverageOrder": 2,
      "band": null, "fallbacks": []
    }
  ]
}
```

- `parent`: il padre nella gerarchia, `null` per una radice. Per l'albero intero si legge il campo di ogni settore.
- `band`: la banda verticale dichiarata nel catalogo, in piedi (`null` dove non è detta, sopra e sotto).
- `fallbacks`: le righe di ripiego **dichiarate**, nell'ordine: stanno davanti al padre quando si cerca chi riceve.
  `baseFt` compreso, `topFt` escluso; `automatic` = riga messa dalla struttura (il ripiego dei militari), non scritta.

`GET /vsop/api/v1/accs/LIRR/coordinations`: le righe come le scrivono gli accordi; un flusso per chi cede, scalo e
tipo, con i suoi punti.

```json
{
  "acc": "LIRR", "resolved": false, "count": 1,
  "flows": [
    {
      "owner": "LIRR_NE_CTR", "kind": "arrival", "airports": [ "LIRF" ],
      "points": [
        {
          "cop": "TIBER",
          "level": { "text": "FL240-", "value": 240, "unit": "fl", "constraint": "atOrBelow", "special": null },
          "receiver": "LIRF_APP", "handler": null, "handlerOnline": null,
          "condition": null, "handoff": null, "speed": null, "variantGroup": null, "variantDepth": 0
        }
      ]
    }
  ]
}
```

Con `?online=LIRR_CTR,LIRF_APP` si aggiungono `resolved: true`, l'elenco `online` (in ordine, come è stato letto),
e per ogni flusso `resolvedOwner` e `ownerOnline` (chi cede davvero: con il settore scritto chiuso, chi ne tiene il
cielo); per ogni punto `handler` (chi riceve davvero, o `UNICOM`) e `handlerOnline`. ⚠️ Lo stesso flusso può uscire
più di una volta, una per chi lo cede davvero.

- `kind`: `arrival`, `departure`, `overflight`, `vfr`, `other`.
- `level.constraint`: `atOrAbove`, `atOrBelow`, `exact`, `special` (allora conta `special`).
- `condition`: la condizione della riga com'è scritta nel documento (piste in uso, area attiva, condizione libera),
  `null` = sempre valida. `handoff`: dove passa il controllo, se è diverso dal punto. Le varianti di una clausola hanno
  lo stesso `variantGroup`.

## 4. Fuori da questo giro

- Le **configurazioni possibili** dei gruppi di settori (Struttura → «Configurazioni possibili») e il **banco di
  prova** con la sua semantica dello scenario: non chiesti.
- Le **frasi** dei coordinamenti come le scrive il documento (italiano e inglese): l'API dà i dati, la frase la
  compone chi legge. `condition` e `level.text` restano nella lingua del sito.
- I **poligoni** dei settori (le aree sulla mappa): non chiesti.
- Una **cache**: ogni richiesta legge il database. Se un programma la chiama spesso, la si aggiunge allora.

## 5. Verifica

- Test: Hosting `ApiSettoriTests` (la proiezione), Infrastructure `SettoriPerApiTests` (quali centri, padri fuori dal
  centro, padri spenti), E2E `ChiaviApiTests` (401, 403 con la chiave degli aeroporti, 200, 404).
- Dal vivo: §6.

## 6. Prova dal vivo (21 ottobre 2026)

Host locale su un database **nuovo**, con dati inventati: il centro LIRR con `LIRR_NE_CTR` e `LIRR_SU_CTR` (radici),
`LIRF_APP` sotto NE e `LIRF_TWR` sotto l'APP, il ripiego NE → SU, un accordo NE ↔ LIRF_APP con una clausola d'arrivo
(TIBER, FL240 o inferiore, a LIRF_APP), un centro nascosto `LIXX` e una chiave di prova col solo permesso `settori`.
Con `curl`:

- `/accs` → 200, il solo LIRR (il nascosto non c'è);
- `/accs/LIRR/sectors` → 200, i quattro settori coi padri e il ripiego di NE;
- `/accs/LIRR/coordinations` → 200, la riga com'è scritta (`receiver` LIRF_APP, `handler` null);
- `/accs/LIRR/coordinations?online=LIRR_SU_CTR` → 200: il flusso di NE, chiuso, lo cede SU (`resolvedOwner`, per il
  ripiego); chi riceve è SU (LIRF_APP chiuso → il padre NE, chiuso → il suo ripiego SU, online);
- `/accs/LIXX/sectors` → 404; senza chiave → 401.

Il database è stato cancellato a fine prova.
