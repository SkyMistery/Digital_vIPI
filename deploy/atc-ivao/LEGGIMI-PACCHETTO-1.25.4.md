# Pacchetto 1.25.4 — solo i file cambiati

> **Timbro:** `1.25.4 · d645c77` (13 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **CUMULATIVO rispetto a 1.25.2**: contiene anche tutto 1.25.3. Si carica **sia che 1.25.3 sia già su, sia
> che no** — se 1.25.3 non è ancora stata caricata, **si salta** e si carica direttamente questo.
> **11 file**: 10 in **radice** e 1 nella sottocartella **`en/`**. ✅ **Nessuna migrazione**, niente `wwwroot`.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

## Che cos'è

Il secondo lotto di correzioni della revisione del 13 settembre, tutto su **ciò che il sito espone a chi
non è entrato** e su **chi può fare cosa**. Da fuori si vede poco, ed è giusto così:

- 🔴 **Chi perde un incarico nello staff IVAO perde anche il livello sul sito**, entro quattro ore. Prima
  restava Admin o Editor finché continuava a visitare il sito. Se IVAO non risponde, nessuno viene buttato
  fuori.
- **La lingua delle pagine pubbliche non passa più da un lettore all'altro** (chi arrivava con il browser in
  inglese poteva decidere la lingua della pagina anche per il visitatore successivo).
- Un indirizzo inventato (`/services/vsop/qualcosa-che-non-esiste`) risponde **404**, non più una pagina «ACC
  sconosciuto» tenuta in cache.
- I cookie del sito viaggiano solo in HTTPS, e il browser ricorda per **un anno** di usare solo HTTPS.
- Lo stream del «live» e una pagina 3D senza ingressi non sono più aperti a chiunque; i tetti di richieste
  dei tre servizi pubblici non si rubano più il posto a vicenda.
- Più tutto 1.25.3: la correzione di sicurezza sulle pagine pubbliche, e **eliminare riservato agli
  amministratori**.

## Gli 11 file, e l'ordine

```
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Infrastructure.pdb
Vipi.Infrastructure.dll
Vipi.Hosting.pdb
Vipi.Hosting.dll
Vipi.Ui.pdb
Vipi.Ui.dll
en/Vipi.Ui.resources.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo (porta il timbro)
```

1. si caricano **tutti** col nome finto (quello di `en/` dentro la cartella `en/`);
2. si rinomina nell'ordine qui sopra: prima ogni `.pdb`, poi il suo `.dll`, e `Vipi.Host.dll` per ultimo;
3. il riavvio: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.
Poi, col login da amministratore, il timbro in barra: **`1.25.4 · d645c77`**.

E due `curl` che dicono se le correzioni sono arrivate:

```sh
# un ACC inventato: deve dire 404
curl -s -o /dev/null -w "%{http_code}\n" https://atc.it.ivao.aero/services/vsop/xx-inventato

# la guida: Vary deve contenere Accept-Language
curl -sD - -o /dev/null https://atc.it.ivao.aero/services/vsop/guide | grep -i '^vary'
```

---

# Il pannello: cosa si può fare adesso

- ✅ **Le direttive nginx** per i file statici: come nei fogli precedenti.
- ✅ **`passenger_min_instances ≥ 1`**: da questo pacchetto **si può**, la lingua non passa più da un lettore
  all'altro.
- ⚠️ **La Cache Rule di Cloudflare**: si può, ma con **una condizione in più** rispetto al foglio di 1.25.1 —
  `Cookie contains ".AspNetCore.Culture"`. La regola aggiornata e il perché sono in
  [`LEGGIMI-DEPLOY.md`](LEGGIMI-DEPLOY.md). Senza quella condizione, il bordo di Cloudflare (che non guarda la
  lingua del browser) rifarebbe da sé l'errore che questo pacchetto toglie.
