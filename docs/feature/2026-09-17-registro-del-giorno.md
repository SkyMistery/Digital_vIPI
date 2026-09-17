# §A59 — Il registro del giorno: richieste e righe nostre, un file al giorno per sette giorni

> 17 settembre 2026. Carta scritta **prima** del codice (FEATURE-PROCESS). Esito e prove in
> `docs/lavori-aperti.md` §A59.
> Domanda del committente: *«quanto ci conviene salvarci in diagnostica tutto il log, un file per giorno, da tenere
> per 7 giorni? il costo vale il beneficio?»* — risposta data: **non tutto il log**, ma due file mirati e uno script.
> Scelta del committente: *«fai carta e codice per 1, 2, 3, 4»*.

## 0. Che cosa manca oggi

`diagnostica/` racconta i **guasti**: `errori-richieste.txt` (stack), `avvisi-log.txt` (Warning+ col contesto di
dieci richieste, §A52), `avvii.txt` (ere, chi è morto). Non racconta **come va il sistema quando non si rompe**:
quanto ci mette ogni pagina, quanto restano aperti i circuiti, che cosa fanno i lavori in background. Nel primo
`avvisi-log.txt` si leggono per caso la Diagnostica a 5 s e `GET /vsop/live/atc → 200 50045 ms` — solo perché
stavano accanto a un avviso. Senza una misura continua l'ottimizzazione si fa per ipotesi.

⚠️ **Corretto dalla prova dal vivo**: `/vsop/live/atc` è lo **stream SSE** della vista live, e i suoi 50 s sono la
vita della connessione, non una pagina lenta. Nella risposta al committente l'avevo citato come lentezza: sbagliato.
Il file lo mostra da sé (in locale: 3 891 ms per una pagina tenuta aperta 4 s), e lo script tiene a parte le
connessioni lunghe.

## 1. Misura prima di costruire

Da `avvii.txt` di produzione (righe `ARRESTO`, ping compresi): **3 807 richieste il 16-set, 2 162 il 17-set fino
alle 08:00Z**. Senza i ping restano poche migliaia al giorno: a ~180 byte a riga sono **~0,5 MB al giorno**. Le
righe `Vipi.*` a Information le domina il poll ATC, una al minuto: **~1 500 righe, ~200 kB**. Sette giorni dei due
file stanno sotto i **10 MB**. Il tetto di sicurezza per file è **5 MB**: dieci volte il previsto, e ferma un
guasto che scrivesse a raffica.

⚠️ Non si accende `Microsoft.*` a Information: EF scrive il testo di ogni query (misurato il 27-ago: 400 volte il
volume, vedi il commento in `appsettings.json`), e gli avvisi del framework li raccoglie già `RegistroAvvisi`.

## 2. Pre-flight

1. **Modello.** Nessuna entità, nessun database: file in `diagnostica/`, come gli altri. I due file nuovi
   **condividono un solo scrittore** (`RegistroGiornaliero`: nome col giorno, tetto, pulizia dei vecchi) invece di
   duplicare la rotazione. `avvisi-log.txt` resta com'è: il suo taglio (firma al giorno, contesto) è un'altra cosa.
2. **Dispatch.** Niente.
3. **Ingressi + verifica.** Nessuna UI: i file si scaricano via FTP come gli altri, e si leggono con lo script.
   Verifica dal vivo: il sito in locale sulla copia di produzione, qualche pagina e un circuito aperto e chiuso, poi
   lo script sui file nati.
4. **Propagazione.** Additiva. Si aggiornano: memoria `diagnostica-di-produzione`, `lavori-aperti.md`, `HANDOFF.md`.

## 3. I due file

### 3.1 `richieste-AAAA-MM-GG.tsv` — una riga per richiesta

```
# vIPI — richieste servite, una riga per richiesta. Orari UTC. …
ora	pid	versione	metodo	rotta	percorso	esito	ms	autenticato
06:03:17.412	41822	1.30.2 · 76aceb3	GET	/services/vsop/{Acc}	/services/vsop/LIBB	200	68	0
06:03:18.020	41822	1.30.2 · 76aceb3	GET	/_blazor	/_blazor	101	52311	1
```

- Scritta da un middleware in testa alla pipeline, **dopo** che la risposta è finita (`Response.OnCompleted`): non
  ruba tempo alla richiesta. Per `/_blazor` (101) e per lo stream SSE `/vsop/live/atc` `ms` è **la vita della
  connessione**.
