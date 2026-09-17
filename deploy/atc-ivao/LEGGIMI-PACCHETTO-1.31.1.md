# Pacchetto 1.31.1 — solo i file cambiati

> **Timbro:** `1.31.1 · ab20c46` (17 settembre 2026). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Su 1.31.0** (`68265f9`, online dal 17 settembre). **PATCH, NESSUNA migrazione**: il database non cambia,
> il pacchetto si consegna da solo via FTP. **20 file**: 7 in radice, 1 in `en/`, 12 in `wwwroot/_content/Vipi.Ui/`.

---

## Che cos'è

**Accendere e spegnere i singoli spazi aerei sulla mappa** (§A66). Nella tabella **Spazi aerei (AIP)** sotto la mappa
AoR, a inizio riga c'è un quadratino col colore del settore: un clic spegne o riaccende **quello spazio** sulla mappa 2D
e nella vista 3D. Passando col mouse su una riga il suo poligono si evidenzia; passando su un poligono compaiono nome e
quote e la riga si evidenzia. Le chip dei settori funzionano come prima, ma non riaccendono uno spazio spento dalla
tabella.

## I 20 file, e l'ordine

Le impronte `sha256` stanno in `IMPRONTE.txt`, dentro la cartella del pacchetto.

```
wwwroot/_content/Vipi.Ui/vipi-aor.js         (+ .br, .gz)
wwwroot/_content/Vipi.Ui/vipi-aor3d.js       (+ .br, .gz)
wwwroot/_content/Vipi.Ui/vipi-theme.css      (+ .br, .gz)
wwwroot/_content/Vipi.Ui/vipi-print.css      (+ .br, .gz)
Vipi.Host.staticwebassets.endpoints.json
en/Vipi.Ui.resources.dll
Vipi.Application.pdb
Vipi.Application.dll
Vipi.Ui.pdb
Vipi.Ui.dll
Vipi.Host.pdb
Vipi.Host.dll      ← per ultimo
```

1. si caricano **tutti** col nome finto;
2. si rinominano nell'ordine qui sopra: prima i dodici file di `wwwroot` **e** `endpoints.json` (viaggiano insieme),
   poi le frasi inglesi, poi ogni `.pdb` prima del suo `.dll`, `Vipi.Host.dll` per ultimo;
3. il riavvio: `tmp/restart.txt` **e poi si apre il sito una volta**, o Passenger non se ne accorge.

⚠️ **La regola del caricamento è quella di sempre**: nome finto e poi rinomina. Procedura per esteso in
[`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

⚠️ **Restano fuori** `Vipi.Infrastructure.dll` e gli altri assiemi (codice non cambiato: le impronte diverse sono solo
ricompilazione), il resto di `wwwroot`, `Vipi.Host.deps.json` e `Vipi.Host.runtimeconfig.json`: identici a 1.31.0,
controllato per impronta.

## Il controllo dopo il riavvio

⚠️ **Non basta che la pagina si apra.** Il controllo che conta è **la Ricerca**, perché passa dal server:
`https://atc.it.ivao.aero/services/vsop/search`, due lettere, **la riga sotto il campo deve cambiare**.

Col login da amministratore:

- il timbro in barra: **`1.31.1 · ab20c46`**;
- `services/vsop/admin/diagnostics`: **`Schema` = `0`**;
- `services/vsop/libb/apps/editor?app=LIBP_APP`, sezione AOR, **dopo Ctrl+F5** (i file JS e CSS nuovi arrivano solo
  così): nella tabella un clic sul quadratino di PESCARA CTR Z2 lo toglie dalla mappa e sbiadisce la riga; il mouse su un
  poligono dice nome e quote.

## ⚠️ Documenti pubblicati con 1.31.0: ripubblicare

Una release congela la mappa. In quelle pubblicate **mentre era online 1.31.0** i poligoni non sanno a quale riga
appartengono: la tabella c'è, ma il quadratino sbiadisce la riga senza togliere il poligono. Si sistema ripubblicando.
Riguarda solo gli APP agganciati (LIBA, LIPH, LIPY, LICR, LIEE, LIRZ, LIBP) pubblicati oggi; **LIBA_APP** in particolare,
se è già stato ripubblicato dopo 1.31.0, va ripubblicato ancora una volta. Le release più vecchie non hanno la tabella e
non sono toccate.

Da fuori, per chi verifica:

```powershell
$env:BASE = 'https://atc.it.ivao.aero'; $env:SOLO_PUBBLICO = '1'
node .claude/skills/verifica-live/pacchetto-verifica.js
```

### Provato prima di spedire

Publish win-x64 avviato dalla sua cartella sulla copia di produzione del 17-set in MariaDB 11.4.10: timbro
`1.31.1 · commit ab20c46`, `pacchetto-verifica.js` **10/10**. Sullo stesso binario (JS minificato) `spazi-aerei-verifica.js`
su LIBP_APP in anteprima: Z2 e Z4 spenti → 4 poligoni su 6; chip dell'APP spenta e riaccesa → torna a 4, non 6; hover riga
↔ poligono con il nome; la 3D nasce con gli spazi spenti e risponde. Nessun errore in console.
Pagina **pubblica** con la release fatta sotto 1.31.0: il quadratino non toglie il poligono (atteso); ripubblicato sul
binario di 1.31.1: 4 poligoni su 6 anche in pubblico.
