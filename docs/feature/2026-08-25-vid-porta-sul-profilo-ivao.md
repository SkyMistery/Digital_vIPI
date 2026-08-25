# Feature — Il VID è una porta sul profilo IVAO

Data: 2026-08-25 · Stato: ✅ **FATTO e VERIFICATO DAL VIVO** sul ramo `statistiche-atc` ·
Cancello: `dotnet build Vipi.slnx -c Release --no-incremental` **0 avvisi**, suite completa verde
(**2254 net8 / 2016 net10**), `Vipi.Ui.Tests` **423** di cui **16 nuovi**.
⚠️ La verifica live (§8) ha trovato **un buco vero**: nove VID muti sul Registro. Chiuso con `VidText`
(§6-bis), e la prova è di nuovo a schermo.

## 1. La richiesta
Del committente, testuale: «se clicchi sul VID, in qualsiasi pagina, ti rimanda al profilo dell'utente»,
con l'indirizzo `https://ivao.aero/Member.aspx?Id=<VID>`.

Fino a ieri un VID a schermo era un numero e basta: per sapere chi fosse si apriva ivao.aero e lo si
incollava a mano nella ricerca membri. Il numero c'era già in dieci pagine — non mancava un dato, mancava
il gesto.

## 2. Il disegno — un componente, tre forme
`src/Vipi.Ui/Components/VidLink.razor`. L'indirizzo del profilo sta lì e **solo** lì: quindici punti lo
usano senza conoscerlo.

⚠️ È **`ivao.aero`**, non `api.ivao.aero`: quello è l'IdP con cui si fa il login (`IvaoOptions.BaseUrl`),
questo è il sito che si apre a un essere umano. I due si assomigliano abbastanza da sbagliarsi, ed è metà
del motivo per cui l'indirizzo non va ricopiato a mano in dodici punti.

Il VID compare a schermo in tre forme, e le sceglie un parametro invece di tre componenti:

| forma | come si chiede | dove |
|---|---|---|
| `704798` nudo | `SoloNumero="true"` | colonne che hanno già «VID» scritto in intestazione |
| `VID 704798` | il default | dentro una frase o una riga di meta |
| `Mario Rossi (VID 704798)` | `Nome="…"` | Versioni e Permessi, dove il nome c'è |

I quindici punti, in dieci file:

| pagina / componente | rotta | punti |
|---|---|---|
| `AdminGrantsPage` | `/services/vsop/admin/permissions` | riga dell'elenco, testata del pannello, «concesso da» |
| `AuditPage` | `/services/vsop/admin/audit` | colonna «chi» (solo il ripiego, §3a) |
| `VersioniPage` | `/services/vsop/{acc}/versions` | autore della bozza, riga di storia, riga di release |
| `ReleasePanel` | componente (i tre editor + Versioni) | «chi ha pubblicato» (solo il ripiego) |
| `AdminTasksPage` | `/services/vsop/admin/tasks` | testata del pannello (solo il ripiego) |
| `ChangedPage` | `/services/vsop/changed` | «pubblicato da» |
| `DiagnosticaPage` | `/services/vsop/admin/diagnostics` | tabella degli admin, colonna VID |
| `MediaCleanupCard` | dentro Diagnostica | colonna «caricato da» |
| `StatsHome` | `/services/stats` | sottotitolo: il VID di chi guarda |
| `StatsDivisionPage` | `/services/stats/division` | podio e colonna VID della classifica |

## 3. Le quattro decisioni che non si vedono dal risultato

**(a) Dove a video c'è il NOME, resta il nome.** In Registro, `ReleasePanel` e Incarichi il VID non si vede
affatto quando il roster il nome ce l'ha: lì il link compare **solo** sul ripiego «VID 123456». La
tentazione era mostrare sempre «Nome (VID …)» per uniformità — ma la colonna `.c-who` del Registro è larga
quanto un nome, e appenderci «(VID 123456)» su cinquecento righe la fa tagliare. La regola che ne esce, e
vale per il prossimo punto che ci si aggancia: **il link sta dove il VID si vede già**, non dove lo si
potrebbe ricavare.