- **`rotta`** = il modello dell'endpoint (`RouteEndpoint.RoutePattern.RawText`): raggruppa `LIBB` e `LIRR` sotto la
  stessa pagina senza indovinare con le regex. `-` se nessun endpoint (404, statici).
- **`percorso`** senza query, tagliato a 160 caratteri. 🔴 Mai la query: su `/signin-oidc` porta il `code` OAuth.
- **`pid` e `versione`** su ogni riga: due processi Passenger vivi insieme sono già successi (§A55), e l'era si legge
  senza incrociare `avvii.txt`.
- **`autenticato`** 0/1, mai il VID: basta a separare traffico pubblico da editor.
- Esclusi, con la stessa regola di `RegistroAvvisi.DaNonRicordare`: ping `/vsop/health`, `/_blazor/*` (negotiate,
  initializers, disconnect), `/_framework`, `/_content`, file statici.

### 3.2 `log-AAAA-MM-GG.txt` — le righe nostre

```
06:03:15.884 41822 INF AtcPollingHostedService · Poll IVAO: 3 ATC divisione online, 45 fuori divisione, …
06:04:02.101 41822 WRN TranslationFillHostedService · Traduzione it→en (azure): … ‖ HttpRequestException: 401
```

- Un `ILoggerProvider` con i filtri **del provider**: `Vipi.*` da Information in su, tutto il resto spento.
- Una riga per voce: gli a capo diventano ` ⏎ `, il messaggio si taglia a 1 000 caratteri, un'eccezione porta solo
  `‖ Tipo: messaggio` (lo stack sta in `errori-richieste.txt` o `avvisi-log.txt`). Query tolte con
  `RegistroAvvisi.SenzaQuery`.

## 4. Lo scrittore comune: rotazione, tetto, pulizia

- Nome col **giorno UTC** della riga: a mezzanotte si passa al file nuovo da soli.
- **Sette giorni**: al primo scritto di ogni giorno (per processo) si cancellano i file dello stesso prefisso con
  data più vecchia di sei giorni fa. Il processo rinasce ogni ~50 s, ma la pulizia costa un elenco di cartella.
- **Tetto 5 MB per file**: si decide sulla **lunghezza vera del file** aperto in coda, non su un contatore del
  processo — due processi insieme non raddoppiano il tetto. Al superamento, una riga
  `# troncato: …` (scritta una volta, riconosciuta in coda al file) e il resto del giorno tace.
- Apertura in coda con `FileShare.ReadWrite | Delete`: due processi scrivono entrambi, e l'FTP legge mentre si scrive.
- UTF-8 **con BOM** (`StartupDiagnostics.Codifica`), come ogni file di `diagnostica/`.
- ⚠️ Non solleva mai: un file che non si scrive non ferma una richiesta.
- ⚠️ Una scrittura per riga, senza buffer: Passenger uccide il processo ~10 s dopo l'ultima richiesta, e un buffer
  non svuotato perderebbe proprio le ultime righe. A poche migliaia di righe al giorno l'I/O non si misura.

## 5. Lo script — `tools/registro-del-giorno.py`

Senza script il file non si legge (regola «si CONTA, non si sfoglia»). Stampa:

1. **per giorno**: richieste, autenticate, 4xx, 5xx, connessioni lunghe, processi;
2. **per rotta** (per versione): n, p50, p95, max, tempo totale — ordinato per **tempo totale**, cioè dove va il
   tempo del server;
3. **connessioni lunghe** (`/_blazor` 101 e lo stream `/vsop/live/atc`): quante, mediana e massimo della vita;
4. le **richieste più lente** una per una;
5. **per ora UTC**: quante richieste, per non confrontare la notte col giorno;
6. dal `log-*.txt`: righe per categoria e livello, e i messaggi più frequenti coi numeri normalizzati.

Filtri: `--versione <testo>`, `--dal AAAA-MM-GG`. Cartella predefinita `../diagnostica`, come `errori-per-era.py`.

## 6. Test

- Scrittore: nome del giorno, cambio di giorno, pulizia a sette giorni (e non tocca altri file), tetto con riga di
  troncamento scritta **una volta** anche da due scrittori, intestazione solo su file nuovo.
- Riga di richiesta: rotta dall'endpoint, percorso senza query, esclusi ping/statici/meccanica del circuito.
- Host vero (`WebApplicationFactory`): una richiesta a una pagina lascia la riga con la rotta; il ping no.
- Provider: con i filtri come in produzione passano `Vipi.*` Information, non passano `Microsoft.AspNetCore` né i
  comandi di EF.
- Guasto provato di proposito: togliere il filtro del provider deve far diventare rosso un test.
