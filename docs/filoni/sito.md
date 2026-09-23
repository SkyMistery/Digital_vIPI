# Filone sito vIPI — stato

> Scrive **solo** l'agente del sito (cartella `vIPI-sito`, ramo `sito/lavori`). Regole:
> [`come-si-lavora-in-parallelo.md`](come-si-lavora-in-parallelo.md). Numerazione del filone: **S1, S2…**
> (le voci §A in `docs/lavori-aperti.md` le scrive l'integratore alla consegna).

## Dove siamo — 23 settembre 2026

- ✅ **S1** editor APP unito, «sezioni comuni» non ricarica più la pagina: fuso e **online in 1.43.0**
  (`docs/lavori-aperti.md` §S1, §A118). Al prossimo scarico di diagnostica: che non tornino gli
  `ObjectDisposedException` di `UnionPanel` su `/services/vsop/<icao>/apps/editor`.
- 🟡 **Aperto**: dopo «Hide» i MEMBRI non si ricaricano (tre pagine ospite).
- ▶ Alla ripresa: `git merge main` nel worktree (il ramo è indietro di tutta la consegna 1.43.0).
- Conteggi del filone: di solito `tests/conteggi/Vipi.Ui.Tests.txt`.
