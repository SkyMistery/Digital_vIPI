# Controllare con un account dell'evento: la vista live col VID dell'evento — carta (1° ottobre 2026)

> **Stato: ✅ ESEGUITA il 1° ottobre 2026** sul ramo `fix/vid-evento` (filone Sito, S95). **Tre migrazioni additive**:
> `VidAccountEvento` (colonna `EventKits.VidEvento`), `AccountEventoInUso` (tabella, §4) e `VidSvuotaEvento` (colonna
> `EventKits.VidSvuotaUtc`, §3); SQLite e MySQL, Postgres le allinea `PostgresSchemaReconciler`.
> Nessun `deploy/`.
> Richiesta del committente: «durante gli eventi live si controlla da pc con account e VID ad hoc. Durante gli eventi un
> utente apre una finestra, inserisce il VID con cui si è connesso, e se è in una lista di VID validi per quell'evento e
> se è online gli dà la pagina live di quell'ente. Magari con un meccanismo temporizzato come quello dei profili».
>
> Dove sta: `EventKit.VidEvento` (Domain) · `EventKitRules.LeggiVid` (la lista) · `EventKitService` (salvare la lista,
> `AccountAsync`, `AccountInCorsoAsync`) · `AccountEventoRegistro` e `AccountEventoService` (Application, chi usa che
> cosa) · `LiveViewService.MyCallsign` (la vista live) · `EfAccountEventoTraccia` (audit) · `EventKitManager` (la lista,
> staff) · `ServicesHome` (la scheda) · `AccountEventoPage` + `AccountEventoPanel` (`/services/event/account`).

## 1. Com'era

La vista live trova la postazione cercando nel feed IVAO (`Details[].UserId`) il VID con cui si è **entrati nel sito**.
Durante un evento si controlla con un VID dato dall'organizzazione: il proprio non è connesso, e la vista resta vuota.
Chi non è staff di divisione vede solo la propria postazione, quindi non c'era nessun modo di arrivarci.

## 2. Le decisioni del committente (1° ottobre 2026)

1. **Chiunque abbia fatto il login** può usare un account dell'evento: il filtro è la lista più il controllo «online».
2. **Nessuna esclusiva**: più persone possono usare lo stesso VID dell'evento (cambio di pc, chi segue la posizione).
3. **La lista sta dentro l'evento dei profili** (`/services/event`): stessa finestra di date, si accende e si spegne
   insieme ai profili.
4. **La porta è una scheda in `/services`, accanto a quella dei profili**, non un riquadro nella vista live.

## 3. Come funziona

- Lo staff di divisione scrive i VID nel pannello dell'evento: uno o più per riga, con la postazione accanto se vuole
  («600100 LIRF_TWR»). Le righe che non cominciano con un VID si **rifiutano dicendo quali**.
- Mentre l'evento si vede (acceso e dentro le date) **e** la lista non è vuota, in `/services`, sotto «Evento in corso»,
  c'è la scheda **«Controlli con un account dell'evento?»** → `/services/event/account`.
- Lì si scrive il VID. Passa se: chi chiede è entrato · l'evento si vede · il VID è in lista · il VID è **online adesso**
  sul feed. Allora la vista live usa quel VID al posto del proprio, e la pagina porta subito alla postazione.
- **Fin quando**: alla fine dell'evento, o al più tardi **dodici ore** (evento senza data di fine). Se l'account
  dell'evento si disconnette, la vista torna al proprio VID **senza dimenticare la scelta**: alla riconnessione riprende.
  «Torna al mio VID» la cancella a mano.
- **Chi cambia l'evento manda fuori tutti**: salvare la testata (spento, date) o la lista svuota gli account in uso.
- **Audit**: una riga «EventAccount» per persona, VID dell'evento e giorno, con la postazione nei dettagli.
- **La lista si cancella da sola** (committente, 1 ottobre 2026): **sette giorni dopo la fine dell'evento**, o alla data
  che lo staff scrive accanto («Cancella la lista il», UTC; vuoto = la regola dei sette giorni). Si cancellano i soli VID
  e chi li stava usando; nome, date, profili e file restano. Lo fa `VidEventoPulizia` ogni ora e all'avvio. Un evento
  senza data di fine e senza data di cancellazione tiene la lista finché qualcuno non decide. Una data già passata si
  rifiuta al salvataggio.

## 4. Le scelte di costruzione

- **La lista è un campo di testo** (`VidEvento`, 4000 caratteri, al massimo 200 VID) e non una tabella: arriva
  dall'organizzazione già fatta e si incolla; si legge sempre con la stessa regola (`LeggiVid`).
- **Chi usa che cosa si legge dalla memoria e si salva anche nel database.** La vista live lo chiede a ogni giro del
  feed, quindi la risposta viene da `AccountEventoRegistro` (singleton: in Blazor Server lo scoped vive quanto il
  circuito, e una seconda scheda o un ricarico perderebbero la scelta). Ogni cambio si scrive anche nella tabella
  `AccountEventoInUso` (una riga per persona), e all'avvio `AccountEventoAvvio` rimette in piedi il registro da lì:
  **un riavvio durante l'evento non fa riscrivere il VID a nessuno** (committente, 1 ottobre 2026, dopo la prima
  versione che lo accettava come limite). Le voci scadute restano fuori; «Torna al mio VID» e chi cambia l'evento
  cancellano anche la copia nel database. Se il database non risponde, la vista va avanti dalla memoria e si perde solo
  la sopravvivenza al riavvio.
- **«Online adesso» si controlla sul feed**, che si aggiorna ogni 60 secondi: chi si è appena connesso può doverlo
  riprovare dopo un minuto, e la pagina lo dice. Senza questo controllo la lista basterebbe a guardare la postazione di
  chiunque vi compaia.
- La vista live per chi non è staff resta **una postazione sola**: quella del VID in uso. Niente selettore, niente
  pagine dello staff.

## 5. Limiti accettati

- Chi conosce un VID della lista e lo trova online vede la vista live di quella postazione: è sola lettura, e lo staff
  la vede già. L'audit dice chi l'ha fatto.

## 6. Reti

`AccountEventoTests` (Application: la data di cancellazione e la pulizia, lettura della lista, righe scartate, rifiuto senza scrivere, chi cambia l'evento
manda fuori, i cinque esiti, dodici ore, il riavvio e le voci scadute, la vista live col VID dell'evento e il ritorno
al proprio) ·
`AccountEventoTracciaTests` (Infrastructure: una riga al giorno con la postazione; l'archivio che si scrive, si toglie
e all'avvio rimette in piedi il registro) · `ServicesHomeTests` (la scheda solo
con evento e lista).
