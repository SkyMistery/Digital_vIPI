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

#### 4c — i testi di ENR 5: fatto

- `AipText` (sezione, testo italiano, testo inglese, prima pagina) in `AipDocument.Texts`. Il lettore (`ItalianProse`): le
  due colonne dell'AIP, l'inglese dalla x dove cominciano le righe della colonna destra (la più frequente a metà pagina);
  il punto elenco dell'inglese, qualche punto prima della colonna, è dell'inglese; le righe in paragrafi — un paragrafo
  nuovo dopo uno spazio più largo dell'interlinea, a un numero seguito da una maiuscola («2.1. Quando», non «15 m (per
  es.»), a un punto elenco, a «NOTA»/«REMARK», dopo un titolo (corto o numerato, senza punto) salvo il suo seguito tutto
  maiuscolo.
- Si leggono i file solo di testo — le introduzioni di **ENR 5.1** (le definizioni delle zone, le note generali, gli
  attraversamenti, il divieto di sorvolo di edifici), **5.2.1** (i poligoni militari), **5.6.1** (i parchi), e **5.3.4**
  (i droni BVLOS, che prima si contava e basta) — e il testo **prima della tabella** di 5.5.1 (le regole dei lanci,
  tre pagine e mezza), 5.5.2, 5.5.3, 5.5.4.
- **Misurato** sul 2611: 8 testi, nessuna segnalazione. Da sapere: l'indirizzo centrato di ENR 5.1 §7.3 (Ministero della
  Giustizia, a cavallo delle due colonne) esce diviso fra italiano e inglese.

#### 4d — l'AIP espanso diviso per parti (D1): fatto

- `AipEspanso.Scrivi` scrive il modello di un ciclo in più file: `li.json` (intestazione, aree di ENR 2 ed ENR 5,
  frequenze e linee degli ACC, pericoli, testi) e accanto `li-enr3.json` (rotte, attese, ENR 3.4.2), `li-enr4.json`
  (radioassistenze, punti, luci), `li-ostacoli.json`, `li-ad2.json` (scali, SMAC, procedure); una parte vuota si toglie.
  `AipEspanso.Modello` lo rilegge intero; un `li.json` di prima, tutto in un file, si legge com'è. Le letture del Lab
  (sessione, correzioni, indice delle pagine) passano tutte di lì; il repo dell'AIP espanso registra già `<ciclo>/*.json`.
  `tools/Aip.Aurora.Cli` legge le aree, che restano nel principale.
- **Misurato** sul 2611: 13,5 MB in un file → `li.json` 3,8 MB, `li-ad2` 3,8, `li-ostacoli` 3,9, `li-enr3` 1,3,
  `li-enr4` 0,7; rimessi insieme, lo stesso modello della lettura di prima (la sola differenza è la sezione degli ostacoli,
  ora quella della regione).

Test: Aip.Pdf 159 (+1 netto: due della prosa, via quello del vecchio «letto senza oggetti» di 5.3.4), Aip 51, Lab 1 384 (+1).

Sul banco (1400 px): «Leggi» scrive `li.json`, `li-enr3`, `li-enr4`, `li-ostacoli` (niente `li-ad2`: nella cartella del banco
non ci sono scali); l'esito dice «155 ostacoli, 1 testo»; l'indice rilegge il modello dalle parti (rotte, luci, 155
ostacoli, aree di 5.5 e 5.6).

#### Il passo 4, chiuso

«Finita quando» di §3: **4 825 + 802 + 86 ostacoli** (controprova riga per riga); le aree di 5.5 e 5.6 **contate sulle
coordinate stampate**: 5 584 su 5 590, le 6 che mancano sono il secondo poligono di **Magra**, segnalato (da decidere col
committente se dividerlo in due aree). In più i testi di ENR 5 e l'AIP espanso diviso per parti (D1). Trovati nel PDF: la
sigla doppia T001645; corretto un difetto vecchio (due tabelle sulla stessa pagina).

### Passo 5 — AD 2 e AD 3, il testo (via del committente, 22 ottobre: «vai con il passo 5»)

Prima di questo passo degli scali si leggevano i dati (ARP, piste, frequenze, spazi aerei, radioassistenze) dei 97
aeroporti di AD 2; il resto del testo no, e i 10 eliporti di AD 3 niente.

#### 5a — gli eliporti di AD 3: fatto

