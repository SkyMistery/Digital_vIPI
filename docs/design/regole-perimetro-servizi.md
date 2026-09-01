# Il perimetro dei servizi — che cosa entra in questo sito e che cosa no (1 settembre 2026) 🟣

> Gemello di [regole-brand](regole-brand.md), [regole-lingua](regole-lingua.md) e
> [regole-ui-pagine-admin](regole-ui-pagine-admin.md): quelle governano colori, lingua e densità, questa
> governa **quali strumenti hanno diritto di stare qui**.
>
> **Perché esiste.** Il 1 settembre 2026 sono state proposte **otto** idee di servizio nuovo. Il committente
> ne ha accettate due, ne ha assorbita una dentro uno strumento che c'è già e ne ha respinte cinque. I «no»
> valgono più dei «sì»: senza il motivo scritto, fra sei mesi le stesse cinque idee tornano identiche — e
> sembrano tutte buone idee, perché *lo sono*. Quel che le esclude non è la qualità: è il perimetro.

## Che cos'è questo sito

Il **contenitore degli strumenti ATC della divisione italiana**: la documentazione operativa (le cinque
famiglie di vSOP) e gli strumenti che la servono. Non è il portale della divisione, non è la piattaforma
IVAO, non è il dipartimento training.

## Le cinque regole

| | Regola | Conseguenza pratica |
|---|---|---|
| **P1** | Un servizio nasce **sul dato che vive già qui**. Se il dato non c'è, prima si discute del dato. | Le statistiche ATC sono nate dopo l'archivio delle sessioni, non prima. |
| **P2** | **Non si duplica uno strumento che IVAO ha già**: Webeye, il booking degli eventi, il sito del training. | Una mappa live nazionale «più bella» resta una seconda copia da tenere allineata. |
| **P3** | Il sito **documenta e assiste**; non **valuta** né **sorveglia** le persone. | Niente registri di lettura, niente quiz, niente libretti di competenza. |
| **P4** | Uno strumento **generico** non entra: entra ciò che è legato ai documenti o all'operatività della divisione. | Il convertitore di coordinate sta qui perché serve a **inserire i dati dei documenti**, non perché è un calcolatore. |
| **P5** | **Un servizio è figlio diretto di `/services`.** Ciò che completa uno strumento esistente si aggiunge **a quello strumento**, non all'hub. | I vSOP militari e gli spazi aerei sono `shortcut`, non servizi (vedi `ServicesHome.razor`). |

⚠️ **P4 non espelle ciò che è già dentro.** Il convertitore di coordinate resta dov'è: la sua
giustificazione non è «è comodo», è che senza di lui i punti di un documento si inseriscono a mano
convertendoli altrove. Una regola scritta dopo non si usa per demolire ciò che una ragione ce l'aveva.

---

## Le otto idee del 1 settembre 2026, e il loro esito

| # | Idea | Esito | Regola |
|---|---|---|---|
| 1 | **Briefing di apertura posizione** (METAR, TL, piste, chi è online, che cosa è cambiato) | 🟡 **ASSORBITA** nella vista live — non è un servizio nuovo | P5 |
| 2 | Presa visione delle release + quiz sui documenti | 🔴 **NO** | P3 |
| 3 | **Coerenza vIPI ↔ sectorfile** | 🟢 **SÌ**, e **visibile** come scorciatoia (`/services/vsop/sectorfile`, scheda nella sezione staff, **livello Editor**) → [piano-coerenza-sectorfile.md](piano-coerenza-sectorfile.md) | P1 + P5 |
| 4 | Copertura eventi / turni ATC | 🔴 **NO** | P2 |
| 5 | **Segnalazioni dal campo** | 🟢 **SÌ** → [piano-segnalazioni.md](piano-segnalazioni.md) | P1 |
| 6 | Cassetta degli attrezzi (calcolatori vari) | 🔴 **NO** | P4 |
| 7 | Mappa live nazionale | 🔴 **NO** | P2 |
| 8 | Libretto del controllore / percorso training | 🔴 **NO** | P2 + P3 |

---

## 🟡 1. Il briefing non è un servizio: è una funzione della vista live

**Decisione del committente:** *«si può incorporare senza problemi nella visuale live, permettendo nella
pagina operativa di selezionare una qualsiasi postazione ATC e vederne la corrispondente pagina, in modo da
non dover creare una seconda pagina ma solo aggiungere una funzione a quella esistente».*

