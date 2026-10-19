using Aip;
using Vipi.SectorLab.Core.PdfAip;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// Carta F6, slice 0c: la cartella dei PDF dell'AIP («Prendi i PDF da Download»), i cicli AIRAC e l'AIP espanso, nel
/// nucleo del Lab. Tutto nel temporaneo: il test non tocca mai la cartella Download vera.
/// </summary>
public sealed class PdfAipTests : IDisposable
{
    private readonly string _radice = Path.Combine(Path.GetTempPath(), "pdf-aip-" + Guid.NewGuid().ToString("N"));

    public PdfAipTests() => Directory.CreateDirectory(_radice);

    public void Dispose() => Directory.Delete(_radice, recursive: true);

    private string Cartella(string nome) => Directory.CreateDirectory(Path.Combine(_radice, nome)).FullName;

    [Theory]
    [InlineData(2026, 1, 22, "2601", 2026, 1, 22)]
    [InlineData(2026, 9, 18, "2609", 2026, 9, 3)]
    [InlineData(2026, 10, 29, "2611", 2026, 10, 29)]
    [InlineData(2026, 10, 28, "2610", 2026, 10, 1)]
    [InlineData(2026, 1, 21, "2513", 2025, 12, 25)]
    // Il 2020 ha quattordici cicli.
    [InlineData(2020, 12, 31, "2014", 2020, 12, 31)]
    [InlineData(2020, 1, 2, "2001", 2020, 1, 2)]
    public void IlCicloDiUnGiorno(int a, int m, int g, string nome, int va, int vm, int vg)
    {
        var (ciclo, inVigore) = CicloAirac.Di(new DateOnly(a, m, g));

        Assert.Equal(nome, ciclo);
        Assert.Equal(new DateOnly(va, vm, vg), inVigore);
        Assert.Equal(inVigore, AipEspanso.CicloInVigore(nome));
    }

    [Theory]
    [InlineData("2611", true)]
    [InlineData("2614", true)]
    [InlineData("2615", false)]
    [InlineData("2600", false)]
    [InlineData("261", false)]
    [InlineData("26a1", false)]
    [InlineData(null, false)]
    public void UnCicloScritto(string? nome, bool valido) => Assert.Equal(valido, CicloAirac.Valido(nome));

    [Theory]
    [InlineData("LI-ENR 5.1.2.pdf", true)]
    [InlineData("LI-AD 2 LIRF 1.pdf", true)]
    [InlineData("LI-Cover Page.pdf", true)]
    [InlineData("LI-GEN 0.4.PDF", true)]
    [InlineData("fattura.pdf", false)]
    [InlineData("LI-ENR 5.1.2.docx", false)]
    [InlineData("EG-ENR 5.1.pdf", false)]
    public void IPdfDellAipSiRiconosconoDalNome(string nome, bool dellAip) => Assert.Equal(dellAip, PdfDellAip.EDellAip(nome));

    [Fact]
    public void PrendiDaDownload_SpostaSoloQuelliDellAip_EUnoColloStessoNomeLoSostituisceDovE()
    {
        var download = Cartella("Downloads");
        var pdf = Cartella("AIP");
        var enr5 = Directory.CreateDirectory(Path.Combine(pdf, "ENR 5")).FullName;
        File.WriteAllText(Path.Combine(enr5, "LI-ENR 5.1.2.pdf"), "vecchio");
        File.WriteAllText(Path.Combine(download, "LI-ENR 5.1.2.pdf"), "nuovo");
        File.WriteAllText(Path.Combine(download, "LI-ENR 5.1.3.pdf"), "nuovo");
        File.WriteAllText(Path.Combine(download, "fattura.pdf"), "mia");
        Directory.CreateDirectory(Path.Combine(download, "dentro"));
        File.WriteAllText(Path.Combine(download, "dentro", "LI-ENR 5.1.4.pdf"), "non si cerca");

        var presa = PdfDellAip.PrendiDaDownload(download, pdf);

        Assert.Equal(["LI-ENR 5.1.3.pdf"], presa.Nuovi);
        Assert.Equal(["LI-ENR 5.1.2.pdf"], presa.Sostituiti);
        Assert.Equal("nuovo", File.ReadAllText(Path.Combine(enr5, "LI-ENR 5.1.2.pdf")));
        Assert.True(File.Exists(Path.Combine(pdf, "LI-ENR 5.1.3.pdf")));
        Assert.True(File.Exists(Path.Combine(download, "fattura.pdf")));
        Assert.True(File.Exists(Path.Combine(download, "dentro", "LI-ENR 5.1.4.pdf")));
        Assert.False(File.Exists(Path.Combine(download, "LI-ENR 5.1.2.pdf")));
        Assert.Equal(["LI-ENR 5.1.2.pdf", "LI-ENR 5.1.3.pdf"], PdfDellAip.Elenco(pdf).Select(Path.GetFileName));
    }

    [Fact]
    public void PrendiDaDownload_SenzaDownloadNonFaNiente()
        => Assert.Equal(0, PdfDellAip.PrendiDaDownload(Path.Combine(_radice, "non-c-e"), Cartella("AIP")).Quanti);

