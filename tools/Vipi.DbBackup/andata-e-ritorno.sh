#!/usr/bin/env bash
# Andata e ritorno della copia del database contro un MariaDB VERO (§A47).
#
#   MARIADB="mariadb -h 127.0.0.1 -P 3306 -u root -p<password>"  \
#   CONN="Server=127.0.0.1;Port=3306;Database=itivao_atc;User Id=...;Password=..."  \
#   SORGENTE=itivao_atc  bash tools/Vipi.DbBackup/andata-e-ritorno.sh
#
# Scrive i valori scomodi nel database SORGENTE (che deve avere già lo schema), fa la copia con lo stesso codice
# del sito, la verifica, la reimporta col client `mariadb` in un database nuovo, e confronta CHECKSUM TABLE
# tabella per tabella. ⚠️ Scrive nella sorgente: solo su un database di prova.
# $MARIADB è un comando con utente capace di creare database; $CONN la connessione per la copia.
set -euo pipefail

: "${MARIADB:?serve MARIADB}" "${CONN:?serve CONN}" "${SORGENTE:?serve SORGENTE}"
RIPRISTINO="${RIPRISTINO:-${SORGENTE}_ripristino}"
qui="$(cd "$(dirname "$0")" && pwd)"
lavoro="$(mktemp -d)"
trap 'rm -rf "$lavoro"' EXIT

sql() { $MARIADB --batch --skip-column-names "$@"; }
fallisci() { echo "::error::$1"; exit 1; }

echo "1. valori scomodi nella sorgente"
sql "$SORGENTE" < "$qui/valori-scomodi.sql"

echo "2. la copia, con lo stesso codice del sito"
dotnet run --project "$qui" -c Release -- scrivi "$CONN" "$lavoro/copia.sql.gz"

echo "3. il verificatore la dichiara intera"
dotnet run --project "$qui" -c Release --no-build -- verifica "$lavoro/copia.sql.gz"

echo "4. reimportata col client mariadb in un database vuoto"
sql -e "DROP DATABASE IF EXISTS \`$RIPRISTINO\`; CREATE DATABASE \`$RIPRISTINO\`;"
gunzip -c "$lavoro/copia.sql.gz" | sql "$RIPRISTINO"

echo "5. CHECKSUM TABLE identico su ogni tabella"
tabelle=$(sql -e "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA='$SORGENTE' AND TABLE_TYPE='BASE TABLE' AND TABLE_NAME <> 'DataProtectionKeys' ORDER BY TABLE_NAME" | tr -d '\r')
[ -n "$tabelle" ] || fallisci "la sorgente non ha tabelle"
n=0
for t in $tabelle; do
  a=$(sql -e "CHECKSUM TABLE \`$SORGENTE\`.\`$t\`" | awk '{print $2}' | tr -d '\r')
  b=$(sql -e "CHECKSUM TABLE \`$RIPRISTINO\`.\`$t\`" | awk '{print $2}' | tr -d '\r')
  [ "$a" = "$b" ] || fallisci "tabella $t: checksum $a nella sorgente, $b dopo il ripristino"
  n=$((n+1))
done
echo "   $n tabelle identiche"

echo "6. i valori scomodi sono tornati uguali (non solo la stessa impronta)"
id0=$(sql -e "SELECT COUNT(*) FROM \`$RIPRISTINO\`.ProvaCopia WHERE Id = 0")
[ "$id0" = "1" ] || fallisci "la riga con Id 0 non è tornata con Id 0"

echo "7. DataProtectionKeys è rimasta fuori"
dpk=$(sql -e "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='$RIPRISTINO' AND TABLE_NAME='DataProtectionKeys'")
[ "$dpk" = "0" ] || fallisci "DataProtectionKeys è finita nella copia"

echo "andata e ritorno riuscita."
