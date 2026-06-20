# HANDOFF — vIPI/vLOA Interactive

**Ultimo aggiornamento:** 20 giugno 2026
**Scopo:** dare a una nuova chat tutto il contesto per riprendere senza rileggere l'intera cronologia.
**Stato progetto:** design UI **completo** sia in consultazione sia in editing (mockup **v2**, **17 schermate** interattive, incl. editor node-driven, Topologia settori con simulatore, editor trasferimenti, editor vLOA e vista **AoR 3D** ruotabile). Codice **non** ancora iniziato. La UI verrà **rifinita più avanti**, quando si inizia a collegarla ai dati reali e si capisce meglio il funzionamento live.

---

## 1. In una frase

Portale web interattivo che trasforma le **vIPI** (istruzioni operative ATC) e le **vLOA** (lettere di accordo) della divisione IVAO Italia da documenti Word statici a documentazione strutturata, con due livelli di dettaglio (Estesa/Ridotta), logica di visibilità live legata a chi è online (AoR top-down) ed editing per i ruoli staff (CH/AOD).

---

## 2. Indice dei documenti (tutti in questa cartella)

| File | Contenuto |
|---|---|
| `PIANO_vIPI_Tool.md` | Piano e architettura completi. **Round più recenti: §22 (round 4), §23 (round 5 — integrazione/auth).** |
| `SPEC_Modello_Dati.md` | Modello dati / schema EF Core. **§7 = aggiornamenti round 4.** |
| `SPEC_Logica_AoR.md` | Logica di visibilità AoR + scenari di test. **§8 = collasso live vs accordion UI.** |
| `REVIEW_Flusso_e_Gap.md` | Confronto flusso utente vs documenti, gap e proposte QoL. **§13–15 = decisioni round 4 + callout + validazione CoP.** |
| `docs/adr/ADR-0001-scelte-architetturali-fondanti.md` | 10 decisioni fondanti. **D1/D7/D9 emendati da ADR-0002.** |
| `docs/adr/ADR-0002-integrazione-e-autenticazione-portabile.md` | Integrazione come RCL Blazor + auth portabile (3 scenari). |
| `mockups/vipi-ui-mockup-v2.html` | **Mockup CANONICO**. **17 schermate** interattive (consultazione + editing + AoR 3D), brand IVAO. Vedi §7 e **§7.1** (aggiornamenti 20 giu). |
| `mockups/vipi-ui-mockup.html` | Mockup v1 (5 schermate). Storico, superato da v2. |
| `Esempi documenti/*.docx` | Esempi reali di vIPI/vLOA (fonte del contenuto, non importati automaticamente). IPI MILANO ACC.docx = riferimento per tabelle config/frequenze/separazioni. |

> Ordine di lettura consigliato per una nuova chat: questo HANDOFF → ADR-0001/0002 → PIANO §22–23 → mockup **v2**.

---

## 3. Decisioni chiave già prese (non rimetterle in discussione salvo richiesta)

**Architettura**
- Stack: **C# / ASP.NET Core**, Clean Architecture a 4 layer (Domain/Application/Infrastructure/Web-UI).
- DB: **SQLite** + EF Core, migrazioni versionate, concorrenza ottimistica.
- UI: **componenti Blazor impacchettati come Razor Class Library (RCL)** — *non* Razor Pages (emendamento ADR-0002).
- Integrazione: la vIPI è una **RCL montata in-process** nel sito host; **eredita l'autenticazione** dell'host via astrazione `ICurrentUserProvider`. Niente API esposte, niente doppio login.
- Portabilità: 3 scenari con stessa codebase → **A** sito attuale, **B** sito nuovo (stesso stack), **C** app autonoma futura (host minimo + OIDC proprio). Regola: RCL/logica non dipendono da tipi specifici dell'host.

