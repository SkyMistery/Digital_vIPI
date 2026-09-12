# vAWOS — il quadro meteo in torre, e i minimi LVP nei documenti 🟢

> **Stato: FATTO** — dieci fette, tutte in `main`. Chiesto dal committente il 12 settembre 2026:
> *«Guarda il sistema in `Awos_base`. È possibile copiarlo nel nostro sito? Dalla visuale operativa, se sei in
> torre, oppure se da un APP apri il chip della torre, un tasto che apre quella pagina. Valuta anche se il
> meccanismo proposto lì può essere sistemato: non è stato fatto con nessun criterio di ingegneria.»*
>
> Risposta breve: **sì, e conviene** — ma portando **il quadro, non il codice**. Il prototipo è un ottimo
> capitolato grafico e un pessimo programma; le tre cose che gli mancano davvero (heading veri, TL vero, pista
> in uso vera) **il sito ce le ha già in archivio**.

**Le sei decisioni del committente**, prese il 12 settembre e recepite qui dentro:

1. **Pubblico**, col **Test METAR riservato allo staff**.
2. Rotta **fuori da `/vsop`**, e il servizio si chiama **vAWOS** — «v» come le vIPI, le vSOP e le vLOA: sono
   **operazioni virtuali**, e il nome lo deve dire prima che lo chieda qualcuno.
3. ~~Il **movimento** si tiene (interpolazione, §4.3).~~ → **Ribaltata la sera stessa** (§11): il vento è quello del bollettino e **non si muove**. «Non abbiamo modo di sapere il vento reale istantaneo nei pressi dell'aeroporto».
4. **Etichette in inglese, fisse.**
5. **LVP suggerito dal dato e configurabile per aeroporto** → e da qui nasce la seconda metà del lavoro: una
   **sezione LVP nelle vIPI e nei vSOP** con i minimi dello scalo (§5).
6. Il vAWOS è pubblico **solo per gli scali che hanno un documento pubblicato** — vIPI, o vSOP se lo scalo è
   solo militare (§6).

---

## 1. Che cos'è il prototipo, misurato

Tre file HTML autonomi, **4 928 righe**, zero dipendenze, zero server: `awos_selector.html` (un aeroporto
qualunque, una pista), `awos_2rwy.html` (due blocchi pista), `awos_lirf.html` (Fiumicino, tre blocchi e le due
configurazioni 16+25 / 34+25).

**Che cosa mette a schermo** (ed è la parte da tenere, tutta):

| Riquadro | Contenuto |
|---|---|
| MET REPORT | QNH grande, Air Temp, Dew Point, **TL**; VISIBILITY; WX (3 righe); CLOUD (3 righe); ATIS INFO (lettera ARR/DEP + orario); SUPPLEMENTARY INFO/TREND |
| RWY STRIP | le due testate, la pista disegnata, la freccia verde/ambra che indica il verso attivo, invertibile col clic |
| WIND ×2 | un pannello **per testata**: DIR, SPEED, Extremes (settore di variabilità), GUST min/max, **Cross**, **Tail** — con soglie a colore (>8 kt ambra, >15 kt rosso; tail > 0 rosso) |
| RVR | TDZ / MID / END |
| Barra | orologio UTC, LED di vitalità, Night/Day, **LVP**, Rwy Setting, Ext. Data, Local Rep. (ultimo METAR grezzo), ATIS Msg, **Test METAR** |

**Da dove prende i dati oggi**: METAR da `api.met.no` (**ogni 10 secondi**), ATIS dal whazzup IVAO pubblico
(~1 MB, cache 60 s **per scheda del browser**), heading pista da quattro file `.rw` scaricati da
`raw.githubusercontent.com` a ogni apertura, più una tabella di 126 aeroporti incorporata nel file.

### I difetti d'ingegneria, in ordine di gravità

1. 🔴 **Codice duplicato dentro lo stesso file.** In `awos_selector.html` **23 funzioni sono definite due
   volte** (`fillPanel`, `calcTL`, `buildStrip`, `refreshBlock`, `openRwySetting`, `loadRunwayHdg`,
   `toggleTheme`…). Vince sempre la seconda; la prima è codice morto che **sembra vivo** — e in un caso le due
   copie hanno **firme diverse** (`buildStrip(k)` per blocco contro `buildStrip()` senza argomenti). Il file
   stesso lo ammette in un commento: *«loadRunwayHdg è definita DUE volte in questo file… Vince la seconda»*.
   Metà del blocco `<script>` del selettore è il codice di Fiumicino a tre blocchi, incollato e inerte.
2. 🔴 **Timer che si accavallano.** `tick` è pianificato **due volte** a 1 s; `tickVitality` **due volte**, a
   450 ms *e* a 1 000 ms — il LED lampeggia a due ritmi contemporaneamente. Nessun timer viene mai fermato.
3. 🔴 **`refreshAll` ogni 10 secondi** interroga met.no. Un METAR si aggiorna ogni 30 minuti: sono **180
   chiamate per ogni bollettino nuovo**, moltiplicate per ogni scheda aperta, verso un servizio pubblico che
   non è nostro. È il difetto che, portato online sulla divisione, ci farebbe bloccare.
4. 🔴 **Un secondo parser METAR**, un secondo calcolo del TL, una seconda tabella di heading pista, una seconda
   fotografia del whazzup. Il sito ha già tutte e quattro le cose, migliori (§2).
5. ⚠️ **Dati inventati presentati come letture di sensore.** `oscDir`/`oscSpd`/`simulateVRB` fanno oscillare
   direzione e velocità con dei seni; le raffiche hanno un `gustMax` calcolato da un seno. E soprattutto:
   **senza gruppi RVR nel METAR il quadro scrive `P2000`**, cioè «oltre 2 000 m», che è un valore, non un
   buco. Questa è l'unica cosa del prototipo che **non** va portata così com'è (§4.3).
6. ⚠️ **Funzionalità morte.** `activateRwy()` è vuota ma è attaccata al clic delle due testate: un gesto che
   non fa niente. Il pannello «Ext. Data» promette il **QFE per pista** ma `_elevDb` non viene più riempito
   (leggeva `/runwayelev` del bridge, rimosso) → dice «UNAVAIL» per sempre. «Precipitazione ultimo minuto»:
   nessuna sorgente, «UNAVAIL» fisso.
