# I NOTAM della divisione in testa ai documenti pubblici — carta (12 ottobre 2026)

> **Stato: 🧪 COSTRUITA e provata a schermo il 12 ottobre 2026** (filone Sito: S103 i documenti, S104 la vista live);
> aspetta che il committente la guardi. **Migrazione additiva** `NotamDellaDivisione` (SQLite e MySQL: una tabella
> nuova, `Notams`). Nessun `deploy/`.
> Richiesta del committente: «aggiungere, in cima ai documenti, in una sezione ben visibile, i NOTAM che interessano
> quell'aeroporto e che rimangano lì (nella versione pubblica del documento) fino a quando non scadono»; e poi, lo
> stesso giorno: «dato che sono importanti, finiscano anche nella pagina live».
>
> Dove sta: `Notam` (Domain: la riga) · `INotamSource`, `INotamStore` (Application: le due porte) · `AmbitoNotam`,
> `NotamInVista`, `NotamImportUseCase`, `INotamDeiDocumenti` (Application: chi riguarda, quando si mostra, il giro) ·
> `IvaoNotamClient`, `NotamImportHostedService` (Infrastructure/Ivao: la sorgente e il giro) · `EfNotamStore`,
> `NotamDeiDocumenti` (Infrastructure: l'archivio e chi lo legge) · `NotamBox` (Ui: il riquadro) · le quattro sedi
> dei documenti (`AeroportoPage`, `MilDocumentPage`, `AppnPage`, `AccVipiPage`) · la vista live (`LiveView.AmbitoNotam`
> e `LiveView.Notam`, i tre descrittori `*LiveStation`, `LiveViewService`, `LivePage`).

## 1. Che cosa dà la sorgente (misurato il 12 ottobre 2026)

`GET https://api.ivao.aero/v2/divisions/IT/notams/all` — **pubblico**, senza token: 4 voci, 3 KB sul filo, 0,16 s.

| NOTAM | Validità | Aeroporti | ACC |
|---|---|---|---|
| IT-B002/25 | 27 nov 2025 → 1 gen 2027 | LIRQ | LIRR |
| IT-A003/25 | 27 nov 2025 → 1 gen 2027 | LIPC LIPE LIPK LIPR | LIMM LIPP LIRR |
| IT-A002/26 | 16 apr 2026 → 31 dic 2026 | LIMG LIMJ | LIMM |
| ITW004/26 | 31 ago 2026, scaduto | LIBA LIBG LIBN LIBV | LIBB LIRO LIRR LIVK LIZZ |

Ogni voce: `id`, `name`, `startTime`, `endTime`, `description`, `military`, `specialAreaId`, `airports[]` (con
`icao`), `centers[]` (con `id`), `regionMap`, `regionMapPolygon`, `createdAt`, `updatedAt`. Il testo è in formato
ICAO (`Q) A) B) C) E)`), a righe separate da CRLF, fino a 700 caratteri da noi (1 700 nel mondo), con un indirizzo
dentro.

Cinque cose che decidono com'è fatta la funzione:

- 🔴 **La sorgente non toglie gli scaduti.** ITW004/26 era in elenco sei settimane dopo la scadenza. Chi è in vigore
  lo decidiamo noi, dalle date.
- 🔴 **`?now=true` sulla divisione è rotto**: perde il filtro e restituisce i 166 NOTAM attivi di tutto il mondo
  (481 KB). Non si usa. Sugli aeroporti e sui centri (`/v2/airports/{icao}/notams/all`) filtra bene, ma lì
  servirebbe una chiamata per documento.
- **Non tutti i nostri cominciano per `IT-`** (`ITW004/26`): si chiede la divisione, non si filtra per nome.
- **I «PERM» hanno una fine.** Il testo dice `C) PERM`, ma la sorgente gli dà il 31 dicembre o il 1° gennaio: quel
  giorno spariscono, se nessuno li rinnova su IVAO. Nel resto del mondo `endTime` (e anche `startTime`) può essere
  la stringa vuota: per noi è «senza fine» e «da sempre».
- `specialAreaId` arriva `null` da un indirizzo e `0` da un altro: non lo leggiamo.

Esistono anche gli elenchi paginati con token (`/v2/divisions/{id}/notams`): non servono.

**Non visto**: come arriva dalla sorgente un NOTAM con inizio futuro (quel giorno non ce n'erano: lo stato «In
arrivo» è provato con NOTAM inventati); se un NOTAM di un'altra divisione che nomina uno scalo italiano entra
nell'elenco IT (i 4 «per paese» coincidevano coi 4 «per divisione»).

## 2. Le decisioni del committente (12 ottobre 2026)

1. **vIPI d'aeroporto, vSOP e APP**: il NOTAM si mostra se riguarda **l'aeroporto**.
2. **vIPI di ACC**: se riguarda **l'ACC**. «Per gli APP nelle vIPI di ACC vale quello detto per gli APP»: una vIPI di
   ACC mostra anche i NOTAM dello scalo di ogni APP che contiene.
3. **vLOA: niente.** «Per le vLOA non prevediamo nulla per ora.» (La prima stesura le copriva con una regola del
   filone — «riguarda tutte e due le parti» — tolta lo stesso giorno. Un test dice che le due sedi delle vLOA non
   montano il riquadro, perché non lo si rimetta «per completezza».)
4. **Da sette giorni prima** dell'entrata in vigore, «con evidenziato da quando è valido e fino a quando».
5. Solo nella **versione pubblica**, fino alla scadenza.
6. **Anche nella vista live** (§6): una chip rossa «! NOTAM» fra il nome della postazione e il campo di ricerca, che
   si vede se un NOTAM riguarda quell'ente e, premuta, apre in fondo alla pagina il NOTAM «come nel documento».

## 3. Le scelte del filone

- **Lo scalo di un APP è la testa del suo codice** (`LIRQ_NN0_APP` → `LIRQ`). Un APP che serve più scali mostra i
  NOTAM del solo scalo che porta nel nome; quelli degli altri li vede chi apre la vIPI di quegli scali.
- **Una pagina unita ha un riquadro solo**, con i NOTAM degli scali di tutti i suoi documenti (`ScaliPerNotam`).
- **Un archivio nostro**, non una chiamata per pagina e non una cache di processo: i NOTAM durano mesi, e così un
  riavvio o una sorgente muta non li tolgono dai documenti. La tabella è lo **specchio** dell'elenco, scaduti
  compresi; aeroporti e centri sono due elenchi di codici in una colonna (le righe sono una manciata, si filtrano in
  memoria).
- **La scadenza la decide la data, alla lettura** (`NotamInVista.SiMostra`: inizio − 7 giorni ≤ adesso < fine). Il
  giro di import decide solo dopo quanto compare un NOTAM nuovo o corretto: **30 minuti**
  (`Ivao:NotamImportMinutes`, minimo 5). Davanti c'è la cache di 60 secondi delle letture anonime.
- **Un NOTAM tolto alla sorgente sparisce** al giro dopo (lo staff l'ha cancellato). Un elenco **vuoto** si prende
  per vero; un elenco **non letto** (non-2xx, 404 compreso; corpo che non è un elenco; voce senza `id` o `name`;
  data scritta che non si legge) fa fallire il giro e l'archivio resta com'era.
- **Senza spunta in Sorgenti**: la riga «NOTAM» c'è, in fondo, con stato e cadenza, ma non si può escludere —
  escluderla vorrebbe dire nascondere un NOTAM in vigore.
- **Il riquadro**: giallo, sotto la testata e sopra il corpo. Per ogni NOTAM una riga sempre in vista — numero,
  «In vigore» o «In arrivo», **dal … al …** in UTC, e la riga `E)` che dice di che cosa parla — e il testo intero
  dietro un `<details>` nativo (le pagine sono SSR statico). Si stampa, e in stampa si apre da sé come le sezioni
  (visto sul PDF: sta subito sotto l'intestazione di stampa, che porta già l'ora).
  Il testo si scrive come testo: solo gli indirizzi `http(s)://` diventano collegamenti.
- **Fuori dalla versione pubblica niente**: bozza e anteprima di release mostrano il documento, non l'oggi. Il
  riquadro non entra nella release, come il METAR.
- **L'API dei documenti** (`/vsop/api/v1/airports/{icao}`) **non porta i NOTAM**: è la proiezione della release.
  Chi li vuole li ha già da IVAO, senza chiave.

## 4. Non fatto

- I NOTAM degli **altri scali serviti da un APP** (§3).
- Il disegno dell'**area** (`regionMapPolygon`): arriva, non si salva.
- Un tasto «rileggi adesso» in Sorgenti: il giro è ogni mezz'ora.
- Un avviso a chi redige quando un NOTAM cambia un documento (oggi i tre in vigore dicono proprio «pagina 4 della
  vLOA: cambia la frequenza»): è materia di «Da rivedere», non di questo riquadro.
- Nella vista live: il **numero** dei NOTAM dentro la chip (sta nel suggerimento); un segnale quando ne arriva uno
  nuovo mentre la pagina è aperta (la chip compare al giro dopo, senza rumore).

## 5. Come si prova

- Regole: `NotamSuiDocumentiTests` (Application). Sorgente, archivio, giro e cadenza: `NotamImportTests`
  (Infrastructure), sul corpo vero del 12 ottobre. Riquadro e presidi sulle sedi e sui descrittori live:
  `NotamBoxTests` (Ui). Chip e pannello della vista live: `LivePageNotamTests` (Ui).
- Dal vivo, **senza chiamare IVAO**: una sorgente finta locale che serve solo `/v2/divisions/IT/notams/all` (il
  corpo vero più qualche NOTAM inventato sugli scali che hanno un documento) e 404 al resto; host con
  `Ivao__BaseUrl` e `Ivao__TokenEndpoint` puntati lì. Il giro parte 50 secondi dopo l'avvio; la riga «NOTAM» in
  `/services/vsop/admin/sources` diventa «Aggiornata». ⚠️ Le pagine pubbliche si leggono con un cookie qualunque
  (o dopo 60 secondi): l'identità di sviluppo è anonima, e la cache serve la copia di prima.
- Fatto il 12 ottobre su una copia del database di sviluppo: LIRN (aeroporto, uno in vigore e uno in arrivo; uno a
  nove giorni non c'è), LIBG (vSOP), LICC_APP, LIBB e LIMM (vIPI ACC, LIMM coi due NOTAM veri), bozza e vLOA senza
  riquadro, lo scaduto ITW004/26 da nessuna parte; vista live di LIRN_GND, LIRN_TWR, LIRN_US0_APP, LICC_APP,
  LIMM_WS2_CTR, LIBB_ES_CTR; la stampa in PDF.

## 6. Nella vista live (S104)

- **La regola è la stessa del documento di riferimento**, e la dichiara il descrittore del tipo di postazione
  (`LiveView.AmbitoNotam`): TWR, GND, DEL → lo scalo; APP → lo scalo del suo ente (anche quando l'APP vive nella
  vIPI di ACC); CTR → l'ACC più gli scali degli APP della sua vIPI, presi dagli **stessi blocchi** da cui li prende
  la pagina del documento. Una postazione e il suo documento mostrano gli stessi NOTAM.
- ⚠️ **Anche se il documento non è pubblicato.** Il committente ha scritto «il NOTAM si vede qui se c'è nel relativo
  documento pubblico»; il filone l'ha letto come «stessa regola», non come «solo se quel documento è online»: un
  controllore su una postazione senza documento un NOTAM sul suo scalo lo deve leggere lo stesso. Per un CTR senza
  vIPI pubblicata restano i soli NOTAM dell'ACC (gli APP li dice il documento). ▶ Se il committente lo vuole legato
  alla pubblicazione, è una riga in `LiveViewService` (`view.NoDocument`).
- **I NOTAM li legge il servizio**, non la pagina: `LiveViewService` compone la vista e poi chiede all'archivio con
  l'ambito dichiarato, in fila con le altre letture (il `DbContext` è quello del circuito). La pagina riceve
  `LiveView.Notam` già scelto e ordinato — e i finti dei test della pagina non hanno dovuto imparare niente.
- **La chip**: un tasto rosso «! NOTAM» (il punto esclamativo in un cerchio suo) subito dopo lo stato della
  postazione, prima del campo di ricerca; c'è solo se `Notam` non è vuoto. Il suggerimento dice quanti sono.
- **Il pannello**: in fondo alla pagina, sotto ogni ramo (scalo scelto, ricerca, settore). È lo stesso `NotamBox`
  dei documenti, coi NOTAM **già aperti** (`Aperti`): chi ha premuto la chip li vuole leggere. Nasce al clic — non
  è un riquadro nascosto col CSS — e la pagina ci scorre (`vipiScrollToId`). Lo stato è del circuito: sopravvive ai
  tick live, non a un ricarico. Premere di nuovo la chip lo chiude.
- La riga sotto il titolo del riquadro dice «Avvisi della divisione, in vigore o in arrivo»: prima diceva «che
  riguardano questo documento», che nella vista live non è vero.
