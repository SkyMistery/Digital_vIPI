# Handoff — il ramo della densità UI (aggiornato 21 agosto 2026)

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

[`docs/design/regole-ui-pagine-admin.md`](../design/regole-ui-pagine-admin.md): **103 voci in 18 gruppi**, ognuna
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
| **Versioni** — *solo lock e azioni* | `/vsop/versioni` | (densità **non** ancora fatta) | `2026-08-21-versioni-lock-e-azioni.md` |

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

## Versioni: fatta la sostanza, resta la forma

Il 21 agosto la pagina è stata aperta per la densità e l'analisi ha trovato **tre buchi di sostanza** che
venivano prima: si poteva **eliminare un documento che un'altra persona stava editando**, «nascondi» non
chiedeva niente, «elimina» chiedeva **due volte**. Chiusi tutti
(carta [`2026-08-21-versioni-lock-e-azioni.md`](../feature/2026-08-21-versioni-lock-e-azioni.md), regole
95-103): badge «chi ci sta lavorando · fino a che ora», hide/delete inibiti **nel service**, force-unlock
admin, conferme in linea al posto di tre `window.confirm`, chip «in modifica» e per ACC, tasto «Aggiorna»,
permessi del markup allineati al grant ACC (li mostrava ai soli admin).

⚠️ **Buco dichiarato e non chiuso**: `AeroportoEditorPage` usa `IAirportEditingService`, **non**
`IEditingService`, quindi non prende il lock del documento — sugli aeroporti il badge non comparirà mai e
hide/delete non saranno mai inibiti. Portare l'aeroporto sul lock è un giro suo.

⚠️ Il lock del `Document` dura **30 minuti senza heartbeat** (`EditResourceLock` invece: 3 min + battito):
si rinnova al salvataggio e si libera con «Fine modifica». Chi chiude la scheda lo lascia in piedi fin quasi
a mezz'ora — è la ragione per cui il force-unlock non è un lusso.

### Quello che resta da fare su Versioni: la DENSITÀ

Misurato il 21 agosto guidandola: **1 664px** a 1600 (la ricognizione del 19 diceva 1 613).

- **sottotitolo** sempre a schermo (`Ver_Subtitle`) e **nessun `HelpHint`** in tutta la pagina;
- **3 callout in fascia** (errore, esito, e il riepilogo di campagna);
- **4 paragrafi** `muted`/`help` + 4 `span.muted`;
- ~**27 blocchi di stile in linea**;
- la pagina usa `.wrap` con `max-width:1100px`, **non** `.wrap.struct`: non è (ancora) una pagina di lavoro
  a piena larghezza. Decidere se portarla sul layout di lavoro fa parte del giro;
- i filtri sono `Chip` con stile in linea, **non** `.sh-chip` in gruppo come Aeroporti/Confinanti, e **non
  contano** (regole 30-32, 68). ⚠️ Adesso sono **quattro** gruppi (tipo, stato, release, ACC): la barra dei
  filtri è cresciuta, e la conversione a `.sh-chip` vale di più di prima;
- il conteggio filtrato è una riga di prosa (`Filtered().Count() … Ver_DocsWord`), non un contatore accanto
  al titolo.

**Le emoji.** 🟢 🕒 🕓 ⚠️ 🔒 — tutte **nel markup**, nessuna dentro i `.resx` (verificato), quindi si tolgono
senza toccare le traduzioni. ⚠️ Ma sono **vocabolario di stato**, non comandi: la regola 40 salva solo le
emoji che sono comandi. La decisione era di tenerle finché non c'è un set di pallini colorati (deferito in
`piano-ux-hardening`) — **da riconfermare prima di toccarle**. I glifi monocromatici (▾ ▴ ▸ ✎ ✕) restano
testo: è la regola.

⚠️ **Una riga resta alta 118px** invece di 67: quella con un lock altrui *e* i diritti da admin (sette
elementi più il force-unlock). Se il giro di densità accorcia le etichette, si richiuderà da sé.

Dopo Versioni la lista §15 continua con Permessi, Sorgenti, Audit, Diagnostica, Nuovo documento, Incarichi,
editor APP/vLOA.

## Aperto, e non è di queste pagine

- ⚠️ La **topbar** fa scorrere la pagina in orizzontale a 1280/1024: `div.right` misura **1 385px dentro
  1 280** (rimisurato il 21 agosto; il 20 erano 1 411), identico su home, struttura, viewer e versioni — e
  **niente dentro il `.wrap` sfora**, verificato elencando gli elementi oltre il bordo. È del chrome, non di
  una pagina: va affrontato per sé. È anche la ragione per cui lo sforo orizzontale, da solo, non è più un
  segnale utile sulle singole pagine finché questo non è chiuso.
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
