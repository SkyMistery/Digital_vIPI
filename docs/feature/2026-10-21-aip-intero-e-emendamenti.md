# L'AIP intero e gli emendamenti AIRAC (21 ottobre 2026)

> Carta madre: [`2026-09-18-aurora-sector-lab.md`](2026-09-18-aurora-sector-lab.md) (§5 i PDF). Viene dopo la F6
> ([`2026-10-18-f6-pdf-aip.md`](2026-10-18-f6-pdf-aip.md)) e la F7 ([`2026-10-19-f7-sid-e-star.md`](2026-10-19-f7-sid-e-star.md)),
> di cui riusa i pezzi (`Aip`, `Aip.Pdf`, `Aip.Pdf.Italia`, `Aip.Aurora`, l'AIP espanso e le sue correzioni).
> Stato: ✅ **APPROVATA** dal committente il 21 ottobre, con le risposte D1-D4 di §5 (tutte come proposte). §2 è deciso
> dal committente; §3 è la proposta dell'agente, approvata. Il lavoro è in §6.

## §0 — La domanda

Il committente (21 ottobre): «vorrei rivedere l'AIP espanso, qualcosa non mi torna». Ha scaricato **tutto l'AIP 2611 che
ci serve, 851 PDF**: ENR 2, 3, 4, 5, 6, AD 2 (97 aeroporti), AD 3 (10 eliporti). Lo abbiamo passato pezzo per pezzo
contro l'AIP espanso di prova (`aip/2610`, `li.json`). Ne escono due lavori:

1. **Rifare l'AIP espanso sul 2611, con tutto** quel che i PDF dicono, e con le date delle pagine controllate.
2. **Tenerlo aggiornato con gli emendamenti**: l'AOD scarica solo il PDF dell'AMDT AIRAC, il Lab fa il resto (pagine
   sostituite nella cartella dei PDF, controllo, AIP espanso rifatto).

## §1 — Cosa c'è (misurato il 21 ottobre)

I PDF del 2611 stanno in `D:\Programmazione\IVAO_Test\SectorLab-prova\AIP 2611\` (fuori dal repo, come da F6 §2),
ordinati per parte: `ENR\ENR 2.1.1 FIR, UIR, TMA`, `ENR\ENR 2.1.2 CTR`, … `ENR\ENR 6 carte`, `AD 2\<ICAO>\`,
`AD 3\<ICAO>\`; i nomi sono quelli dell'eAIP. Confronto con l'elenco del committente: 230 rotte su 230, 97 aeroporti su 97,
10 eliporti su 10. Non esistono nell'eAIP (committente): ENR 5.6.1.11 (Molise), ENR 5.6.1.18 (Umbria), il file 7 dell'AD 2.

**Il difetto principale: l'AIP espanso «2610» non era del 2610.** Il confronto delle date pagina per pagina (testata di
ogni pagina dei PDF 2611 contro la `page.effective` del `li.json`) trova pagine **più vecchie del ciclo dichiarato**:
ENR 2.1.1.3-1 (Brindisi FIR), ENR 3.4.1-3/4, ENR 5.1.4-21/22, AD 2 LICA 1-1/1-11, LIME 1-1/1-15 e **72 pagine di ENR 4.4**
(datate giugno e luglio nell'espanso, 1° ottobre nel PDF). Nessuno l'aveva detto: l'espanso prende il ciclo dalla
cartella, non dalle pagine.

| Parte | AIP espanso (2610) | PDF 2611 | Note |
|---|---|---|---|
| ENR 2.1 FIR/UIR, TMA, CTA, CTR | 3+3, 8+19, 110 (8 CTA), 45 CTR in 113 zone | uguali, zona per zona | non letti: frequenze ACC/SCCAM/FIC, divisione fra gli ACC sopra FL195 (linea per punti), aree VFR della FIR Roma («Isola di Ponza»…) |
| ENR 2.1.3 ATZ militari, 2.2 RMZ/NPZ/FRA, FMC | 16, 36/2/2, 14 + 3 svizzeri | uguali | 2.1.1.2-17/18 (FMC Roma) cambiate col 2611 |
| ENR 3.2 rotte | 29 | 230 | L995 cambiata col 2611; un tratto vuoto (la regola «vale il dato di prima», ENR 3.2-1 nota 3) |
| ENR 3.4.1 attese sui punti | 413 su 245 punti | 248 punti | TUCOZ, JESSY 3000, VANXI 2000: già segnalate nella F6 (celle unite in verticale), mai lette |
| ENR 3.4.2 attese sui radioaiuti | 129 su 51 | 68 elencati | 17 da guardare uno per uno. ⚠️ Corretto nel passo 2a: la colonna «rotte/procedure interessate» l'espanso la leggeva già (`affected`, 129 su 129); quel che si perdeva erano i radioaiuti senza attese; e i radioaiuti sono 72 (sigla e tipo), non 68 |
| ENR 4.1 radioaiuti, 4.5 luci | 55, 11 | uguali | |
| ENR 4.4 punti | 1 254 | 1 256 | NANKA e TEMRI nuovi; 72 pagine vecchie |
| ENR 5.1, 5.2, 5.3.1-5.3.3 | 541, 221, 31 | uguali | ENR 5.1.5 «NIL» |
| ENR 5.3.4, 5.4, 5.5, 5.6 | — | 2 pp.; 4 825 ostacoli puntuali, 802 lineari, 86 piattaforme; 58 pp. di attività sportive; 18 regioni di parchi | mai letti |
| AD 2 | 5 scali (LICA LIMC LIME LIRF LIRN) | 97 | dal testo solo 2.1-2.2, 2.12, 2.13, 2.17, 2.18, 2.19; niente file 5 (avvicinamenti) |
| AD 3 | — | 10 eliporti | |

I file numerati dell'AD 2 (visto su LIRF): **1** testo AD 2.1-2.24, **2** carte di terra e parcheggi, **3** carte solo
immagine, **4** STAR e WAYPOINT LIST, **5** avvicinamenti strumentali (tabelle, attese, waypoint), **6** SID, **8** ATC
Surveillance Minimum Altitude Chart. L'AD 3 ha 1, 4, 5, 6.

**L'emendamento** (`AMDT.pdf`, AIRAC AMDT A12/26, pubblicato il 15 ottobre, in vigore il **26 novembre**, cioè il 2612,
617 pagine) contiene tre cose: l'elenco «pagine da distruggere / pagine da inserire» con la data di ognuna (554/572;
nelle parti che teniamo 450/468), le **pagine nuove**, e il **GEN 0.4** nuovo, cioè l'elenco di tutte le pagine valide
dell'AIP con la loro data. Porta anche una sezione nuova, ENR 2.1.1.4.9 SICILIA CTA. Prova di fattibilità con
`pdftotext` (`amdt.py`, nello scratchpad del 21 ottobre): 593 pagine nuove riconosciute dalla testata; tre (ENR 5.4.1.8-1
e -3, AD 2 LICJ 2-9) sono immagini senza testata leggibile. 🔴 `pdftotext` **sfalsa le colonne** dell'elenco: date
attaccate alla riga sbagliata (138 «date diverse» su 450 che non sono vere), e la stessa cosa nel GEN 0.4.

## §2 — Quel che è già deciso (committente, 21 ottobre)

- **L'AIP espanso si rifà sul 2611, con tutte le informazioni.** Si tiene tutto anche se oggi non serve (come la F6 §2):
  le frequenze ACC/SCCAM/FIC, la divisione degli ACC sopra FL195, le aree VFR della FIR Roma, la colonna «rotte e
  procedure» di ENR 3.4.2, ENR 5.3.4, 5.4, 5.5, 5.6, tutto l'AD 2 e l'AD 3.
- **AD** (proposta dell'agente, accettata): tutti i 97 aeroporti e i 10 eliporti; ogni sottosezione dell'AD 2 a tabella
  diventa dato, quelle in prosa (2.20-2.23) restano testo diviso per sottosezione; dal file 5 procedure, attese e waypoint;
  dei file 2 e 3 l'elenco delle carte (titolo, pagina, data) e le note scritte.
- **Le quote di attesa senza unità** (MHA «9000»): **piedi AMSL** (la colonna è in piedi, ENR 3.2-1 nota c); «FL70» resta
  livello.
- **Le date delle pagine si controllano** (proposta dell'agente, accettata): ogni pagina letta deve essere del ciclo
  dichiarato o più vecchia; una pagina che manca o che è di un ciclo dopo si segnala.
- **Il Lab segnala** ogni errore o mancanza di lettura: se ENAV cambia la struttura di un PDF ce ne accorgiamo e si
  aggiorna l'importer.
- **Emendamenti**: l'AOD scarica solo il PDF dell'AMDT; **il Lab fa il resto**.
- **Due cicli**: il Lab tiene l'AIP in vigore e, se è già uscito, il successivo; quando il successivo entra in vigore il
  vecchio si toglie (coerente con la F6 §2: il ciclo prima non si archivia).
- **AIP SUP ed emendamenti non AIRAC**: fase successiva, uno per uno.

## §3 — La proposta (dell'agente)

### La cartella dei PDF diventa un AIP con le pagine contate

- Una cartella per ciclo: `…\AIP 2611\`, `…\AIP 2612\`. Dentro, l'ordine di §1. Accanto ai PDF, un **indice delle
  pagine** scritto dal Lab (`pagine.json`): per ogni pagina il riferimento (`ENR 4.4-77`, `AD 2 LIRF 5-12`), la data, il
  file, la posizione nel file, e da dove viene (download, AMDT A12/26).
- La pagina si riconosce **dalla testata**; le pagine solo immagine **dalla posizione** (nel file: la k-esima pagina di
  `AD 2 LIRF 3` è `AD 2 LIRF 3-k` se le altre lo confermano; nell'AMDT: l'ordine dell'elenco «da inserire»).
- Il controllo del ciclo è un pezzo a sé: data di ogni pagina ≤ data del ciclo; ogni pagina del GEN 0.4 del ciclo
  presente con la sua data. Le pagine delle parti che non teniamo (GEN, ENR 0, 1) si ignorano.

### La lettura dice cosa ha letto

Ogni lettura produce, accanto al modello, un **rapporto per PDF**: pagine lette, sezioni riconosciute, righe di tabella
lette e non lette, oggetti per tipo, e le segnalazioni che già esistono (`Rifiuto`, «senza quota», `LimitsFromText`…).
In più, per ogni sezione, **i conti attesi** dove l'AIP li stampa (zone numerate di una CTA, righe con coordinate,
sigle `T……`/`L……`/`P……` degli ostacoli, ▲ e ∆ delle rotte): un conto che non torna è una segnalazione. Il Lab lo mostra
nella pagina dell'AIP, a gruppi.

### L'emendamento

Gesto proposto: nella pagina dell'AIP, **«Applica un emendamento…»** → si sceglie il PDF.

1. Il Lab legge l'elenco «da distruggere / da inserire» **con la posizione delle parole** (PdfPig), non col testo a righe.
2. Controlla che le pagine da distruggere siano proprio quelle che abbiamo, con quella data. Se no si ferma e dice
   quali: vuol dire che manca un emendamento prima, o che la cartella non è quella giusta.
3. Crea `AIP <ciclo nuovo>` come copia della cartella in vigore, toglie le pagine da distruggere, mette le nuove al loro
   posto nell'ordine dei numeri, crea i file delle sezioni nuove (SICILIA CTA), toglie i file rimasti vuoti.
4. Controlla la cartella nuova col GEN 0.4 dell'emendamento: zero pagine mancanti, zero date diverse, o l'elenco.
5. Rifà l'AIP espanso del ciclo nuovo e mostra le differenze col ciclo prima (quello che il sector dovrà cambiare).

La cartella in vigore non si tocca finché il ciclo nuovo non entra in vigore.

### I passi

| # | Passo | Finita quando (misurato) |
|---|---|---|
| 1 | **L'indice delle pagine e il controllo del ciclo** (pezzo nuovo in `Aip.Pdf`, la parte italiana in `…Italia`) | sugli 851 PDF del 2611: ogni pagina con riferimento e data, le pagine-immagine per posizione, zero pagine di un ciclo dopo; confrontato con l'espanso 2610 ritrova le 81 pagine vecchie di §1 |
| 2 | **ENR 2, 3.4, 4 rifatti sul 2611** con le aggiunte di §2 (frequenze, divisione ACC, aree VFR, colonna di 3.4.2, MHA in piedi, «vale il dato di prima», celle unite di 3.4.1) e il rapporto di lettura | i conti di §1 più NANKA e TEMRI, TUCOZ/JESSY/VANXI lette, i 17 radioaiuti di 3.4.2 spiegati uno per uno |
| 3 | **ENR 3.2: le 230 rotte** | 230 rotte; i punti di ogni rotta quanti i ▲ e ∆ (controprova come in F6) |
| 4 | **ENR 5 intero**: 5.3.4, 5.4 (ostacoli puntuali, lineari, piattaforme), 5.5, 5.6 | 4 825 + 802 + 86 ostacoli; le aree di 5.5 e 5.6 contate sulle coordinate stampate |
| 5 | **AD 2 e AD 3, il testo**: tutte le sottosezioni, la prosa per sottosezione, l'elenco delle carte | 97 + 10 scali; per ognuno le sottosezioni presenti nel PDF tutte nel modello o dette |
| 6 | **AD 2 e AD 3, le procedure**: SID e STAR di tutti (la F7 estesa), il file 5 (avvicinamenti, attese, waypoint) | per ogni scalo le procedure stampate tutte nel modello o dette |
| 7 | **L'emendamento** (§3 sopra) | l'AMDT A12/26 applicato al 2611 → `AIP 2612` con zero differenze dal GEN 0.4; l'espanso 2612 rifatto |
| 8 | **I due cicli**: in vigore e successivo, il cambio alla data | il Lab apre il 2611 fino al 25 novembre e il 2612 dal 26; il 2611 tolto al cambio |

Dopo, fuori da questa carta: AIP SUP ed emendamenti non AIRAC, uno per uno.

## §4 — Rischi

- **Le colonne sfalsate**: con `pdftotext` l'elenco dell'AMDT e il GEN 0.4 non si leggono bene (§1). Si leggono con le
  posizioni delle parole, come le tabelle della F6.
- **Scrivere i PDF**: oggi il Lab li legge soltanto. Togliere e inserire pagine con PdfPig (`PdfDocumentBuilder`, copia
  di pagine) va provato **per primo** nel passo 7, su carte solo immagine, prima di fidarsi: la pagina scritta deve
  restare identica a quella dell'AMDT.
- **Il peso**: oggi `li.json` pesa 4 MB; con 5 600 ostacoli, 230 rotte e 97 scali crescerà di molto. Il repo di prova
  dell'AIP espanso va su GitHub (limite di 100 MB a file). Vedi D1.
- **Il tempo di lettura**: 851 PDF, 1,1 GB. La lettura intera va misurata nel passo 1; l'emendamento rilegge solo i file
  toccati.
- **Le pagine-immagine** si riconoscono per posizione: se un file ha pagine fuori ordine il Lab deve dirlo, non indovinare.

## §5 — Domande per il committente

| # | Domanda | Proposta dell'agente | Risposta (21 ottobre) |
|---|---|---|---|
| D1 | **L'AIP espanso in un file solo o diviso?** | Diviso per parte: `li-enr2.json`, `li-enr3.json`, … `li-ostacoli.json`, `li-ad2.json`; lo stesso modello, letto insieme | ✅ «dividiamoli per parti» |
| D2 | **Dove sta il PDF dell'emendamento** | Nella cartella del ciclo che crea: `AIP 2612\AMDT\A12-26.pdf`, così si sa da dove viene ogni pagina | ✅ |
| D3 | **L'ordine dei passi** | 1 e 7 prima (la cartella giusta e gli emendamenti, così il 2612 di novembre arriva già col Lab), poi 2, 3, 4, 5, 6, 8 | ✅ «come proponi» |
| D4 | **Il sector vero** | Resta fermo fino al 29 ottobre come deciso; questi passi toccano solo l'importer, la cartella dei PDF e l'AIP espanso di prova | ✅ «per ora è fermo, sistemiamo prima l'AIP espanso» |

## §6 — Il lavoro

### Passo 1 — l'indice delle pagine e il controllo del ciclo: fatto (21 ottobre)

- **`Aip.Pdf.PageIndex`** (pezzo comune, eAIP): le pagine di un file dalla testata (`PageInfo`); quelle senza testata
  **dalla posizione**, solo se il file è di una sezione sola e ogni pagina letta sta al posto del suo numero. I
  controlli della cartella: pagina senza riferimento, senza data (non per quelle dalla posizione), fuori posto, file con
  più sezioni, riferimento doppio, 🔴 **pagina con una data dopo l'entrata in vigore del ciclo**.
- `PageInfo.Reference` salta un numero davanti al riferimento (AD 2 LIPX 5-11 nel 2611: «1 AD 2 LIPX 5 - 11»).
- **«Leggi nell'AIP espanso»** apre ogni PDF una volta sola (`ItalianAip.ReadFile` sulle pagine già lette; chi passa
  solo il percorso legge come prima, e un file che l'importer non sa leggere non si apre) e fa l'indice di **tutti** i
  PDF, anche di quelli che l'importer non legge ancora. Le segnalazioni dell'indice stanno con le altre (cinque tipi
  nuovi; «pagina senza data» la dice solo l'indice, per tutti i file). Un file della cartella che non è un PDF ora si
  dice «non si apre» anche se l'importer non lo legge ancora.
- **L'indice si scrive nell'AIP espanso**, `2611\pagine.json`, una pagina per riga. ⚠️ Cambia da §3 («accanto ai
  PDF»): così è nel repo dell'AIP espanso, e in git la differenza fra due cicli è l'elenco delle pagine cambiate; la
  cartella dei PDF non è un repo. Per l'emendamento (passo 7) l'indice si rifà comunque sulla cartella.
- **Le pagine rispetto al ciclo prima**: dal `pagine.json` del ciclo prima (con le nuove e le tolte) o, se non c'è, dalle
  pagine citate nel suo modello (solo le date diverse). Nel riquadro AIP: «Pagine: 7.328 (6 riconosciute dalla
  posizione)» e «Rispetto al 2610: 90 con un'altra data», che si apre sull'elenco.

**«Finita quando» misurato** sugli 851 PDF del 2611 (`AIP 2611`), con accanto il `li.json` 2610 dell'AIP espanso di
prova (sonda nel temporaneo, non nel repo):
- **7 328 pagine** in 93 s; **6 dalla posizione** (ENR 6.3, le dispari dalla 3 alla 13: nemmeno una parola); il numero
  di ogni pagina è la sua posizione nel file, ogni file è di una sezione, nessun riferimento doppio, **nessuna pagina
  dopo il 29 ottobre**: zero segnalazioni dell'indice.
- **Rispetto al 2610: 90 pagine con un'altra data**: le 81 vecchie di §1, le cambiate col 2611 (ENR 2.1.1.2-17/18,
  ENR 3.2 L995-1, ENR 4.4-77/78) e quattro che il confronto con `pdftotext` di §1 non aveva visto (AD 2 LICA 1-12/13,
  LIME 1-17/18).
- Già oggi l'importer legge **460 PDF su 851**: 1 136 aree, **230 rotte**, 542 attese, 55 radioassistenze, 2 030 punti,
  11 luci, **45 scali**, 31 pericoli, 244 settori SMAC, 1 774 procedure, con 616 segnalazioni (516 di procedure): materia
  dei passi 2-6.

**Sul banco** (1400 px, sei PDF del 2611 e il `li.json` 2610 nell'ambiente di prova, ciclo scritto e «Leggi» premuto
con clic e tasti veri): «Pagine: 72 (6 riconosciute dalla posizione: non hanno testata)», «Rispetto al 2610: 1 con
un'altra data» che si apre su `ENR 2.1.1.3-1`; `2611\pagine.json` scritto, una pagina per riga. Test: Aip.Pdf 122 (+9),
Lab 1 362 (+2).

### Passo 7 — l'emendamento: fatto (21 ottobre, via del committente: «vai col passo 7»)

Prima il rischio di §4: **PdfPig copia le pagine identiche** (`Aip.Pdf.PdfPages`, l'unico posto che scrive PDF). Provato
su una carta solo vettoriale di ENR 6.3 (126 887 tracciati, nessuna lettera), una carta con immagine di AD 2 LIRF e una
pagina dell'AMDT: dimensioni, rotazione, testo, immagini e tracciati uguali.

- **`Aip.Pdf.Italia.ItalianAmendment`**: la copertina (numero «A12/26», «Data di entrata in vigore/Effective date»; non
  «Effective date» da solo, che è anche nella testata di ogni pagina — l'ha trovato un test), l'**elenco a due colonne
  con la posizione delle parole** (la colonna di destra comincia a x 301, la sua intestazione a 388: si taglia a metà
  fra le intestazioni), il **corpo allineato all'elenco «da inserire»** pagina per pagina (le pagine senza testata dalla
  posizione; un riferimento o una data diversa, o i conti che non tornano, si dicono), il **GEN 0.4 a tre colonne**: le
  colonne finiscono con la data, la parte (GEN, ENR, AD) viene dalle intestazioni della colonna perché le righe di GEN ed
  ENR non la ripetono.
- **`Vipi.SectorLab.Core.PdfAip.EmendamentoDellAip`**: le cartelle dei cicli stanno **dentro la cartella dei PDF**
  (`AIP 2611`, `AIP 2612`: un anticipo del passo 8). L'emendamento va al ciclo prima del suo (`AIP 2611` per il 2612) e
  crea quello nuovo accanto, senza toccare il vecchio; scrive in `AIP 2612.scrivendo` e sposta alla fine; copia l'AMDT in
  `AIP 2612\AMDT\A12-26.pdf` (D2). **Si ferma prima di scrivere** se l'emendamento non si legge per intero, se non entra a
  inizio ciclo, se manca la cartella del ciclo prima o c'è già quella nuova, se una pagina da togliere non c'è o ha
  un'altra data (le pagine senza testata non la dicono e passano). Una pagina da togliere di una sezione mai scaricata
  non ferma: si dice. Le parti tenute sono ENR 2-6, AD 2, AD 3; il resto dell'emendamento non si tocca.
- 🔴 **Una sezione che la cartella non ha si scrive solo intera** (tutte le pagine che il GEN 0.4 le dà, o senza GEN 0.4 da
  1 a N): sul banco, con sei PDF, l'emendamento faceva «LI-AD 2 LICJ 2.pdf» con le sole pagine cambiate. Le sezioni a
  metà si elencano («vanno scaricate intere») e le loro pagine restano fra le mancanti.
- **«Leggi»** con le cartelle dei cicli legge `AIP <ciclo>`; senza cartelle di ciclo tutta la cartella, come prima; con
  altre cartelle di ciclo ma non quella chiesta si rifiuta (leggerebbe un altro ciclo).
- **Il gesto**, nel riquadro AIP: «Emendamento AIRAC (il PDF dell'AMDT di ENAV)», proposto dal Lab se in Download c'è un
  PDF col nome «AMDT» (il più recente), e «Applica l'emendamento». Poi il ciclo nuovo si legge da solo nell'AIP espanso,
  e «Rispetto al 2611» dice le pagine cambiate.

**«Finita quando» misurato**: l'AMDT A12/26 vero sugli 851 PDF del 2611 (in una cartella di prova col 2611 collegato, che
non si è toccato: 851 file prima e dopo), in 177 s:
- letto: 584 pagine da togliere, 602 da inserire, 602 nel corpo nello stesso ordine e con le stesse date, 4 vuote
  (ENR 6.3-3/5/7/11) dalla posizione, il GEN 0.4 con 7 856 pagine, **zero problemi**;
- `AIP 2612`: 56 PDF riscritti, 795 copiati, **SICILIA CTA** nuova (`ENR\ENR 2.1.1 FIR, UIR, TMA\LI-ENR 2.1.1.4.9.pdf`),
  508 pagine tolte, 526 messe, 76 pagine di GEN, ENR 0-1, AD 0-1 lasciate;
- **col GEN 0.4: zero date diverse, zero pagine in più, 14 mancanti**: le pagine d'introduzione di ENR 3.1, 3.3, 3.4,
  4.2, 4.3, 5 e 5.4, mai scaricate (due ciascuna);
- controprova: la cartella 2612 riletta da capo, 7 346 pagine, zero segnalazioni dell'indice.

**Sul banco** (1400 px, sei PDF del 2611 in `pdf-aip\AIP 2611`, percorso scritto e tasto premuto con clic e tasti veri):
«AIP 2611 → AIP 2612, 38 pagine tolte e 180 messe», le 39 sezioni a metà elencate, poi da sola la lettura del 2612:
«Rispetto al 2611: 34 con un'altra data, 142 nuove, 0 tolte»; il campo del ciclo passa a 2612. Il campo dell'emendamento
era già proposto: in Download c'è un altro `AMDT.pdf`, del 18 ottobre. ⚠️ Non provato a occhio: una carta riscritta
aperta in un lettore di PDF (qui non c'è un programma che disegni le pagine) — sta nelle prove a mano. Test: Aip.Pdf 128
(+6), Lab 1 378 (+16).

### Passo 2 — ENR 2, 3.4, 4 rifatti (via del committente, 21 ottobre: «vai con il passo 2»)

Quattro pezzi: **2a** le attese di ENR 3.4, **2b** le frequenze ACC/SCCAM/FIC, **2c** la divisione fra gli ACC sopra FL195
e le aree VFR della FIR Roma, **2d** «vale il dato di prima» nei tratti delle rotte.

#### 2a — le attese: fatto

- **Le fasce della tabella** (`ItalianEnr3`): dove la pagina ha i bordi orizzontali, il punto è la sua fascia e il nome
  sta in mezzo alla cella. In una fascia con un nome solo il nome si legge per primo, virata e rotta si cercano nella
  fascia, e una fascia con una quota, una virata e una rotta le abbina anche lontane. **TUCOZ** letto: la quota 3000 e le
  note 1-2 stavano 30 punti sopra il nome e finivano a TOPRU (che ora non ha più le note di TUCOZ); il riferimento
  `RDL103/11NM LIN` viene dalla fascia. Le **5 attese di PAN** in ENR 3.4.2 ritrovano la nota «1. Waypoint holding», che
  stava sopra il nome e andava persa.
- ⚠️ **JESSY e VANXI restano segnalati**: nella fascia ci sono più quote, e l'ordine non basta (JESSY: la seconda 4000 sta
  accanto a 033°, non alla seconda 034°; VANXI: tre quote, due virate, due rotte). Indovinare in silenzio sarebbe peggio:
  si dice «3000 · senza quota: R · senza quota: 034°» e «2000».
- **La quota senza unità è in piedi AMSL** (D del committente): 481 attese su 542 nel 2611; il testo resta com'è
  stampato. Effetto nel Lab: l'ovale di un'attesa in piedi sopra i 14 000 ora si disegna a 1,5 minuti (prima la quota
  senza unità valeva 0).
- **ENR 3.4.2, l'elenco intero** (`NavaidProcedures`, campo nuovo e facoltativo del modello): sigla, tipo, nome, rotte e
  procedure associate di ogni radioassistenza, anche senza attese. Nel 2611 **72**, di cui **20 senza attese**: AHO,
  ALB, CSL, DEC NDB e TACAN, FAL, FRS, GRO TACAN e VOR, IRBN, LPD, NAP, NOV, PAL, PIN, PIS, PTC, RMN, VIE, VIL — i «17
  da guardare» di §1, contati per sigla. Tutte hanno le loro procedure (AHO: EA star, EA sid, EA ial; PAL: L12, L869,
  M616… CJ star…).

**Misurato** sul 2611 e confrontato col `li.json` 2610 sulle pagine rimaste uguali (18 di 3.4.1, 11 di 3.4.2): ENR 3.4.1
**414 attese su 246 punti** (+TUCOZ), nient'altro di diverso salvo le note di TOPRU; ENR 3.4.2 129 attese, uguali salvo le
note di PAN. Test: Aip.Pdf 129 (+1), Aip 50 (il JSON col campo nuovo).

#### 2b — le frequenze ACC, SCCAM e FIC: fatto

- **`ItalianEnr2Frequencies`** legge le tabelle «Frequenze ACC/SCCAM» e FIC di ENR 2.1.1.1-3 (sette colonne, testata in
  ogni pagina; si riconoscono da «STAZIONE» e «FREQUENZA», così le tabelle delle CTA non si toccano). Nel modello
  **`AtsUnitFrequencies`** (campo nuovo e facoltativo): stazione, servizio, nominativo, le frequenze (com'è stampate, in
  MHz, emissione, rimandi) e, della riga, gli orari e le note.
- 🔴 **Una riga è la fascia fra i bordi della colonna del nominativo**: stazione e servizio sono celle unite col testo in
  alto (Roma ACC: un «Servizio di controllo di avvicinamento» per Roma Arrivi, Roma Partenze e Roma Radar) e passano alle
  righe sotto; una fascia senza stazione, servizio e nominativo continua la riga prima, anche nella pagina dopo (Milano
  ACC su tre pagine).
- ⚠️ **Gli orari sono della riga, non della frequenza**: dentro una riga il PDF non ha bordi fra una frequenza e l'altra e
  le celle degli orari stanno a cavallo (Roma FIC: «2200-0600 (2100-0500)» a metà fra 128.800 e 133.250). Si tengono come
  stampati, nell'ordine; attribuirli a una frequenza sarebbe indovinare.
- Si leggono anche «CH125.485» senza lo spazio (Padova) e la parola spezzata a fine riga («Padova Infor- mazioni»), e i
  rimandi scritti sotto la frequenza («134.200» / «(1) (2)»). Un testo nella colonna che non è una frequenza si dice.

**Misurato** sul 2611: **131 frequenze in 23 righe** — Milano 39, Padova 23, Roma 56, Brindisi 13 —, zero segnalazioni.
Controprova valore per valore con `pdftotext`: le sole differenze sono tre frequenze scritte nelle note, non nella tabella
(125.275 due volte, 120.600 di Bolzano INFO) e le due «CH» senza spazio che la controprova non vede. Test: Aip.Pdf 133
(+4).

#### 2c — le linee fra gli ACC e le aree VFR della FIR: fatto

- **`ItalianEnr2Fir`** legge, nella colonna italiana delle note della FIR (dalla prima «NOTE» al titolo della sezione
  dopo), le frasi «a *lato* della linea congiungente i punti … [nello spazio aereo compreso tra … e …]» delle note «N.
  *Unità* ACC/FIC:». Nel modello **`AtsUnitLine`** (campo nuovo e facoltativo): l'unità, il lato, i punti (coordinate,
  anche «433732N - 0121235E» col trattino in mezzo, o nomi), i limiti (`SFC` · `FL 195 (incluso)`, `FL195 (escluso)` ·
  `UNL`) e la frase com'è stampata. I nomi prendono la posizione da ENR 4.4 dopo la lettura di tutti i PDF
  (`AipEspanso.ConLePosizioniDelleLinee`); un nome che ENR 4.4 non ha si dice. Una frase che non si legge si dice.
- **Le aree VFR della FIR** (`a. Roma FIR VFR “Isola di Ponza”`): la tabella a due colonne sotto il titolo, nel modello come
  area `Other` di ENR 2.1 con «Roma FIR VFR» nelle note. Nel 2611 è una: l'area TIBA **Isola di Ponza**, cerchio di 2 NM su
  PNZ, SFC-1500 ft AGL.
- 🔴 **Il lettore comune delle geometrie** (`GeometryText`) ora capisce «with a radius of 2NM» e «con un raggio di», la
  barra attaccata fra italiano e inglese («raggio di/with a») e il nome del centro prima della coordinata («centred on Ponza
  VORTAC 'PNZ' 40°54'43"N…»). Effetto misurato su tutto il 2611: le 1 136 aree di prima identiche; **due settori SMAC di
  Pisa** (AD 2 LIRP 8-3 e 8-4) cambiano, e in meglio: l'arco «di raggio 5 NM» prima non si capiva e il suo centro finiva
  come vertice; ora è un arco col centro e il raggio stampati. Segnalazioni di geometria da 24 a 12.

**Misurato** sul 2611: **15 linee** (Milano 2, Padova 2+1+1, Roma 1+3+1, Brindisi 1+2+1, fra le tre FIR), tutti i nomi
risolti su ENR 4.4, zero segnalazioni; **1 area VFR**. La lettura intera del 2611 confrontata con quella del passo 1: aree,
rotte, punti, radioassistenze, scali, procedure identici; cambiano solo le quote delle attese (2a), i due settori di Pisa, e
si aggiungono Ponza, le 23 righe delle frequenze, le 15 linee, le 72 radioassistenze di ENR 3.4.2. Test: Aip.Pdf 138
(+5), Lab 1 379 (+1), Aip 50.

#### 2d — «vale il dato di prima» e la nota dopo l'ultimo punto: fatto

- **Misurato prima**: sulle 230 rotte del 2611, 1 021 tratti, **nessuno** con limiti, rotta, distanza o ampiezza vuoti; i
  404 livelli minimi che mancano sono tutti su tratti a senso unico (verso «none»), quindi non mancano. Il «tratto vuoto»
  di §1 era l'interruzione di Z910 (la riga di barre), già letta come interruzione.
- **La regola di ENR 3.2-1 nota 3** entra lo stesso, per i cicli che verranno: un tratto senza limiti verticali o ampiezza
  li prende dal tratto prima; **rotta, distanza e livelli minimi no** (sono del tratto e del verso: copiarli farebbe dati
  falsi). Una colonna dei limiti vuota non è più una segnalazione; senza un tratto prima sì.
- **La nota dopo l'ultimo punto è della rotta** (`Route.Remarks`, campo nuovo): M740 finiva con «For continuation see AIP
  MALTA», una cella unita su più colonne, ed era l'unica segnalazione delle rotte («dati dopo l'ultimo punto»). Ora: le
  230 rotte del 2611 **senza segnalazioni**. Dopo l'ultimo punto un dato di tratto (rotta, limiti, frecce, numeri) si
  continua a dire.

Test: Aip.Pdf 140 (+2), Aip 50 (la nota della rotta nel JSON).

#### 2e — il rapporto di lettura: fatto

- **Per ogni PDF letto** (`EsitoDellAipEspanso.Rapporto`): le pagine, quel che ha dato per tipo (aree, rotte, attese,
  frequenze, linee…), le segnalazioni, e **i conti che l'AIP stampa** (`ItalianExpectedCounts`): le zone numerate di
  CTA, TMA e CTR («Zone '7'», «Roma CTR Zona/Zone '3'», una volta sola, a inizio riga) contro le aree con la zona nel
  nome; i punti ▲∆ di una rotta contro i suoi punti. Un conto che non torna è una segnalazione («conto che non torna»):
  vuol dire che il lettore ha perso qualcosa o che il PDF ha cambiato forma (§2).
- Nel riquadro AIP: «Conti dell'AIP (zone numerate, punti delle rotte): N controllati, tutti tornano», che si apre sul
  rapporto dei file che hanno conti (prima quelli che non tornano).
- **Misurato** sul 2611: **268 conti, tutti tornano** — 232 zone in 38 file, 1 274 punti in 230 rotte.
- Sul banco (1400 px, nove PDF del 2611, «Leggi» con clic e tasti veri): «3 controllati, tutti tornano» e le righe
  «LI-ENR 2.1.1.2.pdf — 18 pagine · 28 aree, 56 frequenze, 6 linee fra gli ACC · zone 19 ✓»; senza ENR 4.4 nella
  cartella le linee fra gli ACC dicono i nomi che non si risolvono, come devono.
- In più: l'etichetta «limiti presi dal testo» che mancava; e un altro clic instabile di bUnit messo nel giro del renderer
  (`ConfrontoDiEnr4Tests`, lo stesso difetto di `AipNellaBarraTests` della mattina).

Test: Aip.Pdf 143 (+3), Lab 1 380 (+1).

#### Il passo 2, chiuso

«Finita quando» di §3: i conti di §1 ci sono tutti (ENR 2 zona per zona, 55 radioassistenze, 11 luci, **1 256 punti con
NANKA e TEMRI**), più le aggiunte di §2 (frequenze, linee fra gli ACC, Ponza, ENR 3.4.2 intero, quote in piedi, «vale il
dato di prima»); **TUCOZ letto**; **JESSY e VANXI no, di proposito** (l'abbinamento nella cella unita è ambiguo: si
dicono); i **17 radioaiuti di 3.4.2** spiegati (radioassistenze senza attese, ora nell'elenco con le loro procedure). Il
rapporto di lettura c'è e sul 2611 non ha conti che non tornano.

### Passo 3 — ENR 3.2, le 230 rotte (via del committente, 21 ottobre: «vai con il passo 3»)

**La controprova.** Le 230 rotte le leggeva già il Lab (passi 2d e 2e: 230 rotte, 1 274 punti come i ▲∆). Il passo 3 le
rilegge con un altro lettore, pdfplumber (`controprova32.py` nello scratchpad della sessione `c6ab12b9`): le colonne dai
numeri «1…6» della testata e dai bordi verticali di ogni pagina, il punto dal marcatore ▲∆ alle coordinate, il tratto
sotto le coordinate (anche quando continua nella pagina dopo, con le colonne di quella pagina). Poi confronta cella per
cella con quello che legge il Lab (una sonda che scrive le rotte lette in JSON, non committata). Risultato sul 2611:

- **1 274 punti su 1 274 uguali**: nome (anche su due righe, «PALERMO/Punta Raisi VOR/» «DME 'PAL'»), coordinate al
  milionesimo di grado, ▲ o ∆, note del punto (ente che cambia, «For continuation see AIP …», anche le note su più righe
  di M740).
- **1 044 tratti su 1 044 uguali**: rotta magnetica, distanza, limiti (anche «5500ft» «AMSL» su due righe e le quote in
  piedi di KY139), ampiezza, verso e minima dei livelli dispari e pari (anche in piedi, «4000»), note del tratto (CDR1
  sopra la riga della rotta, orari).
- **23 interruzioni** (le righe di barre): le stesse 23, una per una, che il Lab segna `Discontinuity`.

**Quel che mancava: il riquadro «NOTE / REMARKS».** Sotto la tabella di ogni rotta c'è un riquadro con due colonne,
l'italiano sotto «NOTE» e l'inglese sotto «REMARKS», note numerate: è lì che l'AIP dice che un tratto è **a senso
unico** («Il tratto UNIMI-NAXAV è utilizzabile solo in direzione nord»), che sotto un livello decide l'ACC col QNH, che
serve il radar, che un segmento è chiuso quando sono attive LI D25B/C. Il Lab non lo leggeva. Ora:

- `Route.Notes`: una `RouteNote(Number, Text, English)` per nota; vuota se l'AIP stampa «NIL» o il riquadro vuoto, null
  se il riquadro non c'è. Il lettore prende il testo sotto la tabella e, dalla prima pagina con la tabella in poi, il
  testo delle pagine senza tabella (il riquadro può finire lì: M725, M727); divide le colonne alla x di «REMARKS»
  (N606 e N736 scrivono «REMARK»); una riga «n.…» apre la nota n solo se n è il numero dopo («ENR 6.3-5)» in L613 non
  apre niente).
- Si dicono: testo sotto la tabella senza il riquadro; note in italiano e in inglese in numero diverso.
- Nella scheda della rotta (indice e mappa dell'AIP espanso) le note in italiano, «Nota 1», «Nota 2»; l'inglese resta
  nel modello.

**Misurato** sul 2611: **159 rotte con note (202 note), 67 senza** (57 «NIL», 10 col riquadro vuoto com'è stampato:
L146, L862, L865, L869, M740, Q549, Q865, Z923, Z925, Z982), **4 senza riquadro** (L127, P573, T334, Y29); nessuna
segnalazione. Contro la controprova, parola per parola: 156 rotte su 157 uguali, M725 diversa solo per come i due
lettori spezzano «a.è»; in più il Lab legge N606 e N736 («REMARK»), che la controprova perdeva. Ogni nota ha il suo
inglese. Sul banco (1400 px): L12 con le sue tre note nella scheda, M725 con la nota che finisce nella pagina senza
tabella.

Test: Aip.Pdf 146 (+3), Aip 50, Lab 1 380 (stessi test, un controllo in più).

**Finita quando** (§3): 230 rotte; i punti di ogni rotta quanti i ▲∆ (passo 2e: tutti tornano); controprova cella per
cella senza differenze; le note delle rotte lette. Il passo 3 è chiuso.

### Passo 4 — ENR 5 intero (via del committente, 21 ottobre: «vai con il passo 4»)

Quel che mancava di ENR 5 nel 2611: 5.3.4 (droni BVLOS, solo testo), 5.4 (gli ostacoli: 20 PDF di puntuali per regione, le
piattaforme, 20 PDF di lineari per regione), 5.5 (quattro PDF: lanci paracadutisti, acrobatico, aeroclub e lavoro aereo,
volo da diporto sportivo), 5.6 (18 PDF di parchi e zone di protezione della fauna, uno per regione, e l'introduzione).

#### 4a — gli ostacoli di ENR 5.4: fatto

- **Le tabelle sono stampate di traverso**: il testo sale dal basso in alto e PdfPig ne dà le lettere una per una
  (`Rotate270`). `PdfText` (codice comune `Aip.Pdf`) ora dà, per una pagina con almeno 50 lettere girate, anche la
  **pagina girata** (`PdfPageText.Turned`): la stessa pagina vista in orizzontale, con le parole costruite dalle linee di
  base e dagli spazi, e i bordi scambiati (un verticale diventa orizzontale). Le parole della pagina restano quelle dritte
  (testata e piè di pagina), così l'indice delle pagine del passo 1 non cambia.
- **Il lettore** (`ItalianEnr54`): le colonne dai bordi verticali, ognuna col suo numero nella riga «1 2 3 …» della testata
  (13 colonne i puntuali, 12 le piattaforme col nome, 18 i lineari con inizio, fine, lunghezza, punto più basso e più
  alto); un ostacolo va dalla riga della sua sigla (`T004043`, `P000001`, `L003539`) alla sigla dopo; la tabella delle
  note («ID NOTE/REMARKS», anche a metà pagina) dà la nota all'ostacolo per sigla. «NIL» in una cella vuol dire che il dato
  non c'è. La sezione è quella della regione, dalla testata della pagina (ENR 5.4.1.13 = Puglia).
- **Il modello**: `Obstacle` (sezione, genere, sigla, regione, provincia, comune, località, tipo in italiano e in inglese,
  posizione e fine, lunghezza, altezze in metri e in piedi — anche decimali, «118.2» — segnalazione di giorno e di notte com'è
  stampata, nome della piattaforma, nota), in `AipDocument.Obstacles`.
- **Il conto** (rapporto di lettura): le sigle sulle pagine girate fino alla tabella delle note, contro gli ostacoli letti.
- **Sulla mappa e nell'indice**: genere «Ostacoli», i puntuali e le piattaforme come punti, i lineari come una linea; nell'
  indice un gruppo per regione col suo nome. La scheda è da guardare: le correzioni degli ostacoli non ci sono ancora.
- **Misurato** sul 2611: **4 825 puntuali, 802 lineari, 86 piattaforme** (i numeri di §1), 131 con la nota, conto 5 713 su
  5 713. **Controprova** con pdfplumber dalle lettere girate (`controprova54.py`, scratchpad della sessione `c6ab12b9`), riga per
  riga: le 5 713 sigle tutte nel Lab, coordinate tutte uguali; parole e numeri uguali salvo le celle «NIL» e i numeri che la
  controprova non sa mettere in colonna (verificati a mano: il Lab ha ragione, T004963 ha davvero la cella dei piedi
  vuota).
- **Trovato nel PDF**: la sigla **T001645 è due volte**, in Puglia (40°37'N) e in Sicilia (37°43'N). Si dice: un controllo
  di tutto l'AIP espanso («la stessa sigla 2 volte»), perché un file da solo non lo vede.

#### 4b — le aree di ENR 5.5 e 5.6: fatto

- Il lettore delle aree di ENR 5.2.1 (nomi liberi, colonne variabili) ora prende una descrizione della tabella: quante
  colonne, sezione e tipo, che colonna dice l'attività, gli orari, l'ente, a chi si notifica. Tipi nuovi: `Parachute`,
  `Aerobatic`, `AerialWork`, `Ultralight` (ENR 5.5), `NaturePark` (ENR 5.6); campo nuovo `Airspace.Notification` (5.5.1, la
  colonna «Notifica a»). Sulla mappa i generi «Sport» e «Parchi».
- **Il nome su due righe** di 5.5.3 («AEROMODELLISTI L'AQUILA -CIV-» / «Zona di lavoro aereo/Aerial work area») ha accanto
  alla seconda il limite inferiore: si prendeva per un'area nuova (196 segnalazioni). Ora un'area nuova comincia solo se
  quella aperta ha già i suoi due limiti.
- **Un corridoio largo in metri** («Corridor 150 M wide», Norma in 5.5.4): `GeometryText` lo legge.
- **Un difetto vecchio, trovato qui**: `SenzaTesta` toglieva tutto quello che stava sopra la testata più in basso della
  pagina. In 5.6.1.12 a pagina 12 c'è una seconda tabella: si perdevano «Sacra di San Michele», «Val Troncea» e la chiusura
  del Gran Paradiso. Ora della seconda testata si tolgono solo la riga dei numeri e le parole di testata. Misurato su tutto
  l'AIP prima e dopo: cambiano **solo** quelle due aree in più (e il Gran Paradiso chiuso); ENR 2, 5.1, 5.2, rotte,
  procedure tutto uguale.
- **Il conto** (rapporto di lettura): le coordinate della prima colonna (le note a destra ne citano altre) contro i punti
  delle geometrie lette; un punto stampato due volte di seguito è uno, il primo punto riscritto prima di «to point of
  origin» chiude l'anello.
- **Misurato** sul 2611: **247 aree in ENR 5.5** (41 lanci, 28 acrobatico, 102 aeroclub e lavoro aereo, 76 VDS) e **314 in
  ENR 5.6**; coordinate 5 584 lette su 5 590. Le 6 che mancano sono di **Magra** (5.6.1.8): due poligoni sotto un nome
  solo, e il lettore — come in ENR 2, 5.1 e 5.2 — tiene il primo e lo dice («MoreGeometries»). Da decidere col committente.
- Sul banco (1400 px, Abruzzo 5.4.1 e 5.4.3, 5.4.2, 5.5.4, 5.6.1.12): «155 ostacoli», «Conti … 9 controllati, tutti
  tornano»; nell'indice ENR 5.4.1.1 ABRUZZO 43, 5.4.2 Piattaforme e simili 86, 5.4.3.1 ABRUZZO 26, 5.5 76, 5.6 39; la scheda
  di L003542 con tipo, comune, altezze, segnalazione e nota.

Test: Aip.Pdf 158 (+12), Aip 51 (+1), Lab 1 383 (+3).
