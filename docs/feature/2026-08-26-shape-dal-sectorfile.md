# Le shape dal sectorfile, e il ciclo che non è ancora uscito

> 26 agosto 2026 · ramo `identita-settori` · **carta, prima del codice**
>
> L'anagrafica IVAO ha smesso di dare i poligoni. Il sectorfile Aurora ce li ha tutti — ma è il file del
> ciclo **prossimo**, perché si edita in anticipo. Serve prenderli senza pubblicare il futuro.

## 1. Da dove vengono le shape, in ordine

Deciso col committente il 26 agosto. L'ordine è la parte che va scritta **una volta sola**, perché adesso
le sorgenti sono quattro e ognuna ha una natura diversa.

| # | Sorgente | Copre | Gate AIRAC | Perché |
|---|---|---|---|---|
| 1 | **Anagrafica IVAO** (`regionMapPolygon`) | tutto | no | È la verità primaria. ⚠️ **Oggi torna vuota su tutta l'API** (misurato: 229 risorse, più le forme `/all`) |
| 2 | **Sectorfile Aurora** (`DYNAMIC_SEC/*.tfl`) | CTR · APP · MIL · FSS | **sì** | **Ripiego**, e solo quando la 1 torna vuota. È il file che scriviamo noi, in anticipo sul ciclo |
| 3 | **Cerchio sintetico** | solo TWR | no | Ultimo ripiego, già in casa |

ⓘ Esiste una quarta sorgente che **non** usiamo: il tracker (`/v2/tracker/now/atc/summary`, pubblico) porta
i poligoni pieni annidati in `subcenter`/`atcPosition`, ma solo per chi è connesso in quel momento. Se un
giorno la si aggancia, entra **senza gate**: è quel che IVAO serve ai controllori adesso, quindi è in vigore
per definizione. Vedi `2026-08-26-lassenza-non-cancella.md` §5.

⚠️ **Il sectorfile è un ripiego, non una sorgente.** Se l'anagrafica ricomincia a rispondere, torna a
comandare lei — senza che si tocchi niente.

## 2. Il file: cosa c'è e cosa costa

`DYNAMIC_SEC/` sul repo `ivao-italy/it-aurora-sector` (lo stesso da cui già peschiamo `twrs.tfl`):
**112 blocchi** su 26 file, formato **identico** a quello che il parser già legge.

Due ostacoli, misurati sui file veri:

**(a) Un'intestazione può portare più callsign.**

```
LIBB_ES_CTR LIBB_EU_CTR;CTR;1;CTR;1;
EDMM_CTR EDMM_S_CTR EDMM_FSS EDMM_MIL_CTR;CTR;1;CTR;1;
```

Una shape serve **più enti**. Oggi `ParseTowerShapes` prende `fields[0]` intero e ne fa una chiave sola, che
non combacia con niente.

**(b) 233 righe sono nomi di punto invece di coordinate.**

```
N044.23.16.000;E011.07.44.000;
TUFTE;TUFTE;              ← un punto del catalogo, non un vertice
```

Concentrate in 6 file. Oggi il parser le scambia per un'intestazione e **spezza l'anello in frammenti**, in
silenzio.

**Si risolvono col catalogo navaid**, che però oggi tiene i **nomi senza coordinate** — e non è una
dimenticanza, è scritto in `INavaidSource`: «*Il giorno che servisse la posizione … questo record cresce di
due campi*». È oggi.

Misura della copertura: dei **79** punti distinti citati dai blocchi di settore, **76 si risolvono** —
75 da `itfix.fix`, più `PAN` che è il VOR di Pantelleria. I **3** che restano (`GEMLA`, `GIGUS`, `GODRA`)
sono punti **esteri**, dentro i blocchi di LDZO/LGGG/LFMM.

⚠️ **Un punto che non si risolve invalida l'anello intero, non lo accorcia.** Un vertice mancante in mezzo
non dà un poligono più piccolo: dà un poligono **sbagliato**, che si disegna benissimo e mente. Il blocco si
scarta e si dice quale punto manca.

## 3. Il ciclo AIRAC: il problema vero

Il sectorfile lo scriviamo noi **prima** che il ciclo esca. Quindi in qualsiasi momento può contenere
geometrie che entreranno in vigore fra settimane.

### Quel che già c'è

La sezione `aor` è **`Frozen`** su tutte e 26 le occorrenze reali, e `AccFrozenSectionProvider` non congela
un riferimento: **deriva la vista AoR e serializza il view-model intero** nel payload di release. La pagina
pubblica legge quello.

Quindi metà del meccanismo esiste: una shape che cambia in catalogo **non si vede in pubblico** finché
qualcuno non ripubblica.

### Dov'è il buco

Non nella lettura: nella **pubblicazione**. Il congelamento fotografa quel che trova in catalogo in
quell'istante — e se il `.tfl` ha già la geometria del ciclo prossimo, la mette in vigore in anticipo.

### La forma della domanda

