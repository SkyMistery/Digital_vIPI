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
| ENR 3.4.2 attese sui radioaiuti | 129 su 51 | 68 elencati | 17 da guardare uno per uno; la colonna «rotte/procedure interessate» non letta |
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

(vuoto: si comincia al via del committente)