Ed è la lettura giusta, perché la pagina **c'è già ed è già fatta per questo**: `LivePage.razor` ha due
rotte — `/services/vsop/live` (la postazione con cui sei connesso) e `/services/vsop/live/{callsign}` (una
qualunque, in consultazione). La seconda esiste dal refactor 12: quel che manca non è la pagina, è **il
selettore** che ci porta senza scrivere l'indirizzo a mano, più i pezzi di briefing dentro la vista.

⚠️ **Nota per chi la eseguirà:** i mattoni sono quasi tutti già in casa —
`Vipi.Infrastructure/Weather/NoaaWeatherClient.cs` (METAR), le quote di transizione per fascia di QNH
(`AirportTransitionLevel`), le regole di scelta pista (`AirportRunwayRule`), e trasferimenti risolti, AoR e
catena di copertura sono **già dentro `LiveView`**. È un lavoro di composizione e di selettore, non di
motore. Vuole comunque la sua carta prima del codice (FEATURE-PROCESS), che questa non è.

## 🔴 2. Presa visione e quiz — «non è l'obiettivo dell'app»

**Parole del committente:** *«No, assolutamente no, non è l'obiettivo dell'app.»*

**Perché la regola vale oltre il caso.** Un registro di chi ha letto che cosa cambia la **natura del
rapporto** col lettore: da strumento che aiuta a controllo che verifica. Il sito diventerebbe il posto dove
si va perché *si deve*, e lo staff si troverebbe in mano un elenco di inadempienti che non ha chiesto e che
poi dovrebbe gestire. È **P3**: si documenta e si assiste, non si valuta.

⚠️ Conseguenza da ricordare: **non è un «no» al tracciamento tecnico**. «La copia pubblicata è indietro» e
«questo documento va rivisto» restano — sono fatti sui **documenti**, non voti sulle **persone**.

## 🔴 4. Copertura eventi e turni — «c'è un sito apposito»

**Parole del committente:** *«No, c'è un sito apposito per quello.»*

IVAO ha il proprio sistema di eventi e prenotazioni ATC. Rifarlo qui vorrebbe dire tenere allineati due
elenchi di turni: il primo giorno in cui divergono, quello sbagliato è **il nostro** — perché il posto dove
la gente si prenota davvero resta l'altro. È **P2** nella sua forma più cara: il duplicato non costa il
lavoro di scriverlo, costa il disallineamento per sempre.

## 🔴 6. Cassetta degli attrezzi — fuori scopo

**Parole del committente:** *«No, esce dallo scopo della webapp.»*

Un raccoglitore di calcolatori (holding, top of descent, separazione di scia) non è legato né ai documenti
né all'operatività della divisione: è un sito di utilità aeronautiche, cioè un altro prodotto. **P4**.

⚠️ E vale il chiarimento di sopra: questo **non** rimette in discussione il convertitore di coordinate, che
serve a scrivere i documenti.

## 🔴 7. Mappa live nazionale — esiste Webeye

**Parole del committente:** *«Già esiste Webeye quindi no.»*

**P2**. ⚠️ Da non confondere con la **vista live per postazione**, che è tutt'altra cosa e resta: Webeye
mostra *chi c'è nel mondo*, la nostra vista mostra *che cosa devi fare tu adesso su questa posizione* —
frequenze, trasferimenti, AoR, documento. Il «no» riguarda la vista d'insieme, non la vista operativa.

## 🔴 8. Libretto del controllore — «esiste un sito apposta per il training»

**Parole del committente:** *«No, esiste un sito apposta per il training.»*

**P2** (il dipartimento training ha la sua piattaforma) e **P3** insieme (un libretto di competenze è una
valutazione di persone). Le ore per posizione restano dove sono — nelle **statistiche**, che sono un fatto
misurato e non un giudizio.

---

## La prossima volta

Chi propone un servizio nuovo risponde prima a queste cinque righe:

1. **Su quale dato che è già qui si regge?** (P1)
2. **IVAO ce l'ha già altrove?** Se sì, il lavoro è un link, non un servizio. (P2)
3. **Misura una cosa o giudica una persona?** (P3)
4. **Servirebbe anche a chi non usa i nostri documenti?** Se sì, è un altro prodotto. (P4)
5. **È uno strumento nuovo o la parte mancante di uno che c'è già?** (P5)

Se le risposte ci sono, si scrive la **carta** ([FEATURE-PROCESS](../FEATURE-PROCESS.md)) e poi il codice.
Mai al contrario.
