using Aip.Pdf.Italia;
using Pagina = Aip.Pdf.Tests.Enr4Tests.Pagina;

namespace Aip.Pdf.Tests;

/// <summary>
/// ENR 5.3 (carta F6, «finiamo con l'importer»): i palloni sonda con due cerchi nella stessa riga (AREA ETNEA), un laser
/// con la longitudine stampata a due cifre (Roma), un vulcano con l'altezza sotto le coordinate, e 5.3.4 che non ha oggetti.
/// PDF costruiti qui (<see cref="Enr4Tests.Pdf"/>). Dati inventati sulla forma di quelli veri.
/// </summary>
public sealed class Enr53Tests
{
    private static (double, double, string)[] Testa(string riferimento) =>
        [(450, 30, riferimento), (52, 45, "AIP - Italia AIRAC effective date 03 SEP 2026")];

    private static IEnumerable<(double, double, string)> Numeri(double[] colonne) =>
        Enumerable.Range(0, colonne.Length - 1).Select(i => (colonne[i] + 4, 132.0, (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void IPalloni_UnPericoloPerCerchio_ColRaggioELaQuota()
    {
        double[] c = [52, 122, 187, 220, 270, 318, 368, 418, 467, 515, 565];
        var pagina = new Pagina(c, [100, 130, 145, 300],
        [
            .. Testa("ENR 5.3.1 - 1"), (56, 110, "Localita"), .. Numeri(c),
            (56, 150, "AREA ETNEA"), (56, 160, "(A)"), (56, 170, "Circle of radius"), (56, 180, "16KM centrato su/"),
            (56, 190, "37°45'03''N"), (56, 200, "014°45'38''E"), (56, 210, "(B)"), (56, 220, "Circle of radius"),
            (56, 230, "10KM centrato su/"), (56, 240, "37°45'03''N"), (56, 250, "014°45'38''E"),
            (126, 150, "NOTAM"), (322, 150, "20000"), (471, 150, "INGV"),
        ]);

        var r = ItalianEnr53.ReadBalloons(PdfText.Read(Enr4Tests.Pdf(pagina)));

        Assert.Equal(["AREA ETNEA (A)", "AREA ETNEA (B)"], r.Hazards.Select(h => h.Name));
        Assert.All(r.Hazards, h => Assert.Equal((HazardKind.Balloon, "ENR 5.3.1", "20000 m", "INGV"), (h.Kind, h.Section, h.Vertical, h.Remarks)));
        Assert.Equal([new Distance(16, DistanceUnit.Km), new Distance(10, DistanceUnit.Km)], r.Hazards.Select(h => h.Radius));
        Assert.Equal(37 + 45 / 60.0 + 3 / 3600.0, r.Hazards[0].Position.Lat, 9);
        Assert.Empty(r.Issues);
    }

    [Fact]
    public void ILaser_LaLongitudineADueCifre_ELaQuotaInKm()
    {
        double[] c = [52, 157, 206, 244, 514, 565];
        var pagina = new Pagina(c, [100, 130, 145, 200, 260],
        [
            .. Testa("ENR 5.3.2 - 1"), (56, 110, "Localita"), .. Numeri(c),
            (56, 150, "Roma"), (56, 160, "41°54'06\"N"), (56, 170, "12°30'57\"E"), (161, 150, "H24"), (210, 150, "GND/"), (210, 160, "30 KM"),
            (248, 150, "1.Fascio laser"),
            (56, 205, "Monte Cimone"), (56, 215, "44°11'35\"N"), (56, 225, "010°42'05\"E"), (161, 205, "DAILY H24"), (210, 205, "18"), (248, 205, "NIL"),
        ]);

        var r = ItalianEnr53.ReadLasers(PdfText.Read(Enr4Tests.Pdf(pagina)));

        Assert.Equal(["Roma", "Monte Cimone"], r.Hazards.Select(h => h.Name));
        Assert.Equal(12 + 30 / 60.0 + 57 / 3600.0, r.Hazards[0].Position.Lon, 9);
        Assert.Equal(("GND/ 30 KM", "H24"), (r.Hazards[0].Vertical, r.Hazards[0].Hours));
        Assert.Equal(("18 km", (string?)null), (r.Hazards[1].Vertical, r.Hazards[1].Remarks));
        Assert.Empty(r.Issues);
    }

    [Fact]
    public void IVulcani_LAltezzaSottoLeCoordinate_ElAvvisoNelleNote()
    {
        double[] c = [52, 142, 232, 295, 415, 565];
        var pagina = new Pagina(c, [100, 130, 145, 210, 240],
        [
            .. Testa("ENR 5.3.3 - 1"), (56, 110, "NAME/ID"), .. Numeri(c),
            (56, 150, "ETNA"), (56, 160, "211060"), (146, 150, "37°44'03\"N"), (146, 160, "015°00'14\"E"), (146, 175, "3330m"), (146, 185, "10922ft"),
            (236, 150, "LI-P1"), (299, 150, "Roma ACC/FIC"), (419, 150, "Almost persistent"),
            (56, 215, "NOTA"), (146, 215, "1.In caso di attivita"),
        ]);

        var r = ItalianEnr53.ReadVolcanoes(PdfText.Read(Enr4Tests.Pdf(pagina)));

        var etna = Assert.Single(r.Hazards);
        Assert.Equal(("ETNA 211060", "3330m 10922ft", "LI-P1\nAlmost persistent"), (etna.Name, etna.Vertical, etna.Remarks));
        Assert.Equal(15 + 14 / 3600.0, etna.Position.Lon, 9);
    }

    [Fact]
    public void Enr534_LettoSenzaOggetti()
    {
        var r = ItalianEnr53.ReadRulesOnly(PdfText.Read(Enr4Tests.Pdf(new Pagina([], [], [.. Testa("ENR 5.3.4 - 1"), (52, 100, "ENR 5.3.4 OPERAZIONI CON DRONI")]))));

        Assert.Equal((0, 0, 1), (r.Hazards.Count, r.Issues.Count, r.Pages.Count));
    }
}
