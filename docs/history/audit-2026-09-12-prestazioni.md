# Audit prestazioni — 12 settembre 2026, sera

**Stato:** ✅ **ESEGUITO la sera stessa** — le sette voci di codice sono in `main`, **non ancora in
produzione**. Restano le tre del committente (§O2, §O3). Chiesto da lui: *«i server di IVAO non sono delle
schegge ma sono un po' lenti. Puoi analizzare il nostro sito per vedere se si può fare qualcosa per
migliorarne le performance o comunque qualcosa per diminuire il carico sui server, senza alterarne il
funzionamento?»*

> ## Che cosa è cambiato, misurato prima e dopo
>
> ```
> ogni pagina pubblica, da anonimo   1 WebSocket + 1 SSE   ->   ZERO           (Q1)
> la stessa pagina, letta due volte     25 query, 49 ms     ->   0 query, 1,2 ms (Q5)
> query all'avvio                              259          ->   54            (Q7)
> avvio, totale                             2 180 ms        ->   1 259-1 310 ms
> favicon                                   10 939 byte     ->   2 764         (Q8)
> blazor.web.js                    cache-control: no-cache  ->   public, max-age=86400 (Q4)
> ```
>
> E le due cose che NON sono cambiate, perché era il punto: chi è **entrato** ha ancora il suo circuito e il
> suo stream (`ws=1 sse=1`, misurato), e la pagina d'**aeroporto** si tiene il proprio circuito, perché lì
> le isole sono vere.
>
> ⚠️ **Q1 è stato consegnato prima di `passenger_min_instances`, e il committente lo ha deciso sapendolo**:
> vedi il riquadro §BG in fondo a Q1. La prova da fare dopo il caricamento è cinque `/vsop/ping` distanziati
> di 100 s — se uno paga due secondi, il processo muore e §O3 va chiesto subito.

Il seguito dell'[audit del 27 agosto](audit-2026-08-27-prestazioni.md), a sedici giorni e nove consegne di
distanza (1.17.0 → 1.25.0). Quello misurava l'applicazione; **questo misura anche la produzione vera**, ed è
lì che sono venute fuori le cose grosse — due dei risultati di allora, in produzione, **non sono mai entrati
in funzione**.

**Metodo.** Tre strumenti, e nessuna lettura a occhio:

1. **L'applicazione in locale** — `Vipi.Host` compilato in Release, avviato su una **copia** del `vipi.db`
   reale (25 MB), identità di sviluppo abbassata (`DevIdentity__UserId=123456`,
   `StaffPositions__0=XX-ZZ9`). Query contate dal log di EF, ogni pagina misurata **tre volte**.
2. **Un browser vero** (Edge + puppeteer-core) con `Network.enable` del CDP: richieste, byte sul filo,
   **WebSocket** aperte e **stream SSE**, contati uno per uno. In locale **e** su produzione.
3. **La produzione dall'esterno** — `curl` su `https://atc.it.ivao.aero`: intestazioni, `cf-cache-status`,
   TTFB, byte serviti. È la parte che ha ribaltato l'analisi.

---

## Esito in una riga

Il costo **non è** il lavoro del processo: è il **numero di andate all'origine**.

```
/vsop/ping  (non fa NIENTE: no DB, no render)        181 - 199 ms
un asset con cf-cache-status: HIT (dal bordo)         82 - 100 ms
                                                     ----------------
        ogni andata all'origine evitata vale          ~95 ms
```

E oggi **ogni caricamento di pagina fa quattro andate all'origine garantite**: l'HTML (`cf-cache-status:
DYNAMIC`), `blazor.web.js` (`REVALIDATED`, perché esce `no-cache`), la **WebSocket** del circuito e lo
**stream SSE**. Nessuna delle quattro è assorbita dal bordo, e le ultime due **non servono a niente** per un
lettore anonimo.

L'unica pagina dove il processo pesa davvero è la vIPI pubblicata:

```
/services/vsop/libb/vipi    TTFB 409 - 532 ms      35 KB br  /  274 KB grezzi
                            (il pavimento è 180: ~300 ms sono lavoro nostro)
```

---

## Riepilogo

| # | Voce | Gravità | Dove |
|---|---|---|---|
| Q1 | **Ogni visita apre una WebSocket e uno stream SSE** per un gettone che l'anonimo non può vedere cambiare | 🔴 | `SopLayout.razor:171`, `LiveBadge.razor:1` |
| Q2 | La **Cache Rule di Cloudflare non è mai stata messa** — e così com'è il codice non la sfrutterebbe: **un cookie qualunque spegne la cache per sempre** | 🔴 | pannello CF + `CacheDelleLettureAnonime.cs` |
| Q3 | In produzione i file statici li serve **nginx, non l'applicazione**: niente `Cache-Control`, e le varianti `.br` a qualità 11 **non vengono usate** | 🔴 | direttive nginx di Plesk |
| Q4 | `blazor.web.js` esce `no-cache`: **un'andata all'origine garantita per ogni caricamento** | 🟠 | `VipiStartup` |
| Q5 | **Nessuna cache lato server, da nessuna parte**: zero occorrenze in tutto `src/` | 🟠 | `VipiStartup` |
| Q6 | vAWOS: l'API è `no-store` e il timer **gira anche a scheda nascosta** | 🟡 | `VipiModuleExtensions.cs:396`, `vipi-awos.js:83` |
| Q7 | L'avvio è **peggiorato**: 1 286 → **2 180 ms**, 153 → **256 query** — ~185 sono riconciliazioni one-shot che rigirano a ogni avvio | 🟡 | `RunVipiStartupMaintenance` |
| Q8 | Prima visita 113 KB → **181 KB**: font 66 KB, favicon con **quattro** misure, un foglio 3D su tutte le pagine | 🟡 | `App.razor`, `favicon.ico`, `vipi-fonts.css` |

