# API bridge Aurora — `/vsop/api/v1/transfers/resolve` 🟢

Contratto dell'unico endpoint JSON pubblico del sito. Serve il tool desktop che riempie l'etichetta quota di
Aurora col livello di trasferimento al prossimo ente. Design e razionale: [`../design/piano-aurora-bridge.md`](../design/piano-aurora-bridge.md).

**Fonte di verità del contratto:** i POCO di `src/Vipi.AuroraBridge.Contracts/TransferResolveContract.cs`,
referenziati sia dall'host sia dal tool. Questo file li descrive, non li duplica.

## Caratteristiche

| | |
|---|---|
| Metodo | `POST` |
| Percorso | `/vsop/api/v1/transfers/resolve` |
| **Attivazione** | **`AuroraBridge:Enabled`, default `false`** — spento la rotta non si registra affatto |
| Autenticazione | **nessuna** — espone dati già pubblici nei documenti |
| Scrittura | nessuna: il server non tocca Aurora, lo fa il tool su azione dell'utente |
| Tetto complessivo | `AuroraBridge:RequestsPerMinuteTotal` (default 600/min, tutti insieme) → `429` |
| Tetto per IP | `AuroraBridge:RequestsPerMinutePerIp` (default 120/min) → `429` con `Retry-After: 60` |
| Corpo massimo | `AuroraBridge:MaxRequestBytes` (default 64 KB) |
| Versione | `v1`; ogni cambio incompatibile diventa `v2` e lascia vivo il tool vecchio |

## Configurazione (sezione `AuroraBridge`)

```jsonc
{
  "AuroraBridge": {
    "Enabled": false,                 // l'endpoint si monta SOLO se true. Default: spento
    "LabelConvention": "Number",      // "Number" (250) | "FlPrefixed" (FL250). Valore ignoto → Number
    "MaxCandidates": 8,
    "RequestsPerMinuteTotal": 600,    // tetto di tutti i chiamanti insieme
    "RequestsPerMinutePerIp": 120,
    "MaxTrackedClients": 10000,       // quanti IP distinti il limitatore tiene in memoria
    "MaxRequestBytes": 65536,
    "TopologyCacheSeconds": 30        // riuso della topologia globale fra richieste. 0 = nessuna cache
  }
}
```

Senza sezione valgono i default, **endpoint spento compreso**. La convenzione è una scelta di **leggibilità
del tag**: Aurora accetta testo libero nell'etichetta quota (piano §11.2).

### Perché nasce spento

È l'unica superficie pubblica, anonima e scrivibile-da-fuori del sito, su un dominio servito a una divisione.
Accenderla dev'essere una decisione di chi distribuisce il tool desktop, non la conseguenza di aver fuso un
ramo. `MaxRequestBytes` e `TopologyCacheSeconds` si leggono **all'avvio**: cambiarli richiede un riavvio.

### Perché due tetti e non uno

Dietro il reverse proxy l'indirizzo del chiamante arriva da `X-Forwarded-For`, e `UseForwardedHeaders` è
configurato **senza proxy noti** perché l'IP del proxy non è fisso. Quindi la chiave del tetto per IP **la
sceglie il chiamante**: protegge dal caso vero — un tool in polling stretto, un client difettoso — non da chi
la ruota apposta. Quello che regge davvero è il tetto **complessivo**, che non ha chiave.

Per lo stesso motivo il limitatore tiene al massimo `MaxTrackedClients` indirizzi: senza tetto, ruotare
l'header farebbe crescere il dizionario senza limite — un esaurimento di memoria a colpi di richieste da
200 byte. Raggiunto il tetto si spazzano le finestre scadute; se non basta, un indirizzo **mai visto** viene
rifiutato con `429` invece che tracciato.

## Richiesta

```jsonc
{
  "ownerCallsign": "LIBB_ES_CTR",   // OBBLIGATORIO — da #CONN, o campo 12 di #TRPOS
  "departure": "LIBD",
  "arrival": "LIRF",
  "cruiseLevel": 350,                // FL, già normalizzato da "F330" → 330
  "route": "PISIP UM984 ASPIR",      // serve per le AEROVIE, che routeFixes non porta
  "routeFixes": [                    // da #TRPATHL: fonte preferita per il CoP
    { "fix": "ASPIR", "eto": "0925" }
  ],
  "currentAltitudeFt": 24000,
  "verticalSpeedFpm": 1800,
  "onGround": false,
  "nextStation": "LIRR_US_CTR",      // campo 13 di #TRPOS, se già impostato
  "runwaysInUse": {                  // da #CTRLRWY
    "LIRF": { "departure": ["25"], "arrival": ["16L", "16R"] }
  }
}
```

