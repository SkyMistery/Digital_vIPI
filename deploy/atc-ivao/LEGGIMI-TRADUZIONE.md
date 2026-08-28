# Accendere la traduzione dei documenti

> Gemello di [LEGGIMI-SEGRETI](LEGGIMI-SEGRETI.md): quello dice **dove** si mettono le chiavi, questo dice
> **quale** chiave serve, che cosa succede quando arriva e come si controlla che stia funzionando.
>
> Carta della funzione: `docs/feature/2026-08-27-documenti-bilingue.md`.
> Regole di che cosa si traduce e che cosa no: `docs/design/regole-lingua.md`.

## Il fatto

Il sito è bilingue dal 28 agosto 2026: c'è il selettore IT/EN in barra, le 2 487 etichette
dell'interfaccia sono tradotte a mano nei `.resx`, la briciola di pane è in inglese in tutte e due le
versioni. Tutto questo funziona **senza chiave e senza configurazione**.

Quello che **non** funziona senza chiave è la **prosa dei documenti**: il testo che scrive lo staff dentro
un vSOP, una vLOA, la sezione di un aeroporto. Quella non sta nei `.resx` — cambia a ogni ciclo AIRAC, e
nessuno la ritraduce a mano ogni volta — e viene da una **memoria di traduzione** che un motore automatico
riempie.

⚠️ **Spento, il sito non sembra spento.** Il selettore c'è lo stesso, le etichette cambiano lo stesso, e
solo aprendo un documento in inglese si scopre che è rimasto in italiano. Non compare nessun errore, in
nessun log, in nessuna pagina: `Translation:Enabled` è **falso per default**, ed è la scelta giusta (un
sito senza chiave non è rotto, semplicemente non traduce) — ma vuol dire che *dimenticare di accenderlo*
è indistinguibile dall'averlo acceso male.

Da qui in poi c'è come si accende, e come si vede che è acceso.

## Quanto costa, prima di scegliere il motore

Misurato sul `vipi.db` reale il 28 agosto 2026:

| | |
|---|---|
| Prosa dei documenti (corpus editoriale) | **499 campi, 23 344 caratteri** |
| Prosa vera dei quindici vSOP militari | **74 401 caratteri** |
| Descrizioni e attivazioni delle aree regolamentate | 230 aree, 35 056 caratteri — ma appena **9 descrizioni e 6 attivazioni distinte** (il dedup le rende quasi gratuite) |
| Direzioni da tradurre | **due**: `it→en` per le vIPI, `en→it` per le vLOA (che nascono in inglese) |

**Semina completa misurata: ~98 000 caratteri.** Dopo, si paga solo il **nuovo**: la memoria è indicizzata
sull'impronta del testo normalizzato, e una frase già tradotta non riparte mai — nemmeno se compare in
dieci documenti diversi, nemmeno se cambia un a-capo o un apostrofo tipografico.

La franchigia una tantum di DeepL è **un milione** di caratteri: dieci volte tutto quello che c'è.
**Il costo non è il problema**; il motivo per cui c'è un tetto è un altro, ed è scritto più sotto.

## I due motori

Si registrano sempre tutti e due e **non serve scegliere**: l'ordine di preferenza è
`Translation:Order`, e vale il **primo che ha una chiave e risponde**. Se il primo esaurisce la franchigia
o non risponde, il secondo subentra da sé, e il registro dice chi ha tradotto davvero.

| | Azure AI Translator | DeepL |
|---|---|---|
| Nome nella configurazione | `azure` | `deepl` |
| Posizione di default | **primario** | riserva |
| Franchigia | si **rinnova ogni mese** | **una tantum**, non si rinnova |
| Chiave | portale Azure, risorsa *Translator* | account DeepL API |
| Tranello | ⚠️ senza `Region` risponde **401**, che somiglia a una chiave sbagliata e manda a rigenerare una chiave che andava benissimo | ⚠️ la chiave del piano gratuito finisce in `:fx` e vuole `api-free.deepl.com`; puntare al server sbagliato dà **403**, che somiglia a una chiave scaduta |

Sul secondo tranello il codice si difende da sé: se `Translation:DeepL:BaseUrl` è vuoto, l'indirizzo viene
**dedotto dalla chiave**. Non riempitelo, a meno che DeepL non cambi idea.

