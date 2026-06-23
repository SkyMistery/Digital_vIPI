# Specifica della Logica AoR e Visibilità — vIPI/vLOA Interactive

> ✅ **Implementata e testata.** Gli scenari **S1–S10** sono coperti da test automatici (`AorServiceTests`, `ContentServiceTests`, `RomaAorIntegrationTests`). Motore: `Vipi.Application/Aor/AorService.cs` + tabella di verità in `ContentService.cs`. Il **collasso live** dipende dal polling IVAO (fase F3, non ancora attivo): oggi la consultazione gira con `live=false`.

**Documento:** Specifica funzionale + scenari di test della logica di visibilità (la parte più critica del sistema)
**Versione:** 0.1
**Data:** 13 giugno 2026
**Riferimento:** `PIANO_vIPI_Tool.md` (§6, §20), `SPEC_Modello_Dati.md`

---

## 1. Scopo

Definire in modo non ambiguo e **testabile** come il sistema decide, per ogni `ContentBlock`, se mostrarlo **espanso** o **compresso (collasso morbido)**, dato:

- la posizione `P` aperta dall'utente,
- l'insieme `O` delle posizioni ATC online (dal polling IVAO),
- la modalità `Live` (on/off).

Questa specifica è la **sorgente dei test** (preferibilmente property-based) per `IAorService` e `IContentService`.

---

## 2. Definizioni

| Simbolo | Significato |
|---|---|
| `P` | posizione aperta dall'utente (es. `LIMM_WS2_CTR`) |
| `O` | insieme callsign ATC online (filtrate per pertinenza: subordinate di `P` + neighbour vLOA) |
| `Dom(P)` | dominio top-down di `P` = `P` + chiusura transitiva di `HierarchyRelation` sotto `P` |
| `Sec(X)` | settori posseduti dalla posizione `X` (dopo risoluzione unificazione) |
| `owner(s, O)` | posizione che possiede il settore `s` data la configurazione online `O` |
| `state(s)` | `Online` se `owner(s,O) ∈ O ∧ owner(s,O) ≠ P`, altrimenti `Covered` |

---

## 3. Algoritmo (pseudocodice)

```text
function ResolveView(P, O, Live):
    if not Live:
        return ALL blocks EXPANDED            # consultazione completa

    # 1. Risoluzione unificazione: chi possiede ciascun settore
    ownership = ApplyUnificationRules(Dom(P), O)   # sector -> position

    # 2. Stato di ogni settore nel dominio di P
    for each sector s in SectorsOf(Dom(P)):
        owner = ownership[s]
        state[s] = Online if (owner in O and owner != P) else Covered

    # 3. Decisione di visibilità per ogni blocco
    for each block b in BlocksOf(DocumentsFor(P)):
        s = b.ScopeSectorId
        if s == null or b.Visibility == Always:
            b.render = Expanded
        else:
            switch (b.Visibility, state[s]):
                (Operational, Covered) -> Expanded     # copro io, eseguo le procedure
                (Operational, Online)  -> Collapsed    # delegato a chi è online
                (Handoff,     Online)  -> Expanded     # serve il coordinamento/freq
                (Handoff,     Covered) -> Collapsed    # sono io entrambi, non serve
    return blocks
```

### 3.1 Risoluzione dell'unificazione

```text
function ApplyUnificationRules(positions, O):
    ownership = default ownership from PositionSector (ogni posizione possiede i suoi settori)
    for rule in UnificationRules(P.Fir) ordered by Priority:
        if rule.Condition matches O:
            apply rule.Assignment to ownership   # riassegna sector -> position
    # i settori la cui posizione non è online ricadono top-down sul primo antenato online (o P)
    for each sector s:
        if ownership[s] not in O:
            ownership[s] = nearest online ancestor of ownership[s] in Dom(P), else P
    return ownership
```

---

## 4. Tabella di verità del blocco (riassunto)