- `LI-AD 3 <ICAO> 1.pdf` si legge con lo stesso lettore di AD 2 (`ItalianAd2.ReadHeliport`): le sottosezioni hanno altri
  numeri (3.16 lo spazio aereo, 3.17 le frequenze, 3.18 le radioassistenze, 3.23 le carte) e l'ARP è scritto
  «41°51'59'' N - 016°09'54'' E». `Aerodrome.Heliport`; nell'indice una parte nuova, **AD 3 Eliporti**; sulla mappa sono
  scali col campo «Eliporto».
- Le tabelle «NIL» (spazio aereo, radioassistenze, soglie) non sono righe e non fanno segnalazioni; il radiogoniometro
  **VDF** è un tipo di radioassistenza nuovo (`NavaidKind.Vdf`).
- Il nome del file con **due spazi** dopo «AD 2» (52 scali su 97 nel 2611, «LI-AD 2  LIAF - FOLIGNO 1.pdf») si riconosce:
  prima quegli scali non si leggevano affatto.

#### 5b — tutte le sottosezioni: fatto

- `ItalianAdSections` (codice in `Aip.Pdf.Italia`): ogni parola del corpo della pagina va alla sottosezione dell'ultimo
  titolo sopra di lei («LICA AD 2.3 …», la riga italiana e quella inglese), anche dalla pagina prima. Dentro una tabella di
  tre colonne con la prima stretta e numerata (AD 2.2-2.11, 3.2-3.11) ogni numero apre una **voce** con etichetta e valore,
  fra due bordi orizzontali — il numero non è sempre sulla prima riga — e la voce che continua nella pagina dopo, senza
  numero, resta della sua voce; le altre tabelle si danno **riga per riga**, una cella per colonna; fuori dalle tabelle è
  **prosa** nelle due colonne (il lettore di 4c), una riga sola («Vedi AOC in vigore - See AOC in force») non si divide.
- Il titolo italiano che va a capo fra la riga italiana e quella inglese («… DEI PUNTI DI» / «CONTROLLO», AD 3.8 dei dieci
  eliporti) è del titolo.
- Il modello: `Aerodrome.Sections`, una `AerodromeSection` per sottosezione (numero, titolo in italiano e in inglese,
  voci, righe, testo italiano e inglese, prima pagina).
- **Il conto** (rapporto di lettura): i titoli di sottosezione stampati nel file 1 dello scalo contro le sottosezioni lette.

#### 5c — le carte: fatto

- `Aerodrome.Charts`: l'elenco di AD 2.24 / AD 3.23, titolo e pagina («AD 2 LICA 2-1»). Le righe si prendono da qualunque
  sottosezione con la pagina di una carta nell'ultima colonna: LIPN stampa l'elenco sotto il titolo di AD 2.25.
- La **data** di ogni carta dall'indice delle pagine della cartella (passo 1); una carta elencata la cui pagina non è fra i
  PDF si dice («carta elencata senza la sua pagina fra i PDF»).
- Sulla mappa la scheda dello scalo ha il campo «Carte» e sotto le sottosezioni (le voci, «tabella, n righe», la prima riga
  della prosa) e le carte con la loro pagina.

#### Misurato sul 2611

- **107 scali** (97 + 10 eliporti), sottosezioni **2 655 stampate, 2 655 lette**; **1 610 carte**, tutte con la loro pagina
  fra i PDF. Le aree salgono a 1 733 (+35: ATZ e CTR degli scali che prima non si leggevano), i punti a 2 036, le procedure
  a 1 779. `li-ad2.json` 11,5 MB.
- Segnalato e da guardare nel passo 6 o col committente: **LIET** «senza piste» (la tabella di AD 2.12 si trova solo in
  parte), le ATZ di **Torino Aeritalia** con «ATZ SECTOR 3/4/5» e di **Albenga** con «AIRPORT ATZ» (parole sconosciute
  nella geometria), la quota «FL 70/TRL» di uno SMAC di **LIRA**. Da sapere: in AD 2.16 un «NIL» a cavallo di due pagine
  può perdersi nelle righe.
- Sul banco (1400 px, LICA, LIAF col nome a due spazi, LIBI): nell'indice «AD 2 Aeroporti: LIAF FOLIGNO, LICA LAMEZIA
  TERME» e «AD 3 Eliporti: LIBI Vieste»; i conti «sottosezioni 25 ✓ / 25 ✓ / 23 ✓»; la scheda di LIBI con le 23
  sottosezioni e le 6 carte. Le segnalazioni «scalo» del banco sono carte i cui PDF non sono nella cartella del banco
  (atteso). Nella barra le segnalazioni hanno ora un nome anche per scalo, radioassistenza, punto, luce, pericolo, procedura.

