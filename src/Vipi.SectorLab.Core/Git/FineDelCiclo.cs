using System.Globalization;
using System.Text;

namespace Vipi.SectorLab.Core.Git;

/// <summary>
/// La fine del ciclo nel changelog (carta F4, slice 7; «file per file» C3): <c>changelog.md</c> si archivia in
/// <c>CHANGELOG/&lt;ciclo&gt;.txt</c> così com'è, poi riparte vuoto con l'intestazione del ciclo nuovo (ciclo e data dal
/// calendario AIRAC di vIPI), la stessa cornice <c>#####</c> e le sezioni con <c>*NIL</c>. Si fa nel ramo del ciclo nuovo,
/// quando il changelog dice ancora il ciclo di prima (il controllo C6 lo segnala).
/// </summary>
public static class FineDelCiclo
{
    /// <summary>La cartella dell'archivio, dalla radice del repo.</summary>
    public const string Archivio = "SectorFiles/Include/IT/CHANGELOG/";

    /// <summary>
    /// Archivia il changelog del ciclo di adesso e apre quello del ciclo <paramref name="cicloNuovo"/> (in vigore dal
    /// <paramref name="dal"/>). Torna i file scritti, o il perché in <paramref name="perche"/>. Un archivio che c'è già
    /// (succede: 2610.txt era stato scritto prima della fine del ciclo) si sostituisce, e il diff lo mostra.
    /// </summary>
    public static IReadOnlyList<string> Apri(string radice, string cicloNuovo, DateOnly dal, out string? perche)
    {
        ArgumentException.ThrowIfNullOrEmpty(radice);
        perche = null;
        string file = Path.Combine(radice, Changelog.Percorso.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(file))
        {
            perche = $"Manca {Changelog.Percorso}.";
            return [];
        }

        byte[] grezzi = File.ReadAllBytes(file);
        bool bom = grezzi.Length >= 3 && grezzi[0] == 0xEF && grezzi[1] == 0xBB && grezzi[2] == 0xBF;
        var utf8 = new UTF8Encoding(false);
        string testo = utf8.GetString(grezzi, bom ? 3 : 0, grezzi.Length - (bom ? 3 : 0));
        string capo = testo.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var righe = testo.Split(capo);
        if (Changelog.Intestazione(righe) is not { } vecchio)
        {
            perche = "La prima riga del changelog non è l'intestazione di un ciclo: lo archivia chi sa di che ciclo è.";
            return [];
        }

        if (vecchio.Ciclo == cicloNuovo)
        {
            perche = $"Il changelog dice già A{cicloNuovo}.";
            return [];
        }

        // L'archivio: il file così com'è, byte per byte.
        string archiviato = Archivio + vecchio.Ciclo + ".txt";
        string dove = Path.Combine(radice, archiviato.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(dove)!);
        File.WriteAllBytes(dove, grezzi);

        // Il nuovo: l'intestazione, la cornice di prima (le righe fra l'intestazione e la prima sezione), le sezioni vuote.
        int primaSezione = Array.FindIndex(righe, 1, r => r.TrimStart().StartsWith("**", StringComparison.Ordinal));
        var nuovo = new List<string>
        {
            $"**AIRAC A{cicloNuovo} IN VIGORE DAL {dal.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}",
        };
        nuovo.AddRange(primaSezione > 1 ? righe[1..primaSezione] : [""]);
        foreach (var (_, titolo) in Changelog.Sezioni)
            nuovo.AddRange(["**" + titolo, "*NIL", ""]);
        string scritto = string.Join(capo, nuovo) + (testo.EndsWith('\n') ? capo : "");
        File.WriteAllBytes(file, [.. (bom ? (byte[])[0xEF, 0xBB, 0xBF] : []), .. utf8.GetBytes(scritto)]);
        return [archiviato, Changelog.Percorso];
    }
}

