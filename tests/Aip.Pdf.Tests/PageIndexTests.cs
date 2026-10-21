using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Aip.Pdf.Tests;

/// <summary>
/// L'indice delle pagine (carta «L'AIP intero e gli emendamenti», passo 1): riferimento e data dalla testata, le pagine
/// senza testata dalla posizione, i controlli. PDF costruiti qui; le testate hanno la forma di quelle vere del 2611.
/// </summary>
public sealed class PageIndexTests
{
    private const double Altezza = 842;

    /// <summary>Un PDF: per ogni pagina le righe di testata (null = una pagina vuota, come le dispari di ENR 6.3 nel 2611).</summary>
    private static IReadOnlyList<PdfPageText> Pdf(params string[]?[] pagine)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var righe in pagine)
        {
            var page = builder.AddPage(595, Altezza);
            if (righe is null) continue;
            for (var i = 0; i < righe.Length; i++)
                page.AddText(righe[i], 8, new PdfPoint(52, Altezza - 30 - 14 * i), font);
        }
        return PdfText.Read(builder.Build());
    }

    private static string[] Testata(string riferimento, string data = "11 JUN 2026") =>
        [riferimento, "AIP - Italia AIRAC effective date " + data, "ENR 6.3 CARTE"];

    [Fact]
    public void LeTestateEIlNumeroCheStaAlSuoPosto()
    {
        var indice = PageIndex.Of("LI-ENR 4.4.pdf", Pdf(Testata("ENR 4.4 - 1"), Testata("ENR 4.4 - 2", "01 OCT 2026")));

        Assert.Equal(["ENR 4.4-1", "ENR 4.4-2"], indice.Select(p => p.Reference));
        Assert.Equal([new DateOnly(2026, 6, 11), new DateOnly(2026, 10, 1)], indice.Select(p => p.Effective));
        Assert.All(indice, p => Assert.False(p.ByPosition));
        Assert.Equal("ENR 4.4", indice[0].Section);
        Assert.Equal(2, indice[1].Number);
        Assert.Empty(PageIndex.Check(indice, new DateOnly(2026, 10, 29)));
    }

    // ENR 6.3 nel 2611: le pagine dispari dalla 3 alla 13 non hanno nemmeno una parola.
    [Fact]
    public void UnaPaginaVuotaSiRiconosceDallaPosizione()
    {
        var indice = PageIndex.Of("LI-ENR 6.3.pdf", Pdf(Testata("ENR 6.3 - 1"), Testata("ENR 6.3 - 2"), null, Testata("ENR 6.3 - 4")));

        Assert.Equal("ENR 6.3-3", indice[2].Reference);
        Assert.True(indice[2].ByPosition);
        Assert.Null(indice[2].Effective);
        // Senza testata la data non c'è, e non è un difetto della lettura.
        Assert.Empty(PageIndex.Check(indice));
    }

    // AD 2 LIPX 5-11 nel 2611: un numero della carta sulla stessa riga del riferimento.
    [Fact]
    public void UnNumeroDavantiAlRiferimentoSiSalta() =>
        Assert.Equal("AD 2 LIPX 5-11", PageInfo.Reference(["1 AD 2 LIPX 5 - 11", "0", "AIP - Italia AIRAC 0 effective date 03 SEP 2026"]));

    [Fact]
    public void FuoriPostoNonSiIndovinaESiDice()
    {
        var indice = PageIndex.Of("LI-ENR 6.3.pdf", Pdf(Testata("ENR 6.3 - 2"), null));

        Assert.Null(indice[1].Reference);
        var problemi = PageIndex.Check(indice);
        Assert.Contains(problemi, p => p.Kind == PageIndexIssueKind.OutOfOrder && p.Position == 1 && p.Detail == "ENR 6.3-2");
        Assert.Contains(problemi, p => p.Kind == PageIndexIssueKind.NoReference && p.Position == 2);
    }

    [Fact]
    public void DueSezioniInUnFileNonSiIndovinaESiDice()
    {
        var indice = PageIndex.Of("misto.pdf", Pdf(Testata("ENR 4.4 - 1"), Testata("ENR 4.5 - 2"), null));

        Assert.Null(indice[2].Reference);
        Assert.Contains(PageIndex.Check(indice), p => p.Kind == PageIndexIssueKind.MoreSections && p.Detail == "ENR 4.4, ENR 4.5");
    }

    [Fact]
    public void IControlliDellaCartella()
    {
        IndexedPage[] pagine =
        [
            new("LI-ENR 4.4.pdf", 1, "ENR 4.4-1", new DateOnly(2026, 6, 11)),
            new("LI-ENR 4.4.pdf", 2, "ENR 4.4-2", new DateOnly(2026, 11, 26)),
            new("LI-ENR 4.4.pdf", 3, "ENR 4.4-3", null),
            new("copia\\LI-ENR 4.4.pdf", 1, "ENR 4.4-1", new DateOnly(2026, 6, 11)),
        ];

        var problemi = PageIndex.Check(pagine, new DateOnly(2026, 10, 29));

        Assert.Contains(problemi, p => p.Kind == PageIndexIssueKind.AfterCycle && p.Position == 2 && p.Detail == "ENR 4.4-2 2026-11-26");
        Assert.Contains(problemi, p => p.Kind == PageIndexIssueKind.NoDate && p.Position == 3);
        Assert.Contains(problemi, p => p.Kind == PageIndexIssueKind.Duplicate && p.File == "copia\\LI-ENR 4.4.pdf" && p.Detail == "ENR 4.4-1 (anche in LI-ENR 4.4.pdf p.1)");
        Assert.Equal(3, problemi.Count);
        // Senza il ciclo la data dopo non si controlla.
        Assert.DoesNotContain(PageIndex.Check(pagine), p => p.Kind == PageIndexIssueKind.AfterCycle);
    }

    [Theory]
    [InlineData("ENR 4.4-77", "ENR 4.4", 77)]
    [InlineData("AD 2 LIRF 5-12", "AD 2 LIRF 5", 12)]
    [InlineData("ENR 3.2 KY139-1", "ENR 3.2 KY139", 1)]
    public void SezioneENumero(string riferimento, string sezione, int numero)
    {
        Assert.Equal(sezione, PageIndex.SectionOf(riferimento));
        Assert.Equal(numero, PageIndex.NumberOf(riferimento));
    }
}
