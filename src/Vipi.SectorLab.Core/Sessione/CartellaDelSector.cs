namespace Vipi.SectorLab.Core.Sessione;

/// <summary>
/// La cartella di un clone del sector (<c>ivao-italy/it-aurora-sector</c>), riconosciuta da quella che l'AOD sceglie
/// (carta F3 §2.2, passo 1). La forma, misurata su <c>origin/master</c> del 22 settembre 2026:
/// <code>
/// &lt;radice&gt;/changelog.md
/// &lt;radice&gt;/SectorFiles/ITALY.isc, LIBB.isc, LIMM.isc, LIPP.isc, LIRR.isc, update.ini, delete.upd
/// &lt;radice&gt;/SectorFiles/Include/IT/…          (i dati)
/// </code>
/// </summary>
public sealed class CartellaDelSector
{
    private CartellaDelSector(string radice)
    {
        Radice = radice;
        SectorFiles = Path.Combine(radice, "SectorFiles");
        CartellaIt = Path.Combine(SectorFiles, "Include", "IT");
    }

    /// <summary>La radice del clone: quella che contiene <c>SectorFiles/</c>.</summary>
    public string Radice { get; }

    /// <summary><c>&lt;radice&gt;/SectorFiles</c>: gli <c>.isc</c> e <c>update.ini</c>.</summary>
    public string SectorFiles { get; }

    /// <summary><c>&lt;radice&gt;/SectorFiles/Include/IT</c>: i dati del sector italiano.</summary>
    public string CartellaIt { get; }

    /// <summary>
    /// Riconosce la cartella del sector da quella scelta: va bene la radice del clone, ma anche <c>SectorFiles</c>,
    /// <c>Include</c>, <c>IT</c> o una cartella qualunque lì sotto — si risale finché si trova la radice. Nullo, con
    /// il motivo, se la cartella non è di un sector.
    /// </summary>
    public static CartellaDelSector? Riconosci(string scelta, out string? motivo)
    {
        ArgumentException.ThrowIfNullOrEmpty(scelta);
        motivo = null;

        var cartella = new DirectoryInfo(Path.GetFullPath(scelta));
        if (!cartella.Exists)
        {
            motivo = $"La cartella «{scelta}» non esiste.";
            return null;
        }

        for (var c = cartella; c is not null; c = c.Parent)
        {
            if (!Directory.Exists(Path.Combine(c.FullName, "SectorFiles")))
                continue;

            var trovata = new CartellaDelSector(c.FullName);
            if (!Directory.Exists(trovata.CartellaIt))
            {
                motivo = $"In «{trovata.SectorFiles}» manca la cartella Include\\IT dei dati.";
                return null;
            }
            if (Directory.GetFiles(trovata.SectorFiles, "*.isc").Length == 0)
            {
                motivo = $"In «{trovata.SectorFiles}» non c'è nessun file .isc.";
                return null;
            }
            return trovata;
        }

        motivo = $"«{scelta}» non è la cartella del sector: né lei né le cartelle sopra contengono SectorFiles.";
        return null;
    }

    /// <summary>Il percorso relativo alla radice, con le barre dritte: <c>SectorFiles/Include/IT/GEO/itgeo.geo</c>.</summary>
    public string Relativo(string percorso)
        => Path.GetRelativePath(Radice, percorso).Replace('\\', '/');

    /// <summary>Il percorso sul disco di un relativo.</summary>
    public string Assoluto(string relativo)
        => Path.GetFullPath(Path.Combine(Radice, relativo.Replace('/', Path.DirectorySeparatorChar)));
}
