# I profili per l'evento (30 settembre 2026)

**Chiesto dal committente**: una sezione in `/services` che porti a una pagina **aperta a tutti**, da accendere
vicino a un evento. Lo staff ci carica i profili per postazione, chi controlla li scarica. In più, un posto per i
link di Google Drive: lo zip dello Stand Manager passa di lì. Dopo l'evento la pagina si spegne. Per quello
successivo si riaccende col nome nuovo.

## 1. Le scelte del committente

| Domanda | Scelta |
|---|---|
| Che cosa si mette nella pagina | **File e link**: il file caricato sul sito (profilo per postazione), il link per Drive |
| Come si accende | **Interruttore a mano, più date facoltative** (inizio e fine, UTC) |
| L'evento successivo | **Le voci restano**, lo staff toglie quelle che non servono (lo Stand Manager è sempre lo stesso) |
| Chi lo gestisce | **Staff di divisione** |

## 2. Com'è fatto

- **Un pacchetto solo**, riusato: tabella `EventKits` (una riga) con nome, `IsActive`, `StartsUtc`, `EndsUtc`, chi
  l'ha toccato per ultimo. Le voci in `EventKitItems`: titolo, nota, tipo (`File`/`Link`), link oppure nome del file,
  peso e byte, ordine. Migrazione `ProfiliEvento` (SQLite e MySQL), **solo additiva**: due tabelle e un indice.
- **Si vede** se acceso **e** dentro le date, quando ci sono (inizio compreso, fine esclusa). Spento vince su tutto:
  le date servono a non dover stare svegli all'ora giusta, non sono un secondo interruttore
  (`EventKitRules.Visibile`).
- **`/services/event`**: SSR statica per il pubblico, perché la sera dell'evento la aprono tutti insieme e un
  circuito per ognuno sarebbe il prezzo di un elenco di link. Spenta, dice che non c'è un evento in corso. Lo staff
  ci trova sopra il **pannello di gestione** (`EventKitManager`), un'isola interattiva: nome, date, interruttore,
  «Aggiungi un file», «Aggiungi un link», voci con su / giù / elimina (con conferma).
- **L'hub**: quando il pacchetto si vede, una sezione «Evento in corso» **sopra gli strumenti**, con il nome
  dell'evento nella scheda. Allo staff, nella sua sezione, la scheda «Pacchetto dell'evento» c'è sempre: è da lì che
  lo si accende. La risposta «c'è un evento?» si tiene in memoria 30 secondi (`EventKitVisibilityCache`, una per
  processo) e si svuota a ogni scrittura: l'hub lo apre chiunque arrivi al sito.

## 3. I file

- I byte stanno **nella riga**, come le immagini dei documenti (`MediaAsset`): sono file da pochi KB, e un deposito
  esterno sarebbe un secondo posto da tenere in ordine. Il tetto è quello delle immagini (`Media:MaxUploadBytes`,
  3 MB). Quel che non ci sta va su Drive, ed è il caso dello zip dello Stand Manager.
- **Estensioni ammesse**, una lista che ammette e non una che vieta: `.cpr .clr .isc .sct .ese .pof .txt .xml .json
  .ini .pdf .zip .7z .rar`. Un `.exe` o un `.html` serviti da questo dominio a chiunque sarebbero un regalo a chi
  entrasse con un account staff rubato.
- Il nome del file perde le cartelle, i caratteri di controllo e le virgolette, e non supera 200 caratteri
  (l'estensione resta).
- **`/services/event/file/{id}/{nome}`** serve il file: al pubblico solo mentre il pacchetto si vede, allo staff
  sempre (per provarlo prima di accenderlo). Sempre **allegato**, `application/octet-stream`, `nosniff`,
  `private, no-store`, con un tetto di 60 richieste al minuto per IP e 3000 in tutto. È escluso dalla cache delle
  letture anonime (`CacheDelleLettureAnonime`), che altrimenti riscriverebbe `no-store` in `public, max-age=60`. La
  pagina invece si tiene come le altre: al pubblico, al massimo un minuto di ritardo dopo averla spenta.

## 4. I link

Solo assoluti e `https`. Accanto al titolo si mostra il dominio (`drive.google.com`), così chi clicca sa dove va. Si
aprono in una scheda nuova, `noopener noreferrer`.

## 5. Verifica

Test: regole pure (`EventKitRulesTests`), servizio e archivio sul database vero (`ProfiliEventoTests`), hub
(`ServicesHomeTests`), cache anonima (`CacheDelleLettureAnonimeTests`). **A schermo**, su una copia del DB: da staff,
pagina con il pannello, nome, «Acceso», un link Drive e un file `.cpr` caricato. Lo scaricamento esce allegato
con le intestazioni giuste. Da utente non staff (VID 111111), la pagina pubblica col nome dell'evento, e l'hub
con la sezione «Evento in corso» sopra gli strumenti.
