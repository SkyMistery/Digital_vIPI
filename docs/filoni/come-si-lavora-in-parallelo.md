# Più chat in parallelo: chi fa cosa 🟢

> Nata il 23 settembre 2026, riorganizzata il 26. Il 23 tre agenti avevano lasciato `main` rossa, rami non fusi e
> conflitti su file comuni; fino al 25 lo stato di ogni filone stava in cinque posti che si sono sfasati, il ruolo
> di una chat dipendeva dalla frase d'apertura (una chat ha fatto da integratore mentre ce n'era già uno) e si
> apriva un worktree per ogni correzione. Dal 26: tre chat fisse, tre cartelle fisse, un file di stato ciascuna.
>
> Le regole operative per le chat stanno **fuori dal repo**, nella cartella da cui si lanciano: `..\CLAUDE.md` e
> le skill `..\.claude\skills\{master,sito,lab}` (una chat si apre con `/master`, `/sito` o `/lab`). Qui resta la
> mappa, per chi legge il repo.

## Tre chat, tre cartelle

| Chat | Cartella (sotto `vIPI Ivao Italy\`) | Ramo | Scrive nel repo |
|---|---|---|---|
| Master (`/master`) | `vIPI Ivao Italy` (clone) | `main` | le fusioni, `HANDOFF.md`, §A di `docs/lavori-aperti.md`, `Directory.Build.props`, `deploy/`, i pacchetti |
| Sito (`/sito`) | `vipi-sito` | `sito/lavori` (+ `fix/<cosa>` per le urgenze) | [`sito.md`](sito.md) (voci S1, S2…) e il codice del sito |
| Sector Lab (`/lab`) | `vipi-lab` | `lab/f3` | [`lab.md`](lab.md), le carte del Lab, il codice del Lab |

I worktree restano: dopo una fusione il filone si riallinea con `git merge main`, da solo. Solo il Master porta
un ramo in `main`, e fonde lo sha che il filone ha dichiarato pronto, con la CI verde. Un'urgenza del sito la fa
il Sito: mette in pausa il suo lavoro con un commit WIP, corregge su un ramo `fix/<cosa>` nato da `main`, poi
riprende.

## File comuni del repo

| File | Chi |
|---|---|
| `tests/conteggi/<Assieme>.txt` | chi aggiunge o toglie test in quell'assieme, **nello stesso commit**: `bash tools/conta-test.sh <log> --scrivi <Assieme>` (il log deve avere TUTTI i TFM dell'assieme) |
| `docs/index.md` | nessuno a mano fra i marcatori: carta nuova → `python tools/indice-doc.py` nello stesso commit. Conflitto lì a una fusione → si rigenera |
| `HANDOFF.md`, §A di `docs/lavori-aperti.md`, `Directory.Build.props`, `deploy/` | il Master, quando fonde o consegna, riassumendo dal file del filone |

Codice in comune (`Vipi.Application`, `Vipi.Sectorfile`, `tools/`): chi lo cambia lo scrive nel file del filone e
nel messaggio del commit, perché il Master lo guardi alla fusione. `git stash` è comune a tutti i worktree: niente
stash, un commit WIP.

## Filoni chiusi

[`coordinamenti-aeroporti.md`](coordinamenti-aeroporti.md), [`lista-da-fare.md`](lista-da-fare.md) e
[`lock-uniti.md`](lock-uniti.md): fusi e online nelle 1.46.x, restano come storia. [`da-fare.md`](da-fare.md), la
coda dei lavori assegnati ai filoni, è chiusa dal 26 settembre: la coda la tiene il Master nella sua memoria, dove
un filone la legge senza aspettare una fusione.
