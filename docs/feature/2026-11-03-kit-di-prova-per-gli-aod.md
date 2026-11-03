# Il kit di prova per gli AOD (3 novembre 2026)

Filone Sector Lab. **Approvata** il 3 novembre come proposta in tre pezzi (il committente: «Sì, sei il Lab», dopo la
proposta «zip con avvio a un clic, Giro di prova dentro il Lab, esiti come issue sul fork»). Le scelte di dettaglio
(K1-K9) sono dell'agente e stanno qui; le fette fatte in §5.

## §0 — La domanda

Il committente, 3 novembre: «voglio distribuire il tool agli AOD così loro fanno i test. Però non ho il tempo di
spiegargli come funziona». Lo zip del 29 settembre (eseguibile `f27d4ed4`, slice 0-9) aveva un LEGGIMI coi passi; da
allora il Lab ha tre spazi, il primo avvio, git, l'AIP espanso, i Confronti, il fuoco, l'editor sulla mappa. Un LEGGIMI
lungo non lo legge nessuno, e le domande tornerebbero al committente.

## §1 — Com'è oggi

- **Il primo avvio** (20 ottobre) ha già i link dei due repo di prova come base (`PresaDelSector.IndirizzoDiBase`,
  `RepoDellAip.IndirizzoDiBase`) e la cartella di Aurora proposta; resta da scegliere la **cartella di lavoro**, vuota.
