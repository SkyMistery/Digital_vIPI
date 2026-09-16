using Vipi.Infrastructure.DatabaseCopy;

// -----------------------------------------------------------------------------------------------
// Vipi.DbBackup — la copia di sicurezza del database (§A47, docs/feature/2026-09-16-copia-del-database.md)
//
//   dotnet run --project tools/Vipi.DbBackup -- verifica <file.sql.gz>
//   dotnet run --project tools/Vipi.DbBackup -- scrivi "<connessione MariaDB>" <file.sql.gz>
//
// `verifica` non tocca nessun database: dice se il file scaricato da Diagnostica è intero e non rovinato.
// `scrivi` fa la stessa copia del sito, senza cancello né registro: serve alla CI per l'andata e ritorno
// contro un MariaDB vero, e a chi ha accesso diretto al database.
// -----------------------------------------------------------------------------------------------

const string Uso = """
    Uso:
      verifica <file.sql.gz>                        controlla che la copia sia intera (nessun database)
      scrivi "<connessione MariaDB>" <file.sql.gz>  scrive una copia come quella di Diagnostica
    """;

// Il max_allowed_packet di default di MariaDB, al server e al client.
const long PacchettoDiDefault = 16L * 1024 * 1024;

if (args.Length == 2 && args[0] == "verifica")
{
    if (!File.Exists(args[1])) { Console.Error.WriteLine($"File non trovato: {args[1]}"); return 2; }

    var v = await SqlDumpVerifier.VerifyFileAsync(args[1]);
    Console.WriteLine($"File .............. {Path.GetFileName(args[1])}");
    Console.WriteLine($"Versione del sito . {v.SiteVersion ?? "?"}");
    Console.WriteLine($"Creata ............ {v.CreatedUtc ?? "?"}");
    Console.WriteLine($"Ultima migrazione . {v.LastMigration ?? "?"}");
    Console.WriteLine($"Contenuto ......... {v.Found.Tables} tabelle, {v.Found.Rows} righe, {v.Found.Bytes:N0} byte di SQL");
    Console.WriteLine($"Istruzione max .... {v.Found.LongestStatementBytes:N0} byte");
    Console.WriteLine($"sha256 ............ {v.Found.Sha256}");
    if (!v.Ok)
    {
        Console.Error.WriteLine($"ESITO: NON VALIDA — {v.Problem}");
        Console.Error.WriteLine("Non ripristinarla.");
        return 1;
    }

    Console.WriteLine("ESITO: la copia è INTERA e l'impronta torna.");
    Console.WriteLine("(L'impronta non è una firma: per escludere modifiche volute, confrontala con quella del registro di audit.)");
    if (v.Found.LongestStatementBytes > PacchettoDiDefault)
        Console.WriteLine($"⚠️ C'è un'istruzione più lunga di 16 MB: il server di ripristino deve avere max_allowed_packet ≥ {v.Found.LongestStatementBytes:N0} byte.");
    Console.WriteLine("Ripristino: in un database VUOTO, col sito alla stessa versione (vedi «Ultima migrazione»):");
    Console.WriteLine("  set -o pipefail; gunzip -c <file> | mariadb --max-allowed-packet=1G -u <utente> -p <database>");
    return 0;
}

if (args.Length == 3 && args[0] == "scrivi")
{
    var destinazione = args[2];
    try
    {
        var source = new MySqlDumpSource(args[1]);
        await using var snapshot = await source.OpenAsync();
        Vipi.Application.Diagnostics.DatabaseBackupSummary r;
        await using (var file = File.Create(destinazione))
            r = await DatabaseBackupService.WriteGzipAsync(snapshot, file, "Vipi.DbBackup", DateTime.UtcNow);
        Console.WriteLine($"Scritta {destinazione}: {r.Tables} tabelle, {r.Rows} righe, istruzione max {r.LongestStatementBytes:N0} byte, sha256 {r.Sha256}");
        return 0;
    }
    catch (Exception e)
    {
        // Un file a metà con un nome da copia buona è peggio di nessun file.
        if (File.Exists(destinazione)) File.Delete(destinazione);
        Console.Error.WriteLine($"Copia NON scritta: {e.Message}");
        return 1;
    }
}

Console.Error.WriteLine(Uso);
return 2;

