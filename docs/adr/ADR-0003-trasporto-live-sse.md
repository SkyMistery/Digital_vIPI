# ADR-0003 — Trasporto live: Server-Sent Events

**Data:** 2026-06-23
**Stato:** Accettata
**Contesto:** fase F3 (polling IVAO). Estende ADR-0001 D6.

## Contesto
Il `AtcPollingHostedService` interroga le API IVAO ogni ~60 s e aggiorna una cache in memoria
condivisa (`OnlineAtcCache`, singleton). Le viste live (vista Ridotta: collasso AoR, "primo online"
dei trasferimenti, lista "online nel mio dominio") devono riflettere i cambi senza che l'utente
ricarichi la pagina. Serve un meccanismo di push dal server al browser.

## Opzioni considerate
1. **Server-Sent Events (SSE)** — endpoint `text/event-stream` che emette un evento a ogni cambio
   cache + heartbeat. Il browser (EventSource nativo) riconnette da solo.
2. **Solo circuito Blazor in-process** — i componenti `InteractiveServer` si sottoscrivono
   direttamente all'evento `OnlineAtcCache.Changed` e chiamano `StateHasChanged`.
3. **Polling lato client** — la pagina ri-interroga a intervalli.

## Decisione
Si adotta **SSE** come trasporto canonico (opzione 1).

- Endpoint `GET /sop/live/atc` nell'host (`Program.cs`): si sottoscrive a `OnlineAtcCache.Changed`,
  emette `data: {asOf,count}` a ogni cambio, heartbeat `: ping` ogni 25 s per tenere viva la connessione.
  Il **buffering della response è disabilitato** (`IHttpResponseBodyFeature.DisableBuffering()`) così gli
  eventi raggiungono il browser immediatamente anche dietro reverse-proxy.
- Lato browser `vipi-live.js` (`EventSource`) → su messaggio invoca via JS interop il metodo
  `[JSInvokable] OnLiveUpdate` del componente, che ricarica la vista. Payload volutamente minimale
  (solo `asOf`/`count`): la fonte di verità resta server-side (la pagina ri-legge `IOnlineAtcProvider`).

## Conseguenze
- **Pro:** un solo flusso di dati (cache → SSE) indipendente dal numero di client (RNF-1/RNF-4);
  portabile anche per host non-Blazor quando la RCL sarà embeddata; nessun payload sensibile.
- **Contro / mitigazioni:** in scenari A/B (ADR-0002 D4 "nessun endpoint nuovo") l'endpoint vive
  comunque dentro l'host che monta la RCL ed è **read-only** (conteggio + timestamp), quindi
  accettabile; eventuale auth a livello di routing dell'host.
- L'opzione 2 (evento in-process) resta valida come fallback locale ed è già disponibile
  (`OnlineAtcCache.Changed`), ma SSE è il contratto verso il browser.
