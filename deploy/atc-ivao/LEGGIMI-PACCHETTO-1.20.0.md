# Pacchetto 1.20.0 — solo i file cambiati

> **Timbro:** `1.20.0 · 775170f3` (10 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.19.1.** **13 file**.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟡 CI SONO DUE MIGRAZIONI, E SI CARICANO COMUNQUE
>
> Questa consegna aggiunge **due colonne** alle clausole dei coordinamenti e ne **allarga una**. Le
> migrazioni si applicano **da sole** al primo avvio: **non c'è niente da fare a mano**.
>
> **Perché si può caricare anche adesso**, dentro la finestra in cui nessuno amministra il database: la
> domanda che conta non è «c'è una migrazione?» ma **«può lasciare il database in uno stato da cui
> l'applicazione non riparte?»**.
>
> - Le due colonne nuove nascono con un valore di riposo, e non toccano niente di esistente.
> - L'**allargamento** (`ConditionAreaLabel` da 80 a 200 caratteri) non può troncare niente: si allarga, non
>   si stringe. Non aggiunge vincoli e non crea indici, quindi non può fallire sui dati che ci sono.
>
> ⚠️ **Un presidio automatico ha fermato questa consegna**, ed è giusto così: su MariaDB allargare una
> colonna vuol dire riscrivere la tabella, e mentre lo fa il sito aspetta. È stato **guardato a mano e
> approvato**, con la ragione scritta nel codice: quella tabella ha **sessanta righe**, non le centinaia di
> migliaia per cui il presidio esiste. La riscrittura è istantanea.
>
> ## 🔴 IL FILE DA NON DIMENTICARE
>
> **`Vipi.Infrastructure.MySqlMigrations.dll`** è quello che porta le colonne nuove. Senza di lui il
> pacchetto **sembra funzionare**: il timbro è giusto e tutto il resto c'è, perché le due funzioni nuove
> restano spente e **non danno nessun segnale** finché qualcuno non prova a usarle.
>
> **Come si controlla, da fuori e senza shell:** `admin/diagnostics`, riga **Schema**. Se dice **0**, le
> colonne ci sono.
>
> ## 🟢 NIENTE `wwwroot` QUESTA VOLTA
>
> Nessun `.css` e nessun `.js` è stato toccato: niente `.br`/`.gz` e niente
> `Vipi.Host.staticwebassets.endpoints.json`.
>
> C'è invece **`en/Vipi.Ui.resources.dll`**: le frasi inglesi sono cambiate (sei nuove, cinque tolte). Senza,
> i comandi nuovi parlano italiano a chi legge in inglese.

---

## Che cosa portano questi tredici file

### 🔴 1. Aprendo un documento unito, si legge PRIMA quello che si è aperto

**Segnalato da te.** Unendo la vIPI d'aeroporto, il vSOP e l'avvicinamento dello stesso scalo, la pagina
unita metteva sempre gli stessi documenti nello stesso ordine, e chi apriva l'indirizzo di uno degli altri
veniva **rimandato** altrove.

Adesso **l'ordine dipende da dove si entra**:

| Apri… | Leggi |
|---|---|
| la vIPI d'aeroporto | vIPI · vSOP · APP |
| il vSOP | vSOP · vIPI · APP |
| l'avvicinamento | APP · vIPI · vSOP |

Gli altri seguono nell'ordine deciso col pannello (le due frecce ↑↓). **Negli editor vale lo stesso.**

⚠️ **È l'unica cosa di questo pacchetto che cambia un comportamento pubblico**: l'indirizzo di un documento
unito che prima rimandava altrove, adesso **risponde**. Lo stesso contenuto si legge da più indirizzi, in
ordini diversi — è quel che è stato chiesto.

✅ **E si porta via un difetto**: spostando un membro in cima, l'editor unito **si spostava di pagina** e
sembrava che l'unione fosse sparita. Adesso le frecce spostano solo l'ordine dei documenti che seguono; il
posto dove si lavora non si muove più.

**Che cosa guardare dopo il caricamento** (serve il login): aprire i tre indirizzi dell'unione di Gioia del
Colle e verificare che ognuno metta **sé stesso in testa**, senza rimandare da nessuna parte.

### 🟢 2. Una condizione può dire «con quest'area NON attiva»

**Chiesto da te.** Nella clausola di un coordinamento, la condizione d'area diceva solo *attiva*. Adesso c'è
una casella **«NON attiva/e»**, e il documento esce:

> «… su TORPO **con la $406 non attiva**.» — «… over TORPO **with $406 not active**.»

⚠️ La casella è **spenta finché non scegli un'area**, e dice perché accanto.

### 🟢 3. Le aree si elencano, e la frase si adatta

**Chiesto da te.** Dove prima si sceglieva **un'** area adesso se ne scelgono quante servono — nel pannello
della clausola **e** nella barra che scrive su più righe insieme. Accanto c'è un selettore che dice come
vanno lette:

| Aree | Senso | Documento |
|---|---|---|
| A, B | ne basta una qualunque | «con A **o** B **attive**» |
| A, B | valgono tutte | «con A **e** B **attive**» |
| A, B | una qualunque, NON attive | «con A **o** B **non attive**» |
| A, B | tutte, NON attive | «con A **e** B **non attive**» |

⚠️ Il selettore è **spento con una sola area** — lì la domanda non ha senso — col motivo accanto.

Nella tabella la condizione si legge in breve: `area A / B` (una qualunque) · `area A + B` (tutte) · `⊘` se
è al rovescio.

---

## Dopo il caricamento

1. **La riga `Schema` in `admin/diagnostics` dev'essere `0`.** È la prova che le due colonne sono nate, e si
   fa da fuori senza shell. Se non è zero, `Vipi.Infrastructure.MySqlMigrations.dll` non è arrivato.
2. **Il timbro** in `diagnostica/avvio-diagnostica.txt` dev'essere `1.20.0 · 775170f3`.
   ⚠️ Il timbro dice **quale versione è partita**, non che il sito funzioni.
3. **La Ricerca risponde**: `/services/vsop/search`, due lettere, la riga sotto il campo deve cambiare. È il
   controllo che conta, perché passa dal server.
4. **I tre indirizzi dell'unione di Gioia del Colle**, col login: ognuno mette sé stesso in testa.
5. **Una clausola con due aree**: spuntare «NON attive», scegliere «valgono tutte», e leggere la frase.

⚠️ **Se questo pacchetto viene caricato dopo il 16 settembre**, la deroga scritta nel presidio delle
migrazioni non serve più: dimmelo e la tolgo insieme al file, invece di lasciarla lì a invecchiare.
