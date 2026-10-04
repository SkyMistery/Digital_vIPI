# Un solo motore di copertura — e il piano per coordinamenti e struttura

**4 ottobre 2026.** Carta di lavoro. Segue [`2026-08-31-ricaduta-verticale-e-cicli.md`](2026-08-31-ricaduta-verticale-e-cicli.md)
(la quota) e [`2026-09-10-rinvio-geometrico.md`](2026-09-10-rinvio-geometrico.md) (il luogo). Quelle due carte hanno
dato alla ricaduta dei **trasferimenti** una catena con le fasce e il rinvio; questa la fa leggere a **tutto il resto**.

---

## 1. Il fatto

Il committente (4 ottobre): «C'è una config che prevede ES2/WS2 fino a 325 e poi solo WS5 sopra, e un'altra che
prevede solo ES2/WS2 che vanno fino a UNL: a seconda del se metto l'ES5 figlio del WS5 o dell'ES2 una delle due
config esce sbagliata, anche se provo ad impostare correttamente la separazione dei livelli».

Non era un errore di impostazione. La tabella delle configurazioni della vIPI la calcola `AorService`, che risaliva
i **soli padri**; le righe di ripiego con la fascia scritte in Struttura le leggevano **solo** i trasferimenti, e
solo dal lato di chi riceve. Con un padre solo per settore:

| ES5 figlio di | aperti WS2 + ES2 + WS5 | aperti WS2 + ES2 |
|---|---|---|
| ES2 | ES5 → ES2 ❌ (doveva WS5) | ES5 → ES2 ✅ |
| WS5 | ES5 → WS5 ✅ | ES5 → WS2 ❌ (doveva ES2) |

Chi rispondeva a «chi copre chi», e con che cosa, prima di questo giro:

| Chi | Padri | Righe con fascia | Rinvio (punto) |
|---|---|---|---|
| Trasferimenti, chi **riceve** (vista live, Aurora) | ✅ | ✅ | ✅ |
| Trasferimenti, chi **cede** (vista live) | ✅ | ❌ — si risolveva senza quota | — |
| Filtro «non passare a te stesso» (vista live) | ✅ solo discendenti | ❌ | — |
| Mappa AoR e tabella delle configurazioni | ✅ (+ regole di unificazione, senza editor) | ❌ | — |
| Statistiche | ✅ + geometria | ❌ di proposito | — |

## 2. Il principio (deciso col committente, 4 ottobre)

> **Lo scritto comanda, la geometria avvisa.**

