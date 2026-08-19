namespace Vipi.AgreementsToSections;

/// <summary>Dove lavorare, e se scrivere davvero.</summary>
/// <param name="Sqlite">Percorso del file SQLite; null se si lavora su MySQL/MariaDB.</param>
/// <param name="MySql">Stringa di connessione MySQL/MariaDB; null se si lavora su SQLite.</param>
/// <param name="Applica">Senza questo il tool stampa il piano e non tocca niente.</param>
public sealed record Opzioni(string? Sqlite, string? MySql, bool Applica);

/// <summary>
/// La riga di comando. Due flag e basta: <b>dove</b> e <b>se scrivere</b>.
/// <para>⚠️ La prova a vuoto è il <b>default</b>, non un'opzione: questa conversione fonde quaranta accordi in
/// diciassette e non è invertibile riga per riga, quindi il gesto normale dev'essere quello che si può
/// sbagliare senza conseguenze.</para>
/// </summary>
public static class Argomenti
{
    public const string Uso = """
        Uso:
          dotnet run --project tools/Vipi.AgreementsToSections -- --sqlite <file.db> [--apply]
          dotnet run --project tools/Vipi.AgreementsToSections -- --mysql "<connessione>" [--apply]

        Senza --apply stampa il piano e non scrive niente.

        Prima: migrazione AgreementSectionsAdditive.
        Dopo:  migrazione AgreementSectionsFinalize.
        """;

    public static bool Prova(string[] args, out Opzioni opzioni, out string? errore)
    {
        opzioni = new Opzioni(null, null, false);
        errore = null;

        string? sqlite = null, mysql = null;
        var applica = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--sqlite" when i + 1 < args.Length:
                    sqlite = args[++i];
                    break;
                case "--mysql" when i + 1 < args.Length:
                    mysql = args[++i];
                    break;
                case "--apply":
                    applica = true;
                    break;
                default:
                    errore = $"argomento non riconosciuto: {args[i]}";
                    return false;
            }
        }

        if (sqlite is null && mysql is null)
        {
            errore = "indica --sqlite o --mysql.";
            return false;
        }
        if (sqlite is not null && mysql is not null)
        {
            errore = "--sqlite e --mysql si escludono: un archivio per volta.";
            return false;
        }

        opzioni = new Opzioni(sqlite, mysql, applica);
        return true;
    }
}
