# Sezioni condivise fra più accordi

> ⚠️ **7 ottobre 2026 — l'unità che si condivide è diventata la CLAUSOLA.** Il committente ha provato il primo giro
> e ha chiesto due cose che il modello «sezione condivisa» non sapeva fare: condividere un coordinamento solo, e
> farlo entrare nella sezione uguale che l'altro accordo ha già. I §3–§5 qui sotto raccontano il primo giro e
> restano come storia; **quel che vale oggi è il §10**. Le decisioni del §2 valgono ancora, dette della clausola.

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

## 10. Dalla sezione alla clausola — 7 ottobre 2026

### Il fatto

Il committente, dopo aver provato il §9 sulla copia di produzione: «deve essere possibile condividere anche la
singola clausola, non solo le singole sezioni. Inoltre se esiste una sezione uguale a quella con cui voglio
condividere si linka a quella, non se ne crea una nuova».

Le due richieste sono la stessa. Con la sezione come unità:

- l'accordo che aveva già la **sua** tabella «arrivi LICT» se ne ritrovava accanto una seconda, ospite — due
  tabelle uguali nello stesso documento;
- «le clausole comuni, più quelle solo mie» era possibile solo così, con due sezioni gemelle;
- un coordinamento solo non si poteva condividere.

### Perché non i due modi insieme

Tenere la sezione condivisa **e** aggiungere la clausola condivisa era la strada più corta, ed è sbagliata: una
sezione ospite nell'accordo B è, per chi cerca «la sezione uguale di B», la candidata naturale — ma è la stessa
riga che compare anche in A. Una clausola di un terzo accordo fatta entrare lì finirebbe anche in A, senza che
nessuno l'abbia chiesto. Per evitarlo le sezioni ospiti andrebbero escluse dalla ricerca, e si tornerebbe a
creare la gemella. Due meccanismi che si pestano i piedi: ne resta **uno**.

### Il modello

```
CoordinationAgreement
└── AgreementSection            sta in UN accordo, come prima del 6 ottobre
    ├── AgreementAirport
    ├── AgreementClause         SectionId è la sezione DI CASA
    │   └── AgreementClauseShare   NUOVA: (ClauseId, SectionId) — una sezione OSPITE, di un altro accordo
```

- **Condividere** = dire «chi cede → chi riceve». Nell'accordo di quella coppia la clausola entra nella sezione che
  dice la stessa cosa — stesso traffico, stesso verso, stessi scali — che nasce (con la stessa prosa) **solo se non
  c'è**. Anche l'accordo nasce solo se manca. «Condividi» sulla testata di una sezione è lo stesso gesto per tutte
  le clausole che mostra.
- **Il verso non serve più sulla presenza**: lo dice la sezione che ospita, che è dell'altro accordo.
- **Una presenza per accordo**: una clausola che lì compare già, in qualunque sezione, non si aggiunge.
- **Le ospiti stanno in coda** alla sezione, nell'ordine che hanno di casa. Non c'è un ordine per presenza: si
  riordinano di casa. (Limite accettato: non si può infilare un'ospite fra due clausole della sezione che la ospita.)
- **Un gruppo di varianti viaggia intero.** Varianti ed eccezioni dicono la stessa cosa a condizioni diverse: la
  struttura è **contenuto**, uguale in ogni accordo. Si condivide il gruppo anche scegliendone una riga; una
  variante aggiunta dopo eredita le presenze; una variante sola **eliminata** se ne va per tutti.
- **Numeri di gruppo unici nell'archivio.** `VariantGroup` era progressivo per accordo: portato nella tabella di un
  altro, il gruppo «1» ospite si fonderebbe col gruppo «1» di casa. I gruppi nuovi prendono il massimo di tutto
  l'archivio più uno; un gruppo vecchio cambia numero la prima volta che viene condiviso, se il suo è usato altrove.

### La regola, detta della clausola

> **Il contenuto si distrugge solo quando se ne va l'ULTIMA presenza.**

| Gesto | Che succede |
|---|---|
| **✕ guardando un accordo** | la clausola condivisa si toglie **da quello**; se lì era di casa, la casa passa alla sezione che la ospita («promozione») |
| **✕ su una variante sola** di un gruppo condiviso | eliminazione vera, ovunque: è una modifica al gruppo |
| **✕ nella vista a elenco** | eliminazione vera, ovunque: lì non c'è un accordo da cui si guarda — la stessa clausola è due righe con una spunta sola. La conferma lo dice |
| **Elimina la sezione / l'accordo** | le clausole condivise che ci stavano di casa cambiano casa prima; le altre se ne vanno; quelle ospitate perdono la sola presenza |
| **✂ Stacca** | qui le clausole (coi gruppi interi) diventano copie indipendenti; negli altri accordi restano |
| **Annulla** | uno stato, non un gesto all'indietro: la fotografia di una clausola condivisa ricorda che lo era, se era ospite e dove. Se vive ancora torna la **presenza** (di casa dov'era di casa); se è sparita ovunque torna il contenuto, con le sue presenze |
| **⇢ Sposta** | rifiutato per le clausole condivise e per le sezioni che ne portano: prima si stacca |
| **Unisci gemelle** | le clausole che la sezione assorbita ospitava passano a quella che resta |
| **Copia nel verso opposto** | copia quel che la sezione mostra, ospiti comprese, come clausole sue |

