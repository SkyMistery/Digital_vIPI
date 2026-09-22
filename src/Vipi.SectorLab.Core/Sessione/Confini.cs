namespace Vipi.SectorLab.Core.Sessione;

/// <summary>
/// I confini dell'app (carta madre §1, carta F3 §2.4 passo 1): che cosa il Lab può SCRIVERE nel clone del sector.
/// Solo i dati italiani, <c>update.ini</c> e <c>changelog.md</c>; mai <c>Aurora.exe</c>, le DLL, i suoni, gli
/// <c>.isc</c>, l'updater. Il controllo sta qui e non nella pagina: nessun percorso fuori da qui si scrive, qualunque
/// cosa chieda l'interfaccia.
/// <para>⚠️ La carta madre diceva <c>update.ini</c> senza cartella: sul master sta in <c>SectorFiles/</c>, mentre
/// <c>changelog.md</c> sta nella radice (misurato il 22 settembre 2026).</para>
/// </summary>
public static class Confini
{
    /// <summary>Vero se <paramref name="percorso"/> (assoluto o relativo alla radice) è dentro i confini.</summary>
    public static bool Scrivibile(CartellaDelSector sector, string percorso)
    {
        ArgumentNullException.ThrowIfNull(sector);
        ArgumentException.ThrowIfNullOrEmpty(percorso);

        // Il percorso si normalizza PRIMA di confrontarlo: «IT/../../Aurora.exe» comincia con IT ma non ci resta.
        string pieno = Path.GetFullPath(Path.IsPathRooted(percorso) ? percorso : Path.Combine(sector.Radice, percorso));
        var confronto = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        string it = sector.CartellaIt.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (pieno.StartsWith(it, confronto) && pieno.Length > it.Length)
            return true;

        return string.Equals(pieno, Path.Combine(sector.SectorFiles, "update.ini"), confronto)
               || string.Equals(pieno, Path.Combine(sector.Radice, "changelog.md"), confronto);
    }

    /// <summary>Come <see cref="Scrivibile"/>, ma lancia: è la guardia che il salvataggio chiama su ogni file.</summary>
    public static void Pretendi(CartellaDelSector sector, string percorso)
    {
        if (!Scrivibile(sector, percorso))
            throw new UnauthorizedAccessException($"«{percorso}» è fuori dai confini del Lab: si scrive solo in SectorFiles/Include/IT, SectorFiles/update.ini e changelog.md.");
    }
}