7. ⚠️ **Approssimazioni dichiarate.** `RWY_HDG_FALLBACK` deduce l'heading dal **nome** della pista (17L → 170)
   quando l'aeroporto non è in tabella; `calcTL` usa quattro fasce di QNH cablate (`≥1013 → FL70`) più una
   correzione dalla TA, con `TA_BY_ICAO` che conosce **otto** aeroporti.
8. ⚠️ **Tre file al posto di uno.** `selector` e `2rwy` sono identici al **65 %** riga per riga. Aggiungere un
   aeroporto a quattro piste = un quarto file.
9. ⚠️ Il resto, ordinario ma vero: stato globale su `window`, modali costruite a mano con `cssText` e
   concatenazione di stringhe, `catch(e){}` muti, nessun test, nessuna build, etichette mescolate
   italiano/inglese, larghezza fissa, nessuna accessibilità.

### Che cosa invece è **giusto** e va copiato tale e quale

- L'**impaginazione**: è il quadro AWOS vero, e chi ha controllato in torre lo riconosce al primo colpo.
- I **due pannelli vento per testata** con Cross/Tail a soglie di colore: è l'informazione per cui un torrista
  guarda un AWOS.
- Il **tema notte** (`data-theme="night"`, colori desaturati, niente bianco pieno): è pensato per una torre al
  buio e per un secondo monitor.
- **Test METAR**: il campo che inietta un bollettino a mano. È esattamente la cultura di verifica di questo
  progetto (un caso di tempo brutto non si ordina in produzione: si scrive). Va portato, riservato allo staff.
- Il **LED di vitalità** e l'**invecchiamento** del dato: dicono che il quadro è vivo. Su una pagina lasciata
  aperta quattro ore è l'unica cosa che conta davvero.

---

## 2. Quel che il sito ha già, e che il prototipo non ha

Questa tabella è metà della proposta: quasi tutto il «motore» esiste, e portarlo dentro **toglie** codice al
posto di aggiungerne.

