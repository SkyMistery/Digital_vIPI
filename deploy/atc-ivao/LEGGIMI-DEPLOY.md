# vIPI — build self-contained linux-x64

Build del **9 agosto 2026**, branch `feat/persistenza-mysql`. Self-contained: **non serve installare .NET**,
il runtime è nel pacchetto (~111 MB scompattato, 47,8 MB compresso).

Il database di destinazione è **MariaDB** — quella del vostro server, 11.4.10 — e l'applicazione ci parla
col provider Pomelo. Il login è già predisposto: c'è un `appsettings.Production.json` pronto, in cui restano
da riempire solo i valori segreti.

> ## 🔎 Se qualcosa non parte, guardate qui prima di tutto
>
> A ogni avvio l'applicazione scrive, nella sottocartella **`diagnostica/`** accanto all'eseguibile, due
> file di testo:
>
> - **`diagnostica/avvio-diagnostica.txt`** — con quale configurazione ha provato a partire. Scritto sempre,
>   anche quando l'avvio riesce.
> - **`diagnostica/avvio-errore.txt`** — l'eccezione che ha impedito l'avvio. Se questo file non c'è,
>   l'applicazione è partita (o non è arrivata nemmeno a scriverlo: in quel caso la cartella non è scrivibile
>   e i file finiscono nella temporanea di sistema; il percorso esatto lo stampa a video all'avvio).
>
> ⚠️ **Quella cartella non va servita dal web.** `avvio-errore.txt` contiene lo stack trace completo: è il
> contenuto giusto per chi deve capire, e quello sbagliato da lasciare raggiungibile da fuori. Se
> l'eseguibile sta dentro il documento radice del sito, la riga che serve è già in `nginx-vipi.conf`:
> `location ~ ^/diagnostica/ { deny all; }`. Dall'11 agosto 2026 stanno in sottocartella proprio per poterli
> negare con una riga sola.
>
> Sono pensati per essere **spediti così come sono**: password, ClientId e ClientSecret non ci finiscono
> mai. Della configurazione si riporta solo *se* un valore c'è, non quale.

---

## In cinque passi

### 1. Scompatta

```sh
sudo mkdir -p /opt/vipi && sudo unzip vipi-linux-x64-mariadb-20260809.zip -d /opt/vipi
sudo chmod +x /opt/vipi/Vipi.Host
```

### 2. Riempi `appsettings.Production.json`

È già nella cartella. **È l'unico file da toccare.**

```json
{
  "Persistence":       { "Provider": "MySql" },
  "ConnectionStrings": { "Vipi": "Server=localhost;Port=3306;Database=itivao_atc;User Id=itivao_atc;Password=METTI-QUI-LA-PASSWORD" },
  "VipiAuth":          { "Enabled": true, "ClientId": "", "ClientSecret": "" },
  "Ivao":              { "ClientId": "", "ClientSecret": "" }
}
```

ℹ️ `Provider` si chiama `MySql` anche puntando a MariaDB: è il nome del *dialetto*, non del prodotto.

Sono **cinque caselle ma tre valori**: `VipiAuth.ClientId` e `Ivao.ClientId` sono lo **stesso** ID — è la
stessa app IVAO, usata per due scopi (il login degli utenti e il token applicativo).

| Valore | Dove va | Note |
|---|---|---|
| Password del database `itivao_atc` | dentro `ConnectionStrings.Vipi` | |
| **ClientId IVAO** | `VipiAuth.ClientId` **e** `Ivao.ClientId` | lo stesso valore in entrambi |
| **ClientSecret IVAO** | `Ivao.ClientSecret` | serve all'ATC live, al roster staff e **all'import** dei dati |
| | `VipiAuth.ClientSecret` | **opzionale**: lasciandolo vuoto l'app si comporta da client pubblico (solo PKCE), che funziona — a meno che l'app su IVAO non sia registrata come *confidential* |

⚠️ Il ClientId va scritto due volte: **copia-incolla, non ribattere.** Se sbagliate solo `Ivao.ClientId` il
login funziona benissimo e sembra tutto a posto, ma non partono ATC live, roster e **import** — cioè il
modo in cui il sito si popola. (`diagnostica/avvio-diagnostica.txt` segnala se i due sono diversi.)

