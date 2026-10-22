# «Lettura e consegna» dell'AIP espanso, rifatta (22 ottobre 2026)

Filone Sector Lab. Segue la carta [«L'AIP intero e gli emendamenti»](2026-10-21-aip-intero-e-emendamenti.md) (passi 1-8 e
8-bis).

## §0 — La domanda

Il committente, alle prove a mano A1-A4 sul 2611 completo (22 ottobre):

- **A2**: «Il campo ciclo proponeva 2610, l'ho dovuto cambiare a mano». Aveva copiato `ENR`, `AD 2`, `AD 3` direttamente
  nella cartella dei PDF, senza la cartella `AIP 2611`: senza cartelle di ciclo il Lab proponeva il ciclo di oggi, e la
  prima lettura aveva scritto i PDF del 2611 nel modello del 2610 (non registrato: poi il Lab non poteva più cambiare
  ramo). Le date delle pagine dicevano 2611: 116 pagine del 29/10/2026.
- **A3**: «una progress bar su Leggi l'AIP espanso, per capire a che sta».
- «La UI di lettura e consegna è proprio scomoda, la possiamo ripensare?» — e poi: «un modo per vedere le aree da dove
  sono prese, le rotte da dove sono prese ecc.»

## §1 — Deciso (committente, 22 ottobre)

- **D1** La pagina a tutta larghezza, senza mappa né scheda: «la mappa se la voglio guardare (o voglio sistemare
  qualcosa) vado dall'indice». La casella «Mostra l'AIP espanso sulla mappa» e «Aggiungi» (le correzioni) vanno
  nell'Indice.
- **D2** Niente campo «Ciclo»: il ciclo lo dicono le **date stampate nelle pagine dei PDF** («dalle date del PDF,
  giusto? perché altrimenti con la 2611 non ci troviamo»).
- **D3** Da dove viene ogni cosa: dall'esito si risale ai PDF (proposta dell'agente, accettata: «procedi pure»).
- **D4** L'ambiente di prova del committente si reimposta da zero (lo cancella l'agente, il 2611 lo ricopia lui).

## §2 — Le regole

Le stesse, in parole semplici, in `SectorLab-prova\REGOLE della lettura.txt`.

1. **Il ciclo delle pagine.** Ogni pagina dell'AIP porta in fondo la sua data; una pagina che non cambia tiene quella
   vecchia. Il ciclo dei PDF è quello in vigore il giorno della **pagina più recente** (`LetturaDeiPdf.CicloDellePagine`):
   nel 2611 completo il 29/10/2026, cioè il 2611.
2. **Una cartella per ciclo.** I PDF di un ciclo stanno in `<cartella dei PDF>\AIP <ciclo>` (come dal passo 7). «Leggi»
   legge solo quella cartella.
3. **Le date contro la cartella.** Leggendo `AIP 2611`: pagine di un ciclo **dopo** (una del 26/11/2026) → la lettura
   si ferma **prima di scrivere** e dice come chiamare la cartella; **nessuna** pagina del 2611 (la più recente è più
   vecchia) → si legge e si dice («va bene se i PDF che hai non sono cambiati»): i PDF di un ciclo possono non avere
   pagine nuove.
4. **I PDF sciolti.** I PDF dell'AIP fuori dalle cartelle dei cicli si leggono con «Leggi e metti nel loro ciclo»: il
   ciclo dalle date (regola 1), poi il modello, poi i PDF spostati in `AIP <ciclo>` con le loro sottocartelle; le
   cartelle rimaste vuote se ne vanno, quelle con dentro altro restano. Senza nessuna data, o con la cartella del ciclo
   che c'è già, non si sposta e non si scrive niente.
5. **Il repo prima di leggere.** Con un ciclo noto il Lab passa al suo ramo prima della lettura lunga; per i PDF sciolti
   (ciclo non ancora noto) controlla che non ci sia niente di non registrato, e passa al ramo dopo aver letto le date.
6. **L'avanzamento.** Durante la lettura: «N di M PDF · l'ultimo letto · manca circa …», con la barra. La stima viene dai
   PDF già letti: i PDF non pesano uguale, è un'idea.
7. **Da dove viene.** Ogni numero dell'esito è un riquadro: il clic elenca i PDF che l'hanno dato (quanti, le pagine, i
   conti dell'AIP); un PDF si apre nel browser. «PDF non letti» elenca quelli che il Lab non legge ancora (carte solo
   immagine, introduzioni). Una segnalazione apre il suo PDF alla sua pagina; nella scheda di una voce (Indice) «Apri il
   PDF» va alla pagina della voce, dall'indice delle pagine del ciclo. Il PDF si apre con Edge, che capisce `#page=`;
   senza Edge, il lettore di Windows dalla prima pagina.

## §3 — La pagina

- In testa il giro dell'AIP (R5). A sinistra: **I cicli** (una riga per ciclo — che cos'è oggi, quanti PDF, fino a che
  data arrivano le pagine lette, quando è stato scritto il modello, «è quello su cui lavori»; i gesti «Leggi»/«Rileggi»,
  «Indice», «Togli il …» col suo sì), i **PDF sciolti**, l'**Emendamento AIRAC**, il **repo** (Prendi, Registra, Manda).
  A destra la **lettura**: l'avanzamento mentre legge, poi l'esito a riquadri, i conti, «Rispetto al …», le date, le
  **segnalazioni** per tipo (8-bis).
- L'esito c'è dopo una lettura fatta da quando il Lab è aperto: il rapporto dei PDF non sta nel modello. Dopo un riavvio
  la pagina dice di rileggere.

## §4 — Il lavoro

Fatto il 22 ottobre in un commit: `AipEspanso.LeggiIPdf` / `Componi` (la lettura divisa in due, con l'avanzamento),
`CicliDellAip.PdfSciolti` / `MettiNelCiclo` / `Righe`, `SessioneDelLab.LeggiAsync` (le regole 3-5),
`ApriIlPdf` / `ApriLaPaginaDellAip`, `PaginaDellaLettura.razor` al posto di `AipNellaBarra.razor`, la casella della mappa
nell'Indice. Provato a schermo sul banco a 1400 px (la barra da 0 a 25 PDF con la stima, i riquadri, «aree da 8 PDF»,
l'Indice con la casella e «Apri il PDF»). Le prove a mano A ripartono dalla A1 (`SectorLab-prova\PROVE.md`).

Trovata coi test, e corretta: spostata la casella della mappa nell'Indice, che carica lo strato entrando, due
`MostraLAipAsync` potevano correre insieme e toccare le stesse collezioni da due thread (il test delle correzioni di un
punto cadeva 2 volte su 5). Ora il caricamento dello strato è in fila (`_caricaLAip`): 0 su 15.

Test: Lab 1 407 (+5: `LetturaDeiCicliTests`). Non fatto: l'esito non sopravvive a un riavvio (si rilegge);
l'avanzamento conta i PDF, non le pagine.
