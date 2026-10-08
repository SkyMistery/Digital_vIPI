# Le configurazioni possibili di un gruppo di settori — dichiarate in Struttura

**8 ottobre 2026.** Carta di lavoro (S99). Segue [`2026-10-04-copertura-unica.md`](2026-10-04-copertura-unica.md):
quella ha dato una risposta sola a «con questi aperti, chi tiene cosa»; questa dice **quali insiemi di aperti
esistono**.

---

## 1. Il fatto

In produzione, dall'8 ottobre, la Diagnostica dà un errore che tiene `/vsop/health` in Degraded:

> Trasferimento senza ripiego — LIMM — «Chiuso il ricevente il traffico va su UNICOM, ma quel punto lo copre
> qualcun altro: manca un ripiego (LIMM_WS2_CTR → LIMM_ES2_CTR)».

La sonda chiude il **solo** ricevente e lascia aperti tutti gli altri: chiuso WS2, il punto lo copre ES2. Il
committente: «È un errore: ES2 non può essere aperto se non è aperto WS2». Lo scenario che la sonda prova non
esiste, quindi non manca nessun ripiego.

Poi ha allargato il tema (8 ottobre): «Ci sono settori che possono aprire solo se ce n'è un altro aperto. Per
Milano, se non è aperto WS2, ES2 e gli altri non possono aprire; se non è aperto WS5, ES5 non può aprire. Ma
anche al contrario: se è aperto LIMF_WW0 non può aprire LIMF_WN0. Non sempre un settore non può aprire se non è
aperto il padre, quindi **la gerarchia non ci aiuta**».

Ha ragione, e i dati lo mostrano:

| Regola detta | Che cosa dice l'albero |
|---|---|
| ES5 apre solo con WS5 | il padre di ES5 è **ES2**; WS5 è una riga di ripiego sopra FL325 |
| WW0 aperto ⇒ WN0 no | WN0 è **figlio** di WW0 |
| un APP sta aperto col suo CTR chiuso | è il caso normale: il figlio apre senza il padre |

## 2. Le strade guardate

**A. «Il figlio non apre senza il padre».** Falsa in generale (gli APP), e non dice ES5/WS5 né WW0/WN0.

**B. Regole a coppie in Struttura** («apre solo con», «esclude»). Era la prima proposta. Regge i tre esempi, ma
non sa dire «LIMJ_WS0 apre solo con WW0 **o** WN0», e affianca un secondo elenco a quello che il documento ha già.

**C. Le configurazioni possibili, dichiarate per gruppo.** Proposta dal committente: «potremmo dichiarare le
configurazioni possibili per un gruppo di settori nella struttura e riportarlo in automatico nel documento».
**È la strada scelta.** Un elenco di insiemi dice tutto quello che dicono le regole a coppie e di più; le regole
si **ricavano** e si mostrano, invece di scriverle; e il documento smette di avere una copia scritta a mano.

Le configurazioni esistevano già, ma nel posto sbagliato: nel `BodyJson` della sezione `configurations` della
vIPI (blocco Aerovia, gruppi APP, vIPI APP propria). Misurato sulla copia di produzione dell'8 ottobre: le hanno
solo Milano e Brindisi.

| Gruppo | Scritte nel documento |
|---|---|
| LIMM, settori d'area | {WS2} · {ES2, WS2} · {WS2, WS5} · {ES2, ES5, WS2, WS5} |
| LIMF_WW0_APP (Torino–Genova) | {WW0} · {WW0, LIMJ_WS0} · {WN0, LIMJ_WS0} |