> ⚠️ **Il filo che lega i primi tre: non sono difetti del codice, sono difetti dell'ANELLO fra il codice e
> l'host.** Tutti e tre riguardano lavoro fatto e funzionante che in produzione **non arriva a esprimersi** —
> una regola del pannello mai creata, un `Vary` che il bordo ignora, un middleware che nginx scavalca. Il
> 27 agosto sono stati misurati e chiusi in locale; nessuno li ha **rimisurati da fuori**. È la stessa specie
> del «403 verificato» del 16 agosto che era invecchiato in silenzio: quando dipende dall'hosting,
> **rimisurare, non ricordare**.

---

## Q1 — Ogni pagina apre una WebSocket e uno stream SSE per il gettone in barra

`LiveBadge` dichiara `@rendermode InteractiveServer` e sta in `SopLayout.razor:171`, cioè nel **layout di
tutte le pagine vSOP**. Misurato con Edge, contando le WebSocket e gli stream `text/event-stream`:

```
                        richieste   ws   sse
/services/vsop              25       1    1     wss://…/_blazor  +  /vsop/live/atc
/services/vsop/limm/vipi    60       1    1
/services/vsop/guide        24       1    1
/services                   24       1    1
/services/vawos             22       0    0     <-- altro layout: nessuno dei due
```

**Uguale in produzione** (`https://atc.it.ivao.aero`, da anonimo):

```
/services/vsop        req=25  byte=188 392  ws=1  sse=1
/services/vsop/guide  req=24  byte= 33 689  ws=1  sse=1
```

**Il conto delle isole lo dice da solo.** Contando i marcatori `Blazor:{"type":"server"}` nell'HTML servito:

```
/services                        1 isola     <-- LiveBadge, e nient'altro
/services/vsop                   1
/services/vsop/limm/vipi         1
/services/vsop/guide             1
/services/vsop/lirr/airports     2           <-- + AirportListPanel
…/libb/airports?icao=LIBD        3           <-- + AirportSids, AirportWeather
```

Su un documento, sulla guida, sull'hub e sulla home dei servizi **l'unica isola interattiva è il gettone**.

**E per un anonimo quel gettone non può cambiare.** Il layout gli passa `UserId="_user?.UserId"`, che per chi
non è entrato è `null`; `Resolve()` allora scrive `_callsign = null` e il componente rende il ramo «off», un
link a `/services/vsop/live`. Non esiste evento che possa cambiarlo. In cambio ogni scheda aperta tiene:

- una **WebSocket** con keep-alive ogni **15 s** (`AddHubOptions`),
- uno **stream SSE** con ping ogni **25 s** (`/vsop/live/atc`),

**per sempre, su un processo solo, senza backplane.** È lo stesso difetto di P5 del 27 agosto — un
`@rendermode` su una pagina senza comandi — ricomparso **un piano più in basso**, dove un `grep` di
`@rendermode` sui file di pagina non lo vede: sta nel *layout*.

### La controprova

Tolto `@rendermode` da `LiveBadge.razor`, ricompilato, rimisurato, **e rimesso a posto**:

```
                                  prima            dopo
/services/vsop              req=25 ws=1 sse=1   req=22 ws=0 sse=0
/services/vsop/limm/vipi    req=60 ws=1 sse=1   req=57 ws=0 sse=0
/services/vsop/guide        req=24 ws=1 sse=1   req=21 ws=0 sse=0
…/libb/airports?icao=LIBD   req=30 ws=1 sse=1   req=27 ws=1 sse=0   <-- il circuito RESTA
```

L'ultima riga è quella che conta: la pagina d'aeroporto **si tiene il suo circuito**, perché lì le isole
servono davvero (meteo, SID, elenco). Il difetto è solo il gettone.

### La cura

Il rendermode si mette **nel punto d'uso**, e solo se c'è un VID. Il layout `_user` ce l'ha già
(`SopLayout.razor:270`), quindi non serve una query in più:

```razor
@if (_user?.UserId is { } vid) { <LiveBadge UserId="vid" @rendermode="InteractiveServer" /> }
else                           { <LiveBadgeSpento /> }   @* il ramo "off" di oggi, reso statico *@
```

Resa: **−1 WebSocket, −1 stream SSE, −3 richieste all'origine** per ogni visita anonima — cioè per il grosso
del traffico. Comportamento identico: chi è entrato mantiene il gettone che si aggiorna da sé.

⚠️ **Non basta rendere statico il componente**: il ramo «off» va estratto, o chi è entrato perde
l'aggiornamento. E il parametro deve restare serializzabile (`int?` lo è), perché un rendermode nel punto
d'uso serializza i parametri.

⚠️ **`blazor.web.js` resta e deve restare**: serve la navigazione «enhanced», che è un'altra cosa dal
circuito. Un anonimo che naviga verso una pagina con isole vere apre il circuito **lì**.

### 🔴 L'interazione con §BG, che cambia l'ORDINE

C'è un legame che va detto prima di toccare niente, e non l'ho trovato misurando: sta scritto in `§BG`
(4 settembre, `lavori-aperti.md`). Lì, contando 33 processi corti in produzione, si era misurato che il
processo **non muore per inattività ma viene ucciso da fuori**, vita media **46 s** — con **una** eccezione:

> «E un processo è vissuto **1h 29m** con 609 richieste — l'unico del file *svegliato da
> `/_blazor/negotiate`*, cioè da un browser vero con un circuito aperto: **una connessione lunga tiene su il
> processo e una richiesta corta no**.»

