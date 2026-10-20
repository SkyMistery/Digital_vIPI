# Riordino delle pagine del Sector Lab (20 ottobre 2026)

> Carta madre: [`2026-09-18-aurora-sector-lab.md`](2026-09-18-aurora-sector-lab.md). Viene dopo la F7
> ([`2026-10-19-f7-sid-e-star.md`](2026-10-19-f7-sid-e-star.md)).
> Stato: ✅ **direzione APPROVATA** dal committente il 20 ottobre (le quattro pagine: «sì, mi sembra ok, vediamo come
> viene»). Le decisioni D1-D7 di §4 sono come proposte. Il lavoro è in §6.

## §0 — La domanda

Il committente (20 ottobre), aprendo l'app: «ora è molto confusionaria. Vogliamo riorganizzarla?». Si parte dall'analisi
di tutte le operazioni, raggruppate per tipo, per decidere come dividere le pagine.

Sui due schermi il committente dice che chi li ha usa i pannelli nell'altra finestra, ma non tutti li hanno: l'app deve
andare bene **su uno schermo solo** e sfruttarne **due** quando ci sono.

## §1 — Cosa c'è (inventario del 20 ottobre, ramo `lab/f3` @ `8934f7ec`)

Una sola pagina (`Pages/Home.razor`) con la barra in alto, la colonna di sinistra (Sfoglia, Scali, Problemi), la colonna
della Scheda e delle Modifiche, la colonna degli strati e la mappa. Con «Pannelli in un'altra finestra» la colonna, la
Scheda e le Modifiche passano in `Pages/Pannelli.razor`. Le operazioni sono circa centocinquanta. Raggruppate per tipo
di lavoro:

| # | Gruppo | Operazioni | Dove stanno oggi |
|---|---|---|---|
| 1 | **Aprire il sector** | cartella, «Aperti di recente», «Prendi» dal repo, select ISC «Nomi risolti con», «Cambia cartella…» | schermata iniziale, barra |
| 2 | **Trovare e guardare** | ricerca, albero di cartelle e file, elenco dei record, Voci (come la selezione di Aurora), Scali, «Chi lo usa» del file e del colore, clic sulla mappa, Strati e generi, Colori e schema, «In vista» / «Solo questo sulla mappa» | colonna di sinistra, colonna di destra, mappa, tasti nella Scheda e nel file |
| 3 | **Modificare un record** | Campi (dieci editor diversi), Metadati, Vertici con «Incolla da testo», Linea dei `.geo`, Vincoli dei punti, Tratti, Righe a mano, Nascondi/Mostra, Nuovo record / Record come questo / Togli, modello ATIS e anteprima a voce, disegno dei simboli, slot degli stand, nome della voce e delle parti | Scheda |
| 4 | **Gesti su più record o file** | Rinomina un punto e le citazioni, La stessa forma (famiglia, Allinea, Prendi la sua, bordo e riempimento), gemello in `VFR_NASCOSTI.fix`, Aerovie del file (Sistema le etichette, Nuova, Togli), Composta da, «a tutta la voce», Correggi tutte, Sposta sopra i commenti, «allinea anche questo» | Scheda, Problemi, Modifiche |
| 5 | **Controllare** | errori e avvisi con filtri, problemi nuovi delle modifiche, note da Aurora, esito del salvataggio | linguetta Problemi, testo in barra, Scheda, Modifiche |
| 6 | **AIP** | Prendi i PDF da Download, Prendi l'AIP espanso, Leggi nell'AIP espanso, segnalazioni, Registra/Manda del repo AIP, strato sulla mappa, Aggiungi/Correggi/Togli/Ritira una voce, 13 confronti (CTR, ATZ ×2, ENR 5, SID e STAR, rotte, attese, radioassistenze ×2, punti, scali, piste, frequenze) coi gesti di riga Porta nel sector / Sostituisci / Dividi / Scrivi i tratti / Correggi l'uso / Mappa delle SID | tendina AIP della barra; la scheda di una voce AIP solo dal clic sulla mappa |
| 7 | **Salvare e consegnare** | Modifiche col diff, Salva, Annulla tutto, ↶ ↷ e Ctrl+Z/Y, Registra, delete.upd, Manda e PR, Allinea alla base, changelog (Proponi, Scrivi, Archivia), riporto master → ciclo coi conflitti, Nuovo lavoro, Passa a un altro ramo, Chiedi le novità | pannello Modifiche, tendina Ramo, Storia in barra |
| 8 | **Aurora** | Porta in Aurora (vetrina), lotto «Da guardare in Aurora» (✅/❌, Visto), Prove in Aurora (Prepara, Butta) | tendina Aurora |
| 9 | **Impostazioni** | configurazioni, tratto minimo delle etichette, VID, repo del sector, vetrina, PDF, AIP espanso, repo dell'AIP, tema, Registro, pannelli in un'altra finestra | tendina Impostazioni e tasti in barra |

