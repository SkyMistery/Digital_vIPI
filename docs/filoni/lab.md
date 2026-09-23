# Filone Sector Lab — stato

> Scrive **solo** l'agente del Lab (cartella `vipi-lab`, ramo `lab/f3`). Regole:
> [`come-si-lavora-in-parallelo.md`](come-si-lavora-in-parallelo.md). Storia fino al 23 settembre 2026:
> `docs/lavori-aperti.md` §A71, §A113, §A115, §A116.

## Dove siamo — 23 settembre 2026, sera

**In corso: le prove a mano del committente** (slice 6 di F3-bis; PROVE.md 28-36 = richieste della sera). Il committente fa le prove e porta i risultati in
una chat nuova. `lab/f3` è pulito, spinto, **CI verde** sull'ultimo commit di codice. Test: motore 471 (net8 e net10),
Lab 327.

- **Eseguibile di prova**: `D:\Programmazione\IVAO_Test\SectorLab-prova\VipiSectorLab.exe`, ripubblicato il 23
  settembre alle 18:48 (tutto quello che c'è su `lab/f3`). Le prove sono in `SectorLab-prova\PROVE.md`: 1-11 di F3,
  **12-22 di F3-bis**, **23-27** delle correzioni di oggi. Ripubblicare dopo ogni correzione:
  `dotnet publish src/Vipi.SectorLab -c Release -r win-x64 --self-contained -o D:\Programmazione\IVAO_Test\SectorLab-prova`.
  🔴 Prima: `Get-Process VipiSectorLab` — il committente tiene spesso l'app aperta, e le DLL in uso non si sovrascrivono.
- **Sector di prova**: il clone `D:\Programmazione\IVAO_Test\it-aurora-sector-test`. Ha ancora modifiche locali di
  prove vecchie (`twrs.tfl`: l'ATZ di LIRN, `APT.fix`) che il committente vede nel `git diff`: si tolgono con
  `git checkout -- .` (detto al committente il 23 sera: lo fa lui).
- **La consegna** (slice 11 di F3: workflow `sectorlab-v*`, zip + SHA-256, scheda `/services`) **la decide il
  committente**. Prima vuole qualcosa da presentare agli AOD (è nel team AOD: copre ~80% dei bisogni, il resto dopo).

### F3 — l'app

Carta [`2026-09-22-f3-l-app.md`](../feature/2026-09-22-f3-l-app.md): slice 0-10 fatte, resta la 11 (consegna, vedi sopra).
La scheda `/services` entra nel sito: quella parte della consegna la fa l'integratore.

### F3-bis — copie gemelle e mappe composte

Carta [`2026-09-23-f3-bis-copie-e-mappe-composte.md`](../feature/2026-09-23-f3-bis-copie-e-mappe-composte.md): il §8
«Traccia» ha numeri e scoperte di ogni slice.

| Slice | Cosa | Stato |
|---|---|---|
| 0 | Misure sul fork | ✅ gemelli 3/13/1; 58 aggregati, 16 già disallineati; D7 chiusa (l'8º campo è `RNAV`) |
| 1 | Gemelli nel motore, regola `CopieDiverse` | ✅ 17 chiavi sul fork |
| 2 | Una modifica va anche sulle copie gemelle | ✅ una voce, più diff; «allinea anche questo» |
| 3 | Tag fra virgolette, chiave `composta`, `<br>` nel modello | ✅ 545/545 `<br>`, 2782/2782 tag |
| 4 | Rigenerazione delle mappe composte, due regole del validatore | ✅ `lime.str`: STAR spostata → −2 +2 |
| 5 | «Composta da» nella scheda | ✅ «Composta da quello che disegna oggi»: 20/55 aggregati col solo tag |
| 6 | Prove a mano del committente, Aurora compresa | 🟡 in corso (vedi sopra) |

Decisioni del committente: D1-D10 in carta §5. Due **riviste** dopo le misure: **D4** (le virgolette restano, anche
se i nomi con spazi si leggevano già) e **D8** (per mappa: troncate di norma, `intere=si` le disegna intere).

### Correzioni dalle prove del committente (23 settembre)

- `ad26a3b4` i record di un file si vedono **sotto** il file (erano in fondo alla colonna, fuori schermo).
- `bea96c8c` togliere la scelta di un file **non chiude** più la sua cartella.
- STAR «(ALL)» spezzate al `<br>` in `Geometria` (si ricollegavano l'una all'altra: `lirn.str` STAR 06(ALL)).
- Zoom della mappa 16 → **20**, coordinate per la mappa a 6 decimali (~11 cm).
- **Due schermi**: tasto «Pannelli in un'altra finestra» → `/pannelli` (Sfoglia/Problemi, scheda, modifiche) in una
  seconda finestra del guscio (`FinestraDeiPannelli`), sull'altro schermo; la principale tiene mappa e strati. 🟡 Il
  guscio a due finestre l'ha provato solo bUnit: la prova vera sono la 25-27 del committente.

### Richieste della sera del 23 settembre (a–f) e prova 5 — PROVE.md 28-36

- **Le due finestre insieme** (`FinestreInsieme`): cliccata l'una, l'altra risale subito dietro (`SetWindowPos` senza
  attivare). Non con `Owner`: la posseduta starebbe sempre sopra, e con uno schermo i pannelli coprirebbero la mappa.
- **Colonne ridimensionabili**: divisori `data-divide` (sectorlab.js, un ascoltatore sul documento), larghezza in una
  variabile `--lab-l-<nome>` su `<html>`, ricordata nel localStorage della WebView2 (doppio clic = di base). 🟡 Il
  committente vuole, con le future impostazioni, l'interruttore «salva il tavolo di lavoro all'uscita»: oggi si
  ricorda sempre.
- **Tema scuro + brand IVAO**: il Lab NON seguiva il brand (Segoe UI, `#0b5cad`). Ora token del sito (`vipi-theme.css`:
  atmos/ocean/fuselage/semantic), Poppins/Nunito Sans/IBM Plex Mono serviti da `wwwroot/fonts/`, barra blu IVAO.
  Automatico/chiaro/scuro (`sectorlab-tema.js` nel `<head>`); i colori degli strati sono `--lab-strato-<id>` e la mappa
  li rilegge al cambio di tema.
- **Annulla/ripeti** (`SessioneDelLab.NellaStoria`/`Annulla`/`Ripeti`, tasti ↶ ↷ e Ctrl+Z/Ctrl+Y fuori dai campi):
  annullare = tutto com'era all'apertura (`AnnullaTutto` + `Modifiche = new()`) e si rigiocano i gesti tranne l'ultimo.
  Dopo un salvataggio o una rilettura la storia riparte. 🔴 Preso strada facendo: la mappa aveva UNA versione della
  geometria e ridisegnava solo l'ultimo strato toccato (un gesto sulle copie gemelle ne tocca più d'uno; anche il cambio
  di master non ridisegnava niente) → `VersioneDelloStrato`.
- **Anteprima dell'incolla** (`Core/Modifiche/TestoDaIncollare`, la STESSA lettura dell'incolla): nella scheda
  (`AnteprimaDelDisegno`, SVG: oggi grigio, nuova verde tratteggiata, centri) e sulla mappa; segue testo e densità a
  ogni scatto. Il «°» della densità non va più a capo.
- Test Lab 306 → **327**. Eseguibile ripubblicato alle 20:55.

### Aperto, da chiedere o dire al committente

- ❓ **Nomi di procedura con spazi** (63 su 1169: `RNP10 UPETI` di `lica.str`, le rotte `AAR …` di `lizz.str`): oggi
  non possono stare nell'elenco di una mappa composta (casella spenta). Allargare la grammatica (nomi fra virgolette
  anche nell'elenco)? Fra i 58 aggregati le usa solo `lica` `RNP10`.
- 🟡 **Errore nei dati del fork**: `limf.sid:28` `LIMF18;TOP1B LAG2L; ; ;0;LAGEN;` (manca il `;` fra `LIMF` e `18`,
  dal commit «LIMF: Revisione SIDs»). Il validatore oggi non lo dice: una regola possibile per dopo.

### Codice comune toccato (per l'integratore, alla fusione)

`Vipi.Sectorfile`: `Validazione/CopieGemelle.cs`, `Regola.CopieDiverse`, `Regola.CompostaConProceduraAssente`,
`Regola.CompostaNonAllineata`; `IO/Metadati.cs` (virgolette, `composta`, `intere`, `Togli`, `NomeElencabile`);
`IO/MappeComposte.cs`; `StrRecord`/`StrParser`/`StrSaver` (`IniziaUnTratto`). `tools/Vipi.SectorfileProva` (sezione
5b, `composta` sulle MAPS). Il sito non usa niente di questo; la build della soluzione è verde.

### Dove lavorare

- Conteggi del filone: `tests/conteggi/Vipi.SectorLab.Tests.txt` e, se si tocca il motore, `Vipi.Sectorfile.Tests.txt`
  (`bash tools/conta-test.sh <log> --scrivi <Assieme>`, nello stesso commit dei test).
- Prova sull'albero intero: `dotnet run --project tools/Vipi.SectorfileProva -c Release -- <…\SectorFiles\Include\IT>`.