/// <summary>
/// I file tolti dal sector (carta F4, slice 7; «file per file» C4): la loro riga in <c>SectorFiles/delete.upd</c>, così i
/// client di Aurora li cancellano all'aggiornamento (<c>IT\cartella\file</c>, una per riga), e via le righe <c>F;</c> che li
/// caricano dagli <c>.isc</c> (es. A9: <c>test.artcc</c> e la sua riga di <c>ITALY.isc</c>).
/// <para>Gli <c>.isc</c> stanno in <c>SectorFiles/</c>, fuori da <c>Include/IT</c>: per questo gesto entrano nei confini
/// (<see cref="Registrazione.DentroIConfini"/>), come aveva deciso C4.</para>
/// </summary>
public static class Cancellazioni
{
    /// <summary>La riga di <c>delete.upd</c> di un file tolto: <c>IT\DYNAMIC_SEC\lipp_ne2_ctr.tfl</c>.</summary>
    public static string RigaDiDelete(string percorso)
    {
        ArgumentException.ThrowIfNullOrEmpty(percorso);
        const string Da = "SectorFiles/Include/";
        return (percorso.StartsWith(Da, StringComparison.Ordinal) ? percorso[Da.Length..] : percorso).Replace('/', '\\');
    }

    /// <summary>
    /// Scrive le righe di <c>delete.upd</c> dei file tolti (una volta sola ciascuna) e toglie dagli <c>.isc</c> le righe
    /// <c>F;</c> che li caricano. Torna i file cambiati (dalla radice del repo).
    /// </summary>
    public static IReadOnlyList<string> Applica(string radice, IReadOnlyList<string> tolti)
    {
        ArgumentException.ThrowIfNullOrEmpty(radice);
        ArgumentNullException.ThrowIfNull(tolti);
        var cambiati = new List<string>();
        var sectorTolti = tolti.Where(t => t.StartsWith(Registrazione.CartellaDelSector, StringComparison.Ordinal)).ToList();
        if (sectorTolti.Count == 0)
            return cambiati;

        string delete = Path.Combine(radice, Registrazione.FileDaCancellare.Replace('/', Path.DirectorySeparatorChar));
        var (righe, capo, finale) = Leggi(delete);
        var nuove = sectorTolti.Select(RigaDiDelete).Where(r => !righe.Contains(r, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (nuove.Count > 0)
        {
            Scrivi(delete, [.. righe, .. nuove], capo, finale || righe.Count == 0);
            cambiati.Add(Registrazione.FileDaCancellare);
        }

        // Nell'.isc il file si cita dalla cartella IT: «F;OTHER\lirr.frq», «F;test.artcc».
        var citazioni = sectorTolti.Select(t => t[Registrazione.CartellaDelSector.Length..].Replace('/', '\\')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string isc in Directory.EnumerateFiles(Path.Combine(radice, "SectorFiles"), "*.isc").Order(StringComparer.Ordinal))
        {
            var (suo, capoIsc, finaleIsc) = Leggi(isc);
            var resta = suo.Where(r => !(r.TrimStart().StartsWith("F;", StringComparison.OrdinalIgnoreCase) && citazioni.Contains(r.Trim()[2..].Trim()))).ToList();
            if (resta.Count == suo.Count)
                continue;
            Scrivi(isc, resta, capoIsc, finaleIsc);
            cambiati.Add("SectorFiles/" + Path.GetFileName(isc));
        }

        return cambiati;
    }

    private static (List<string> Righe, string Capo, bool Finale) Leggi(string file)
    {
        if (!File.Exists(file))
            return ([], "\r\n", true);
        string testo = File.ReadAllText(file);
        string capo = testo.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        bool finale = testo.EndsWith('\n');
        var righe = testo.Split(capo).ToList();
        if (finale)
            righe.RemoveAt(righe.Count - 1);
        return (righe, capo, finale);
    }

    private static void Scrivi(string file, IReadOnlyList<string> righe, string capo, bool finale)
        => File.WriteAllText(file, string.Join(capo, righe) + (finale ? capo : ""), new UTF8Encoding(false));
}