⚠️ Conseguenza pratica: `Chi(...)` in quelle tre pagine è rimasto una **stringa** e gli si è affiancato un
`ChiMarkup(...)` che è un `RenderFragment`. Non è duplicazione da potare: la stringa serve dove il markup
non entra — la ricerca del Registro cerca su ciò che la pagina **mostra**, e `Dettaglio` di `ReleasePanel`
compone il `title` della riga, che è un attributo.

**(b) `@onclick:stopPropagation`.** In Permessi il VID sta **dentro** una riga che è essa stessa un comando
(`role="button"`, `@onclick="() => Pick(...)"`: sceglie la persona nel pannello di destra). Senza fermare la
risalita, aprire un profilo avrebbe anche spostato la selezione della pagina alle spalle di chi ha
cliccato. La fermata sta nel componente e non alla chiamata, perché la chiamata non può raggiungere l'`<a>`
che il componente rende.

**(c) È un `<a>`, senza render mode.** Metà di quei punti sono resi in **SSR statico** — classifica di
divisione, Cambiati, sottotitolo delle statistiche: `<Routes />` non ha render mode e quelle pagine non
hanno un circuito. Un `@onclick` lì non avrebbe fatto assolutamente niente, in silenzio. Un link funziona
in tutt'e due i mondi.

**(d) `target="_blank"` + `rel="noopener"`.** Il profilo sta su un altro sito. Portarci via chi stava
editando una vIPI — lock aperto, bozza non salvata — sarebbe un modo di far perdere lavoro. È la stessa
scelta già presa per tutte le altre uscite dal modulo (anteprima della release, Guida).

## 4. Il colore: punteggiato, non blu
`vipi-theme.css`, accanto alla regola base dei link — che è ciò contro cui questa deve difendersi.

Il VID non sta mai da solo: sta dentro la `.dr-sub` grigia dei Permessi, dentro la `.vmeta` delle Versioni,
dentro una colonna che ne mostra quaranta di fila. Con `a{color:var(--brand-ink-2)}` ogni riga di quegli
elenchi avrebbe acceso un numero blu — e a quel punto il colore non segnala più niente: segnala che lì c'è
un VID, cosa che si vedeva già.

Quindi `color:inherit` e **sottolineatura punteggiata**, che si nota da vicino e sparisce da lontano; il
colore arriva al passaggio e col fuoco, dove il gesto è già cominciato. `text-decoration-color` è un
`color-mix` su `currentColor` e non un token, perché lo stesso link vive in contesti di colore diverso e in
due temi: legato a `currentColor` resta leggibile ovunque senza una riga per contesto.

⚠️ **In stampa `.vid-link` va nominato a parte.** `vipi-print.css` appiattiva già i link con `.vipi-root a`
— una classe e un tag — ma `.vipi-root .vid-link` sono **due classi** e vince: senza la riga in più il VID
si sarebbe portato dietro la sua punteggiatura, cioè un invito a premere, stampato.

## 5. Una chiave di risorsa cade
`Stats_Subtitle` («{0} · VID {1}», identica nelle due lingue) è stata rimossa: era una frase già formattata
e non lasciava spezzare il `{1}` in markup. Il sottotitolo ora è `@_utente.Name · <VidLink … />` — il «·» è
punteggiatura, non testo da tradurre, e la parola «VID» resta tradotta **una volta sola**, in `Audit_VidN`,
che è la chiave che il componente usa per tutte le sue etichette.

Nuova chiave: `Vid_ProfileTitle` («Apri il profilo IVAO — VID {0}» / «Open the IVAO profile — VID {0}»),
il `title` del link.

## 6. Cosa resta fuori, e perché
Due posti, e sono limiti del **formato**, non scelte. In tutt'e due il VID compare solo come ripiego, cioè
quando il roster non conosce il nome:

| dove | perché no |
|---|---|
| chip «avanzamento per editore», Incarichi | un `<a>` dentro un `<button>` non è HTML valido |
| tendine di assegnazione (Incarichi, Permessi) | un `<option>` può contenere **solo testo** |

