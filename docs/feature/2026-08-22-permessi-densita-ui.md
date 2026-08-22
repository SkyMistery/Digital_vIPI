# Permessi (Area Staff) — densità e uso (22 agosto 2026)

> Nona pagina del ramo di modifica, prima della lista «da rifare» di
> [regole-ui-pagine-admin](../design/regole-ui-pagine-admin.md) §15.
>
> ⚠️ **Il numero della ricognizione era falso.** §15 diceva **1 346px**: misurato con la tabella dei permessi
> **vuota**, perché il DB di sviluppo non ha grant. Con **16 grant** scritti nella copia del DB la pagina
> misura **2 449px in italiano e 2 623 in inglese** — è la più alta della lista, non la più bassa. È la
> regola 69 («si misura la pagina come si usa») applicata ai **dati**: una pagina che si misura vuota mente.

## Pre-flight (FEATURE-PROCESS)

1. **Modello** — nessuna entità nuova. `EditGrant` resta l'unica sede del permesso; la **persona** è un
   raggruppamento **in pagina** (`GroupBy(UserId)`), non un modello gemello: la revoca continua a viaggiare
   per `Id` del grant. I nomi dei VID si risolvono col roster già usato da Versioni
   (`IStaffRosterRepository.GetDisplayNamesAsync`), non con una tabella nuova.
2. **Dispatch** — nessuno `switch(tipo)` nuovo. La **barra admin** nasce come **un componente solo**
   (`AdminNav`) con l'elenco delle pagine in un posto: due elenchi copiati (uno qui, uno in `StrutturaPage`)
   sono esattamente il difetto che questa pagina ha oggi.
3. **Ingressi + verifica** — ⚠️ qui c'è un **catch-22 reale**: Audit, Incarichi e Diagnostica si raggiungono
   **solo** dalle card di questa pagina. Non si tolgono le card senza mettere prima la barra che le sostituisce.
   Il «?» punta a una sezione di Guida **nuova** (`#admin-permessi`), registrata in `GuideSearchCatalog`
   (regola 12). Verifica: live a 1600/1440/1280/1024, IT ed EN, zoom 0.8→1.5, **con i 16 grant** nella copia.
4. **Propagazione** — spariscono le sei card. Le loro **descrizioni** (`Grants_StructDesc`, `Grants_DocsDesc`,
   `Grants_AuditDesc`, `Grants_TasksDesc`, `Grants_DiagDesc`) però non si buttano: diventano i `title` delle
   voci di barra — stesso testo, altro posto, stessa chiave (regola 8). Escono da entrambi i resx solo le due
   che non ha più nessuno: `Grants_ActiveGrants` e `Grants_When`, i titoli della tabella. `Home_*` resta: la
   usa anche `SopHome`.

## Cosa non va, misurato (1600×900, italiano, 16 grant)

| | Difetto | Regola |
|---|---|---|
| 1 | **È due pagine in una**: rotta `/services/vsop/admin/permissions`, titolo «Area Staff», e sopra un hub di navigazione da **485px** (6 card da 138) | 0, 1 |
| 2 | ⚠️ L'hub **non è completo** (mancano ACC, Aeroporti, Confinanti, Sorgenti, che stanno solo nella barra di Struttura) e allo stesso tempo è **l'unico** ingresso ad Audit, Incarichi e Diagnostica | 3 |
| 3 | La tabella lavora in **mezza pagina** mentre l'altra metà è il form «Concedi» sempre aperto: riga **86px** invece di ~40, nome a capo, data a capo in **tre righe** | 0, 13 |
| 4 | Sotto il form ci sono ~**1 100px di bianco**: il form è alto 373, la tabella 1 438 | 0 |
| 5 | L'elenco è ordinato per ACC: la **stessa persona compare in punti lontani** (555001 su LIBB e LIMM) mentre la domanda è «**chi** può cosa» | 24 |
| 6 | **VID non risolti**: «Concesso da 704798», e «—» nel nome anche quando il roster lo conosce | — |
| 7 | **Niente ricerca, niente conteggio, nessun filtro per ACC** | 1, 30, 41 |
| 8 | **«Revoca» non chiede niente**: toglie l'accesso a una persona, magari mentre sta editando | 98 |
| 9 | Il campo **VID mostra `0`**: il segnaposto «es. 123456» non si vede mai, e `0` non è un VID | — |
| 10 | Sottotitolo sempre a schermo, **nessun «?»**, un callout in fascia (73px) | 5, 7 |
| 11 | Label dei campi in **MAIUSCOLO** (`STAFFISTA IT`, `NOME (OPZIONALE)`) | 8 |
| 12 | `.wrap` a **1 000px**: la pagina di lavoro sta in due terzi di schermo | — |