⚠️ **Il 403 di Azure vuol dire due cose, con azioni opposte**, e le distingue solo il codice dentro il
corpo della risposta: `403000` è la **chiave** (va rigenerata), `403001` è la **quota** (va aspettato il
mese, o va usato l'altro motore). Il rapporto del giro riporta il dettaglio così com'è arrivato.

**Ne basta uno.** Con una chiave sola la catena funziona, semplicemente non ha una riserva.

## Che cosa fare, in cinque minuti

**1. Procuratevi una chiave.** Per Azure servono due valori: la chiave e la **regione** della risorsa — la
nostra è **`italynorth`**. Per DeepL basta la chiave.

⚠️ Se dovete crearne una nuova: **West Europe rifiuta le risorse Cognitive Services nuove**
(«region not accepting new customers»). Non è un errore vostro, è quella regione.

**2. Aggiungetela al file `.json` della cartella `segreti`** — lo stesso file dove stanno già la password
del database e le credenziali IVAO, quello dal nome non indovinabile
(vedi [LEGGIMI-SEGRETI](LEGGIMI-SEGRETI.md)). Si aggiunge una sezione, non si tocca il resto:

```json
{
  "ConnectionStrings": { "Vipi": "…" },
  "VipiAuth": { "ClientId": "…", "ClientSecret": "…" },
  "Ivao":     { "ClientId": "…", "ClientSecret": "…" },

  "Translation": {
    "Azure": { "ApiKey": "LA-CHIAVE-VERA", "Region": "italynorth" }
  }
}
```

Con DeepL invece di Azure, la sezione è `"DeepL": { "ApiKey": "…" }`. Con tutti e due, ci stanno tutti e
due.

⚠️ **La chiave non va in `appsettings.Production.json` e non va in `appsettings.json`.** È la stessa regola
già in vigore per il database e per IVAO, e il motivo è scritto per esteso in LEGGIMI-SEGRETI: quei file
sono viaggiati nel pacchetto o scaricabili, e l'assetto dell'hosting è già cambiato due volte.

**3. L'interruttore è già acceso** in `appsettings.Production.json`:

```json
"Translation": { "Enabled": true }
```

Se aggiornate da un pacchetto precedente al 28 agosto 2026 questa riga **non c'è**: va aggiunta, o la
chiave che avete appena messo non serve a niente. È l'errore più facile da fare, perché non dà nessun
segnale.

**4. Riavviate** con `tmp/restart.txt` — e poi **aprite il sito una volta**, che è quello che fa
accorgere Passenger del file (vedi la nota in fondo a LEGGIMI-SEGRETI).

**5. Controllate `diagnostica/avvio-diagnostica.txt`.** Devono esserci tre righe nuove:

```
  Translation:Enabled ........ true
  Translation:Azure:ApiKey ... valorizzato (32 caratteri)  (regione: italynorth)
  Translation:DeepL:ApiKey ... VUOTO
```

Se `Enabled` è acceso e **nessuno** dei due motori è valorizzato, la diagnostica ve lo dice in chiaro, con
un blocco `⚠` sotto le righe della configurazione. Se non lo dice e la riga `Enabled` manca del tutto, non
è arrivato il file di configurazione: state leggendo la copia in cache del browser (Ctrl+F5, o guardate
l'ora nella prima riga).

## Quando comincia a tradurre, e come si vede

Il giro **non parte al salvataggio** di un documento, ed è voluto: il testo italiano *è* il documento, la
traduzione è un servizio, e un disservizio di Azure non deve poter impedire a un controllore di salvare il
proprio lavoro. Il giro è per conto suo:

- parte **due minuti dopo l'avvio** (dopo gli import e la derivazione: tradurre prima vorrebbe dire
  tradurre il corpus di ieri e ripagare domani);
- poi gira **ogni quarto d'ora**. Non ogni giorno come gli altri giri: qui il metro non è il ritmo di una
  sorgente, ma quanto aspetta un lettore prima di vedere in inglese una frase appena scritta;
- se non manca niente, **non tocca la rete affatto**.

Dove guardare, in ordine di comodità:

1. **`/services/vsop/admin/translations`** — il Registro: tutte le frasi della divisione, le mai riviste in
   cima. Se dopo mezz'ora è vuoto, non ha tradotto niente.
2. **Una pagina pubblica in inglese** — apritene una che sapete essere scritta in italiano e cambiate
   lingua in barra.
3. **I log**, alla voce `TranslationFillHostedService`: una riga per giro **solo quando c'è qualcosa da
   dire** (un giro che non trova niente da fare è il caso normale e non si annota, o riempirebbe il
   registro nascondendo quelli che contano).

## Le cose che vanno sapute

⚠️ **Non serve aspettare il giro per pubblicare.** Ogni pubblicazione **fotografa** le traduzioni note in
quell'istante, ed è voluto: una correzione fatta su un documento non deve cambiare l'inglese già
pubblicato di un altro, sotto gli occhi di chi lo sta leggendo. Ma la fotografia **non è un muro**: le
frasi che lo snapshot non porta — quelle scritte pochi minuti prima di premere Pubblica — le riempie la
memoria viva appena il motore le ha tradotte. Chi pubblica non deve guardare l'orologio.

⚠️ **Comparirà l'avviso «traduzione automatica, non revisionata»**, e non è un guasto. Finché una persona
non ha riletto una frase nel Registro o nel pannello Traduzione dell'editor, il documento tradotto porta
un riquadro che lo dice. È la difesa che conta: misurato contro il servizio vero, «riporta sottovento»
torna «bring it back downwind» — grammatica giusta, identificatori intatti, e **non è fraseologia**.
Plausibile e sbagliato è peggio di assente, perché nessuno se ne accorge leggendo. La cura vera è il
**glossario** (`Translation:DeepL:GlossaryId`), che va costruito da un controllore e non da chi scrive il
codice.

**L'avviso si spegne**, quando il lavoro è fatto: si rileggono le frasi nel pannello Traduzione, si
**ripubblica**, e il riquadro sparisce. ⚠️ Il *ripubblicare* è necessario — il timbro «riletta» viaggia
dentro lo snapshot, quindi una revisione fatta dopo l'ultima pubblicazione la vede solo la bozza. ⚠️ E le
release pubblicate **prima del 28 agosto 2026** portano snapshot senza timbro: restano marcate finché non
si ripubblicano, come ogni altra correzione editoriale.

⚠️ **Il tetto di spesa è una stima, e sottostima.** `Translation:DeepL:MaxCaratteriTotali` (450 000 nel
pacchetto) ferma DeepL prima che finisca la franchigia — che per lui è una tantum. Ma il conto dei
caratteri spesi si ricava dalle righe di memoria ancora vive attribuite a quel motore: quando una persona
**corregge** una traduzione, quella riga passa alla persona e i suoi caratteri **escono dal conto**, pur
essendo stati spesi davvero. Più si revisiona, più il tetto si allarga. Su questi volumi non cambia
niente; se un giorno il corpus crescesse di un ordine di grandezza, il tetto va rifatto su un contatore
suo. È annotato in `docs/lavori-aperti.md`.

## In sviluppo

Sulla macchina di sviluppo **è già configurata** dal 27 agosto 2026 (user-secrets di `Vipi.Host`,
`Translation:Enabled` acceso, Azure con la sua regione): queste righe servono a chi parte da zero, o a
ricostruirla dopo una macchina nuova.

Non toccate `appsettings.Development.json`: la chiave si mette nei **user-secrets**, che non finiscono nel
repository.

```
dotnet user-secrets --project src/Vipi.Host set "Translation:Enabled" "true"
dotnet user-secrets --project src/Vipi.Host set "Translation:Azure:ApiKey" "…"
dotnet user-secrets --project src/Vipi.Host set "Translation:Azure:Region" "italynorth"
```

⚠️ **Lasciatela spenta se non vi serve.** Un ambiente di sviluppo che traduce spende franchigia vera per
prove che nessuno leggerà — e quella di DeepL non si rinnova.

## Spegnere

`"Translation": { "Enabled": false }` e riavviare. Non si perde niente: la memoria resta nel database e i
documenti già tradotti continuano a leggersi tradotti. Si ferma solo il riempimento — e il congelamento
delle traduzioni dentro le release nuove.
