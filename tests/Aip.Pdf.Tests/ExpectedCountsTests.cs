using Aip.Pdf.Italia;

namespace Aip.Pdf.Tests;

/// <summary>I conti che l'AIP stampa (carta «L'AIP intero», passo 2e): zone numerate e punti ▲∆ delle rotte.</summary>
public sealed class ExpectedCountsTests
{
    private static PdfWord Parola(double x, double y, string testo) => new(testo, x, x + testo.Length * 4.5, y - 7, y + 1, y);

    private static PdfPageText Pagina(params (double X, double Y, string Testo)[] righe)
    {
        var parole = new List<PdfWord>();
        foreach (var (x0, y, t) in righe)
        {
            var x = x0;
            foreach (var p in t.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                parole.Add(Parola(x, y, p));
                x += p.Length * 4.5 + 3;
            }
        }
        return new PdfPageText(1, 595, 842, parole, []);
    }

    [Fact]
    public void LeZoneNumerate_UnaVoltaSola_SoloAInizioRiga()
    {
        var pagine = new[]
        {
            Pagina((52, 100, "Zone '1' -QUIRINALE"), (320, 100, "Zone '1' -QUIRINALE"), (52, 200, "Zone '2' -MEDITERRANEO"),
                (420, 210, "Roma CTA Zone 6 \"Mongibello\""), (52, 300, "Roma CTR Zona/Zone '3'")),
        };

        Assert.Equal([new ExpectedCount("zone", 3)], ItalianExpectedCounts.Of("LI-ENR 2.1.1.4.3.pdf", pagine));
        Assert.Empty(ItalianExpectedCounts.Of("LI-ENR 4.4.pdf", pagine));
    }

    [Fact]
    public void IPuntiDiUnaRotta()
    {
        var pagine = new[] { Pagina((56, 182, "▲ UNIMI"), (56, 258, "∆ NAXAV"), (56, 334, "▲ VICENZA VOR/DME")) };

        Assert.Equal([new ExpectedCount("punti", 3)], ItalianExpectedCounts.Of("LI-ENR 3.2 L99.pdf", pagine));
    }

    [Fact]
    public void IlLetto_AreeConLaZonaNelNome_PuntiDelleRotte()
    {
        var area = new Airspace
        {
            Section = "ENR 2.1", Type = AirspaceType.Cta, Designator = "ROMA CTA Zone '1'", Upper = LimitText.Parse("FL195"),
            Lower = LimitText.Parse("FL95"), Geometry = new CircleGeometry(new Position(42, 12), new Distance(1, DistanceUnit.Nm)),
        };
        var letto = new ImportResult([area, area with { Designator = "ROMA FIR" }], [], []);

        Assert.Equal(1, ItalianExpectedCounts.Read("zone", letto));
        Assert.Equal(0, ItalianExpectedCounts.Read("punti", letto));
    }
}