## La forma nuova

```
.wrap.struct
 └ .doc-head.st-head    Permessi + pill «N persone» · «?» · [+ Concedi] · chip esito/errore
 └ nav.admin-nav        le UNDICI pagine admin in una riga (componente condiviso, non un elenco copiato)
 └ .perm-layout         griglia 1.35fr / 1fr, altezza MISURATA (vipiFitViewport, collapseBelow 900)
    ├ .panel.st-pane    SINISTRA — una riga per PERSONA
    │   ├ .struct-bar   ricerca (VID o nome) · chip ACC CHE CONTANO · ✕
    │   └ .st-scroll    VID · nome · chip degli ACC concessi
    └ .panel.st-pane    DESTRA — tre modi, uno alla volta:
        ├ persona scelta   i suoi ACC, ognuno con data, chi l'ha concesso e la sua ✕ (conferma in linea)
        │                  + «aggiungi ACC a questa persona»
        ├ «+ Concedi»      il form per una persona NUOVA (menu staffista, VID, nome, ACC)
        └ niente scelto    la riga d'aiuto
```

**Perché una riga per persona.** «Chi può cosa» è la domanda della pagina; oggi la risposta va ricostruita
scorrendo un elenco ordinato per ACC in cui la stessa persona compare due volte lontane. Raggruppando: 12
righe invece di 16, e i due ACC di Elena Barbieri si leggono in un colpo. ⚠️ La **revoca resta per-grant**
(`Id`): il raggruppamento è una vista, non un modello nuovo — ogni chip ACC porta la sua ✕ nel pannello.

**Perché il form non sta più sempre aperto.** Concedere un permesso è un gesto **raro**; guardare chi ce l'ha
è il gesto **frequente**. Sta al gesto raro pagare un clic («+ Concedi» in testata), non al frequente pagare
metà larghezza — è la stessa decisione di «Coppia a mano» su Confinanti. E il caso davvero comune — dare un
**secondo ACC a chi c'è già** — diventa un menu dentro il pannello della persona, senza ridigitare il VID.

**Perché la barra admin è un componente.** L'elenco delle pagine admin oggi esiste **due volte e diverso**:
sei card qui, quattro link nella barra di Struttura, e nessuno dei due elenca tutto. Un componente
(`AdminNav`) lo tiene in un posto solo; per ora lo monta questa pagina, e se regge si estende alle altre —
quello è un giro suo, non questo.

**Revoca con conferma in linea** (regola 98): non distrugge un documento, ma toglie l'accesso a una persona —
e il testo dice **chi** e **quale ACC**, perché è l'unica cosa che serve sapere prima di premere.

## Slice

1. **Carta** (questo file).
2. **`AdminNav` + testata in riga**: le sei card diventano la barra completa a undici voci; titolo, pill del
   conteggio, «?» (sezione di Guida `#admin-permessi` + `GuideSearchCatalog`), esito in chip `.st-msg`.
3. **Layout di lavoro**: `.wrap.struct` + `.perm-layout` a due pannelli misurati; elenco **per persona** con
   ricerca e chip ACC che contano; nomi risolti dal roster.
4. **Concedi e revoca**: pannello della persona con i suoi ACC (data, chi, ✕ con conferma), «aggiungi ACC»,
   «+ Concedi» per una persona nuova, campo VID vuoto invece di `0`.
5. **Verifica live** (assetti, lingue, zoom, 16 grant nella copia) e chiusura: regole, ricognizione §15,
   handoff, memoria.