- **`SectorLab-prova\PrimoAvvio\`** ha un `.cmd` che avvia il Lab con `--dati` accanto e un LEGGIMI per il committente
  (non per un AOD: dice «git è già installato e GitHub ti conosce già»).
- **Le prove** stanno in `PROVE.md` (fuori repo) e nella pagina delle prove (artifact): sono scritte per il committente,
  coi numeri del suo 2611 e del sector vero.
- **GitHub** (guardato il 3 novembre): `SkyMistery/it-aurora-sector-test` è un fork **pubblico** con le **Issues spente**
  (i fork nascono così); `SkyMistery/it-aip-espanso-test` è **privato**, Issues accese. Un AOD non invitato non prende
  l'AIP espanso, e senza scrittura sul fork «Manda» non parte.

## §2 — I tre pezzi

1. **Lo zip che parte da solo**: l'eseguibile, un `Avvia il Lab.cmd`, un LEGGIMI di una pagina con quel che va
   installato. Il primo avvio arriva già compilato: i due repo di prova e una cartella di lavoro accanto allo zip.
2. **Il Giro di prova dentro il Lab**: un pannello che resta sopra le pagine, coi compiti uno alla volta, e per ognuno
   ✅ va · ❌ non va · ❓ non ho capito e una nota. I compiti sono la spiegazione.
3. **Gli esiti al committente**: «Manda gli esiti» apre nel browser una issue già compilata sul fork di prova (come la
   PR, D2 della F4: niente token nel Lab); l'AOD preme «Submit new issue».

## §3 — Le scelte

- **K1 — I dati accanto allo zip.** Il `.cmd` avvia con `--dati "%~dp0dati"`: impostazioni, registro, esiti e WebView2
  stanno nella cartella dello zip, e per ricominciare si cancella quella. Un Lab vero già installato non vede niente.
- **K2 — La cartella di lavoro proposta.** `--lavoro-proposto <cartella>` (il `.cmd` passa `"%~dp0lavoro"`): il campo
  «Cartella di lavoro» del primo avvio nasce scritto. Si può cambiare; il Lab non la crea prima di «Prepara tutto».
- **K3 — Il giro si accende col kit.** `--giro-di-prova` (dal `.cmd`) accende il Giro; senza, il Lab è com'era (il
  committente non se lo trova in mezzo). Il giro acceso si ricorda nei dati: riaperto senza il `.cmd`, resta.
- **K4 — I compiti stanno nel Lab** (`GiroDiProva.Compiti`, in `Vipi.SectorLab.Ui` perché portano a una pagina), non in un file accanto: si cambiano con un'uscita del
  Lab come il resto, e un test guarda che ogni compito porti a una pagina che c'è. Sono scritti per chi non ha mai
  visto il Lab, senza numeri del sector vero (il fork di prova può cambiare): «dovresti vedere» dice la forma, non il
  conto.
- **K5 — Il pannello del giro**: un tasto «🧪 Giro di prova 3/12» nella barra lo apre e lo chiude; il pannello sta in basso
  a destra sopra la pagina (anche Consegna e Impostazioni, che non hanno la mappa), largo 360 px. Dentro: il compito
  (titolo, «Fai», «Dovresti vedere»), «Vai a …» che apre la sua pagina, i tre esiti, la nota, ‹ ›, e una fila di pallini
  (uno per compito, col colore dell'esito) per saltare. Dato un esito si passa al compito dopo.
- **K6 — «❓ Non ho capito» è un esito come gli altri**: è il segnale che serve al committente (dove il Lab non si spiega
  da solo), non un ❌.
- **K7 — Gli esiti si salvano a ogni gesto** in `giro-di-prova.json` nei dati (compito, esito, nota, quando); un
  riavvio riprende dal primo compito senza esito.
- **K8 — La issue**: sul repo del sector del primo avvio (`IndirizzoDelSector`, il fork di prova), titolo «Giro di
  prova · VID 123456 · 03/11/2026», testo in markdown: chi (VID, versione del Lab con l'impronta del commit, Windows), una riga per
  compito con l'esito e la nota, poi gli errori del registro (le righe `ERRORE` degli ultimi giorni, la prima riga di
  ognuna, al massimo 15). Il link ha un tetto (6 000 caratteri di testo): se non ci sta, il testo si taglia e dice
  «il testo intero è in …\esiti-del-giro.md: trascinalo qui». Il file si scrive sempre. Senza VID il giro lo chiede
  prima di mandare (il primo compito è scriverlo).
- **K9 — Il committente**: attiva le Issues sul fork di prova e invita gli AOD sui due repo di prova (l'AIP espanso è
  privato). Sono clic suoi (impostazioni del repository); il LEGGIMI dice all'AOD di accettare l'invito.

## §4 — I compiti (G1-G12)

| # | Pagina | Fai | Dovresti vedere |
|---|---|---|---|
| G1 | Impostazioni | Scrivi il tuo VID; scegli il profilo di Aurora che usi | Il VID resta; i punti sulla mappa coi simboli del tuo profilo |
| G2 | Sector · Mappa | Sfoglia la colonna di sinistra, scegli un file e un record | La Scheda a destra coi gruppi (Dati, Geometria, Collegamenti…), la forma accesa sulla mappa |
| G3 | Sector · Mappa | Linguetta **Scali**, il tuo scalo, **◎ Lavora su …** | La mappa sul sedime, solo lo scalo; la colonna resta sullo scalo |
| G4 | Sector · Mappa | Scegli uno stand o una taxiway; **✎ Modifica sulla mappa**, trascina un vertice o lo stand | La forma si sposta; in barra «Salva (1)» |
| G5 | Sector · Consegna | Guarda la modifica col suo diff; **Annulla** | Le righe prima e dopo; annullata, la barra torna a «Salva» |
| G6 | Sector · Mappa | Linguetta **Settori**, una posizione, **◎ Lavora su …**, poi **esci** | Il settore e, in grigio, quelli accanto; uscito, il sector intero |
| G7 | AIP espanso · Indice | Cerca una voce del tuo scalo (una SID, un punto) e aprila | La scheda della voce e la sua forma sulla mappa |
| G8 | Confronti | Apri **Punti significativi**: guarda uguali, diverse, mancanti; apri una riga diversa | I due lati sulla mappa, arancio (AIP) e blu (sector); il gesto per portare l'AIP nel sector |
| G9 | Sector · Testo | Apri un file, cambia una riga, **Applica**; poi annulla in Consegna | La modifica in sospeso come le altre |
| G10 | Sector · Mappa | **Problemi** (in barra «N errori · M avvisi»): aprine uno | La voce del problema e il record che lo dà |
| G11 | Sector · Consegna | Una modifica vera e piccola sul tuo scalo: **Salva**, **Registra il sector**, **Manda** | Si apre GitHub con la PR già scritta verso il fork di prova: premi «Create pull request» |
| G12 | — | Che cosa ti è mancato? Cosa ti serve che il Lab non fa? | (scrivilo nella nota) |

## §5 — Le fette

1. **Primo avvio dal kit** (K1-K3): `--lavoro-proposto`, `--giro-di-prova`, ricordato nei dati.
2. **Il Giro** (K4-K7): `GiroDiProva` nel Core (compiti, esiti, file), il pannello e il tasto nella barra.
3. **Manda gli esiti** (K8): il testo, il link della issue, il file.
4. **Lo zip**: `tools/kit-aod/prepara-kit.ps1` (dal commit registrato) pubblica il Lab in `app\`, mette accanto
   `Avvia il Lab.cmd` e il `LEGGIMI.txt` (in `tools/kit-aod/`, con a capo di Windows e l'impronta), fa l'autoprova e lo
   zip `AuroraSectorLab-prova-<sha>.zip` in `SectorLab-prova\kit-aod\` (fuori repo). Prova da zero: lo zip estratto in
   una cartella nuova, primo avvio coi repo di prova veri.

   Dentro lo zip:

       Aurora Sector Lab (prova)\
         Avvia il Lab.cmd     --dati dati  --lavoro-proposto lavoro  --giro-di-prova
         LEGGIMI.txt          cosa installare (GitHub e i due inviti, Git for Windows, Aurora), come si avvia
         app\                 il Lab completo, win-x64, self-contained
         dati\  lavoro\       nascono al primo avvio