| `Visibility` | `ScopeSector` nullo | settore `Covered` | settore `Online` |
|---|---|---|---|
| **Always** | Espanso | Espanso | Espanso |
| **Operational** | (n/a) | **Espanso** | **Compresso** |
| **Handoff** | (n/a) | **Compresso** | **Espanso** |

In **Live OFF**: tutto Espanso, sempre.

Il "compresso" è **collasso morbido**: striscia etichettata e riespandibile, mai rimozione (sicurezza operativa, §20.3 del piano).

---

## 5. Scenari di test (casi di verità)

Ogni scenario è un caso atteso per i test automatici. Notazione: `P` = posizione aperta, `O` = online.

### S1 — Top-down completo (nessun subordinato online)
- **Setup:** `P = LIMM_WS2_CTR`; `O = {LIMM_WS2_CTR}`.
- **Atteso:** tutti i settori di Milano in stato `Covered`. Blocchi `Operational` di tutti i sotto-settori **espansi**; blocchi `Handoff` **compressi**.
- **Verifica chiave:** nessuna info operativa nascosta quando sei l'unico online.

### S2 — Subordinato APP si connette (esempio Pisa)
- **Setup:** `P = LIRR_NE_CTR`; `O = {LIRR_NE_CTR, LIRP_APP}`.
- **Atteso:** settori di Pisa → `Online`. Blocchi `Operational` di Pisa **compressi**; blocco `Handoff` Pisa (freq + coordinamenti) **espanso**. Resto del NE invariato (`Covered`).

### S3 — Esempio canonico WS2 ↔ ANE
- **Setup A:** `P = LIMM_WS2_CTR`; `O = {LIMM_WS2_CTR}` (ANE offline).
  - **Atteso:** procedure operative ANE **espanse**; blocco freq+coordinamenti verso ANE **compresso**.
- **Setup B:** `O = {LIMM_WS2_CTR, LIMC_ANE_APP}` (ANE online).
  - **Atteso:** procedure operative ANE **compresse**; blocco freq+coordinamenti ANE **espanso**.
- **Verifica chiave:** è l'inversione operativo↔handoff descritta dall'utente.

### S4 — Split di settore ACC (unificazione)
- **Setup A:** `P = LIRR_SU_CTR`; `O = {LIRR_SU_CTR}`.
  - **Atteso:** regola unificazione assegna a SU sia i settori SU che ES → entrambi `Covered`. Blocchi operativi di SU e ES espansi.
- **Setup B:** `O = {LIRR_SU_CTR, LIRR_ES_CTR}`.
  - **Atteso:** regola di split attiva → settori ES passano a `LIRR_ES_CTR` → stato `Online`. I blocchi operativi ES nella vista di SU **compressi**; handoff SU↔ES **espanso**.

### S5 — Sotto-settore TS che "ruba" all'NE
- **Setup:** `P = LIRR_NE_CTR`; `O = {LIRR_NE_CTR, LIRR_TS_CTR}`.
- **Atteso:** settori del TS → `Online`. Blocchi `Operational` taggati TS **compressi** nella vista NE; handoff NE↔TS **espanso**.

### S6 — Catena a tre livelli (ACC → APP → TWR)
- **Setup:** `P = LIRR_NE_CTR`; `O = {LIRR_NE_CTR, LIBP_TWR}` (TWR online, APP offline).
- **Atteso:** poiché `LIBP_APP` è offline, l'APP ricade top-down. Ma la TWR è online → i settori/blocchi di competenza TWR `Online`; i blocchi APP restano `Covered` (li copre l'NE finché l'APP è chiuso). Verifica che la risoluzione top-down gestisca il "buco" intermedio.