## §2 — Perché è confusionaria

1. **Tutto in una pagina, nell'ordine delle slice.** Ogni lavoro ha aggiunto il suo pezzo dove c'era posto: la barra ha
   11 controlli e 4 tendine, la Scheda fino a 21 sezioni impilate (Stand e Ordine di disegno prima di Voce, Composta da
   fra Aerovie e Linea), la mappa non fa che scegliere.
2. **L'AIP è un'altra app dentro una tendina** larga 24rem, senza altezza massima: 13 confronti, fino a cinque elenchi
   di risultati, due moduli e un giro git suo.
3. **Il giro di consegna è spezzato in tre posti**: Salva sta nelle Modifiche (con i pannelli staccati, nell'altra
   finestra), Registra e Manda nella tendina Ramo, Porta in Aurora nella tendina Aurora.
4. **Le due finestre hanno barre diverse**: quella dei pannelli non ha Ramo, Aurora e AIP.
5. **Doppioni**: tre posti per un nuovo record, quattro per un nome, due editor per i vertici di un `.geo` (Linea e
   Vertici), «Chi lo usa» con lo stesso titolo per il file e per il punto, l'accensione dello strato AIP in due posti.
6. **Gesti sul file dentro la scheda del record**: «Aerovie del file» compare su ogni record di un'aerovia; la rinomina
   della voce e «a tutta la voce» toccano molti record senza che si veda.
7. **Verbi incoerenti**: per scartare «annulla», «Lascia stare», «Lascia com'era», «No, lasciale», «Torna alle
   modifiche»; «Togli» cancella un record del sector, una voce dell'AIP o solo una nota; «Registra» e «Manda» valgono per
   due repo diversi; «Porta in Aurora» copia nella vetrina, «Porta nel sector» è un gesto sul sector.
8. **I rifiuti stanno lontano dal tasto** che li ha causati (in fondo alla Scheda, in fondo alla tendina).
9. **Dal record del sector non si arriva alla voce AIP corrispondente**; il contrario solo dalla mappa.

Due **difetti** trovati nell'analisi, indipendenti dal riordino:
- **«Cambia cartella…» butta le modifiche non salvate senza chiedere** (`SessioneDelLab.Chiudi` azzera modifiche e
  storia), ed è il tasto più evidente della barra.
- Il link «aperta nel browser» della PR dell'AIP (`AipNellaBarra.razor`, `target="_blank"`) è probabilmente rifiutato da
  `Finestra.AllaRichiestaDiUnaFinestra`, che accetta solo `/pannelli`.

## §3 — La proposta

**Quattro pagine**, una per modo di lavorare, scelte con le linguette nella barra:

| Pagina | Gruppi | Cosa c'è |
|---|---|---|
| **Sector** (quella di base) | 2, 3, 4, 5 | mappa, colonna (Sfoglia, Scali, Problemi), Scheda, Modifiche |
| **AIP** | 6 | a sinistra l'AIP espanso (PDF, lettura, segnalazioni, git dell'AIP) e l'elenco dei confronti; al centro i risultati in una tabella larga coi gesti; la mappa accanto |
| **Consegna** | 7, 8 | nell'ordine vero del lavoro: le modifiche → Salva → Registra → Porta in Aurora e il lotto → Manda, PR, changelog; a parte i rami (nuovo lavoro, passa, riporto) e le prove in Aurora |
| **Impostazioni** | 9, e «Cambia cartella» | le impostazioni di oggi, il registro, il tema, i pannelli |