Cioè: **le due connessioni che Q1 propone di togliere sono oggi l'unica cosa che tiene caldo il processo.**
Toltele, ogni prima visita dopo una pausa paga l'avvio — che nel frattempo è cresciuto a **2 180 ms** (Q7).
Q1 resta giusto — tenere caldo un processo con una WebSocket per lettore è pagare un affitto con una cosa che
non c'entra — ma **non va consegnato da solo**:

1. prima `passenger_min_instances ≥ 1` (§O3, bloccato sul committente, **da chiedere dopo il 16 settembre**
   per decisione sua: chi può metterci mano in Ivao.It è via);
2. **poi** Q1, e rimisurando: `/vsop/ping` a freddo dopo una pausa dice subito se il processo resta su.

⚠️ Se Q1 arrivasse **prima**, il sito potrebbe risultare **più lento** all'apparenza pur essendo più leggero:
meno carico costante, più avvii a freddo da 2,2 s. È il caso in cui due misure giuste portano a una decisione
sbagliata se si guardano separate.

**Un indizio, e lo chiamo indizio apposta.** Cinque richieste a `/vsop/ping` distanziate di 100 s, stasera:

```
19:52:06  305 ms   19:53:47  183 ms   19:55:27  187 ms   19:57:07  216 ms   19:58:47  181 ms
```

Nessuna paga un avvio, e nemmeno la prima (305 ms sono la mia connessione, non 2 180 ms di avvio).
**Non è una misura**: il sito è pubblico, non lo si può tenere in silenzio, e chiunque altro — o un circuito
aperto da qualcuno — basta a tenere su il processo. Serve solo a dire che **la verifica di Q1 è facile**: dopo
la modifica, cinque ping distanziati di 100 s; se uno di loro paga due secondi, il processo muore e §O3 non è
stato fatto. Il conto serio è quello di §BG — 33 processi contati dal log — non cinque `curl`.

---

## Q2 — La cache delle letture anonime: la regola non c'è, e il codice non la sfrutterebbe

### Metà uno: il bordo non tiene niente

```
$ curl -D - https://atc.it.ivao.aero/services/vsop
Cache-Control: public, max-age=60
vary: Accept-Encoding, Cookie
cf-cache-status: DYNAMIC          <-- il bordo lo butta
```

L'origine dichiara correttamente quel che P7 aveva deciso il 27 agosto, e **nessuno lo raccoglie**: la Cache
Rule descritta in `LEGGIMI-DEPLOY.md` §«Una regola di Cloudflare che vale la pena aggiungere» non è stata
creata. Sedici giorni e nove consegne dopo, **metà di P7 è ancora inespressa**.

### Metà due: un cookie qualunque spegne la cache, per sempre

E questa metà è **nostra**. `CacheDelleLettureAnonime.Riutilizzabile` dice no se
`context.Request.Cookies.Count > 0`. Misurato, sull'applicazione:

```
/                          -> Set-Cookie: .AspNetCore.Antiforgery.…      (pagina esclusa)
/services/vsop/search      -> Set-Cookie: .AspNetCore.Antiforgery.…      (pagina esclusa)
/services/vsop?culture=en  -> Set-Cookie: .AspNetCore.Culture   expires: 2027   <-- UN ANNO
```

Cioè: **chi passa dalla home, dalla ricerca, dai «cambiati» o dal live** — tutte pagine *escluse*, dove il
cookie antiforgery non viene tolto perché la risposta non è cacheabile — **oppure chi ha scelto la lingua una
volta sola** si porta addosso un cookie, e da quel momento **ogni** sua pagina torna a rispondere
`no-cache, no-store`. La cache anonima, per un lettore vero che gira il sito, **è morta**.

La clausola sul cookie era nata come «rete in più rispetto a quella sull'identità», e il commento lo dice:
serve perché in sviluppo l'identità è finta. Ma «un cookie qualunque» è troppo larga: i due cookie che il
sito emette da solo sono **entrambi innocui per questa decisione**.

- `.AspNetCore.Antiforgery.*` — per un anonimo non protegge nulla, ed è **già** la premessa dichiarata e
  tenuta ferma da `CacheDelleLettureAnonimeTests`: in tutta l'interfaccia non esiste un `<form method="post">`
  né un `<EditForm>`. Il codice lo **toglie già** dalle risposte cacheabili; non riconoscerlo in entrata è
  un'incoerenza fra le due metà della stessa decisione.
- `.AspNetCore.Culture` — seleziona la lingua. La risposta deve **variare** con lui, non essere rifiutata.

Cura: la decisione guarda i cookie **che restano** dopo aver scartato quei due nomi. Non «zero cookie».

### E il bordo non onora `Vary: Cookie`

⚠️ **Questa è la trappola da non sbagliare, ed è scritta al contrario nel foglio di deploy.**
`LEGGIMI-DEPLOY.md` dice «`Vary: Cookie` fa il resto: chi arriva col proprio cookie di sessione non riceve
mai la copia anonima». **Per il browser è vero; per Cloudflare no**: il bordo non onora `Vary` su nient'altro
che `Accept-Encoding`. Attivare la regola così com'è scritta significa che **chi è entrato può ricevere la
copia anonima** — la pagina di un altro, senza i propri tasti. Esattamente il guasto che quella riga doveva
impedire.

La regola va scritta con la condizione **nell'indirizzo**, non nel `Vary`:

| campo | valore |
|---|---|
| quando | `URI Path starts with /services/` **and not** `cookie contains "vipi.auth"` |
| cosa fare | *Eligible for cache*, **Respect origin TTL** |
| chiave di cache | includere il cookie **`.AspNetCore.Culture`** |

`vipi.auth` è il nome scritto in `VipiStandaloneAuthExtensions.cs:63`. La chiave sulla lingua serve perché
l'indirizzo non cambia con la lingua (regole-lingua R5: nessuna rotta localizzata): senza, il bordo
servirebbe la prima delle due copie che gli capita.

