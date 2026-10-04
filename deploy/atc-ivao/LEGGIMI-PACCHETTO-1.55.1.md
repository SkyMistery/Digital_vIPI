# Pacchetto 1.55.1 — solo i file cambiati

> **Timbro:** `1.55.1 · 5493151` (1 ottobre 2026), nel **piè di pagina** (staff), nella riga `Versione` della
> **Diagnostica** e in `diagnostica/avvio-diagnostica.txt`.

> **Parte da 1.55.0** (`395bce9`, online dal 1 ottobre). È una **PATCH senza migrazioni**: solo `Vipi.Ui`. Si consegna
> da sola via FTP, il database non cambia, e **il rollback a due rinomine resta valido**. Nessun segreto nuovo, nessuna
> configurazione da toccare.
> **4 file**, tutti in **radice**. Niente in `en/` né in `wwwroot`.
>
> ⚠️ **Il caricamento si fa come sempre**: nome finto, poi rinomina — procedura in
> [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). Meglio non usare l'editor mentre si carica, ed
> **evitare hh:55–57**.

---

## Che cos'è

- **S91 — lingua nei documenti uniti**: ogni membro di un documento unito tiene la sua regola di lingua. Caso che l'ha
  fatta nascere: LIRP, dove la vIPI è bilingue e il vSOP solo in inglese; prima il vSOP prendeva la regola della vIPI.

## I 4 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

```
Vipi.Ui.pdb                             Vipi.Ui.dll
Vipi.Host.pdb                           Vipi.Host.dll                            ← per ultimo (timbro)
```

Poi `tmp/restart.txt` **e si apre il sito una volta**. ⚠️ **Non aprire il sito fra le rinomine e il `restart.txt`**: il
processo vecchio, ancora acceso, può caricare un file nuovo e dare una pagina d'errore per qualche secondo (è successo
con 1.54.2).

⚠️ **Restano fuori** tutti gli altri assiemi (sorgente invariato, cambiati solo per la ricompilazione),
`en/Vipi.Ui.resources.dll` (nessuna frase cambiata), `deps.json`, `runtimeconfig.json`, `appsettings.json` e tutto
`wwwroot` con l'indice: identici a 1.55.0, controllato per impronta e col `git diff`.

## Il controllo dopo il riavvio

⚠️ **Da anonimo la Ricerca è chiusa** (login obbligatorio da 1.54.0): il controllo della Ricerca si fa **col login**.
`https://atc.it.ivao.aero/services/vsop/search`, scrivete **`LIRF`**: devono comparire **dei documenti** (vIPI Roma,
LIRF…), non solo la riga «N risultati». «0 risultati per LIRF» è un **guasto**, anche se la riga è cambiata.

- col login da staff: il timbro **`1.55.1 · 5493151`** nel piè di pagina; in Diagnostica `Schema` resta **`0`**;
- LIRP unito, in italiano: la parte vIPI in italiano, la parte vSOP in inglese;
- `https://atc.it.ivao.aero/vsop/health` risponde **`Healthy`**.

## Restano

I gesti su LIRE/LIBG, che aspettano il SOD.
