# Vipi.MilSopLoader

Mette una **trascrizione** di SOP militare al posto giusto nel documento del campo, e dice che cosa resta
fuori. Carta: [`docs/feature/2026-08-27-vsop-militari.md`](../../docs/feature/2026-08-27-vsop-militari.md) §9.

**Gira su net8**, come `Vipi.Host` e per lo stesso motivo: l'unico provider EF che regge MariaDB è Pomelo,
che esiste solo per EF Core 8.

## Uso

```sh
# il piano, senza toccare niente
dotnet run --project tools/Vipi.MilSopLoader -- --sqlite src/Vipi.Host/vipi.db --icao LIPI

# scrive davvero
dotnet run --project tools/Vipi.MilSopLoader -- --sqlite src/Vipi.Host/vipi.db --icao LIPI --autore 704798 --apply

# in produzione
dotnet run --project tools/Vipi.MilSopLoader -- --mysql "<connessione>" --icao LIPI --autore <vid> --apply
```

Senza `--apply` non scrive niente: scrivere dentro il documento di qualcun altro è la cosa che va guardata
prima di farla. Esce con **2** se una chiave della trascrizione non esiste nel profilo — quel contenuto non
finirebbe da nessuna parte, e in mezzo a trenta righe di rendiconto non lo noterebbe nessuno.

## ⚠️ Non è un lettore di PDF, e non deve diventarlo

Il contenuto è trascritto **a mano** per due ragioni che nessun parser risolve:

- il documento nasce in **italiano** (carta §1d) e i SOP sono in inglese: è una traduzione redazionale, non
  una conversione;
- metà di ciò che conta nei quindici SOP sono **figure** — flussi di rullaggio, posizioni di armamento,
  circuiti VFR — e vanno estratte e caricate come immagini.

Quello che questo strumento fa è mettere una trascrizione al posto giusto senza sbagliare chiave, e
distinguere i motivi per cui una sezione resta vuota.

## Che cosa garantisce

- **Idempotente**: non ripassa sopra a una sezione che ha già contenuto, si ferma e lo dice. Il blocco
  *segnaposto* delle sezioni rese dalla pagina non conta come contenuto (nasce vuoto alla creazione).
- **Distingue i quattro motivi** di una sezione vuota: contenitore, scheda disegnata dalla pagina, figura
  non ancora riportata, l'originale non ce l'ha. I primi due li dice il **catalogo**, non un elenco scritto
  qui dentro.
- **Nasconde** le sezioni che su quel campo non esistono (`qra` e `lowlevel` su Rivolto): una sezione vuota
  lasciata in vista dice al lettore «qui manca qualcosa», che è falso.
- Il documento resta in **bozza**: diventa pubblico solo con una release AIRAC, premuta da una persona.

## Aggiungere un campo

Un file come `SopLipi.cs` e una riga nello `switch` di `Program.cs`. La parte che conta non è scriverlo — è
**rileggerlo con qualcuno che conosca il campo**. I PDF di partenza non stanno nel repo.
