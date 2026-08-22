# Handoff — il ramo del brand IVAO (22 agosto 2026)

> **A cosa serve.** Ripartire a freddo sul ramo `brand-atmosphere` senza rileggere la cronologia.
> Chi deve **scrivere un colore o un font** legge solo [`docs/design/regole-brand.md`](../design/regole-brand.md).
> Chi vuole sapere **cosa è stato trovato e misurato** legge
> [`docs/feature/2026-08-22-brand-atmosphere.md`](../feature/2026-08-22-brand-atmosphere.md).

## Dove siamo

Ramo **`brand-atmosphere`**, allineato col remoto, **non fuso in `main`**. Tre commit:

| commit | cosa |
|---|---|
| `a7ce633` | allineamento al brand: font scambiati, 501 letterali → token, tema scuro |
| `f197fc3` | il tema lo sceglie l'utente: automatico / chiaro / scuro |
| `c662d5e` | la legenda del visore 3D era bianca su bianco: quel foglio era sfuggito ai token |

Cancello: `dotnet build -c Release` (**0 avvisi**, gli avvisi sono errori) + `dotnet test` — **1520 verdi**.

⚠️ Il lavoro tocca **solo** presentazione: nessuna modifica a modello, rotte, dati o migrazioni. Non ha
quindi il vincolo che blocca il ramo `accordi-coordinamento` (la conversione della MariaDB di produzione).

## La cosa da sapere prima di tutte le altre

