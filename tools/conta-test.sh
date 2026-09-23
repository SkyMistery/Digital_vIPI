#!/usr/bin/env bash
# Conta i test eseguiti, per assieme e per TFM, e pretende che non CALINO.
#
# Perché esiste: `dotnet test` sulla soluzione non fallisce quando un progetto sparisce dalla corsa — la
# riga esce zero e la CI diventa verde su meno test di ieri. Non è teoria: fino all'11 agosto 2026 girava
# net10 per tutti tranne Vipi.Infrastructure.Tests, cioè ~1000 test che non toccavano mai il runtime di
# produzione, e ad accorgersene è stato un ragionamento, non un cancello (revisione del 6 settembre, R-028).
#
# Basta che un .csproj perda un TFM, che un filtro escluda una classe o che un progetto esca dalla
# soluzione: la CI resta verde e nessuno vede che i test sono meno. Qui calare fa rumore.
#
# Uso:
#   dotnet test Vipi.slnx ... | tee corsa.log
#   tools/conta-test.sh corsa.log                 # confronta con tests/conteggi-attesi.txt
#   tools/conta-test.sh corsa.log --tfm net8.0    # solo quel TFM: per il job che gira il solo ramo embedding
#   tools/conta-test.sh corsa.log --scrivi        # RISCRIVE l'atteso (dopo aver aggiunto dei test)
#
# ATTENZIONE: `--tfm` si DICHIARA, non si deduce dal log. Dedurlo vorrebbe dire che una corsa in cui il
# ramo net8 sparisce del tutto passerebbe in silenzio - cioe' proprio il guasto che questo script esiste
# per prendere.
#
# ⚠️ L'atteso NON si alza da sé. Qui c'era scritto il contrario, e non era vero: il file si riscrive solo con
# `--scrivi`, che la CI non passa, e l'8 settembre 2026 l'atteso era rimasto fermo alla 1.16.1 — un calo di
# ~190 test in Application sarebbe passato verde, cioè il guasto che R-028 doveva chiudere (T-056, revisione
# del 13 settembre 2026). Da allora il cancello scatta nei DUE sensi: un calo e una salita non dichiarati
# fermano entrambi la corsa. Chi aggiunge test riscrive l'atteso nello stesso commit, e il diff lo mostra.
set -uo pipefail

LOG="${1:?serve il file di log di dotnet test}"
ATTESI="$(dirname "$0")/../tests/conteggi-attesi.txt"
MODO="${2:-}"
TFM_SOLO="${3:-}"

