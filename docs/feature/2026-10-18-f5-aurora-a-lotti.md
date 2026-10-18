# F5 — Aurora a lotti: vedere in Aurora tante modifiche con una ricarica sola (18 ottobre 2026)

> **Stato: 📝 PROPOSTA** — da approvare dal committente (§6). Nessun codice prima dell'approvazione.
> Quinta fase di Aurora Sector Lab ([carta madre](2026-09-18-aurora-sector-lab.md), decisione 10 e §7). Usa l'app di
> F3 e il git di [F4](2026-10-17-f4-git-nel-lab.md). Metodo: [FEATURE-PROCESS](../FEATURE-PROCESS.md).
> 🔴 Nessun dato di vIPI si tocca, il sito non cambia. 🔴 Aurora è un programma che il committente usa: il Lab non lo
> avvia, non lo chiude, non ne scrive le impostazioni, non tocca il sector pubblicato.

## §0 — La domanda

La carta madre: Aurora ricarica il sector a mano e ci mette **fino a un minuto**, quindi la mappa del Lab è l'ossatura e
Aurora si guarda **a lotti** («7 modifiche da vedere»). F5 è finita quando **un lotto = una ricarica sola**.

Oggi, per vedere in Aurora quel che si è fatto nel Lab, l'AOD deve sapere da sé quali file ha cambiato, portarli dove
Aurora li legge, ricordarsi cosa guardare, e annotare a parte com'è andata. Le prove in Aurora di questo mese (la T3
dei simboli) sono state preparate così, a mano: cartelle, file da scambiare, un `LEGGIMI`.

## §1 — Le misure (18 ottobre, sul PC del committente, in sola lettura)

- **Aurora ha un «secondo sector»**: `Settings\profile.ini` di `%APPDATA%\IVAO\AuroraWindows`, sezione `[SECTOR]`,
  chiave `SecondSector`. Il committente: il sector pubblicato resta in `%APPDATA%\IVAO\AuroraWindows\SectorFiles`, il
  secondo è quello che si edita. Oggi `SecondSector=D:\IVAO\it-aurora-sector\SectorFiles`: un clone del repo vero su
  `master`, con due file cancellati in locale.
- 🔴 Lo stesso `profile.ini` tiene **l'accesso IVAO** del controllore (token di sessione, password cifrata). Il Lab ne
  legge **solo la riga `SecondSector`** e non lo scrive mai: dove guarda Aurora lo sceglie l'AOD, in Aurora.
- `Settings\last.ini`: l'ultimo profilo aperto (`…\Profiles\LIRR_EW_CTR.cpr`).
- La ricarica: a mano, fino a un minuto (carta madre §0). Come si chiede ad Aurora di ricaricare non è scritto da
  nessuna parte; il Lab non ci prova (non pilota Aurora).

## §2 — Quel che è già deciso

