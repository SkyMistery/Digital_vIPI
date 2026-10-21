# Via le note dei dati strutturati — carta (21 ottobre 2026)

**S123**, filone Sito. Il committente, chiedendo i link ovunque si scrive testo (S122): le note «devono sparire, dove
voglio scrivere testo seleziono paragrafo o quello che è e lo metto». Scelte da lui, tutte e quattro: la nota delle
**LVP**, le note delle **regole piste**, le note delle **tabelle delle aree**, le note dei **trasferimenti**. Il testo
già scritto va in un paragrafo della sezione, come per la tecnica del radar di torre (S118).

## 1. In tre passi

| Passo | Che cosa | Stato |
|---|---|---|
| **S123a** | Nota delle LVP, note delle regole piste | §2 |
| **S123b** | Note delle tabelle delle aree: spazi aerei dell'AoR (vIPI APP e ACC), aree di lavoro e BOAT del vSOP militare | §3 |
| **S123c** | Note dei trasferimenti | §4: elenco letto, poi tolte senza portarle nei documenti (committente) |

Come per la torre radar, le **colonne si tolgono dopo**, con una migrazione in una consegna successiva a quella che
porta il trasloco in produzione.

## 2. S123a — LVP e regole piste

- **Le sezioni diventano «scheda + blocchi»** (`HB`): `lvp` e `runwayrules`, nella vIPI d'aeroporto e nel vSOP
  militare. Sotto la tabella si scrive coi blocchi del documento (paragrafo, callout, tabella, immagine, allegato),
  come in «Validità e revisione».
- **Il segnaposto non si vede più nell'editor**: le sezioni rese dalla pagina nascono con un blocco tabella senza testo
  né JSON, e in una sezione `HB` l'editor lo mostrava come una tabella senza colonne. `DocumentSectionsEditor` ora lo
  salta (solo le tabelle vuote: un paragrafo appena aggiunto nasce vuoto anche lui).
- **Via i campi**: la nota delle LVP (`AirportLvpEditor`), la nota di ogni regola, il suo riassunto e la nota nella
  prova delle regole (`AirportRunwayRulesEditor`). I modelli d'editor non la portano più (`LvpEdit`, `RuleEdit`).
- **Il trasloco** all'avvio, `IDocumentMaintenance.NoteDelloScaloNeiBlocchiAsync`, dopo `AddMissingCatalogSections`:
  la nota delle LVP diventa un paragrafo della sezione `lvp`; le note delle regole UN paragrafo della sezione
  `runwayrules`, un elenco con una riga per regola — `- #3 Notte: solo dalle 22` (il numero è quello della tabella).
  Nella vIPI e nel vSOP dello scalo, ultima versione e pubblicata corrente; poi le colonne si svuotano, solo se il
  testo è arrivato nell'ultima versione di almeno un documento.
- **Le release già uscite** portano la nota nella loro fotografia (`AirportLvpView.Note`, `AirportRuleRowView.Note`):
  la lettura la mostra ancora, e la colonna «Note» delle regole compare solo se una riga ne ha una. Le release nuove
  la portano vuota.
- ⚠️ **Unioni**: con la sezione `HB` la scheda «sezioni in comune» non propone più di nascondere LVP e regole piste
  (la proposta vale per le sezioni di soli dati): ora possono portare testo diverso nei due documenti.
- ⚠️ La pastiglia della regola vincente nella vista delle piste (solo staff) mostrava la nota della regola: senza note
  non dice più niente in più, e non si è toccata.

**Migrazione dopo** (consegna successiva): via `AirportLvpMinima.Note` e `AirportRunwayRules.Note`, coi controlli
`SELECT … WHERE Note IS NOT NULL` → nessuna riga; via anche `AirportLvpMinima.NotaMassima` e il controllo del tetto in
`AirportEditingService`.

## 3. S123b — tabelle delle aree

Le note non hanno colonne: stanno nel JSON del blocco payload della sezione — `aor` della vIPI APP (anche militare) e
dei blocchi della vIPI ACC (`AorAirspaceEdit.Note`, per volume), `regulated` e `lowlevel` del vSOP militare
(`MilRegulatedPayload.Notes`, per area). **Niente migrazione dello schema.**

- **`aor` diventa «scheda + blocchi»** (`HB`) nel profilo APP (e APP militare) e nei due profili dei blocchi della vIPI
  ACC (Aerovia, gruppo APP); la vLOA resta com'è. `AccSectionBody` mostrava i blocchi propri della sola validità:
  ora anche dell'AoR. Le aree del vSOP erano già `HB`.
- **Via i campi**: la nota di ogni volume nella tabella degli spazi aerei (`AorAirspaceTable`) e di ogni area
  (`MilWorkingAreas`, che perde anche il parametro `OnSetNote`, e `MilSectionsEditor` i due gestori). La colonna
  «Note» resta solo se una riga ne ha una: le release già uscite.
