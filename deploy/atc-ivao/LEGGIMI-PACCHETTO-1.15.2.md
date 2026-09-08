# Pacchetto 1.15.2 — solo i file cambiati

> **Timbro:** `1.15.2 · eb7f3894` (8 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.15.1**, online dalla notte del 7. **7 file.**
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟢 NIENTE DATABASE, NIENTE `wwwroot`
>
> Nessuna migrazione, nessuna tabella e nessuna colonna nuova: si carica quando volete, anche dentro la
> finestra cieca fino al 16. E nessun foglio di stile né script cambia — la trappola dei file che
> «viaggiano insieme» **non si applica** a questo pacchetto.
>
> ## 🟢 Non serve caricare tutto insieme
>
> Come in 1.15.1: la visibilità fra i pezzi non cambia, e sul server girano già tutti gli assiemi di
> 1.15.0. Restano comunque **7 file**, quindi la differenza è teorica.
>
> ## ⚠️ C'è una frase nuova
>
> `en/Vipi.Ui.resources.dll` entra: in Diagnostica compare un avviso nuovo (sotto il perché). Senza quel
> file, chi legge in inglese lo vedrebbe in italiano.

---

## 🔴 Le due cose che l'uso vero ha trovato in 1.15.1

### 1. L'editor non deve più morire mentre lo si usa

Il registro degli errori del server diceva due cose, e adesso non le dovrebbe più dire:

- **«A second operation was started on this context»** — la pagina e uno dei pannelli che ci stanno dentro
  interrogavano il database **nello stesso momento sullo stesso canale**, e l'editor si spegneva con la
  riga rossa «Attempting to reconnect». Cercati **tutti** i punti che potevano farlo, non solo quello che
  era esploso: dodici, e adesso è uno solo — l'unico caso in cui è voluto.
- **Chiudere l'editor mentre stava ancora caricando** lasciava un errore nel registro. Ora la chiusura
  **aspetta** il caricamento in volo.

ℹ️ **Non c'è niente da fare né da guardare**: sono guasti che si vedevano come «la pagina si è
riconnessa». La prova la darà il prossimo `errori-richieste.txt` scaricato qualche giorno dopo.

### 2. L'avviso «da ripubblicare» ora sparisce davvero appena si pubblica

Da 1.14.2 pubblicare chiude **all'istante** la riga «la copia pubblicata è indietro». Ma il banner giallo
in cima all'editor **continuava a mostrarla** finché non si ricaricava la pagina: la riga era già chiusa,
il banner no. Chi pubblicava e restava lì si vedeva chiedere il lavoro appena fatto.

**Adesso il banner si svuota da sé**, senza ricaricare niente.

⚠️ **Un caso che NON è un difetto**: pubblicando *adesso*, al posto di «da ripubblicare» può comparire
**«da preparare al ciclo entrante»**. Non è la stessa riga rimasta lì — è la sorella, e dice una cosa
diversa: quel che avete pubblicato è giusto oggi, ma al prossimo ciclo AIRAC non lo sarà più. Si chiude
programmando la release al ciclo entrante.

### 3. E se quel ricalcolo si guasta, adesso lo dice

Il ricalcolo che chiude la riga è fatto apposta per **non** far fallire una pubblicazione riuscita: se
salta, la pubblicazione resta valida e ci pensa il controllo giornaliero. Ma finora saltava **in silenzio**,
e allora il sintomo era identico al difetto: «ho ripubblicato e l'avviso è ancora lì», senza una riga da
nessuna parte a dire perché.

Ora in **Diagnostica → Documenti da rivedere** compare un avviso quando l'ultimo tentativo è fallito, con
la data e l'errore. Se non c'è, vuol dire che l'ultimo è andato bene.

---

## ✅ Che cosa guardare dopo aver caricato

1. **La Ricerca**: due lettere nel campo in alto, la riga sotto deve cambiare. È l'unico controllo che passa
   dal **server**: una pagina che si vede intera non dimostra che l'applicazione sia partita.
2. **Timbro**: `diagnostica/avvio-diagnostica.txt` deve dire `1.15.2 · eb7f3894`.
3. Con occhi da **amministratore**, ed è la prova vera di questo pacchetto: aprite un documento che il
   sistema segnala come **«da ripubblicare»** (lo trovate in «Da fare», oppure il banner giallo in cima al
   suo editor), **pubblicatelo** dal pannello «Versioni & release» in fondo alla pagina, e **restate lì**:
   il banner giallo deve sparire — o cambiare in «da preparare al ciclo entrante» — **senza ricaricare la
   pagina**. Con 1.15.1 restava com'era.
4. In **Diagnostica**, sotto «Documenti da rivedere»: **non** ci deve essere l'avviso nuovo sulla
   «ripulitura che segue ogni pubblicazione». Se compare, portatecelo: dice la data e l'errore.
