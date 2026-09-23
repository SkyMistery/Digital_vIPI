# Filone sito vIPI — stato

> Scrive **solo** l'agente del sito (cartella `vIPI-sito`, ramo `sito/lavori`). Regole:
> [`come-si-lavora-in-parallelo.md`](come-si-lavora-in-parallelo.md). Numerazione del filone: **S1, S2…**
> (le voci §A in `docs/lavori-aperti.md` le scrive l'integratore alla consegna).

## Dove siamo — 23 settembre 2026

- ✅ **S1** editor APP unito, «sezioni comuni» non ricarica più la pagina: fuso e **online in 1.43.0**
  (`docs/lavori-aperti.md` §S1, §A118). Al prossimo scarico di diagnostica: che non tornino gli
  `ObjectDisposedException` di `UnionPanel` su `/services/vsop/<icao>/apps/editor`.
- ✅ **S2** dopo «Hide» (sezioni in comune) i MEMBRI si ricaricano — **pronto da fondere**, nessun pacchetto.
  - **Difetto**: `UnioneCambiata` delle tre pagine ospite (APP, aeroporto, vSOP MIL) ricaricava solo il
    proprio documento; le sezioni nascoste nei membri restavano a schermo fino al ricarico della pagina.
  - **Perché non era banale**: `UnionMembersEditor` teneva una lista in cui i membri si aggiungevano e basta —
    un membro tolto o rimontato restava dentro con lo scope chiuso, e ricaricarli tutti = `ObjectDisposedException`.
  - **Rimedio**: `RegistroMembri` (nuovo, `Components/Doc/`) tiene gli editor **per documento**: un editor nuovo
    dello stesso documento sostituisce il vecchio, `OnParametersSet` toglie chi non è più membro e riordina
    secondo l'unione (prima l'indice seguiva l'ordine di registrazione, non «Sposta»). `@key="m.DocumentId"`
    sulle sezioni dei membri. L'ospite chiama `_membri.RicaricaAsync(<id dei membri appena riletti>)`: chi è
    appena uscito non viene toccato.
  - **Test**: `RegistroMembriTests` (9: registro + guardie sul sorgente delle tre pagine e del `@key`).
    `Vipi.Ui.Tests` 1659 → **1668** su net8 e net10.
  - **Prova dal vivo** (copia del DB di sviluppo, unione vIPI LIBV + vSOP MIL LIBV, editor aeroporto): «Hide» su
    «Piste» nel solo MIL → il membro passa da 1 a 2 sezioni nascoste senza ricarico, zero errori nel log.
    **Controprova** con la riga spenta: il servizio nasconde «LVP» («one section changed») e il membro resta fermo.
- ✅ **S3** fra i punti dei trasferimenti mancavano le STAR (segnalato: «ERIKA» a LIRN non proponeva ERIKA 1A) —
  **pronto da fondere**, nessun pacchetto.
  - **Causa**: i suggerimenti vengono dal DB (tabella viva, rilette a ogni apertura del form), ma solo le righe in
    vigore OGGI. Le STAR il sito le legge dal 21 settembre (1.35.0): al primo import sono righe nuove e prendono il
    ciclo che il sectorfile dichiara — `2610.txt`, in vigore dal 1° ottobre — quindi tutte fuori nel 2609.
    Riprodotto su copia del DB con l'import acceso: 10 STAR di LIRN timbrate `2610`, le SID restano `2607`.
  - **Rimedio** (scelto dal committente fra tre): `ProcedureReferenceResolver.ElencoAsync` deriva la tabella al
    ciclo ENTRANTE (`IAiracService` facoltativo nel costruttore). Vale per i punti dei trasferimenti e per il
    selettore «Cita». ⚠️ **Codice in comune toccato**: `Vipi.Application` (solo questo file).
  - ⚠️ **Non toccato, di proposito**: fino al 1° ottobre le sezioni «STAR» dei documenti aeroporto restano vuote
    (stesso timbro 2610); si sistemano da sole col ciclo.
  - Test: `L_elenco_guarda_al_ciclo_ENTRANTE`. `Vipi.Application.Tests` 2903 → **2904**. Dal vivo: accordo
    LIBB_ES → LIRR_TS, sezione Arrivi LIRN, «ERIK» propone ERIKA 1A (24) ed ERIKA 1C (06).
- ▶ Alla ripresa: se `main` è andata avanti, `git merge main`.
- Conteggi del filone: di solito `tests/conteggi/Vipi.Ui.Tests.txt`.
