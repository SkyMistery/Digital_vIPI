# vIPI — aggiornamento del 24 agosto 2026 (pacchetto «e»)

**Leggete questo foglio.** Va sopra il pacchetto «d», ed è **soli file**.

> ## ⛔ Il database NON si tocca
>
> Niente `.sql`, niente `DROP DATABASE`, niente import. **Lo schema non cambia**: l'ultima migrazione è del
> 22 agosto ed è già sul server dal pacchetto «d». Verificato: dal pacchetto «d» a oggi non è stata aggiunta
> nessuna migrazione.

> ## ⚠️ Questo pacchetto è più grosso dei precedenti
>
> I due prima portavano un file solo. Questo ne porta **sedici**, perché il pacchetto «d» è stato costruito
> all'**1:04 di stanotte** e nella giornata è stato fuso in `main` anche il lavoro sui **coordinamenti letti
> dal lato di chi riceve**. Non è solo la correzione delle mappe: è tutto quello che è entrato dopo l'1:04.

---

## Che cosa cambia

### 1. I coordinamenti si leggono anche dal lato di chi riceve

Fuso in `main` stamattina. Le tabelle di coordinamento dicevano sempre la frase dal lato di **chi
trasferisce** («Zagreb Radar trasferisce a…»), anche quando il nodo che state leggendo è quello che
**riceve**. Ora la frase ha quattro forme con la stessa testa e cambia il verbo secondo il verso della riga.

> ### 🔴 Questa parte NON si vede finché non si RIPUBBLICANO i documenti
>
> `Sentence` e `LeadSentence` sono **stringhe già scritte dentro la release**: i documenti pubblicati
> continueranno a dire la frase vecchia finché non ne esce una nuova. Misurato fianco a fianco sulla stessa
> vIPI ACC di Brindisi:
>
> | | tabelle | «riceve da» | nodi tagliati |
> |---|---|---|---|
> | viewer pubblico (release vecchia) | 33 | **0** | 0 |
> | editor (derivato live) | 39 | **13** | 4 |
>
> **La differenza è tutta la ripubblicazione.** Caricare i file non basta: dopo il riavvio va ripubblicato
> ogni documento che si vuole aggiornato. Lo fa il committente dall'editor, e si può fare con calma — il
> sito nel frattempo funziona, dice solo le frasi di prima.

### 2. Le mappe non restano più a scacchi

Tre cose, tutte sulle mappe di AoR e minime di vettoramento:

- **Il ritentatore delle tessere aveva un soffitto.** Copriva ~19 secondi: un'interruzione più lunga se lo
  mangiava tutto e le tessere restavano nere **per sempre**, fino a ricaricare la pagina. Misurato con un
  guasto indotto all'80% per 30 secondi: prima **25 tessere su 9 mappe** morte e immutate anche 35 secondi
  dopo la fine del guasto; ora **zero in 10 secondi**. Regge anche 90% per un minuto.
- **Se il fondo non arriva e non arriverà, ora si toglie.** Contro un blocco stabile — un'estensione del
  browser che filtra i domini delle tessere, un DNS che non risolve — nessun ritento serve. Invece del
  riquadro a scacchi restano lo sfondo neutro e i poligoni dei settori: la stessa faccia che la mappa ha già
  mentre carica. Non è definitivo: una sonda leggera riprova, e se le tessere tornano il fondo torna.
- **Le minime: il terreno spariva ingrandendo.** Passato lo zoom 13 il fondo a rilievo se ne andava del
  tutto e restava il grigio, proprio allo zoom in cui serve di più. Ora resta fino a 17.

### 3. La barra in alto ha due voci in meno

Il tasto **«Incarichi»** non c'è più: la pagina si apre premendo **il cerchio con le iniziali**. È sparita
anche la **chip dei ruoli** («IT-AOA1 · IT-T03»). Chiesto da chi la usa, e la barra era a corto di spazio.

ℹ️ Nel menù «☰» la voce «Incarichi» resta: su schermo stretto la barra nasconde il cerchio, e lì il menù è
l'unica strada.

---

## Che cosa caricare

**Sedici file.** Nella cartella `solo-16-file-e/` li trovate **già rinominati con `.nuovo`** e nelle
sottocartelle giuste: caricateli così come sono, senza rinominare niente sul vostro PC.

⚠️ Il motivo per cui ve li diamo già rinominati: finché ogni nome finisce per `.nuovo`, **nessun file vivo
può essere toccato per sbaglio**, nemmeno trascinando l'intera cartella. Il sito continua a girare mentre
caricate.

