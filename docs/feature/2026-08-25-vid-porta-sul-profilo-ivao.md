# Feature — Il VID è una porta sul profilo IVAO

Data: 2026-08-25 · Stato: **FATTO sul ramo `statistiche-atc`** (commit `03463bf`, spinto su origin) ·
Cancello: `dotnet build Vipi.slnx -c Release --no-incremental` **0 avvisi**, `Vipi.Ui.Tests` **412 verdi**
su net8 e net10 (5 nuovi).
⚠️ **Non ancora visto dal vivo** — vedi §8.

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
Tre posti dove il VID compare e **non** è premibile. In tutt'e tre è il ripiego che scatta solo quando il
roster non conosce il nome, quindi si vedono di rado:

| dove | perché no |
|---|---|
| chip «avanzamento per editore», Incarichi | un `<a>` dentro un `<button>` non è HTML valido |
| tendine di assegnazione (Incarichi, Permessi) | un `<option>` può contenere **solo testo** |
| «Deciso da …» di Sorgenti | `Sorg_DecidedBy` è una frase già formattata: il VID è un `{0}` dentro un `string.Format`, e per renderlo premibile andrebbe spezzata la chiave come si è fatto per `Stats_Subtitle` |

I primi due sono limiti del formato, non scelte: lì il gesto non c'è e basta. Il terzo si può chiudere
ricalcando §5, se qualcuno lo vuole.

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

## 8. Cosa resta da fare
🟢 **La verifica live.** Non è stata fatta: `Vipi.Host` era acceso durante il lavoro e teneva bloccati i
`.dll` in `bin/Debug`, quindi le pagine servite erano quelle della build vecchia. Da guardare, in ordine di
rischio:

1. **Permessi** — che cliccare il VID **non** sposti la selezione della riga (§3b). È l'unico punto in cui
   la fermata della risalita si può vedere fallire.
2. **Classifica di divisione e Cambiati** — sono SSR statiche: il link deve funzionare senza circuito.
3. **La punteggiatura nei due temi** (§4): è l'unica cosa che nessun test guarda.
4. **La stampa** di una pagina che porta VID, per la riga di `vipi-print.css`.

⚠️ **La Guida in-app non nomina il gesto.** `GuidaPage` parla di VID nella sezione Permessi ma non dice che
il numero si può premere. Da aggiungere quando si tocca la Guida per il servizio statistiche — §12 della
carta delle statistiche ha già quella voce aperta.

## 9. File toccati
- **nuovo** `src/Vipi.Ui/Components/VidLink.razor`, **nuovo** `tests/Vipi.Ui.Tests/VidLinkTests.cs`
- `Components/ReleasePanel.razor`, `Components/MediaCleanupCard.razor`
- `Pages/AdminGrantsPage.razor`, `Pages/AdminTasksPage.razor`, `Pages/AuditPage.razor`,
  `Pages/ChangedPage.razor`, `Pages/DiagnosticaPage.razor`, `Pages/StatsDivisionPage.razor`,
  `Pages/StatsHome.razor`, `Pages/VersioniPage.razor`
- `Resources/SharedResource{,.en}.resx` (+`Vid_ProfileTitle`, −`Stats_Subtitle`)
- `wwwroot/vipi-theme.css`, `wwwroot/vipi-print.css`