Un trasferimento può avvenire su un punto e a una quota che non stanno esattamente dentro uno spazio aereo (capita
con gli APP, che cedono un po' fuori). Quindi il ricevente resta **quello scritto** nell'accordo, e chi lo
sostituisce quando è chiuso lo dice la **catena** scritta in Struttura. La geometria non sposta mai un trasferimento
da sola: serve agli avvisi e al rinvio «copertura del punto», che resta una riga scelta a mano.

⚠️ Scartata, per questo, la proposta di far decidere il ricevente al punto e alla quota fra i settori online.

## 3. Il piano, in cinque passi

| # | Cosa | Stato |
|---|---|---|
| 1 | **Un solo motore di copertura**: la catena la leggono anche AoR, tabella delle configurazioni, chi cede, filtro «a te stesso» | ✅ questa carta, §4 |
| 2 | **Banco di prova in Struttura**: scelgo chi è aperto (o una configurazione già scritta in una vIPI) e vedo chi assorbe chi, con le fasce | ✅ §7 |
| 3 | **Banco sui trasferimenti**: stesso scenario, tutte le clausole dell'ACC con chi cede e chi riceve | ✅ §7 |
| 4 | **Sposta sezione o clausole in un altro accordo**, più l'avviso «quota fuori dalla banda del settore scritto» | ▶ |
| 5 | **Sezione condivisa** fra più accordi (Trapani ⇄ `LIRR_SU` per i GAT e Trapani ⇄ `LIRR_MIL` per gli OAT): si scrive una volta | ▶ |

Decisione del committente sul passo 5 (4 ottobre): nel documento e nella vista live la sezione condivisa esce come
**una tabella per accordo** — due tabelle uguali, scritte una volta sola — e non come una riga con i due riceventi
etichettati. Quindi nessun dato «GAT/OAT» sulla sezione.

## 4. Passo 1 — cosa è cambiato

**Una funzione nuova, pura**: `FallbackChain.Holders(settore, banda, righe, padre, tiene)` — chi tiene il cielo di
un settore, **fascia per fascia**. È la stessa camminata di `Candidates`, chiesta a ogni quota che il settore
possiede invece che a quella di un punto. I tagli sono i piedi e i tetti delle righe di ogni settore che la catena
può toccare, dentro la banda del settore; due fasce contigue nella stessa mano si riuniscono.

| Dove | Prima | Adesso |
|---|---|---|
| `AorService.Resolve` | primo antenato online, dentro il dominio | `Holders` per ogni settore; `AorResult.Holdings` porta le fasce |
| `Topology` | padri, regole, righe | + `Bands`: la banda dichiarata di ogni settore (limiti dei due cataloghi, in piedi) |
| `ConfigTableProjector` | ownership a una voce | le fasce: un settore diviso sta sotto tutti e due gli aperti, `LIMM_XX_CTR (FL325–UNL)` |
| `TransferResolution.Resolve` (nuova, era il corpo di `ResolveForAccAsync`) | chi cede: una volta, senza quota | chi cede: alla quota di **ogni punto**; due cedenti → il flusso esce due volte, ognuna coi suoi punti |
| `LiveStationParts.IsRealHandoff` | «il ricevente è un mio discendente chiuso?» | «il ricevente **risolto** sono io?» |

Con ES5 sotto ES2 e la riga «ES5, FL325–UNL → WS5» le due configurazioni di Milano escono giuste **tutte e due**
dallo stesso albero.

### Quel che vale la pena sapere

- ⚠️ **A tabella dei ripieghi vuota non cambia niente**: senza righe `Holders` è la risalita dei padri, riga per
  riga. Gli scenari S1–S10 dell'AoR sono rimasti verdi senza toccarli.
- ⚠️ **`Ownership` resta a una voce sola**, per chi una divisione non la sa disegnare (mappa, stato dei blocchi):
  su un settore diviso c'è P se ne tiene almeno una fascia, altrimenti chi tiene la più bassa. Chi sa disegnarla
  legge `Holdings`.
- ⚠️ **Una riga può portare il cielo fuori dal dominio di P.** WS5 tiene ES5 pur non essendone antenato: prima la
  risalita si fermava al bordo del dominio e dava il settore a P, ora lo dà a chi lo tiene davvero.
- ⚠️ **I rinvii «copertura del punto» in AoR non rispondono**: chiedono chi copre un *punto*, e un settore un punto
  non ce l'ha. La catena prosegue sul padre — come per `LIMM_MIL_CTR`, che dalla tabella delle configurazioni è
  comunque fuori (ha la sua sezione).
- ⚠️ **La fascia nella tabella è scritta senza parole di una lingua** (`SFC`, `UNL`, `FL325`, `2500 ft`): la tabella
  finisce dentro le release pubblicate, che si leggono in italiano e in inglese.
- 🔴 **Il filtro «a te stesso» sbagliava in due versi**, non uno. WS5 che tiene ES5 per una riga si vedeva passare
  il traffico a sé stesso; e un punto verso una torre chiusa **spariva** anche quando a raccoglierlo era il suo
  avvicinamento aperto, che sta in mezzo.
- **Le vIPI vanno ripubblicate** per vedere la tabella corretta nel documento pubblico: lo snapshot è una fotografia.
  Nell'editor la tabella si deriva dal vivo, e cambia subito.

### Cosa resta sui soli padri, di proposito

- **Statistiche** (`CoverageResolver.Owners`): la differenza è dichiarata dal 31 agosto e fissata da un test.
- **Chi presiede un aeroporto** (`AirportPresidencyResolver`) e la **catena mostrata a GND/DEL**
  (`LiveStationParts.CoverageChain`): sono «a chi salgo», non «chi tiene il mio cielo a questa quota».

### Aperto

- **`UnificationRule`**: resta com'era — motore senza editor, zero righe nell'archivio di sviluppo — e si applica
  ancora prima della catena. Toglierla vuol dire una migrazione che cancella una tabella sui due provider: una
  slice sua, non dentro questa. Finché c'è, è il secondo modo di dire «chi tiene chi» che il pre-flight §1 non vuole.

## 5. Pre-flight (FEATURE-PROCESS)

1. **Modello.** Nessun concetto nuovo: `SectorFallback` c'era, la catena c'era. Si toglie un lettore gemello
   (la risalita dei soli padri dentro `AorService`), non se ne aggiunge uno.
2. **Dispatch.** Nessuno `switch` nuovo.
3. **Ingressi + verifica.** Nessun ingresso nuovo: le righe si scrivono dove si scrivevano (Struttura). Verifica:
   editor di una vIPI ACC, sezione Configurazioni, con la struttura di Milano; vista live di WS5 con ES5 chiuso.
4. **Propagazione.** `LiveStationParts.TransfersAsync` perde il parametro `topology` (non serve più: decide il
   ricevente risolto). `ResolvedTransferFlow`: lo stesso flusso può uscire più volte. Commenti e carte aggiornati
   nello stesso giro.

## 6. Verifica

- Test: `CoperturaUnicaTests` (23: il cuore, l'AoR, la tabella, chi cede), `LiveStationPartsTests` (+2 e uno
  corretto), `AccProfileTests.Config_Table_Legge_le_righe_di_ripiego…` (dal database alla tabella).
- **Rossi sul codice di prima**: riportando le tre logiche a com'erano (soli padri in AoR, cedente senza quota,
  filtro spento) cadono 8 test, e solo quelli.
