# Pacchetto 1.22.0 — solo i file cambiati

> **Timbro:** `1.22.0 · f538b6f2` (11 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.21.1.** **19 file**.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟡 C'È UNA MIGRAZIONE, E SI CARICA COMUNQUE
>
> Questa consegna aggiunge **una colonna** alla tabella degli aeroporti (`Category`). La migrazione si applica
> **da sola** al primo avvio: **non c'è niente da fare a mano**.
>
> **Perché si può caricare adesso**, dentro la finestra in cui nessuno amministra il database: è una colonna
> **nuova** che nasce con un valore di riposo (`Civil`). Non tocca e non toglie niente di quel che c'è, non crea
> indici, non esegue SQL. Il presidio automatico delle migrazioni è **verde** senza deroghe.
>
> Subito dopo, **nello stesso avvio**, una passata riempie la colonna coi dati di oggi — vedi «Le quattro
> categorie» qui sotto. Nel registro d'avvio compare «Portati 34 aeroporti alla loro categoria».
>
> ## 🔴 I TRE FILE DA NON DIMENTICARE
>
> - **`Vipi.Infrastructure.MySqlMigrations.dll`** porta la colonna. Senza, il codice nuovo la cerca e la
>   tabella non l'ha.
> - **`Vipi.Hosting.dll`** fa la passata che la riempie. Senza, la colonna nasce ma resta tutta «Civile», e i
>   campi solo militari perdono la loro categoria finché non gira il giro notturno dell'anagrafica.
> - **`Vipi.Host.dll`** porta il **timbro**, anche se il suo codice non è cambiato.
>
> **Come si controlla la migrazione, da fuori e senza shell:** `admin/diagnostics`, riga **Schema**. Se dice
> **0**, la colonna c'è.
>
> ## 🟡 QUESTA VOLTA C'È ANCHE `wwwroot`
>
> `vipi-theme.css` è cambiato (le chip delle piste, la tendina delle categorie): viaggia **coi suoi `.br` e
> `.gz`** e **insieme** a `Vipi.Host.staticwebassets.endpoints.json`, o il sito chiede nomi che non esistono.
> Sono gli unici asset diversi: gli altri sono stati confrontati per impronta e sono identici.
>
> C'è anche **`en/Vipi.Ui.resources.dll`**: le frasi inglesi sono cambiate.
>
> ## ⚠️ L'ORDINE, QUESTA VOLTA
>
> Diversamente da 1.21.1, qui **gli assiemi cambiano firma fra loro** (il vecchio `Vipi.Ui` non parla col
> nuovo `Vipi.Application`). Quindi:
>
> 1. prima si caricano **tutti e 19** i file col nome finto;
> 2. poi le rinomine, **una di seguito all'altra e senza pause**, lasciando **`Vipi.Host.dll` per ultimo**.
>
> Se un riavvio cade a metà delle rinomine l'avvio può fallire con un assieme misto: si rimette da solo al
> riavvio successivo, a rinomine finite, come è già successo con 1.21.0. Il database non corre rischi in
> nessun ordine: la colonna nuova, se nasce prima del codice che la usa, il codice vecchio la ignora.

---

## Che cosa portano questi diciannove file

### 🟢 1. Piste: APP procedures, Patterns e Circling a chip

**Chiesto da te.** Le tre colonne dell'editor delle piste non sono più testo libero:

| Colonna | Chip |
|---|---|
| APP procedures | ILS · LOC · RNP · VOR · NDB · TAC · HTAC · PAR · SRA |
| Patterns | L · R · L JET · R JET |
| Circling | L · R — nessun chip = circling non ammesso |

Se ne accendono quante servono, un secondo clic spegne, e ogni clic salva come prima.

✅ **I documenti già pubblicati non cambiano**: il dato resta scritto come prima (`ILS, LOC, VOR`).
⚠️ **Le voci vecchie fuori elenco non si perdono**: «RNAV», o la «N» nel circling, compaiono come **chip
gialla con la ✕** e restano finché qualcuno non le toglie. Cliccare un altro chip non le tocca.

### 🟢 2. Le quattro categorie d'aeroporto

**Chiesto da te.** Un aeroporto è ora in una di quattro categorie, e la categoria decide quali documenti può
avere:

| Categoria | Chi la decide | vIPI | vSOP |
|---|---|---|---|
| Civile | la sorgente (nessuna presenza militare) | ✅ | ❌ |
| Solo militare | un amministratore | ❌ | ✅ |
| Civile con presenza militare | un amministratore | ✅ | ❌ |
| Militare con presenza civile | un amministratore | ✅ | ✅, in qualunque ordine |

- **Dove si sceglie**: pagina **Aeroporti**, sotto lo stato di ogni campo con presenza militare (serve
  «Inizia modifica»). Gli altri mostrano «Civile» e basta.
- **Dove si vede**: in **ogni documento** dello scalo — vIPI, avvicinamento, vSOP, e i loro editor.
- **La conferma**: se la categoria nuova esclude un documento che c'è già, prima di scrivere lo dice. Il
  documento **non viene toccato**; la **Diagnostica** lo segnala finché non lo nascondi o lo elimini.
- **La regola «prima la vIPI, poi il vSOP» non c'è più.**

**Al primo avvio i dati si riempiono da soli**, così:

| Oggi | Diventa |
|---|---|
| marcato «solo militare» | Solo militare |
| presenza militare, **con** un vSOP | Militare con presenza civile |
| presenza militare, senza vSOP | Civile con presenza militare |
| senza presenza militare | Civile |

⚠️ **Da fare a mano, una volta**: guardare i campi finiti in «Civile con presenza militare». Da oggi **non
possono avere un vSOP** finché non li sposti. Nell'archivio di sviluppo ci è finito anche **Ghedi**, che non
era mai stato marcato «solo militare»; **Pisa** probabilmente va in «Militare con presenza civile».

---

## Dopo il caricamento

1. **La riga `Schema` in `admin/diagnostics` dev'essere `0`.** È la prova che la colonna è nata. Se non è
   zero, `Vipi.Infrastructure.MySqlMigrations.dll` non è arrivato.
2. **Il timbro** in `diagnostica/avvio-diagnostica.txt` dev'essere `1.22.0 · f538b6f`.
   ⚠️ Il timbro dice **quale versione è partita**, non che il sito funzioni.
3. **La Ricerca risponde**: `/services/vsop/search`, due lettere, la riga sotto il campo deve cambiare. È il
   controllo che conta, perché passa dal server.
4. Col login, **pagina Aeroporti**: sui campi con presenza militare c'è la tendina della categoria, e i sei
   campi che erano «solo militare» lo sono ancora. Se fossero tutti «Civile con presenza militare», la passata
   d'avvio non è girata: `Vipi.Hosting.dll` non è arrivato.
5. Col login, **un editor aeroporto**, sezione Piste: le tre colonne sono chip.
