# Pacchetto 1.13.0 — solo i file cambiati

> **Timbro:** `1.13.0 · 708257ed` (6 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.12.0.** **11 file.**
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟢 NIENTE DATABASE
>
> **Nessuna migrazione**: niente da concordare con chi amministra il database, nessuna copia di sicurezza,
> nessuna finestra da aspettare. Si carica quando volete, anche dentro la finestra cieca fino al 16.
>
> ## 🟢 E NIENTE `wwwroot`, questa volta
>
> Nessun foglio di stile e nessuno script cambiano: la trappola dei file che «viaggiano insieme»
> **non si applica** a questo pacchetto. Sono undici file e basta, tutti `.dll` e `.pdb`.
>
> ℹ️ Non è una dimenticanza: è stato **verificato con le impronte**, non a memoria — `vipi-theme.css`,
> `vipi-print.css`, `vipi-ui.js` e l'indice degli asset hanno lo `sha256` identico a 1.12.0.
>
> ## ⚠️ C'è un progetto in più del solito: `Vipi.Hosting.dll`
>
> Non compare in tutti i pacchetti. Ci vive il **cablaggio dei passi che girano all'avvio**, e questa volta
> ce ne sono due nuovi (vedi «Che cosa succede al primo avvio»). Senza quel file i vSOP militari già scritti
> resterebbero nella forma vecchia, in silenzio — nessun errore, solo un sito che non fa la cosa nuova.
>
> ## ⚠️ E ci sono FRASI nuove
>
> `en/Vipi.Ui.resources.dll` entra: è la nota che spiega da dove arrivano le coordinate delle soglie.
> Senza quel file quella riga si vedrebbe in italiano anche a chi legge in inglese.

---

## ✨ LA COSA NUOVA: i vSOP militari hanno l'indice che ha chiesto il SOD

L'elenco delle sezioni di un vSOP militare non lo decidiamo noi: lo ha mandato il SOD, ed è quello dei
documenti veri. Confrontato con quello che avevamo, **non mancava niente** — quindi non si è tolto nulla per
allinearsi, si è aggiunto.

### Dodici sezioni nuove

Nei **Dati generali**: «Coordinate delle soglie» (dentro «Piste») e «Planimetria dell'aeroporto», più
«Flusso di rullaggio sui piazzali» dentro «Parcheggi».

Nelle **Procedure di volo**: «Restrizioni all'arrivo» e «Restrizioni di circuito», che stanno in testa
accanto a quelle al decollo, più «Punti significativi VFR» dentro le porte VFR jet.

Nelle **Aree di lavoro**: «Procedure di partenza» e «Procedure di arrivo», ciascuna con dentro **VFR** e
**IFR**.

Nascono **vuote**: sono sezioni da scrivere, non contenuto che arriva da solo. Chi non le usa su un certo
campo le **nasconde**, come si è sempre fatto.

### Una sezione se ne va: «QRA / Scramble»

Era l'unica sezione che avevamo aggiunto noi, e nei quindici SOP reali non esiste — QRA ci compare solo
come colonna, e solo sulle quattro basi di difesa aerea. Il SOD non la vuole, e quindi esce.

⚠️ **Chi ci aveva scritto dentro non perde niente.** Se la sezione è vuota sparisce; se contiene del testo
**resta**, col suo titolo e il suo contenuto, e diventa una sezione libera come tutte le altre — si può
rinominare, spostare o cancellare a mano.

### Le coordinate delle soglie sono una sezione, non più una tabella dentro «Piste»

Stesso dato e stessa tabella di prima: cambia solo che ha un titolo suo nell'indice, e che si può
nascondere da sola.

⚠️ **Non si scrivono a mano, né qui né altrove**: arrivano da IVAO insieme alle piste. Un campo che non le
ha ancora mostra la riga che lo dice; per averle si **ri-importa l'aeroporto** dalla pagina delle sorgenti.

---

## ⚠️ LA COSA DA SAPERE PRIMA DI GUARDARE: dodici sezioni non si vedono nella vista ATC

Il SOD ha marcato dodici sezioni come **«per i piloti»**, e da questo pacchetto **nascono già così**:

> Aeroporti alternati · Coordinate delle soglie · Nominativi · Parcheggi · Flusso di rullaggio sui piazzali ·
> Messa in moto · Armamento/disarmo · Restrizioni al decollo · Restrizioni all'arrivo · Punti significativi
> VFR · Punti significativi strumentali · Bassa quota (BOAT)

**Che cosa vuol dire in pratica.** Un documento ha tre viste: **Tutti** (quella di partenza), **Pilota** e
**ATC**. Nella vista **ATC** quelle dodici sezioni **non compaiono**. Non sono sparite e non sono
riservate: chi controlla e le vuole leggere torna su **«Tutti»**, che è la vista con cui la pagina si apre
sempre.

🔴 **È una scelta, non un difetto**, e va detto a chi userà il documento — o la prima segnalazione sarà
«ho perso metà del SOP».

---

## Che cosa succede al primo avvio

Il sito, appena riparte, sistema da solo i vSOP militari già scritti. Non c'è niente da premere.

1. **Aggiunge le dodici sezioni nuove** dove il catalogo le vuole, vuote, senza toccare quello che c'è.
2. **Toglie «QRA / Scramble»** con la regola qui sopra: vuota sparisce, scritta diventa sezione libera.
3. **Marca «per i piloti»** le sezioni dell'elenco qui sopra che stavano ancora su «per tutti».

⚠️ **Il passo 3 ribalta una volta sola le marcature messe a mano prima di oggi.** Se qualcuno aveva
scelto di proposito «per tutti» su una di quelle dodici, se la ritrova su «per i piloti» e deve rimetterla
— un clic. Non c'è modo di distinguere una scelta fatta apposta da un valore mai toccato: sono lo stesso
valore. Sono pochi documenti e la cosa si vede subito, ma è giusto che non venga scoperta a schermo.

⚠️ **I documenti già PUBBLICATI non cambiano.** Chi apre la pagina pubblica di un vSOP continua a vedere
l'indice vecchio finché quel documento non viene **ripubblicato**. È la regola di sempre: una release è una
fotografia, e non si riscrive da sé.

---

## I file

Tutti in `httpdocs/app/` (o dove sta l'applicazione), rispettando le sottocartelle.

```
Vipi.Application.dll        Vipi.Application.pdb
Vipi.Infrastructure.dll     Vipi.Infrastructure.pdb
Vipi.Hosting.dll            Vipi.Hosting.pdb
Vipi.Ui.dll                 Vipi.Ui.pdb
Vipi.Host.dll               Vipi.Host.pdb
en/Vipi.Ui.resources.dll
```

Le impronte `sha256` di tutti e undici stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

---

## Il controllo finale — e non è «la pagina si apre»

⚠️ Il selettore della lingua, lo zoom e il tema **funzionano anche su un sito in cui Blazor non è mai
partito**: non provano niente. Serve un comando che passa dal **server**.

1. **La Ricerca risponde.** `https://atc.it.ivao.aero/services/vsop/search` → si scrivono due lettere → la
   riga sotto il campo deve cambiare. Se resta ferma, il caricamento è incompleto: rifarlo.
2. **La versione è quella giusta.** `diagnostica/avvio-diagnostica.txt`, riga `Versione`: deve dire
   `1.13.0`.
3. **La cosa nuova c'è davvero** (il controllo che il timbro non dà). Si apre in **modifica** un vSOP
   militare qualsiasi e si guarda l'indice: devono comparire «Planimetria dell'aeroporto» fra i dati
   generali e «Restrizioni all'arrivo» fra le procedure di volo, e **non** deve più esserci
   «QRA / Scramble» — a meno che su quel campo non ci fosse scritto qualcosa, e allora è lì col suo nome
   come sezione libera.

ℹ️ Il punto 3 va fatto **mentre chi ha caricato è ancora al telefono**: è il momento in cui un file
dimenticato si rimette in trenta secondi.