**La barra** si riduce a: nome del sector · ramo e stato (porta a Consegna) · errori e avvisi (portano ai Problemi) ·
↶ ↷ · **Salva (N)** · le linguette · ⚙. Uguale nelle due finestre.

**Uno o due schermi.** Su uno schermo ogni pagina sta tutta nella finestra. Con due, la finestra principale tiene la
barra e la mappa, quella dei pannelli tiene i pannelli **della pagina scelta**: Sector → colonna, Scheda e Modifiche;
AIP → AIP espanso, confronti e risultati. Le linguette sono le stesse e si seguono fra le due finestre; Consegna e
Impostazioni non hanno mappa e stanno nella finestra dei pannelli se c'è, altrimenti nella principale.

**La Scheda** per gruppi fissi, sempre nello stesso ordine: intestazione (tasti del record) · **Dati** (Campi, Metadati,
nome della voce e delle parti, Slot) · **Geometria** (Vertici, Incolla da testo, Ordine di disegno; un solo editor dei
vertici anche per i `.geo`) · **Procedura** (Vincoli, Tratti, Legami, Composta da) · **Collegamenti** (Chi lo usa, La
stessa forma, Gemello, Confine, Attesa, la voce AIP corrispondente) · **Righe del file**. I gesti sul file (Aerovie del
file, Sistema le etichette) passano al pannello del file nella colonna. Il rifiuto compare accanto al tasto.

## §4 — Decisioni

- **D1** Quattro pagine: Sector, AIP, Consegna, Impostazioni (§3), con le linguette in barra.
- **D2** Una sola mappa con un solo stato (strati, colori, vista, scelta) per Sector e AIP; cambiando pagina non si perde
  niente. Consegna e Impostazioni non la mostrano.
- **D3** Due finestre: la principale tiene barra e mappa, i pannelli seguono la pagina scelta; la barra è la stessa nelle
  due. Su uno schermo solo tutto sta nella principale come oggi.
- **D4** «Salva (N)» in barra, sempre visibile; Ctrl+S salva. «Cambia cartella» va nelle Impostazioni e chiede conferma se
  ci sono modifiche non salvate.
- **D5** La Scheda per gruppi fissi (§3); i gesti sul file nel pannello del file.
- **D6** Un vocabolario solo: **Scrivi** per ogni gesto che cambia il sector, **Lascia stare** per scartare un modulo,
  **Annulla** solo per la storia e le modifiche, **Togli** solo per il sector; per l'AIP «Togli dall'AIP espanso», per le
  note «Archivia»; «Registra/Manda il sector» e «Registra/Manda l'AIP»; «Porta in vetrina» al posto di «Porta in Aurora».
- **D7** I `data-tasto`, `data-campo` e simili restano gli stessi dove il gesto resta lo stesso: i test e l'autoprova li
  usano.

## §5 — Le slice

- **R0** I due difetti di §2: conferma su «Cambia cartella» con modifiche in sospeso; il link della PR dell'AIP.
- **R1** L'impianto: linguette e pagine, barra nuova e uguale nelle due finestre, Salva in barra con Ctrl+S, Impostazioni
  come pagina (con Cambia cartella e Registro). Sector resta com'è.
- **R2** Consegna: Modifiche, Ramo e Aurora in una pagina sola, nell'ordine del giro.
- **R3** AIP come pagina: elenco dei confronti, tabella dei risultati, moduli, git dell'AIP; mappa accanto.
- **R4** La Scheda per gruppi, i gesti sul file nel pannello del file, il legame record → voce AIP, un solo editor dei
  vertici per i `.geo`.
- **R5** I verbi (D6), i rifiuti accanto al tasto, le scorciatoie (Ctrl+F per la ricerca).

Ogni slice si prova **a schermo** sul banco, su uno e su due schermi, oltre ai test.

## §6 — Il lavoro

(da scrivere slice per slice)
