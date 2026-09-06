# Pacchetto 1.14.0 — solo i file cambiati

> **Timbro:** `1.14.0 · a9979306` (6 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.13.0.** **5 file** — è il pacchetto più piccolo finora.
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
> ## 🟢 E NIENTE `wwwroot`
>
> Nessun foglio di stile e nessuno script cambiano: la trappola dei file che «viaggiano insieme»
> **non si applica** a questo pacchetto. Cinque file e basta.
>
> ℹ️ Verificato **con le impronte**, non a memoria: `vipi-theme.css`, `vipi-print.css`, `vipi-ui.js`,
> `vipi-boot.js` e l'indice degli asset hanno lo `sha256` identico a 1.13.0.
>
> ## ⚠️ Ci sono FRASI nuove
>
> `en/Vipi.Ui.resources.dll` entra: sono le tre righe nuove del cruscotto della traduzione. Senza quel file
> chi legge in inglese le vedrebbe in italiano.
>
> ## ℹ️ E manca un progetto che c'era la volta scorsa
>
> Solo `Vipi.Ui` è cambiato davvero. `Vipi.Application`, `Vipi.Infrastructure` e `Vipi.Hosting` **non**
> entrano: le loro impronte cambiano a ogni ricompilazione (è l'identificativo interno dell'assieme), ma il
> codice è lo stesso di 1.13.0. Ogni file in più è una rinomina in più su un file che il processo tiene
> aperto: non è prudenza, è rischio.

---

## ✨ LA COSA NUOVA: lo stato della traduzione, nell'editor, non si spegne più da solo

Nell'editor di ogni documento c'è il blocco **«Traduzione»**, chiuso di suo. Da questo pacchetto dice
sempre a che punto è quel documento — prima si spegneva proprio nei casi normali.

### Che cosa si vede adesso

Nella testata del blocco, senza aprirlo: la **percentuale** tradotta e, se manca qualcosa, quante frasi.
Aprendolo, una riga sola:

> bozza tradotta al 84% · pubblicato al 78% · 3 da tradurre · il giro passa fra ~13 min   **[Traduci ora]**

- **due percentuali, mai una media**: *bozza* è quel che state per pubblicare, *pubblicato* è quel che un
  lettore vede adesso. «Bozza 100%, pubblicato 40%» dice una cosa che una media di «70%» nasconderebbe;
- se il documento non ha una release in vigore la seconda voce dice **«nessuna release»**, che non è
  «0% tradotto»: sono due cose diverse;
- **il giro automatico passa ogni quarto d'ora** e traduce da sé quel che manca: la riga dice quanto manca
  al prossimo.

### E funziona anche in italiano

Prima, con la barra su **IT** e un documento scritto in italiano, il blocco diceva soltanto «passa
all'altra lingua per rivedere come viene letto» — nessuna percentuale, nessun tempo, nessun tasto. Cioè il
cruscotto mancava proprio a chi **scrive**. Adesso lo stato e il tasto ci sono in tutt'e due le lingue;
l'elenco delle frasi da rileggere resta in inglese, perché è lì che si legge come suona.

### Il tasto «Traduci ora» c'è sempre

Prima spariva appena il giro automatico aveva fatto il suo lavoro — cioè quasi sempre. Adesso è sempre
premibile, e serve dopo aver scritto o corretto una frase per vederla resa **subito** invece che entro un
quarto d'ora.

ℹ️ **Non spende se non c'è niente da fare**: a zero frasi mancanti risponde «Non mancava niente» senza
chiamare nessun motore. Se un giro automatico sta già girando lo dice e non fa nulla — si riprova fra poco.

### E c'è anche sui documenti dell'editor unito

Quando due o più documenti si redigono insieme (per esempio il vIPI di un aeroporto e il suo vSOP
militare), fino a ieri il blocco compariva **solo sul primo**. Adesso ogni documento ha il suo, con il suo
stato: la traduzione è del documento, non della pagina che lo ospita.

---

## ✨ E i link dell'elenco dei vSOP militari aprono la vista PILOTA

Da `/services/vsop/mil`, il nome di un aeroporto apre il documento già nella vista **Pilota** invece che
in «Tutti».

⚠️ **Non si vedrà finché non c'è un vSOP militare pubblicato** — oggi non ce n'è nessuno su nessuno dei
quattro ACC. È una riga che aspetta il primo documento in vigore.

---

## I file

Tutti in `httpdocs/app/` (o dove sta l'applicazione), rispettando le sottocartelle.

```
Vipi.Ui.dll                 Vipi.Ui.pdb
Vipi.Host.dll               Vipi.Host.pdb
en/Vipi.Ui.resources.dll
```

Le impronte `sha256` di tutti e cinque stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

---

## Il controllo finale — e non è «la pagina si apre»

⚠️ Il selettore della lingua, lo zoom e il tema **funzionano anche su un sito in cui Blazor non è mai
partito**: non provano niente. Serve un comando che passa dal **server**.

1. **La Ricerca risponde.** `https://atc.it.ivao.aero/services/vsop/search` → si scrivono due lettere → la
   riga sotto il campo deve cambiare. Se resta ferma, il caricamento è incompleto: rifarlo.
2. **La versione è quella giusta.** `diagnostica/avvio-diagnostica.txt`, riga `Versione`: deve dire
   `1.14.0`.
3. **La cosa nuova c'è davvero** (il controllo che il timbro non dà). Si apre un documento qualsiasi in
   **modifica**, con la barra della lingua su **IT**, e si apre il blocco «Traduzione»: deve comparire la
   riga con le due percentuali e il tasto **«Traduci ora»**. Se dice solo «stai leggendo nella lingua in
   cui questo documento è scritto», sta girando ancora la versione vecchia.

ℹ️ Il punto 3 va fatto **mentre chi ha caricato è ancora al telefono**: è il momento in cui un file
dimenticato si rimette in trenta secondi.