| Serve al quadro | Nel prototipo | Nel sito, oggi |
|---|---|---|
| METAR grezzo | met.no ogni 10 s, una sorgente | `IWeatherProvider` — catena **NOAA → IVAO → VATSIM**, cache per ICAO, deduplica delle richieste in volo, tetto d'attesa **per sorgente**, pastiglia di provenienza ([[metar-tre-sorgenti]]) |
| METAR decodificato | `parseMetar()` a regex, in pagina | `MetarParser` → `ParsedMetar`, coi codici resi in parole da `WxText` nelle due lingue ([[metar-decodificato-si-traduce-in-ui]]) |
| Heading delle testate | tabella cablata + 4 file scaricati da GitHub + deduzione dal nome | `AirportRunway.Bearing`, **dato di sorgente IVAO**, in archivio, per ogni scalo |
| Elevazione soglia (per il QFE) | mai caricata → riquadro morto | `AirportRunway.ThresholdElevationFt`, in archivio dal 30 agosto |
| TL | quattro fasce cablate + 8 TA | `AirportTransitionLevel` (QnhFrom/QnhTo → Level) **per aeroporto**, dall'AIP, già mostrato da `AirportQuickPanel.TlNow()` |
| Cross / Tail | `calcWind` in pagina | `RunwaySuggestion` — headwind/crosswind, già usati da vIPI e vSOP |
| Pista in uso | scelta a mano («Rwy Setting») | `AtisRunways.Leggi` (piste dall'ATIS di chi è in frequenza) **+** `RunwaySuggestion.EvaluateRules` (le regole piste, §CX/§CY) |
| ATIS lettera/orario | 1 MB di whazzup per scheda | `AtcPollingHostedService` scarica **già** il whazzup una volta al minuto, ATIS compreso |
| Chi presiede lo scalo | — | `AirportPresidencyService` |
| CAT dell'avvicinamento per pista | — | `AirportRunway.AppProcedures`, colonna editoriale già a chip ([[piste-colonne-a-chip]]) |
| Orologio UTC, avviso di simulazione, tema | scritti a mano | `data-utc-clock`, `<SimDisclaimer />`, i token di tema |
| Minimi LVP | soglia inesistente: il tasto colora e basta | **non c'è** → §5, ed è l'unica cosa da costruire davvero |

**Le tre bugie che spariscono da sole**: l'heading diventa quello vero (LIRF 16L è **159**, non 160), il TL
diventa quello dell'AIP di quello scalo, e la pista attiva la dice l'ATIS di chi sta controllando invece di un
menù da ricordarsi di girare.

---

## 3. Pre-flight (FEATURE-PROCESS)

**1. Modello — aggiungo un concetto o ne esiste già uno?**
Il quadro vAWOS **non aggiunge niente**: è una vista su dati che esistono (anagrafica scalo, meteo, rete).
Un solo tipo nuovo e in sola lettura, `AwosView`, composto da un servizio di `Vipi.Application` come `LiveView`.
I **minimi LVP** invece sono un concetto nuovo, e nel sito non esiste niente che li rappresenti (verificato:
«LVP» compare in **due commenti** e in nessun tipo). Nasce **una** entità, `AirportLvpMinima`, figlia
dell'aeroporto come `AirportTransitionLevel` e `AirportRunwayRule` — cioè **nell'anagrafica dello scalo, non nel
documento**. Fra sei mesi «dove si salvano i minimi LVP» ha **un** posto solo.
⚠️ Il punto delicato del quadro è **non scrivere un secondo `MetarParser`**: al parser di casa mancano tre
gruppi (settore di variabilità `dddVddd`, **RVR** `R16L/1400U`, visibilità verticale `VV///`). Si **estende
quello**, coi test che ha già.

**2. Dispatch — sto per switchare su un tipo che switcho già altrove?**
No, e va tenuto così. Il prototipo ha tre file perché ha un layout per numero di piste; qui la pagina rende
**N strisce**, accoppiando le testate per rotta opposta (±20°) prese dall'archivio. Fiumicino non ha codice
suo: ha tre righe di pista. Le due «CONFIG» di Fiumicino sono già esprimibili come **regole piste** (§CX), che
è dove vivono adesso.
La sezione LVP nei due profili **non è un ramo**: come le SID (§CV) e le regole piste (§CX), il dato sta
nell'anagrafica e i due documenti lo **leggono**. Si biforca solo la **scrittura**, e quella biforcazione
esiste già (`ScaloSenzaCivile`, §AS).

**3. Ingressi + verifica**
Ingressi: §6. Verifica: campo **Test METAR** (staff) + il banco `Weather__FallbackMetarUrl` già usato l'11–12
settembre per il METAR tradotto — un server finto di 10 righe che serve un bollettino deterministico
(`24018G32KT 200V280 0500 R16R/0350U R16L/P2000N -SHRA FG VV002 09/08 Q0998`), così si vedono a schermo
raffica, settore di variabilità, RVR con tendenza, soffitto e **LVP in vigore** senza aspettare il maltempo.

**4. Propagazione**
Additiva quasi ovunque. Le tre cose che vanno aggiornate **nello stesso giro**:
- `OnlineAtc` cresce di un campo (§4.4) → chi lo costruisce e chi lo legge;
- il commento in testa a `SectionProfile.AirportMil` dice *«le code per campo — **LVP di Pratica**… NON si
  seminano: sono sezioni libere»*. Con questa sezione **non è più vero**, e la frase va riscritta insieme al
  codice (è la falla che il gate chiama «record vero a metà»);
- `docs/feature/2026-08-27-vsop-militari.md` §209 cita la stessa cosa.

---

## 4. Il quadro vAWOS

### 4.1 Dove sta la pagina

**Rotta: `/services/vawos/{icao}`** — un **servizio**, fratello di `/services/vsop` e
`/services/profile-swapper`, non una pagina della documentazione ([[services-hub-and-tools]]: sotto `/services`
ogni figlio è un servizio, allo stesso livello). Card nell'hub, così è raggiungibile anche da chi non è
connesso a IVAO.
Il nome è **vAWOS** e non «AWOS»: è la stessa «v» di vIPI, vSOP, vLOA — **operazioni virtuali**, detto nel nome
prima che qualcuno lo scambi per un quadro vero. Titolo di pagina, card e tasti dicono tutti vAWOS.

**Layout dedicato**, non `SopLayout`: nessuna topbar, nessun breadcrumb, nessun menù. È una pagina da **secondo
monitor**, si apre in una scheda nuova e ci resta per tutto il turno. Tema **notte di default**, `noprint`
(come il riquadro METAR: un AWOS su carta è meteo scaduto).

### 4.2 Come si aggiorna — la decisione tecnica che conta

**Dati dal server ogni 60 s; movimento in pagina, in JavaScript.**

Non un'isola `InteractiveServer` che ridisegna a 1 Hz: dodici numeri che cambiano ogni secondo su un circuito
sono dodici diff al secondo **per ogni controllore collegato**, e soprattutto una pagina lasciata aperta quattro
ore su un secondo monitor è esattamente il caso in cui il circuito cade e non si riprende
([[riconnessione-circuito]]). Il quadro deve sopravvivere alla rete, non al circuito.

Quindi: la pagina è **SSR statica**, porta il primo `AwosView` già dentro l'HTML, e un modulo JS (~200 righe,
uno solo) fa tre cose: rilegge `GET /api/vawos/{icao}` ogni 60 s, interpola fra un dato e l'altro, tiene
l'orologio e il LED. Un solo `setInterval` per compito, tutti fermati sull'`unload`. Se una lettura fallisce il
quadro **invecchia visibilmente** (l'età del dato in barra ingiallisce, poi arrossisce) invece di mentire — che
è la cosa che il LED del prototipo promette e non fa.

L'endpoint risponde dalla cache del meteo (nessuna chiamata esterna per richiesta), non tocca il DbContext di
circuito, e applica **lo stesso cancello** della pagina (§6).

### 4.3 Il dato: che cosa si mostra, e che cosa si dichiara mancante

**Regola non negoziabile: si disegna solo ciò che il bollettino dice.** Quello che manca si scrive `///` —
che è già la convenzione del quadro reale e del prototipo per il vento non disponibile. In concreto:

- **RVR assente dal METAR → `///`**, mai `P2000`. È il difetto n. 5, ed è l'unico che sarebbe grave in
  produzione: un torrista che legge «RVR oltre 2 000» quando la macchina non l'ha misurato ha in mano un
  numero che nessuno ha detto.
- **QFE per pista**: ora si può, con `ThresholdElevationFt`. Solo per le soglie che l'elevazione ce l'hanno
  (arriva col re-import piste, che è per aeroporto e non automatico).
- **«Precipitazione ultimo minuto»**: si toglie il riquadro. Nessuna sorgente lo dà, e un «UNAVAIL» eterno è
  arredamento.
- **Provenienza del METAR** (pastiglia IVAO/VATSIM): come altrove, **da `DivisionStaff` in su**
  ([[metar-decodificato-si-traduce-in-ui]]).

🔴 **Il movimento è stato tolto** (§11). Era: la direzione spazzava il settore `200V280` con un seno di
dodici secondi, la velocità oscillava fra vento e raffica. Sembrava difendibile — «interpola dentro i valori
che il bollettino dichiara» — e non lo era: **il vento istantaneo non lo sappiamo**, e quel movimento era
fabbricato. Restano `<SimDisclaimer />` e l'ora del bollettino.

**Le etichette sono in inglese e fisse** (decisione 4): DIR, SPEED, GUST, CROSS, TAIL, RVR TDZ/MID/END, QNH,
TL, VISIBILITY, CLOUD, ATIS INFO. Sono sigle ICAO uguali in ogni torre del mondo, e tradurle sarebbe l'unico
modo di renderle meno leggibili. Le uniche parole vere — i codici di tempo presente — passano da `WxText`, che
esiste già e traduce nelle due lingue.

### 4.4 L'unica estensione a un tipo esistente

`OnlineAtc(Callsign, UserId, Name, Rating)` non porta l'ATIS, mentre `SourceAtcConnection` sì (le righe
arrivano già nella fotografia del minuto, e il poller le usa per le piste delle statistiche). Serve un campo:
la **lettera** e l'**orario** dell'ATIS, più le piste già lette da `AtisRunways`.
⚠️ Si estende `OnlineAtc` e la cache che lo costruisce, **non** si fa una seconda chiamata al whazzup: quella
del prototipo, moltiplicata per le schede aperte, sarebbe un megabyte al minuto per controllore.
La lettera si legge dal testo (`INFORMATION CHARLIE` → `C`) con la tabella NATO che il prototipo ha già.

### 4.5 La pista attiva

