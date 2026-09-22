namespace Vipi.SectorLab.Tests;

/// <summary>
/// Un clone del sector in miniatura, in una cartella temporanea, con la forma vera (carta F3, <c>CartellaDelSector</c>):
/// i dati sono i campioni del motore (<c>tests/Vipi.Sectorfile.Tests/Campioni</c>, file veri del master), e intorno
/// gli <c>.isc</c>, <c>update.ini</c>, <c>changelog.md</c> e un <c>Aurora.exe</c> finto — quello che il Lab non deve
/// mai scrivere.
/// </summary>
internal sealed class AlberoDiProva : IDisposable
{
    public AlberoDiProva()
    {
        Radice = Path.Combine(Path.GetTempPath(), "sectorlab-" + Guid.NewGuid().ToString("N"));
        CartellaIt = Path.Combine(Radice, "SectorFiles", "Include", "IT");

        foreach (string campione in Directory.EnumerateFiles(Campioni, "*", SearchOption.AllDirectories))
        {
            string relativo = Path.GetRelativePath(Campioni, campione);
            if (relativo == ".gitattributes")
                continue;
            string destinazione = Path.Combine(CartellaIt, relativo);
            Directory.CreateDirectory(Path.GetDirectoryName(destinazione)!);
            File.Copy(campione, destinazione);
        }

        Scrivi("SectorFiles/ITALY.isc", "[INFO]\r\nIT\r\n");
        Scrivi("SectorFiles/LIRR.isc", "[INFO]\r\nIT\r\n");
        Scrivi("SectorFiles/update.ini", "[Update]\r\n");
        Scrivi("changelog.md", "# Changelog\r\n");
        Scrivi("Aurora.exe", "MZ");
    }

    public string Radice { get; }

    public string CartellaIt { get; }

    /// <summary>I campioni veri del motore, cercati risalendo fino alla radice del repo (dove sta Vipi.slnx).</summary>
    public static string Campioni { get; } = TrovaCampioni();

    public string Percorso(string relativo) => Path.Combine(Radice, relativo.Replace('/', Path.DirectorySeparatorChar));

    public void Scrivi(string relativo, string testo)
    {
        string percorso = Percorso(relativo);
        Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
        File.WriteAllText(percorso, testo);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Radice, recursive: true);
        }
        catch (IOException)
        {
            // Una cartella temporanea rimasta non fa cadere nessun test.
        }
    }

    private static string TrovaCampioni()
    {
        for (var c = new DirectoryInfo(AppContext.BaseDirectory); c is not null; c = c.Parent)
        {
            if (File.Exists(Path.Combine(c.FullName, "Vipi.slnx")))
            {
                string campioni = Path.Combine(c.FullName, "tests", "Vipi.Sectorfile.Tests", "Campioni");
                return Directory.Exists(campioni)
                    ? campioni
                    : throw new DirectoryNotFoundException($"Mancano i campioni del motore: {campioni}");
            }
        }
        throw new DirectoryNotFoundException("Radice del repo (Vipi.slnx) non trovata sopra l'assieme dei test.");
    }
}
