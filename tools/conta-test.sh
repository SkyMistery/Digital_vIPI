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
#   tools/conta-test.sh corsa.log --tfm net8.0    # solo quel TFM: per il job che gira la sola produzione
#   tools/conta-test.sh corsa.log --scrivi        # RISCRIVE l'atteso (dopo aver aggiunto dei test)
#
# ATTENZIONE: `--tfm` si DICHIARA, non si deduce dal log. Dedurlo vorrebbe dire che una corsa in cui il
# ramo net8 sparisce del tutto passerebbe in silenzio - cioe' proprio il guasto che questo script esiste
# per prendere.
#
# ⚠️ L'atteso si alza da sé quando si aggiungono test — il confronto è «non meno di» — ma si ABBASSA solo a
# mano, con un commit che dice perché. È lì che sta il valore: un calo diventa una decisione, non un caso.
set -uo pipefail

LOG="${1:?serve il file di log di dotnet test}"
ATTESI="$(dirname "$0")/../tests/conteggi-attesi.txt"
MODO="${2:-}"
TFM_SOLO="${3:-}"

# Le righe di riepilogo di `dotnet test`, una per assieme e TFM:
#   Passed!  - Failed: 0, Passed: 2237, ... - Vipi.Application.Tests.dll (net8.0)
CORSA="$(grep -E '^(Passed|Failed)!' "$LOG" \
  | sed -E 's/.*Passed: *([0-9]+),.*- (Vipi[^ ]+\.dll) \((net[0-9.]+)\).*/\2 \3 \1/' \
  | sort)"

if [ -z "$CORSA" ]; then
  echo "conta-test: nel log non c'è nessuna riga di riepilogo. La corsa dei test è avvenuta davvero?" >&2
  exit 1
fi

if [ "$MODO" = "--scrivi" ]; then
  { echo "# Test eseguiti per assieme e TFM. Il confronto è «non meno di»: questo file si alza da sé"
    echo "# quando si aggiungono test, e si abbassa SOLO a mano, con un commit che dice perché."
    echo "# Rigenerare: tools/conta-test.sh <log di dotnet test> --scrivi"
    echo "$CORSA"
  } > "$ATTESI"
  echo "conta-test: atteso riscritto in $ATTESI"
  exit 0
fi

[ -f "$ATTESI" ] || { echo "conta-test: manca $ATTESI" >&2; exit 1; }

GUASTI=0
while read -r assieme tfm atteso; do
  case "$assieme" in \#*|"") continue;; esac

  # Il job che gira la sola produzione esegue i soli assiemi net8.0: le attese sugli altri TFM non
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
    echo "CALATO  $assieme ($tfm): attesi almeno $atteso, eseguiti $visto"
    GUASTI=$((GUASTI+1))
  else
    echo "ok      $assieme ($tfm): $visto (atteso >= $atteso)"
  fi
done < "$ATTESI"

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

echo "conta-test: nessun calo."