## Verifica

⚠️ **Con i dati veri, non con la tabella vuota.** I 16 grant si scrivono nella copia del DB
(`INSERT INTO EditGrants (AccId, DisplayName, GrantedAtUtc, GrantedByUserId, UserId)`), con nomi lunghi
(«Alessandra Ferrari-Colombo») e persone con **due** ACC: sono quelli che mandano a capo le colonne.
Casi da guidare: concedere a una persona nuova, aggiungere un ACC a chi c'è già, revocare l'ultimo ACC di una
persona (la riga deve sparire), filtrare per ACC fino a zero righe, e la pagina vista da **non-admin**.

## Esito, misurato guidando la pagina

| | Prima | Dopo |
|---|---:|---:|
| Altezza a 1600×900 (16 grant) | 2 449 | **900** (= il viewport: la pagina non scorre) |
| …in inglese | 2 623 | **900** |
| Riga d'elenco | 86px (nome e data a capo) | **63px**, tutte uguali |
| Righe | 16 grant | **12 persone** |
| Navigazione admin | 6 card = 485px, elenco parziale | **barra da 43px**, undici voci |
| Form «Concedi» | sempre aperto, metà larghezza | tasto in testata, e «aggiungi ACC» dentro la persona |
| «?» in pagina | 0 | **1** + la sezione di Guida `#admin-permessi` |

Provato a **1600/1440/1280/1024 × IT/EN × zoom 0.8→1.5**, con i 16 grant nella copia del DB.
La pagina non scorre in verticale; lo sforo **orizzontale** a 1280/1024 resta quello della topbar (dentro il
`.wrap` non sfora niente).

⚠️ **A zoom 1.5 la pagina torna a scorrere, ed è voluto** (regola 15): lo spazio che resta sotto la testata
scende sotto il pavimento dei 320px e `vipiFitViewport` molla l'altezza fissa invece di produrre due barre di
scorrimento annidate. Su Versioni non succedeva perché lì non c'è la barra admin: sono i suoi 43px (73 a
schermo stretto) a far scattare il pavimento.

**I chip contano esatto**: cliccati uno per uno, il numero sul chip è il numero di righe mostrate
(3, 3, 2, 3, 3, 1, 1) e la pill del titolo passa a «N/12».

**Guidati i gesti veri**: scegliere una persona (l'elenco **non si muove**: seconda riga a `y=376` prima e
dopo), concedere a una persona **nuova** (si finisce sul suo pannello, l'elenco passa a 13), dare un
**secondo ACC** a chi c'è già (il menu si sposta da solo sul prossimo ACC disponibile), **revocare** con la
conferma che dice *chi* e *quale ACC*.

## Tre difetti trovati guidando

1. ⚠️ **La barra admin la vedeva anche chi admin non è**: era fuori dal ramo autorizzato, dove prima stavano
   le card. Una barra di scorciatoie mostrata a chi non può entrare in nessuna di quelle pagine è un elenco
   di porte chiuse.
2. Il tasto diceva **«+ + Concedi»**: il segno stava sia nell'icona sia nella stringa del resx.
3. ⚠️ Il menu **«aggiungi ACC» nasceva vuoto**: `@bind` a una stringa vuota non sceglie nessuna opzione — la
   casella sembra rotta e il tasto accanto sembra non fare niente. Va portato su un valore che esiste **fra le
   sue opzioni**, e ricalcolato dopo ogni concessione.

## Quello che resta aperto

- La barra `AdminNav` la monta **solo questa pagina**. Estenderla alle altre nove (e ritirare la
  `.struct-nav` di Struttura, che è il suo doppione parziale) è il giro successivo, non questo.
- ⚠️ Un permesso può esistere su un **ACC nascosto** (`Stations.Accs` non lo offre nei menu): il chip lo mostra
  e la revoca funziona, ma da qui quel permesso non si può ri-concedere. È un caso di dati, non di forma.
- Lo sforo orizzontale della **topbar** a 1280/1024: del chrome, vale su tutte le pagine.
