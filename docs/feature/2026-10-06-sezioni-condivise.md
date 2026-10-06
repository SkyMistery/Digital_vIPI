# Sezioni condivise fra più accordi

**6 ottobre 2026.** Carta di lavoro. È il passo 5 di [`2026-10-04-copertura-unica.md`](2026-10-04-copertura-unica.md);
il modello degli accordi è quello di [`2026-08-18-accordi-a-sezioni.md`](2026-08-18-accordi-a-sezioni.md), che
questa carta **estende** in un punto solo.

---

## 1. Il fatto

Il committente (4 ottobre): «a volte ci sono dei trasferimenti che vanno uguali in due agreement (es. su Trapani ci
sono dei coordinamenti che sono validi sia per Trapani ⇄ LIRR_SU che per Trapani ⇄ LIRR_MIL, poiché il SU controlla
i GAT e il MIL gli OAT)».

Dal 18 agosto un accordo è **una coppia di enti**, e una sezione sta in **un** accordo. Quella scelta ha un prezzo
che la carta di allora dichiarava — «TS EXE trasferisce a PS EXE / PN EXE si scrive come due accordi» — e qui il
prezzo si paga per intero: le stesse clausole vanno scritte due volte, e tenute uguali a mano. Misurato sulla copia
di produzione del 1° ottobre: `LIRR_SU_CTR ⇄ LICT_APP` ha **19 clausole**, e l'accordo con `LIRR_MIL_CTR` non c'è
ancora — cioè oggi il traffico OAT di Trapani non ha coordinamenti scritti, perché scriverli vuol dire ricopiarli.

⚠️ E per i militari non è una comodità: dal 24 settembre un MIL vede **solo** i trasferimenti scritti verso di lui
([`2026-09-24-mil-solo-traffico-militare.md`](2026-09-24-mil-solo-traffico-militare.md)). Quel che è scritto per SU
non ricade su MIL.

## 2. Cosa è stato deciso (committente, 4 e 6 ottobre)

| Bivio | Deciso |
|---|---|
| Come esce nel documento e nella vista live | **Una tabella per accordo** — due tabelle uguali, scritte una volta. Nessuna etichetta GAT/OAT sulla sezione |
| Copia o collegamento | **Collegata**: una sola sezione, agganciata a più accordi |
| Se la modifico | **Cambia in tutti** gli accordi che la portano: è lo scopo |
| Se la tolgo da un accordo | **Si stacca solo da quello** e resta negli altri. Sparisce davvero quando si toglie dall'ultimo |
| Quando i due devono dire cose diverse | Un comando **«Stacca»**: in quell'accordo diventa una copia indipendente |
| Regole di unificazione | **Si tolgono**, nella stessa consegna (zero righe in sviluppo e in produzione, nessun editor) — §7 |

## 3. Il modello

Una riga in più, e nient'altro cambia di posto:

```
CoordinationAgreement
└── AgreementSection            resta com'è: AgreementId è l'accordo DI CASA
    ├── AgreementAirport
    ├── AgreementClause
    └── AgreementSectionShare   NUOVA: (SectionId, AgreementId, Direction, Order) — un accordo OSPITE
```

Una sezione **compare** in un accordo o perché è di casa (`AgreementSection.AgreementId`) o perché è ospite
(`AgreementSectionShare`). Chi legge non vede la differenza: `AgreementRow.Sections` le porta tutte e due.

- **Il verso sta sulla presenza.** I lati di ogni accordo sono canonici (id minore = A) in un ordine che non ha
  niente a che vedere con quello dell'altro: lo stesso «Trapani cede» è `AtoB` in un accordo e può essere `BtoA`
  nell'altro. Per questo `Direction` è una colonna della riga di condivisione, come lo è della sezione per
  l'accordo di casa.
- **Il contenuto è uno**: traffico, aeroporti, prosa, clausole e outline stanno sulla sezione. Modificarli da un
  accordo li modifica per tutti.