**Dominio / contenuti**
- Contenuto strutturato: albero `DocumentSection` (annidamento **max 3 livelli**) + `ContentBlock` taggati con `Tier` (Reduced/Extended), `Visibility` (Operational/Handoff/Always), `ScopeSectorId`, `Format` (Table/Prose/Image/List/**AorMap/Callout**).
- Visibilità live = collasso **morbido** (mai rimozione) secondo tabella di verità in `SPEC_Logica_AoR.md`. Due collassi distinti: live/AoR (dominio) e accordion UI (presentazione).
- Gerarchia top-down, ownership settori e regole di unificazione = **dato manuale** curato dagli editor (le API IVAO non lo espongono).
- **Single source of truth Estesa/Ridotta:** non esistono due documenti. C'è **un'unica struttura** (albero + blocchi); la Estesa è la resa completa, la **Ridotta è una proiezione** dello stesso dato (filtrata per `Tier`/posizione live + collasso morbido). Il dato si inserisce **una volta sola**.
- **Trasferimenti come righe strutturate:** per alimentare entrambe le viste, ogni trasferimento è un record (CoP, FL, flusso/destinazione, **catena ordinata di handler** + trasferimento *standard* di fallback), non una tabella libera. Estesa = raggruppa per settore interagente; Ridotta = risolve la catena in base a chi è online (primo online; se nessuno → standard, poi UNICOM).

**Funzionalità & UX (round 4)**
- Navigazione: home con **4 ACC** (LIRR/LIMM/LIPP/LIBB) + ricerca callsign + live, **coesistono**. Default apertura = **Estesa**.
- APP **remotizzati** → doc nella vIPI di ACC; **non remotizzati** → documento proprio (`Position.ApproachKind`).
- Template d'ordine vIPI ACC: sommario → separazioni → AoR → config → frequenze → minime → coordinamenti (annidati per settore→aeroporto→trasferimenti+CoP).
- **Più sezioni AoR e più sezioni minime** per documento.
- **Callout colorati** (Info/Success/Warning/Danger) piazzabili ovunque.
- QoL accettate: breadcrumb, deep-link a sezione, ricerca full-text, pannello "chi è online nel mio dominio", anteprima "vista controllore" nell'editor, toggle densità tabelle.
- "Cosa è cambiato dall'ultimo AIRAC" = **pagina a parte**, non dentro il documento.
- Export PDF = **solo Estesa**.

**Autenticazione (dal codice del sito host `Ivao.It`)**
- Host = ASP.NET Core + Blazor Server + ASP.NET Core Identity, IVAO OIDC come external login.
- Identità nei **claim di sessione**: `id` (vid), `centerId` (FIR), `divisionId`, `userStaffPositions` (es. `IT-DIR`). CH/AOD rilevabili da lì → **non serve** la chiamata API `userStaffPositions` negli scenari embedded.
- Staff position IT già mappate a **ruoli Identity** dal sito (`IvaoRolesHandler`).

---

## 4. Decisioni ancora APERTE (da affrontare)

1. **Minime di vettoramento + SID** — modellate ma **implementazione FUTURE**. Fonte = **sectorfile divisione su GitHub** per *entrambe* (formato/struttura repo/schedulazione del parsing da definire). Le **SID di ogni aeroporto si prendono sempre dal sectorfile su GitHub**, non si inseriscono a mano; la tabella si allinea all'AIRAC del sectorfile.
2. **Live: SSE vs circuito Blazor** — su host Blazor Server gli aggiornamenti push viaggiano nativamente sul circuito; decidere se SSE (ADR-0001 D6) serve ancora. → ADR successivo.
3. **Codici staff position esatti CH/AOD** da mappare a `CanEdit` (verificare gli `staffPositionId` reali).
4. ~~**Numerazione template vIPI ACC**~~ ✅ **Risolto (20 giu):** ordine in PIANO §22.3 → …(7) coordinamenti, (8) settore SCCAM, (9) aree regolamentate. Eventuali ATIS/best-practice restano aggiungibili come sezioni opzionali.
5. **Dove generare lo scheletro .NET** — domanda posta, non ancora risposta (nuova solution separata vs dentro `Ivao.It` vs solo struttura su carta). L'utente ha preferito **progettare la UI prima** (fatto).
6. **Registrazione app IVAO** — l'utente ha le credenziali; redirect URL serve solo per lo scenario C (app autonoma). Per A/B non necessario.

---

## 5. Prossimi passi proposti

1. ~~**Progettare l'EDITOR**~~ ✅ **FATTO** (sessione 20 giu): editor node-driven, Topologia settori con simulatore, editor trasferimenti, editor vLOA. Vedi §7.
2. **Scaffolding solution .NET** (F0/F1) — **prossimo passo principale**: `Vipi.Domain`, `Vipi.Application`, `Vipi.Infrastructure`, `Vipi.Ui` (RCL) + test; astrazione `ICurrentUserProvider` con i due adapter (host / OIDC); modello di dominio round 4; schema EF Core + prima migration SQLite; tema brand; seed strutturale FIR **Roma** (pilota).
3. **Derivare i componenti Blazor** dalle schermate v2 approvate, collegandoli al dominio e al polling IVAO.
4. **Rifinitura UI** — **rimandata di proposito**: si raffina quando la UI è funzionante e si capisce il comportamento live reale (l'utente l'ha chiesto esplicitamente).
5. **ADR successivi**: SSE vs circuito Blazor; formato shape AoR (GeoJSON vs WKT); parsing sectorfile (quando si fanno le minime).

### 5.1 Placeholder UI da collegare ai dati veri (wiring, non schermate mancanti)
- **Mappe AoR**: ora SVG statici → geometria reale dal database IVAO (GeoJSON/WKT).
- **Minime di vettoramento (carte MVA)**: "in sviluppo" → parsing sectorfile GitHub.
- **METAR/TAF, lista online, collassi live**: ora mock → polling IVAO + motore AoR.
- **SID**: tabella mock → sectorfile GitHub.

---

## 6. Note operative per la nuova chat

- **Cartelle connesse:** `vIPI Ivao Italy` (progetto, questa) e `Ivao Italy site` (codice del sito host `Ivao.It` — utile per riferimenti su auth/claims/struttura).
- **FIR pilota:** Roma (LIRR). Validare modello e logica AoR su una sola FIR prima di estendere.
- **Brand obbligatorio:** palette §15.1 del PIANO (blu `#0D2C99`, light blue `#3C55AC`, ecc.), font Nunito Sans (titoli) + Poppins (testo).
- **Nessun import dai docx:** gli esempi servono come riferimento di contenuto; i dati si inseriscono a mano tramite editor.
- **Parte più rischiosa:** la logica AoR/visibilità → coprire con test (preferibilmente property-based) sugli scenari S1–S10 di `SPEC_Logica_AoR.md`.

---

## 7. Mockup v2 — schermate e decisioni UI (sessione 19 giu 2026)

> ⚠️ **Alcune voci di questa sezione sono superate da §7.1 (20 giu):** Aree regolamentate e SCCAM non sono più gruppi dei Coordinamenti ma **sezioni top-level**; "METAR live" → **METAR & TAF**; "Confine · UNICOM" → **UNICOM**; gli APP non remotizzati usano la struttura TW1 (verso ACC / verso torri). Sezione tenuta come storico.

File: `mockups/vipi-ui-mockup-v2.html`. Barra grigia in alto = navigatore del prototipo (non fa parte del prodotto). Top bar blu **sticky** col toggle Estesa/Ridotta. Container fluido fino a ~1640px.

**Schermate (10):** 1 Home (4 ACC + ricerca + pagine) · 2 Landing ACC · 3 vIPI ACC Estesa · 3b vIPI Aeroporto · 3c APP non remotizzato · 3d vLOA estera · 4 Vista Ridotta · 5 Editor (illustrativo) · 6 Cosa è cambiato (AIRAC) · 7 Ricerca full-text.

**vIPI ACC Estesa (screen 3):**
- Separazioni: sottosezioni **Standard** e **Ridotta** (con condizioni di riduzione).
- AoR: toggle per mostrare/nascondere settori + **selettore Configurazioni** che evidenzia le righe corrispondenti nella tabella Configurazioni operative (mappatura config→righe da formalizzare: a mano o derivata dai settori attivi).
- Configurazioni operative: 4 colonne **Settore Unificato (cella unica per gruppo) | Settore | Center Point | Range** (stile IPI Milano).
- Frequenze: **Settore unico (cella unica) | Posizione | Callsign | Frequenza**; principale evidenziata (★), scelta nell'editor.
- Minime di vettoramento = **mappe** (carte MVA), da sectorfile GitHub.
- Coordinamenti: gruppi **Settori ACC / Settori APP / vLOA estere / Aree regolamentate**; settore → flusso (Dest/DEP/OVF) con prosa + tabella CoP/FL/Next + immagini + **tip** (riquadro navy). Gli APP (es. TW1) hanno sezione **VFR** = solo paragrafo di gestione (punti/codici stanno nell'aeroporto). Espandi/Comprimi tutto: globale, per gruppo e per singolo settore. Aree regolamentate: shape + range quota + descrizione.

**vIPI Aeroporto (3b):** METAR live + decodifica · Quote di transizione (TA + tabella TL per fasce QNH) **affiancate** alle Frequenze · Piste (TORA/LDA/APP proc/Patterns/Circling) con **suggerimento pista dal vento** (dep/arr ricalcolati) · SID con **selettore pista** (default = pista partenze), **ricerca**, colonna **Transition** (una riga per coppia SID+transition), preferenziali in cima.

**Vista Ridotta (4):** centrata. Switcher rapido **"Il mio settore" ↔ aeroporto** (gli aeroporti sotto controllo diretto; quelli passati a una posizione online diventano "delegato" ma restano selezionabili in sola lettura). Vista rapida aeroporto = TA, TL, piste suggerite, **selettore pista** + tabella SID (Punto/SID/Transition/Salita iniz./Cat./WTC). Sezioni indipendenti (Frequenze e Trasferimenti aperti insieme).
- **Trasferimenti (cuore della Ridotta):** maxi-sezione = **relazione FIR↔FIR** (Roma↔Milano, Roma↔Padova, Roma↔Roma interno, Roma↔estero). Dentro: sottosezioni **Arrivi** poi **Partenze**; in ciascuna **una card per aeroporto** (arrivi = aeroporto di destinazione; partenze = aeroporto di origine). Riga = **CoP · FL(↑/↓) · → settore successivo**.
- Risoluzione "settore successivo" sugli online (toggle WS2/ES2/TS/DTTC…): primo della **catena** online; per i CoP la catena può differire (es. LIMC: VALMA→WS2 sempre; DEVOX/RIXUV→ES2 se aperto, sennò WS2).
- **Interni vs esterni:** gli interni all'ACC si mostrano **solo se** qualcuno della catena è online (altrimenti li tieni); gli esterni si mostrano **sempre** (rilascio al livello giusto, "→ Confine · UNICOM" se nessuno online).

**vLOA (3d):** documento bilaterale, transfer points per direzione (LIRR→DTTC / DTTC→LIRR), coordinamento. **APP non remotizzato (3c):** doc proprio (separazioni, AoR a cerchio, frequenze, VFR-paragrafo, minime-mappe, coordinamenti verso ACC/TWR). **Cosa è cambiato (6):** lista con badge Modificato/Aggiunto/Rimosso + diff inline. **Ricerca (7):** risultati con snippet evidenziati e filtri.

**Note di stile:** valori numerici grandi (TA, FL separazioni, piste) sono stati rimpiccioliti su richiesta. Lessico: "Vento in prua" (non "testa-vento").

---

## 7.1 Aggiornamenti UI — sessione 20 giugno 2026

Il mockup `vipi-ui-mockup-v2.html` è cresciuto da 10 a **17 schermate** (chip nel navigatore in alto). Riepilogo modifiche e nuove schermate:

**Modifiche a schermate esistenti**
- **vIPI ACC Estesa:** **SCCAM** e **Aree regolamentate** sono ora **sezioni top-level a sé**, *fuori* dai Coordinamenti (prima erano gruppi dentro Coordinamenti). SCCAM = AoR dal DB IVAO + descrizioni; Aree regolamentate = shape + range + descrizione.
- **vIPI Aeroporto (3b):** la sezione meteo è ora **METAR & TAF** con toggle; il TAF mostra validità + timeline dei gruppi di cambiamento (BECMG/TEMPO) decodificati.
- **APP non remotizzato (3c):** tolta la separazione netta "verso ACC / verso TWR"; ora come **TW1**: una sezione **Trasferimenti verso ACC** e una **verso le torri** (una sotto-sezione per torre se l'APP copre più scali, es. Catania).
- **vLOA estera (3d):** ricostruita con struttura completa (EN): **Purpose · Areas of Responsibility (2 AoR: IT + estero) · Frequencies (2 tabelle) · General procedures · Coordination (identica ai Coordinamenti ACC) · Military areas coordination and management · Validity and Revision**.
- **Vista Ridotta (4):** più **compatta e a tutta larghezza**; sezioni piccole affiancate in alto; relazioni FIR↔FIR in **masonry**. I **settori di destinazione hanno un colore stabile** (WS2 blu, ES2 verde, CE1 viola, TS arancio, DTTC rosso, **UNICOM** grigio): si nota a colpo d'occhio quando cambia il "next". (Etichetta "Confine · UNICOM" → solo **UNICOM**.)

**Nuove schermate**
- **5 · Editor admin** — ora **node-driven**: selezioni un nodo nell'albero → canvas + proprietà cambiano per tipo (prosa/tabella/frequenze con principale ★/AorMap/SCCAM AoR+testo/aree/sezioni-gruppi/settori-flussi coordinamento/minime future). CTA verso Topologia e Trasferimenti; toolbar bozza/validato/pubblica.
- **5b · Topologia settori** — albero gerarchia/copertura + editor regole (unificazioni figlio⊂genitore, adiacenze condizionate, neighbour vLOA) + **simulatore live** che calcola l'ownership risolta (Covered/Online) ed effetti su blocchi/tabelle, con preset scenari **S1–S7**. È la dimostrazione visiva della logica di `SPEC_Logica_AoR.md`.
- **5c · Editor trasferimenti** — inserimento trasferimenti come **righe strutturate** (CoP · FL · catena di handler ordinata · fallback std), tabella per relazione FIR↔FIR, form "nuova riga" con costruttore catena e **anteprima di risoluzione** Estesa vs Ridotta su online. Migrazioni/collassi derivano dalla Topologia, non si scrivono qui.
- **5d · Editor vLOA** — stesso motore dell'editor (mount riutilizzabile), albero sezioni vLOA; banner bilaterale **Home LIRR editabile / Neighbour DTTC sola lettura**.
- **5e · Bozze & versioni** — stato documenti (bozza/pubblicato/programmato), storico versioni con confronto/ripristino, diff e pubblicazioni programmate per ciclo AIRAC.
- **4b · Vista Ridotta APP** — Ridotta per una posizione di avvicinamento (LIRP): frequenze, vista rapida aeroporto, trasferimenti verso ACC e verso le torri (badge colorati).
- **8 · Stati & messaggi** — galleria stati: nessun ATC online, feed live stale, METAR/TAF non disponibile, documento assente, accesso sola lettura, collasso morbido.
- **9 · Export PDF** — export **solo Estesa** con opzioni e anteprima a "foglio".
- **🧊 AoR 3D** — vista tridimensionale dei settori come **volumi estrusi** dalla quota inferiore a quella superiore, **ruotabile** (orbit manuale, zoom rotellina), con legenda/toggle per settore. Mostra le interagenze laterali e verticali (es. NE basso sotto NE alto). Usa **Three.js r128** da CDN (caricato solo lì), init lazy all'apertura, **fallback** se la libreria non è disponibile. Dati settori illustrativi → shape reali dal DB IVAO. Entry point: bottone "Vista 3D ↗" nella sezione AoR della vIPI ACC.
  - **Costo/performance:** impatto **server nullo** (rendering client-side su GPU; Three.js ~150 KB gzip, una tantum + cache). Rendering **on-demand** (disegna solo all'interazione, niente loop continuo a riposo). In produzione (Blazor): caricare Three.js **lazy solo sul componente AoR 3D** e valutare il **self-host** del file invece del CDN. Alternativa più leggera ma meno fedele: pseudo-3D in CSS.

**Note tecniche mockup**
- L'editor è generalizzato in una funzione `mount(tree, canvas, props, nodes, default)` riusata da vIPI ed vLOA; handler delegati su canvas/props (toggle, segmented, data-goto, radio frequenza principale).
- Decisioni di dominio confermate da queste schermate: SCCAM e Aree regolamentate sono **sezioni di pari livello** (non coordinamenti); la vLOA ha **due AoR e due tabelle frequenze**; gli APP non remotizzati separano i trasferimenti **verso ACC** e **verso torre/i**.
- ⚠️ Interattività **simulata** con dati mock: la prova reale arriva con i componenti Blazor + dati (vedi §5.1).