# Le righe di riepilogo di `dotnet test`, una per assieme e TFM:
#   Passed!  - Failed: 0, Passed: 2237, ... - Vipi.Application.Tests.dll (net8.0)
#
# ⚠️ La riga può arrivare SPEZZATA in due: con più assiemi in parallelo `dotnet test` la scrive a pezzi, e
# sul runner del 14 settembre 2026 è uscita «Passed! ... Duration: 5 s» e, alla riga dopo, « - Vipi.Application
# .Tests.dll (net8.0)». Il cancello ha dato «MANCA» su una corsa verde. Qui le due metà si ricuciono prima
# di contare: una riga di riepilogo senza «.dll (» prende la riga che segue, se comincia con « - ».
#
# ⚠️ E può arrivare INCOLLATA in coda a un'altra: il 23 settembre 2026 (corsa 35829372818, e prima il 21) è uscita
# «A total of 1 test files matched the specified pattern.Passed!  - Failed: 0, Passed: 68, … Hosting.Tests.dll
# (net8.0)», e il cancello ha dato «MANCA» su una corsa verde. Qui un riepilogo che comincia a metà riga si porta
# a capo prima di tutto il resto.
#
# ⚠️ E le due cose insieme (corsa 35849102486, 23 settembre 2026, agente del sito): «Passed! … 65, Duration: 270
# ms» con INCOLLATO in coda il riepilogo intero di Assets, e la coda « - Vipi.AuroraProfiles.Tests.dll (net8.0)»
# alla riga dopo. Portato a capo quel che era incollato, fra la testa e la sua coda si mette un riepilogo
# COMPLETO: la testa aspetta oltre i riepiloghi completi, e non solo la riga che la segue.
CORSA="$(sed -E 's/(.)((Passed|Failed)! +- )/\1\n\2/g' "$LOG" | awk '
    pend != "" {
        if ($0 ~ /^ *- /) { print pend $0; pend = ""; next }
        if ($0 ~ /^(Passed|Failed)!/ && $0 ~ /\.dll \(/) { print; next }
        print pend; pend = ""
    }
    /^(Passed|Failed)!/ && $0 !~ /\.dll \(/ { pend = $0; next }
    { print }
    END { if (pend != "") print pend }' \
  | grep -E '^(Passed|Failed)!' \
  | sed -E 's/.*Passed: *([0-9]+),.*- (Vipi[^ ]+\.dll) \((net[0-9.]+)\).*/\2 \3 \1/' \
  | sort)"

if [ -z "$CORSA" ]; then
  echo "conta-test: nel log non c'è nessuna riga di riepilogo. La corsa dei test è avvenuta davvero?" >&2
  exit 1
fi

if [ "$MODO" = "--scrivi" ]; then
  { echo "# Test eseguiti per assieme e TFM. Il confronto è ESATTO: un calo o una salita non dichiarati fermano"
    echo "# la CI. Si riscrive nello stesso commit che aggiunge o toglie test, così la decisione si vede nel diff."
    echo "# Rigenerare: tools/conta-test.sh <log di dotnet test> --scrivi"
    echo "$CORSA"
  } > "$ATTESI"
  echo "conta-test: atteso riscritto in $ATTESI"
  exit 0
fi

[ -f "$ATTESI" ] || { echo "conta-test: manca $ATTESI" >&2; exit 1; }

GUASTI=0
SALITI=0
while read -r assieme tfm atteso; do
  case "$assieme" in \#*|"") continue;; esac

  # Il job del ramo embedding (net8, dal 13-set non piu' la produzione) esegue i soli assiemi net8.0: le attese sugli altri TFM non
  # riguardano quella corsa, e vanno saltate DICENDOLO.
  if [ "$MODO" = "--tfm" ] && [ "$tfm" != "$TFM_SOLO" ]; then
    echo "salto   $assieme ($tfm): questa corsa e' limitata a $TFM_SOLO"
    continue
  fi

  visto="$(echo "$CORSA" | awk -v a="$assieme" -v t="$tfm" '$1==a && $2==t {print $3}')"

  if [ -z "$visto" ]; then
    echo "MANCA   $assieme ($tfm): atteso $atteso, non è stato eseguito affatto"
    GUASTI=$((GUASTI+1))
  elif [ "$visto" -lt "$atteso" ]; then
    echo "CALATO  $assieme ($tfm): attesi $atteso, eseguiti $visto"
    GUASTI=$((GUASTI+1))
  elif [ "$visto" -gt "$atteso" ]; then
    echo "SALITO  $assieme ($tfm): attesi $atteso, eseguiti $visto — l'atteso non e' stato riscritto"
    SALITI=$((SALITI+1))
  else
    echo "ok      $assieme ($tfm): $visto"
  fi
done < "$ATTESI"

# Un assieme che gira ma non e' nell'atteso: un progetto di test nuovo che nessuno ha dichiarato.
while read -r assieme tfm visto; do
  [ -z "$assieme" ] && continue
  if [ "$MODO" = "--tfm" ] && [ "$tfm" != "$TFM_SOLO" ]; then continue; fi
  if ! awk -v a="$assieme" -v t="$tfm" '$1==a && $2==t {trovato=1} END {exit !trovato}' "$ATTESI"; then
    echo "NUOVO   $assieme ($tfm): $visto test, assente dall'atteso"
    SALITI=$((SALITI+1))
  fi
done <<< "$CORSA"

if [ "$GUASTI" -gt 0 ]; then
  cat >&2 <<'FINE'

I test sono MENO di quelli attesi, e la corsa è comunque verde: è esattamente il modo in cui l'11 agosto
2026 mille test hanno smesso di girare sul runtime di produzione senza che niente diventasse rosso.

Da guardare, in quest'ordine:
  1. un `.csproj` che ha perso un TFM (`TargetFrameworks` diventato `TargetFramework`);
  2. un progetto uscito da Vipi.slnx;
  3. un filtro o un `Skip=` nuovo.

Se il calo è VOLUTO, si riscrive l'atteso — `tools/conta-test.sh <log> --scrivi` — nello stesso commit che
lo causa, così la decisione si vede nel diff.
FINE
  exit 1
fi

if [ "$SALITI" -gt 0 ]; then
  cat >&2 <<'FINE'

I test sono PIU' di quelli dichiarati. Non e' un guasto del codice: e' l'atteso che non e' stato riscritto.
Va fatto nello stesso commit che aggiunge i test, o il prossimo calo si nasconde sotto questa salita:
  tools/conta-test.sh <log di dotnet test> --scrivi
FINE
  exit 1
fi

echo "conta-test: conteggi identici all'atteso."