**E la verifica, dopo**: una richiesta con `Cookie: vipi.auth=qualunque` **non** deve tornare `HIT`. Se torna
HIT, la regola è sbagliata e va spenta subito.

Resa: la vIPI pubblicata da **409–532 ms a ~85 ms**, e il processo che la rende **una volta al minuto** invece
di una volta per lettore. È il giorno della pubblicazione AIRAC, che è il solo momento in cui questo sito ha
una folla.

---

## Q3 — In produzione i file statici li serve nginx, e l'applicazione non lo sa

Scoperto dalle intestazioni, non dal codice.

```
$ curl -D - https://atc.it.ivao.aero/_content/Vipi.Ui/vipi-theme.css
last-modified: Fri, 11 Sep 2026 19:10:28 GMT
etag: W/"6aa45224-3a162"
cf-cache-status: HIT
        (e NESSUN Cache-Control)
```

Quell'etag non è di ASP.NET (che scrive un tick esadecimale, `"1dd421d898c89bb"`): è la forma
**`mtime-size`** di nginx. E i conti tornano **esatti, tre volte su tre**:

```
                                 etag              size in esadecimale   file nel pacchetto
vipi-theme.css        W/"6aa45224-3a162"               237 922    =        237 922
vipi-theme.css.br       "6aa45224-6faf"                 28 591    =         28 591
favicon.ico           W/"6a946da3-2abb"                 10 939    =         10 939
```

La prova che chiude: **non esiste un percorso nel nostro codice che serva un file statico senza
`Cache-Control`.** `OnPrepareResponse` ne mette uno sempre — `public,max-age=86400`, o `604800` per i
`.woff2`, o `no-store` in sviluppo. L'assenza di quell'intestazione dimostra che **l'applicazione quel file
non l'ha mai visto**. Le rotte dinamiche invece sì: `/vsop/ping` risponde `Cache-Control: no-store`, che è la
nostra riga.

Il documento radice del sito è `public_atc` e dentro c'è `wwwroot/`: nginx trova il file su disco e lo serve,
e solo quel che su disco non c'è scende a Passenger.

**Tre conseguenze, tutte misurate:**

1. **Niente cache dichiarata.** Il browser cade sull'euristica (una frazione dell'età del file), il bordo sul
   proprio default. I font, che nel codice hanno sette giorni, arrivano così:
   `content-length: 8000`, nessun `Cache-Control`, `cf-cache-status: MISS`.
2. **`AssetPrecompressi` non gira mai.** Le varianti a **qualità 11** preparate dal publish (P3 del 27
   agosto) stanno nel pacchetto e non le riceve nessuno. Chi comprime lo fa al volo, a qualità più bassa:

   ```
   vipi-theme.css servito (br)     32 348 byte
   il nostro vipi-theme.css.br     28 591 byte      <-- 3 757 byte buttati, su un file solo
   vipi-theme.css servito (gzip)   35 130 byte
   ```

   Il `.br` è scaricabile **direttamente** e torna 28 591 byte esatti: c'è, è giusto, e non viene usato.