⚠️ **Un terzo caso c'era, e non era un limite: era un buco.** Il VID scritto *dentro una frase* — «Deciso
da …» di Sorgenti, «Assegnato da …» di Incarichi, e soprattutto le frasi del narratore del Registro. Stava
in questa tabella come «si può chiudere se qualcuno lo vuole»; la verifica live ha mostrato che erano
**nove** VID muti sulla sola pagina del Registro, cioè il posto dove i VID si vedono di più dopo la
classifica. Chiuso: §6-bis.

## 6-bis. `VidText` — quando il VID è una parola, non un campo
`src/Vipi.Ui/Components/VidText.razor`.

`VidLink` risolve il caso in cui il VID è un **campo**: sta da solo in una colonna, o accanto a un nome, e
il markup lo può avvolgere. In tre punti però il VID è una **parola dentro una frase**, e quelle frasi
nascono da template tradotti (`Audit_Fr_*`, `Sorg_DecidedBy`, `AdminTasks_CreatedBy`): spezzarle in markup
vuol dire spezzare la traduzione in pezzi che nessun traduttore può rimettere insieme.

`VidText` prende la frase **già composta** e la taglia sulla forma che scriviamo noi — `Audit_VidN`, cioè
«VID 1234567» — emettendo i pezzi in mezzo come **testo**.

⚠️ **Niente `MarkupString`.** Quelle frasi portano dentro titoli di documento e note scritte da persone:
passarle per markup vorrebbe dire fidarsi di quel contenuto. C'è una prova apposta
(`Il_testo_intorno_non_diventa_markup`) perché è la scorciatoia che qualcuno prenderà, un giorno, per
comodità.

⚠️ **La forma tagliata dipende da una risorsa tradotta**, ed è il modo in cui questa feature potrebbe morire
in silenzio: se un domani `Audit_VidN` diventasse «ID IVAO {0}», `VidText` non troverebbe più niente e
nessuno se ne accorgerebbe fino alla prossima verifica live. Perciò `VidTextTests` legge i due `.resx`
**dal disco** e fa fallire la suite se il formato non è più tagliabile.

I quattro punti agganciati: Registro (frase del narratore), Versioni (la stessa frase nella riga di storia),
Sorgenti («Deciso da …»), Incarichi («Assegnato da …»).

## 7. Test
`tests/Vipi.Ui.Tests/VidLinkTests.cs`, 5 casi:

1. l'indirizzo è `ivao.aero/Member.aspx?Id=<VID>` e il testo è «VID 704798»;
2. scheda nuova e `rel="noopener"`;
3. `SoloNumero` toglie l'etichetta e **non** il link;
4. col nome esce «Mario Rossi (VID 704798)» e a essere link è **solo** il VID — la parentesi resta fuori;
5. **VID 0 non è una porta**: le righe scritte dal sistema (import, migrazione, seed) hanno `UserId = 0`,
   il testo resta ma il link non c'è, perché dall'altra parte non c'è nessun profilo.

⚠️ Il localizer delle prove **formatta** invece di restituire la chiave: lo stratagemma solito di
`InlineConfirmTests` avrebbe fatto uscire `Audit_VidN` come testo del link, cioè proprio la cosa da
guardare.

## 8. La verifica live — eseguita, e cos'ha detto
Edge + puppeteer-core su una copia del `vipi.db` (skill `verifica-live`), nove pagine guidate.