Le quattro di Milano dicono **da sole** le regole del committente. Le tre di Torino no, e la realtà lo conferma
(sessioni dal 6 ottobre 2025 all'8 ottobre 2026): WN0 è stato online 584 volte, **mai** con WW0, e per il 95%
del tempo senza LIMJ_WS0; LIMJ_WS0 per l'81% del suo tempo da solo. Il committente: «WW0 può essere aperto da
solo o con WS0, WN0 da solo o con WS0 accanto; WS0 da solo è valido». Quindi a Torino mancano {WN0} e {WS0}: è
il genere di buco che il disegno deve far **vedere** (§4).

## 3. Il modello

**Un gruppo** è una cosa che esiste già, in due forme:

- i **settori d'area** di un ACC (il blocco Aerovia della sua vIPI) — chiave: il codice dell'ACC;
- un **ente** con le sue posizioni (`AtcUnit` / `AtcUnitPosition`: gruppo APP nella vIPI dell'ACC, o vIPI APP
  propria) — chiave: il codice dell'ente, che non cambia mai.

**Un elenco** è la lista delle configurazioni di un gruppo: per ognuna un nome e i settori aperti, ognuno col
suo Center Point e Range. È lo stesso `AccConfiguration` che il documento usava già: si sposta, non si affianca.

**Che cosa dice un elenco** — tre regole, e sono tutta la semantica:

1. Un elenco parla dei **soli settori che nomina**. A Milano nessuna configurazione nomina `LIMM_MIL_CTR` o
   `LIMM_FSS` (hanno le loro sezioni): restano liberi, non «mai aperti».
2. Fra i settori nominati, un insieme di aperti è **previsto** se è una delle configurazioni scritte — oppure
   se è vuoto: «tutti chiusi» non si dichiara.
3. Un gruppo **senza elenco** non ha vincoli: si comporta come prima che questa tabella esistesse.

Da un elenco si ricava, per ogni settore nominato:

- **sempre con**: gli altri settori presenti in *tutte* le configurazioni che lo contengono (ES2 → WS2);
- **mai con**: i settori nominati che non compaiono mai insieme a lui (WW0 ⟂ WN0);
- **da solo**: se esiste la configurazione fatta di lui e basta.

E la domanda che serve ai calcoli — «chiusi questi, chi **non può** restare aperto?»: un settore nominato per
cui ogni configurazione che lo contiene contiene anche un chiuso. Chiuso WS2 cadono ES2, WS5 ed ES5; chiuso ES5
non cade nessuno.

⚠️ **Vincoli fra gruppi non ce ne sono** (chiesto: «no»). Un APP che apre solo con un certo CTR, se un giorno
servirà, non entra in questo modello.

## 4. Dove si usa

| Dove | Che cosa cambia |
|---|---|
| **Struttura** | Si scrivono lì, sotto il lock della struttura. Sotto ogni elenco, le conseguenze ricavate: è il modo di accorgersi che manca {WN0} leggendo «WN0: sempre con LIMJ_WS0». |
| **Banco di prova** | Gli scenari pronti vengono dalla struttura (prima: dalla vIPI pubblicata). Una combinazione non prevista si dice. |
| **Diagnostica** | La sonda di «Trasferimento senza ripiego» e la scala di risalita, chiuso un settore, chiudono anche chi senza di lui non può stare aperto. |
| **Documento** | La sezione Configurazioni legge l'elenco dalla struttura. Si congela alla pubblicazione come le altre sezioni derivate: le release già uscite restano come sono. |
| **Vista live** | «Configurazione non prevista» su un gruppo online fuori elenco. **Secondo giro**, non in questo lavoro. |

**Che cosa NON cambia: chi è online davvero.** La vista live, Aurora e ogni risoluzione su stazioni vere
continuano a usare chi c'è. ES2 è stato online senza WS2 per 140 minuti su 1089: lì il traffico va a ES2, e la
regola serve al più a dirlo. Le configurazioni possibili entrano **solo** dove il sistema si inventa uno
scenario.

⚠️ **Le esclusioni non danno forma agli scenari inventati.** «Tutti aperti» contiene WW0 e WN0 insieme, che non
è una configurazione prevista; ma con padre e figlio aperti ognuno tiene il suo, ed è la divisione più fine —
quella che la sonda vuole. Si è scelto di non toccarla: chiudere «uno dei due» vorrebbe dire scegliere quale.

## 5. Il travaso

All'avvio, una volta: per ogni gruppo **senza** elenco in struttura si copia quello scritto nella versione di
lavoro del suo documento (la bozza più recente, altrimenti la pubblicata). Idempotente: un gruppo che ha già una
riga non si tocca, nemmeno se la riga è un elenco vuoto.

Così in produzione le quattro configurazioni di Milano arrivano in struttura da sole, e il rilievo di S99
sparisce senza che nessuno riscriva niente. A Torino arriva l'elenco incompleto che c'è oggi: le conseguenze
ricavate lo mostreranno, e si aggiungono {WN0} e {WS0}.

Il `BodyJson` della sezione `configurations` resta dov'è e non lo legge più nessuno; le release congelate hanno
la loro tabella già scritta.

## 6. Le fette

1. Questa carta; il modello puro (`ConfigurazioniPossibili`) coi suoi test.
2. La tabella, la migrazione (SQLite e MariaDB), il servizio; rinomina dei nominativi e cancellazioni.
3. Diagnostica: la sonda e la scala.
4. Struttura: l'editor e le conseguenze; il banco.
5. Il documento legge dalla struttura; il travaso.

## 7. Perché il rilievo è nato l'8 ottobre

Il committente non ha toccato la struttura di Milano. Con la 1.57.0 (S97) sono però cambiati l'espansione degli
accordi e il motore di copertura, che alimentano la sonda: è la spiegazione più probabile, **non provata** —
il sito non è stato fatto girare sulla copia per vedere quale clausola fa scattare il rilievo. Il difetto della
sonda (uno scenario che non esiste) c'era comunque da prima.
