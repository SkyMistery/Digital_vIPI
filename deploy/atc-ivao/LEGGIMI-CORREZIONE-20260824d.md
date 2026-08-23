# vIPI — correzione del 24 agosto 2026 (pacchetto «d»)

**Leggete questo foglio.** Va sopra il pacchetto «c», ed è **soli file**.

> ## ⛔ Il database NON si tocca
>
> Niente `.sql`, niente `DROP DATABASE`, niente import. Lo schema non cambia.

> ## ⚠️ Il modo di caricare è cambiato, e questa volta è la parte importante
>
> La procedura di caricamento è [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md), nuova in
> questo pacchetto. **Non** i passi di `LEGGIMI-AGGIORNAMENTO.md`, che dicono «sovrascrivete i file» e
> «fermate l'applicazione dal pannello»: la prima cosa è ciò che ha buttato giù il sito la notte del 23→24
> agosto, la seconda non è eseguibile senza accesso al pannello Plesk.
>
> In breve: **si carica col nome finto e si rinomina**, mai sovrascrivere l'applicazione viva.

---

## Che cosa corregge

Niente che si veda usando il sito. Corregge una cosa sola, e riguarda **noi che dobbiamo capire i guasti**.

La notte del 23→24 agosto il sito è rimasto irraggiungibile per ore **senza lasciare una sola riga**: né nei
log, né in `diagnostica/avvio-errore.txt`, che è il file scritto apposta per gli host dove non si possono
leggere i log. La causa è stata trovata per esclusione, misurando dimensioni di file, e sono state due
serate.

Il motivo per cui quel file è rimasto vuoto era un difetto nostro. Il programma installava la propria rete
di sicurezza come **prima istruzione** dell'avvio — ma il runtime .NET, prima di eseguire la prima
istruzione di un metodo, deve **risolvere tutti i tipi** che quel metodo usa. Se il fallimento è lì (una
libreria mancante, arrivata troncata, o rimasta indietro di una versione), il programma muore **prima** che
la rete di sicurezza esista.

Ora il corpo dell'avvio sta in un metodo separato, chiamato dentro un `try`. Un fallimento di quel tipo
diventa un errore gestito, e `diagnostica/avvio-errore.txt` lo scrive col nome preciso — per esempio:

```
System.IO.FileNotFoundException: Could not load file or assembly 'Vipi.Hosting, Version=1.0.0.0…'
```

ℹ️ Provato per davvero, non solo compilato: tolta una libreria dall'applicazione, il file compare e dice
quale libreria manca.

**In più, in questo pacchetto:** il foglio [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md),
che è la procedura di aggiornamento buona per questo server, e `LEGGIMI-FTP.md` corretto di conseguenza.

---

## Che cosa caricare

Rispetto al pacchetto «c» cambia **un file solo**:

| File | Dimensione attesa |
|---|---|
| `Vipi.Host.dll` | **64.000 byte** |

Tutto il resto — le altre librerie, `wwwroot`, `content`, i file di sistema — è **byte per byte identico** a
quanto è già sul server.

ℹ️ `Vipi.Host.pdb` (42.160 byte) è facoltativo: non serve a far girare niente, dà solo i numeri di riga
negli errori. Caricatelo se volete diagnosi più precise.

---

## Come si carica

Per esteso in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md). In sintesi:

1. FileZilla in **binario**;
2. caricate il file come **`Vipi.Host.dll.nuovo`** — si fa ad applicazione accesa, senza rischi;
3. verificate che sul server misuri **64.000 byte esatti**. Se no, ricaricate e non proseguite;
4. due rinomine: `Vipi.Host.dll` → `Vipi.Host.dll.vecchio`, poi `Vipi.Host.dll.nuovo` → `Vipi.Host.dll`;
5. riavviate: `restart.txt` vuoto dentro `tmp/` (se `tmp/` non c'è, createla — vedi il foglio);
6. aprite il sito.

⚠️ **Lasciate `Vipi.Host.dll.vecchio` sul server**: è il rollback, due rinomine al contrario.

---

## Come si vede che è andata

| Controllo | Cosa deve succedere |
|---|---|
| `diagnostica/avvio-diagnostica.txt` | la prima riga porta **data e ora di adesso**. È l'unica prova che sia ripartita la versione nuova: che il sito risponda non basta |
| `https://atc.it.ivao.aero/services/vsop` | la pagina si apre con gli ACC: LIRR, LIMM, LIBB |
| Il login IVAO | entra, e in alto compare il vostro nome |
| `diagnostica/avvio-errore.txt` | **non deve esistere** |

⚠️ Sul server ce n'era uno vecchio, del **16 agosto 2026** (`Access to the path '/var/lib/vipi' is denied`),
di un guasto risolto quel giorno stesso e mai rimosso. Se è ancora lì, **cancellatelo**: finché resta,
quest'ultima riga darà sempre un falso allarme.

Compilato con gli avvisi trattati come errori: **0 avvisi**, **3621 test verdi**.

⚠️ Come i precedenti, questo pacchetto **non è mai stato eseguito su Linux**: è compilato in modo incrociato
da Windows. La correzione, però, è stata provata eseguendola su Windows con una libreria tolta di mezzo.
