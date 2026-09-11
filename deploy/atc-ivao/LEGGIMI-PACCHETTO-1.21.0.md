# Pacchetto 1.21.0 — solo i file cambiati

> **Timbro:** `1.21.0 · 8291dd14` (11 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.20.0.** **9 file**.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟢 NESSUNA MIGRAZIONE
>
> Lo schema del database resta quello di 1.20.0: non c'è niente da fare a mano, e la riga **Schema** in
> `admin/diagnostics` deve restare **0** com'è adesso.
>
> ## 🟡 AL PRIMO AVVIO SI SCRIVE NEI DATI, ED È VOLUTO
>
> I vSOP militari guadagnano una sezione nuova, **«SID»**. Al primo avvio l'applicazione la **aggiunge da
> sola** ai vSOP già scritti: è la stessa passata che gira da sempre a ogni avvio, e che ha già aggiunto
> ogni sezione nuova del catalogo. Scrive **righe**, non tocca lo schema.
>
> ⚠️ **Sulle pagine pubbliche la sezione compare alla prossima pubblicazione** di ciascun vSOP. Una release
> già in vigore non cambia: è una fotografia, e resta quella. Provato: su un vSOP con la release in vigore
> la pagina pubblica, dopo l'avvio, **non** mostra ancora le SID.
>
> ⚠️ **Quindi i vSOP militari già pubblicati possono comparire fra i documenti «da ripubblicare»** (in
> *Diagnostica* e in *Da fare*): la bozza ha una sezione che la copia pubblicata non ha. È il segnale
> giusto, e si chiude ripubblicandoli. È già successo così quando sono arrivate le *Carte aeroportuali*.
>
> ## 🟢 NIENTE `wwwroot`
>
> Confrontati uno per uno col pacchetto di prima, **tutti** i `.css`, i `.js` e l'indice degli asset sono
> identici: niente `.br`/`.gz` e niente `Vipi.Host.staticwebassets.endpoints.json`.
>
> C'è invece **`en/Vipi.Ui.resources.dll`**: sei frasi nuove. Senza, la conferma e il rilievo nuovi parlano
> italiano a chi legge in inglese.
>
> ⚠️ **E c'è `Vipi.Host.dll` anche se il suo codice non è cambiato**: il numero di versione vive lì dentro.
> Senza, la pagina direbbe ancora «1.20.0».

---

## Che cosa portano questi nove file

### 🟢 1. Le SID nel vSOP militare

**Chiesto da te.** Sezione **«SID»** in **Dati generali**, subito dopo le **Piste**.

- Si **legge** dall'anagrafica dell'aeroporto su **tutti** i campi, come frequenze, piste e quote di
  transizione.
- Si **scrive** dentro il vSOP **solo** sui campi **solo militari senza vIPI civile**. Sui campi misti resta
  la nota che rimanda all'editor della vIPI civile: due porte di scrittura sullo stesso dato sono il modo in
  cui una delle due comincia a mentire.

⚠️ **Nell'indice troverai due voci «SID»**: questa (la **tabella** delle procedure, in *Dati generali*) e
quella che c'era già in *Carte aeroportuali* (la **raccolta delle carte**). Stanno in due gruppi diversi, ed
è la stessa coppia che la vIPI civile ha da sempre.

### 🟢 2. Un campo «solo militare» con una vIPI civile: adesso lo si dice

**Chiesto da te.** Marcare «solo militare» un campo che ha già una vIPI civile **non tocca** quel
documento: resta com'è, e se è pubblicato resta online — finché qualcuno non lo nasconde o lo elimina.
Prima però non lo diceva nessuno. Adesso:

- nella pagina **Aeroporti** la pastiglia militare **chiede conferma** quando accende «solo militare» su un
  campo che ha una vIPI civile — e **solo lì**: sugli altri il clic resta secco;
- in **Diagnostica** c'è un rilievo nuovo, **«vIPI civile su campo solo militare»**, che dice i **due**
  gesti per uscirne: **nascondere** la vIPI **e sciogliere l'unione** col vSOP, se sono uniti. Nascondere da
  solo non basta: ogni pubblicazione del vSOP pubblica anche i documenti uniti, nascosti compresi.

✅ **Il caso a posto tace**: vIPI già fuori dal pubblico e già staccata ⇒ nessun rilievo.

---

## Dopo il caricamento

1. **Il timbro** in `diagnostica/avvio-diagnostica.txt` dev'essere `1.21.0 · 8291dd14`.
   ⚠️ Il timbro dice **quale versione è partita**, non che il sito funzioni.
2. **La Ricerca risponde**: `/services/vsop/search`, due lettere, la riga sotto il campo deve cambiare. È il
   controllo che conta, perché passa dal server.
3. **La riga `Schema` in `admin/diagnostics` resta `0`.**
4. Col login, **l'editor di un vSOP militare** (per esempio Gioia del Colle): in *Dati generali*, dopo le
   Piste, c'è **«SID»**. È la prova che la passata d'avvio ha lavorato.
5. In **Diagnostica**, se in produzione c'è un campo solo militare con la vIPI civile ancora online, adesso
   compare il rilievo nuovo.