Test: Aip.Pdf 163 (+4), Aip 52 (+1), Lab 1 387 (+3). In più stabilizzato un rosso intermittente di bUnit
(`TreMappeTests`, il clic su un tasto appena ridisegnato).

#### Il passo 5, chiuso

«Finita quando» di §3: **97 + 10 scali**, e per ognuno le sottosezioni presenti nel PDF **tutte nel modello** (2 655 su
2 655, contate). Le carte con titolo, pagina e data; le note scritte sono nella prosa delle sottosezioni. Il passo 6 (le
procedure del file 5, attese e waypoint) è il prossimo.

### Passo 6 — AD 2 e AD 3, le procedure (via del committente, 22 ottobre: «vai con il passo 6»)

Misurato all'inizio sul 2611 (file 4, 5 e 6 di tutti gli scali): SID e STAR lette in 42 dei 45 aeroporti che le hanno, con
le segnalazioni raccolte a Pisa, Cagliari, Viterbo, Trento, Alghero e Catania; non lette le pagine di testo di quattro scali
militari (Foggia, Comiso, Trapani, Grosseto) né i file degli eliporti; il file 5 (51 aeroporti e 10 eliporti, gli
avvicinamenti) mai letto. Il passo è diviso in 6a (SID, STAR e attese di tutti), 6b (il file 5), 6c (mappa, scheda, banco).

#### 6a — SID, STAR e attese di tutti gli scali: fatto

- **I designatori in altre forme**: col genere davanti («STAR MIVKI 1D», LIRP, LIEE), con le lettere spezzate
  («STAR K OVAS 1V», «STAR C AR 1 S»), con le piste dopo («SID BEROK 6B RWY 03R/L», «SID GILIO 5N (RWY 04C)») e ripetuti
  sotto («BEROK 6F»), dentro un titolo («Trento RNP AVCOL 5A Departure», gli eliporti), con un rimando che non è una sezione
  («STAR UPADU 1A (ATC Discretion)»), a 21 punti dalla riga «Terminator» (LICC: prima i suoi tratti finivano nella STAR della
  pagina prima, senza dirlo).
- **Le pagine in due lingue** degli scali militari e di qualche civile (LIBF, LICB, LICT, LIRS, LIDT, LICC, LIEE): il
  designatore è due volte sulla riga, l'italiano a sinistra; si tiene la colonna inglese.
- **Le piste in un titolo a sé** («DEPARTURES FROM RWY 02 – AEA VOR/DME», «RWY 26» sotto «CATANIA FONTANAROSSA STAR RNAV 1»)
  valgono per le sezioni che non le dicono. La prima riga della descrizione di una SID («Initial climb procedure completed,
  turn left…») non è più presa per una salita iniziale (LIEA, LIEE: 33 SID «né tratti né descrizione»).
- **Le intestazioni**: «P ath» spezzata (la colonna dal «Terminator» sotto), «Flyover», «Course/Track».
- **La WAYPOINT LIST nel formato ARINC 424** («MIVKI N44132743 E010041382», anche «N415153.56»); la sigla di una
  radioassistenza nella colonna dei punti («ELB VOR») è la sigla.
- **Le attese delle tabelle** (STAR, avvicinamenti): prima la riga HM entrava come tratto della procedura di sopra, o
  restava «fuori da una procedura». Ora è un'**attesa** (`Holding`, sezione `AD 2 <ICAO>` / `AD 3 <ICAO>`) con le colonne
  in più: rotta vera, tratto com'è stampato (minuti o NM), quota massima, velocità, specifica, e la riga intera com'è
  (`Text`). Le colonne cambiano da scalo a scalo e si leggono per forma; il punto è il nome più vicino alla colonna
  «Waypoint» (una nota in maiuscolo accanto, «MHA … LOCRI», non lo inganna); le note scritte dentro la riga restano nel
  testo. La stessa attesa nel file 4 e nel 5 una volta sola. La chiave dell'attesa di uno scalo comincia con la sezione
  (`AD 2 LIRN ESCOB 145 left`): la stessa può essere anche in ENR 3.4.
- **Gli eliporti**: `LI-AD 3 <ICAO> 4/6` si leggono come gli aeroporti; nell'indice punti, procedure e attese stanno sotto
  l'eliporto in AD 3.