- Migrazione **additiva** sui due provider: una tabella nuova. Con la tabella vuota non cambia niente.

> **Alternativa scartata: togliere `AgreementId` dalla sezione e mettere TUTTE le presenze nella tabella nuova.**
> Più simmetrica — niente «casa» e «ospite» — ma è una migrazione che riscrive la chiave di ogni sezione esistente
> e tocca ogni lettura dell'area, per un'asimmetria che resta chiusa dentro il repository (§5). Si riapre se
> l'asimmetria esce di lì.

> **Alternativa scartata: più enti per lato** (tornare ad `AgreementParty`). Condividerebbe l'accordo **intero**,
> e il committente dice «a volte»: Trapani ⇄ MIL può avere anche sezioni sue. E riporterebbe il prodotto
> cartesiano tolto il 18 agosto.

### La regola che regge tutto

> **Il contenuto si distrugge solo quando se ne va l'ULTIMA presenza.**

Togliere la sezione dall'accordo di casa mentre ha ospiti non la cancella: la casa passa al primo ospite
(**promozione**). Vale anche quando si elimina l'accordo di casa intero. È l'asimmetria del modello, pagata in un
posto solo.

## 4. Chi legge — non cambia niente a valle

`EfAgreementRepository.ListByAccAsync` restituisce per ogni accordo le sezioni di casa **e** le ospiti, queste col
verso e l'ordine della loro presenza. `AgreementExpansion` e i suoi cinque consumatori — derivazione dei documenti
(vIPI ACC, vIPI APP, vLOA), frasi, vista live, matcher Aurora, banco di prova, rilievi di consistenza — leggono
`AgreementRow.Sections` e non sanno niente: una tabella per accordo esce da sé.

`AgreementSectionRow` porta in più `SharedWith`: gli **altri** accordi in cui la sezione compare, per dirlo a chi
scrive.

⚠️ **Lo stesso id di clausola compare sotto due accordi.** Chi cerca una clausola o una sezione «fra tutti gli
accordi dell'ACC» deve sapere in quale accordo sta guardando: nella pagina le chiavi di riga e le ricerche
diventano (accordo, sezione).

⚠️ **Le release pubblicate non cambiano da sole**: condividere una sezione la fa comparire nei documenti dell'altro
accordo alla prossima pubblicazione.

## 5. Chi scrive

| Gesto | Che succede |
|---|---|
| **Condividi con…** | si indica «chi cede → chi riceve» (i campi partono da quelli della sezione; di solito si cambia un ente). La sezione compare nell'accordo di quella coppia, che nasce se non c'è |
| **Modifica** di clausole, aeroporti, traffico, prosa | vale per tutti gli accordi che la portano |
| **Gira il verso** (⇄) | vale per **questa** presenza: il verso è suo |
| **Togli** (✕) da un accordo | se ha altre presenze: si stacca da questo e basta. Se è l'ultima: si elimina, come oggi |
| **Stacca** | in questo accordo diventa una sezione indipendente con le stesse clausole; negli altri resta com'è |
| **Elimina l'accordo** | le sue presenze se ne vanno; le sezioni che vivono anche altrove restano là |
| **Annulla** | rimette la **presenza**, non una copia: la fotografia di una sezione condivisa ricorda che lo era |

Rifiutati, con una frase, nel primo giro:

- **Spostare** (⇢) una sezione condivisa in un'altra coppia: prima si stacca, o si toglie la condivisione. Spostare
  una presenza sola è un gesto che si legge in due modi, e nessuno dei due è ovvio.
- **Unire** due sezioni se una è condivisa: l'unione cambierebbe il contenuto anche negli altri accordi.

E una cosa che smette di essere un avviso: una sezione condivisa e una **sua** con lo stesso traffico e gli stessi
scali non sono «gemelle da unire» — è il modo normale di scrivere «le clausole comuni, più quelle solo mie».

Le altre porte che toccano gli accordi:

