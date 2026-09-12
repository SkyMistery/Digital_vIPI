# Pacchetto 1.25.0 — solo i file cambiati

> **Timbro:** `1.25.0 · 909e3f03` (12 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.24.1.** **25 file**. 🔴 **C'È UNA MIGRAZIONE** — additiva, si applica da sola all'avvio:
> il database **non** va sostituito, ma un file di questo pacchetto è quello che la porta e **non si può
> dimenticare** (vedi qui sotto).
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🔴 IL FILE DA NON DIMENTICARE
>
> **`Vipi.Infrastructure.MySqlMigrations.dll`**. È quello che porta la tabella nuova
> (`AirportLvpMinima`). Senza di lui il codice nuovo parte, cerca una tabella che il database non ha, e le
> pagine dei documenti d'aeroporto **cadono**. La prova, dopo il riavvio, è la riga **`Schema`** in
> `admin/diagnostics`: dev'essere **`0`**.
>
> ## 🔴 QUESTA VOLTA CI SONO FILE IN DUE SOTTOCARTELLE
>
> - **`en/Vipi.Ui.resources.dll`** va dentro la cartella **`en/`**: è il file delle frasi inglesi, e questo
>   aggiornamento ne porta 21 nuove. Messo in radice non serve a niente e l'inglese resta quello di prima —
>   senza nessun errore da nessuna parte.
> - **nove file** vanno in **`wwwroot/_content/Vipi.Ui/`**: `vipi-awos.css` e `vipi-awos.js` (**nuovi**),
>   `vipi-boot.js` (cambiato), ciascuno coi suoi `.br` e `.gz`.
>
> ⚠️ **`Vipi.Host.staticwebassets.endpoints.json` viaggia INSIEME a quei nove**, e sta in **radice**. È
> l'indice che dice al sito con che nome chiedere ogni file di `wwwroot`: caricarne uno solo dei due fa
> chiedere al browser nomi che non esistono, e la pagina esce senza stile.
>
> ## L'ORDINE
>
> 1. prima si caricano **tutti e 25** i file col nome finto, ciascuno nella sua cartella;
> 2. poi le rinomine, **una di seguito all'altra e senza pause**, in questo ordine:
>    1. i nove di `wwwroot/_content/Vipi.Ui/` e `Vipi.Host.staticwebassets.endpoints.json`;
>    2. `en/Vipi.Ui.resources.dll`;
>    3. **`Vipi.Infrastructure.MySqlMigrations.dll`** e gli altri `.dll`/`.pdb`;
>    4. **`Vipi.Host.dll` per ultimo** (porta il timbro).
> 3. poi il riavvio: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge.
>
> ⚠️ Tutti e sette gli assiemi `Vipi.*` cambiano insieme in questa consegna, quindi le rinomine vanno fatte
> **di seguito**: se il processo ripartisse a metà, le pagine cadrebbero finché non arriva il resto.

---

## Che cosa portano questi venticinque file

### 🟢 1. vAWOS — il quadro meteo di torre

Una pagina nuova: **`/services/vawos`**, e per un singolo scalo **`/services/vawos/LIRF`**. È il pannello
che un torrista tiene aperto su un secondo monitor mentre controlla, e mostra **quello che dice il METAR**,
messo nella forma in cui lo si legge in torre:

- il **vento** proiettato su ogni testata di pista: componente **frontale**, **traverso** e **coda**, coi
  colori che cambiano sulle soglie;
- l'**RVR**, una cella **per testata** (`RVR 07`, `RVR 25`); dove il bollettino tace, `///`;
- **QNH**, **QFE** di ciascuna soglia, **Transition Level** preso dalla tabella dell'aeroporto (non da una
  formula);
- l'**ATIS** di chi presiede, se c'è, con la sua lettera;
- la **pista in uso**, e — soprattutto — **chi l'ha decisa**: `from ATIS`, `from rule <nome>`, oppure
  `from wind`. In quest'ordine: l'ATIS batte le regole, perché dice cosa **sta succedendo**.

**Chi la vede:** chiunque, per gli aeroporti che hanno una **vIPI o un vSOP pubblicati**. Su uno scalo senza
documenti pubblici la pagina dice «no published document» e non mostra niente. Chi ha i permessi di editor
entra comunque, anche prima di pubblicare.

Il **Test METAR** — il riquadro per incollare un bollettino inventato e vedere come reagisce il quadro — è
**dello staff di divisione in su**: agli altri il tasto non compare, e il server ignora il parametro anche
se qualcuno se lo scrivesse a mano nell'indirizzo.

Si arriva al quadro da tre porte: la scheda in **`/services`**, il tasto **`vAWOS ↗`** nella scheda rapida di
un aeroporto, e lo stesso tasto nella **vista operativa** quando si è su una posizione d'aeroporto.

