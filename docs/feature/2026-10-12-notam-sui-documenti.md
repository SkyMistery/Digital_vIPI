# I NOTAM della divisione in testa ai documenti pubblici — carta (12 ottobre 2026)

> **Stato: ✅ ESEGUITA il 12 ottobre 2026** (filone Sito: S103 i documenti, S104 la vista live), **online in 1.61.0**
> dallo stesso giorno (§A153). **Migrazione additiva** `NotamDellaDivisione` (SQLite e MySQL: una tabella nuova,
> `Notams`). Nessun `deploy/`. 🟢 Dopo la 1.61.0, pronti da fondere e **non ancora in `main`**: il NOTAM dentro il
> blocco dell'APP nella vIPI di ACC (S105, §7), la sezione negli editor e quella sempre presente nella vista live
> (S106, §8 e §6).
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
   ACC mostra anche i NOTAM dello scalo di ogni APP che contiene — **dentro il blocco di quell'APP**, non in testa
   (§7: «se il blocco compare dentro l'APP a cui si riferisce è meglio»).
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
  riquadro, lo scaduto ITW004/26 da nessuna parte; vista live con la chip su LIRN_GND, LIRN_TWR, LICC_APP,
  LIMM_WS2_CTR, LIBB_ES_CTR e senza su LIML_TWR, LIBA_APP (nessun NOTAM in vigore) e su LIRR_TS_CTR, LIRR_NE_CTR,
  LIRN_US0_APP (vIPI di LIRR non pubblicata); la stampa in PDF.

## 6. Nella vista live (S104)

- **La regola è la stessa del documento di riferimento**, e la dichiara il descrittore del tipo di postazione
  (`LiveView.AmbitoNotam`): TWR, GND, DEL → lo scalo; APP col documento proprio → lo scalo del suo ente; APP che
  vive nella vIPI di ACC → i NOTAM del **suo blocco** in quella vIPI (§7); CTR → l'ACC più gli scali degli APP
  della sua vIPI, cioè il documento intero, testa e blocchi. Una postazione e il suo documento mostrano gli stessi
  NOTAM.
- 🔴 **Solo se il documento di riferimento è pubblico** (committente, 12 ottobre 2026: «solo a documento
  pubblico»). La prima stesura mostrava la chip anche senza documento online — il filone aveva letto «il NOTAM si
  vede qui se c'è nel relativo documento pubblico» come «stessa regola» — e il committente l'ha corretta: nella
  vista live il NOTAM si vede se lo si leggerebbe in testa a quel documento. La domanda «è pubblico?» è **una per
  tutti i tipi** (`LiveViewService.Pubblico`): una release in vigore e non nascosto, sulla chiave che il
  descrittore mette in `LiveView.ExtendedDoc` — lo scalo, il codice dell'ente, o l'ACC per la vIPI di ACC.
  ⚠️ `NoDocument` da solo non bastava: per un APP col documento proprio vuol dire «il documento non esiste», non
  «non è pubblicato». Conseguenza da sapere: un APP che vive nella vIPI di un ACC non pubblicata non ha la chip,
  nemmeno se il suo scalo ha la vIPI online (nella prova: `LIRN_US0_APP` con la vIPI di LIRR non pubblicata).
- **I NOTAM li legge il servizio**, non la pagina: `LiveViewService` compone la vista e poi chiede all'archivio con
  l'ambito dichiarato, in fila con le altre letture (il `DbContext` è quello del circuito). La pagina riceve
  `LiveView.Notam` già scelto e ordinato — e i finti dei test della pagina non hanno dovuto imparare niente.
- **La chip**: un tasto rosso «! NOTAM» (il punto esclamativo in un cerchio suo) subito dopo lo stato della
  postazione, prima del campo di ricerca; c'è solo se `Notam` non è vuoto. Il suggerimento dice quanti sono.
- **La sezione** (S106; nella 1.61.0 era un pannello che nasceva al clic e si chiudeva ripremendo la chip): il
  committente, 12 ottobre sera — «la sezione NOTAM deve essere sempre visibile in fondo al documento ma compressa.
  Se si clicca sul pulsante NOTAM si apre e la finestra scorre al NOTAM».
  - **C'è sempre**, in fondo alla pagina sotto ogni ramo (scalo scelto, ricerca, settore), fatta come le altre
    sezioni della vista (`details.acc-item`, col conto a destra), e **nasce chiusa**: niente `open` nel markup e
    niente `data-persist` — a ogni apertura della pagina riparte chiusa, a differenza delle altre. Senza NOTAM dice
    «Nessun NOTAM in vigore o in arrivo per questa postazione» (conto 0).
  - Dentro, lo stesso `NotamBox` dei documenti coi NOTAM **già aperti** (`Aperti`): chi ha aperto la sezione li
    vuole leggere, non aprirli uno per uno.
  - **La chip la apre e ci porta la finestra** (`vipiApriDettaglio` in `vipi-ui.js`). ⚠️ Apre e basta: non fa più
    da interruttore, perché la sezione è un `details` nativo che si chiude dalla sua intestazione, e il circuito
    non ne conosce lo stato — un interruttore sarebbe, una volta su due, un clic che non fa niente.
  - ⚠️ **Il margine sopra si misura, non si scrive**: in alto la pagina è coperta dalla barra del sito e dalla
    testata appiccicata, che va a capo e cambia altezza. Con un `scroll-margin` fisso (90px, poi 130) l'intestazione
    della sezione finiva sotto la testata, che a 1280px arriva a 152. La funzione somma `top` e altezza di ciò che
    resta appiccicato e lo scrive sull'elemento prima di scorrere.
- La riga sotto il titolo del riquadro dice «Avvisi della divisione, in vigore o in arrivo»: prima diceva «che
  riguardano questo documento», che nella vista live non è vero.

## 7. Nella vIPI di ACC: il NOTAM dentro il blocco dell'APP (S105)

Fino alla 1.61.0 la vIPI di un ACC aveva **un riquadro solo, in testa**, con dentro due cose insieme: i NOTAM
dell'ACC e quelli degli scali dei suoi APP. Il committente, 12 ottobre 2026: «se il blocco compare dentro l'APP a
cui si riferisce è meglio. E se compare lì allora darà anche nella pagina live di quell'ente».

- **In testa** restano i soli NOTAM che riguardano **l'ACC** (`AmbitoNotam.Acc(acc, nessuno scalo)`).
- **Dentro il blocco di ogni APP**, sotto il titolo del blocco e sopra le sue sezioni, i NOTAM che riguardano lo
  **scalo dei suoi membri** (`AmbitoNotam.BloccoApp`: la testa di ogni nominativo del blocco).
- ⚠️ Un NOTAM che nomina **sia l'ACC sia lo scalo** sta in **tutti e due** i posti, e non è un doppione da
  togliere: riguarda l'uno e l'altro (oggi IT-A002/26: Milano e Genova). Uno che nomina **solo lo scalo** sta solo
  nel blocco: in testa non si vede più, ed è voluto.
- **La vista live** segue il documento: la postazione di un APP remotizzato mostra i NOTAM **del suo blocco**
  (prima: del solo proprio scalo); un settore d'area continua a mostrare tutti quelli del documento. La regola del
  blocco è una sola (`AmbitoNotam.BloccoApp`), usata dalla pagina e dal descrittore live.
- ⚠️ **Il blocco, non l'APP**: se un blocco raggruppa APP di scali diversi, il suo riquadro porta i NOTAM di tutti
  quegli scali, e la vista live di ognuno dei suoi APP li mostra tutti. Nella copia di sviluppo c'è un blocco
  «Nuovo gruppo APP» con dentro due APP di Torino e uno di Genova: la chip di `LIMF_WN0_APP` conta il NOTAM di
  Genova. È la conseguenza di «se compare lì, dà anche nella live di quell'ente»; se i blocchi veri tengono
  insieme APP dello stesso scalo, non si nota.
- **Una lettura sola** per la testa e per tutti i blocchi (`INotamDeiDocumenti.PerAsync` con più ambiti), e un solo
  «adesso»: due blocchi della stessa pagina non possono dissentire su un NOTAM che scade in quell'istante.
- Provato sulla copia del database di sviluppo: vIPI di Milano con IT-A002/26 e IT-A003/25 in testa e IT-A002/26
  nel blocco dell'APP di Genova (dati veri); vIPI di Brindisi con un NOTAM inventato sul solo scalo di Bari nel
  blocco «Brindisi CS0» e non in testa; bozza senza riquadri; chip su `LIBD_CS0_APP` (1), `LIBB_ES_CTR` (2),
  `LIMJ_WS0_APP` (1), `LIMM_WS2_CTR` (2).

## 8. Negli editor: la sezione NOTAM si vede, come il meteo (S106)

Il committente, 12 ottobre sera: «la sezione NOTAM deve essere visibile nell'editor, come lo è la sezione
METAR/TAF».

- **Che cosa si vede**: una nota «NOTAM dal vivo» — in questo punto della versione pubblica compaiono da soli, non
  c'è niente da compilare, in bozza e in anteprima non si vedono — e sotto **il riquadro com'è nel documento
  pubblico**, coi NOTAM di adesso (`NotamNellEditor`). C'è **sempre**: senza NOTAM la nota dice «Adesso non ce n'è
  nessuno in vigore o in arrivo».
- **Dove**: dove sta nel documento. In testa alla colonna delle sezioni negli editor d'aeroporto, del vSOP e
  dell'APP (slot nuovo `BeforeSections` di `DocumentSectionsEditor`, lo specchio di `AfterSections`); nell'editor
  della vIPI di ACC in testa (i NOTAM dell'ACC) e dentro ogni blocco di gruppo APP (quelli del blocco, §7).
  L'ambito è lo stesso della pagina pubblica.
- ⚠️ **Non è una sezione del documento**, e la differenza col meteo è voluta: il meteo è una voce del catalogo
  delle sezioni (si sposta, si nasconde, ha la sua riga nell'indice), il NOTAM no — sta in testa per decisione del
  committente e non entra nella release. Per questo non compare nell'indice dell'editor e non ha comandi.
- **Scope proprio che aspetta** (`ScopeProprioCheAspetta`, lettura dentro `InFilaAsync`): legge dal database mentre
  l'editor intorno fa le sue letture, e chi lascia l'editor con la lettura in volo non deve portarsi via il
  `DbContext` (lo pretende `TerzaPortaTests`, che ha fermato la prima stesura).
- **Rilegge solo se cambia la chiave** (`apt:LIRN`, `acc:LIMM`, `blk:<membri>`): un editor si ridisegna a ogni
  tasto. Nell'editor della vIPI di ACC la chiave del blocco porta i suoi membri, così aggiungere o togliere un APP
  rilegge.
- Provato sulla copia del database di sviluppo: editor di LIRN (due NOTAM, sopra «METAR & TAF»), di LIML («nessuno»),
  del vSOP di LIBG, di LICC_APP, della vIPI di Milano (in testa i due veri, nel blocco APP IT-A002/26).