    [Fact]
    public void LAipEspanso_IlModelloNellaCartellaDelCiclo()
    {
        var pdf = Cartella("AIP");
        File.WriteAllBytes(Path.Combine(pdf, "LI-ENR 5.1.2.pdf"), PdfAipFinto.Enr51("09 JUL 2026", "LI R901 - Prova Uno", "LI R902 - Prova Due"));
        File.WriteAllText(Path.Combine(pdf, "LI-ENR 6.pdf"), "una slice futura");
        File.WriteAllText(Path.Combine(pdf, "LI-ENR 5.1.3.pdf"), "questo non è un PDF");
        var espanso = Path.Combine(_radice, "espanso");

        var esito = AipEspanso.Leggi(PdfDellAip.Elenco(pdf), espanso, "2611");

        Assert.Equal(Path.Combine(espanso, "2611", "li.json"), esito.Scritto);
        Assert.Equal(3, esito.Pdf);
        Assert.Equal(["LI-ENR 5.1.2.pdf"], esito.Letti);
        Assert.Equal(["LI-ENR 6.pdf"], esito.NonAncora);
        Assert.StartsWith("LI-ENR 5.1.3.pdf", Assert.Single(esito.Illeggibili), StringComparison.Ordinal);
        Assert.Equal(2, esito.Aree);
        Assert.Equal([(new DateOnly(2026, 7, 9), 1)], esito.Date);
        Assert.Empty(esito.Problemi);

        var doc = AipJson.Deserialize(File.ReadAllText(esito.Scritto));
        Assert.Equal(("LI", "2611", new DateOnly(2026, 10, 29)), (doc.State, doc.Airac, doc.Effective));
        Assert.Equal(["LI R901", "LI R902"], doc.Airspaces.Select(a => a.Designator));
        Assert.Empty(AipCheck.Run(doc));
        Assert.False(File.Exists(esito.Scritto + ".scrivendo"));
    }

    [Fact]
    public void LAipEspanso_LeCorrezioniSiApplicanoAOgniLettura()
    {
        var pdf = Cartella("AIP");
        File.WriteAllBytes(Path.Combine(pdf, "LI-ENR 5.1.2.pdf"), PdfAipFinto.Enr51("09 JUL 2026", "LI R901 - Prova Uno", "LI R902 - Prova Due"));
        var espanso = Path.Combine(_radice, "espanso");
        var dalPdf = AipJson.Deserialize(File.ReadAllText(AipEspanso.Leggi(PdfDellAip.Elenco(pdf), espanso, "2611").Scritto));
        var togli = new AipCorrection
        {
            Id = "2026-10-19-1",
            Kind = CorrectionKind.Airspace,
            Key = dalPdf.Airspaces[1].Key,
            Op = CorrectionOp.Remove,
            Reason = "prova",
            Source = "AIC di prova",
        };
        var correzioni = AipEspanso.FileDelleCorrezioni(espanso);
        Directory.CreateDirectory(Path.GetDirectoryName(correzioni)!);
        File.WriteAllText(correzioni, AipCorrections.Serialize(new AipCorrectionFile { State = "LI", Corrections = [togli] }));

        var esito = AipEspanso.Leggi(PdfDellAip.Elenco(pdf), espanso, "2611");

        Assert.Equal(Path.Combine(espanso, "correzioni", "li.json"), correzioni);
        Assert.Equal(CorrectionOutcomeKind.Applied, Assert.Single(esito.Correzioni).Outcome);
        Assert.Equal("1 applicata", esito.CorrezioniInParole());
        Assert.Equal(["LI R901"], AipJson.Deserialize(File.ReadAllText(esito.Scritto)).Airspaces.Select(a => a.Designator));
        var scritta = Assert.Single(AipCorrections.Deserialize(File.ReadAllText(correzioni)).Corrections);
        Assert.Equal("2611", scritta.Since);
        Assert.NotNull(scritta.Before);
        Assert.False(File.Exists(correzioni + ".scrivendo"));
    }

    [Fact]
    public void LAipEspanso_UnFileDelleCorrezioniRottoFermaTutto()
    {
        var pdf = Cartella("AIP");
        File.WriteAllBytes(Path.Combine(pdf, "LI-ENR 5.1.2.pdf"), PdfAipFinto.Enr51("09 JUL 2026", "LI R901 - Prova Uno"));
        var espanso = Path.Combine(_radice, "espanso");
        var correzioni = AipEspanso.FileDelleCorrezioni(espanso);
        Directory.CreateDirectory(Path.GetDirectoryName(correzioni)!);
        File.WriteAllText(correzioni, "{ non è JSON");

        Assert.Throws<InvalidDataException>(() => AipEspanso.Leggi(PdfDellAip.Elenco(pdf), espanso, "2611"));
        Assert.False(File.Exists(AipEspanso.File(espanso, "2611")));

        File.WriteAllText(correzioni, AipCorrections.Serialize(new AipCorrectionFile { State = "LF" }));
        Assert.Throws<InvalidDataException>(() => AipEspanso.Leggi(PdfDellAip.Elenco(pdf), espanso, "2611"));
    }

    [Fact]
    public void LAipEspansoNonStaNellaCartellaDeiPdf()
    {
        Assert.NotNull(AipEspanso.PercheNonVa(null, null));
        Assert.NotNull(AipEspanso.PercheNonVa("relativa", null));
        Assert.NotNull(AipEspanso.PercheNonVa(Path.Combine(_radice, "AIP") + Path.DirectorySeparatorChar, Path.Combine(_radice, "AIP")));
        Assert.Null(AipEspanso.PercheNonVa(Path.Combine(_radice, "espanso"), Path.Combine(_radice, "AIP")));
        Assert.Throws<ArgumentException>(() => AipEspanso.Leggi([], Path.Combine(_radice, "espanso"), "26"));
    }
}
