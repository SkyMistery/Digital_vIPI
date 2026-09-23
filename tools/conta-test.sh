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
#   tools/conta-test.sh corsa.log                 # confronta con tests/conteggi/
#   tools/conta-test.sh corsa.log --tfm net8.0    # solo quel TFM: per il job che gira il solo ramo embedding
#   tools/conta-test.sh corsa.log --scrivi Vipi.Ui.Tests   # RISCRIVE l'atteso di UN assieme (test aggiunti lì)
#   tools/conta-test.sh corsa.log --scrivi        # RISCRIVE l'atteso di TUTTI (corsa intera della soluzione)
#
# L'atteso sta in tests/conteggi/, UN FILE PER ASSIEME (`Vipi.Ui.Tests.txt`: una riga «<tfm> <numero>» per TFM).
# Fino al 23 settembre 2026 era un file solo, e due rami che aggiungevano test ad assiemi diversi cambiavano righe
# VICINE: conflitto a ogni fusione (sito S1 contro Sector Lab, fusione del 23-set). Con un file per assieme due
# rami si scontrano solo se toccano lo STESSO assieme, cioè quando lo scontro è vero.
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
ATTESI="$(dirname "$0")/../tests/conteggi"
MODO="${2:-}"
TFM_SOLO="${3:-}"      # con --tfm: il TFM; con --scrivi: l'assieme (facoltativo)

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

# Scrive il file di UN assieme, con le righe «<tfm> <numero>» che la corsa ha visto per lui.
scrivi_assieme() {
  local assieme="$1"
  echo "$CORSA" | awk -v a="$assieme" '$1==a {print $2, $3}' > "$ATTESI/${assieme%.dll}.txt"
  echo "conta-test: atteso riscritto in tests/conteggi/${assieme%.dll}.txt"
}

if [ "$MODO" = "--scrivi" ]; then
  mkdir -p "$ATTESI"
  if [ -n "$TFM_SOLO" ]; then
    # Un assieme solo: quello a cui si sono aggiunti test. Gli altri file non si toccano, anche se la corsa
    # li ha visti: sono di un altro filone, e riscriverli qui rifarebbe il conflitto che il formato evita.
    solo="${TFM_SOLO%.dll}.dll"
    echo "$CORSA" | awk -v a="$solo" '$1==a {t=1} END {exit !t}' \
      || { echo "conta-test: $solo non compare nella corsa: niente da scrivere" >&2; exit 1; }
    # ⚠️ Un log di un TFM solo (`dotnet test -f net10.0`) cancellerebbe in silenzio la riga dell'altro: il
    # cancello smetterebbe di guardare net8 per quell'assieme. Ogni TFM già dichiarato deve stare nella corsa.
    vecchio="$ATTESI/${solo%.dll}.txt"
    if [ -f "$vecchio" ]; then
      for t in $(grep -vE '^[[:space:]]*(#|$)' "$vecchio" | tr -d '\r' | awk '{print $1}'); do
        echo "$CORSA" | awk -v a="$solo" -v t="$t" '$1==a && $2==t {x=1} END {exit !x}' \
          || { echo "conta-test: nella corsa manca $solo ($t), che l'atteso dichiara: serve una corsa con TUTTI i TFM" >&2; exit 1; }
      done
    fi
    scrivi_assieme "$solo"
  else
    # Tutti: la corsa intera della soluzione decide, e un assieme che non c'è più perde il suo file.
    rm -f "$ATTESI"/*.txt
    for a in $(echo "$CORSA" | awk '{print $1}' | sort -u); do scrivi_assieme "$a"; done
  fi
  exit 0
fi

[ -d "$ATTESI" ] || { echo "conta-test: manca la cartella tests/conteggi" >&2; exit 1; }

# L'atteso ricomposto come una tabella sola, «<assieme> <tfm> <numero>», dal file di ogni assieme.
ATTESO="$(for f in "$ATTESI"/*.txt; do
  [ -f "$f" ] || continue
  a="$(basename "$f" .txt).dll"
  grep -vE '^[[:space:]]*(#|$)' "$f" | tr -d '\r' | awk -v a="$a" '{print a, $1, $2}'
done)"
[ -n "$ATTESO" ] || { echo "conta-test: tests/conteggi è vuota" >&2; exit 1; }

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
done <<< "$ATTESO"

# Un assieme che gira ma non e' nell'atteso: un progetto di test nuovo che nessuno ha dichiarato.
while read -r assieme tfm visto; do
  [ -z "$assieme" ] && continue
  if [ "$MODO" = "--tfm" ] && [ "$tfm" != "$TFM_SOLO" ]; then continue; fi
  if ! echo "$ATTESO" | awk -v a="$assieme" -v t="$tfm" '$1==a && $2==t {trovato=1} END {exit !trovato}'; then
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

Se il calo è VOLUTO, si riscrive l'atteso — `tools/conta-test.sh <log> --scrivi <assieme>` — nello stesso commit che
lo causa, così la decisione si vede nel diff.
FINE
  exit 1
fi

if [ "$SALITI" -gt 0 ]; then
  cat >&2 <<'FINE'

I test sono PIU' di quelli dichiarati. Non e' un guasto del codice: e' l'atteso che non e' stato riscritto.
Va fatto nello stesso commit che aggiunge i test, o il prossimo calo si nasconde sotto questa salita:
  tools/conta-test.sh <log di dotnet test> --scrivi <assieme>    # solo l'assieme dei test nuovi
FINE
  exit 1
fi

echo "conta-test: conteggi identici all'atteso."