- Dal vivo: §7.

## 7. Passi 2 e 3 — il banco di prova

In fondo a `/services/vsop/admin/sector-structure`, sezione richiudibile **«Banco di prova»** (nasce chiusa, e da
chiusa non legge niente). Si sceglie un ACC, si aprono e chiudono i suoi settori d'area e i suoi avvicinamenti, e
sotto escono due tabelle:

- **chi tiene cosa** — una riga per aperto, con quel che assorbe (e la fascia, se un settore si divide); in fondo
  chi raccoglie da **fuori dall'elenco** e, se c'è, quel che non raccoglie **nessuno**;
- **i trasferimenti dell'ACC** con quegli aperti — cedente scritto e cedente vero, ricevente scritto e ricevente
  vero, evidenziato dove non coincidono; di suo mostra solo i punti che **cambiano mano**.

Dalla pagina Trasferimenti il link «⚗ Banco di prova» ci porta con l'ACC già scelto (`?bench=LIMM`).

### Le scelte

- ⚠️ **Non ha un motore suo.** `CoverageBenchService` chiama `FallbackChain.Holders` e
  `IAgreementService.ResolveForAccAsync` con l'insieme di aperti dello scenario: le stesse due porte della mappa
  AoR, della tabella delle configurazioni e della vista live. Un banco che calcolasse per conto suo proverebbe
  un'altra cosa.
- ⚠️ **Tutto ciò che è fuori dall'elenco si considera aperto** (altri centri, torri). La domanda è «come si divide
  il mio cielo»: coi vicini chiusi ogni trasferimento verso fuori finirebbe su UNICOM e coprirebbe le sole
  differenze che si vogliono vedere.
- ⚠️ **Non scrive niente**, quindi non chiede il lock della struttura. Usa la struttura **salvata**.
- ⚠️ **Gli scenari pronti sono le configurazioni della vIPI PUBBLICATA**, non della bozza: la porta della bozza
  (`LoadForEditAsync`) garantisce il documento, cioè può scrivere. Chi sta ancora scrivendo una configurazione la
  prova nell'editor della vIPI, dove la tabella si deriva dal vivo — e da questo giro, giusta.
- Le letture passano dalla **fila della pagina** (`InFila`): il servizio è scoped come gli altri della pagina.
- Si parte da **tutti aperti**: ognuno tiene il suo, e chiudendone uno si vede chi lo raccoglie.

### Verifica

- Test: `CoverageBenchTests` (8, il cuore puro), `StructureBenchTests` (7, il componente con un servizio finto).
- **Dal vivo — 4 ottobre 2026**, host di sviluppo su un database **nuovo e inventato** (nessuna copia di dati
  veri): struttura di Milano com'è in produzione (ES5 sotto ES2, riga «ES5, FL325–UNL → WS5»), un centro vicino e
  un accordo ES5 → `LIPP_CE1_CTR` con due clausole (`ALTOP` a FL350, `BASSO` «as coordinated»).

  | Aperti | Chi tiene cosa | Trasferimenti |
  |---|---|---|
  | tutti | ognuno il suo | 2 punti, 0 cambiano mano |
  | WS2, ES2, WS5 | WS5 tiene ES5 | `ALTOP` lo cede **WS5**, `BASSO` lo cede **ES2** |
  | WS2, ES2 | ES2 tiene ES5, WS2 tiene WS5 | tutti e due li cede **ES2** |
  | nessuno | «Nobody (UNICOM)»: tutti e quattro | — |

  ⚠️ La seconda riga è il banco che fa il suo mestiere: `BASSO` **non ha una quota**, quindi la riga con la fascia
  non si può valutare e il punto resta al padre. Non è un difetto: è quel che succede davvero, e adesso si vede
  prima che succeda. Guardato anche in Edge (tema scuro, pagina inglese): zero errori in console, zero risposte
  ≥ 400; il link dai Trasferimenti porta `?bench=LIMM`.
- 🔴 **Trovato per strada, non di questo giro**: su un database dove un import di catalogo non è **mai** riuscito
  la pagina Struttura risponde 500 — `ImportStates.LastSuccessUtc` vale `0001-01-01`, `GetLastSuccessAsync` la
  rende come data vera e `SogliaTimbro.Calcola` le toglie un giorno. Segnalato a parte; in produzione gli import
  sono riusciti e il caso non si presenta.
