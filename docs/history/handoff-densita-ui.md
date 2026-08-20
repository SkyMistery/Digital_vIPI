# Handoff — il ramo della densità UI (aggiornato 20 agosto 2026)

> **A cosa serve.** Ripartire a freddo sul ramo `ui-trasferimenti-densita` senza rileggere la cronologia.
> Chi deve fare **la prossima pagina** legge solo questo file più
> [`docs/design/regole-ui-pagine-admin.md`](../design/regole-ui-pagine-admin.md).

## Dove siamo

Ramo **`ui-trasferimenti-densita`**, allineato col remoto, **non fuso in `main`**.
Cancello a ogni commit: `dotnet build Vipi.slnx -c Release --no-incremental` (**0 avvisi**, gli avvisi sono
errori e `dotnet test` non li vede) + `dotnet test Vipi.slnx` — **entrambi i TFM**, net8 e net10.

Il giro riscrive **la forma** delle pagine di lavoro admin: niente cambia nel modello, nelle rotte o nei dati.
Il perno è che **ogni fascia tolta in testa diventa contenuto visibile**.

## Le regole sono già scritte — leggerle PRIMA di toccare una pagina

[`docs/design/regole-ui-pagine-admin.md`](../design/regole-ui-pagine-admin.md): **94 voci in 17 gruppi**, ognuna
già costata un giro di correzioni, più la **ricognizione misurata** (§15) di tutte le pagine con cosa manca a
ognuna e in che ordine conviene farle. Non è un regolamento di stile: è l'elenco di ciò che, saltato, si ripaga.

Il §«Dove sta la roba» in coda dice quale classe/funzione usare per ogni pezzo: il pacchetto tecnico
(`.st-head`, `.st-msg`, `.res-table.sticky-head`, `.st-pane`/`.st-scroll`, `.struct-bar`, `.sh-chip`,
`.conf-layout`, e in `vipi-ui.js` `vipiFitViewport` / `vipiStickyOffset` / `rootZoom` / `placeHelpPop`) **c'è
già e si riusa**, non si riscrive.

## Sette pagine chiuse

| Pagina | Rotta | Prima → dopo | Carta |
|---|---|---|---|
| Accordi | `/vsop/admin/trasferimenti` | → 900 | `2026-08-19-accordi-densita-ui.md` |
| Struttura | `/vsop/admin/sectorstructure` | → 900 | `2026-08-19-struttura-densita-ui.md` |
| ACC | `/vsop/admin/acc` | 8 714 (testata appiccicata) | `2026-08-19-acc-admin-densita-ui.md` |
| Aeroporti | `/vsop/admin/airports` | 13 745 → 900 | `2026-08-19-aeroporti-densita-ui.md` |
| Editor aeroporto | `/vsop/{acc}/airports/editor` | 31 286 → 4 913 (LIRF) | `2026-08-20-editor-aeroporto-densita-ui.md` |
| Editor ACC | `/vsop/{acc}/editor` | 9 690 → 5 595 **in modifica** | `2026-08-20-editor-acc-densita-ui.md` |
| **Confinanti (vLOA)** | `/vsop/admin/confinanti` | **2 515 → 900** | `2026-08-20-confinanti-densita-ui.md` |

Le carte stanno in `docs/feature/`.

## Il metodo, in sei righe

1. **Carta prima del codice** ([FEATURE-PROCESS](../FEATURE-PROCESS.md)), una slice per commit.
2. **Misurare la pagina COME SI USA**: in modifica se è un editor, aperta se ha un dettaglio che si apre.
   L'editor ACC pesava 6 466 in lettura e 9 690 in modifica; Confinanti 2 515 chiusa e molto peggio aperta.
3. **Misurare batte stimare, sempre** — e le larghezze di colonna si misurano col **font calcolato** sui
   **valori veri del DB**, non a occhio e non su esempi.
