namespace Aip.Pdf.Tests;

/// <summary>
/// 🔴 Gli importer si portano via (carta F6 §3): la parte comune dipende dal modello e da PdfPig, quella italiana dalla
/// parte comune e dal modello. Niente Lab, niente sito, niente motore del sector, niente «vIPI» nei nomi.
/// </summary>
public sealed class NessunaDipendenzaTests
{
    private static List<string> Estranei(Type tipo, params string[] ammessi) =>
        tipo.Assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? "")
            .Where(n => n != "netstandard" && n != "mscorlib" && !n.StartsWith("System", StringComparison.Ordinal)
                && !ammessi.Any(a => n == a || n.StartsWith(a + ".", StringComparison.Ordinal)))
            .ToList();

    [Fact]
    public void LaParteComuneDipendeSoloDalModelloEDaPdfPig() =>
        Assert.Empty(Estranei(typeof(GeometryText), "Aip", "UglyToad.PdfPig"));

    [Fact]
    public void LaParteItalianaDipendeSoloDallaComuneEDalModello() =>
        Assert.Empty(Estranei(typeof(Italia.ItalianAip), "Aip", "Aip.Pdf"));

    [Fact]
    public void NeiNomiNonCeVipi()
    {
        foreach (var assieme in new[] { typeof(GeometryText).Assembly, typeof(Italia.ItalianAip).Assembly })
        {
            Assert.DoesNotContain("vipi", assieme.GetName().Name!, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(assieme.GetTypes(), t => (t.Namespace ?? "").Contains("vipi", StringComparison.OrdinalIgnoreCase));
        }
    }
}