ℹ️ **Il vento non si anima**, ed è voluto: il METAR dichiara un **intervallo** (`200V280`, `18G32`), non che
cosa succede in un dato istante, e il vento istantaneo vicino alla pista noi non lo sappiamo. Il settore e la
raffica hanno le loro caselle, `EXTREMES` e `GUST`. Quel che si muove — orologio UTC, LED, l'età del dato che
ingiallisce — parla del **pannello**, non del tempo che fa.

### 🟢 2. I minimi LVP, sezione nuova di vIPI e vSOP

Nei documenti d'aeroporto compare una sezione **LVP** con i minimi per **preparazione**, **entrata in
vigore** e **cancellazione** (RVR e ceiling). Il quadro vAWOS li legge e propone lo stato: `LVP PREP`,
`LVP`, `LVP CANCEL?`.

- Se l'aeroporto **non dichiara** i suoi minimi, il quadro usa quelli **standard** (800 m / 300 ft in
  preparazione, 550 m / 200 ft in vigore) **e lo dice**: scrive `LVP (standard)`. Non abbiamo seminato lo
  standard su settanta scali: sarebbe stato affermare settanta volte una cosa che nessuno ha letto sull'AIP.
- Un aeroporto può dichiarare che **le LVP non si applicano**: il quadro scrive `LVP N/A`.
- La sezione si scrive **una volta sola**: i minimi stanno nell'anagrafica dello scalo, e vIPI e vSOP dello
  stesso aeroporto mostrano gli stessi — come già succede per frequenze, SID e piste.

⚠️ **Il quadro suggerisce, non decide.** Attivare o cancellare le LVP resta una decisione dell'aeroporto: la
pastiglia lo scrive per esteso passandoci sopra col mouse.

### 🟢 3. L'indice dei documenti d'aeroporto cambia

- Nella **vIPI civile** «Regole di selezione pista» diventa **figlia** di «Piste», e **LVP** nasce subito
  **dopo** «Tecnica operativa».
- Nel **vSOP militare** «LVP» sta al **primo livello, dopo «Procedure di volo»**. Le regole piste del vSOP
  **non** si toccano.

🔴 **All'avvio il sito sistema da solo i documenti che già esistono.** Nel log compare una riga come
«Sistemate «Regole piste» e «LVP» in N documenti d'aeroporto (vIPI e vSOP)». È una passata idempotente: al
secondo riavvio dirà 0.

⚠️ **Ma il pubblico vede il nuovo indice solo alla PROSSIMA PUBBLICAZIONE.** I documenti già pubblicati
compariranno fra i **«da ripubblicare»**: finché non li si ripubblica, la versione pubblica ha ancora
l'indice vecchio e **non ha** la sezione LVP. Non è un guasto, è come funzionano le release.

---

## Dopo il caricamento

1. **Il timbro** in `diagnostica/avvio-diagnostica.txt` dev'essere `1.25.0 · 909e3f03`.
   ⚠️ Il timbro dice **quale versione è partita**, non che il sito funzioni.
2. 🔴 **`admin/diagnostics`, riga `Schema`: dev'essere `0`.** Se non lo è, la migrazione non è entrata —
   il file `Vipi.Infrastructure.MySqlMigrations.dll` è quello vecchio o non è stato rinominato.
3. **La Ricerca risponde**: `/services/vsop/search`, due lettere, la riga sotto il campo deve cambiare. È il
   controllo che conta, perché passa dal server.
4. **Il quadro vAWOS si apre e si aggiorna**: `/services/vawos/LIRF` (o un altro scalo con documenti
   pubblici). Dev'esserci il METAR, le strisce di pista, e in basso l'età del dato che **cresce di secondo in
   secondo** — se resta ferma a «—», il JavaScript non è arrivato: rileggi il punto sui nove file di
   `wwwroot` **e** l'indice degli endpoint.
5. **La pagina ha il suo stile**: sfondo scuro, caselle a griglia. Se esce come testo nudo su fondo chiaro è
   `vipi-awos.css` a non essere arrivato.
6. **Uno scalo senza documenti pubblici** (per esempio un ICAO inventato, `/services/vawos/LIXX`) deve dire
   «no published document» — non un errore.
7. **Una vIPI d'aeroporto in bozza** ha adesso «Regole di selezione pista» **dentro** «Piste» e una sezione
   «LVP» dopo «Tecnica operativa».

ℹ️ **Da fare dopo, con calma, e non è roba da FTP**: scrivere i minimi LVP veri sugli scali che li hanno, e
ripubblicare i documenti perché la sezione arrivi al pubblico.