Nel prototipo si sceglie a mano e ci si dimentica. Qui è **derivata**, in ordine:

1. **l'ATIS** di chi presiede lo scalo, se c'è (`AtisRunways.Leggi`) — è la verità operativa del momento;
2. altrimenti **le regole piste** dello scalo (`RunwaySuggestion.EvaluateRules`, §CX/§CY);
3. altrimenti il **massimo headwind** (`RunwaySuggestion.Suggest`), com'è già nella vista rapida.

Sopra resta l'**override a mano** — la freccia si inverte col clic, come nel prototipo — ma la pagina scrive
**da dove viene** la scelta («from ATIS LIRF_TWR», «rule *Config 16*», «headwind», «manual»). Un quadro che dice
la pista senza dire chi l'ha decisa è la ragione per cui quella del prototipo va girata a mano.

---

## 5. La sezione LVP — nelle vIPI e nei vSOP

> Decisione 5, per esteso: *«mettiamo una sezione LVP nelle vIPI e nelle vSOP (se un aeroporto ha sia vIPI che
> vSOP, le vSOP si copiano le LVP dalle vIPI come per frequenze, SID e piste); in questa sezione si mettono le
> minime per andare in LVP, suggerendo di default quelle previste dalla regolamentazione».*

### 5.1 «Si copiano» è già risolto: non si copia, è lo stesso dato

Esattamente come per le SID (§CV) e le regole piste (§CX): i minimi **non stanno nel documento**, stanno
nell'**anagrafica dello scalo** (`AirportLvpMinima`, per `AirportId`). La vIPI civile è solo la **porta di
scrittura**. Quindi in lettura non c'è nessun ramo, nessuna copia, nessuna sincronizzazione da tenere allineata:
il vSOP legge dall'aeroporto, misto o solo militare che sia. Si biforca **solo la scrittura**, e quella
biforcazione esiste dal §AS:

| lo scalo | chi scrive i minimi |
|---|---|
| ha una vIPI civile (Civil, CivilWithMilitaryPresence, MilitaryWithCivilPresence) | l'editor della **vIPI**; nel vSOP la sezione è in **sola lettura** col rimando all'editor civile |
| è **solo militare** (`ScaloSenzaCivile`) | l'editor del **vSOP** |

La guardia del lock è quella che c'è (`AirportLockGuard`: pretende il lock della vIPI civile se esiste,
altrimenti quello del vSOP).

### 5.2 Il modello

Una riga per aeroporto, **0 o 1** — l'assenza è un fatto («questo scalo non ha minimi dichiarati»), non uno zero.

| campo | che cos'è |
|---|---|
| `Declared` | lo scalo dichiara di operare in LVP. Falso ⇒ la sezione dice «LVP not applicable» e il vAWOS spegne la pastiglia **dicendo perché** ([[tasto-spento-dice-perche]]) |
| `PrepRvrM`, `PrepCeilingFt` | soglie della **fase preparatoria** |
| `LvpRvrM`, `LvpCeilingFt` | soglie di **LVP in vigore** |
| `CancelRvrM`, `CancelCeilingFt` | soglie di **cancellazione** (con tendenza in miglioramento) |
| `Note` | testo libero: la coda per campo (chi le attiva, quali piste, CAT II/III, restrizioni al piazzale) |

⚠️ **I minimi restano d'aeroporto, non di pista.** La CAT dell'avvicinamento **per pista** c'è già ed è
un'altra colonna (`AirportRunway.AppProcedures`, resa a chip): duplicarla qui darebbe due posti dove cercare la
stessa cosa. Se un domani servisse una soglia per pista, si aggiunge lì.

⚠️ **Migrazione additiva** (una `CreateTable`). Fino al 16 settembre vale la
[[finestra-cieca-al-16-settembre]]: solo additivo, nessun `DropColumn`, nessun `Sql` grezzo. Questa lo rispetta.
Il pacchetto è **MINOR** (sezione nuova nel catalogo + migrazione), non PATCH.

### 5.3 I default «previsti dalla regolamentazione»

Il committente li vuole **suggeriti**. La forma che propongo — ed è una scelta di onestà, non di comodo:

- **il default si propone, non si scrive.** Il tasto «+ Declare LVP minima» nell'editor **precompila** i valori
  standard; finché nessuno salva, in archivio non c'è niente. Seminare d'ufficio settanta righe vorrebbe dire
  affermare, per settanta scali, una cosa che nessuno ha letto sull'AIP.
- **dove i minimi non sono dichiarati, il vAWOS usa lo standard e lo dice**: pastiglia `LVP (standard)`, non
  `LVP`. Una soglia standard è utile; una soglia standard spacciata per quella di Fiumicino no.

I valori standard proposti, **da confermare sull'AIP Italia AD 2 prima di metterli nel codice** — sono i
correnti in uso operativo, ma non li ho verificati documento alla mano e non li scrivo come fatti:

| fase | RVR | soffitto |
|---|---|---|
| preparazione | ≤ 800 m | ≤ 300 ft |
| LVP in vigore | < 550 m | < 200 ft |
| cancellazione | > 800 m | > 300 ft, con tendenza in miglioramento |

⚠️ Le soglie stanno in **una costante sola** (`LvpStandard`), citata dall'editor e dal ripiego del vAWOS: se
domani si scopre che uno scalo italiano usa 600 m, si cambia in un posto.

### 5.4 Dove va la sezione, nei due profili

- **vIPI civile** (`SectionProfile.Airport`): **subito dopo «Regole piste»**, cioè
  `weather → runwayrules → lvp → transition → frequencies → runways → sids → …`. Le due sezioni che si leggono
  **dal METAR** stanno vicine, e chi cerca «quando cambia il modo di operare» le trova insieme.
- **vSOP militare** (`SectionProfile.AirportMil`, dentro «Dati generali»): stessa posizione relativa, **dopo
  «Regole piste»** e prima delle SID; i fratelli successivi si rinumerano. Profondità 2, il limite è 3: nessun
  problema di `MaxDepth` ([[indice-del-sod]]).
- Titolo: **«LVP»** in italiano e in inglese — è una sigla, non si traduce. Sottotitolo umano nel catalogo:
  «Procedure in bassa visibilità» / «Low visibility procedures».

### 5.5 Le tre conseguenze da dire a chi carica

