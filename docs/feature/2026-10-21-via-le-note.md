# Via le note dei dati strutturati — carta (21 ottobre 2026)

**S123**, filone Sito. Il committente, chiedendo i link ovunque si scrive testo (S122): le note «devono sparire, dove
voglio scrivere testo seleziono paragrafo o quello che è e lo metto». Scelte da lui, tutte e quattro: la nota delle
**LVP**, le note delle **regole piste**, le note delle **tabelle delle aree**, le note dei **trasferimenti**. Il testo
già scritto va in un paragrafo della sezione, come per la tecnica del radar di torre (S118).

## 1. In tre passi

| Passo | Che cosa | Stato |
|---|---|---|
| **S123a** | Nota delle LVP, note delle regole piste | §2 |
| **S123b** | Note delle tabelle delle aree: spazi aerei dell'AoR (vIPI APP e ACC), aree di lavoro e BOAT del vSOP militare | da fare |
| **S123c** | Note dei trasferimenti | §4: prima l'elenco, poi la decisione (committente) |

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

Da fare. Le note non hanno colonne: stanno nel JSON del blocco payload della sezione (`aor` della vIPI APP e dei
blocchi della vIPI ACC: `AorAirspaceEdit.Note`; `regulated` e `lowlevel` del vSOP militare: `MilRegulatedPayload.Notes`).
`aor` va portata a `HB` (APP, blocchi Aerovia e gruppo APP), e `AccSectionBody` deve chiedere `KeepsOwnBlocks` invece
di mostrare i blocchi della sola validità.

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

⚠️ Da non toccare comunque: le etichette che fanno parte della clausola (condizione, condizione personalizzata, punto
e livello di trasferimento, livello speciale) — entrano nella frase, nel confronto dei voli e in Aurora.