- **Il conto** (rapporto di lettura): i designatori stampati nei file 4 e 6 contro le procedure lette (designatori distinti,
  salite iniziali escluse).

Misurato sul 2611: **procedure 1 779 → 2 034**, **attese 543 → 727** (184 degli scali), punti 2 036 → 2 192; il conto delle
procedure **1 903 stampate, 1 903 lette** su 109 file. Restano, detti:
- 16 punti con le coordinate stampate male nell'AIP (RF407, RF412, RF414, RF707, RN534, RN544, CR401, CR402, MJ601, MJ602, MJ611,
  MJ612, BD434, BC621, PE557, BI106: secondi con l'apice sbagliato, «N E» in più, i due punti al posto dei gradi);
- un'attesa di LIRA 5 con un refuso nella riga («HM 5) RMC(1)»);
- 462 procedure convenzionali di cui la lettura della prosa (F7, slice 7b) non ricava i tratti: il testo è nel modello, la
  frase non capita si dice. Prima erano dei soli cinque scali della F7; la più frequente (Alghero, 32) comincia dove finisce
  la salita iniziale, che la prosa non dice dove sia;
- le tabelle della salita iniziale «ICP» di LIRP 6 p.3 sono disegnate, senza testo: non si leggono.

Test: Aip.Pdf 167 (+4), Aip 52, Lab 1 389 (+2).

#### 6b — il file 5, gli avvicinamenti: fatto

Il file 5 di 51 aeroporti e 10 eliporti: le carte sono immagini; le pagine di testo («TABULAR DESCRIPTION») hanno il titolo
dell'avvicinamento, una tabella per segmento, le attese, la WAYPOINT LIST, la tabella FIX/POINT; altre pagine il blocco SBAS
FAS DATA BLOCK. Scelte dell'agente (da confermare col committente):
- **Un avvicinamento è una `Procedure` di genere nuovo `Approach`, una per segmento stampato** (`Segment`: «RNAV to ILS
  TRANSITION - APPROACH FROM BENTO», «ILS to RNAV TRANSITION - MISSED APPROACH», «Transition to RNP Approach via VIFOP»;
  null per la tabella senza titolo — il finale e il mancato —, «(2)», «(3)» per le seguenti, che ricominciano da «010»).
  Il designatore dal titolo: «ILS Z RWY24», «RNP Z 164» (gli eliporti), «RNP RWY16R via SUVOK» (LIRF ne ha quattro).
- I tratti hanno in più il **numero d'ordine** e il **VPA/TCH**; le colonne di riferimento di LIRV e LIPX (radioassistenza
  raccomandata, radiale e distanza, distanza dalla soglia) in `Reference`, com'è stampate.
- La **FIX/POINT** (IAF, IF, FAF, MAPt e i punti definiti da radiale e DME, con le coordinate) è `Fixes` dell'avvicinamento;
  il **blocco SBAS FAS** è `DataBlock`, riga per riga.
- Le carte convenzionali (ILS, VOR) sono solo immagine: della loro procedura resta la FIX/POINT sul retro. Se il retro ha il
  titolo («VOR Z RWY17», LIBC) lo prende; se no il nome è la pagina della carta, e l'AIP espanso lo cambia nel designatore
  del titolo della carta in AD 2.24 («ICAO INSTRUMENT APPROACH CHART ILS Z RWY 24» → «ILS Z RWY24»). Lo stesso per le
  transizioni RNAV verso un ILS stampate senza il titolo dell'avvicinamento (LIPO, LIPX, LIPZ).
- I titoli hanno molte forme: con «Instrument Approach procedure», sotto «TABULAR DESCRIPTION» su una riga a sé («RNP - Z
  RWY 08», «PANTELLERIA RNP RWY25», «IAC RNP APCH RWY 14 LIEE», «IAP RNP APCH RWY 32 LIEE – VIA EPIDA», «RNP A RWY10»), o
  dentro una riga («TABULAR DESCRIPTION Alghero/Fertilia RNP RWY 20»). Una pagina senza tabelle (il retro di una carta, una
  pagina-carta col testo) non apre segmenti e non dà testo.
- **Il conto** (rapporto di lettura): i titoli d'avvicinamento stampati sulle pagine con la tabella contro gli avvicinamenti
  letti coi loro tratti.
