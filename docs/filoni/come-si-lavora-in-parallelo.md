# Più agenti in parallelo: chi scrive dove 🟢

> Nata il 23 settembre 2026. Quel giorno tre agenti (Sector Lab, sito, vista condivisa per l'hub) hanno lavorato
> insieme: il codice era a posto, ma `main` era rossa per un commit di sola carta, due rami erano non fusi, e ogni
> fusione si scontrava su righe vicine di file comuni (conteggi dei test, `HANDOFF.md`, `docs/index.md`). Qui c'è
> chi scrive dove, perché i conflitti nascano solo quando due lavori toccano davvero la stessa cosa.

## Un agente = un worktree = un ramo

| Filone | Cartella (sotto `vIPI Ivao Italy\`) | Ramo | Il suo file |
|---|---|---|---|
| Integratore | `vIPI Ivao Italy` (clone principale) | `main` | `HANDOFF.md`, `docs/lavori-aperti.md` |
| Sector Lab | `vipi-lab` | `lab/f3` | [`lab.md`](lab.md) |
| Sito vIPI | `vIPI-sito` | `sito/lavori` | [`sito.md`](sito.md) |

Un filone nuovo: dal clone principale `git worktree add ../<cartella> -b <ramo> main`, una riga in questa tabella
(la aggiunge l'integratore) e un file `docs/filoni/<filone>.md`. Mai `cd`, build o test nella cartella di un altro.
`git stash` è COMUNE a tutti i worktree: niente stash, un commit WIP.

## Chi scrive che cosa

| File | Chi |
|---|---|
| `docs/filoni/<filone>.md` | **solo** quel filone: lo stato, cosa è fatto, cosa resta, numerazione sua (sito: S1, S2…) |
| `tests/conteggi/<Assieme>.txt` | chi aggiunge o toglie test in quell'assieme, **nello stesso commit** dei test: `bash tools/conta-test.sh <log> --scrivi <Assieme>` (il log deve avere TUTTI i TFM dell'assieme) |
| `docs/index.md` | nessuno a mano fra i marcatori: una carta nuova → `python tools/indice-doc.py` nello stesso commit. Conflitto lì a una fusione → si rigenera |
| `HANDOFF.md`, `docs/lavori-aperti.md` (le voci §A) | **l'integratore**, quando fonde o consegna: riassume dal file del filone |
| `Directory.Build.props` (versione), `deploy/`, i pacchetti | **l'integratore**: un pacchetto lo prepara uno solo |
| `main` | **l'integratore**: ci si arriva solo per fusione di un ramo verde in CI |

Codice in comune (`Vipi.Application`, `Vipi.Sectorfile`, `tools/`): chi lo cambia lo scrive nel suo file di filone
e nel messaggio del commit, perché l'integratore lo guardi alla fusione.

## Il rito

- **Apertura** (primo messaggio della chat): «Sei l'agente <filone>. Lavori solo in `<cartella>` sul ramo
  `<ramo>`. Leggi `docs/filoni/come-si-lavora-in-parallelo.md` e `docs/filoni/<filone>.md`.» Per l'integratore:
  «Sei l'integratore: stato dei rami, CI, cosa fondere.»
- **Prima di cominciare**: `git merge main` nel proprio ramo, se `main` è andata avanti.
- **Chiusura**: commit, push, CI verde sul proprio ramo (`gh run list --branch <ramo>`), `docs/filoni/<filone>.md`
  aggiornato con «pronto da fondere» o «in corso».