1. **Pratica di Mare (LIRE) ha già una sezione LVP libera** (lo dicono il commento del catalogo e la carta dei
   vSOP militari): quando arriva quella strutturata, allo scalo ne risultano **due**. Va **unita a mano** — il
   testo della libera diventa la `Note` dei minimi — ed è un lavoro di dati, non di codice.
2. **La passata d'avvio** (`AddMissingCatalogSectionsAsync`) semina la sezione anche nell'ultima versione
   **pubblicata**: il pubblico non cambia (legge la fotografia della release), ma i documenti già pubblicati
   possono comparire fra i **«da ripubblicare»**. È successo identico con le SID e con le regole piste.
3. **Unioni vIPI + vSOP già «ripulite»**: la sezione nuova nasce **visibile**, quindi «LVP» compare due volte
   finché non si ripassa dalla scheda delle sezioni in comune (che la propone già spuntata).

### 5.6 Come il vAWOS usa i minimi

Il quadro confronta i minimi con il METAR corrente:

- **RVR**: il minimo dei gruppi RVR se ci sono; altrimenti la **visibilità** — e lo **dichiara** («from VIS»),
  perché non sono la stessa misura;
- **soffitto**: la base più bassa fra BKN e OVC, oppure la visibilità verticale (`VV002` → 200 ft).

Stati: **NIL** (sopra le soglie) · **PREP** (ambra) · **LVP** (arancio, in vigore) · e, mentre è in vigore, il
suggerimento di cancellazione quando il dato risale sopra le soglie.

🔴 **Suggerisce, non decide.** L'interruttore manuale resta, e la pastiglia dice sempre **da dove viene** lo
stato: «suggested by LIRF minima», «standard minima», «manual». L'attivazione LVP è una procedura d'aeroporto,
non una formula — e un quadro che la dichiarasse da solo direbbe una cosa che nessun controllore ha deciso.

⚠️ **Il vAWOS legge i minimi VIVI**, non quelli della release. Non è un documento e non ha una release: è uno
strumento, come il vento. La stessa asimmetria già accettata per la vista rapida e l'elenco aeroporti. Dove il
documento pubblicato e il quadro divergessero, **l'autorità è il documento**, e va detto nella Guida.

---

## 6. Come ci si arriva, e chi può entrare

### Gli ingressi

| Da dove | Che cosa si aggiunge |
|---|---|
| **Vista live, sei in torre** (`AirportLiveStation`: TWR/ITWR/GND/DEL → `_view.AirportIcao` valorizzato) | un tasto **vAWOS** nella testata, accanto a «Documento esteso», `target="_blank"` |
| **Vista live da APP/ACC, chip di uno scalo** | il chip apre già `AirportQuickPanel`: il tasto **vAWOS** va nella sua testata, accanto a «Apri vIPI completa →». Vale in un colpo solo per la vista live **e** per la pagina dell'aeroporto, che monta lo stesso componente |
| **Pagina aeroporto** `/services/vsop/{acc}/airports?icao=` | stesso tasto, gratis per la riga sopra |
| **Hub** `/services` | una card, per aprirlo senza passare dal live |

Dentro il vAWOS, un selettore di aeroporto per cambiare scalo senza tornare indietro — è la cosa migliore di
`awos_selector.html`, e va tenuta. **L'elenco del selettore è lo stesso del cancello qui sotto**: mostrare uno
scalo che poi rifiuta di aprirsi sarebbe un gesto che non fa niente ([[gesto-piu-corto]]).

### Il cancello (decisione 6)

**Pubblico, ma solo per gli scali che hanno un documento pubblicato.** In pratica è la regola che il sito ha
già: `HasEffectiveRelease && !IsHidden` sul documento dello scalo ([[public-list-visibility-gate]]), applicata
secondo la **categoria** ([[categorie-aeroporto]]):

| categoria dello scalo | il vAWOS è pubblico se… |
|---|---|
| `Civil`, `CivilWithMilitaryPresence` | la **vIPI** è pubblicata |
| `MilitaryOnly` | il **vSOP** è pubblicato |
| `MilitaryWithCivilPresence` | **almeno uno dei due** |

È esattamente la regola `AeroportiDellAcc` («scali con almeno un documento pubblico») già scritta nella Ui:
si riusa quella, non se ne scrive una seconda.
Uno scalo senza documenti pubblicati → pagina «nessun documento pubblicato per LIXX», col rimando all'elenco.
**Un Editor lo apre lo stesso**, con una fascia che dice che il pubblico non lo vede: è la stessa logica di
`?as=draft`, e senza di essa non si potrebbe provare un vAWOS prima di pubblicare (il catch-22 del gate).

**Il Test METAR è riservato allo staff** (decisione 1): inietta dati falsi, e su una pagina pubblica dev'essere
impossibile per sbaglio. Il pulsante non c'è proprio, e l'endpoint rifiuta il parametro.

---

## 7. Le fette, in ordine

Ogni fetta è verticale, chiude con build verde e si può fermare lì.

| # | Fetta | Che cosa si vede alla fine |
|---|---|---|
| 1 | `MetarParser` cresce di tre gruppi (`dddVddd`, RVR, `VV///`), coi test | niente a schermo; la suite copre i tre gruppi nuovi |
| 2 | `AwosView` + `IAwosService` in Application: scalo + piste + TL/TA + METAR + pista attiva, **e il cancello** | test d'unità sul servizio, dati finti |
| 3 | Pagina + layout + CSS del quadro, **statica**, un aeroporto, senza movimento | il vAWOS a schermo su LIRF, dati veri, fermi |
| 4 | Endpoint JSON + modulo JS: aggiornamento, orologio, LED, invecchiamento | il quadro vive e sopravvive a un guasto di rete |
| 5 | N strisce pista dall'archivio + freccia + override | funziona su LIRF (3), LIMC (2), LIRA (1) senza codice per scalo |
| 6 | `OnlineAtc` porta l'ATIS → lettera, orario, pista in uso derivata | «ATIS INFO C — 12:20z», pista attiva senza toccare niente |
| 7 | **LVP: entità + migrazione + sezione nei due cataloghi + viewer + editor + default standard** | la sezione LVP nelle vIPI e nei vSOP, scritta da una porta sola |
| 8 | Il vAWOS consuma i minimi: stato NIL/PREP/LVP, provenienza dichiarata | la fascia RVR si accende quando deve, e dice perché |
| 9 | I quattro ingressi (§6) | ci si arriva dalla torre e dal chip |
| 10 | Ext. Data (QFE per pista), Test METAR allo staff | il quadro completo |