- `UpdateAgreementAsync` e la sostituzione di un settore (`EfCallsignRenameService`): quando i lati di un accordo si
  scambiano, si ribaltano i versi delle sezioni di casa **e** delle presenze ospiti.
- `RestoreAgreementAsync` / `RestoreSectionAsync`: una sezione condivisa torna come presenza se esiste ancora,
  altrimenti come contenuto.
- Le scritture per ACC (`SectionsOf`, `ClausesOf`): una sezione ospite in un accordo dell'ACC si può modificare da
  quell'ACC, come le sue.

## 6. Pre-flight (FEATURE-PROCESS)

1. **Modello.** Estende, non affianca: la sezione resta una, la tabella nuova dice solo «compare anche qui». Chi
   cerca «dove sta scritto questo coordinamento» trova un posto.
2. **Dispatch.** Nessuno `switch` nuovo. «Casa o ospite» si decide in **un** punto del repository (la presenza);
   nessun consumatore lo ridecide.
3. **Ingressi + verifica.** Ingresso: «Condividi con…» sulla testata della sezione nei Trasferimenti. Nessun
   catch-22: l'accordo di arrivo nasce col gesto. Verifica: Trapani — condividere la sezione di `LICT_APP ⇄
   LIRR_SU_CTR` con `LIRR_MIL_CTR`, e leggere le due tabelle nella vIPI e nella vista live.
4. **Propagazione.** `DeleteSectionAsync`, `UpdateSectionAsync`, `CopySectionToReverseAsync` prendono l'accordo da
   cui si agisce. I commenti che dicono «una sezione sta in un accordo» (entità, carta del 18 agosto, memoria
   `accordi-di-coordinamento`) si aggiornano nello stesso giro.

## 7. Le regole di unificazione se ne vanno

`UnificationRule` è un motore **senza editor**: le applicava solo `AorService`, prima della catena. Zero righe nel
`vipi.db` di sviluppo e nella copia di produzione del 1° ottobre. Dal passo 1 «chi tiene chi» lo dice la catena di
ripiego, che ha il suo editor in Struttura e il suo banco di prova: tenere accanto un secondo modo, invisibile, è
il gemello che il pre-flight §1 vieta.

Via l'entità, `Topology.Rules`, il passo 2 di `AorService`, la lettura in `TopologyBuilder`. Migrazione
**distruttiva** sui due provider: cancella la tabella. ⚠️ È vuota, ma è l'unica operazione di questa consegna che
non si disfa: sta in una migrazione **sua**, dopo quella additiva.

## 8. Esecuzione — fette

| # | Fetta | Stato |
|---|---|---|
| 1 | Entità, mappatura, migrazione additiva (due provider) | ✅ |
| 2 | Lettura: le presenze ospiti in `ListByAccAsync`, `SharedWith` | ✅ |
| 3 | Scrittura: condividi, togli con promozione, stacca, verso per presenza, elimina accordo, annulla, rifiuti | ✅ |
| 4 | Le altre porte: lati che si scambiano (accordo e sostituzione del settore) | ✅ |
| 5 | La pagina: etichetta, «Condividi con…», «Stacca», chiavi (accordo, sezione), gemelle | ✅ |
| 6 | Via le regole di unificazione, con la loro migrazione | ✅ |
| 7 | Guida, carte, memoria; prova a schermo e sulla copia della produzione | ✅ |

## 9. Com'è andata

### Fette 1–4: schema, lettura, scrittura

`AgreementSectionShare` e la sua migrazione `SezioniCondivise` (una `CreateTable` e due indici, su tutti e due i
provider). `EfAgreementRepository.ListByAccAsync` dà a ogni accordo le sezioni di casa più le ospiti; le scritture
nuove sono `ShareSectionAsync`, `RemoveSectionAsync`, `DetachSectionAsync`, `UndoPresenceAsync`.

⚠️ **L'annulla è uno stato, non un gesto all'indietro** (`AgreementPresenceUndo`: casa e ospiti com'erano). Togliere
la sezione dall'accordo di casa ne sposta la casa al primo ospite: «ricondividi con l'accordo di prima» la
rimetterebbe ospite e in coda, mentre deve tornare di casa dov'era.

⚠️ **Le guardie per ACC si sono allargate**: una sezione ospite in un accordo dell'ACC la riguarda quanto le sue
(`SectionsOf`, `ClausesOf`). Senza, da quell'ACC la si leggeva e non la si poteva scrivere.

🔴 **Due test erano verdi per caso, e li ha smascherati una mutazione.** «Il verso è della presenza» e «i lati che
si scambiano» passavano anche col verso copiato tale e quale dalla sezione di casa: nel seed l'ente comune ha l'id
più basso, quindi sta a sinistra in **tutti** gli accordi e i due versi coincidono. Rifatti scegliendo gli enti
**per ordine di id** — il comune in mezzo — così nell'accordo di casa sta a destra e in quello ospite a sinistra.
Cinque mutazioni (niente promozione, verso copiato, casa non rimessa dall'annulla, presenze non ribaltate, verso
dell'ospite scritto sulla sezione) fanno ora cadere sei test.

Test: `AgreementShareTests` (21), più un caso in `SostituisciSettoreTests`. Le quattro scritture nuove sono entrate
da sole nei presidi che provano ogni scrittura degli accordi contro ruolo e lock.

### Fetta 5: la pagina

Sulla testata di una sezione: l'etichetta «⛓ condivisa con …», il tasto **⛓** (condividi: lo stesso form «chi cede →
chi riceve» dello spostamento), **✂** (stacca) e una **✕** che per una sezione condivisa chiede «Togliere la sezione
da questo accordo? Le sue N clausole restano in: …». Il tasto ⇢ resta al suo posto, spento, e dice perché.
L'etichetta sta anche nel pannello della clausola: chi corregge deve sapere che corregge anche di là.

🔴 **Quattro modi in cui la pagina poteva sbagliare senza un errore**, tutti presidiati sul sorgente
(`SezioniCondiviseNellaPaginaTests`):

1. **Scrivere il verso senza dire in quale accordo.** «Gira il verso», il salvataggio della sezione e quello dei
   suoi aeroporti mandano il verso della presenza che si guarda: senza l'accordo finirebbe sulla sezione di casa, e
   girerebbe il verso **in un altro accordo**. Ora tutte e tre le chiamate passano `AccordoDi(sec)`.
2. **Eliminare invece di togliere.** La pagina non chiama più `DeleteSectionAsync`: toglie la sezione dall'accordo
   che si guarda, e arma l'annulla giusto per ciascuno dei due esiti.
3. **Cercare un id fra tutti gli accordi.** La stessa clausola sta sotto due accordi: `AgreementOf`, `SectionOf`,
   `SectionById` e la sezione a fuoco partono dall'accordo aperto, o il pannello mostrerebbe la frase dell'altro.
4. **L'id della clausola come chiave di riga.** Nella vista a elenco le due righe stanno nella stessa tabella:
   Blazor rifiuta il render. La chiave è (accordo, clausola).

E le **gemelle**: una sezione condivisa accanto a una dell'accordo con lo stesso traffico e gli stessi scali non si
segnala più (né sulla sezione, né nel cruscotto delle lacune).

### Dal vivo — 6 ottobre 2026

Host di sviluppo su un database **nuovo e inventato** (struttura di Milano, accordo `LIMM_ES2_CTR ⇄ LIPP_CE1_CTR` con
tre clausole), guidato con Edge:

| Gesto | Esito a schermo |
|---|---|
| ⛓, cambiato chi cede in `LIMM_WS2_CTR` | «Section shared with the agreement LIMM_WS2_CTR → LIPP_CE1_CTR. The agreement did not exist and has been created.»; due accordi nel navigatore, tutti e due «1 ▤ 3»; etichetta «⛓ shared with …» |
| Aperto l'altro accordo | stesse tre clausole, `LIMM_WS2_CTR → LIPP_CE1_CTR`, etichetta che nomina il primo |
| Vista a elenco | **6 righe** (tre clausole × due accordi), nessun errore |
| ✕ dall'accordo ospite | «Remove the section from this agreement? Its 3 clauses stay in: …» → «Section removed from this agreement; it stays in …»; l'ospite resta «0 ▤ 0» |
| Annulla | «Section put back into this agreement.», di nuovo condivisa |
| ✂ | «Section detached: in this agreement it is now an independent copy.», etichetta sparita |
| Annulla | «Detach undone: the section is shared again.» |
| ⛓ una seconda volta verso la stessa coppia | «The section already appears in that agreement: nothing to do.», nessun annulla |

Zero errori in console, zero risposte ≥ 400.

⚠️ **Non provato a schermo**: la tabella nella vIPI e nella vista live dell'altro accordo. Che la sezione ospite
arrivi a chi legge lo provano i test del repository (è `AgreementRow.Sections`, da cui tutti derivano); il documento
reso con una sezione condivisa non l'ho aperto.

### Fetta 6: via le regole di unificazione

Tolti `UnificationRule` e `Acc.UnificationRules` (dominio), `Topology.Rules` e `UnificationRuleSpec`, il passo che
le applicava in `AorService`, la lettura e i due parser JSON in `TopologyBuilder`, il `DbSet` e la mappatura.
Migrazione `ViaLeRegoleDiUnificazione` sui due provider: una `DropTable`, in una migrazione sua dopo quella
additiva.

Nei test se ne va il caso «la regola riassegna TS a ES» (provava un motore che non c'è più) e la voce
`UnificationRule` fra le entità col last-write-wins voluto; tredici `Topology` di prova perdono la riga `Rules`.

⚠️ **`AgreementSectionShare` è entrata fra le modifiche che segnalano «Coordinamenti»** (`ModificheInAttesa.Di`):
la riga elencava a mano le entità degli accordi, e quella nuova non c'era — condividere una sezione non avrebbe
fatto partire il giro che rimette «da rivedere» i documenti. Trovato togliendo `UnificationRule` dalla riga accanto.

### Sulla copia della produzione — 6 ottobre 2026

La copia del 1° ottobre caricata in una **seconda** base di un MariaDB privato (quella che usa il committente non
si tocca), poi buttata. Una prova temporanea, non versionata, apre il contesto col provider MySql e fa due cose.

**Le migrazioni.** `UnificationRules` aveva **zero righe**. Sei migrazioni in attesa, applicate in ordine senza
errori — le quattro già online dopo la copia, poi `SezioniCondivise` e `ViaLeRegoleDiUnificazione`. Dopo: la
tabella delle regole non c'è più, `AgreementSectionShares` c'è.

**Il caso che ha fatto nascere il passo.** L'accordo `LIRR_SU_CTR ⇄ LICT_APP` ha due sezioni — arrivi LICT (13
clausole) e partenze LICT (6). Condivise tutte e due con la coppia Trapani ⇄ `LIRR_MIL_CTR`, lasciando chi cede e
mettendo il MIL al posto del SU:

- la prima **crea** l'accordo ospite, la seconda lo **riusa**;
- l'accordo ospite legge 2 sezioni e 19 clausole, con **gli stessi id di clausola** dell'accordo di casa: è un
  collegamento, non una copia;
- i versi sono giusti nei lati dell'ospite (`LIRR_MIL_CTR → LICT_APP` gli arrivi, `LICT_APP → LIRR_MIL_CTR` le
  partenze), e ogni sezione dice «condivisa con LIRR_SU_CTR ⇄ LICT_APP»;
- `AgreementExpansion` — quel che leggono documenti, vista live e matcher — dà 2 flussi e 19 punti per parte.

Non provato lì: la pagina (chiede l'accesso IVAO) e il documento reso.