ℹ️ Nel file c'è anche **`AllowedHosts: "atc.it.ivao.aero"`**: il sito risponde solo a quel nome. È un valore
in più da ricordare **se il dominio cambia** — con un nome diverso l'applicazione risponde `400`. Il default
di ASP.NET Core sarebbe `*`, che accetta qualunque `Host`, e da quello l'handler OIDC costruisce il
`redirect_uri` del login.

Contiene segreti: `sudo chown vipi:vipi /opt/vipi/appsettings.Production.json && sudo chmod 600 …`

⚠️ Va ricreato a ogni redeploy che sovrascrive la cartella. Se un giorno sparisce, l'applicazione **riparte
lo stesso su SQLite**, con un database vuoto. Mettetelo nel backup.

### 3. Carica i contenuti (consigliato) — oppure parti da vuoto

Insieme a questo pacchetto vi consegniamo un file **`.sql`** (~4 MB) con i contenuti veri: ACC, settori,
aeroporti, aree regolamentate, documenti e loro pubblicazioni. **È la strada consigliata**: importatelo nel
database `itivao_atc` **prima** del primo avvio.

```sh
mysql -u itivao_atc -p itivao_atc < vipi-atc-it-ivao-aero-<data>.sql
```

Il file porta con sé anche lo **schema** e la tabella di storia delle migrazioni, quindi l'applicazione al
primo avvio non riapplica nulla e trova tutto pronto. Non contiene le chiavi di sessione, che l'app si
ricrea da sé.

Se invece partite da un database vuoto, l'applicazione crea lo schema da sé al primo avvio (38 tabelle) —
ma il sito sarà **vuoto**: gli ACC e i settori si importano poi dalle pagine di amministrazione, e i
documenti editoriali non ci sarebbero affatto.

⚠️ In entrambi i casi la prima istruzione della migrazione è `ALTER DATABASE … CHARACTER SET utf8mb4;`, che
la libreria emette da sé: serve il permesso **ALTER sul database**. Con `GRANT ALL ON itivao_atc.*` c'è.

### 4. Servizio e reverse proxy

`vipi.service` e `nginx-vipi.conf` sono nella cartella `deploy/`, già scritti.

```sh
sudo cp deploy/vipi.service /etc/systemd/system/
sudo systemctl daemon-reload && sudo systemctl enable --now vipi
```

⚠️ L'unit contiene `ASPNETCORE_ENVIRONMENT=Production`. **Senza quella variabile
`appsettings.Production.json` viene ignorato in silenzio** — nessun errore, l'applicazione parte e si
comporta come se il file non esistesse.

Il file nginx ha già le direttive WebSocket. **Non sono opzionali**: l'applicazione è Blazor Server e senza
quelle le pagine si aprono e restano mute — nessun errore, i pulsanti semplicemente non rispondono.

⚠️ Il proxy deve mandare all'applicazione **tutto il dominio**, non solo `/vsop`: i callback del login IVAO
stanno sulla radice (`/signin-oidc`, `/signout-callback-oidc`).

⚠️ **Una sola istanza dell'applicazione.** Blazor Server tiene lo stato dell'utente in un circuito vivo nel
processo che l'ha aperto: due processi dietro un bilanciatore fanno cadere le pagine in riconnessione
continua. Se un domani servisse scalare, prima serve un backplane condiviso, poi il secondo processo.

### 5. Registra i redirect sul portale IVAO

```
https://atc.it.ivao.aero/signin-oidc
https://atc.it.ivao.aero/signout-callback-oidc
```

Devono combaciare esatti: `https`, nessuno slash finale.

---

## Due impostazioni del vostro MariaDB che ci servono

