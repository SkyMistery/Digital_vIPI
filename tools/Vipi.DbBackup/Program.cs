using Vipi.Infrastructure.DatabaseCopy;

// -----------------------------------------------------------------------------------------------
// Vipi.DbBackup — la copia di sicurezza del database (§A47, docs/feature/2026-09-16-copia-del-database.md)
//
//   dotnet run --project tools/Vipi.DbBackup -- verifica <file.sql.gz>
//   dotnet run --project tools/Vipi.DbBackup -- scrivi "<connessione MariaDB>" <file.sql.gz>
//
// `verifica` non tocca nessun database: dice se il file scaricato da Diagnostica è intero e non ritoccato.
// `scrivi` fa la stessa copia del sito, senza cancello né registro: serve alla CI per l'andata e ritorno
// contro un MariaDB vero, e a chi ha accesso diretto al database.
// -----------------------------------------------------------------------------------------------

const string Uso = """
    Uso:
      verifica <file.sql.gz>                      controlla che la copia sia intera (nessun database)
      scrivi "<connessione MariaDB>" <file.sql.gz>  scrive una copia come quella di Diagnostica
    """;

if (args.Length == 2 && args[0] == "verifica")
{
    if (!File.Exists(args[1])) { Console.Error.WriteLine($"File non trovato: {args[1]}"); return 2; }

    var v = await SqlDumpVerifier.VerifyFileAsync(args[1]);
    Console.WriteLine($"File .............. {Path.GetFileName(args[1])}");
    Console.WriteLine($"Versione del sito . {v.SiteVersion ?? "?"}");
    Console.WriteLine($"Creata ............ {v.CreatedUtc ?? "?"}");
    Console.WriteLine($"Contenuto ......... {v.Found.Tables} tabelle, {v.Found.Rows} righe, {v.Found.Bytes:N0} byte di SQL");
    Console.WriteLine($"sha256 ............ {v.Found.Sha256}");
    if (v.Ok)
    {
        Console.WriteLine("ESITO: la copia è INTERA e l'impronta torna.");
        return 0;
    }
    Console.Error.WriteLine($"ESITO: NON VALIDA — {v.Problem}");
    return 1;
}

if (args.Length == 3 && args[0] == "scrivi")
{
    var source = new MySqlDumpSource(args[1]);
    await using var snapshot = await source.OpenAsync();
    await using var file = File.Create(args[2]);
    var s = await DatabaseBackupService.WriteGzipAsync(snapshot, file, "Vipi.DbBackup", DateTime.UtcNow);
    Console.WriteLine($"Scritta {args[2]}: {s.Tables} tabelle, {s.Rows} righe, sha256 {s.Sha256}");
    return 0;
}

Console.Error.WriteLine(Uso);
return 2;
