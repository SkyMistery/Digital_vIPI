using Xunit;

namespace Vipi.Assets.Tests;

/// <summary>
/// <b>L'indice dice «tutti i documenti», e deve essere vero.</b>
///
/// <para><c>docs/index.md</c> si apre con «mappa di <b>tutti</b> i documenti del progetto» e propone un
/// ordine di lettura per chi arriva senza contesto. Al 7 settembre 2026 ne mancavano <b>44 su 166</b>, e
/// trentatré erano carte di funzionalità — cioè il posto dove sta scritto <i>perché</i> una cosa è fatta
/// così. Chi arrivava dall'indice non sapeva che esistessero (revisione del 6 settembre 2026, R-030).</para>
///
/// <para>⚠️ Il difetto non era l'elenco incompleto: era che si <b>dichiarasse completo</b>. È la stessa
/// specie di R-012 — la «lista migrazioni autoritativa» ferma all'85ª di 114 — e ha lo stesso rimedio: o
/// l'elenco lo genera un comando, o non promette di essere tutto. Qui lo genera
/// <c>tools/indice-doc.py</c>, e questo test è ciò che rende la promessa mantenibile.</para>
/// </summary>
public class IndiceDeiDocumentiTests
{
    [Fact]
    public void Ogni_carta_di_docs_e_citata_nell_indice()
    {
        var docs = Path.Combine(Radice(), "docs");
        var indice = File.ReadAllText(Path.Combine(docs, "index.md"));

        var assenti = Directory.EnumerateFiles(docs, "*.md", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(docs, f).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(rel => rel != "index.md")
            .Where(rel => !indice.Contains(rel, StringComparison.OrdinalIgnoreCase))
            .OrderBy(rel => rel, StringComparer.Ordinal)
            .ToList();

        Assert.True(assenti.Count == 0,
            $"Carte sotto docs/ che l'indice non nomina ({assenti.Count}):\n  " +
            string.Join("\n  ", assenti.Take(20)) +
            "\n\nL'indice promette «tutti i documenti». Si rigenera la sezione in fondo con " +
            "`python tools/indice-doc.py`; le sezioni curate sopra restano scritte a mano, perché quelle " +
            "dicono cosa LEGGERE e questa dice cosa C'È.");
    }

    /// <summary>
    /// ⚠️ E il rovescio: l'indice non deve nominare carte che non esistono più. Un link morto in una mappa
    /// è peggio di una casella vuota — manda a cercare qualcosa, invece di dire che non c'è.
    /// </summary>
    [Fact]
    public void L_indice_non_nomina_carte_che_non_esistono()
    {
        var docs = Path.Combine(Radice(), "docs");
        var indice = File.ReadAllText(Path.Combine(docs, "index.md"));

        var citate = System.Text.RegularExpressions.Regex
            .Matches(indice, @"\]\((?<p>[A-Za-z0-9._/\-]+\.md)\)")
            .Select(m => m.Groups["p"].Value)
            .Where(p => !p.StartsWith("../", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        var fantasmi = citate.Where(p => !File.Exists(Path.Combine(docs, p))).ToList();

        Assert.True(fantasmi.Count == 0,
            $"L'indice nomina carte che non esistono ({fantasmi.Count}):\n  " +
            string.Join("\n  ", fantasmi.Take(20)));
    }

    private static string Radice()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Vipi.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException($"radice del repository non trovata da {AppContext.BaseDirectory}");
    }
}
