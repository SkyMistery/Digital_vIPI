using System.Text.RegularExpressions;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Riordino D6, un vocabolario solo: «Scrivi» per confermare un gesto che cambia il sector, «Lascia stare» per scartare,
/// «Annulla» solo per la storia e le modifiche, «Togli» solo per il sector, «Registra/Manda il sector» e «… l'AIP»,
/// «Porta in vetrina». Questa guardia legge i componenti del Lab e trova i tasti che tornano ai nomi di prima.
/// </summary>
public sealed partial class VocabolarioDeiTastiTests
{
    // I nomi di prima (carta del riordino, §2 punto 7) e quelli che D6 ha sostituito.
    private static readonly string[] NomiDiPrima =
    [
        "Porta in Aurora", "Lascia com'era", "No, lasciale", "No, lascialo", "Torna alle modifiche", "Resta qui",
        "Annulla il riporto", "Salva la correzione", "Applica", "Scarta", "Registra", "Manda", "annulla", "No",
    ];

    [GeneratedRegex(@"<button\b(?<tutto>.*?)</button>", RegexOptions.Singleline)]
    private static partial Regex Tasto();

    // Il testo del tasto: dopo l'ultimo attributo, senza i tag e i commenti Razor dentro.
    private static string Testo(string tutto)
    {
        int k = tutto.LastIndexOf("\">", StringComparison.Ordinal);
        var corpo = k >= 0 ? tutto[(k + 2)..] : tutto[(tutto.IndexOf('>', StringComparison.Ordinal) + 1)..];
        corpo = Regex.Replace(corpo, @"@\*.*?\*@", "", RegexOptions.Singleline);
        corpo = Regex.Replace(corpo, "<[^>]+>", "");
        return Regex.Replace(corpo, @"\s+", " ").Trim();
    }

    [Fact]
    public void NessunTastoDelLab_ColNomeDiPrima()
    {
        // La radice del repo: i campioni del motore stanno in tests/Vipi.Sectorfile.Tests/Campioni.
        var radice = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(AlberoDiProva.Campioni)))!;
        var componenti = Path.Combine(radice, "src", "Vipi.SectorLab.Ui", "Components");
        Assert.True(Directory.Exists(componenti), componenti);

        var sbagliati = new List<string>();
        foreach (var file in Directory.EnumerateFiles(componenti, "*.razor", SearchOption.AllDirectories))
            foreach (Match m in Tasto().Matches(File.ReadAllText(file)))
                if (NomiDiPrima.Contains(Testo(m.Groups["tutto"].Value), StringComparer.Ordinal))
                    sbagliati.Add($"{Path.GetFileName(file)}: «{Testo(m.Groups["tutto"].Value)}»");

        Assert.Empty(sbagliati);
    }
}
