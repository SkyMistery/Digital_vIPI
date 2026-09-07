# Pacchetto 1.14.2 — solo i file cambiati

> **Timbro:** `1.14.2 · <COMMIT>` (7 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.14.1**, che è online da stamattina. **<N> file.**
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟢 NIENTE DATABASE
>
> **Nessuna migrazione, nessuna tabella e nessuna colonna nuova**: niente da concordare con chi amministra
> il database, nessuna copia di sicurezza, nessuna finestra da aspettare. Si carica quando volete, anche
> dentro la finestra cieca fino al 16.
>
> ## 🟢 E NIENTE `wwwroot`
>
> Nessun foglio di stile e nessuno script cambiano: la trappola dei file che «viaggiano insieme»
> **non si applica** a questo pacchetto.
>
> ## ⚠️ Ci sono FRASI nuove
>
> `en/Vipi.Ui.resources.dll` entra: sono le tre righe del pulsante e dell'avviso qui sotto. Senza quel file
> chi legge in inglese le vedrebbe in italiano.

---

## ✨ LA COSA DI QUESTO PACCHETTO: la lista «Da fare» smette di chiedere lavoro già fatto

### Il difetto, come lo si vedeva

Si pubblicava un documento, e la riga **«La copia pubblicata è indietro rispetto alla bozza»** restava lì.
Si ripubblicava, e restava lì lo stesso. Sembrava che la pubblicazione non funzionasse.

**Funzionava.** Il controllo che apre e chiude quelle righe girava **una volta ogni ventiquattr'ore**, e
nient'altro le toccava: fra il gesto e la prova che il gesto era servito potevano passare ventiquattro ore.

Su **LIBD** è successo esattamente questo: la riga è stata aperta il **6 settembre alle 19:27Z**, il
documento è stato ripubblicato il **7 alle 06:52**, e a metà mattina la riga era ancora lì — perché il
controllo, semplicemente, non era più passato.

### Che cosa cambia adesso

- **Pubblicare, programmare e annullare** rivalutano **subito** il documento toccato. Si esce dal pannello
  e la lista dice già la verità.
- ⚠️ **Programmare al ciclo entrante non chiede più di ripubblicare.** Una release programmata non è quella
  in vigore, quindi il controllo continuava a confrontare con la vecchia e a chiedere il lavoro — **fino al
  cambio di ciclo**, cioè per settimane, proprio a chi aveva fatto la cosa giusta. Ora, se la release
  programmata porta già quello che c'è nella bozza, la riga tace.
  ℹ️ E se dopo aver programmato si **cambia ancora** la bozza, la riga torna: quella programmata porterebbe
  un testo superato, ed è giusto sentirselo dire.

### Le due cose nuove in Diagnostica (`/services/vsop/admin/diagnostics`)

Nel riquadro **«Documenti da rivedere»**:

1. un pulsante **«Rilancia il controllo»**. Prima quel controllo si poteva solo **aspettare**: non c'era
   nessun modo di chiedergli di passare adesso.
2. un **avviso** se il controllo non passa da più di **36 ore**.

⚠️ **Il secondo non è decorativo, ed è la cosa da guardare per prima su questo sito.** Il controllo dorme
ventiquattr'ore fra una passata e l'altra, e **a ogni riavvio del processo il conto riparte da capo**. Qui
il processo viene riciclato spesso: se viene riciclato prima della scadenza, quella passata **non arriva
mai** — e le righe restano aperte per sempre, anche dopo che il lavoro è stato fatto. Se vedete l'avviso,
premete il pulsante: è la risposta.

---

## ✅ Che cosa guardare dopo aver caricato

Il timbro dice quale versione è partita, **non** che il sito funzioni: la prova che conta è sempre la
**Ricerca**, perché passa dal server.

Poi, per questo pacchetto, due controlli che chi carica può fare in un minuto — servono **occhi da
amministratore**, da fuori non si vedono:

1. **Diagnostica** → riquadro «Documenti da rivedere»: ci deve essere il pulsante **«Rilancia il
   controllo»**, e sotto la data dell'ultimo giro. **Premetelo**: deve rispondere con «Fatto: N documenti
   esaminati, X segnalazioni aperte, Y richiuse». ⚠️ Se prima di premerlo c'era l'avviso delle 36 ore,
   dopo deve sparire.
2. **Un documento qualsiasi** con la riga «da ripubblicare» in **«Da fare»** (`/services/vsop/tasks`):
   apritelo, pubblicatelo, tornate alla lista. **La riga non ci deve più essere**, senza ricaricare niente
   e senza aspettare.

ℹ️ **Le segnalazioni aperte oggi sono legittime.** Il catalogo delle sezioni è cresciuto (le «Carte
aeroportuali», e l'indice del SOD del 6 settembre), quindi ogni documento pubblicato è davvero indietro
finché non lo si ripubblica: il lavoro c'è. Quel che cambia con questo pacchetto è che **farlo si vede
subito**.