| cosa | esito |
|---|---|
| **la risalita del clic** in Permessi | ✅ il clic arriva all'ancora (`clic: 1`) e la selezione **non** si muove: `aria-pressed` a 0 prima e dopo, pannello invariato. ⚠️ Con **controprova**: cliccando la riga lontano dal VID la selezione cambia («Carmine Granato · 1 ACCs») — senza, «non è successo niente» avrebbe potuto voler dire «il clic non è arrivato» |
| **SSR statico** (classifica, Cambiati) | ✅ 53 link nella classifica, 1 in Cambiati, `href` giusti, senza circuito |
| **i due temi** | ✅ il `color` del link è identico a quello della cella (`rgb(33,33,46)` chiaro, `rgb(250,250,255)` scuro): `color:inherit` fa il suo lavoro. Punteggiata al 45% in tutt'e due |
| **col mouse sopra** | ✅ il colore arriva (`rgb(60,85,172)`) e la riga passa a `solid` |
| **stampa** | ✅ `text-decoration-line: none` sotto `emulateMediaType('print')` |
| **il censimento** | ✅ Permessi 1/1, Cambiati 1/1, Statistiche 1/1, Diagnostica 1, classifica 53, Versioni 7/7 |
| **Registro** | ❌ **9 VID a schermo, 0 link.** Vedi sotto |

**Il buco, e perché nessun test lo vedeva.** La colonna «cosa» del Registro porta le frasi del narratore —
«Granted VID 704798 permission on LIRR» — e lì il VID non è un campo ma una parola. Nessuna prova
sbagliava: nessuna guardava quella colonna, perché quella colonna non era stata toccata. **Solo lo schermo
poteva dirlo**, e l'ha detto contando: nove su nove. Chiuso con `VidText` (§6-bis) e riverificato: **9 su 9
premibili**.

⚠️ **Le due frasi di Sorgenti e Incarichi non si vedevano su questi dati** — zero incarichi in archivio e
nessuna policy mai decisa. Sono state **seminate** nella copia (un incarico e una riga `ImportPolicies`, con
un VID che il roster **non** conosce apposta, per far scattare il ripiego), e allora si sono viste:
«Assigned by VID 123456 on 20 Aug 2026» e «Decided by VID 123456 on 20 Aug 2026», tutt'e due premibili.

### Due cose viste e **non** toccate

1. **«Carmine (704798)» nella colonna «chi» del Registro non è un link, e va bene così.** Quel numero è
   dentro un *nome*: è il `publicNickname` di IVAO, che nel payload reale vale letteralmente
   «Carmine (704798)» (vedi il commento di `EfStaffRosterRepository.UpdateVerifiedAsync`). Linkarlo vorrebbe
   dire cercare numeri dentro i nomi delle persone — una regola che sbaglierebbe il giorno che un nickname
   contiene una cifra qualunque. In produzione, dove il nome vero arriva dal login (`firstName lastName`),
   il caso sparisce da sé.
2. **«VID 0» esiste davvero nei dati**, e non è un link: la prima versione di un documento generato dal
   profilo aeroporto ha `CreatedByUserId = 0`. È il caso che `VidLink` tratta come «nessuna persona», ed è
   la conferma sul campo che quel ramo serviva. ℹ️ A schermo resta scritto «VID 0», che è com'era prima di
   questo giro: dirlo meglio («generato dal sistema») è un'altra decisione, e non è di questo giro.

## 8-bis. Cosa resta
🟢 **La Guida in-app non nomina il gesto.** `GuidaPage` parla di VID nella sezione Permessi ma non dice che
il numero si può premere. Da aggiungere quando si tocca la Guida per il servizio statistiche — §12 della
carta delle statistiche ha già quella voce aperta, così la Guida si tocca una volta sola.

## 9. File toccati
- **nuovi** `src/Vipi.Ui/Components/VidLink.razor` e `Components/VidText.razor`, **nuovi**
  `tests/Vipi.Ui.Tests/VidLinkTests.cs` e `VidTextTests.cs`
- `Components/ReleasePanel.razor`, `Components/MediaCleanupCard.razor`
- `Pages/AdminGrantsPage.razor`, `Pages/AdminTasksPage.razor`, `Pages/AuditPage.razor`,
  `Pages/ChangedPage.razor`, `Pages/DiagnosticaPage.razor`, `Pages/StatsDivisionPage.razor`,
  `Pages/StatsHome.razor`, `Pages/VersioniPage.razor`, `Pages/SorgentiAdminPage.razor`
- `Resources/SharedResource{,.en}.resx` (+`Vid_ProfileTitle`, −`Stats_Subtitle`)
- `wwwroot/vipi-theme.css`, `wwwroot/vipi-print.css`