3. **Le intestazioni di sicurezza del middleware non arrivano sugli asset** (la CSP e `permissions-policy`
   compaiono sulle risposte dell'applicazione, non su `vipi-theme.css`). Non è una voce di prestazioni, ma è
   la stessa causa e va sotto gli occhi di chi legge questa carta.

**Cura — direttive nginx aggiuntive del sito in Plesk** (`nginx-vipi.conf` lì **non si carica**: è lo stesso
posto dove stanno già le regole che negano `/diagnostica/` e `appsettings*.json`):

```nginx
location ~* \.(css|js|woff2|ico|svg)$ {
    expires 1y;
    add_header Cache-Control "public, immutable";
}
brotli_static on;    # se il modulo c'è: serve i .br già pronti
gzip_static  on;     # il ripiego, per gli stessi file
```

⚠️ **`immutable` con un anno è sicuro qui, e non lo sarebbe altrove**: l'indirizzo porta già l'impronta
**SHA-256 del contenuto** (`AssetVersion`), quindi un file che cambia cambia indirizzo. I `.woff2` non
passano da `AssetVersion` — sono citati dentro `vipi-fonts.css` — ma hanno nomi content-addressed di Google e
non cambiano mai.

⚠️ **Senza `brotli_static`/`gzip_static` la precompressione resta inutilizzata anche dopo.** Le due cose
vanno chieste insieme, o si sistema la cache e si continua a spedire il 13% di byte in più.

**Se l'host non collabora**, la strada che non dipende da nessuno: far servire gli asset da un prefisso che
**su disco non esiste**, così tornano a Kestrel e alle nostre intestazioni. È un cambio in `AssetVersion` più
un middleware di riscrittura — più lavoro, e da misurare, ma è nostro.

---

## Q4 — `blazor.web.js`: un'andata all'origine garantita per ogni caricamento

```
$ curl -D - https://atc.it.ivao.aero/_framework/blazor.web.js
cache-control: no-cache
cf-cache-status: REVALIDATED        TTFB 135 - 178 ms      53 586 byte br
```

53 KB che **il bordo non può tenere**: li rivalida sempre. È il default di Blazor per i file di framework, e
non lo tocca il nostro `UseStaticFiles` — quel file non sta in `wwwroot`, lo serve il middleware di Blazor
(già annotato il 27 agosto: «non si precomprime, non è un file fisico»). Allora si era guardato il **peso**;
il costo vero è l'**andata**.

Cura: un middleware corto che riscrive `Cache-Control` **su quel percorso solo**, prima di
`MapRazorComponents`. Un giorno basta: quel file cambia con la versione di .NET, non col nostro codice, e il
publish lo rigenera solo quando si aggiorna il framework.

⚠️ **Non `immutable`, e non un anno.** Lì l'indirizzo **non** porta un'impronta: una durata lunga, il giorno
di un aggiornamento di .NET, terrebbe in giro un client che parla un protocollo diverso dal server — e il
sintomo sarebbe «la pagina si vede e non risponde», che è il più difficile da leggere.

Resa: −95 ms e −1 richiesta all'origine per **ogni** caricamento di pagina di **ogni** visitatore.

---

## Q5 — Nessuna cache lato server, in nessun punto del codice

```
$ grep -rn "AddOutputCache|UseOutputCache|AddResponseCaching|IMemoryCache|IDistributedCache|HybridCache" src/
        (nessuna occorrenza)
```

Ogni lettura anonima ri-rende da capo. Quanto costa, misurato tre volte per pagina (numeri identici nelle tre
passate, poller fermi):

| pagina | query | byte br |
|---|---|---|
| `/services`, `/services/vsop`, `/services/vsop/{acc}` | 0 – 2 | ~5 KB |
| `{acc}/airports` | 12 – 13 | 5,6 KB |
| `guide` | 25 | 32,5 KB |
| `libb/vipi` (1 blocco) | 23 | 24,6 KB |
| **`limm/vipi` (2 blocchi)** | **70** | **34,5 KB** |

⚠️ **Le 70 di `limm` NON sono un N+1**, e vale la pena scriverlo perché sembrano esserlo: nel log si vedono
due blocchi identici di 25 query. Sono `AccViewDerivationService.ResolveForViewAsync` che itera i **blocchi**
del documento — 25 query per blocco, e LIMM ne ha due, LIBB uno. È per costruzione.

Su produzione quella pagina costa **~300 ms di processo** sopra il pavimento (409–532 ms contro 181–199 di
`/vsop/ping`). `AddOutputCache`, con le **stesse sette clausole** già scritte in `CacheDelleLettureAnonime` e
TTL **60 s** — identico al `max-age` che già dichiariamo ai lettori, quindi niente di nuovo promesso a
nessuno — porta le ripetizioni a zero query.

⚠️ **Non risparmia il brotli.** `UseResponseCompression` sta **più a monte** nella pipeline, quindi avvolge il
flusso di risposta e la cache vede il corpo **grezzo**: la compressione di 274 KB si ripaga a ogni richiesta
(misurata: la stessa pagina chiesta senza `Accept-Encoding` ha TTFB 346 ms contro 409–532). Quel pezzo lo
risparmia solo il bordo, cioè Q2.

⚠️ E resta il **secondo** motivo per volerlo comunque: è l'unica delle difese che **non dipende da un pannello
di terzi**. Q2 vale di più, ma questa è nostra.

---

## Q6 — vAWOS: `no-store` su un dato vecchio di dieci minuti, e un timer che non dorme

`/services/vawos/api/{icao}` risponde `Cache-Control: no-store` (`VipiModuleExtensions.cs:396`). Ma sotto c'è
un METAR con TTL **dieci minuti** (`Weather:TtlMinutes`): la risposta è **identica per minuti** e il bordo non
può tenerne copia. Un `public, max-age=30` la farebbe assorbire da Cloudflare e resterebbe **più fresca del
dato che porta**.

E `vipi-awos.js:83` è un `setInterval(60000)` che non guarda `document.hidden`: una scheda vAWOS dimenticata
in fondo al browser interroga l'origine **una volta al minuto per sempre**. Il costo per richiesta lo dice il
commento accanto al limitatore: «una lettura dell'elenco documenti più il profilo dello scalo».

Cura: una guardia su `visibilitychange` — salta il giro se la scheda è nascosta, leggi **subito** quando torna
visibile. Non si vede e toglie il carico.

⚠️ La pastiglia «età del dato» si calcola da un timbro assoluto nel payload, quindi una copia tenuta al bordo
per 30 s mostra l'età **giusta**, non un'età congelata. Va verificato a schermo prima di consegnare: è il
genere di cosa che si rompe in silenzio.

---

## Q7 — L'avvio: 1 286 → 2 180 ms, e 185 query su 256 sono lavoro già fatto

Dal cronometro d'avvio (`StartupDiagnostics.CronometroAvvio`), oggi contro il 27 agosto:

```
                                    27 ago      12 set
CreateBuilder .................       46 ms       51 ms
registrazioni dei servizi .....       41 ms      323 ms     <-- x8
builder.Build .................       15 ms       31 ms
migrazione del database .......      537 ms      881 ms     <--
manutenzioni d'avvio ..........      621 ms      828 ms     <--
resto della pipeline ..........       26 ms       66 ms
TOTALE ........................    1 286 ms    2 180 ms
query prima di «Now listening»          153         256
```

Delle 256 query, **~185 sono quattro forme di SELECT sui documenti, ripetute 67, 64, 37 e 17 volte**. Sono le
**quattordici riconciliazioni «one-shot»** di `ReconcileVipiDocuments` che riscandiscono tutti i documenti a
**ogni** avvio: chiavi storiche `custom`, sezioni nascoste, chiavi di catalogo di vLOA e aeroporti, parcheggi
militari, «Regole piste» e LVP, sezioni di catalogo mancanti, QRA, pubblico di default, riga AIRAC seminata,
puntatori a bozza. Sono passate idempotenti che **hanno già finito il loro lavoro** — e crescono col
contenuto: oggi sono 185 query, con il triplo dei documenti saranno il triplo.

Cura: lo schema è **già in casa**. `IImportStateStore` con `GatedImportLoop` fa esattamente questo per gli
import: un timbro per categoria, e chi è fresco non riparte. Un timbro per passata, **legato alla versione
dell'applicazione**, le fa rigirare **una volta dopo ogni deploy** e poi tacere. La rete di sicurezza resta
(un riavvio dopo un aggiornamento le rifà tutte); il costo per riavvio no.

⚠️ **Il timbro va legato alla VERSIONE, non alla data né a un «fatto una volta».** Queste passate esistono per
riparare dati scritti da versioni precedenti: una che non rigirasse dopo un aggiornamento lascerebbe il dato
vecchio con lo schema nuovo, ed è il difetto che escono a prevenire.

⚠️ **E NON vanno spostate in background.** `LinkAirportDocumentsAsync` è, testuale nel commento, «il legame
che tutte le letture del documento d'aeroporto useranno da qui in avanti»: servire pagine prima che finisca
vuol dire servirle su dati non riconciliati. Il commento dice anche perché `LoadVipiRoleOverrides` sta
**prima** di tutto. L'ordine di quelle passate è informazione, non abitudine.

⚠️ **Perché questo conta meno degli altri sei**: si paga solo a freddo. Se
`passenger_min_instances ≥ 1` è impostato — chiesto dal 27 agosto, **ancora da confermare** — il processo
parte una volta e 2 180 ms non li vede nessuno. Senza, li paga **il primo visitatore dopo ogni pausa**.

---

## Q8 — Prima visita: 113 KB (27 agosto) → 181 KB

Misurato in produzione con Edge, `/services/vsop`, cache vuota: **188 392 byte in 25 richieste**. La
composizione, coi byte come li riceve il browser:

| | byte | |
|---|---|---|
| **font** (5 facce scaricate su 24 dichiarate) | **66 445** | 37% |
| **`blazor.web.js`** | **53 452** | 30% |
| **`vipi-theme.css`** (438 KB grezzi → 237 minificati) | **32 348** | 18% — sarebbero 28 591 con Q3 |
| **`favicon.ico`** | **10 883** | 6% |
| HTML | 5 043 | |
| gli altri tredici file | ~16 700 | |

Due regali, senza rischio:

- **La favicon contiene quattro immagini** — 16, 32, 48 e 64 px, BMP a 32 bit non compressi (776, 1 950,
  3 291 e 4 852 byte). Tenere 16 e 32 fa **2 726 byte**: **−8,2 KB**.
- **`vipi-aor3d.css`** (807 byte br) sta nell'`<head>` di **tutte** le pagine e serve al solo visualizzatore
  3D. La condizione sul percorso è già scritta due volte lì accanto, per `vipi-swapper.css` e
  `vipi-awos.css`, col commento che spiega perché sul percorso e non sul DOM: è il `<head>`, la pagina non è
  ancora resa.

E una decisione che **non è tecnica**: tre famiglie (Nunito Sans, **Poppins con sei pesi**, IBM Plex Mono),
24 `@font-face`, cinque facce scaricate su una pagina qualunque. Sono il pezzo più grosso della prima visita.
I font sono già fatti bene — self-hosted, `unicode-range` per sottoinsieme, `font-display: swap` — e non c'è
grasso tecnico da togliere: ridurre i pesi di Poppins **cambia la tipografia del sito**, e la scelta è del
committente, non di chi misura.

---

## Il carico verso i server IVAO: quasi zero, e non è dove sembra

Il poller chiama `/v2/tracker/whazzup` una volta al minuto. Misurato ora:

```
168 803 byte sul filo, 0,13 s          ~243 MB al giorno, 1 440 chiamate
cf-cache-status: HIT    Age: 8    last-modified: …
```

⚠️ **Quell'endpoint è servito dal bordo Cloudflare di IVAO.** Il nostro giro **non tocca i loro server
applicativi**: consuma banda di CDN, che è la risorsa che a loro costa meno. La decompressione automatica è
attiva e va lasciata (senza, sarebbero 705 KB al minuto — è scritto nel commento alla registrazione).

L'unica leva è `Ivao:PollSeconds` (60). Raddoppiarlo dimezza la banda, ma **allunga il ritardo del pallino «in
frequenza» e sgrana le statistiche di traffico**, che campionano da lì. È un cambio di funzionamento, non
un'ottimizzazione: **non l'ho toccato, e non lo consiglierei.**

⚠️ **E il valore in produzione va confermato**: `appsettings.Production.json` non porta una sezione `Ivao`,
quindi vale il 60 del file base — a meno che la cartella «segreti» non ne porti un altro. Da leggere dal
log d'avvio.

**La risposta alla domanda del committente, quindi, è questa**: i «server IVAO lenti» non sono le loro API,
sono l'**hosting nostro**. 181 ms per una richiesta che non fa niente sono rete e proxy, e si aggirano solo
non andandoci.

---

## Cosa ho misurato e SCARTATO

Come il 27 agosto: dove la misura ha ribaltato l'ipotesi, l'intervento non si fa e la misura resta scritta.

- **Abbassare `Ivao:PollSeconds`** — vedi sopra: cambia la freschezza del pallino e la grana delle
  statistiche. È una funzione, non un costo.
- **Ridurre i pesi di Poppins** (~17–27 KB) — è tipografia. Va proposto, non fatto.
- **Spezzare `vipi-theme.css` fra pubblico e amministrazione** — 28,6 KB br su ogni prima visita, e una parte
  serve solo a editor e admin. Ma il foglio è **cache-bustato per contenuto** e sta al bordo: il costo è una
  volta per visitatore, non per pagina. Contro: dividere un foglio di 1 173 classi con `:where(.vipi-root)` e
  gli scaglioni della topbar è il genere di lavoro che rompe stili in punti che nessuno riapre. **Non ora.**
- **Togliere CSS morto** — `classi-morte.py` dice **16 classi su 1 173** senza citazioni, e quasi tutte sono
  falsi positivi noti (`.xt-ind1..4` nascono da `$"xt-ind{n}"`, `.components-reconnect-*` le mette Blazor).
  **Non c'è niente da recuperare**: il foglio è denso, non gonfio.
- **ReadyToRun** — già scartato su misura il 27 agosto. Il cronometro di oggi lo **riconferma e rafforza**:
  1 709 ms su 2 180 sono `migrazione` + `manutenzioni`, cioè database. La compilazione non è il problema, ed
  è cresciuta la parte che ReadyToRun non tocca.
- **Spostare le manutenzioni d'avvio in background** — 828 ms di guadagno secco, respinto sulla correttezza:
  vedi Q7.
- **GET condizionale sul whazzup** (`If-Modified-Since`) — l'endpoint manda `last-modified`, quindi
  tecnicamente si potrebbe. Ma il whazzup si rinnova ogni ~15 s e noi chiediamo ogni 60: un 304 sarebbe
  l'eccezione. Complessità in più per un risparmio che non capita.

---

## ⚠️ Il metodo — tre trappole, e una vale più dell'audit

### 1. La prima misura delle query era rumore, e diceva numeri spettacolari

La prima passata ha dato **315 query** su `lirr/vloa`, **234** su `limm/vloa`, **181** su `libb/airports`.
Con i poller fermi, le stesse pagine: **9, 9, 13**. La differenza erano i **giri di fondo** — il poller ATC, il
registratore di traffico, gli import — che scrivono nello stesso log da cui contavo, mentre contavo.

Il conto si fa con l'applicazione **messa a tacere** (`Ivao__PollSeconds=86400`, `Ivao__BaseUrl` su una porta
morta), e si verifica che il rumore sia **zero** prima di fidarsi: sei secondi senza richieste → 0 query. Poi
tre passate per pagina, e i numeri devono **ripetersi identici**. È il corollario operativo di «una misura
singola non è una misura» del 27 agosto: lì la lezione era *ripeti*, qui è *guarda anche chi altro scrive*.

