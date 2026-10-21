#!/usr/bin/env bash
# Prova della LETTURA di conta-test.sh: le righe di riepilogo di `dotnet test` come sono uscite davvero, rotte, nei
# giorni in cui il cancello ha dato «MANCA» su una corsa verde.
#
# Perché esiste: ogni correzione della lettura (14 settembre, 23 settembre, 21 ottobre 2026) è stata scritta guardando
# il solo caso del giorno, e niente diceva se i casi di prima reggevano ancora. Qui stanno tutti, e la CI li rilegge
# prima di contare: una lettura che ne rompe uno si ferma qui, non su una corsa vera.
#
# Uso: bash tools/conta-test-prova.sh        (esce 0 se ogni caso si legge come atteso)
set -uo pipefail

CONTA="$(dirname "$0")/conta-test.sh"
DIR="$(mktemp -d)"
trap 'rm -rf "$DIR"' EXIT

FALLITI=0

# caso <nome> <log> <atteso>: il log passa da `conta-test.sh --leggi` e deve dare esattamente l'atteso.
caso() {
  local nome="$1" log="$2" atteso="$3"
  printf '%b' "$log" > "$DIR/corsa.log"
  local letto
  letto="$(bash "$CONTA" "$DIR/corsa.log" --leggi 2>&1)"
  if [ "$letto" = "$atteso" ]; then
    echo "ok      $nome"
  else
    echo "ROTTO   $nome"
    echo "  atteso:"; echo "$atteso" | sed 's/^/    /'
    echo "  letto:";  echo "$letto"  | sed 's/^/    /'
    FALLITI=$((FALLITI+1))
  fi
}

caso "riga intera" \
  'Passed!  - Failed:     0, Passed:  2237, Skipped:     0, Total:  2237, Duration: 5 s - Vipi.Application.Tests.dll (net8.0)\n' \
  'Vipi.Application.Tests.dll net8.0 2237'

# 14 settembre 2026: la coda alla riga dopo.
caso "spezzata (14-set)" \
  'Passed!  - Failed:     0, Passed:  2237, Skipped:     0, Total:  2237, Duration: 5 s\n - Vipi.Application.Tests.dll (net8.0)\n' \
  'Vipi.Application.Tests.dll net8.0 2237'

# 23 settembre 2026, corsa 35829372818: il riepilogo incollato in coda a un'altra riga.
caso "incollata dopo (23-set)" \
  'A total of 1 test files matched the specified pattern.Passed!  - Failed:     0, Passed:    68, Skipped:     0, Total:    68, Duration: 1 s - Vipi.Hosting.Tests.dll (net8.0)\n' \
  'Vipi.Hosting.Tests.dll net8.0 68'

# 23 settembre 2026, corsa 35849102486: testa, riepilogo COMPLETO incollato, coda della testa alla riga dopo.
caso "testa, completo, coda (23-set)" \
  'Passed!  - Failed:     0, Passed:    65, Skipped:     0, Total:    65, Duration: 270 msPassed!  - Failed:     0, Passed:    63, Skipped:     0, Total:    63, Duration: 1 s - Vipi.Assets.Tests.dll (net8.0)\n - Vipi.AuroraProfiles.Tests.dll (net8.0)\n' \
  'Vipi.Assets.Tests.dll net8.0 63
Vipi.AuroraProfiles.Tests.dll net8.0 65'

# 21 ottobre 2026, corsa 39593831601: testa con l'ANNUNCIO di un altro assieme incollato, coda alla riga dopo.
caso "testa, annuncio, coda (21-ott)" \
  'Passed!  - Failed:     0, Passed:  3418, Skipped:     0, Total:  3418, Duration: 6 sTest run for /home/runner/work/Digital_vIPI/Digital_vIPI/tests/Vipi.E2E.Tests/bin/Release/net10.0/Vipi.E2E.Tests.dll (.NETCoreApp,Version=v10.0)\n - Vipi.Application.Tests.dll (net10.0)\nPassed!  - Failed:     0, Passed:   510, Skipped:     0, Total:   510, Duration: 40 s - Vipi.E2E.Tests.dll (net10.0)\n' \
  'Vipi.Application.Tests.dll net10.0 3418
Vipi.E2E.Tests.dll net10.0 510'

# Due teste in fila prima delle code: le code vanno alle teste nell'ordine in cui sono arrivate.
caso "due teste in fila" \
  'Passed!  - Failed:     0, Passed:    10, Skipped:     0, Total:    10, Duration: 1 s\nPassed!  - Failed:     0, Passed:    20, Skipped:     0, Total:    20, Duration: 1 s\n - Vipi.Domain.Tests.dll (net8.0)\n - Vipi.Domain.Tests.dll (net10.0)\n' \
  'Vipi.Domain.Tests.dll net10.0 20
Vipi.Domain.Tests.dll net8.0 10'

if [ "$FALLITI" -gt 0 ]; then
  echo "conta-test-prova: $FALLITI casi letti male. La lettura di conta-test.sh è cambiata: rileggi i commenti sui casi." >&2
  exit 1
fi
echo "conta-test-prova: tutti i casi si leggono come atteso."
