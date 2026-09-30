# Statistiche ATC: la nuova veste della pagina personale e di quella della divisione — carta (30 settembre 2026)

> **Stato: ✅ ESEGUITA il 30 settembre 2026** sul ramo `fix/statistiche-grafica` (filone Sito, S72), in fila su
> `fix/titolo-documento`, provata a schermo su una copia del DB (§4). Migrazione `NomeBreveAccessi` (una colonna nel registro degli accessi). Nessun `deploy/`.
> Nasce da una richiesta del committente (30 settembre 2026): «vorrei ripensare la grafica della pagina delle
> statistiche… prima la progettazione della UI, discutiamone e poi la applichi al sito», fedele al resto del sito.
> Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md). Registro degli accessi: [2026-09-30-registro-accessi.md](2026-09-30-registro-accessi.md).
>
> Le tavole (Claude Design, private del committente): pagina personale desktop e telefono, pagina della divisione
> desktop e telefono. Sono il riferimento a schermo di questa carta.

## 1. Le decisioni del committente (30 settembre 2026)

1. **Testata con il nome.** Il proprio per intero; di un altro il **nome breve** («Mario R.») se è entrato nel sito
   almeno una volta, altrimenti quello del roster, altrimenti il VID. Sotto: postazione preferita e **settimane di
   fila**; a destra la posizione in classifica («7° su 498 in divisione · per ore»).
2. **I numeri hanno un contesto.** Su 30 e 90 giorni il confronto col periodo prima (come ora); su 12 mesi e «tutto»
   il periodo prima non esiste (la sorgente tiene dodici mesi) e sotto il numero va la **media della divisione per
   controllore**. Presenze restano, con la nota «tratte viste, parcheggiati compresi». Nuovo: durata media del turno,
   con quella della divisione.
3. **Ordine**: numeri · ore per mese | dove controlli · quando (mappa giorno × ora) · aeroporti | aeromobili · ultimi
   turni (dieci, gli altri sotto «Mostra tutti»).
4. **La mappa giorno × ora torna sulla pagina personale** («Mappa come nella tavola»), superando la decisione del
   25 agosto 2026 che l'aveva lasciata alla sola divisione: senza numeri nelle caselle (erano quel che la rendeva
   illeggibile) e con le ore di ogni giorno a destra.
5. **Classifica della divisione con i nomi**: «Nome + iniziale del cognome + VID» per chi ha fatto il login, visibile a
   chi vede la classifica (lo staff, o tutti gli utenti entrati se è pubblica). Chi non è mai entrato resta un VID.
   IVAO consente di mostrarli; l'informativa lo dice.
6. **Pagina della divisione**: stesso stile; l'ordine resta com'è, con gli aeroporti subito sotto i numeri come già
   chiesto. Aeroporti e ricerca per VID restano allo staff.

## 2. Pre-flight

**1. Modello.** Un concetto nuovo: il **nome breve** di chi entra nel sito, `AccessoAlSito.NomeBreve`, composto al
login dai claim nome/cognome (`ComponiNomeBreve`: il nome intero, l'iniziale del cognome e il punto). Si legge solo a
gruppi di VID (`IRegistroAccessi.NomiBreviAsync`), mai il nome intero fuori dal registro. Il resto sono numeri che le
statistiche già calcolavano (`TotalsAsync(null, …)` per la divisione, il rango, il profilo giorno × ora).

**2. Dispatch.** Nessuno nuovo: quale riferimento sotto un numero (confronto o media) dipende solo dal periodo
(`periodo.Days <= 90`).

**3. Sicurezza.** Il nome breve compare dove compare già il VID dello stesso controllore, a chi può già vederlo: la
visibilità della classifica non cambia. La ricerca per VID e gli aeroporti restano dietro lo stesso cancello staff.

**4. Migrazione.** Una colonna nullable (`NomeBreve`, 60) in SQLite e MySQL; Postgres la riconcilia da solo. Le righe
esistenti restano senza nome breve fino al prossimo accesso.

## 3. Che cosa cambia, in breve

- Componenti: `StatsKpi` ha una `Nota` sotto l'etichetta; `CoverageHeatmap` ha `Numeri`, `Totali` e, sulla pagina
  personale, i toni **rispetto alla casella più piena** (chi controlla copre al massimo qualche percento di una
  fascia: con le soglie della copertura sarebbe tutto chiaro).
- 🔴 **Difetto trovato per strada**: le caselle della mappa avevano la classe letterale `cov-q@q` (Razor la leggeva
  come un indirizzo email), quindi la mappa della divisione **non si era mai colorata**. Ora `cov-q@(q)`.
- Pagina della divisione: cinque numeri con la media per controllore (ore, turni, movimenti), chi conta come
  controllore, durata media del turno; aeroporti (staff) in un riquadro con le barre dei movimenti e della quota
  coperta, l'avviso del consolidamento in corso accanto al titolo, i primi dieci e «Mostra tutti»; ore per mese
  accanto alla barra divisa per tipo (sessioni, ore, quota); copertura; classifica col podio, i nomi brevi
  (`IRegistroAccessi.NomiBreviAsync` sui cinquanta VID), i primi dieci più la propria riga in coda («…26»),
  «Mostra i primi 50», la ricerca per VID (staff) nella testata del riquadro; postazioni su due colonne;
  strumenti dello staff come riquadri.
- Stile: blocco `sx-*` in `vipi-theme.css` (riquadri, righe a due colonne, righe a barra, barra divisa per tipo di
  postazione), soglie sul contenitore come il resto del tema.

## 4. Prove

- Test Ui: il nome breve in testata di chi è guardato (`StatsProfileAccessTests`); in classifica il nome breve di
  chi è entrato e il solo VID degli altri, i primi dieci più la propria riga (`StatsDivisionPageTests`). Ui 1925. Domain, App e Infra: nome breve
  composto, registrato, letto a gruppi.
- **A schermo** su una copia del DB (VID 764245, 7° su 498): scuro 1280 px, chiaro 375 px senza scorrimento
  orizzontale, mappa colorata (76/64/19/7/2 caselle per tono). Divisione in scuro 1280 px e chiaro 375 px, con
  nomi brevi inventati scritti nella copia: podio e classifica coi nomi, 26° in coda, «Mostra i primi 50» → 50
  righe, schede degli aeroporti allineate sul telefono.
