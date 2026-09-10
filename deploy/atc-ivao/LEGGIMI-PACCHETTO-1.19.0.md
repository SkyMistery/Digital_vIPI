# Pacchetto 1.19.0 — solo i file cambiati

> **Timbro:** `1.19.0 · 6f38e58` (10 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.18.2.** **22 file**.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟡 C'È UNA MIGRAZIONE, E SI CARICA COMUNQUE
>
> Questa consegna aggiunge **una colonna** (`SectorFallbacks.TargetKind`). La migrazione si applica **da
> sola** al primo avvio, come sempre, e **non c'è niente da fare a mano**.
>
> **Perché si può caricare anche adesso**, dentro la finestra in cui nessuno amministra il database: la
> domanda che conta non è «c'è una migrazione?» ma **«può lasciare il database in uno stato da cui
> l'applicazione non riparte?»**. Questa aggiunge una colonna con un valore di default e non tocca nulla di
> esistente: se si fermasse a metà, si fermerebbe **prima** di aver cambiato qualcosa.
>
> ⚠️ Un presidio automatico controlla questa regola a ogni consegna, e **in questa ha fermato** una
> modifica più invasiva che avevo scritto per errore: la colonna sarebbe stata riscritta per intero, e su
> MariaDB questo vuol dire riscrivere la tabella. È stata rifatta additiva.
>
> ## 🔴 E QUESTA VOLTA CI SONO ANCHE `wwwroot` E L'INGLESE
>
> Due file di asset sono cambiati — `vipi-aor3d.js` e `vipi-theme.css` — e vanno caricati **coi loro
> compressi** (`.br` e `.gz`) **e insieme** a `Vipi.Host.staticwebassets.endpoints.json`.
>
> ⚠️ **Perché insieme:** quell'indice dice al sito con che nome chiedere ogni file. Caricarne uno solo fa
> chiedere al browser nomi che non esistono.
>
> C'è anche **`en/Vipi.Ui.resources.dll`**: le frasi inglesi sono trentotto in più. Senza, le pagine nuove
> parlano italiano a chi legge in inglese.
>
> 🔴 **E `Vipi.Infrastructure.MySqlMigrations.dll` è il file da non dimenticare.** È quello che porta la
> colonna nuova. Senza di lui il pacchetto **sembra funzionare**: il timbro è giusto e tutto il resto c'è,
> perché la funzione nuova resta spenta finché nessuno scrive una riga di ripiego. Ci si accorgerebbe del
> file mancante solo al primo tentativo di scriverne una, che fallirebbe.

---

## Che cosa portano questi ventidue file

### 🟢 1. Il ripiego di un settore che sta «sopra tutti» — MIL e FSS

Era la domanda: *«il settore MIL è sempre per tutto l'ACC, ma se un aereo parte da Ghedi `LIPX_ES0` lo
trasferisce a MIL, e se MIL è chiuso deve andare a ES2; da `LIMC_ANE` invece, con MIL chiuso, i traffici
verso est vanno a ES2 e quelli verso ovest a WS2»*.

**Guardato in produzione, il sistema non poteva dirlo.** La catena di `LIMM_MIL_CTR` era
`→ LIMM_WS2_CTR` e nient'altro: MIL chiuso mandava **sempre** a WS2. Giusto per metà dei casi, sbagliato per
l'altra metà, e senza nessun segnale.

⚠️ **La causa non erano i dati.** Un settore che copre **tutto** l'ACC dal suolo all'illimitato non è
«dentro» nessun altro: sotto di lui ci sono **due** settori d'area, e oltre confine quelli di un altro
centro. Il campo che dice «chi c'è sopra di me» è **uno solo**, e non può nominarli tutti.

**Adesso** un ripiego può avere per bersaglio, invece di un nome, una **domanda**: «chi copre *questo punto*,
a *questa quota*, adesso». La risposta la dà la geometria delle aree, che il sito ha già.

**Che cosa fare per accenderlo** (niente si accende da sé — vedi «Cosa resta da fare» in fondo): in
**Struttura**, sul settore MIL, si aggiunge una riga di ripiego e si spunta **«copertura del punto»**. Una
riga per ogni MIL d'ACC, e basta.

⚠️ **Finché quella riga non c'è, il comportamento è identico a 1.18.2.** Non cambia niente da solo.

### 🟢 2. «Come risale» — si può finalmente *guardare* una configurazione

Nei **Trasferimenti**: si selezionano una o più clausole, si preme **«Come risale»**, e il pannello mostra
la discesa per intero — chi prende il traffico, e poi, chiuso quello, chi lo prende ancora, fino a UNICOM.
Ogni gradino dice **perché**: il ricevente scritto, una riga di ripiego, la copertura del punto, il padre.

Il tasto è acceso fino a **dieci punti** (non dieci righe: una clausola può portare più punti, e ogni punto
ha la sua scala). Oltre, il tasto si spegne e dice quanti punti avete selezionato.

Le scale identiche si raggruppano, così dieci righe diventano due o tre blocchi invece di un muro — e la
**differenza** fra due punti salta all'occhio, che è la cosa che si viene a vedere.

### 🟢 3. Quattro controlli nuovi in Diagnostica

- **Ricaduta che non copre la quota** — un settore alto la cui catena di ripiego non ha nessuno a quella
  quota. ⚠️ Sul nostro archivio ne trova già uno vero: `LIMM_WS5_CTR` parte da FL325 e, chiuso lui, il
  traffico va a chi lì non ha niente.
- **Albero proiettato divergente** — quando la gerarchia dei cataloghi e quella interna dicono due cose
  diverse.
- **CoP senza posizione** — quanti punti dei vostri accordi nessun catalogo sa collocare. Serve a sapere
  *prima* su quanti la «copertura del punto» non potrà rispondere.
- **Trasferimento senza ripiego** — un ricevente che, chiuso, manda su UNICOM mentre quel punto lo copre
  qualcun altro.

### 🟢 4. Le quote delle aree escono in piedi

Era la segnalazione: *«nell'AoR dei BOAT le quote escono in FL e non in piedi — 150 ft diventa FL150, nella
AT Basilicata che è 150 ft – 1000 ft»*.

**Era peggio di come si vedeva.** Non solo l'etichetta: la banda veniva letta a rovescio (il tetto finiva
*sotto* il piede) e il **3D disegnava quell'area a 15 000 piedi**, alta cento, invece che fra 150 e 1000.