**Ordine di grandezza**: 1–4 sono il grosso del quadro (c'è e vive); **7 è il lavoro più pesante** ed è l'unico
con una migrazione; 5–6 e 8–10 sono completamento.
**Due consegne separate** hanno senso: *(a)* il vAWOS senza LVP — PATCH/MINOR, nessuna migrazione; *(b)* la
sezione LVP + il consumo nel quadro — MINOR con migrazione. Così la parte che tocca i documenti di tutti non
viaggia insieme alla parte che non li tocca.

**Il codice del prototipo che sopravvive letteralmente**: il CSS del tema notte, la geometria dei riquadri, la
tabella NATO delle lettere ATIS, le soglie di colore di cross/tail. ⚠️ **Non** le sue oscillazioni: vedi §11. Il resto — le 4 928 righe di JavaScript —
non si porta: le tre cose che fa (parsare il METAR, calcolare il vento sulle testate, decidere il TL) qui
esistono già, fatte meglio e con i test attorno.

---

## 8. Le tre decisioni piccole, chiuse il 12 settembre

1. **I valori standard**: confermati dal committente. Sono in `LvpStandard`, un posto solo.
2. **`Declared = false`**: la sezione si **mostra** e scrive «LVP not applicable». «Non applicabile» è
   un'informazione operativa; il silenzio non lo sarebbe.
3. **LIRE**: la unisce a mano il committente (la sezione libera esistente → la nota dei minimi).

---

## 9. Che cosa è stato costruito, e come si è verificato

**Dieci commit, uno per fetta**, dal parser al quadro completo. Nessun modello gemello, una sola migrazione
(additiva). Suite intera verde sui due TFM (Application 2464, Ui 1512, Infrastructure 1373, E2E 319) e
`dotnet build Vipi.slnx -c Release --no-incremental` a **zero avvisi**.

### 🔴 I sette difetti trovati GUIDANDO L'APP, non dai test

Sono la ragione per cui il runbook pretende la verifica dal vivo: la suite era verde per tutti e sette.

| | che cosa si vedeva | perché |
|---|---|---|
| 1 | **16L accoppiata con 34L** invece che con 34R (dal dato vero di Fiumicino) | l'accoppiamento guardava solo la rotta: girandosi, la sinistra diventa destra |
| 2 | tutti i pannelli vento a **`--` per un minuto** | il JS scriveva sopra i valori del primo disegno prima della sua prima lettura |
| 3 | la riga **CROSS/TAIL tagliata** su tre piste | `container-type: size` pretende un'altezza definita, e 46vh diviso tre non bastava |
| 4 | WX e nubi tornavano ai **codici grezzi** dopo un minuto | le parole hanno una lingua, e il JavaScript non ce l'ha: ora le compone il server |
| 5 | il pannello del METAR di prova **nasceva aperto** | `display:flex` vince su `[hidden]`, e la regola non c'era |
| 6 | su una pista sola i pannelli erano **stirati** per tutta l'altezza | crescono con la larghezza, non con l'altezza |
| 7 | col vento calmo la casella diceva **«360»** | `00000KT` è *calmo*, non «da nord a zero nodi» |

### Le prove, con i dati veri

- **METAR reale**: Fiumicino, tre strisce di pista dall'anagrafica, rotte 68/248 e 161/341 (non 70/250 e
  160/340), TL FL70 dalla tabella dello scalo, QFE per soglia dalle elevazioni IVAO.
- **ATIS reale**: `LICC_TWR` informazione **ECHO** delle 11:36z → pista **26** marcata verde e
  «RWY IN USE: 26 · from ATIS LICC_TWR»; `LIEA_TWR` informazione **INDIA** → pista 20.
- **Tempo brutto su richiesta**, col Test METAR: `24018G32KT 200V280 0450 R07/0350U FG VV002 Q0998` →
  visibilità 450 m, `VV 200 FT`, RVR `350U`, la direzione che spazza fra 200 e 280, la velocità fra 18 e 32,
  coda 31 kt in rosso sulla 07, e la fascia RVR arancione con la pastiglia **LVP**.
- **La sezione LVP** compare nell'indice e nel corpo della **bozza** di LIBD (tabella Preparation/In force/
  Cancellation) e nel vSOP militare di **LIBG**; l'editor la dichiara col tasto e salva a ogni gesto.
- **Il cancello**, guidato con un'identità a **basso livello** (VID 123456, posizione `XX-ZZ9`):
  `/services/vawos/api/LIRF` → **404 `NonPubblicato`**, LIBD → 200; la pagina di LIRF spiega perché e elenca
  gli scali che si aprono; il tasto **Test METAR non c'è**, e il parametro `?test=` viene **ignorato** (torna
  il METAR vero, Q1018, non quello iniettato).
- **Tema giorno**, **telefono a 400px** e tre piste: nessun taglio, nessuno sfondamento orizzontale, zero
  errori di console e zero 4xx in tutte le passate.

### ⚠️ Le due cose attese, da dire a chi carica

1. **MINOR con migrazione** (`AirportLvpMinima`, una `CreateTable` sui due provider).
2. La passata d'avvio ha seminato **17 sezioni** nei documenti già scritti — e le semina anche nell'ultima
   versione **pubblicata**: il pubblico non cambia (legge la fotografia della release), ma i documenti già
   pubblicati possono comparire fra i **«da ripubblicare»**. La sezione arriva in pubblico alla prossima
   pubblicazione: misurato su LIBD, dove la bozza ha «LVP» e la pubblica ancora no.
3. In un'unione vIPI + vSOP già «ripulita», «LVP» si vede **due volte** finché non si ripassa dalla scheda
   delle sezioni in comune (che la propone già spuntata).

---

**Riferimenti**: [[metar-tre-sorgenti]] · [[metar-decodificato-si-traduce-in-ui]] · [[live-view-design]] ·
[[services-hub-and-tools]] · [[categorie-aeroporto]] · [[public-list-visibility-gate]] ·
[[catalogo-sezioni-fonte-unica]] · [[indice-del-sod]] · [[regole-piste-giorno-operativo]] ·
[[piste-colonne-a-chip]] · [[quote-transizione-colonna-destra]] · [[riconnessione-circuito]] ·
[[avviso-di-simulazione]] · [[tasto-spento-dice-perche]] · [[finestra-cieca-al-16-settembre]]