### 2. La controprova che distingue

Q1 non è stato dedotto: è stato **provato togliendo la causa**. Rendermode via, ricompilato, rimisurato —
`ws=0 sse=0` sulle pagine-documento e `ws=1` su quella d'aeroporto, cioè **il risultato che una deduzione
sbagliata non avrebbe prodotto**. Poi rimesso a posto, e `git status` vuoto.

Senza quella passata, «è LiveBadge» era un'ipotesi ragionevole basata sul conteggio delle isole — e sarebbe
stata la seconda pagina interattiva a smentirla.

### 3. Tre voci su otto si vedono SOLO da fuori

Q2 (`cf-cache-status: DYNAMIC`), Q3 (l'etag di nginx), Q4 (`REVALIDATED`) **non sono leggibili nel codice**.
Il codice, su tutte e tre, ha ragione: scrive gli header giusti, prepara le varianti compresse, dichiara
`max-age=60`. Sono le **tre** voci più gravi di questo audit, e l'audit del 27 agosto — fatto bene, misurato
in Release su un database vero — non poteva vederle, perché ha misurato **la nostra macchina**.

> **La regola che ne esce, e che vale oltre le prestazioni: un intervento che dipende dall'host non è finito
> quando il codice è giusto. È finito quando è misurato dall'esterno, sull'indirizzo vero.** Per Q1–Q8 la
> misura da fuori costa tre `curl` e vale sedici giorni.

---

## Cosa resta, e di chi è

| Voce | Stato | Lavoro aperto |
|---|---|---|
| **`passenger_min_instances ≥ 1`** e **`proxy_read_timeout ≥ 100s`** — chiesti il 27 agosto, **mai confermati** | 🔴 committente | **§O3** |
| **Q2b** **Cache Rule su Cloudflare** con la clausola `vipi.auth` e la chiave sulla lingua | 🔴 committente | **§O2** |
| **Q3** **direttive nginx in Plesk**: `immutable` + `brotli_static`/`gzip_static` | 🔴 committente | **§O3** |
| **Q8-bis** pesi di Poppins (tipografia, non prestazioni) | 🔴 committente | §CZ |
| **Q1** rendermode di `LiveBadge` solo ai loggati | ✅ fatto | §CZ |
| **Q2a** cookie innocui in `Riutilizzabile` (+ test) | ✅ fatto | §CZ |
| **Q4** `Cache-Control` su `/_framework/blazor.web.js` | ✅ fatto | §CZ |
| **Q5** `AddOutputCache` sulle otto clausole | ✅ fatto | §CZ |
| **Q6** vAWOS: l'intestazione che mentiva + guardia su `document.hidden` | ✅ fatto | §CZ |
| **Q7** timbro di versione sulle riconciliazioni d'avvio | ✅ fatto | §CZ |
| **Q8** favicon a due misure, `vipi-aor3d.css` col suo modulo | ✅ fatto | §CZ |
| ✅ Corretto `LEGGIMI-DEPLOY.md`, che diceva che `Vary: Cookie` protegge il bordo, e aggiunte le direttive nginx | ✅ fatto | — |
| `ListOrphansAsync`: ~150 query per otto orfani | 🟢 invariato | §O1 |

**Ordine consigliato:**

1. **§O3** (Passenger) — è il prerequisito di Q1, non una voce parallela.
2. **Q2a** e **Q2b** (§O2) **insieme**, il codice prima della regola.
3. **Q1**, e rimisurando `/vsop/ping` a freddo dopo una pausa.
4. **Q3** (§O3, seconda metà) e **Q4**.
5. **Q5**.
6. Q6–Q8 in coda a una consegna qualunque.

⚠️ **Q2b non si attiva prima di Q2a**, e non perché non funzionerebbe: funzionerebbe **per pochi**, e una
regola che sembra messa è più difficile da sospettare di una che manca.

⚠️ **Q1 non si consegna prima di §O3**: vedi il riquadro in fondo a Q1 — le due connessioni che toglie sono
oggi l'unica cosa che tiene caldo il processo.

---

## L'esecuzione, e le due cose che ha insegnato

### Un difetto in più, trovato SCRIVENDO la cura di Q6

L'endpoint del vAWOS scriveva `Cache-Control: no-store`. Non era vero, e la produzione lo diceva:

```
GET /services/vawos/api/LIBA  ->  Cache-Control: public, max-age=60
```

`/services/vawos/api/{icao}` comincia per `/services` e non porta nessuno dei segmenti esclusi, quindi
ricade sotto `CacheDelleLettureAnonime` — che scrive in `OnStarting`, cioè **dopo** l'endpoint, e vince.
Quella riga non faceva niente se non raccontare una cosa falsa a chi la leggeva, ed è **l'unico** endpoint
del sito in quella condizione (gli altri stanno sotto `/vsop/`, fuori dal raggio).

Il comportamento vero è anche quello giusto — sotto c'è un METAR con dieci minuti di TTL — quindi la cura è
stata **togliere la riga e scrivere perché**, più un test che pinna le due facce (anonimo sì, con
`vipi.auth` no). ⚠️ La lezione generale: **un'intestazione scritta in un endpoint non è l'intestazione che
esce**, se un middleware più a monte la riscrive in `OnStarting`. Vale per chiunque ne aggiunga una domani.

### Q7: che cosa è stato messo sotto gate, e che cosa no

Il timbro copre **le dodici passate sui documenti**. Restano fuori, di proposito,
`LinkAirportDocumentsAsync` e `ReconcileAirportCategoriesAsync`: la seconda tiene un **invariante** con la
presenza militare, e quella cambia a runtime quando gira l'import dell'anagrafica. Una riconciliazione che
mantiene un invariante non è one-shot, e saltarla dopo un riavvio lascerebbe il dato storto fino alla
consegna dopo — in silenzio. Costano una manciata di query: è il prezzo giusto per non doverci ripensare.

Tre regole rendono il gate sicuro, e vanno lette insieme: la chiave **porta la versione** (dopo ogni
consegna rigirano una volta); si timbra **solo un giro che non ha cambiato niente** (il timbro certifica un
fatto osservato, non una previsione); **senza timbro di build il gate non esiste**, quindi in sviluppo
girano sempre. Misurato: primo avvio 259 query e il timbro; secondo avvio **54**, con la riga di log che
dice perché.

## Verifica di questo audit

**Dell'analisi:**

- Numeri di query: tre passate identiche per pagina, con i giri di fondo fermi e il rumore verificato a zero.
- Numeri di byte, intestazioni e `cf-cache-status`: presi su `https://atc.it.ivao.aero`, non in locale.
- WebSocket e stream SSE: contati dal CDP di un browser vero, in locale **e** in produzione.
- I tre etag di nginx: confrontati byte per byte coi file dentro `artifacts/publish/linux-x64-20260912c`.

**Dell'esecuzione:**

- Build **Release della soluzione intera**, `--no-incremental`: **0 errori, 0 avvisi**.
- Suite: **15 progetti con esito**, 11 778 risultati, **zero falliti**.
- **Q1**, in un browser vero e in due identità: da **anonimo** `ws=0 sse=0` su hub, guida, documenti; da
  **entrato** `ws=1 sse=1`; la pagina d'aeroporto tiene il proprio circuito in tutti e due i casi. Il
  conteggio delle isole scende di uno esatto su ogni pagina (1→0, 2→1, 3→2).
- **Q5** su un indirizzo mai chiesto in quel processo: 1º giro **25 query / 48,9 ms**, 2º e 3º **0 query /
  1,2 ms**, stessi 262 399 byte. E le tre esclusioni provate una per una: col cookie `vipi.auth` **23 query
  ogni volta** e `no-cache, no-store`; con `?as=draft` **25 ogni volta**; **col cookie della lingua una
  copia sua** — «Stampa» contro «Print», impronte diverse, e la seconda lettura inglese a zero query.
- **Q6**: `awos-verifica.js` **13 su 15** (i due rossi sono «non minificato», che dipende dal girare da
  sorgente e non da un pacchetto), più una prova scritta apposta: a scheda **nascosta** zero chiamate in
  70 secondi — un giro intero del timer — e **una** entro 2,5 s da quando torna visibile.
- **Q7**: primo avvio 259 query e il timbro scritto; **secondo avvio 54**, ripetuto identico due volte. Fase
  «manutenzioni d'avvio» 828 → **455 / 474 ms**, totale 2 180 → **1 259 / 1 310 ms**.
- **Q8**: favicon 10 939 → **2 764 byte**, due immagini PNG valide (16 e 32 px), servita 200. Il foglio del
  3D: **zero** `<link>` sulla guida, **uno** sulla pagina intera del 3D (canvas alto 418 px), e sulla vIPI
  ACC **zero prima** del tasto «3D view» e **uno dopo**, con il canvas a 538 px.
- `lazy-verifica.js`: **tutto a posto** — 66 tessere, 187 poligoni, 164 chip, e il modulo che arriva dopo la
  navigazione «enhanced». `pacchetto-verifica.js`: **un solo rosso**, lo stesso «non minificato».
- ⚠️ **Quel che NON è stato provato**: il comportamento a freddo dopo Q1 (vuole la produzione), e la
  minificazione (vuole un publish). Tutti e due si guardano al prossimo pacchetto.
