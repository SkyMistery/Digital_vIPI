# Pacchetto 1.12.0 — solo i file cambiati

> **Timbro:** `1.12.0 · e5077ab9` (6 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.11.0.** **17 file.**
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
> ## ⚠️ C'È `wwwroot`: DIECI FILE CHE VIAGGIANO INSIEME
>
> È l'unica trappola vera del pacchetto, ed è la stessa del 24 agosto. Questi dieci devono arrivare
> **tutti**, nello stesso caricamento:
>
> - `wwwroot/_content/Vipi.Ui/vipi-theme.css` + `.br` + `.gz`
> - `wwwroot/_content/Vipi.Ui/vipi-print.css` + `.br` + `.gz`
> - `wwwroot/_content/Vipi.Ui/vipi-ui.js` + `.br` + `.gz`
> - `Vipi.Host.staticwebassets.endpoints.json`
>
> L'ultimo è l'**indice**: dice al sito con che nome chiedere ogni foglio di stile e ogni script. Caricarne
> uno senza l'indice — o l'indice senza gli altri — fa chiedere al browser nomi che sul server non esistono,
> e la pagina esce **senza grafica** o con i tasti che non rispondono.
>
> ## ⚠️ E ci sono FRASI nuove
>
> `en/Vipi.Ui.resources.dll` entra: sono le frasi della ricerca negli allegati, dell'orientamento del
> riquadro e la parola **«Summary»** del sommario. Senza quel file la parte nuova si vedrebbe in italiano
> anche a chi legge in inglese.

---

## ✨ LA COSA NUOVA: gli allegati si cercano e si girano

### Cercare, invece di scorrere

Nel blocco **Allegato** dell'editor, sopra la tendina della biblioteca c'è ora una **casella di ricerca**.
In produzione la biblioteca ha **121 voci** e si percorreva solo a occhio.

- Cerca nel **titolo**, nella **sigla**, nel **perimetro** («LIRR») e nel **tipo** («LoA»); più parole si
  **sommano** (`loa lirr`).
- Filtrare **non sceglie**: la scelta resta un gesto in tendina, come prima.
- L'allegato **già scelto resta in elenco** anche se il filtro lo escluderebbe, così non sembra mai perso.

### Girare il riquadro

Quando un allegato è mostrato **incorporato**, l'editor ha una tendina in più — **«Come sta girato»** — con
quattro scatti: come nel file, 90° a destra, capovolto, 90° a sinistra. Serve alle scansioni orizzontali che
il PDF tiene in verticale.

⚠️ **È l'orientamento di PARTENZA.** Chi legge il documento trova **due tasti** sopra il riquadro e lo gira
come vuole, esattamente come nel visualizzatore PDF del browser. Quel giro resta nel **suo** browser: non
tocca il documento e non si vede agli altri.

ℹ️ Gira **il riquadro**, non il PDF: il file lo mostra il visualizzatore di Google, e gira anche la sua
barra. È il prezzo — l'alternativa non era un giro più pulito, era nessun giro.

---

## ✨ IL SOMMARIO È UNO SOLO, E UGUALE DAPPERTUTTO

La barra di navigazione a sinistra era scritta in **tre** modi diversi: uno per la vIPI ACC, uno per gli
altri quattro documenti, uno per gli editor. Ora è **una sola**, e vale anche negli editor.

**Che cosa si vede cambiato:**

- Si chiama **«Sommario»** in italiano e **«Summary»** in inglese, dappertutto. Prima l'inglese diceva
  «Contents» e la vIPI ACC diceva «Navigazione».
- ⚠️ **Le voci con sotto-sezioni nascono CHIUSE**, con una freccetta da premere per aprirle. Prima
  nascevano aperte, e su un vSOP militare erano venticinque righe che occupavano tutta la colonna. **Non è
  un guasto e non manca niente**: il sommario è più corto apposta.
- La vIPI ACC mostra ora anche le **sotto-sezioni**: prima si fermava al primo livello, e certe sezioni non
  erano raggiungibili da nessuna voce.
- 🔴 **Su tre documenti su cinque il sommario spariva scorrendo** (vIPI APP, vSOP d'aeroporto, vSOP
  militare): restava in cima e usciva dallo schermo. Ora **segue la pagina** su tutti e cinque, come faceva
  già sulla vIPI ACC e sulle vLOA.

---

## ✨ NELL'EDITOR SI CHIUDONO ANCHE LE SOTTO-SEZIONI

Fino a 1.11.0 nell'editor si poteva compattare solo una sezione di **primo livello**. Su un vSOP militare le
sotto-sezioni sono **venticinque su trentadue**: per arrivare all'ultima si scorrevano tutte le altre,
aperte.

Adesso ogni sezione ha la sua freccetta. **«Comprimi tutto» chiude tutto davvero** — 32 sezioni su 32,
prima ne chiudeva 7 — e il documento intero sta in una schermata.

⚠️ Conseguenza da sapere per chi riordina trascinando: per lasciare una sezione **fra due figlie** di
un'altra bisogna prima **aprire** quella voce. Nessun gesto è sparito; qualcuno costa un clic in più.

---

## Che cosa NON cambia

- **I documenti già pubblicati non cambiano**: una release è congelata. L'orientamento di un allegato entra
  alla prossima pubblicazione.
- Gli allegati **già inseriti** restano come sono: nascono «come nel file», cioè si vedono come ieri.
- Nessuna pagina nuova, nessuna sezione nuova, nessun permesso cambiato.

---

## I 17 file

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto: dopo il
caricamento si possono riconfrontare per essere sicuri che sia arrivato tutto intero.

| | |
|---|---|
| `Vipi.Host.dll` + `.pdb` | porta il **timbro** della versione |
| `Vipi.Ui.dll` + `.pdb` | il sommario, gli editor, il blocco allegato |
| `Vipi.Application.dll` + `.pdb` | l'orientamento nel riferimento dell'allegato |
| `en/Vipi.Ui.resources.dll` | le frasi inglesi nuove |
| `Vipi.Host.staticwebassets.endpoints.json` | l'**indice** degli asset |
| `wwwroot/_content/Vipi.Ui/vipi-theme.css` (+`.br`+`.gz`) | sommario, sotto-sezioni, tasti del giro |
| `wwwroot/_content/Vipi.Ui/vipi-print.css` (+`.br`+`.gz`) | i tasti del giro non si stampano |
| `wwwroot/_content/Vipi.Ui/vipi-ui.js` (+`.br`+`.gz`) | il giro del riquadro |

---

## Dopo il caricamento, tre controlli in un minuto

1. `diagnostica/avvio-diagnostica.txt` dice **`Versione 1.12.0 · commit e5077ab`**.
2. Un documento qualunque: la colonna a sinistra dice **«Sommario»** e **segue la pagina** mentre si scorre.
3. Un vSOP militare: le voci del sommario con la freccetta sono **chiuse**, e premendo la freccetta si
   aprono.

⚠️ Se la pagina esce **senza grafica**, è mancato uno dei dieci file di `wwwroot` o l'indice: si ricaricano
tutti e dieci insieme.
