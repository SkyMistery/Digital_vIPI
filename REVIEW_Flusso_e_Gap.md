# Review del flusso utente e analisi dei gap — vIPI/vLOA Interactive

**Documento:** Confronto tra il flusso utente descritto (round 4) e i documenti di pianificazione esistenti
**Versione:** 0.1
**Data:** 16 giugno 2026
**Riferimenti:** `PIANO_vIPI_Tool.md`, `SPEC_Logica_AoR.md`, `SPEC_Modello_Dati.md`
**Stato:** Da rivedere insieme a Carmine

---

## 1. Sintesi della review

Il flusso che descrivi è coerente con l'impianto già pianificato (modello a `ContentBlock` taggati, due tier, collasso morbido, logica AoR), ma introduce o esplicita **sette elementi nuovi** che oggi nei documenti non ci sono, o ci sono solo parzialmente. In ordine di impatto:

1. **Navigazione per ACC** invece di (o oltre a) ricerca per callsign — cambio del modello di ingresso.
2. **Gerarchia di sezioni annidate fino a 3 livelli** — il modello dati oggi è piatto (un solo enum `Section`).
3. **Concetto di "remotizzato" vs "non remotizzato"** per gli avvicinamenti — oggi assente come attributo esplicito.
4. **Più sezioni AoR nello stesso documento** (una ACC + una per ogni APP remotizzato) — oggi `Aor` è una sezione singola.
5. **Template d'ordine fisso della vIPI di ACC** — oggi l'ordine non è normato.
6. **Minime di vettoramento dal sectorfile su GitHub** — oggi non modellato (e tu chiedi di documentarlo come *future*).
7. **Comportamento accordion in vista ridotta** (collasso automatico di ciò che non interessa) — oggi descritto per la modalità live, non per la ridotta statica.

Sotto, il dettaglio punto per punto.

---

## 2. Tabella di conformità del flusso

| # | Passo che descrivi | Stato nei documenti | Note |
|---|---|---|---|
| 0 | Apertura in **full documentation**, switch a *reduced* | ⚠️ Parziale | Il toggle Ridotta/Estesa esiste (RF-3), ma **non è specificato che il default è la versione estesa/full**. Da fissare. |
| 1 | Homepage con i **4 ACC** in alto (LIRR, LIMM, LIPP, LIBB) | ❌ Divergente | Il PIANO prevede ingresso **per ricerca/autocomplete di callsign** (RF-1). La navigazione a partire dai 4 ACC è un modello diverso. Va deciso se sostituisce o affianca la ricerca. |
| 2 | Selezione ACC → scelta tra vIPI ACC / aeroporto / vLOA con stato estero | ⚠️ Parziale | I dati per costruire questo menù esistono (gerarchia, `DocumentParty`, vLOA), ma **manca la "pagina indice di ACC"** come vista navigabile. |
| 3.1 | Aeroporto → documentazione aeroporto | ✅ Coperto | È la vIPI con `ScopePositionId` = aeroporto. |
| 3.2 | Avvicinamento **non remotizzato** → sua documentazione (i remotizzati stanno nelle vIPI di ACC) | ❌ Mancante | Manca l'attributo **remotizzato/non remotizzato** sulla posizione/APP. È il punto che governa *dove* vive la documentazione dell'APP. |
| 3.3 | vLOA con stato estero → documento vLOA | ✅ Coperto | `Document.Type=Vloa` + `DocumentParty` Home/Neighbour. |
| nav | Barra per cambiare ACC sempre presente | ⚠️ Parziale | Coerente con l'header descritto in §10.1 del PIANO, ma da formalizzare come navigazione persistente a 4 ACC. |

Legenda: ✅ già coperto · ⚠️ parziale, da esplicitare · ❌ assente, da aggiungere.

---

## 3. Struttura della vIPI di ACC (template d'ordine)

Hai dato un ordine preciso. Oggi `BlockSection` è un enum non ordinato e i blocchi si ordinano solo con `Order` dentro la sezione. **Manca un template/ordinamento canonico delle sezioni** per la vIPI di ACC. Proposta di mappatura sullo schema esistente:

| Ordine | Sezione descritta | `BlockSection` | Note / gap |
|---|---|---|---|
| 1 | Sommario navigabile | *(generato)* | Da rendere **dinamico** (vedi §5.1). Non è un blocco di contenuto ma una vista calcolata dall'albero delle sezioni. |
| 2 | Riquadro separazioni radar richieste | `Separations` | OK. `Visibility=Always`. |
| 3 | Disegno AoR di ACC (settori alti) | `Aor` | Shape da API IVAO `subcenters`. Vedi §4 (più sezioni AoR) e §6 (rendering interazione shape). |
| 4 | Tabella configurazioni operative | `OperationalSettings` | OK come tabella. |
| 5 | Tabella frequenze (dal DB) | `Frequencies` | OK, `Tier=Reduced`. |
| 6 | Tabella minime di vettoramento | `Separations` o nuova `VectoringMinima` | **Da modellare; implementazione future** (vedi §7). |
| 10 | Sezione coordinamenti | `Coordination` | Struttura annidata: sottosezione per settore interagente → per aeroporto → lista trasferimenti + tabella riepilogo. **Richiede sezioni annidate** (vedi §5.2). |

> **Gap di numerazione:** salti da 6 a 10. Verifica se i punti 7–9 (es. ATIS, aree/corridoi, best practice) vanno previsti nel template o se la numerazione è solo indicativa.

---

## 4. Sezioni AoR multiple nello stesso documento

**Gap rispetto al modello dati.** Oggi `Aor` è un valore singolo dell'enum `BlockSection` e non esiste un'entità "sezione AoR" con la lista dei settori di cui disegnare la shape. Tu chiedi:

- più sezioni AoR nello stesso documento (una per l'ACC, una per ogni APP remotizzato);
- analogamente più sezioni minime.

Serve quindi un blocco/sezione di tipo AoR **parametrizzato** con la lista dei settori da renderizzare. Proposta: un `ContentBlock` con `Format = AorMap` (nuovo valore) il cui `BodyJson` contiene l'elenco dei `SectorId`/`GeometryId` da disegnare e le opzioni di resa. Questo permette N sezioni AoR per documento senza cambiare la cardinalità dell'enum.

---

## 5. Funzioni di editing (admin CH/AOD)

### 5.1 Sommario dinamico
Richiesto: TOC che si aggiorna da sola. **Gap:** oggi non c'è un albero di sezioni da cui derivare la TOC; il modello è piatto. Risolto insieme a §5.2.

### 5.2 Sezioni annidate fino a 3 livelli — **gap di modello dati principale**
Oggi `ContentBlock.Section` è un singolo enum e l'annidamento non esiste. Tu chiedi sotto-sezioni, sotto-sotto-sezioni e sotto-sotto-sotto-sezioni (3 livelli sotto la sezione radice).

Proposta: introdurre un'entità **`DocumentSection`** ad albero:

| Campo | Tipo | Note |
|---|---|---|
| `Id` | int PK | |
| `DocumentVersionId` | int FK | |
| `ParentSectionId` | int? FK→DocumentSection | null = radice |
| `Title` | string | |
| `Order` | int | ordine tra fratelli |
| `Depth` | int | 0..3, vincolo applicativo a max 3 |
| `SectionKind` | enum | mappa l'attuale `BlockSection` (Aor, Coordination, ...) |

I `ContentBlock` passerebbero da `Section` (enum) a `SectionId` (FK→DocumentSection). La TOC dinamica si genera percorrendo l'albero. Questo è il cambiamento più sostanziale: tocca `SPEC_Modello_Dati.md` §3.12 e §4.

### 5.3 Tabelle con proprietà di collasso in ridotta
Richiesto: per ogni tabella, una proprietà che dica se collassare o no in modalità ridotta. **Parziale:** oggi abbiamo `Tier` (Reduced/Extended) e `Visibility` (Operational/Handoff/Always). Probabilmente basta combinarli, ma manca un flag esplicito "collassata di default in ridotta / sempre aperta". Proposta: aggiungere `CollapsedByDefault: bool` al blocco tabella (indipendente dalla logica live).

### 5.4 Sezioni AoR e minime aggiungibili dall'editor
Coerente con §4. L'editor deve poter creare un blocco AoR scegliendo i settori dall'anagrafica e un blocco minime (future).

---

## 6. Rendering delle shape AoR (interazione visiva)

Chiedi di disegnare le shape "ognuna con sfumature leggermente diverse o modi per evidenziare come interagiscono". Il PIANO §13 prevede già la vista mappa AoR da `subcenters`/`ATCPositions`, ma **non specifica la resa dell'interazione/overlap**. Proposta di esplicitare nel design (F2/F4):

- palette derivata dal brand (`--ivao-blue` e varianti light blue) con opacità/tratteggio differenziati per settore;
- evidenza degli **overlap** (dove due settori si toccano/sovrappongono) con bordo marcato o pattern;
- in modalità live, codifica Covered vs Online (es. pieno vs tratteggiato), riusando gli stati già definiti in `SPEC_Logica_AoR.md`.

---

## 7. Minime di vettoramento (documentato, **implementazione future**)

> ⚠️ Questa funzione è **documentata ora ma pianificata per il futuro**, come richiesto. Non entra nella prima release.

**Fonte dati.** Le minime di vettoramento (Minimum Vectoring Altitude / aree e relative quote) si ricavano dal **sectorfile della divisione su GitHub** (es. file EuroScope `.ese`/`.sct` o equivalente Aurora). Non arrivano dalle API IVAO né si inseriscono a mano: vanno **importate/parsate** dal repo di divisione.

**Modello proposto (future).**

- Nuovo `BlockSection`/`SectionKind = VectoringMinima` (o riuso di `Separations` con sotto-tipo).
- Possibile entità `VectoringMinimaSet` legata a un settore/area, con `Source = SectorfileGitHub`, `SourceCommit`/`SourceAiracCycle`, e righe `{ areaName, minimaFt, note }`.
- Un `SectorfileImportService` (Infrastructure) che legge il file dal repo GitHub di divisione, lo parsa e popola le minime; ri-eseguibile a ogni cambio AIRAC (si lega all'`AiracService`, §16 del PIANO).
- Come per le sezioni AoR, possono esserci **più sezioni minime** nello stesso documento (§4).

**Aperto da decidere (per quando si implementerà):** formato esatto del sectorfile, struttura del repo, e se il parsing è schedulato o on-demand.

---

## 8. Vista ridotta — comportamento accordion

Hai descritto la ridotta con: tabella frequenze, tabelle trasferimenti, e una sezione per selezionare rapidamente gli aeroporti sotto il proprio settore ACC; più l'**auto-collasso** di ciò che non è più di interesse quando se ne apre un altro, con riespansione manuale.

- Frequenze + trasferimenti in ridotta: ✅ già coperto da `Tier=Reduced`.
- **Selettore rapido aeroporti sotto l'ACC:** ❌ non presente come componente; va aggiunto (lista navigabile degli APP/TWR del dominio top-down).
- **Accordion con auto-collasso:** ⚠️ il "collasso morbido" è oggi definito per la **modalità live** in funzione dell'AoR (`SPEC_Logica_AoR.md`). Qui invece descrivi un accordion **lato UI**, indipendente dal live: aprendone uno, gli altri si comprimono. Sono due meccanismi diversi che convivono — va chiarito che il collasso "da interazione UI" è puramente di presentazione e non sovrascrive lo stato live.

---

## 9. Riepilogo dei gap sul modello dati

Modifiche da riportare su `SPEC_Modello_Dati.md` se confermi il flusso:

1. **`DocumentSection` (nuova entità ad albero, max 3 livelli)** e migrazione di `ContentBlock.Section` → `ContentBlock.SectionId`. *(impatto alto)*
2. **Attributo `IsRemotized` (o `ApproachKind: Remotized | Standalone`) sulla `Position`** di tipo APP. Governa se la doc dell'APP vive nella vIPI di ACC o in un documento proprio. *(impatto medio)*
3. **`Format = AorMap`** con `BodyJson` = lista settori/geometrie, per supportare **N sezioni AoR** per documento. *(impatto medio)*
4. **`CollapsedByDefault: bool`** sui blocchi tabella per la resa in ridotta. *(impatto basso)*
5. **`VectoringMinima` + `VectoringMinimaSet` + `SectorfileImportService`** — *documentato, implementazione future*. *(impatto medio, rinviato)*
6. **Default di tier = Estesa/Full** all'apertura (config, non schema). *(impatto basso)*

---

## 10. Decisione principale — ✅ RISOLTA: convivono

> **Esito (16 giu 2026):** la navigazione a 4 ACC **convive** con la ricerca/rilevamento postazione. La home a 4 ACC è la porta d'ingresso documentale; tutta la logica live/AoR resta agganciata alla postazione aperta dall'utente.

Il punto da sciogliere era: **la navigazione per 4 ACC sostituisce la ricerca per callsign, oppure le due convivono?**

- *Sostituisce:* l'homepage diventa i 4 ACC; l'ingresso "apro la mia postazione e vedo solo ciò che mi serve" (RF-1, RF-5/RF-6, modalità live) diventa un percorso secondario o sparisce. Più semplice da navigare come libreria documentale, ma si perde l'ingresso operativo "centrato sulla mia posizione".
- *Convivono:* homepage con i 4 ACC come **navigazione documentale** (consultazione/lettura), e in più il rilevamento "sei connesso a X → vista live ridotta della tua posizione". È l'opzione più completa e quella che valorizza il lavoro già fatto sulla logica AoR.

La mia raccomandazione è **convivono**: la navigazione a 4 ACC è la porta d'ingresso per la consultazione, mentre tutta la logica live/AoR resta agganciata al rilevamento della postazione aperta dall'utente.

---

## 11. Proposte di quality-of-life (extra)

Idee per chi *usa* e chi *mantiene* il sistema, oltre a quanto già in §13 del PIANO:

1. **Breadcrumb gerarchico** (ACC › Aeroporto/APP › Sezione) sempre visibile, così l'utente sa dove si trova nella navigazione a 4 ACC.
2. **Deep-link a sezione/blocco** (`/sop/LIRR#coordinamenti-pisa`): copiabile e condivisibile in chat/Discord durante una sessione, fondamentale per i briefing rapidi.
3. **Ricerca full-text trasversale** dentro le vIPI/vLOA (CoP, FIX, callsign), utile anche se l'ingresso principale è a 4 ACC.
4. **"Cosa è cambiato dall'ultimo AIRAC"**: vista che evidenzia i blocchi modificati nell'ultimo ciclo (sfrutta il versionamento già previsto).
5. **Indicatore di freschezza del sectorfile** nelle sezioni minime: mostrare commit/AIRAC della fonte GitHub, così l'utente sa quanto sono aggiornate (lega §7).
6. **Pannello "chi è online ora nel mio dominio"** in ridotta/live: lista compatta delle posizioni subordinate aperte, con un clic per saltare al loro handoff.
7. **Validazione editor in tempo reale dei CoP**: quando l'autore scrive un trasferimento, segnalare CoP/FIX non presenti nel nav-data (estende la validazione semantica §17.2).
8. **Anteprima "vista controllore"** nell'editor: simulare `P` + `O` (chi è online) per vedere come collasserà il documento in live prima di pubblicare.
9. **Stampa/PDF della sola vista ridotta** come "kneeboard" di posizione (estende l'export briefing già accettato).
10. **Toggle densità** (compatta/comoda) per le tabelle, utile su tablet in cabina.

---

## 12. Prossimi passi proposti

1. Sciogliere la decisione di §10 (navigazione 4 ACC: sostituisce o convive).
2. Se confermi i gap, aggiorno `SPEC_Modello_Dati.md` con `DocumentSection`, `IsRemotized`, `Format=AorMap`, `CollapsedByDefault` e la sezione minime marcata *future*.
3. Aggiorno `PIANO_vIPI_Tool.md` con il template d'ordine della vIPI di ACC (§3) e il comportamento accordion della ridotta (§8).
4. Aggiungo a `SPEC_Logica_AoR.md` la distinzione tra collasso "live/AoR" e collasso "UI accordion".

> Nota: nessuno di questi gap mette in discussione l'architettura (Clean Architecture, SQLite, SSE, logica AoR). Sono estensioni del modello dei contenuti e della navigazione, non riscritture.

---

## 13. Esiti delle decisioni (round 4 — 16 giugno 2026)

| Tema | Esito |
|---|---|
| §10 Navigazione 4 ACC vs ricerca | **Convivono** (navigazione documentale + live agganciata alla postazione). |
| QoL-1 Breadcrumb gerarchico | ✅ Accettata. |
| QoL-2 Deep-link a sezione/blocco | ✅ Accettata. |
| QoL-3 Ricerca full-text | ✅ Accettata. |
| QoL-4 "Cosa è cambiato dall'ultimo AIRAC" | ✅ Accettata **ma come pagina a parte**, non dentro il documento (per non disturbare visivamente). |
| QoL-5 Freschezza sectorfile | ✅ Mostrare l'**AIRAC del sectorfile** per verificare l'allineamento con il documento. |
| QoL-6 Pannello "chi è online nel mio dominio" | ✅ Accettata. |
| QoL-7 Validazione CoP in editor | ✅ Accettata; **fix presi dal sectorfile**. Nota: esistono CoP fittizi tipo `Jx` che **non sono fix reali** → il validatore non deve segnalarli come errori (vedi §14). |
| QoL-8 Anteprima "vista controllore" nell'editor | ✅ Accettata. |
| QoL-9 Stampa/PDF | ⚠️ Modificata: **solo la versione Estesa** è esportabile in PDF (niente export della ridotta/kneeboard). |
| QoL-10 Toggle densità tabelle | ✅ Accettata. |

---

## 14. Nuovo requisito — blocchi callout colorati

Richiesto: poter inserire **blocchi informativi colorati**, piazzabili ovunque serva nel documento, in quattro varianti semantiche allineate al brand (§15.1 del PIANO):

| Variante | Colore brand | HEX |
|---|---|---|
| **Info** | Info Blue | `#7EA2D6` |
| **Success** | Green | `#2EC662` |
| **Warning** | Yellow | `#F9CC2C` |
| **Danger** | Red | `#E93434` |

Modellazione: nuovo `BlockFormat = Callout` con `CalloutKind ∈ {Info, Success, Warning, Danger}` (vedi `SPEC_Modello_Dati.md`). È un `ContentBlock` come gli altri, quindi assegnabile a qualsiasi sezione e a qualsiasi profondità, con `Tier`/`Visibility` propri.

> Nota brand: §15.1 del PIANO indica i colori semantici "solo per interazioni/stati". I callout sono un'estensione d'uso deliberata di questi colori a contenuto editoriale — da annotare nella guida di stile per coerenza.

---

## 15. Validazione CoP e CoP non-fix (dettaglio QoL-7)

I CoP (Coordination Points) usati nei trasferimenti vanno validati contro i **fix del sectorfile** di divisione (stessa fonte delle minime, §7). Attenzione: non tutti i CoP sono fix reali — esistono punti convenzionali tipo `Jx` (es. `J1`, `J2`...) definiti operativamente ma **assenti dal nav-data**. Il validatore semantico deve quindi:

- accettare come validi sia i fix reali del sectorfile sia i **CoP convenzionali** definiti in un'apposita lista/whitelist editabile;
- segnalare (warning, non blocco) solo i token che non rientrano in nessuna delle due categorie.
