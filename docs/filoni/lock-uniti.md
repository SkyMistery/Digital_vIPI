# Filone «lock uniti» — ramo `fix/lock-uniti`

Aperto il 25 settembre 2026 dopo un check chiesto dal committente sui documenti uniti (carta
`docs/feature/2026-09-03-documenti-uniti.md`): il lock deve bloccarli tutti, e la pubblicazione di uno deve
pubblicare anche gli altri.

## Esito del check (dal vivo su LIBV, vSOP MIL #33 + vIPI #34, copia del DB)

- ✅ Pubblicazione accoppiata: «Pubblica ora» dalla porta del MIL e «Pianifica» dalla porta della vIPI
  pubblicano tutti e due allo stesso ciclo e alla stessa data, bozze promosse, lock mollati.
- ✅ «Modifica» prende il lock di tutti e due, «Fine modifica» li molla tutti e due; un lock altrui gia'
  noto al caricamento ferma tutto e non lascia lock presi a meta'.
- 🔴 **Difetto 1** — il membro decideva sul `_shell.Lock` letto al CARICAMENTO: uscito il collega,
  «Modifica» rifiutava ancora fino al ricarico della pagina.
- 🔴 **Difetto 2 (grave)** — collega entrato DOPO il caricamento, bozza gia' aperta: `AcquireLockAsync` non
  solleva (torna il lock altrui) e `StartEditingAsync` metteva `IsEditing = IsEditable`. La modifica unita si
  apriva col membro in mano al collega, senza avviso; le scritture le fermava il server («lock scaduto»).
  Il guscio e' comune: valeva anche per il documento singolo.

## Correzione

- `DocumentEditorShell.StartEditingAsync`: in modifica solo se il lock tornato e' NOSTRO; altrimenti
  `Lock` = quello altrui (il nome arriva all'avviso dell'unione).
- `PrendiLockAsync` dei tre `*SectionsEditor`: tolto il pre-controllo sulla cache, decide il database.
- Test: 2 sul guscio (`DocumentEditorShellTests`), 3 guardie sul sorgente (`DocumentiUnitiTests`);
  provati ROSSI sul codice di prima (4 su 5, il quinto e' il controllo).

## Stato

Pronto da fondere quando la CI del ramo e' verde.
