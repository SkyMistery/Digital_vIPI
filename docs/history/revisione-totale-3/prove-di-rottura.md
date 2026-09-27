# Prove di rottura consigliate — tema «un utente può rompere il sito?» (rev3, d15)

Tutte SOLO sulla copia locale della 1.46.5 (MariaDB copia di produzione). Mai su atc.it.ivao.aero.
Le identità: anonimo · utente senza livello · Editor · Admin.

## A. Messaggio SignalR oltre il tetto (finding d15-01)  ★ la più importante
Obiettivo: dimostrare che un campo di testo > ~32 KB inviato in UN messaggio del circuito
abbatte il circuito e fa perdere il non salvato, senza errore leggibile.

1. **Editor**, apri l'editor di un documento (es. `/services/vsop/{acc}/airports/editor?icao=LIBD`),
   entra in modifica, aggiungi un blocco di prosa. Nella `RichTextArea` incolla ~40.000 caratteri
   (genera: `python -c "print('LIRF '*8000)"`). Sposta il fuoco fuori dal campo (parte `@onchange`).
   - Atteso se il difetto c'è: barra rossa «Attempting to reconnect», poi ricarica automatica e
     banner «gesto perso»; il blocco NON è stato salvato. In console del browser: errore SignalR
     «message size» / connessione chiusa. Nel log server: nessuna riga d'errore applicativa.
2. **Editor**, stessa pagina, pannello import tabella: incolla nella textarea `data-tab-testo`
   un HTML/testo > 40 KB (una tabella copiata da un PDF grande) e togli il fuoco.
   - Atteso: circuito abbattuto sullo stesso schema.
3. **DivisionStaff**, `/services/coordinates`: incolla in una volta > 40 KB di coordinate o un KML
   grande nel `textarea` (`@oninput`, invia subito).
   - Atteso: circuito abbattuto al momento dell'incolla.
4. **Editor/Admin**, `/services/vsop/admin/transfers`, box «incolla» (`@bind:event="oninput"`):
   incolla > 40 KB.
   - Atteso: idem, al primo oninput.
5. Misura la SOGLIA reale: prova con 30 KB (dovrebbe passare) e 40 KB (dovrebbe cadere) per
   confermare che il limite è ~32 KB (`HubOptions.MaximumReceiveMessageSize` di default, mai
   configurato in `VipiStartup`).
   - Controprova: gli upload da `<InputFile>` (immagini, .cpr, .xlsx) NON cadono anche oltre 32 KB,
     perché usano lo streaming a chunk — serve a dimostrare che il difetto è dei valori inviati
     come argomento dell'evento, non degli upload.

## B. Circuiti anonimi senza tetto + ricerca a scansione piena (finding d15-02)
1. **Anonimo**, apri molte schede/connessioni a `/services/vsop/search` e `/services/vsop/changed`
   (script che apre N WebSocket `/_blazor` in parallelo). Misura la memoria del processo
   (`diagnostica`/MemoriaDelProcesso) al crescere di N.
   - Atteso se il difetto c'è: memoria che sale senza un tetto applicativo; con «una sola istanza»
     e Passenger, avvicinarsi al limite del processo → riavvio → sito giù per tutti.
2. **Anonimo**, su un circuito di `/services/vsop/search` invia una sequenza di ricerche da 2
   caratteri diverse a raffica (aggira il debounce cambiando testo). Ogni ricerca fa
   `EfSearchRepository.SearchAsync`, che carica in memoria TUTTI i `Documents` con gli include
   (Sectors→Acc, Airport→Acc, MilAirport→Acc, Parties→Sector→Acc) e poi le teste delle release.
   - Misura: query e tempo per ricerca sul DB condiviso; moltiplica per il numero di circuiti aperti.
   - Nota: correttezza e crash (T-014, T-042) sono già chiusi; qui si misura solo il COSTO ripetuto.

## C. Controlli di conferma (non-findings, da confermare a posto)
- vAWOS `/services/vawos/api/{icao}`: tetto per IP 10/min + globale 600/min (`RequestRateLimiter`).
  ⚠️ Dietro il proxy l'IP è quello del proxy/Cloudflare (T-020, già aperto): prova con più «client»
  dallo stesso PoP e verifica se collidono sul secchio per-IP.
- SSE `/vsop/live/atc`: da **anonimo** deve dare 401 (T-021); da loggato conta verso il tetto 300.
- Copia DB `/…/database-backup`: da non-Admin → 404; con `Sec-Fetch-Site` esterno → 403.
- Editor lock 30 min senza heartbeat: apri l'editor in due schede/identità, verifica che il secondo
  non entri in modifica (fix 25-set) e che «Fine modifica» liberi.

## D. Input strani (robustezza dei parser d'ingresso)
- `/services/coordinates` e import tabella: incolla con caratteri RTL, zero-width, emoji, surrogati
  spezzati, `\0`; verifica che non rompano render/export e che la copia del DB resti valida.
- Import XLSX: `r="ZZZZZZ1"` e Id duplicati (T-022, verificare che il cap regga ancora).
- Import HTML: `<tr>` senza `</tr>` su 8 MB (T-023, verificare i timeout regex ancora presenti).
