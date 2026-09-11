# Le regole piste nel vSOP militare 🟢

**Chiesto dal committente l'11 settembre 2026**: *«Le regole di pista nelle vSOP mancano. Quando ci sono
aeroporti senza vIPI non ci sono nelle vSOP. Voglio che ci siano sotto la sezione delle piste e deve funzionare
esattamente come per le vIPI, suggerendo le piste in base alle regole.»*

Due decisioni prese col committente prima del codice:

1. **Dove**: sezione a sé in «Dati generali», **subito dopo Piste** e prima delle SID. Sorella, non figlia.
2. **Scali misti** (militari con una vIPI civile, es. LIML, LIRP): la sezione **c'è**, in sola lettura, con la
   pista suggerita e il rimando all'editor civile per cambiarle. È come funzionano già le SID (§CV).

## La precisazione che semplifica tutto (la stessa delle SID)

Le regole piste **non stanno «nella vIPI»**: stanno nell'**anagrafica dello scalo** (`AirportRunwayRules`, per
`AirportId`). La vIPI civile è solo la **porta di scrittura**. Quindi in lettura non c'è nessun ramo: il vSOP
legge dall'aeroporto, misto o solo militare che sia. Si biforca solo la scrittura, e quella biforcazione esiste
dal §AS (`ScaloSenzaCivile`).

## Pre-flight (FEATURE-PROCESS)

1. **Modello** — nessun concetto nuovo. Il dato è quello di sempre; la chiave di sezione è **la stessa** del
   profilo civile (`runwayrules`). Cercando «dove si salvano le regole piste» fra sei mesi si trova un posto.
2. **Dispatch** — due pezzi stavano scritti **una volta** per un solo ospite e ora ne servono due: il calcolo
   della pista in uso (privato di `AirportMemberLoader`) e la lettura della regola in forma d'editor (dentro
   `AirportSectionsEditor`). Estratti in un commit **meccanico** a parte: `PistaInUso.Calcola` e
   `AirportRuleMapping.FromRow`. Nessuno `switch` nuovo.
3. **Ingressi + verifica** — i vSOP già scritti ricevono la sezione all'avvio da
   `AddMissingCatalogSectionsAsync` (lo prova `ParcheggiNeiDatiGeneraliTests`, che parte da un vSOP vecchio);
   si scrive dall'editor del vSOP su un campo senza vIPI civile. Verifica dal vivo: una regola scritta su un
   campo solo militare deve cambiare la pista marcata nel viewer.
4. **Propagazione** — non si toglie niente, ma si **ribalta una frase scritta**: in `MilDocumentBody` c'era
   «niente `InitialRunway` né `DepIdents` … le regole piste, una sezione che il profilo militare NON ha».
   Riscritta nello stesso giro, insieme al commento del catalogo e al test che diceva che una figlia di «Piste»
   avrebbe sforato `MaxDepth` (falso: starebbe a profondità 2, come le soglie; il limite è 3).

## 🔴 Quel che c'è già, e non va scritto

| | |
|---|---|
| Il dato, il servizio di scrittura e la sua guardia del lock (`SaveRunwayRulesAsync` → `EnsureLockMineAsync`, che pretende il lock della vIPI civile se esiste, altrimenti quello del vSOP — `AirportLockGuard`) | ✅ |
| La derivazione per la vista: `AirportDerived.Rules`, già calcolata da `MilMemberLoader` e buttata via | ✅ |
| Il congelamento alla release: `AirportFrozenSectionProvider` è registrato per le due edizioni e sa già fotografare `runwayrules` | ✅ |
| I componenti: `AirportRunwayRules` (lettura), `AirportRunwayRulesEditor` (scrittura, **col banco di prova**), `AirportRunways`/`AirportSids` coi parametri della pista in uso | ✅ |
| La semina nei vSOP già scritti (`AddMissingCatalogSectionsAsync`) | ✅ |
| La scheda delle sezioni in comune di un'unione vIPI + vSOP: confronta per **chiave**, quindi le regole ci entrano da sole | ✅ ⚠️ ma vedi sotto: in un'unione già «ripulita» la sezione nuova nasce **visibile** e compare due volte finché non si ripassa dalla scheda |

## Che cosa cambia

| pezzo | dove |
|---|---|
| La sezione «Regole piste» nel profilo `AirportMil`, fra `runways` e `sids` (rinumerati i successivi) | `SectionCatalog` |
| Il titolo in memoria come **umano**, «Runway selection rules» (lo stesso del catalogo) | `TitoliUfficiali` |
| La pista in uso calcolata anche per il vSOP (regole **vive**, poi vento del METAR) | `MilMemberLoader` → `PistaInUso.Calcola` |
| Viewer: la tabella delle regole; le Piste marcano DEP/ARR; le SID partono dalla pista in uso | `MilDocumentBody` |
| La «meccanica» (quale regola vince, da dove viene la pista) solo da `DivisionStaff` in su, come nella vIPI | `MilDocumentBody.MeccanicaVisibile` |
| Editor: le regole si scrivono **solo** se `ScaloSenzaCivile`; sui misti resta il rimando (ramo `default`) | `MilSectionsEditor` |

**Nessuna migrazione, nessuna chiave resx nuova, niente `wwwroot`.**