- In più, letti anche nei file 4 e 6: le righe in cima a una pagina che continuano la tabella della pagina prima (prima
  finivano nelle note: LIRP 4 +9 tratti), una tabella stampata senza intestazione (LIDT 5), l'intestazione troncata
  («Terminato», LIBC 5), le coordinate con l'emisfero davanti («N 43°53’25.1’’ E 012°50’41.3’’», LIPR, LIEA), con la
  longitudine a due cifre («09°41’55.70” E», LIEO, LIMF) e con gli apici misti («00.2’'N», LIMG). I minuti a tre cifre
  («014°024’13.45”E», LIRN) restano un refuso detto.
- Sulla mappa il genere **«Avvicinamenti»**; nella scheda il segmento, la FIX/POINT, il numero di righe del blocco FAS, i
  tratti col numero d'ordine e il VPA/TCH. Non entrano nel confronto col sector (il sector li tiene altrove).

Misurato sul 2611: **procedure 2 034 → 2 274** (240 avvicinamenti, 1 044 tratti, 154 punti FIX/POINT, 44 blocchi FAS),
**attese 727 → 794**, **punti 2 192 → 2 514**; il conto degli avvicinamenti **119 stampati, 119 letti** su 49 file. Restano,
detti: 4 celle di LIMC 5 (un RF col centro su un'altra riga, due distanze «/»), 10 punti con le coordinate stampate male
(CG753, CJ516, MC722, MC724, MC744, MC906, MC907, PE502 a 60 secondi, RA761, RA763) e la FAF di LIRN 5-4 coi minuti a tre
cifre. Non letta: la tabella delle attese senza «Path Terminator» di LIDT 5 p.8 (UVIBE e AVCOL, che sono anche nel
file 4).

Test: Aip.Pdf 169 (+2), Aip 53 (+1), Lab 1 391 (+2).

#### 6c — sul banco: fatto

Sul banco (1400 px, LIRN 1/4/5/6 e LIBI 4/5/6 insieme ai PDF di prima): «81 procedure», «19 attese», «Conti dell'AIP
(sottosezioni, procedure, avvicinamenti, zone, punti, ostacoli, coordinate): 19 controllati, tutti tornano» — l'etichetta
ora dice i conti che ci sono, prima elencava solo quelli del passo 2. Nell'indice LIRN ha i sottogruppi Attese, Punti, STAR,
Avvicinamenti, Salite iniziali, SID; LIBI i suoi sotto AD 3. Gli avvicinamenti uno per segmento, i retri delle carte LOC col
nome dalla carta («LOC X RWY24», «LOC Y RWY06»). La scheda del finale di RNP Z RWY24 coi tratti numerati e il VPA/TCH; sulla
mappa la linea evidenziata arriva alla soglia. Trovati e corretti a schermo:
- il finale senza titolo dopo le transizioni si chiamava «(2)»: ora è l'avvicinamento stesso (restano «(2)» dove l'AIP
  stampa davvero più tabelle senza titolo, come le tre di Crotone);
- la soglia nei tratti («RWY24», «THR RWY 22C») non si trovava: ora è quella della pista dello scalo in AD 2.12;
- cinque attese avevano per punto una quota o una nota («FL100», «FL130», «ENR», «TCH»): l'intestazione delle attese di LIRN,
  LIME, LIMF, LICD ha «Waypoint» due volte, e si prendeva la seconda. Ora la prima, e una quota non è mai il punto. Attese
  sul 2611: 789 (cinque doppie col nome sbagliato in meno).

Test: Aip.Pdf 170 (+1), Lab 1 392 (+1).

#### Il passo 6, chiuso

«Finita quando» di §3: **per ogni scalo le procedure stampate tutte nel modello o dette.** Sul 2611: SID e STAR **1 903
designatori stampati, 1 903 letti** (109 file), gli avvicinamenti con la tabella **119 stampati, 119 letti** (49 file); le
procedure convenzionali solo in prosa sono nel modello col loro testo, e le 466 frasi di cui la prosa (F7, 7b) non ricava i
tratti sono dette una per una; le carte convenzionali solo immagine danno la loro FIX/POINT. Le attese degli scali (789 in
tutto con ENR 3.4) e i punti delle WAYPOINT LIST (2 514) sono nel modello. Dette, perché stampate male nell'AIP: le
coordinate di 25 punti (segnalazioni «punto»); da sapere: le tabelle ICP disegnate di LIRP 6 e l'attesa senza «Path Terminator» di LIDT 5 non si
leggono. Il passo 8 (i due cicli) è il prossimo; da proporre al committente, se vuole, il seguito della 7b (la prosa che
comincia dalla fine della salita iniziale, 32 SID di Alghero).
