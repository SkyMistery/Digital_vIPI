# Pacchetto 1.59.0 — solo i file cambiati

> **Timbro:** `1.59.0 · fc442f2` (9 ottobre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.58.0** (`2ecbdd6`, online dall'8 ottobre). È una **MINOR senza migrazioni**, e si consegna da sola via
> FTP. Nessun segreto nuovo, nessuna configurazione da toccare.
> **13 file**: 1 in **`en/`**, 3 in **`wwwroot/_content/Vipi.Ui/`** e 9 in **radice**.

> ## ⚠️ Per tornare indietro, questa volta c'è un gesto in più
>
> L'ordine salvato di una sezione degli accordi ora può avere il **verso opposto** («per punto Z→A», «per quota 9→1»).
> Sono due valori nuovi, scritti nel database col loro nome. La 1.58.0 **non li conosce**: se dopo il carico qualcuno
> salva una sezione col verso opposto e poi si rimettono i file vecchi, la pagina degli accordi e i documenti che
> leggono quella sezione possono dare errore. **Prima di tornare indietro** quelle sezioni vanno rimesse su «a mano», «per
> punto A→Z» o «per quota 1→9». Finché nessuno usa il verso opposto, bastano le due rinomine come sempre.

> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **S100 — accordi di coordinamento, l'ordinamento ha un verso**:
  - il comando «Ordina» è rifatto: una chiave e, accanto, il verso (**A→Z / Z→A**, per la quota **1→9 / 9→1**), in
    barra e nella testata di ogni sezione;
  - «Ordina» c'è anche nella vista **Elenco**: per mittente, ricevente, aeroporto, tipo, punti, quota — e un clic
    sull'intestazione di una colonna fa lo stesso;
  - l'ordine **salvato** di una sezione ricorda il verso: vale nella pagina, nei documenti e nella vista live;
  - chi non ha punti o non ha quota resta **in fondo** in tutti e due i versi;
  - i **tasti di riga** sono icone quadrate, e non escono più dal bordo della tabella.
- **S101 — il motto della divisione**, «it takes time», nel piè di pagina sotto il marchio.

ℹ️ Fuse il 9 ottobre su richiesta del committente **prima** del suo sguardo a schermo: le guarda online. Le scelte
prese dal Sito e da confermare stanno in «Dopo il carico».

## I 13 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

1. **`en/` (1)** — per primo: `en/Vipi.Ui.resources.dll`
2. **`wwwroot/_content/Vipi.Ui/` (3)**:

```
vipi-theme.css   vipi-theme.css.br   vipi-theme.css.gz
```

3. **in radice (9)**, in quest'ordine, ogni `.pdb` col suo `.dll`:

```
Vipi.Host.staticwebassets.endpoints.json                ← insieme ai file di wwwroot
Vipi.Domain.pdb         Vipi.Domain.dll
Vipi.Application.pdb    Vipi.Application.dll
Vipi.Ui.pdb             Vipi.Ui.dll
Vipi.Host.pdb           Vipi.Host.dll                   ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**. ⚠️ **Non aprire il sito fra le rinomine e il `restart.txt`**: il
processo vecchio, ancora acceso, può caricare un file nuovo e dare una pagina d'errore per qualche secondo (è successo
con 1.54.2).

⚠️ **Restano fuori** `Vipi.Infrastructure`, `Vipi.Infrastructure.MySqlMigrations` e `Vipi.Hosting` (sorgente
invariato; di `Vipi.Application` usano solo firme che non cambiano — in questo pacchetto ci sono soltanto aggiunte),
`Vipi.AuroraBridge.Contracts`, `Vipi.AuroraProfiles`, `deps.json`, `runtimeconfig.json`, `appsettings.json` e il resto
di `wwwroot`: controllato per impronta e col `git diff`.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- il timbro **`1.59.0 · fc442f2`** nel piè di pagina (staff), e lì sotto il marchio la riga **«it takes time»**;
- in Diagnostica **`Schema` = `0`** (non c'è migrazione: deve restare 0 com'era);
- la pagina degli **accordi**: accanto a «Ordina» c'è il tasto del verso; in **Elenco** «Ordina» offre mittente,
  ricevente, aeroporto, tipo, punti, quota; i tasti in fondo a ogni riga sono icone e stanno dentro la tabella;
- se la pagina appare con i tasti di riga storti o senza icone, è il CSS vecchio in cache o `vipi-theme.css` non
  caricato con l'indice: da fuori l'indirizzo dev'essere `vipi-theme.css?v=abf90f76`.

## Dopo il carico

- **Da guardare, e dire se va** (scelte del Sito): senza punti o senza quota → in fondo in tutti e due i versi; il
  verso si legge «A→Z / Z→A» e «1→9 / 9→1»; le voci della sezione sono «per punto / per quota»; il motto è scritto in
  minuscolo, com'è stato dato.
- Restano i gesti della 1.58.0, se non ancora fatti: accendere «L'elenco è completo» sui settori d'area di **LIMM**
  (finché no, `/vsop/health` dice `Degraded`), Torino–Genova, Padova e Bologna, e ripubblicare le vIPI ACC.
- Restano i gesti su LIRE/LIBG, che aspettano il SOD.
