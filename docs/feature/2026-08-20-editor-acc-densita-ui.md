# Editor ACC — densità e uso (20 agosto 2026)

Sesto giro della famiglia, dopo [accordi](2026-08-19-accordi-densita-ui.md),
[struttura](2026-08-19-struttura-densita-ui.md), [ACC admin](2026-08-19-acc-admin-densita-ui.md),
[aeroporti](2026-08-19-aeroporti-densita-ui.md) e [editor aeroporto](2026-08-20-editor-aeroporto-densita-ui.md):
`/vsop/{acc}/editor` — `src/Vipi.Ui/Pages/AccEditorPage.razor`. Prima voce della lista «da rifare» nelle
[regole](../design/regole-ui-pagine-admin.md) (6 466px misurati in ricognizione).

Carta scritta prima, resoconto aggiunto dopo: dal §3 in giù c'è quello che è successo davvero.

## 1. Cosa c'era (letto nel codice, prima di misurare)

| # | Cosa | Dove | Regola |
|---:|---|---|---|
| a | Testata su **due righe**: `.doc-head` con `<h2>` e sotto il sottotitolo `Acc_Subtitle` («Settori di aerovia + gruppi APP»), che ripete quello che dice il pangrattato | `AccEditorPage.razor:38-42` | 1, 6, 7 |
| b | **Nessun «?» di pagina** e nessuna pill di stato in testata (la pill Bozza/Pubblicata sta solo nel rail) | idem | 7, 12 |
| c | **Errore in fascia** sopra il contenuto: spinge in giù tutto il documento proprio mentre ci si lavora | `:51` | 5 |
| d | **Lock altrui in fascia**, e la stessa informazione è già un badge nel rail | `:46-50` + `:322-327` | 1, 5 |
| e | I **blocchi** (Aerovia, gruppi APP) sono `<div class="acc-block">` **non collassabili**: dentro ognuno 9 (Aerovia) o 10 (gruppo APP) sezioni di primo livello, tutte aperte tranne `regulated` | `:62-104`, `SectionCatalog.cs` | 0 |
| f | Il TOC **non ha Espandi/Comprimi tutto** (l'editor aeroporto sì, stesso componente `EditorToc.Footer`) | `:55` | 68 |
| g | `vipiEditorSections` apre/chiude **solo** `details.ed-sec`: non conosce le card `details.block.cb` di questo editor | `vipi-editor.js:37` | — |
| h | **Prosa sempre a schermo**: `Aor_ExtraShapesHelp`, `Aor_ColorsHelp`, `Acc_LinkedFreqsHint` sulla pagina; `AppFreq_EditHelp`, `AppCfg_EditHelp`, `Acc_AllAreasAuto` + i «CountHint» nei componenti condivisi. Moltiplicata **per blocco** | `:496,502,536`; `AppFrequencies:82`, `AppConfigurations:9`, `RegulatedAreasEditor:31,88,156` | 7, 8, 9 |
| i | **Nessuna tabella con `thead` fermo**: frequenze, `cfg-table` delle configurazioni, coordinamenti e VFR sono `<table>` nudi | componenti `App/*` | 23, 51, 57 |
| j | **Emoji comandi**: 💾 ✎ 🔒 ✓ nel rail; 👁 🙈 ¶ ⤒ ⤓ nell'editor di sezioni condiviso | `:318-334`, `DocumentSectionsEditor:255-283` | 40 |
| k | **AoR: 340px di mappa fissi per blocco**, più chip settori, chip configurazione e due righe di prosa | `AccAor.razor:22` | 0 |
| l | Stringhe **scritte a mano in italiano** nel viewer AoR («Mappa 2D», «Configurazione:», «azzera», «Shape AOR non disponibile», le due frasi di spiegazione) e `"Nuovo gruppo APP"` nella pagina | `AccAor.razor`, `AccEditorPage:286` | 43 |

Quello che invece **era già a norma** e non andava toccato: TOC e rail sono appiccicati (`.editor-toc-side` /
`.ed-rail`, `top:70px`), quindi chi lavora in fondo ha i comandi a schermo; le àncore hanno il loro
`scroll-margin-top`; le mappe dentro un `<details>` chiuso sono già gestite (`vipi-aor.js:134`,
`invalidateSize` all'apertura) — **collassare non rompe le mappe, le rende più leggere**.

## 2. Giro 0 — misurato (1600×900, copia del DB di sviluppo)

| Pagina | In lettura | **In modifica** |
|---|---:|---:|
| `/vsop/libb/editor` | 6 466px | **9 690px** |
| `/vsop/limm/editor` | 5 020px | **8 155px** |

**Il primo numero della ricognizione era quello sbagliato**: l'editor si guarda poco e si *modifica*, e in
modifica la pagina cresce del 50%. Ripartizione in modifica (LIBB, blocco Aerovia = 4 513px):

| Sezione | In lettura | In modifica | Cosa la gonfia |
|---|---:|---:|---|
| AOR | 587 | **1 074** | mappa 446px + picker shape extra + colori + due prose |
| Frequenze | 643 | **1 040** | 12 righe da ~60px + prosa + riquadro «collega frequenza» |
| Coordinamenti | 545 | 551 | già a `<details>` annidati con Espandi/Comprimi propri |
| Configurazioni | 189 | 506 | |
| Separazioni radar | 207 | 228 | |
| Procedure generali | 90 | 269 | |
| Minime / Validità / Nuova sezione | 90 | **139** ciascuna | una sezione **vuota** costa 139px di soli «+ Paragrafo/Callout/Tabella/Immagine» |
| Aree regolamentate | 88 | 93 | nasce chiusa (catalogo) — è la prova che il collasso funziona |

I due blocchi di LIBB fanno 4 513 + 3 744 = 8 257px degli 9 690 totali: **la pagina è i blocchi**, e un blocco
chiuso vale quanto dieci sezioni e una mappa in meno. Testata 84px, TOC 798 (21 voci), rail 221, release 974.

Trovato guardando gli screenshot (regola 47) e non previsto dalla carta:

- **La riga della tabella frequenze è alta ~60px** (12 righe = 761px in modifica). È la regola 59 in piccolo.
- Una sezione **senza contenuto** non è gratis: 139px per la sola barra dei «+».
- In **EN** la pagina è **mista**: i testi localizzati passano all'inglese e restano in italiano le frasi
  scritte a mano dentro `AccAor` («Accendi/spegni i settori sopra la mappa…», «Configurazione:», «azzera»),
  «Trasferimenti dei settori del blocco…» e i titoli di sezione che vengono dal DB (quelli sono dato, non lingua).
- I coordinamenti **hanno già** i loro «Espandi tutto / Comprimi tutto» annidati: i tasti nuovi del TOC devono
  convivere con quelli, non sostituirli.

## 3. Cosa è cambiato

Una slice per commit, `dotnet build Vipi.slnx -c Release --no-incremental` verde sui due TFM e `dotnet test`
verde a ogni passo.

1. **Testata in una riga** (`.st-head`): titolo · «?» · pill di stato · chip del lock · ⤢ a fondo riga. Il
   sottotitolo `Acc_Subtitle` non è stato riscritto: sta dentro il «?», stessa chiave (regola 8).
2. **Il lock è uno stato in riga**, non una fascia. E i badge del lock che stavano nel rail scrivevano l'ora in
   **UTC** mentre la `lockbar` delle altre pagine la scrive **locale**: la stessa scadenza compariva a due ore
   diverse. Ora c'è un posto solo, e legge come tutte le altre pagine.
3. **L'errore è un toast fisso** (`.editor-toast`, come l'editor aeroporto): in una pagina da migliaia di pixel
   una fascia in cima è un messaggio che nessuno vede, e intanto spinge in giù la sezione su cui si lavora.
4. **Il blocco si chiude** — la slice che vale. `<details>` con la maniglia sul titolo, il conteggio delle
   sezioni accanto (a blocco chiuso è l'unica cosa che dice quanto c'è dentro), stato ricordato fra le
   navigazioni, **fisarmonica** (aprendone uno gli altri si chiudono) e «Espandi tutto / Comprimi tutto» in
   fondo all'indice, gli stessi tasti dell'editor aeroporto. Nasce aperto **solo il primo**, per decisione.
5. **La prosa delle sezioni è un «?»** nella riga-titolo, reso da `DocumentSectionsEditor` per **tutti e tre**
   gli editor che lo montano (ACC, APP, vLOA) da una mappa sola chiave-sezione → chiave-testo. Sulla pagina, i
   due pannelli AoR prendono la riga `.ed-h3` con titolo e «?».
6. **Guida**: sezioni nuove `editor-frequenze`, `editor-configurazioni`, `editor-aor`, e `editor-acc`
   riscritta — raccontava una pagina che non esiste più («multi-albero», «In evidenza»). Tutte registrate in
   `GuideSearchCatalog`, altrimenti la ricerca globale non le trova.
7. **La tabella frequenze in modifica** torna alta 43px per riga: `.freq-edit` **esisteva già nel foglio dal
   giro APP e non la usava nessuno**. Applicata solo in modifica — nel documento pubblico la riga si legge.
8. **Lingua e icone**: le frasi scritte a mano dentro `AccAor`/`AccAor3d` prendono la loro chiave IT+EN, e
   👁/🙈 diventano `Icon` in `DocumentSectionsEditor`. 🔴 Live / 🧊 Congelata restano (vocabolario di stato);
   🔒 resta emoji perché nel set di `Icon` il lucchetto non c'è, e lo si dice (regola 40).
9. **⤢ Larghezza piena** riusando `.ed-layout.sid-wide`: indice e rail via, colonna centrale da 946 a 1 536px a
   1600. Le chiavi `Ape_SidWide*` diventano `Ed_Wide*`/`Ed_Narrow*`: sono chrome di editor, e ora le usano in due.

E due difetti **vecchi**, trovati guidando la pagina e non previsti dalla carta:

10. **Il tour di onboarding scorreva la pagina da solo.** `place()` gira anche a ogni evento `scroll` e
    chiamava `scrollIntoView` senza condizioni: con un bersaglio **appiccicato** (l'indice, `position:sticky`)
    non si centra mai, quindi ogni giro scorreva ancora. Misurato: **263 chiamate** e la pagina scesa da sola a
    3 268px senza che nessuno l'avesse toccata — su **ogni** editor, alla prima visita. Ora il tour scorre
    quando cambia passo (`centra()`, che salta i bersagli sticky e quelli già a schermo) e il riquadro segue
    chi scorre.
11. **Un «?» chiuso allargava la pagina**: il popover è `position:absolute` e a `<details>` chiuso il suo box
    resta nell'area scorribile — a 1280 quello del rail arrivava a 1 305px. Chiuso, ora non esiste.

## 4. Verifica — i numeri

Guidata con Edge+puppeteer sulla copia del DB di sviluppo, 1600×900, **in modifica** (è così che si usa).

| | Prima | Dopo, all'apertura | Tutto compresso | «Espandi tutto» |
|---|---:|---:|---:|---:|
| `/vsop/libb/editor` | 9 690px | **5 595** | **1 468** | 10 080 |
| `/vsop/limm/editor` | 8 155px | **3 611** | — | — |

Gli altri due, dopo: LIRR 4 669, LIPP 3 527. Nel dettaglio:

| Pezzo | Prima | Dopo |
|---|---:|---:|
| Testata | 84px su due righe | **38px, una riga** |
| Riga della tabella frequenze | 60px | **43px** |
| Sezione Frequenze (blocco Aerovia, LIBB) | 1 040px | **784** |
| Blocco su cui non si sta lavorando | sempre aperto, ~3 700px | **51px** |

Comportamento, guidato davvero:

- **Fisarmonica**: aperto il secondo blocco il primo si chiude (4 512 → 50px) e la pagina resta 2 793.
- **«Espandi tutto» apre tutti i blocchi** (10 080px): la scelta esplicita vince sulla fisarmonica. Le aperture
  di gruppo sono marchiate sull'elemento, perché il `toggle` **arriva dopo** e una bandiera spenta in fondo
  alla funzione era già spenta quando l'evento arrivava — con la bandiera, «espandi tutto» ne apriva **uno**.
- **Salto dall'indice** a una sezione dentro un blocco chiuso: il blocco si apre e il bersaglio atterra a
  **76px**, subito sotto la top-bar (prima: −249, cioè fuori schermo di sopra).
- **Assetti** 1600 / 1440 / 1280 / 1024, **IT ed EN**, zoom 0.8→1.5: testata sempre in una riga (contenuto
  832px in IT, 746 in EN, dentro 1 536 disponibili; a zoom 1.5 sono 1 247 dentro 1 504).
- **Componenti condivisi** contati sulle altre pagine che li montano (regola 49): **editor APP** (2 «?» di
  sezione, 10 icone occhio, nessuna emoji, nessuna prosa fissa), **editor vLOA** (1 «?», 7 icone), **viewer
  vIPI ACC** e **viewer aeroporto** — nessun errore di console, nessuna prosa vecchia rimasta.

## 5. Quello che qui NON si applica (e perché)

- **Testata appiccicata (regola 4)**: no. I comandi di scrittura stanno nel rail, che è già appiccicato.
- **Altezza misurata con `vipiFitViewport` (regola 13)**: no. È una pagina che **scorre**; qui vale la 57.
- **Riquadro col tetto sulle tabelle (regola 57)**: **non ancora**. Sul DB di sviluppo la tabella più lunga è
  di 12 righe (LIBB e LIRR; LIPP 7, LIMM 6) e due barre di scorrimento annidate costerebbero più di quanto
  rendono. Scelta **dichiarata** (regola 63): quando un ACC vero passerà le ~15 righe si rimisura.
- **Salva-tutto e stato «sporco» (regole 35-38)**: no. Questo editor **salva a ogni gesto**: non esiste una
  scrittura pendente da perdere, e un contatore di modifiche sarebbe un comando che non fa niente.
- **Chip-filtro «un blocco alla volta»**: scartato il 20 agosto. Il blocco si comprime e basta — un filtro
  sarebbe un secondo modo di nascondere la stessa cosa.

## 6. Quello che resta aperto

- **La topbar sfora a 1280**: `.topbar .right` arriva a 1 411px dentro 1 280, e succede su **tutte** le pagine
  (misurato su home, struttura, viewer aeroporto), non solo qui. È il chrome: è un giro suo.
- **Il pannello release è diventato il pezzo più alto della pagina**: 974px con 13 rilasci in timeline. A
  blocchi chiusi la pagina *è* la timeline. Da guardare quando toccherà a `ReleasePanel`.
- `"Nuovo gruppo APP"` resta scritto in italiano nel codice: è il **titolo di partenza di un dato**, non una
  stringa di chrome — localizzarlo scriverebbe la lingua dell'editor dentro il documento.