La fonte del brand è **[ivaoaero/atmosphere](https://github.com/ivaoaero/atmosphere)** →
`brand/src/tokens.json`, e contiene **solo `color` e `font`**. Spaziature, raggi e regole sul logo **non
sono brand**: sono decisioni di prodotto, e restano nostre.

⚠️ **I font erano scambiati.** Il brand vuole **Poppins ai titoli** e **Nunito Sans al corpo** (più IBM
Plex Mono). L'errore non era deriva: era scritto a tavolino in `piano-vipi-tool.md` §15.2, ora corretto.
Se una carta vecchia dice «Nunito Sans → titoli», è vecchia.

## Com'è fatto il tema adesso

`vipi-theme.css`, tre livelli dentro `:root`:

1. **Livello 1 — copia, non progetto.** La scala di brand alla lettera, coi nomi che
   `@ivao/atmosphere-brand` emette (`--ivao-color-atmos-700`, …). **L'unico posto dove un esadecimale è
   legittimo** (97, tutti lì). Il giorno che l'ospitante importa `tokens.css`, questo blocco si cancella e
   basta: i livelli 2 e 3 continuano a funzionare senza una modifica.
2. **Livello 2 — ruoli.** `--surface`, `--ink`, `--brand-ink`, `--ok-ink`, … Valori sempre dal livello 1.
3. **Livello 3 — tinte e categorie**, derivate con `color-mix()`.

**Sotto il blocco dei token non c'è un solo colore letterale**, ed è quello che rende possibile il tema
scuro: ridefinisce **solo i token**, mai una regola del corpo.

Il tema lo sceglie l'utente (automatico / chiaro / scuro), `vipi-theme-mode.js`, `localStorage`.

## Le cinque trappole che sono costate un giro ciascuna

⚠️ **`background:#fff` è ambiguo.** `--surface` (si gira) e `--on-brand` (non si gira) distano **zero**
da `#ffffff`: la scelta non la fa la distanza, la fa **il selettore**. Sbagliarla lascia 58 superfici
bianche col testo bianco sopra — e **si vede solo spegnendo la luce**.

⚠️ **Il blu di brand fa due mestieri opposti**: 112 usi come colore del testo, 30 come fondo. Al buio il
testo deve schiarirsi e il fondo no. Sono `--brand-ink` e `--ivao-blue`.

⚠️ **`color-scheme` va dichiarato**, o `<input>`, `<select>` e le barre di scorrimento restano chiari
qualunque cosa dica il CSS.

⚠️ **La carta è sempre chiara.** I due agganci del tema scuro stanno sotto `@media screen`. Senza, chi ha
il sistema in tema scuro stampa un documento operativo a blocchi neri.

⚠️ **`color-mix()` non si serializza in `rgb()`**: `getComputedStyle` rende `color(srgb 0.95 0.95 0.97)`
coi canali fra **0 e 1**. Ha già fatto sbagliare due volte — una sonda di contrasto e il rilevatore di tema
del visore 3D. Chi legge un colore da JS deve gestirlo.

## Come si verifica — e perché i test non bastano

**I test sono verdi anche con un tema completamente rotto.** La verifica vera è a schermo:
`.claude/skills/verifica-live/` (`driver.js`, `sweep.js`, `probe.js`).

⚠️ **La lezione del terzo commit.** `vipi-aor3d.css` è **sfuggito** alla passata sui token e nessuno se
n'è accorto: nel tema chiaro non si vedeva, nessun test lo guardava, e `driver.js` controllava solo cinque
pagine. Il difetto l'ha trovato **il committente**, usando l'applicazione. Da lì i due controlli che
prendono l'intera classe:

- `node sweep.js` → fondi che non si sono girati, su 12 pagine in tema scuro;
- il **lint statico** sulle `font-family` scritte per nome — ⚠️ **anche su `.razor` e `.js`**, non solo sui
  fogli: due dei casi stavano in uno stile in linea e in una stringa costruita da JavaScript.

Tutt'e due hanno falsi positivi noti, scritti in testa ai file. Leggerli prima di «correggere» qualcosa che
non è rotto.

## Cosa NON è stato ricondotto al brand, e perché

Non sono sviste: ognuna ha il perché scritto accanto nel codice.

- **`AorColorScheme`** — colori **cartografici**: gli anelli AoR si sovrappongono e si riempiono al 16%, e
  i passi di brand a piena saturazione a quell'opacità diventano indistinguibili. Inoltre finiscono in un
  `<input type=color>` e nel DB, quindi devono restare esadecimali veri.
- **`--cat-*`** — insieme **categoriale**: il loro lavoro è distinguersi *fra loro*. ⚠️ Ma sul **testo** i
  valori pieni non reggono AA, perciò esistono anche `--cat-*-ink`.
- **`--nbr-ink`** (viola «confinante / FSS») — il viola non esiste nella palette come colore semantico, e
  l'unico del brand (`product.creators`) su bianco fa 4.23:1, sotto AA.
- **`--ink-faint` / `--ink-dim`** — non reggono AA e non devono: sono il tono spento. I valori di prima
  erano **peggio**.

## Cosa resta aperto

1. **Il logo IVAO non è usato da nessuna parte.** Atmosphere ha un componente `IVAOLogo` pronto (SVG,
   varianti orizzontale/icona, `white`/`atmos`); noi abbiamo un quadrato con la scritta `vIPI`. Non usarlo
   è più prudente che usarlo male, ma è una **scelta da fare**, non un difetto da correggere in silenzio.
   ⚠️ Il logo IVAO è un marchio e la licenza Apache 2.0 §6 non ne concede l'uso: prima di metterlo, va
   chiesto.
2. **`MainLayout` / `NavMenu` dell'host** sono ancora il template Blazor di serie nella **struttura**:
   barra laterale, link «About» a `learn.microsoft.com`, `blazor-error-ui` con testo **in inglese** in
   un'applicazione localizzata. Qui sono stati portati sui token solo i **colori**: cosa debba mostrare
   quella pagina è prodotto, non brand. Si vede solo sulla pagina d'errore (la home rimanda a `/vsop`).
3. **Le tessere della mappa base restano chiare** nel tema scuro: arrivano da CARTO `light_all` ed esiste
   `dark_all`. Una riga — ma i colori dei poligoni sono tarati su una base chiara e vanno rimisurati.
4. **La scelta del tema è per browser**, non per utente: sta in `localStorage`, non nel profilo. Se dovrà
   seguire la persona fra dispositivi, va nel DB — e allora conviene accorparla alla migrazione
   dell'archiviazione incarichi, che è già in attesa (una migrazione sola).
5. **Il tema scuro non è stato guardato pagina per pagina.** Verificate a schermo: landing, vIPI ACC,
   elenco aeroporti, struttura admin, guida, visore 3D. Editor, vista live e blocchi mappa hanno il tema
   per costruzione — non contengono più colori propri — ma non sono stati aperti uno per uno. `sweep.js` li
   copre in automatico ed è pulito, il che è una garanzia sui **fondi**, non sull'estetica d'insieme.
6. ⚠️ **Un test rosso non identificato.** Su quattro giri della suite, **uno** ha segnato 1 fallimento; i
   tre successivi sono stati 1520/1520 e non è stato possibile catturare quale fosse. I due soli test che
   toccano il markup modificato (`AccAor3dTests`, `StructureComponentsTests`) non guardano né font né
   colori e passano. Classificato come **ballerino**, non come regressione — ma resta non identificato, e
   chi lo rivede dovrebbe segnarselo.
