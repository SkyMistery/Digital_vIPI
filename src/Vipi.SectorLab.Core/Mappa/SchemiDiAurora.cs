using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.Mappa;

/// <summary>
/// Dove stanno gli schemi di colori di Aurora e quale si usa (lotto «Subito» slice 4, D3). Lo schema è scelto una volta
/// e sta <b>fuori dal sector</b>.
/// <para>Dal 20 ottobre (committente: «ora Aurora mette tutto in AppData\Roaming\IVAO\AuroraWindows») si cercano prima
/// nella <b>cartella di Aurora</b> (<see cref="CartellaDiAuroraDiBase"/>, o quella che l'AOD sceglie), in
/// <c>ColourSchemes</c> — il nome che usa Aurora — o <c>ColorSchemes</c>; se lì non ce n'è nessuno, accanto a
/// <c>SectorFiles</c> nel clone, come nel repository del sector di prima.</para>
/// </summary>
public static class SchemiDiAurora
{
    /// <summary>Lo schema del committente (carta «file per file» §5): è la scelta di base quando c'è.</summary>
    public const string DiBase = "LIRR_RDR_V1.0.clr";

    /// <summary>Dove Aurora tiene le sue cose sul PC dell'AOD: schemi, profili, impostazioni, il sector pubblicato.</summary>
    public static string CartellaDiAuroraDiBase { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IVAO", "AuroraWindows");

    /// <summary>I nomi della cartella degli schemi: quello di Aurora e quello del repository del sector.</summary>
    public static readonly IReadOnlyList<string> NomiDellaCartella = ["ColourSchemes", "ColorSchemes"];

    /// <summary>
    /// La cartella degli schemi: la prima che ha dei <c>.clr</c> fra quelle della cartella di Aurora e quelle del clone.
    /// Se nessuna ne ha, quella dove li si aspetta (di Aurora se c'è, se no del clone), per dirlo all'AOD.
    /// </summary>
    public static string Cartella(CartellaDelSector cartella, string? cartellaDiAurora = null)
    {
        ArgumentNullException.ThrowIfNull(cartella);
        var candidate = Candidate(cartella, cartellaDiAurora).ToList();
        return candidate.FirstOrDefault(c => Directory.Exists(c) && Directory.EnumerateFiles(c, "*.clr").Any()) ?? candidate[0];
    }

    private static IEnumerable<string> Candidate(CartellaDelSector cartella, string? cartellaDiAurora)
    {
        if (!string.IsNullOrWhiteSpace(cartellaDiAurora))
        {
            foreach (string nome in NomiDellaCartella)
                yield return Path.Combine(cartellaDiAurora, nome);
        }
        foreach (string nome in NomiDellaCartella.Reverse())
            yield return Path.Combine(cartella.Radice, nome);
    }

    /// <summary>I nomi degli schemi, in ordine; vuoto se non ce n'è.</summary>
    public static IReadOnlyList<string> Disponibili(CartellaDelSector cartella, string? cartellaDiAurora = null)
    {
        string dove = Cartella(cartella, cartellaDiAurora);
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
    public static SchemaDeiColori Leggi(CartellaDelSector cartella, string nome, IWarningCollector avvisi, string? cartellaDiAurora = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(nome);
        return new ClrParser(avvisi).Parse(Path.Combine(Cartella(cartella, cartellaDiAurora), Path.GetFileName(nome)));
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
