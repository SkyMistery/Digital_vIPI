# Pacchetto 1.22.1 — solo i file cambiati

> **Timbro:** `1.22.1 · 659c334b` (11 settembre 2026, sera). È quel che compare nella barra in alto agli
> amministratori, e nella riga `Versione` di `diagnostica/avvio-diagnostica.txt`.

> **Sostituisce 1.22.0.** **6 file**.
>
> ⚠️ **La regola del caricamento è quella di sempre**: si carica col **nome finto** e poi si **rinomina**.
> Sovrascrivere un `.dll` mentre l'applicazione gira lo tronca sotto il processo, che muore all'istante. La
> procedura per esteso è in [`LEGGIMI-AGGIORNARE-VIA-FTP.md`](LEGGIMI-AGGIORNARE-VIA-FTP.md).

---

> ## 🟢 PICCOLO, E SENZA SORPRESE
>
> **Nessuna migrazione**, nessuna frase nuova, nessun `.css` o `.js`: niente `wwwroot`, niente satellite
> inglese, niente `MySqlMigrations`. Solo `Vipi.Application`, `Vipi.Ui` e `Vipi.Host` (dove sta il numero di
> versione), coi loro `.pdb`.
>
> ⚠️ **Per ogni file, le due rinomine una SUBITO dopo l'altra**, e `Vipi.Host.dll` per ultimo. Qui gli assiemi
> restano compatibili con quelli che ci sono già (`Vipi.Application` aggiunge soltanto), quindi un riavvio a
> metà non fa danni — ma il buco fra le due rinomine di uno stesso file è meglio non aprirlo.

---

## Che cosa porta

### 🟢 1. Le procedure d'avvicinamento si leggono «ILS, VOR»

**Chiesto da te.** Nel documento le APP procedures escono sempre con **una virgola e uno spazio** fra le voci,
qualunque sia la forma in cui erano state scritte a mano prima delle chip («VOR,RNP», una tabulazione in testa,
un punto e virgola). Vale **anche per i documenti già pubblicati**, senza ripubblicarli, e nessun aeroporto
finisce fra i «da ripubblicare» per una virgola.

### 🔴 2. Il vSOP di un campo senza vIPI civile: ora si riempiono anche le frequenze

**Segnalato da te su LIMS.** Su un campo «solo militare» senza vIPI, l'editor del vSOP scriveva piste, SID e
quote di transizione, ma la sezione **Frequenze** restava vuota e **non si poteva riempire**: le sue righe
nascono dal catalogo delle posizioni ATC, e il pannello che lo amministra c'era solo nell'editor della vIPI.

Adesso nell'editor del vSOP, **solo su quei campi**, ci sono:

- il pannello **«Settori ATC»** — lo stesso della vIPI: importa le posizioni dalla sorgente, frequenza
  principale, limiti, «nascondi»; e la voce «Settori» nel sommario;
- il tasto **«Re-importa da IVAO»** nella barra a destra: piste, Transition Altitude e posizioni.

E se la pagina dell'editor era rimasta aperta mentre la vIPI veniva eliminata, premendo **«✎ Modifica»** si
rimette in pari da sola.

⚠️ **Su LIMS le frequenze resteranno vuote anche importando**: la sorgente IVAO per Piacenza **non elenca nessuna
posizione ATC**. L'import risponde «niente», come dall'editor della vIPI. Per avere frequenze su LIMS si
collegano quelle di altri enti col campo «cerca callsign/freq» della sezione Frequenze.

---

## Dopo il caricamento

1. **Il timbro** in `diagnostica/avvio-diagnostica.txt` dev'essere `1.22.1 · 659c334`.
   ⚠️ Il timbro dice **quale versione è partita**, non che il sito funzioni.
2. **La Ricerca risponde**: `/services/vsop/search`, due lettere, la riga sotto il campo deve cambiare. È il
   controllo che conta, perché passa dal server.
3. Col login, **l'editor del vSOP di un campo solo militare senza vIPI**: sotto le sezioni c'è «Settori ATC», e
   nella barra «Re-importa da IVAO».