| Impostazione | Cosa serve | Perché |
|---|---|---|
| **`max_allowed_packet`** | **≥ 4 MB** | Le immagini dei documenti sono `longblob` e viaggiano in un pacchetto solo. L'applicazione taglia a 3 MB per immagine, quindi 4 MB bastano; il default MariaDB (16 MB) va benissimo, ma su alcune configurazioni condivise è 1 MB e allora i caricamenti sopra il mega fallirebbero. |
| **`sql_mode`** | ci basta **saperlo** | Fuori dalla modalità *strict* MariaDB converte silenziosamente invece di lanciare. Abbiamo provato l'applicazione in entrambe le modalità senza vedere differenze, ma è l'unica cosa che non possiamo verificare per davvero da qui. |

Le tabelle nascono con i **nomi in maiuscolo** (`Accs`, `Documents`, …) e su Linux i nomi sono sensibili
alle maiuscole: il `.sql` che consegniamo è stato prodotto apposta per rispettarli. Non passatelo attraverso
strumenti che «normalizzano» i nomi.

---

## Guasti già visti, e cosa significano

| Sintomo | Causa |
|---|---|
| Il servizio **non parte** | leggete `diagnostica/avvio-errore.txt`: la prima riga è quasi sempre la causa |
| `/vsop/auth/login` risponde **404** | `VipiAuth.Enabled` non è arrivato a `true`. Quell'indirizzo **non esiste** finché il login non è attivo: non è una pagina rotta, è una rotta che l'applicazione non registra. Quasi sempre significa che `appsettings.Production.json` non viene letto — vedi `diagnostica/avvio-diagnostica.txt` |
| Le pagine si aprono ma non rispondono | WebSocket non inoltrati dal proxy |
| Le pagine cadono in «riconnessione» a ripetizione | più di un processo dietro il proxy (vedi passo 4), oppure timeout del proxy troppo brevi |
| Il login torna in `http://` e fallisce | manca `X-Forwarded-Proto` |
| `Access denied for user 'itivao_atc'` | password sbagliata nella connection string |
| «Table 'accs' already exists» | una migrazione era fallita a metà. La DDL di MariaDB non è transazionale e non torna indietro: svuotate il database e ripartite |
| Un'immagine non si carica | quasi certamente `max_allowed_packet` troppo basso (vedi sopra) |

Se avete accesso alla console, l'avvio a mano mostra tutto subito:
`cd /opt/vipi && ASPNETCORE_ENVIRONMENT=Production ./Vipi.Host`

---

## Cosa è stato verificato, e cosa no

**Verificato** (6–9 agosto 2026, contro una **MariaDB 11.4.10 vera**, con un utente che ha permessi solo sul
proprio database — le stesse condizioni del vostro server):

- migrazioni applicate da zero, sia dagli strumenti sia **all'avvio dell'applicazione**, che è il percorso
  vero: 38 tabelle;
- **collation** `utf8mb4_uca1400_as_cs` su 163 colonne stringa su 163 (le 2 escluse sono la tabella di
  storia di EF, innocua); `LIRF` e `lirf` convivono nello stesso indice unico e il `WHERE` li distingue;
- **travaso dei dati veri** e `.sql` **riletto in un database vuoto**: 39 tabelle su 39 con conteggi
  identici all'origine, e nessuna migrazione riapplicata all'avvio;
- le **chiavi di sessione** sopravvivono a un riavvio (stanno nel database, non su disco);
- **flussi editoriali guidati sull'applicazione vera**: import di ACC, settori e aeroporti; import delle SID
  per singolo aeroporto; **pubblicazione di tutti e tre i tipi di documento** (vIPI ACC, aeroporto, vLOA);
  lock di modifica; ricerca globale; vista live; caricamento e rilettura di un'immagine, byte per byte
  identica;
- la stessa verifica di schema e collation gira a ogni modifica nella nostra integrazione continua, su
  MariaDB 11.4.10 su Linux.

**Non verificato:**

- **questo pacchetto non è mai stato eseguito su Linux**: è compilato in modo incrociato da Windows. Il
  primo avvio da voi è anche la prima prova su quel sistema;
- il login IVAO completo fino al ritorno sul dominio definitivo: manca la registrazione dei redirect.

⚠️ Il branch `feat/persistenza-mysql` non è ancora fuso in `main`: questa è la build del passaggio a
MariaDB, non una release ordinaria.
