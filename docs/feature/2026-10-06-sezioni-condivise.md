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
| 1 | Entità, mappatura, migrazione additiva (due provider) | ▶ |
| 2 | Lettura: le presenze ospiti in `ListByAccAsync`, `SharedWith` | ▶ |
| 3 | Scrittura: condividi, togli con promozione, stacca, verso per presenza, elimina accordo, annulla, rifiuti | ▶ |
| 4 | Le altre porte: lati che si scambiano (accordo e sostituzione del settore) | ▶ |
| 5 | La pagina: etichetta, «Condividi con…», «Stacca», chiavi (accordo, sezione), gemelle | ▶ |
| 6 | Via le regole di unificazione, con la loro migrazione | ▶ |
| 7 | Guida, carte, memoria; prova a schermo | ▶ |