### S7 — vLOA con neighbour (cross-FIR)
- **Setup A:** `P = LIRR_SU_CTR`; `O = {LIRR_SU_CTR}` (Tunisi `DTTC` offline).
  - **Atteso:** blocchi di coordinamento della vLOA LIRR↔DTTC (`Handoff`) **compressi** (nessuno dall'altro lato).
- **Setup B:** `O = {LIRR_SU_CTR, DTTC_CTR}`.
  - **Atteso:** blocchi vLOA verso Tunisi **espansi**.
- **Nota:** il neighbour non è `LI*`; il filtro live deve includere i confinanti citati nelle vLOA della posizione.

### S8 — Modalità Live OFF
- **Setup:** qualunque `P`, `Live = false`.
- **Atteso:** **tutti** i blocchi espansi, indipendentemente da `O`. Nessuna compressione.

### S9 — Always
- **Setup:** blocco con `Visibility = Always` (es. minime di separazione generali).
- **Atteso:** sempre espanso in tutti gli scenari sopra.

### S10 — Robustezza al feed stale
- **Setup:** `O` indica `LIRP_APP` online ma è un falso positivo.
- **Atteso (comportamento, non bug):** i blocchi Pisa risultano compressi **ma riespandibili** (collasso morbido). Test UI: la striscia compressa è sempre presente e apribile ⇒ nessuna perdita di accesso all'informazione.

---

## 6. Proprietà invarianti (per test property-based)

Per qualunque `P`, `O`, configurazione:

1. **Nessuna perdita d'accesso.** Ogni blocco è sempre *raggiungibile* (espanso o compresso-riespandibile); mai rimosso del tutto in live.
2. **Esclusività operativo/handoff.** Per uno stesso settore `s` con stato definito, un blocco `Operational` e il corrispondente `Handoff` non sono mai entrambi espansi né entrambi compressi (sono in opposizione).
3. **Monotonia top-down.** Se `O' = O ∪ {Q}` con `Q` subordinato, lo stato dei settori può solo passare da `Covered` a `Online` (mai il contrario) per i settori di `Q`.
4. **Idempotenza Live OFF.** Con `Live=false`, l'output è invariante rispetto a `O`.
5. **Determinismo.** Stesso `(P, O, regole)` ⇒ stessa vista (nessuna dipendenza da ordine non specificato).
6. **Chiusura del dominio.** Solo i blocchi di documenti in `DocumentsFor(P)` e i settori in `Dom(P)` (+ neighbour vLOA) influenzano la vista.

---

## 7. Note implementative

- `IAorService.Resolve(P, O)` restituisce `Dictionary<SectorId, SectorState>` + ownership; puro e testabile (nessuna dipendenza I/O).
- `IContentService.BuildView(P, O, tier, live)` applica la tabella §4 e produce il modello di vista (blocco + flag Expanded/Collapsed + etichetta di collasso).
- Caching: l'output dipende solo da `(P, hash(O), regoleVersion, tier, live)` ⇒ cache-key componibile, invalidata al cambio cache IVAO o pubblicazione.
- Le `UnificationRule` sono dato editabile: i test caricano set di regole di esempio (Roma) come fixture.

---

*Documenti collegati:* `PIANO_vIPI_Tool.md` (§20, §22), `SPEC_Modello_Dati.md` (entità coinvolte).

---

## 8. Due collassi distinti — live/AoR vs accordion UI (round 4)

Esistono **due meccanismi di collasso indipendenti** che non vanno confusi:

1. **Collasso live/AoR** (questo documento, §3–4): guidato da `(P, O, Live)` e dalla `Visibility` del blocco. Decide cosa è espanso/compresso in funzione di chi è online. È **logica di dominio**, calcolata da `IAorService`/`IContentService`.
2. **Collasso accordion UI** (vista ridotta, `PIANO` §22.4): puramente di **presentazione**. Aprendo un elemento, gli altri fratelli si comprimono; riespandibili a mano. Non dipende da `O` e **non sovrascrive** lo stato calcolato dal punto 1.

Regola di precedenza: in **Live ON**, lo stato del punto 1 determina espanso/compresso iniziale; l'accordion UI agisce solo come interazione dell'utente sopra quello stato (e resta sempre riespandibile, coerente con il collasso morbido §4). In **Live OFF**, vale solo l'accordion UI; tutti i blocchi sono logicamente espansi e l'utente li comprime a piacere.
