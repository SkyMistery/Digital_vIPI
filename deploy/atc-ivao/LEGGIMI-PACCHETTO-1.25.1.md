# Pacchetto 1.25.1 — solo i file cambiati

> **Timbro:** `1.25.1 · 33aa578` (12 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.25.0.** **19 file**. ✅ **NESSUNA MIGRAZIONE**: il database non si tocca, e non c'è nessun
> file «da non dimenticare» che lo riguardi.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

## Che cos'è questo pacchetto, in una riga

**Non porta niente di nuovo da guardare.** Porta le stesse funzioni che costano meno: meno connessioni
aperte, meno lavoro ripetuto, meno byte. È il seguito misurato dell'audit delle prestazioni del
12 settembre, e da fuori **non si deve vedere niente**.

Le due sole cose che si potrebbero notare sono volute:

- **la favicon** ha due misure invece di quattro (quelle che i browser usano davvero);
- chi **non è entrato** non apre più una connessione permanente col server. Chi **è entrato** ha
  esattamente il gettone «live» di prima, che si aggiorna da sé quando si collega in frequenza.

---

> ## 🔴 I FILE IN SOTTOCARTELLA, E I DUE CHE VIAGGIANO INSIEME
>
> - **`wwwroot/favicon.ico`** va dentro **`wwwroot/`**;
> - **nove file** vanno in **`wwwroot/_content/Vipi.Ui/`**: `vipi-aor3d.js`, `vipi-awos.js` e
>   `vipi-boot.js`, ciascuno coi suoi `.br` e `.gz`.
>
> ⚠️ **`Vipi.Host.staticwebassets.endpoints.json` viaggia INSIEME a quei dieci**, e sta in **radice**. È
> l'indice che dice al sito con che nome chiedere ogni file di `wwwroot`: caricarne uno solo dei due fa
> chiedere al browser nomi che non esistono, e la pagina esce senza stile.
>
> ## L'ORDINE
>
> 1. prima si caricano **tutti e 19** i file col nome finto, ciascuno nella sua cartella;
> 2. poi le rinomine, **una di seguito all'altra e senza pause**, in questo ordine:
>    1. i dieci di `wwwroot/` e `Vipi.Host.staticwebassets.endpoints.json`;
>    2. `Vipi.Application.dll`, `Vipi.Hosting.dll`, `Vipi.Ui.dll` e i loro `.pdb`;
>    3. **`Vipi.Host.dll` per ultimo** (porta il timbro).
> 3. poi il riavvio: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge.
>
> ⚠️ Quattro assiemi cambiano insieme: le rinomine vanno fatte **di seguito**, perché se il processo
> ripartisse a metà le pagine cadrebbero finché non arriva il resto.

---

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il selettore della lingua, lo zoom e il tema funzionano anche su un
sito in cui il motore non è mai partito. Il controllo che conta è **la Ricerca**, perché passa dal server:

1. `https://atc.it.ivao.aero/services/vsop/search`
2. si scrivono due lettere (es. `LI`)
3. **la riga sotto il campo deve cambiare**.

Poi, col login da amministratore, il timbro in barra: **`1.25.1 · 33aa578`**.

ℹ️ **Al primo avvio dopo questo pacchetto** l'applicazione rifà una volta le riconciliazioni interne dei
documenti — è previsto, ed è come funziona adesso: le rifà **una volta a ogni versione nuova** e poi smette,
invece che a ogni riavvio. Nel log compare una riga che lo dice. Dal secondo avvio in poi tace.

---

# 🔴 E poi le DUE cose che questo pacchetto non può fare da solo

**Valgono più di tutte e sette le modifiche qui dentro messe insieme**, e sono due impostazioni del vostro
pannello. Le trovate per esteso, con le istruzioni, in [`LEGGIMI-DEPLOY.md`](LEGGIMI-DEPLOY.md).

## 1. La Cache Rule su Cloudflare

Oggi **nessuna pagina del sito viene tenuta da Cloudflare**: ogni lettore arriva fino al vostro server,
anche per leggere una pagina identica a quella che avete appena servito a qualcun altro. Misurato:
`cf-cache-status: DYNAMIC` su tutto.

| campo | valore |
|---|---|
| quando | `URI Path starts with /services/` **and not** `Cookie contains "vipi.auth"` |
| cosa fare | *Eligible for cache*, **Respect origin TTL** |
| chiave di cache | includere il cookie **`.AspNetCore.Culture`** |

⚠️ **Le due righe in fondo non sono facoltative.** Cloudflare non guarda l'intestazione `Vary` se non per la
compressione: senza la condizione sul cookie, **chi è entrato potrebbe ricevere la pagina di un anonimo** —
senza i propri tasti. E senza la chiave sulla lingua, chi legge in inglese può ricevere la copia italiana.

**La verifica, subito dopo averla creata** — due comandi, e il secondo è quello che conta:

```sh
curl -sD - -o /dev/null https://atc.it.ivao.aero/services/vsop | grep -i cf-cache-status
curl -sD - -o /dev/null -H 'Cookie: vipi.auth=qualunque' \
  https://atc.it.ivao.aero/services/vsop | grep -i cf-cache-status
```

Il primo, alla seconda esecuzione, deve dire `HIT`. **Il secondo non deve dire `HIT` mai.** Se lo dice, la
regola è sbagliata e va spenta subito.

## 2. Due direttive nginx per i file statici

Sul vostro server i file di `wwwroot/` **li serve nginx**, non la nostra applicazione — l'abbiamo verificato
dall'esterno. Va benissimo, nginx lo fa meglio; ma vuol dire che **due cose che il pacchetto prepara non
arrivano a nessuno**: la durata di cache degli asset, e le versioni **già compresse al massimo** che stanno
accanto a ogni file (i `.br`). Oggi il sito spedisce il 13% di byte in più del necessario e non dice a
nessun browser per quanto tenere i file.

Fra le **direttive nginx aggiuntive** del sito in Plesk — lo stesso posto dove stanno già le regole che
negano `/diagnostica/` e `appsettings*.json`:

```nginx
location ~* \.(css|js|woff2|ico|svg)$ {
    expires 1y;
    add_header Cache-Control "public, immutable";
}

brotli_static on;    # serve i .br già pronti, se il modulo c'è
gzip_static  on;     # il ripiego, per gli stessi file
```

⚠️ **Un anno è sicuro qui, e non lo sarebbe su un altro sito**: ogni indirizzo porta già l'impronta del
contenuto (`?v=…`), quindi un file che cambia **cambia indirizzo** e nessuno resta con la copia vecchia.

⚠️ **Da NON estendere a `/_framework/`**: quei file non portano l'impronta, e una cache lunga lì darebbe, il
giorno di un aggiornamento, un sito che si vede e non risponde.

**La verifica:** dopo le direttive, `curl -sD - -o /dev/null https://atc.it.ivao.aero/_content/Vipi.Ui/vipi-theme.css`
deve mostrare `Cache-Control: public, immutable`, e i byte scaricati devono **scendere**.

---

## I 19 file

Le impronte `sha256` di ognuno stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

**In radice (9):**

```
Vipi.Application.dll        Vipi.Application.pdb
Vipi.Hosting.dll            Vipi.Hosting.pdb
Vipi.Ui.dll                 Vipi.Ui.pdb
Vipi.Host.dll               Vipi.Host.pdb
Vipi.Host.staticwebassets.endpoints.json
```

**In `wwwroot/` (1):**

```
favicon.ico
```

**In `wwwroot/_content/Vipi.Ui/` (9):**

```
vipi-aor3d.js   vipi-aor3d.js.br   vipi-aor3d.js.gz
vipi-awos.js    vipi-awos.js.br    vipi-awos.js.gz
vipi-boot.js    vipi-boot.js.br    vipi-boot.js.gz
```

⚠️ **Restano fuori** `Vipi.Domain.dll`, `Vipi.Infrastructure.dll`, `Vipi.Infrastructure.MySqlMigrations.dll`
e i due assiemi Aurora: il loro codice non è cambiato, e nessuno di loro implementa qualcosa che sia
cambiato altrove. **Ogni file in più è una rinomina in più su un file che il processo tiene aperto**: non è
prudenza, è rischio.