---

## 10. La revisione indipendente (12 settembre 2026, sera) — dieci rilievi, tutti chiusi

Riletto come se il codice l'avesse scritto un altro, cercando i guasti invece delle conferme. **Due difetti
riprodotti in un browser vero**, non dedotti; gli altri otto sono debiti di struttura o di copertura.

### 🔴 1-2. Il quadro moriva con la navigazione «enhanced», e i suoi timer no

Una causa sola: il modulo dava per buono che **un caricamento = un pannello**.

| | misurato prima | misurato dopo |
|---|---|---|
| hub → `/services/vawos` → clic su un ICAO | **zero** chiamate all'API, età ferma a «—», **orologio che scorreva** | una chiamata, età «1s» |
| entrata da un'altra pagina del sito | lo `<script>` per percorso **non viene eseguito** dalla navigazione enhanced | il modulo arriva e si aggancia |
| uscita verso `/services` | una chiamata a `/services/vawos/api/LIRN` **due minuti dopo** | zero chiamate in 66 s |

Il primo caso è il **percorso di scoperta normale** — la card dell'hub porta lì — e il quadro restava fermo
*con l'aria di essere vivo*: esattamente la bugia che l'invecchiamento del dato doveva impedire.

**La cura**: il modulo entra nella macchina dei **moduli pigri** di `vipi-boot.js`, che il progetto ha già e
che sceglie **sul DOM, non sull'indirizzo** — è scritto nella nota in testa a quella lista, e non l'avevo
seguita. `vipiInitAwos` si chiama a ogni navigazione: se il quadro c'è si (ri)aggancia all'aeroporto che
trova, se non c'è **spegne i timer**.

### 🟠 3. Le soglie di cancellazione erano inerti

Si scrivevano, si mostravano, si precompilavano — e nessuno le leggeva. La carta (§5.6) prometteva il
suggerimento di cancellazione: non era stato consegnato.

Ora c'è l'**isteresi** vera: `LvpValutatore.Valuta(..., giaInVigore)`. Sotto la soglia d'ingresso si entra;
risaliti fra ingresso e cancellazione **si resta in vigore** (o si sfarfalla a ogni metro di RVR); sopra la
cancellazione si propone di uscire. ⚠️ Per **entrare** basta una misura bassa («o»), per **uscire** devono
essere risalite **tutt'e due** («e») — scritto con un «o» si proporrebbe di cancellare col soffitto a 100 ft.
⚠️ La memoria ce l'ha solo il **quadro** e gliela rimanda al server (`?inforce=`): un documento si rende da
capo ogni volta e quello stato non lo mostra mai, che è la risposta giusta a una domanda che non può porsi.
Aggiunta anche la validazione che mancava: la cancellazione non può stare **sotto** la preparazione.

### 🟠 4-5. Un endpoint pubblico senza tetto, e due letture per pagina

`/services/vawos/api/{icao}` è anonimo e costa due interrogazioni: ora ha il **limitatore** degli altri due
endpoint pubblici (10/min per IP, 600 globali). Provato: dieci `200` poi `429`, da due client diversi.
E `ElencoAsync` + `BuildAsync` leggevano **due volte** l'elenco completo dei documenti a ogni resa: ora la
lettura è memoizzata nello **scope della richiesta** — non è una cache con un problema di freschezza, è la
stessa domanda posta due volte nello stesso istante.

### 🟡 6. Cinque coppie di logica scritte due volte, in C# e in JavaScript

Riga «RWY IN USE», pastiglia LVP (testo, classe, spiegazione) e le tre celle RVR: ora le compone
`AwosTesto.Scritte`, **un posto solo**, che serve la pagina al primo disegno e l'endpoint a ogni giro.
Una di quelle coppie era **già divergita** durante lo sviluppo (WX ai codici grezzi dopo un minuto).
⚠️ Restano fuori le sole due che si **animano** — direzione/velocità e traverso/coda — e non è una svista:
sono diverse a ogni fotogramma, e il server non può scriverle.

### 🟡 7-8. Un null-forgiving su una pagina pubblica, e il cancello senza rete

`View.Minimi!` stava su un dato che arriva **deserializzato** da uno snapshot di release: ora la guardia è
sul nullo, e nel dubbio il documento dice «non dichiarati» invece di non aprirsi.
Il cancello era provato **solo dal vivo**: le sue tre decisioni (chi entra, che cosa si elenca, quale ATIS
conta) sono uscite dal servizio in `AwosGate`, **pure**, e hanno **17 test** — compresi il documento
nascosto, la release non effettiva, e il callsign di un altro scalo che non deve parlare per questo.

### ⚪ 9-10. Le due minori

- Un ATIS che dice «arrival runway 16L 16R» dichiara **due** piste, e se ne marcava una: `AwosActive.Dep/Arr`
  sono ora **elenchi**. ⚠️ Il cambio ha rotto `eAttiva` nel JavaScript (trattava ancora stringhe) — trovato
  rileggendo, sarebbe esploso a ogni giro d'animazione.
- La freccia col vento calmo era **un gesto che non faceva niente**: non c'era una pista attiva da invertire.
  Ora il clic **sceglie**, con un giro a tre stati (derivata → sinistra → destra → derivata) e la riga sotto
  che scrive «manual».

### Che cosa regge, e resta vero

Cancello a basso livello (404 sul non pubblicato, `?test=` ignorato a chi non è staff), nessuna copia del
dato LVP fra vIPI e vSOP, accoppiamento piste col lato speculare, migrazione additiva su due provider,
nessun `innerHTML` con dati esterni. Suite verde sui due TFM (2488 + 1512 + 1373 + 319) e build Release a
zero avvisi.


---

## 11. Il vento non si anima (12 settembre 2026, sera) — decisione 3 ribaltata

Il committente ha aperto il quadro e ha chiesto: *«il vento come lo generi? la direzione cambia ogni
secondo»*. La risposta era: non lo genero, lo **interpolo** — la direzione spazza il settore `dddVddd` con un
seno di dodici secondi, la velocità oscilla fra vento e raffica. Poi la decisione, in una riga che chiude la
questione:

> **«È meglio riferirsi al METAR e basta, non abbiamo modo di sapere il vento reale istantaneo nei pressi
> dell'aeroporto.»**