| File | Dimensione attesa |
|---|---|
| `Vipi.Application.dll` | **1.300.992 byte** |
| `Vipi.AuroraBridge.Contracts.dll` | **15.360 byte** |
| `Vipi.AuroraProfiles.dll` | **12.288 byte** |
| `Vipi.Domain.dll` | **83.456 byte** |
| `Vipi.Host.dll` | **64.000 byte** |
| `Vipi.Host.staticwebassets.endpoints.json` | **55.108 byte** |
| `Vipi.Hosting.dll` | **54.784 byte** |
| `Vipi.Infrastructure.MySqlMigrations.dll` | **580.608 byte** |
| `Vipi.Infrastructure.dll` | **2.929.664 byte** |
| `Vipi.Ui.dll` | **1.865.728 byte** |
| `content/coordination-sentence.json` | **609 byte** |
| `en/Vipi.Ui.resources.dll` | **169.984 byte** |
| `wwwroot/_content/Vipi.Ui/vipi-aor.js` | **30.651 byte** |
| `wwwroot/_content/Vipi.Ui/vipi-boot.js` | **2.100 byte** |
| `wwwroot/_content/Vipi.Ui/vipi-mva.js` | **15.077 byte** |
| `wwwroot/_content/Vipi.Ui/vipi-theme.css` | **263.050 byte** |

Totale **7.443.459 byte**. Tutto il resto del pacchetto è identico a quello che è già sul server.

ℹ️ I `.pdb` non sono nell'elenco: non servono a far girare niente, danno solo i numeri di riga negli errori.
Quelli vecchi rimasti sul server non fanno danno — non corrispondono più alle librerie nuove e vengono
semplicemente ignorati, quindi si perdono i numeri di riga e nient'altro. Se li volete, stanno nel pacchetto
intero `linux-x64-20260824-e/`.

---

## Come si carica

Per esteso in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). La regola non cambia:
**si carica col nome finto e si rinomina, mai sovrascrivere l'applicazione viva.**

1. FileZilla in **binario** (Trasferimento → Tipo di trasferimento → Binario, non «Auto»).
2. Caricate la cartella `solo-16-file-e/` **dentro la radice dell'applicazione**, rispettando le
   sottocartelle (`content/`, `en/`, `wwwroot/_content/Vipi.Ui/`). Arrivano tutti come `.nuovo`: il sito non
   se ne accorge.
3. **Misurate prima di toccare.** F5 nel riquadro remoto e confrontate i byte di ogni `.nuovo` con la tabella
   qui sopra. Se anche uno solo non corrisponde, ricaricate quello e **non proseguite**: fin qui non avete
   toccato niente.
4. **Le rinomine**, due per file, per tutti e sedici:
   `nomefile` → `nomefile.vecchio`, poi `nomefile.nuovo` → `nomefile`.
5. ⚠️ **Riavviate solo quando avete finito tutti e sedici.** Non a metà. `Vipi.Host.staticwebassets.endpoints.json`
   è l'indice che dice all'applicazione quali sono i file di `wwwroot` e con che impronta: se riparte con
   l'indice nuovo e i `.js` vecchi (o viceversa) i due non si corrispondono. Le rinomine sono istantanee e
   l'applicazione viva non se ne accorge, quindi finitele tutte e **poi** riavviate.
6. Riavvio: `restart.txt` vuoto dentro `tmp/` (se `tmp/` non c'è, createla — vedi il foglio).
7. Aprite `https://atc.it.ivao.aero/services/vsop`.

⚠️ **Lasciate i `.vecchio` sul server.** Sono il rollback già pronto: due rinomine al contrario per file, e
si torna esattamente a prima senza ricaricare niente.

### Le tre cose da NON cancellare

| Cosa | Se sparisce |
|---|---|
| `appsettings.Production.json` | contiene la password del database: il sito riparte su uno SQLite vuoto e sembra che i dati siano persi |
| `vipi-keys/` | sono le chiavi che firmano le sessioni: **ogni** login fallisce |
| `tmp/` | serve per il riavvio |

---

## Come si vede che è andata

| Controllo | Cosa deve succedere |
|---|---|
| `diagnostica/avvio-diagnostica.txt` | la prima riga porta **data e ora di adesso**. È l'unica prova che sia ripartita la versione nuova: che il sito risponda non basta |
| `diagnostica/avvio-errore.txt` | **non deve esistere**. Se c'è con una data vecchia, è un residuo: leggetelo e cancellatelo |
| `https://atc.it.ivao.aero/services/vsop` | la pagina si apre con gli ACC: LIRR, LIMM, LIBB |
| La barra in alto | **niente più tasto «Incarichi»** e **niente chip dei ruoli**; il cerchio con le iniziali apre gli incarichi |
| Una vIPI ACC, sezione AoR | la mappa si riempie di tessere entro pochi secondi, senza riquadri neri |
| Il login IVAO | entra, e in alto compare il vostro nome |

⚠️ Le frasi dei coordinamenti **resteranno quelle vecchie** finché non ripubblicate i documenti. Non è un
guasto del caricamento: è §1 qui sopra.

---

Compilato con gli avvisi trattati come errori: **0 avvisi**, **1926 test verdi**.

⚠️ Come i precedenti, questo pacchetto **non è mai stato eseguito su Linux**: è compilato in modo incrociato
da Windows. Il comportamento delle mappe e della barra è stato verificato guidando un browser vero
sull'applicazione in esecuzione su Windows.
