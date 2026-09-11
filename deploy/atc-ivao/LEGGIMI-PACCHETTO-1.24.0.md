# Pacchetto 1.24.0 — solo i file cambiati

> **Timbro:** `1.24.0 · ed4c0f36` (12 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.23.0.** **6 file**. **Nessuna migrazione**: il database non si tocca.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟢 QUESTA VOLTA NON C'È `wwwroot`, E NEMMENO IL SATELLITE INGLESE
>
> Nessun foglio di stile, nessuno script, nessuna frase nuova: solo tre assiemi coi loro `.pdb`.
>
> ## 🔴 IL FILE DA NON DIMENTICARE
>
> **`Vipi.Host.dll`** porta il **timbro**, anche se il suo codice non è cambiato.
>
> ## L'ORDINE
>
> 1. prima si caricano **tutti e 6** i file col nome finto;
> 2. poi le rinomine, **una di seguito all'altra e senza pause**, lasciando **`Vipi.Host.dll` per ultimo**.
>
> ⚠️ `Vipi.Ui.dll` e `Vipi.Application.dll` viaggiano **insieme**: il secondo porta un tipo che il primo usa
> con un campo in più. Se il processo si riavviasse con solo uno dei due, le pagine dei documenti d'aeroporto
> potrebbero cadere finché non arriva l'altro — dura il tempo delle due rinomine, ma per questo si fanno
> **una subito dopo l'altra**.

---

## Che cosa portano questi sei file

### 🟢 1. Le regole piste nelle vSOP militari

**Chiesto da te.** Le regole di scelta pista mancavano nelle vSOP, e su un campo **senza vIPI civile** non si
potevano scrivere da nessuna parte.

- Nel documento c'è una sezione **«Regole piste»** in *Dati generali*, **subito dopo le Piste** e prima delle
  SID.
- **Funziona come nella vIPI**: dalla regola che vale adesso, col vento del METAR, la tabella **Piste** marca
  la pista in uso (🛫 partenze, 🛬 arrivi) e le **SID** si aprono già su quella pista.
- **Dove si scrivono**: su un campo **solo militare senza vIPI civile** si scrivono **nell'editor della vSOP**,
  con lo stesso banco di prova della vIPI («dammi un vento e un momento, ti dico quale regola vince»). Su un
  campo che ha anche la vIPI civile la sezione si **vede** ma si scrive di là, e il rimando è in pagina.
- Le regole sono un dato **dell'aeroporto**: le stesse le legge chiunque altro citi quello scalo.

⚠️ **Oggi nessun campo solo militare ha regole scritte** — non c'era modo di scriverle. La sezione nasce
**vuota** e la pista marcata la sceglie il vento, finché qualcuno non le scrive.

### 🟢 2. Il verdetto segue la sezione (vale anche per le vIPI civili)

La tabella delle regole seguiva già l'interruttore **🧊 Frozen / 🔴 Live** della sezione: congelata = la
fotografia della release, live = quel che c'è adesso. Il **verdetto** — quale regola vince, quale pista è
marcata — si calcolava invece **sempre** sulle regole di adesso. Con la sezione congelata bastava cambiare una
regola dopo aver pubblicato perché la stessa pagina dicesse **due cose diverse**.

Adesso il verdetto si calcola sulle **stesse regole che il lettore ha davanti**. Il **vento** resta sempre
quello attuale: non è contenuto del documento.

⚠️ Le release **già pubblicate** non portano il dato nuovo: lì la pista marcata continua a venire dalle regole
di adesso (come prima), ma spariscono la pastiglia «QNH attuale» sulla riga e la scritta «consigliate dalla
regola X», che indicherebbero una riga di un'altra tabella. Tornano alla **prima ripubblicazione** di quel
documento.

---

## ⚠️ Due cose che vedrai al primo avvio, e sono attese

1. **I vSOP già pubblicati possono comparire fra i «da ripubblicare».** All'avvio la sezione nuova viene
   aggiunta ai vSOP che già esistono, anche nella loro versione pubblicata. Il **pubblico non cambia** — legge
   la fotografia della release — ma il sistema si accorge che la bozza e la copia pubblicata non coincidono
   più. È lo stesso segnale che diedero le Carte aeroportuali e le SID. Si chiude ripubblicando, quando vuoi.
2. **In un'unione vIPI + vSOP dello stesso scalo** dove avevi già nascosto le sezioni ripetute, «Regole piste»
   compare **due volte** finché non riapri la scheda delle **sezioni in comune** e confermi: la propone già
   spuntata, è un clic.

---

## Dopo il caricamento

1. **Il timbro** in `diagnostica/avvio-diagnostica.txt` dev'essere `1.24.0 · ed4c0f3`.
   ⚠️ Il timbro dice **quale versione è partita**, non che il sito funzioni.
2. **La Ricerca risponde**: `/services/vsop/search`, due lettere, la riga sotto il campo deve cambiare. È il
   controllo che conta, perché passa dal server.
3. **Un vSOP militare** (per esempio `/services/vsop/libb/mil?icao=LIBG`): fra «Piste» e «SID» c'è **«Regole
   piste»**. Su un campo senza regole scritte la tabella è vuota: è giusto.
4. **Col login, sull'editor di un campo solo militare senza vIPI** (LIBG, LIBN, LIMS): «Modifica», poi la
   sezione «Regole piste» ha i campi e il banco di prova. Scrivi una regola con una pista in DEP e guarda il
   documento: quella pista dev'essere marcata nelle Piste.
5. **Su un campo misto** (LIML, Pisa): la sezione c'è, i campi **no**, e c'è il rimando all'editor della vIPI.
6. **Una vIPI civile d'aeroporto** già pubblicata (per esempio LIBD): le regole si vedono come prima, e sulla
   riga **non** c'è più la pastiglia finché non ripubblichi. Non è un guasto: è il punto 2 qui sopra.
