# Struttura (gerarchia settori) — densità della pagina (19 agosto 2026)

Round di sola **forma** su `/services/vsop/admin/sector-structure`: nessun modello, nessuna rotta, nessuna migrazione.
È il gemello di [2026-08-19-accordi-densita-ui.md](2026-08-19-accordi-densita-ui.md): stessa diagnosi, stessa
cura, e ora le due pagine admin che si somigliano hanno anche la stessa testata.

## Il difetto

Sopra l'albero c'erano **sei fasce** prima del primo nodo: briciole, titolo+sottotitolo con i sei tasti a
destra (che andavano a capo su due righe, `max-width:560px`), barra del lock per conto suo, titolo di sezione,
paragrafo d'aiuto da tre righe, riga chip di salute, riga ricerca. Su un portatile restavano ~8 nodi visibili
su ~250, e il pannello di dettaglio — dove si lavora — spariva appena si scorreva.

Due cose in più si **muovevano sotto il puntatore**: il chip «⚠ N da agganciare» esisteva solo se c'erano
orfani (all'ultimo agganciato la riga si accorciava da sé), e con lui appariva/spariva il «↺ mostra tutti».

## Cosa cambia

1. **Testata in una riga**: `titolo · «?» · sei tasti · —— · lock`. Classe `.st-head`, gemella di `.xt-head`
   dei Trasferimenti; la `.lockbar` si spinge a destra da sé (`margin-left:auto`) e i suoi margini di fascia
   si azzerano **nel CSS della testata, non nel componente** — struttura nuova pagina e nuovo documento la
   usano ancora come fascia a sé.
2. **Il sottotitolo diventa il «?»**. `Struct_Subtitle` («Gerarchia di copertura globale + documenti») rimossa
   da **entrambi** i resx; il suo contenuto, più cosa si fa qui, sta in `Struct_HelpBody`.
3. **Il paragrafo d'aiuto diventa il «?» accanto a «Gerarchia di copertura»**. La stringa
   `Struct_HierarchyHelp` è riusata **identica** (contiene già `<b>` e il link alla pagina ACC): cambia solo
   dove si legge. Il popover di serie è largo 270px e per questo testo è stretto → nuova `ExtraClass="wide"`
   (360px), riusabile da chiunque abbia un aiuto lungo.
   ⚠️ I «?» si aprono **solo al clic** (`<details>/<summary>`, nessun hover): regola generale del progetto.
4. **Una barra sola** al posto di due: `ricerca · nazione · N risultati · espandi/comprimi · chip di stato`,
   letta da sinistra a destra come si lavora (cosa cerco → come guardo → come sto).
5. **I chip non saltano**. Il chip degli orfani c'è **sempre**: a zero è spento e dice «✓ tutto agganciato»
   (la frase lunga di prima è passata nel `title`), sopra zero è giallo e filtra. Il «↺ mostra tutti» sparisce:
   il chip è già un interruttore, e due comandi per lo stesso stato sono uno di troppo — `Struct_ShowAll` è
   rimossa da entrambi i resx.
6. **Altezza misurata** (era il difetto grosso): `.gerarchia-2col` è alta *schermo meno ciò che le sta sopra*,
   e la misura la fa `vipiFitViewport('.gerarchia-2col', 900)` a ogni render. Dentro ogni pannello scorre
   **solo il corpo** (`.st-scroll`): l'intestazione, la barra dei filtri e i tasti del dettaglio restano fermi.
   Sotto i 900px l'altezza fissa **sparisce** (una colonna sola, pagina che scorre): un riquadro alto quanto lo
   schermo dentro una pagina che scorre significa due barre annidate.
   Conseguenza sui nomi: `.detail-sticky` non è più agganciata — la classe e il suo CSS spariscono, il
   pannello è `.st-pane` come l'altro. Un nome che descrive un meccanismo sparito mente a chi legge.
7. **Guida**: i due «?» puntavano a `#admin`, che della struttura dice una riga. Nasce la sezione
   `#struttura` (`GuidaPage`) + la sua voce in `GuideSearchCatalog`, così la ricerca globale la trova.
8. **Tre cose decise dalla MISURA, non dalla carta** (la prima stesura della testata andava a capo lo stesso):
   - **Etichette dei sei tasti accorciate** — «Gestione aeroporti» → «Aeroporti», «ACC confinanti (vLOA)» →
     «Confinanti», e così via: 825px di navigazione contro 595. Sono nomi brevi, **non troncamenti**: un tasto
     con l'etichetta tagliata è un altro tasto. L'icona resta e la destinazione è nel link.
   - **«Sola lettura» e basta** (`Lock_ReadOnly`, componente condiviso). La frase lunga — «Prendi il lock per
     modificare (una persona alla volta)» — è parola per parola ciò che il «?» accanto dice al clic, e costava
     **647→289px** di barra: da sola mandava a capo la testata sotto i 1500px. Ne guadagna anche la testata
     dei Trasferimenti, che monta la stessa barra.
   - **I chip in un gruppo** (`.sb-chips`). Sciolti nella barra andava a capo **l'ultimo da solo**, e quale
     fosse cambiava con la larghezza del pannello: la barra sembrava rotta, non stretta. Raggruppati, la riga
     si spezza in un punto solo — sopra ricerca/nazione/espandi-comprimi, sotto i tre chip.

9. **L'esito in testata** (aggiunto insieme alla pagina ACC): l'avviso d'errore era una fascia sotto la
   testata e spingeva in giù l'albero mentre ci si lavorava. Ora è un chip `.st-msg` fra i comandi e il lock,
   con la ✕ per chiuderlo.

## Trappole attese (dai round gemelli)

- **Specificità**: le regole nuove vanno scritte con `.struct` davanti, e verificate sul valore **calcolato**;
  contro `.res-table`/`.inline-form` una regola da due classi perde in silenzio.
- **Il JS misura, il CSS stima**: il `calc()` su `.gerarchia-2col` è solo il valore di partenza prima che il
  JS misuri, non la verità. Va rimisurato a **ogni** render: l'avviso del lock compare e sparisce da solo.
- **Sei tasti + titolo + lock in riga**: sotto ~1280px vanno a capo (wrap). Nessun testo tagliato — un tasto
  con l'etichetta troncata è un altro tasto.

## Verifica

`dotnet build Vipi.slnx -c Release --no-incremental`: **0 avvisi, 0 errori** su entrambi i TFM.
`dotnet test`: **2570 verdi**, nessun rosso (il primo giro aveva una `AuroraClientTests` rossa per timeout di
socket: ripetuta, verde — è flaky, non c'entra con questo round).

Guidata con Edge+puppeteer su **copia** del DB (skill `verifica-live`, porta 5035 perché la 5034 era occupata
dall'app dello sviluppo), in italiano e in inglese:

| Larghezza | Testata | Barra filtri | Altezza griglia (scritta dal JS) | La pagina scorre? |
|---|---|---|---|---|
| 1600 | **una riga** (51px) | 2 righe (77px) | `717px` | no |
| 1440 | una riga | 2 righe | `617px` | no |
| 1280 | una riga | 2 righe | `617px` | no |
| 1152 | una riga | 2 righe | `617px` | no |
| 1024 | due righe (99px) | 3 righe | `569px` | no |
| 860 | due righe | — | **nessuna** (inline vuota) | sì, come deve |

Altro misurato: il pannello dell'albero scorre **dentro di sé** (`scrollHeight 10995` in un riquadro da 563) e
il dettaglio con il tasto **Applica** finisce a `982` su un viewport da `1000` — prima bisognava scorrere.
I due «?» restano chiusi al passaggio del mouse e si aprono al clic (popover 360px); quello di sezione tiene il
link alla pagina ACC. Il chip a zero è verde e spento (verificato forzandone lo stato nel DOM: nel DB di
sviluppo ci sono 92 nodi da agganciare). La sezione `#struttura` della Guida esiste, è in indice e si apre.
Nessun errore di console, nessuna risposta HTTP ≥400, nessun letterale Razor non valutato.

Screenshot in `scratchpad/live/struct-*.png`.

## Resta da fare

- La colonna **Dettaglio** ha ancora testi italiani cablati (`Catena di fallback`, `Copre (dominio)`) che non
  passano dai resx: si vedono in inglese com'erano. È un difetto **precedente** a questo round, non toccato.