Ha ragione, e la distinzione su cui si reggeva §4.3 — «interpolare dentro i valori dichiarati non è
inventare» — **non regge**: il bollettino dichiara un *intervallo*, non una successione di istanti. Il valore
che il quadro mostrava a ogni fotogramma non l'aveva misurato nessuno.

### E l'animazione si portava dietro tre difetti che nessuno aveva ancora visto

1. 🔴 **Traverso e coda si calcolavano sulla direzione inventata.** I due numeri che un controllore usa
   davvero oscillavano da soli, attraversando avanti e indietro le soglie ambra e rossa. Nemmeno il prototipo
   lo faceva: mostrava la spazzata ma calcolava le componenti sulla direzione **base**.
2. 🔴 **La direzione misurata spariva.** Con `24018G32KT 200V280` la casella DIR non mostrava mai **240**:
   l'unico valore che il bollettino afferma era l'unico che non si poteva leggere.
3. ⚠️ Un seno che tocca sempre esattamente i due estremi, quattro volte al secondo, **si legge come un
   generatore casuale** — ed è esattamente l'impressione che ha avuto chi l'ha aperto.

### Com'è adesso

DIR, SPEED, CROSS e TAIL vengono dal bollettino e stanno **fermi** finché non ne arriva un altro. Il settore
di variabilità e la raffica hanno già le loro caselle — **EXTREMES** e **GUST** — ed è lì che
quell'informazione appartiene, senza fingere una misura che nessuno ha preso. Il tasto **MOV** è uscito con
l'animazione che accendeva.

⚠️ **Il quadro resta vivo**, e la differenza è tutta qui: l'orologio UTC scorre, il LED di vitalità gira e
l'età del dato invecchia — sono tre cose che parlano del **pannello**, non del tempo. A muoversi è quel che
sappiamo che si muove.

Provato a schermo con `24018G32KT 200V280`: otto campioni in sette secondi, **identici** — DIR 240, SPEED 18,
EXTREMES 200/280, GUST 32, e sulla 07 coda 18 kt in rosso, ferma.

### ✅ E la stessa regola ha chiuso **RVR MID**

La fascia RVR aveva tre celle — TDZ, MID, END — perché il quadro vero ha tre sensori lungo la pista. **Il
METAR non li ha**: dà un valore **per testata**. Quel «MID» era la **media delle due**, cioè un numero che
nessuno ha misurato sotto il nome di un sensore che non abbiamo. Lo stesso difetto del vento, scritto con
un'altra formula.

Deciso dal committente: **rinominare le celle con le testate a cui appartengono**. Da cui, per forza, il
numero delle celle segue la pista invece di essere tre fisse:

| prima | adesso |
|---|---|
| `RVR TDZ` · `RVR MID` · `RVR END` | `RVR 07` · `RVR 25` |
| MID = media di TDZ e END | la cella non esiste: non c'è una testata a cui appartenga |
| END = l'RVR dell'altra testata, sotto il nome di un sensore di questa | ogni cella porta l'ident di cui parla |

⚠️ Una testata di cui il bollettino non dice l'RVR tiene la sua cella a `///`: **la pista c'è, il valore no**,
e sono due fatti diversi. Una testata spaiata ne ha una sola.

Provato a schermo: su LIRF, `R16R/0350U R16L/P2000N R25/M0050D` → `RVR 16R 350U`, `RVR 16L P2000N`,
`RVR 25 M50D`, e `///` sulle tre testate che il bollettino non nomina.


---

## 12. L'indice della vIPI civile cambia (12 settembre 2026, sera)

Deciso dal committente dopo aver letto §5.4: **nella vIPI civile** le regole piste scendono **dentro
«Piste»** e le LVP vanno **sotto «Procedure generali»** — sotto nel senso di *dopo*, sorella e non figlia.

| | prima | adesso |
|---|---|---|
| `runwayrules` | radice, la 2ª del documento | **figlia** di `runways` |
| `lvp` | radice, la 3ª | radice, **subito dopo** `operationaltechnique` |

```
1 METAR & TAF · 2 Quote di transizione · 3 Frequenze
4 Piste
   └ Regole piste
5 SID · 6 Procedure generali · 7 LVP · 8 Carte aeroportuali · 9 Validità e revisione
```

⚠️ **Il vSOP militare NON cambia**: là le regole piste restano **sorelle** di «Piste» (§CX, decisione
dell'11 settembre) e le LVP dopo di loro, dentro «Dati generali». I due indici divergono qui, ed è voluto —
quello militare lo detta il SOD.

### 🔴 Ripubblicare NON basta: serve un passo di manutenzione

Il catalogo decide la struttura solo alla **nascita** del documento, e il motore di riordino sposta soltanto
fra **fratelli**: a un documento già scritto il padre non glielo cambia nessuno, e ripubblicare
fotograferebbe la struttura vecchia. Serve `IDocumentMaintenance.ReparentAirportSectionsAsync`, che gira
all'avvio prima di `AddMissingCatalogSections` — stessa forma e stesso ordine del passo dei parcheggi
militari (3 settembre).
⚠️ **Tocca solo quel che è rimasto dov'era il catalogo**: se qualcuno ha già portato altrove una delle due
sezioni, quella è la scelta di chi scrive. È anche ciò che rende il passo idempotente.
⚠️ **Le release già pubblicate non si toccano**: il pubblico vede l'indice nuovo alla **prossima
pubblicazione** di ogni vIPI.
Misurato all'avvio sulla copia del DB: *«Sistemate «Regole piste» e «LVP» in 10 vIPI d'aeroporto.»*

### 🔴 E il trasloco ha rotto un titolo, in una lingua sola

`AirportLegacySections.ForView` risolveva i titoli di catalogo **solo sulle radici**, con scritto accanto
*«non serve scendere nei figli: il profilo Airport è piatto»*. Da questa modifica non è più vero, e il
difetto si vedeva **solo in inglese**: la sezione figlia si chiamava **«Runway rules»** — la resa della
macchina — invece di «Runway selection rules» del catalogo. In italiano tutto a posto, perché lì il titolo
salvato coincide.

Ora la risoluzione **scende**, e con lo stesso giro si chiude un difetto **preesistente e mai notato**: le
cinque raccolte di «Carte aeroportuali», figlie dal 3 settembre, avevano lo stesso problema.
⚠️ Una sotto-sezione **libera** non è nel catalogo e resta com'è: il suo titolo è di chi scrive.