`DocRelease` porta già `ReleaseAiracCycle` e `ReleaseEffectiveUtc`. Quindi la domanda giusta non è «qual è
la shape di oggi» ma:

> **«qual è la shape in vigore al ciclo di *questa* release?»**

Formulata così regge in tutti e due i versi: pubblico per il ciclo corrente → prende quella vecchia;
pubblico **in anticipo per il ciclo prossimo** — che è precisamente quel che si fa preparando un AIRAC →
prende quella nuova. Senza interruttori da ricordare.

### ⚠️ La cosa che si sposta

Il committente ha deciso (26 agosto): **l'editor mostra sempre ciò che è nel DB**, perché un aggiornamento
può essere la correzione di un errore da pubblicare subito; il **pubblico** mostra l'ultima rilasciata.

Ne segue che il catalogo tiene **la più recente** — e allora, al momento di congelare, quella *in vigore*
non ce l'ha più nessuno. Va tenuta da parte: una seconda geometria sulla riga, più il ciclo da cui la nuova
subentra. Non è in conflitto con la decisione, perché **l'editor continua a leggere la colonna di sempre**:
la seconda la guarda solo il congelamento.

L'alternativa — ripescare la geometria dalla release precedente — evita la colonna ma lega il congelamento
alla forma interna di uno snapshot vecchio, e non ha risposta quando release precedenti non ce ne sono.

### Il gate, per intero

- `RegionMapPolygon` — **la più recente**. La legge l'editor, la derivazione, tutto quel che legge oggi.
  Nulla cambia per chi già la usa.
- `RegionMapPolygonInForce` — quella in vigore. La guarda **solo** il congelamento di release.
- `ShapeAiracCycle` — il ciclo dal quale `RegionMapPolygon` entra in vigore.
- `ShapeSource` — da dove viene (anagrafica · sectorfile · sintetica). **Il gate vale solo per il
  sectorfile**: è l'unica sorgente che corre avanti.

**Quando si vede una geometria nuova** (primo avvistamento, come per le SID): si scrive in
`RegionMapPolygon`, si stampa `ShapeAiracCycle` = ciclo **successivo** a quello corrente, e
`RegionMapPolygonInForce` resta la vecchia. L'import promuove da sé quando il ciclo gira: nessun lavoro
schedulato, nessuna magia sull'orologio — ogni giro chiede «la pendente è entrata in vigore? allora
promuovila».

⚠️ **La prima shape non si differisce mai.** Se un settore non ne ha mai avuta, differire vuol dire mostrare
**nessuna area** fino a 28 giorni, che è peggio di una in anticipo. Primo riempimento → in vigore subito.

⚠️ **E il differimento non deve poter bloccare una correzione.** È il caso che il committente ha nominato
per primo. Al momento di pubblicare, chi pubblica **vede** che l'area è cambiata e non è ancora in vigore, e
può pubblicarla lo stesso — il gemello di `AirportSid.ForcePublished`. Il congelamento sostituisce di
default e dice cosa sta facendo; non decide da solo e in silenzio.

## 4. Quando il sectorfile toglie un blocco

Deciso: **non si cancella niente, si apre una segnalazione.** Cancellare per assenza è esattamente l'errore
appena corretto sull'API (`2026-08-26-lassenza-non-cancella.md`), e un blocco può sparire dal file per un
refuso quanto per una decisione.

## 5. Pre-flight (FEATURE-PROCESS)

1. **Modello** — nessun modello gemello: le shape restano dove sono, sulle righe di catalogo. Il catalogo
   navaid **cresce di due campi** invece di nascere una seconda volta accanto (è la strada che il suo stesso
   commento indica).
2. **Dispatch** — il ripiego per-tipo esiste già (`GithubTowerShapeService` per le TWR): questo è il suo
   gemello per gli altri enti, e la scelta della sorgente va scritta **in un posto solo**, non in due
   servizi che si somigliano.
3. **Ingressi + verifica** — nessun ingresso nuovo: le shape entrano dal giro. Ma il congelamento che
   sostituisce **deve dirlo** a chi pubblica. Verifica: sui file veri, contro i poligoni del backup 25-ago,
   che sono la stessa geometria vista da un'altra strada.
4. **Propagazione** — `NavaidName` cresce: chi lo costruisce va aggiornato nello stesso giro.

## 6. L'ordine dei lavori

1. **Il parser regge i file di settore** — intestazioni multi-callsign, punti per nome, anello scartato se un
   punto non si risolve. Provato sui 112 blocchi veri.
2. **La sorgente sectorfile** — provider e servizio di ripiego, che scrive solo dove la 1 non ha dato niente.
3. **Il gate AIRAC** — le colonne, la promozione al giro, la sostituzione al congelamento con l'avviso.

I primi due si reggono da soli e valgono anche senza il terzo: senza gate le shape entrano e basta, che è
comunque meglio di adesso. Il terzo è quello che tocca la pubblicazione.