- **Decisione 10** della carta madre: la mappa nostra è l'ossatura, Aurora si guarda a lotti.
- Dalle prove di settembre-ottobre (F3, lotto «Subito»): le cose da guardare in Aurora sono **domande precise** («il
  `//discontinuity` di L613 spezza l'aerovia?», «DUMMY sparisce dalla *MVA Selection*?»), con una risposta che cambia
  il lavoro. Oggi stanno in `PROVE.md` e nella carta «file per file» (le voci «da provare in Aurora»: A8, B9, F5, I7,
  K2, O4, R4, T3 e i `SYMBOLS_` dei profili).

## §3 — La proposta

**Il Lab sa dove guarda Aurora.** Nella barra, accanto al ramo: «Aurora legge questa cartella» oppure «Aurora legge
un'altra cartella: `D:\…`» (dalla sola riga `SecondSector`). Se non è questa, il Lab dice come puntarla — si fa in
Aurora — e lavora lo stesso.

**Il lotto.** Da quando l'AOD ha detto l'ultima volta «visto in Aurora», il Lab tiene l'elenco di quel che è cambiato
sul disco del sector che Aurora legge: i gesti salvati (le stesse voci della storia che fanno il messaggio di
«Registra», F4 slice 4) e, per ognuno, **dove guardarlo in Aurora** quando il Lab lo sa (la regola o il tipo di
record: «una MVA → la *MVA Selection*», «una SID → la finestra delle procedure di LIRF»). Un'etichetta nella barra:
«7 modifiche da vedere in Aurora».

**Una ricarica, poi l'esito.** L'AOD ricarica Aurora una volta e scorre il lotto: per ogni voce ✅ «come doveva» o ❌
con due parole («l'etichetta copre il fix»). «Visto in Aurora» chiude il lotto: le ✅ spariscono, le ❌ restano come
note sul loro record (nella scheda e nel pannello dei problemi), finché l'AOD non le toglie.

**Le prove in Aurora diventano lotti preparati.** Una prova (come la T3) = un ramo `prova/<argomento>` fatto dal Lab
con le varianti da provare, e un lotto già scritto con le domande, al posto del `LEGGIMI` a mano. L'esito si scrive nel
lotto, e il ramo di prova si butta (non si manda mai).

### Le slice

| # | Slice | Finita quando (misurato) |
|---|---|---|
| 0 | Misure (§1, fatte) e il formato del lotto sul disco (dati del Lab, per cartella del sector: sopravvive a un riavvio) | — |
| 1 | **Dove guarda Aurora**: la sola riga `SecondSector`, confrontata con la cartella aperta; la barra lo dice | sul PC del committente: «un'altra cartella», poi «questa» dopo che lui l'ha puntata |
| 2 | **Il lotto**: i gesti salvati da «visto», dove guardarli, il conteggio nella barra; sopravvive al riavvio del Lab | tre salvataggi → «N modifiche da vedere», riaperto il Lab sono ancora lì |
| 3 | **Gli esiti**: ✅/❌ con una nota, «Visto in Aurora», le ❌ come note sui record | una ❌ si vede nella scheda del record e nei problemi, e se ne va solo quando l'AOD la toglie |
| 4 | **Le prove come lotti**: ramo `prova/…` con le varianti, il lotto con le domande; il ramo non si manda | la prossima prova del committente (una delle voci «da provare in Aurora») fatta così, senza `LEGGIMI` |
| 5 | **Il giro vero**: un lotto di modifiche del secondo giro R-10 guardato in Aurora con una ricarica | il committente: una ricarica, gli esiti scritti nel Lab |

**Cosa NON è in F5**: ricaricare Aurora dal Lab (non lo pilotiamo); scrivere le impostazioni di Aurora; i `SYMBOLS_`
dei profili (si decidono con una prova, che la slice 4 rende più facile).

## §4 — Rischi

- **Aurora legge un sector a metà**: se l'AOD ricarica mentre il Lab sta salvando, Aurora può leggere un file mezzo
  scritto. Il salvataggio del Lab è atomico file per file (F3), non l'insieme: il lotto dice «salvato alle …», e se
  l'AOD ricarica durante un salvataggio rifà la ricarica.
- **Il ramo cambia sotto Aurora**: passare a un altro ramo (F4) cambia i file che Aurora legge. Il lotto lo dice
  («cambiato ramo: tutto da rivedere») invece di fingere che sia una modifica piccola.

## §5 — Domande al committente

| # | Domanda | Proposta |
|---|---|---|
| D1 | **Dove legge Aurora**: (a) `SecondSector` punta **direttamente alla cartella di lavoro del Lab** (il clone): quel che salvi è quel che Aurora vede alla ricarica, nessuna copia; (b) una **vetrina**: «Porta in Aurora» copia lo stato scelto in una cartella fissa a cui Aurora punta, e intanto nel Lab si continua a lavorare. | **(a)**: più semplice, nessuna copia da tenere allineata; (b) solo se ti serve guardare una cosa mentre ne modifichi un'altra. |
| D2 | **Le ❌**: note solo nel Lab (sul PC dell'AOD), o anche un tag `//@` nel record (finisce nel sector e nella PR)? | Solo nel Lab: un esito in Aurora è un appunto di lavoro, non un dato del sector. |
| D3 | **Dove guardare**: la tabella «tipo di record → dove si vede in Aurora» la scrivo io dalle prove fatte finora e la correggi tu, o la scrivi tu? | La scrivo io come proposta, segnata «da confermare», e la correggi tu alla prima prova. |

✅ Approvata da: — (in attesa)
