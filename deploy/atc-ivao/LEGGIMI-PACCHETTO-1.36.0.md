# Pacchetto 1.36.0 — solo i file cambiati

> **Timbro:** `1.36.0 · 0493ef5` (21 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Su 1.35.0** (`b38359b`, online dal 21 settembre). **MINOR con UNA migrazione**: il pacchetto si consegna
> da solo via FTP, il database si aggiorna all'avvio. Nessun segreto nuovo, nessuna configurazione da toccare.
> **12 file, tutti in radice** — niente in `wwwroot`, niente in `en/`.
>
> 🔴 **SECONDA CONSEGNA CON UNA MIGRAZIONE DI FILA**, quindi valgono le stesse due cose della 1.35.0:
> 1. **una copia di sicurezza del database PRIMA del carico** — la scarica un Admin dalla Diagnostica;
> 2. **`Vipi.Infrastructure.MySqlMigrations.dll` DEVE essere fra i file caricati.** Senza, la tabella nuova
>    non nasce. ⚠️ E qui il guasto è **più silenzioso** che in 1.35.0: là le pagine delle procedure andavano
>    in errore e si vedeva. Qui non si rompe niente — semplicemente il freno **non frena**, la traduzione
>    continua a ripagare le frasi che non sa rendere, e l'unico modo di accorgersene è la bolletta di Azure.
>    La prova col login è la riga **`Schema` = `0`** (sotto).
>
> ✅ **Niente `wwwroot` in questo pacchetto**: nessun foglio di stile, nessun JavaScript è cambiato. Quindi
> **non** va caricato `Vipi.Host.staticwebassets.endpoints.json` — quello viaggia solo insieme a un file di
> `wwwroot` che cambia, e stavolta non ce n'è nessuno. Un file in meno da rinominare.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

## Che cos'è

Due correzioni, tutt'e due uscite dalla lettura dei file di diagnostica del **21 settembre**. Nessuna pagina
nuova, niente da imparare: quel che cambia è che due cose smettono di succedere.

### 1. L'errore che ogni tanto sbatteva in faccia la pagina «Si è verificato un errore»

La barra in cima agli editor che dice **«che cosa resta da fare su questo documento»** partiva a leggere il
database e nessuno aspettava che avesse finito. Chi chiudeva l'editor mentre quella lettura era ancora in
volo si portava via il database sotto la query: il più delle volte non se ne accorgeva nessuno, ma il **19
settembre alle 19:48** è toccato a un utente vero, che si è visto la pagina d'errore aprendo l'editor APP di
Roma.

Era la causa **più frequente** delle voci nel registro degli errori: **quaranta** su centottantaquattro, e
l'unica ancora viva dopo le correzioni di settembre. Adesso la barra, quando l'editor si chiude, **aspetta**
di aver finito prima di lasciare andare la connessione.

⚠️ **Non c'è niente da guardare**: è un errore che spariva e ricompariva, e la prova che è chiuso è il
prossimo file di diagnostica, non una schermata.

### 2. La traduzione automatica smette di ripagare le frasi che non sa tradurre

Quando il motore di traduzione restituisce una frase **rotta** — le manca un nominativo, un identificatore —
quella frase **non si salva**, giustamente: una frase a cui manca il callsign è peggio della frase non
tradotta. Finora però il giro successivo la rispediva. Ogni quarto d'ora. Per sempre.

Nei cinque giorni di registro scaricati il 21 settembre, **due sole frasi** avevano bruciato così
**170 506 caratteri** — circa **un milione al mese** — in **410 spedizioni tornate rotte tutte e 410**:

| | |
|---|---|
| `«Se presente LIBN_G_APP e assente Lecce APP, Brindisi ACC/MIL coordinerà…»` | 297 giri, italiano → inglese |
| `«• 37th WING A/A TRAINING AREA · …»` | 113 giri, inglese → italiano |

La causa è nota da settembre e **non è un guasto, è una regola**: le parole scritte tutte in maiuscolo
(`WING`, `TRAINING`, `AREA`, `LIBERO`) vengono protette come se fossero sigle, il motore le traduce lo
stesso, e la frase non si ricompone. Riprovarla non può funzionare.

**Adesso c'è un freno**: dopo **tre** tentativi andati male la frase **smette di partire** e il registro lo
scrive **una volta sola**, con il testo per esteso, così si sa quale frase va scritta a mano. Tre tentativi
invece di infiniti vuol dire, su quelle due frasi, circa **1 200 caratteri invece di 170 506**.

Se ne esce da soli in due modi: qualcuno scrive la resa a mano (dal pannello traduzioni), oppure il testo
del documento cambia — e allora la frase nuova riparte con i suoi tre tentativi.

## La migrazione

| Migrazione | Che cosa fa |
|---|---|
| `20260921011623_FrenoTraduzioneQuarantena` | crea la tabella `TranslationQuarantines` (nuova) con il suo indice unico |

✅ **È solo una tabella nuova**, e il corpo è quello generato automaticamente: nessuna tabella rinominata,
nessuna colonna tolta, **nessun dato esistente toccato**. Provata su un database vuoto in avanti: la tabella
nasce, l'indice regge il doppione e ammette il verso opposto.

## I 12 file, e l'ordine

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

**Tutti in radice**, in quest'ordine:

```
Vipi.Domain.pdb
Vipi.Domain.dll
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Infrastructure.MySqlMigrations.pdb
Vipi.Infrastructure.MySqlMigrations.dll    ← 🔴 la migrazione: senza questo la tabella non nasce
Vipi.Infrastructure.pdb
Vipi.Infrastructure.dll
Vipi.Ui.pdb
Vipi.Ui.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. **prima** la copia di sicurezza del database (Diagnostica → copia del database);
2. si caricano **tutti** col nome finto;
3. si rinomina nell'ordine qui sopra: ogni `.pdb` e poi il suo `.dll`, `Vipi.Host.dll` per ultimo;
4. il riavvio: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge.

⚠️ **Restano fuori** `Vipi.Hosting.dll`, `Vipi.AuroraProfiles.dll` e `Vipi.AuroraBridge.Contracts.dll`: il
loro codice non è cambiato e differiscono solo per ricompilazione — controllato che nessuno dei tre nomini un
tipo cambiato né implementi l'interfaccia nuova. Fuori anche `en/Vipi.Ui.resources.dll`: **nessuna frase è
cambiata** in questo pacchetto, e quel file cambia impronta a ogni ricompilazione anche a frasi ferme.
Fuori tutto `wwwroot`, l'indice degli asset, `deps.json`, `runtimeconfig.json` e `appsettings.json`: identici
a 1.35.0, controllato per impronta — **466 file confrontati, 447 identici, 19 diversi**.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore:

- il timbro: **`1.36.0 · 0493ef5`**. Sta nella barra in alto — ⚠️ se la barra è stretta la spia della versione
  è la prima cosa che sparisce, e allora si legge nella riga `Versione` della **Diagnostica**;
- 🔴 `services/vsop/admin/diagnostics`, riga **`Schema`** = **`0`**: vuol dire che la migrazione è entrata. Se
  non è `0`, manca quasi certamente `Vipi.Infrastructure.MySqlMigrations.dll`;
- un **editor di documento** qualsiasi si apre, e la barra gialla «che cosa resta da fare» in cima c'è.

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

---

## Dopo il carico: cose da sapere e da fare

- ▶ **Le due frasi di oggi vogliono comunque una resa a mano.** Il freno smette di pagarle, non le traduce:
  finché nessuno scrive la loro versione, restano nella lingua di partenza.
  - `«• 37th WING A/A TRAINING AREA · …»` è testo **di IVAO**, arriva dall'import: si mette in memoria dal
    codice (seme), non dal pannello;
  - `«Se presente LIBN_G_APP e assente Lecce APP…»` è testo **di un documento**: si scrive dal **pannello
    traduzioni**.
- ⚠️ **Non c'è ancora una pagina** che elenchi le frasi fermate dal freno. Per ora lo dice il registro degli
  avvisi, una riga per frase, la prima volta che la ferma.
- ⚠️ **Il riavvio del processo non è stato riprovato a mano** in questo giro: `vipi-riconnessione.js` — il
  file che fa ricaricare la pagina dopo un riavvio — è **byte per byte identico** a quello online, non è fra i
  19 file diversi. Non c'è niente in questa consegna che possa averlo rotto.
- ⚠️ Da tenere d'occhio, non risolto qui: `ConnectionError` di EF su MariaDB, poche volte e senza nessuna
  richiesta fallita — capita al primo risveglio del processo. Se cresce si guardano il pool
  (`MaximumPoolSize=20`) e `max_connections`.
- ✅ **Quel che il registro dice di buono**: la versione **1.34.3**, rimasta online un giorno intero, ha
  chiuso con **zero** errori su **5990** richieste. Le correzioni di settembre hanno tenuto.

# ⚠️ Il runtime .NET del server è ancora 8.0.28

Questo pacchetto è compilato per **net8**, come tutti i precedenti. Il salto a net10 slitta a **1.37.0**.