## ⚠️ Conseguenze da dire a chi carica

- **MINOR**, non PATCH: una sezione nuova nel catalogo (stessa regola di §CV).
- La passata d'avvio scrive la sezione **anche nell'ultima versione pubblicata** dei vSOP (misurato per le SID in
  1.21.0): il pubblico non cambia — legge la fotografia della release — ma i vSOP già pubblicati possono
  comparire fra i **«da ripubblicare»**. La sezione arriva in pubblico alla prossima pubblicazione.
- Sui campi solo militari le regole oggi **non ci sono** (nessuno aveva una porta per scriverle): la sezione nasce
  vuota e la pista marcata la sceglie il vento, finché qualcuno non le scrive.

## La prova

- Test: `ProfiloMilitareTests` (45 sezioni, posizione, stessa chiave derivata del civile),
  `ParcheggiNeiDatiGeneraliTests` (un vSOP **vecchio** riceve la sezione al posto giusto),
  `PistaInUsoTests` (regola che vince, regola che cade, solo vento), `AirportRuleValidationTests`
  (`FromRow` → `ToRow` non perde niente), `TitoliUfficialiTests`.
- Dal vivo: vedi §«Verificato a schermo» qui sotto.
- Suite intera verde su 15 progetti-TFM, `dotnet build Vipi.slnx -c Release --no-incremental` con 0 avvisi.

## Revisione indipendente (11 settembre 2026, notte)

Un revisore senza il contesto di chi ha scritto il codice: diff, suite, build Release, e l'app guidata su
una copia del DB (anche con un'identità a basso livello e con una pubblicazione vera). Esito: **funziona,
con riserve**; nessun difetto che rompa qualcosa. Il commit meccanico è davvero meccanico.

- 🟡 **In vista pubblica la regola «adesso» può cadere sulla riga sbagliata**: la tabella viene dalla release
  congelata, il numero della regola vincente dalle regole vive. Cancellata la #1 dopo aver pubblicato, la
  pagina segnava la #1 della fotografia mentre le Piste citavano un'altra regola. Solo da `DivisionStaff` in
  su. **Preesistente, identico nella vIPI**.
- 🟡 **Unioni già «ripulite»**: la passata d'avvio crea la sezione **visibile**, quindi dove nel vSOP si erano
  nascoste le ripetizioni «Regole piste» compare due volte finché non si ripassa dalla scheda delle sezioni in
  comune (che la propone già spuntata). Introdotto da questo lavoro; con le SID era successo lo stesso.
- 🟡 **Anteprima di release del vSOP**: `MilMemberLoader` non passa il ciclo della release alle derivate
  (la vIPI sì), quindi le SID in `?as=rel:` sono quelle di oggi. Preesistente dal 10 settembre.
- ⚪ Buchi di test: nessun test di componente sul collegamento `MilDocumentBody` → Piste/SID/regole, e
  `DatiDelloScaloMilitareTests` non elenca `AirportRunwayRulesEditor`. Oggi lo prova solo il vivo.
- ⚪ Frasi corrette dopo la revisione: la guardia del lock (vedi tabella sopra), «su uno solo militare»
  (vale per i campi SENZA vIPI civile), e «un posto solo» per il calcolo (vista rapida ed elenco hanno
  ancora la loro copia).

## Verificato a schermo (11 settembre 2026, notte)

Edge + puppeteer-core, `dotnet run` su una **copia** del `vipi.db` di sviluppo.

- All'avvio: **«Aggiunte 7 sezioni di catalogo mancanti»** — i sette vSOP dell'archivio.
- **LIBG** (solo militare, senza vIPI civile, zero regole), bozza: «Runway selection rules» sta fra «Runways»
  (con le sue soglie) e «SID». Vento 360/4 e nessuna regola ⇒ marcata la **35**, dal vento.
- Editor di LIBG, col lock: la sezione ha l'editor delle regole col banco di prova e la nota «dato dello
  scalo»; «+ Regola», chip DEP **17**, vento in coda massimo 40, nome «Prova vSOP» ⇒ salvata a ogni gesto.
- Dopo, **bozza e pubblica** di LIBG marcano la **17** 🛫🛬, e le Piste dicono «raccomandate dalla regola attiva
  «Prova vSOP»». Le SID partono dalla **17🛫**. La tabella delle regole c'è in bozza, con la #1 segnata; nella
  pubblica la sezione non c'è ancora (la release è una fotografia) ma la pista in uso sì — è un dato vivo.
- **LIML** (misto): la sezione c'è nel viewer; nell'editor zero campi e il rimando a
  `/services/vsop/limm/airports/editor?icao=LIML`.
- Nessun errore di console, nessun 4xx, nessuna espressione Razor letterale; `vipi.db` del progetto intatto.

⚠️ **Trovato per strada, preesistente e NON toccato**: la pastiglia della regola attiva nella tabella delle
regole usa la chiave `Airport_QnhNow` («QNH attuale» / «current QNH»), quella della sezione Quote di
transizione. Succede identico nella vIPI civile: si vede anche nel vSOP solo perché ora la sezione c'è.
Correggerla vuol dire una chiave resx nuova (e il satellite inglese nel pacchetto).