Ora le quote delle aree si leggono in piedi, e la **mappa dice la stessa cosa della tabella** che le sta
sotto — prima la mappa diceva `FL150` dove la tabella diceva `150 ft`, della stessa area. Vale per le aree
regolamentate, per le aree BOAT e per i volumi caricati dal KMZ.

### 🟢 5. NIL

Un blocco di testo nuovo **nasce dicendo `NIL`** invece di «Nuovo testo…»: in un documento operativo NIL
vuol dire «qui non c'è niente, e non è una dimenticanza» — che è già la cosa giusta da leggere se nessuno ci
scrive.

E un testo che è **soltanto** NIL **non viene tradotto**: non finisce nella memoria di traduzione, non va al
motore (quindi non costa), non conta come «da tradurre» nel cruscotto, e a schermo si legge **NIL in
entrambe le lingue**.

⚠️ La parola dentro una frase resta contenuto: *«NIL for runway 07»* si traduce normalmente.

---

## Dopo il caricamento

1. Aprite `https://atc.it.ivao.aero/services/vsop` e controllate il timbro in alto: **`1.19.0 · 6f38e58`**.
2. ⚠️ **Non fermatevi al timbro.** Provate la **Ricerca** (`/services/vsop/search`, due lettere): passa dal
   server, e un caricamento incompleto darebbe un sito che si vede intero e non risponde a niente — col
   timbro giusto.
3. Aprite **Diagnostica**: se `Vipi.Infrastructure.MySqlMigrations.dll` non fosse arrivato, è lì che si
   vedrebbe.
4. Aprite una **vSOP militare con aree BOAT** e controllate che le quote dicano `ft`.

## Cosa resta da fare, e va fatto a mano

Niente di questo si accende da sé — sono decisioni, non automatismi:

1. **La riga «copertura del punto» sui cinque MIL d'ACC** (Struttura → il settore MIL → aggiungi ripiego →
   spunta «copertura del punto»). È il gesto che accende il punto 1.
2. **Tre settori di Roma sono «radici»**: `LIRR_MIL_CTR`, `LIRR_FSS` e `LIRR_PLN_FSS` non hanno nessuno
   sopra di loro, quindi chiusi mandano il traffico su **UNICOM**. Va deciso a chi devono ricadere.
3. **`LIMM_WS5_CTR` non ha nessun ripiego a FL325**: una riga sola lo sistema, ed è il rovescio esatto di
   quella che esiste già su `LIMM_ES5_CTR`.
4. Roma ha **cinque radici** in tutto: se è voluto va bene, ma vale una guardata.
