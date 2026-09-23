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
gunzip -c "$lavoro/copia.sql.gz" | sql --max-allowed-packet=1G "$RIPRISTINO"

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

echo "5b. SHOW CREATE TABLE identico su ogni tabella (indici, chiavi esterne, collation, AUTO_INCREMENT)"
# CHECKSUM TABLE guarda solo le righe: una colonna che perde la collation o un indice sparito passerebbero.
for t in $tabelle; do
  a=$(sql -e "SHOW CREATE TABLE \`$SORGENTE\`.\`$t\`" | tr -d '\r')
  b=$(sql -e "SHOW CREATE TABLE \`$RIPRISTINO\`.\`$t\`" | tr -d '\r')
  [ "$a" = "$b" ] || { diff <(echo "$a") <(echo "$b") || true; fallisci "tabella $t: SHOW CREATE TABLE diverso dopo il ripristino"; }
done
echo "   $n definizioni identiche"

echo "5b-bis. le viste condivise (v_share_) sono tornate, leggono il database ripristinato e danno le stesse righe"
# Le crea una migrazione: un ripristino che le perdesse lascerebbe __EFMigrationsHistory a dire «fatto» su una
# vista che non c'è. E una vista che puntasse ancora al database di partenza darebbe le righe giuste per sbaglio.
viste=$(sql -e "SELECT TABLE_NAME FROM information_schema.VIEWS WHERE TABLE_SCHEMA='$SORGENTE' ORDER BY TABLE_NAME" | tr -d '\r')
v=0
for t in $viste; do
  c=$(sql -e "SELECT COUNT(*) FROM information_schema.VIEWS WHERE TABLE_SCHEMA='$RIPRISTINO' AND TABLE_NAME='$t'" | tr -d '\r')
  [ "$c" = "1" ] || fallisci "vista $t: assente dopo il ripristino"
  def=$(sql -e "SELECT VIEW_DEFINITION FROM information_schema.VIEWS WHERE TABLE_SCHEMA='$RIPRISTINO' AND TABLE_NAME='$t'" | tr -d '\r')
  case "$def" in *"\`$SORGENTE\`."*) fallisci "vista $t: dopo il ripristino legge ancora il database $SORGENTE";; esac
  a=$(sql -e "SELECT GROUP_CONCAT(COLUMN_NAME, ':', COLUMN_TYPE ORDER BY ORDINAL_POSITION) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='$SORGENTE' AND TABLE_NAME='$t'" | tr -d '\r')
  b=$(sql -e "SELECT GROUP_CONCAT(COLUMN_NAME, ':', COLUMN_TYPE ORDER BY ORDINAL_POSITION) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='$RIPRISTINO' AND TABLE_NAME='$t'" | tr -d '\r')
  [ "$a" = "$b" ] || fallisci "vista $t: colonne diverse dopo il ripristino ($a / $b)"
  a=$(sql -e "SELECT COUNT(*) FROM \`$SORGENTE\`.\`$t\`" | tr -d '\r')
  b=$(sql -e "SELECT COUNT(*) FROM \`$RIPRISTINO\`.\`$t\`" | tr -d '\r')
  [ "$a" = "$b" ] || fallisci "vista $t: $a righe nella sorgente, $b dopo il ripristino"
  v=$((v+1))
done
echo "   $v viste tornate"

echo "5c. la tabella di prova è uscita in più INSERT, e il blob da 3 MB in uno suo"
inserts=$(gunzip -c "$lavoro/copia.sql.gz" | grep -c '^INSERT INTO `ProvaCopia`' || true)
[ "$inserts" -ge 3 ] || fallisci "ProvaCopia in $inserts INSERT: la prova non ha spezzato niente, quindi non prova lo spezzare"

echo "6. i valori scomodi sono tornati uguali (non solo la stessa impronta)"
id0=$(sql -e "SELECT COUNT(*) FROM \`$RIPRISTINO\`.ProvaCopia WHERE Id = 0")
[ "$id0" = "1" ] || fallisci "la riga con Id 0 non è tornata con Id 0"
doppi=$(sql -e "SELECT COUNT(*) FROM \`$SORGENTE\`.ProvaCopia a JOIN \`$RIPRISTINO\`.ProvaCopia b USING (Id) WHERE a.Doppio = b.Doppio AND a.Id BETWEEN 3 AND 6")
[ "$doppi" = "4" ] || fallisci "i double a 17 cifre non sono tornati identici ($doppi su 4)"
blob=$(sql -e "SELECT LENGTH(Bin) = 3145728 AND SHA2(Bin,256) = (SELECT SHA2(Bin,256) FROM \`$SORGENTE\`.ProvaCopia WHERE Id = 7) FROM \`$RIPRISTINO\`.ProvaCopia WHERE Id = 7")
[ "$blob" = "1" ] || fallisci "il blob da 3 MB non è tornato identico"

echo "7. DataProtectionKeys è rimasta fuori"
dpk=$(sql -e "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='$RIPRISTINO' AND TABLE_NAME='DataProtectionKeys'")
[ "$dpk" = "0" ] || fallisci "DataProtectionKeys è finita nella copia"

echo "andata e ritorno riuscita."