### Che cosa è stato tolto

`AgreementSectionShare` con verso e ordine per presenza, `RemoveSectionAsync`, `DetachSectionAsync`,
`UndoPresenceAsync`, l'`agreementId` di `UpdateSectionAsync` e `CopySectionToReverseAsync`, il ribaltamento delle
presenze ospiti quando i lati si scambiano, l'eccezione «una sezione condivisa non è una gemella». Con loro i
quattro modi di sbagliare del §9 «la pagina» che riguardavano il verso per presenza.

Migrazione `ClausoleCondivise` sui due provider: crea `AgreementClauseShares`, cancella `AgreementSectionShares`.
⚠️ La tabella che se ne va era nata il giorno prima e in produzione non è mai esistita: le condivisioni fatte per
prova sulla copia del committente si perdono (le clausole restano di casa dov'erano).

### Com'è andata

Test: `AgreementShareTests` riscritto (30 casi), `ClausoleCondiviseNellaPaginaTests` (presidi sul sorgente: ogni
eliminazione dice da quale accordo si guarda; in elenco «Stacca» è spento; la fotografia ricorda le presenze;
l'eliminazione in blocco fotografa ogni clausola una volta sola).

🔴 **L'annulla dell'eliminazione in blocco poteva esplodere**, e non per il modello nuovo: le «sorelle» di un
gruppo sciolto arrivavano al ripristino una volta per accordo che mostrava la clausola, e il ripristino le metteva
in un dizionario per id. Con la prima clausola condivisa in elenco, chiave doppia. Ora si prende la prima.

**Mutazioni.** Quattordici ritocchi al solo comportamento del repository — niente promozione, il gruppo non
viaggia intero, la sezione uguale non si cerca, il gruppo vecchio tiene il suo numero, la variante nuova non segue
le presenze, l'annulla rimette ospite chi era di casa, sezione e accordo eliminati portano via le condivise, la
variante sola si toglie «da qui», il verso copiato dalla sezione di casa, la stessa coppia non si salta, lo stacco
non toglie la presenza, l'unione perde le ospiti, la clausola condivisa si sposta — fanno cadere ciascuno almeno un
test. ⚠️ Al primo giro risultavano tutti verdi: lo script leggeva l'esito in un formato che `dotnet test -v q` non
stampa. Un cancello che non scatta mai va provato una volta rotto.

**Sulla copia della produzione** (seconda base, poi buttata; quella del committente non si tocca). Sette migrazioni
applicate in ordine, `ClausoleCondivise` compresa: `AgreementClauseShares` c'è, `AgreementSectionShares` no.
Trapani, a clausole:

| Gesto | Esito |
|---|---|
| condividi **una** clausola degli arrivi (`MEGAN 1A`) con `LIRR_MIL_CTR` | 1 condivisa; nascono l'accordo e la sezione «arrivi LICT» |
| condividi la sezione degli arrivi (13 clausole) | **12** condivise, nella sezione che c'era già: quella di prima non si ripete |
| condividi la sezione delle partenze (6) | 6 condivise; nasce la sezione «partenze LICT» |
| l'accordo coi militari | 2 sezioni, 19 clausole, tutte ospiti, stessi id di quello di casa; 19 punti derivati |
| ✕ su una clausola guardando l'accordo coi militari | lì 18, di casa restano 19 |

**A schermo**, su un database nuovo e inventato (Milano coi volumi, due accordi verso Padova di cui il secondo ha
già la sua tabella di sorvoli):

| Gesto | Esito a schermo |
|---|---|
| spunta `BASSO`, **⛓ Share…**, chi cede → `LIMM_WS2_CTR` | «Clause shared with the agreement LIMM_WS2_CTR → LIPP_CE1_CTR. Into the section that was already there…»; ⛓ sulla riga, «⛓ 1 shared clause» sulla testata |
| aperto l'altro accordo | **una** sezione: `SUOVE`, poi `BASSO ⛓` in coda |
| ✕ su `BASSO` lì | «Remove the clause from this agreement? It stays in: LIMM_ES2_CTR ⇄ LIPP_CE1_CTR.» → resta `SUOVE`; **Annulla** la rimette |
| spunta `BASSO`, **✂ Detach** | la riga resta, senza ⛓; **Annulla** rimette la condivisione |
| condividi `MEDIO` e **Annulla** | «Sharing undone.», `MEDIO` senza ⛓ |
| vista a elenco | `BASSO ⛓` è due righe, una spunta le prende tutte e due; «Detach» spento («applies to one agreement: open one»); la conferma dice «will disappear from every agreement», quella di riga «It is shared: it will also disappear from: …» |

Zero errori in console, zero risposte ≥ 400. ⚠️ **Non aperto a schermo** il documento reso con clausole condivise:
che arrivino a chi legge lo provano i test del repository e i punti derivati sulla copia.

## 11. Il tasto dove lo si cerca, l'accordo intero, e l'ordine — 7 ottobre 2026, secondo giro

Il committente, provando il §10: «non avevamo detto che doveva essere condivisibile anche la singola clausola
oltre che l'intero agreement? Inoltre non avevamo detto di poter mettere diversi modi di ordinarli? Tipo quello
alfabetico?».

**La clausola singola si condivideva già, ma non si trovava.** Il gesto stava solo nella barra delle clausole
scelte: bisognava spuntare la riga e cercare «⛓ Condividi…» in cima alla pagina. Sulla riga c'erano ⑂ ↳ ⧉ ✎ ✕, e
⛓ no. Una funzione che chi l'ha chiesta non trova è una funzione che non c'è.

| Dove | Che cosa condivide |
|---|---|
| **⛓ sulla riga** (nuovo) | quella clausola (col suo gruppo). Il form si apre sotto la testata della sua sezione; in elenco, dove le sezioni non ci sono, sceglie la clausola e apre il form della barra |
| ⛓ sulla testata della sezione | tutte le clausole che la sezione mostra |
| **⛓ Condividi… sulla testata dell'accordo** (nuovo) | tutte le sezioni: `ShareAgreementAsync(accordo, al posto di, con)`. Si cambia **uno** dei due enti; ogni sezione va nella sezione uguale dell'altro accordo, e chi cede resta chi cede in ognuna. Un esito e un annulla solo |
| ⛓ Condividi… nella barra | le clausole spuntate |

**L'ordine.** Nel §10 le ospiti stavano in coda, e lo si era scritto come limite accettato. Non lo era. Un ordine
per presenza (ogni ospite col suo posto nella tabella che la ospita) avrebbe voluto ogni operazione dell'outline —
sposta, annida, sciogli — consapevole delle presenze. Il committente ha indicato la strada più corta: un ordine
**dichiarato**.

- `AgreementSection.ClauseOrder`: `Manual` · `Points` (alfabetico per punto) · `Level` (per quota). Si sceglie col
  selettore ⇅ sulla testata della sezione, ed è **salvato**: vale nella pagina, nei documenti e nella vista live.
- Lo applica la lettura, in un posto (`AgreementClauseOrdering.Sort`): con un ordine dichiarato le clausole di casa
  e le ospiti si ordinano insieme, e ognuna va al suo posto.
- **Un gruppo di varianti si muove intero**, e dentro resta com'era: lì l'ordine è la struttura.
- **L'ordine scritto a mano non si perde**: `AgreementClause.Order` non si tocca, e tornando a `Manual` si ritrova.
  La riga in lettura porta il posto a schermo (`Order`) e quello salvato (`StoredOrder`); la fotografia per
  l'annulla usa il secondo.
- Con un ordine dichiarato il trascinamento e ↑↓ sono spenti: scriverebbero un ordine che non si vede.
- La sezione che nasce per ospitare prende l'ordine di quella di casa.
- ⚠️ `UpdateSectionAsync` riscrive la sezione intera: ogni chiamata della pagina che ne parte da una riga riporta
  `ClauseOrder`, o girando il verso la tabella tornerebbe «a mano» (presidiato sul sorgente).
- ⚠️ Non è «Ordina per» in cima alla pagina, che c'era già: quello cambia solo come si guarda.

Migrazione `OrdineDelleClausole`, additiva (una colonna, default `Manual`), sui due provider.

### Com'è andata

Test: `AgreementClauseOrderingTests` (7, puri), sei casi in più in `AgreementShareTests` (l'ospite al suo posto e
così anche nelle righe piatte che leggono i documenti; tornando a mano l'ordine scritto si ritrova; l'annulla in
una sezione ordinata rimette il posto salvato; la sezione nata per ospitare prende l'ordine; l'accordo intero coi
due versi, e i suoi rifiuti), tre presìdi sul sorgente della pagina.

Mutazioni: il posto non si riscrive, il gruppo non si muove intero, la quota confrontata come numero e non in
piedi, l'ordine che non si salva, l'ordine dichiarato che sovrascrive il posto salvato, la sezione nata che non
prende l'ordine, l'accordo intero che non sostituisce l'ente — tutte rosse. ⚠️ «Il gruppo non si muove intero» al
primo giro era verde: in ordine alfabetico le righe di un gruppo hanno gli stessi punti e restano vicine anche
ordinate una per una. Lo vede solo l'ordine per quota, e il caso è stato aggiunto.

🔴 **Trovato dalla suite, non da me**: `ClauseOrder` è un enum-stringa con un default, e su MySQL una colonna
`longtext` non può averne uno. Il presidio `IndexedStringLengthTests` l'ha fermato; la lunghezza ora è dichiarata
in `MySqlStringLengths`.

**A schermo**, sul database inventato:

| Gesto | Esito a schermo |
|---|---|
| ⛓ sulla riga di `BASSO`, chi cede → `LIMM_WS2_CTR` | il form si apre sotto la testata della sezione («The clause «BASSO» …»); «Clause shared with the agreement LIMM_WS2_CTR → LIPP_CE1_CTR. Into the section that was already there: no new one was created.» |
| nell'altro accordo, ⇅ → «alphabetical (point)» | da `SUOVE`, `BASSO ⛓` a `BASSO ⛓`, `SUOVE`; «Table order saved.»; le righe non si trascinano più |
| ⇄ due volte | l'ordine resta «alphabetical» |
| ⇅ → «by hand» | di nuovo `SUOVE`, `BASSO ⛓` |
| ⛓ Share… sull'accordo, senza cambiare niente | «Change only one of the two units: the other stays the one of the agreement.» |
| ⛓ Share… sull'accordo, `LIMM_WS5_CTR` al posto di `LIMM_ES2_CTR` | «3 clauses shared with the agreement LIMM_WS5_CTR ⇄ LIPP_CE1_CTR. The agreement did not exist and has been created.»; nel nuovo accordo le tre clausole, tutte ⛓ |
| Annulla | «Sharing undone.», gli accordi tornano due |

Zero errori in console, zero risposte ≥ 400.

