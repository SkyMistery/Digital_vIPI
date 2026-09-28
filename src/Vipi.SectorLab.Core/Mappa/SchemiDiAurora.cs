using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.Mappa;

/// <summary>
/// Dove stanno gli schemi di colori di Aurora e quale si usa (lotto «Subito» slice 4, D3). Lo schema è scelto una volta
/// e sta <b>fuori dal sector</b>: nella cartella <c>ColorSchemes</c> accanto a <c>SectorFiles</c>, come nel repository
/// del sector e in un'installazione di Aurora.
/// </summary>
public static class SchemiDiAurora
{
    /// <summary>Lo schema del committente (carta «file per file» §5): è la scelta di base quando c'è.</summary>
    public const string DiBase = "LIRR_RDR_V1.0.clr";

    /// <summary>La cartella degli schemi di un clone.</summary>
    public static string Cartella(CartellaDelSector cartella)
    {
        ArgumentNullException.ThrowIfNull(cartella);
        return Path.Combine(cartella.Radice, "ColorSchemes");
    }

    /// <summary>I nomi degli schemi del clone, in ordine; vuoto se la cartella non c'è.</summary>
    public static IReadOnlyList<string> Disponibili(CartellaDelSector cartella)
    {
        string dove = Cartella(cartella);
        return Directory.Exists(dove)
            ? [.. Directory.GetFiles(dove, "*.clr").Select(Path.GetFileName).OfType<string>().Order(StringComparer.OrdinalIgnoreCase)]
            : [];
    }

    /// <summary>
    /// Lo schema da usare: quello ricordato se c'è ancora, se no quello di base, se no il primo. Null se non ce n'è.
    /// </summary>
    public static string? Scegli(IReadOnlyList<string> disponibili, string? ricordato)
    {
        ArgumentNullException.ThrowIfNull(disponibili);
        return disponibili.FirstOrDefault(d => string.Equals(d, ricordato, StringComparison.OrdinalIgnoreCase))
               ?? disponibili.FirstOrDefault(d => string.Equals(d, DiBase, StringComparison.OrdinalIgnoreCase))
               ?? disponibili.FirstOrDefault();
    }

    /// <summary>Legge uno schema della cartella. Gli avvisi della lettura finiscono in <paramref name="avvisi"/>.</summary>
    public static SchemaDeiColori Leggi(CartellaDelSector cartella, string nome, IWarningCollector avvisi)
    {
        ArgumentException.ThrowIfNullOrEmpty(nome);
        return new ClrParser(avvisi).Parse(Path.Combine(Cartella(cartella), Path.GetFileName(nome)));
    }

    /// <summary>
    /// I nomi di <c>[DEFINE]</c> del master: i <c>.def</c> che carica, nell'ordine dei carichi (vince l'ultimo, come
    /// dentro un <c>.def</c>). Si leggono dal disco: il Lab tiene i <c>.def</c> come testo.
    /// </summary>
    public static ColorPalette Definiti(SessioneAperta sessione, CatalogoDeiPunti? master, IWarningCollector avvisi)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        var palette = new ColorPalette();
        if (master is null)
            return palette;

        foreach (string relativo in master.FileCaricati.Where(f => f.EndsWith(".def", StringComparison.OrdinalIgnoreCase)))
        {
            string percorso = sessione.Cartella.Assoluto(relativo);
            if (!File.Exists(percorso))
                continue;
            foreach (var voce in new DefParser(avvisi).Parse(percorso).Entries.Values)
                palette.Add(voce);
        }

        return palette;
    }
}