**Nessun dato personale**: il callsign dell'aeromobile non serve alla risoluzione e **non va inviato**.

Solo `ownerCallsign` è obbligatorio (altrimenti `400`). Tutto il resto è facoltativo: meno contesto si manda,
meno il server può discriminare — e i candidati arrivano con punteggi più bassi e più avvisi.

## Risposta

```jsonc
{
  "asOf": "2026-08-03T08:44:27Z",
  "onlineAsOf": "2026-08-03T08:44:18Z",   // freschezza della cache ATC: mostrala all'utente
  "resolvedOwner": "LIBB_ES_CTR",         // settore riconosciuto (accetta anche "LIBB_ES")
  "accCode": "LIBB",
  "candidates": [{
    "flowId": 3, "pointId": 3,
    "flowKind": "Arrival", "airportIcao": "LIRF",
    "cop": "ASPIR", "copEto": "0925",
    "level": {
      "value": 210, "unit": "Fl", "constraint": "AtOrBelow", "special": null,
      "parity": "Odd", "verticalState": "Descending", "text": "FL210- ↓ (dispari)"
    },
    "nextSectorCallsign": "LIRR_US_CTR",
    "resolvedHandler": "UNICOM", "handlerOnline": false,
    "condition": { "display": null, "match": "none" },
    "auroraValue": "210", "writable": true,
    "score": 0.806,
    "reasons": ["arrivo a LIRF", "CoP ASPIR in rotta (ETO 0925)", "livello di crociera dispari"]
  }],
  "warnings": []
}
```

### Campi che il client deve rispettare

- **`auroraValue` è una stringa**, non un numero: è ciò che va passato tale e quale a `#LBALT;CS;<valore>`.
  Non contiene mai `;` (separatore del protocollo). Se `writable` è `false`, `auroraValue` è `null` e il
  pulsante di scrittura va disabilitato: il livello esiste ma non è esprimibile come etichetta.
- **`score`** (0..1) ordina i candidati, **non** è una probabilità. Il primo è il migliore, la scelta resta umana.
- **`reasons`** va mostrato accanto al livello: è il motivo per cui quel candidato sta lì, ed è ciò che permette
  al controllore di smentire la proposta.
- **`condition.match`**: `matched` | `unmatched` | `unknown` | `none`. `unknown` significa *non verificabile in
  automatico* (area attiva, condizione personalizzata, piste non note ad Aurora) → va segnalato a video,
  non nascosto.
- **`resolvedHandler`** è chi prende davvero il traffico ORA risalendo la gerarchia; `UNICOM` se nessuno è online.
  `nextSectorCallsign` resta l'ente nominale della vIPI.

### Avvisi (`warnings`)

Stringhe già in italiano, da mostrare così come sono. Casi tipici: callsign non riconosciuto, nessun flusso per
la postazione, condizioni non verificabili, nessun candidato scrivibile.

## Codici di stato

| Codice | Quando |
|---|---|
| `200` | sempre, anche con `candidates` vuoto (il perché è in `warnings`) |
| `400` | `ownerCallsign` mancante |
| `429` | superato il tetto (complessivo o per IP); riprova dopo `Retry-After` |
| `405` | l'endpoint **non è attivo** su quel sito (`AuroraBridge:Enabled=false`). Non è `404` perché il catch-all delle pagine risponde al `GET` di qualunque percorso: a mancare è il verbo, non il percorso |

## Esempio

```bash
curl -s -X POST http://localhost:5034/vsop/api/v1/transfers/resolve \
  -H "Content-Type: application/json" \
  -d '{"ownerCallsign":"LIBB_ES_CTR","arrival":"LIRF","cruiseLevel":350,
       "routeFixes":[{"fix":"ASPIR","eto":"0925"}]}'
```

## Cosa NON fa

- Non scrive in Aurora e non conosce Aurora: parla di trasferimenti, non di protocollo.
- Non assume traffico, non sceglie al posto del controllore, non filtra in silenzio i candidati deboli.
- Non espone i documenti: per quelli ci sono le pagine `/services/vsop`.
