# Annuncio Discord — 1.5.0

> Da incollare così com'è nel canale della divisione. Sotto, la versione inglese e una riga sola per chi
> vuole solo il titolo. Il markdown è quello che Discord capisce: `**grassetto**`, `> citazione`,
> `` `codice` ``, e le liste con `-`.

---

## 🇮🇹 Versione italiana

**📢 vIPI — aggiornamento 1.5.0**

Due cose: una che si rompeva e non si romperà più, e una nuova per chi prepara gli AIRAC.

**🔧 Correzioni**
- **L'editor non si blocca più aprendo un blocco «Allegato».** Era il difetto per cui la pagina smetteva di rispondere e bisognava ricaricare: capitava aprendo un documento che cita un allegato. Risolto.
- Lo stesso problema, più nascosto, era anche nel riquadro **«Validità e revisione»** (che sta in ogni documento) e nella resa delle **vLOA**: sistemati tutti e tre.
- Se la lista degli allegati non si carica, adesso **lo dice** invece di lasciare una tendina vuota che sembrava una biblioteca vuota.

**🆕 Novità — preparare il ciclo AIRAC**
- Su **Bozze e versioni** c'è una sezione nuova, **«Prossimo AIRAC»**: dice qual è il ciclo che sta per entrare, quando entra e **quanti documenti non hanno ancora una pubblicazione programmata** a quel ciclo. Con un tasto le programma tutte.
- ℹ️ Una pubblicazione programmata **entra in vigore da sola** alla data del ciclo: non serve tornare sul sito il giorno del cambio. E la fotografia che salva è quella *di quel ciclo*, quindi contiene già le SID e i confini che entrano allora.
- La lista **«Da fare»** adesso avvisa **prima** del cambio ciclo, con una riga «da preparare», invece di accorgersene il giorno dopo.
- **Le SID escono al ciclo giusto.** Prima il ciclo di una SID importata dipendeva dall'ora in cui era passato il giro automatico, e poteva restare nascosta **un mese in più** senza che niente lo dicesse. Adesso il ciclo lo dichiara il sectorfile stesso.

**Serve fare qualcosa?** No. Nessun cambiamento ai documenti, niente da rifare. Chi cura le vIPI trova la sezione nuova in cima a «Bozze e versioni».

---

## 🇬🇧 English version

**📢 vIPI — update 1.5.0**

Two things: something that used to break and won't any more, and something new for whoever prepares AIRAC cycles.

**🔧 Fixes**
- **The editor no longer freezes when you open an “Attachment” block.** That was the bug where the page stopped responding and you had to reload — it happened on documents that cite an attachment. Fixed.
- The same problem, less visible, was also in the **“Validity and revision”** box (which appears in every document) and in how **vLOAs** are rendered: all three are fixed.
- If the attachment list fails to load, it now **says so** instead of leaving an empty dropdown that looked like an empty library.

**🆕 New — preparing the AIRAC cycle**
- **Drafts & versions** has a new section, **“Next AIRAC”**: it tells you which cycle is about to start, when it starts, and **how many documents have no release scheduled** for it yet. One button schedules them all.
- ℹ️ A scheduled release **takes effect on its own** on the cycle date: no need to come back on changeover day. And the snapshot it saves is the one *for that cycle*, so it already contains the SIDs and boundaries that start then.
- The **“To do”** list now warns you **before** the cycle changes, with a “to prepare” row, instead of noticing the day after.
- **SIDs now appear at the right cycle.** Previously the cycle of an imported SID depended on when the automatic run happened to pass, and a SID could stay hidden **a month longer** with nothing to say so. Now the cycle is the one the sector file itself declares.

**Anything to do?** No. No changes to your documents, nothing to redo. If you curate vIPIs, the new section is at the top of *Drafts & versions*.

---

## Una riga sola

> **vIPI 1.5.0** — risolto il blocco dell'editor sui blocchi «Allegato», e su *Bozze e versioni* arriva **«Prossimo AIRAC»**: dice quanti documenti non hanno ancora una pubblicazione programmata al ciclo entrante, e le programma tutte con un tasto.

---

## ⚠️ Note per chi scrive il messaggio, da NON incollare

- **Non citare numeri di versione dei difetti** né i nomi dei componenti (`AttachmentBlockEditor`, `ValidityStamp`): a chi legge non dicono niente, e trasformano un annuncio in un changelog.
- **Il tasto «Programma i mancanti» scrive davvero.** Se qualcuno lo prova per curiosità, crea pubblicazioni programmate. Vale la pena dirlo solo se il messaggio va a un canale dove ci sono persone con i permessi di redazione.
- **Non serve annunciare** il registro eventi di Windows, lo sweep delle release o i tempi d'avvio: sono cose nostre, invisibili a chi usa il sito.
- ℹ️ La **prima volta in cui «Prossimo AIRAC» servirà davvero** è la preparazione del **2610 (1º ottobre)**: per il 2609 il sectorfile non aveva ancora dati nuovi.