4. **Guidare la pagina** (skill `verifica-live`) a 1600/1440/1280/1024, **IT ed EN**, zoom 0.8→1.5.
5. **Guardare** gli screenshot, non solo produrli: metà dei difetti di questi giri non aveva un'asserzione che
   li cercasse, e il peggiore di tutti (`.sector-pick` che significava due cose) l'ha visto un umano.
6. Chiudere il giro aggiornando **carta + regole + ricognizione §15 + memoria**.

⚠️ Le mie misure (altezze, sfori orizzontali) **non vedono un elemento assoluto che copre il contenuto**, e non
vedono i posti dove *manca* qualcosa. Per quelli servono gli occhi.

## Prossima: Versioni — `/vsop/versioni`

**Ricognizione del 19 agosto: 1 613px.** Misurato di nuovo il 20 agosto sul sorgente
(`src/Vipi.Ui/Pages/VersioniPage.razor`, **638 righe**), da confermare guidandola:

- **sottotitolo** sempre a schermo (`Ver_Subtitle`) e **nessun `HelpHint`** in tutta la pagina;
- **3 callout in fascia** (errore, esito, e il riepilogo di campagna);
- **4 paragrafi** `muted`/`help` — la ricognizione ne contava 7, **da ricontare guidandola**;
- **27 blocchi di stile in linea**;
- la pagina usa `.wrap` con `max-width:1100px`, **non** `.wrap.struct`: non è (ancora) una pagina di lavoro
  a piena larghezza. Decidere se portarla sul layout di lavoro fa parte del giro;
- i filtri sono `Chip` con stile in linea, **non** `.sh-chip` in gruppo come Aeroporti/Confinanti, e **non
  contano** (regole 30-32, 68);
- il conteggio filtrato è una riga di prosa (`Filtered().Count() … Ver_DocsWord`), non un contatore accanto
  al titolo.

**Le emoji.** 🟢 ×3, 🕒 ×3, 🕓 ×1, ⚠️ ×3 — tutte **nel markup**, nessuna dentro i `.resx` (verificato: nessuna
chiave `Ver_*` le contiene), quindi si tolgono senza toccare le traduzioni. ⚠️ Ma la ricognizione le aveva
marcate come **vocabolario di stato**, non come comandi: dicono *effettiva / programmata / senza release*, e
la regola 40 salva solo le emoji che sono **comandi**. La decisione era di tenerle finché non c'è un set di
pallini colorati (deferito in `piano-ux-hardening`) — **da riconfermare prima di toccarle**.
I glifi monocromatici (▾ ▴ ▸ ✎ ✕) restano testo: è la regola.

Dopo Versioni la lista §15 continua con Permessi, Sorgenti, Audit, Diagnostica, Nuovo documento, Incarichi,
editor APP/vLOA.

## Aperto, e non è di queste pagine

- ⚠️ La **topbar** fa scorrere la pagina in orizzontale a 1280/1024: `.topbar .right` misura **1 411px dentro
  1 280**, identico su home, struttura e viewer. È del chrome, non di una pagina: va affrontato per sé.
- ⚠️ `Vipi.AuroraBridge.Tests` ha **un test instabile**: ogni tanto fallisce nella suite completa e passa
  sempre da solo (78/78).
- Sull'editor ACC a blocchi chiusi il pezzo più alto è ormai il **pannello release** (974px con 13 rilasci):
  è roba del giro di `ReleasePanel`, non della densità.

## Ambiente di verifica

Skill di progetto `.claude/skills/verifica-live/` — copia del DB, `VipiAuth__Enabled=false`, Edge +
puppeteer-core, e si attende `window.Blazor`, **non** il DOM (la prima risposta è il prerender).

⚠️ Se l'app di sviluppo è già in esecuzione, i `bin/` sono **bloccati**: `dotnet publish` in una cartella dello
scratchpad e avviare **su un'altra porta** invece di uccidere l'istanza di chi sta lavorando. E fermare solo
la propria (`Get-Process Vipi.Host | Where-Object { $_.Path -like '*scratchpad*' }`).
