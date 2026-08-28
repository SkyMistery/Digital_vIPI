namespace Vipi.MilSopLoader;

/// <param name="Sqlite">Percorso del file SQLite; null se si lavora su MySQL/MariaDB.</param>
/// <param name="MySql">Stringa di connessione MySQL/MariaDB; null se si lavora su SQLite.</param>
/// <param name="Icao">Quale SOP caricare. Oggi ce n'è uno trascritto.</param>
/// <param name="Autore">Il VID a cui attribuire i blocchi scritti.</param>
/// <param name="Applica">Senza questo lo strumento stampa il piano e non tocca niente.</param>
public sealed record Opzioni(string? Sqlite, string? MySql, string Icao, int Autore, bool Applica);

/// <summary>
/// La riga di comando. La prova a vuoto è il <b>default</b>: scrivere dentro il documento di qualcun altro
/// è la cosa che va guardata prima di farla.
/// </summary>
public static class Argomenti
{
    public const string Uso = """
        Uso:
          dotnet run --project tools/Vipi.MilSopLoader -- --sqlite <file.db> [--icao LIPI] [--autore <vid>] [--apply]
          dotnet run --project tools/Vipi.MilSopLoader -- --mysql "<connessione>" [--icao LIPI] [--autore <vid>] [--apply]

        Senza --apply stampa il piano e non scrive niente.

        Carica il contenuto di un SOP militare TRASCRITTO nelle sezioni del documento del campo,
        creando il documento se manca. Non tocca le sezioni che hanno già contenuto.
        """;

    public static bool Prova(string[] args, out Opzioni opzioni, out string? errore)
    {
        opzioni = new Opzioni(null, null, "LIPI", 0, false);
        errore = null;

        string? sqlite = null, mysql = null, icao = "LIPI";
        var autore = 0;
        var applica = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--sqlite" when i + 1 < args.Length: sqlite = args[++i]; break;
                case "--mysql" when i + 1 < args.Length: mysql = args[++i]; break;
                case "--icao" when i + 1 < args.Length: icao = args[++i].Trim().ToUpperInvariant(); break;
                case "--autore" when i + 1 < args.Length && int.TryParse(args[i + 1], out var v):
                    autore = v; i++; break;
                case "--apply": applica = true; break;
                default:
                    errore = $"argomento non riconosciuto: {args[i]}";
                    return false;
            }
        }

        if (sqlite is null && mysql is null) { errore = "indica --sqlite o --mysql."; return false; }
        if (sqlite is not null && mysql is not null) { errore = "indica UNO fra --sqlite e --mysql."; return false; }

        opzioni = new Opzioni(sqlite, mysql, icao!, autore, applica);
        return true;
    }
}