- **Il trasloco** all'avvio, `NoteDelleAreeNeiBlocchiAsync`: per ogni sezione, nell'ultima versione e nella pubblicata
  corrente, un paragrafo con una riga per volume o area — `- FIRENZE CTR: Attivo H24` (il nome del volume dalla sua
  chiave naturale), `- LI R14A - S.Severa: …` (il nome dell'area dall'anagrafica, l'id se manca) — e il JSON
  riscritto senza note. Un volume che aveva solo la nota esce dal JSON; la classe scritta a mano resta.
- ⚠️ Si riscrive solo un blocco con una nota **vera**: il serializzatore scrive `"Note":null` anche su un volume con la
  sola classe, e riscriverlo a ogni avvio direbbe «cambiato» per sempre.
- Resta nel modello `AorAirspaceEdit.Note` e nel servizio del vSOP `SaveAreaNoteAsync`: non li chiama più nessuno
  dall'interfaccia; si tolgono quando si tolgono le colonne di S123a.

## 4. S123c — trasferimenti

La nota della coppia (`CoordinationAgreements.Note`) e la descrizione della sezione (`AgreementSections.Description`)
**non sono mai uscite in un documento**: si vedono solo nella pagina Trasferimenti, e la descrizione nella nuova API
dei settori (`description`). Portarle in un paragrafo vorrebbe dire pubblicarle la prima volta, in fino a tre
documenti per accordo. Il committente: **prima le vede, poi decide**. Le domande da fare a una copia di produzione:

```sql
SELECT a.Id, s1.Callsign AS LatoA, s2.Callsign AS LatoB, a.Note
FROM CoordinationAgreements a
JOIN Sectors s1 ON s1.Id = a.SideASectorId JOIN Sectors s2 ON s2.Id = a.SideBSectorId
WHERE a.Note IS NOT NULL AND TRIM(a.Note) <> '';

SELECT s.Id, a.Id AS Accordo, s.Kind, s.Direction, s.Description
FROM AgreementSections s JOIN CoordinationAgreements a ON a.Id = s.AgreementId
WHERE s.Description IS NOT NULL AND TRIM(s.Description) <> '';
```

**Che cosa c'è scritto** (copia di produzione del 21 ottobre 2026, 1.70.0; 48 accordi, 125 sezioni; la copia è
stata cancellata subito dopo): **nessuna nota di coppia**, e **cinque descrizioni di sezione**:

| Sezione | Accordo | Lati | Verso | Descrizione |
|---|---|---|---|---|
| 14 | 7 | LIBB_MIL_CTR ↔ LIBV_APP | arrivi A→B | «ARR LIBV» |
| 15 | 7 | LIBB_MIL_CTR ↔ LIBV_APP | partenze B→A | «LIBV dep» |
| 18 | 9 | LIBB_MIL_CTR ↔ LIBA_APP | arrivi A→B | «arrivi da sud» |
| 73 | 35 | LIMM_WS2_CTR ↔ LIMF_WN0_APP | arrivi A→B | «Milano WS2 trasferisce a WN0 i traffici in arrivo autorizzati alle STAR pubblicate e in discesa per FL150» |
| 81 | 38 | LIMM_WS5_CTR ↔ LIMM_ES5_CTR | sorvoli A→B | «Milano ws5 trasferisce a ES5 i traffici in transito, assicurando una separazione di almeno 5 NM stabili o in aumento tra aa/mm con stessa rotta o almeno 1000 ft.» |

Le prime tre sono etichette di lavoro; le due di Milano sono frasi operative, e la seconda dice una regola (la
separazione al trasferimento) che le clausole non portano.

**Deciso dal committente** (21 ottobre 2026, sera): «togli le note e le descrizioni dei trasferimenti senza portarle
nei documenti: le due frasi di Milano le riscrivo io dove servono». Fatto:

- **Via i campi** dalla pagina Trasferimenti (nota della coppia, descrizione della sezione), dal navigatore e dalla
  ricerca, dai modelli (`AgreementInput`/`AgreementRow`, `AgreementSectionInput`/`AgreementSectionRow`,
  `TransferFlowRow`), dal repository (anche dalle copie per «Condividi» e «Copia nel verso opposto» e dall'«annulla»).
- **Via `description` dall'API dei settori** (`/coordinations`): non lo popolava più niente. L'API era uscita in 1.69.0
  il giorno stesso.
- **Migrazione `TogliNoteTrasferimenti`**, sui due provider: `DropColumn` di `CoordinationAgreements.Note` e
  `AgreementSections.Description`. ⚠️ **Distruttiva, e il testo si perde apposta**: le cinque descrizioni sono in
  questo paragrafo, e le due di Milano si riscrivono prima o dopo, dove servono.
- `tools/Vipi.AgreementsToSections` (la conversione una tantum degli accordi vecchi, di settembre) scrive ancora quelle
  colonne: lavora sullo schema di prima della conversione, che non esiste più, ed era già fuori uso.

⚠️ Da non toccare comunque: le etichette che fanno parte della clausola (condizione, condizione personalizzata, punto
e livello di trasferimento, livello speciale) — entrano nella frase, nel confronto dei voli e in Aurora.
