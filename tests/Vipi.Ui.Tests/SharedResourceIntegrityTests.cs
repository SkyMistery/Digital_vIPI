using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Vipi.Ui.Tests;

/// <summary>
/// Guardie sui due file di risorse condivise. Nascono da un guasto reale: 14 chiavi <c>Country_*</c> erano
/// finite due volte in ciascun file, il compilatore di risorse le scartava con altrettanti <c>MSB3568</c>, e
/// il job CI <c>build-net8</c> — che compila con <c>-warnaserror</c> — non passava più. La suite era verde
/// lo stesso, perché <c>dotnet test</c> gira senza quel flag: <b>1391 test verdi e build di produzione rotta
/// sono compatibili</b>, ed è la ragione per cui queste tre guardie stanno qui e non nella CI.
///
/// <para>Leggono i <c>.resx</c> dal disco, non le risorse compilate: un duplicato, per definizione, nella
/// risorsa compilata non c'è più — l'ha già buttato via chi compila.</para>
/// </summary>
public sealed class SharedResourceIntegrityTests
{
    private readonly ITestOutputHelper _out;

    public SharedResourceIntegrityTests(ITestOutputHelper output) => _out = output;

    private const string PercorsoIt = "src/Vipi.Ui/Resources/SharedResource.resx";
    private const string PercorsoEn = "src/Vipi.Ui/Resources/SharedResource.en.resx";

    [Theory]
    [InlineData(PercorsoIt)]
    [InlineData(PercorsoEn)]
    public void Nessuna_chiave_ripetuta(string percorsoRelativo)
    {
        var chiavi = Chiavi(percorsoRelativo);
        var doppie = chiavi.GroupBy(k => k, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} ×{g.Count()}")
            .ToList();

        foreach (var d in doppie) _out.WriteLine(d);

        Assert.True(doppie.Count == 0,
            $"{percorsoRelativo}: {doppie.Count} chiavi ripetute. Il compilatore di risorse le scarta con " +
            "MSB3568 e con -warnaserror la build di produzione fallisce; in più quale delle due vinca " +
            "dipende dall'ordine nel file, cioè da niente di dichiarato.\n  " + string.Join("\n  ", doppie));
    }

    [Fact]
    public void Italiano_e_inglese_hanno_le_stesse_chiavi()
    {
        var it = Chiavi(PercorsoIt).ToHashSet(StringComparer.Ordinal);
        var en = Chiavi(PercorsoEn).ToHashSet(StringComparer.Ordinal);

        var soloIt = it.Except(en).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var soloEn = en.Except(it).OrderBy(k => k, StringComparer.Ordinal).ToList();

        Assert.True(soloIt.Count == 0 && soloEn.Count == 0,
            "I due file di risorse non combaciano. Una chiave che esiste solo in italiano non è un errore " +
            "di compilazione: in inglese ricade sul valore italiano, in silenzio.\n" +
            $"  solo in it ({soloIt.Count}): {string.Join(", ", soloIt)}\n" +
            $"  solo in en ({soloEn.Count}): {string.Join(", ", soloEn)}");
    }

    /// <summary>
    /// Ogni <c>L["Chiave"]</c> scritto nei sorgenti deve esistere nelle risorse: una chiave assente non
    /// lancia — il localizzatore restituisce il NOME della chiave, che finisce a schermo.
    /// </summary>
    [Fact]
    public void Ogni_chiave_usata_nel_codice_esiste_nelle_risorse()
    {
        var definite = Chiavi(PercorsoIt).ToHashSet(StringComparer.Ordinal);
        var radice = RadiceDelRepo();

        var usate = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(radice, "src", "Vipi.Ui"), "*.*", SearchOption.AllDirectories)
                     .Where(f => (f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) ||
                                  f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) &&
                                 !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                                 !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"\bL\[""([A-Za-z0-9_]+)""(?<coda>\s*\+)?"))
            {
                // `L["Country_" + code]`: la chiave si compone a runtime, qui non è verificabile.
                if (m.Groups["coda"].Success) continue;
                usate.TryAdd(m.Groups[1].Value, Path.GetRelativePath(radice, file));
            }
        }

        var mancanti = usate.Where(kv => !definite.Contains(kv.Key))
            .Select(kv => $"{kv.Key}  ({kv.Value})")
            .ToList();

        foreach (var m in mancanti) _out.WriteLine(m);

        Assert.True(mancanti.Count == 0,
            $"{mancanti.Count} chiavi usate nel codice e assenti da SharedResource.resx: a schermo compare " +
            "il nome della chiave.\n  " + string.Join("\n  ", mancanti));
    }

    private static IReadOnlyList<string> Chiavi(string percorsoRelativo)
    {
        var percorso = Path.Combine(RadiceDelRepo(), percorsoRelativo.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(percorso), "File di risorse non trovato: " + percorso);

        // I `<data>` di intestazione (mimetype/version/reader/writer) sono `<resheader>`: non entrano.
        return XDocument.Load(percorso).Root!.Elements("data")
            .Select(e => e.Attribute("name")?.Value)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .ToList();
    }

    /// <summary>Risale dalla cartella dell'assembly fino alla soluzione: fallisce forte se non la trova.</summary>
    private static string RadiceDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Vipi.slnx"))) dir = dir.Parent;
        Assert.True(dir is not null, "Vipi.slnx non trovata risalendo da " + AppContext.BaseDirectory);
        return dir!.FullName;
    }
}
