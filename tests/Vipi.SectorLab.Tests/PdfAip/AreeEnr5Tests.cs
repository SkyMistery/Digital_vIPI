using Aip;
using Aip.Aurora;
using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Core.PdfAip;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// Carta F6, slice 4b: le aree di ENR 5 contro <c>italy.prohibit</c>, <c>.restrict</c>, <c>.danger</c>, coi casi della
/// misura del 19 ottobre: il nome che comanda (<c>LI R4</c> ↔ <c>R4</c>), una voce per più aree (<c>R300AB</c>, <c>D75</c>),
/// la zona fra virgolette (TSA74 «Zona "A"»), i pezzi della stessa voce, un'area piccola dentro una grande che non è lei,
/// il cerchio col raggio diverso (P140: 1 NM nel sector, 0,5 nell'AIP). Dati inventati.
/// </summary>
public sealed class AreeEnr5Tests
{
    private static Airspace Area(string designatore, Geometry geometria, string? nome = null, string sezione = "ENR 5.1") => new()
    {
        Section = sezione,
        Type = AirspaceType.Restricted,
        Designator = designatore,
        Name = nome,
        Upper = new VerticalLimit { Text = "FL95" },
        Lower = new VerticalLimit { Text = "GND" },
        Geometry = geometria,
    };

    private static RingGeometry Quadrato(double lat, double lon, double lato = 0.2) =>
        new([new LineSegment(new(lat, lon)), new LineSegment(new(lat, lon + lato)), new LineSegment(new(lat - lato, lon + lato)), new LineSegment(new(lat - lato, lon))]);

    private static Position[] Contorno(double lat, double lon, double lato = 0.2) =>
        [new(lat, lon), new(lat, lon + lato), new(lat - lato, lon + lato), new(lat - lato, lon), new(lat, lon)];

    /// <summary>Una voce di un file delle aree, come la dà la mappa del Lab (una linea).</summary>
    private static FormaDellaMappa Voce(string nome, Position[] punti, string file = "italy.restrict", int record = 0) => new(
        $"SectorFiles/Include/IT/GEO/{file}", record, TipoDiForma.Linea, nome,
        [[.. punti.Select(p => new Coordinate(p.Lat, p.Lon))]], []);

    private static Dictionary<string, DifferenzaDiForma> Confronta(IEnumerable<Airspace> aip, IEnumerable<FormaDellaMappa> sector) =>
        ConfrontoDelleForme.AreeEnr5(aip, sector).ToDictionary(e => e.Aip ?? "solo: " + e.Sector);

    [Theory]
    [InlineData("LI R4", "R4")]
    [InlineData("LI D122 A", "D122A")]
    [InlineData("LI/LD D35 B e C", "D35BC")]
    [InlineData("LI R18/A", "R18A")]
    [InlineData("EUC 60", "EUC60")]
    public void IlNomeDiUnArea(string scritto, string atteso) => Assert.Equal(atteso, ConfrontoDelleForme.NomeDiEnr5(scritto));

    [Fact]
    public void IlNomeComanda_LaVoceDelloStessoNome_AncheSeUnAltraEPiuVicina()
    {
        var aip = Area("LI R4", Quadrato(44, 7));
        // R5 sta proprio sopra R4, R4 del sector un filo più in là: vince il nome.
        var e = Confronta([aip], [Voce("R5", Contorno(44, 7)), Voce("R4", Contorno(44, 7.0005))]);

        Assert.Equal(EsitoDelConfronto.Uguale, e["LI R4"].Esito);
        Assert.Equal("R4", e["LI R4"].Sector);
        Assert.Equal(EsitoDelConfronto.SoloNelSector, e["solo: R5"].Esito);
    }

    [Fact]
    public void UnaVocePerPiuAree_LeLettereOIlNomeDellaBase()
    {
        var aip = new[]
        {
            Area("LI R300/A", Quadrato(41, 15)), Area("LI R300/B", Quadrato(41, 15.2)),
            Area("LI D75/A", Quadrato(37, 15), sezione: "ENR 5.1"), Area("LI D75/B", Quadrato(37, 15.2)), Area("LI D75/C", Quadrato(37, 15.4)),
        };
        Position[] Due(double lat, double lon, int quante) => [new(lat, lon), new(lat, lon + 0.2 * quante), new(lat - 0.2, lon + 0.2 * quante), new(lat - 0.2, lon), new(lat, lon)];
        var e = Confronta(aip, [Voce("R300AB", Due(41, 15, 2)), Voce("D75", Due(37, 15, 3), "italy.danger")]);

        var r300 = e["LI R300/A + LI R300/B"];
        Assert.Equal((2, "R300AB"), (r300.Zone, r300.Sector));
        var d75 = e["LI D75/A + LI D75/B + LI D75/C"];
        Assert.Equal((3, "D75"), (d75.Zone, d75.Sector));
        // Le linee in comune fra due aree vicine non sono nel sector (lui ha il contorno di tutte): una forma diversa, non un «manca».
        Assert.DoesNotContain(e.Values, x => x.Esito == EsitoDelConfronto.MancaNelSector);
    }

    [Fact]
    public void LaZonaFraVirgolette_EIPezziVicini_EQuelloLontanoAParte()
    {
        var aip = new[]
        {
            Area("LI TSA74", Quadrato(42.5, 13.5), "Gran Sasso d'Italia Zona \"A\""),
            Area("LI TSA74", Quadrato(42.5, 13.7), "Gran Sasso d'Italia Zona \"B\""),
        };
        var b = Contorno(42.5, 13.7);
        var e = Confronta(aip,
        [
            Voce("TSA74A", Contorno(42.5, 13.5)),
            // TSA74B in due pezzi che si toccano, e un terzo pezzo con la stessa etichetta a 280 NM.
            Voce("TSA74B", b[..3], record: 10), Voce("TSA74B", b[2..], record: 11), Voce("TSA74B", Contorno(46.3, 11.6), record: 20),
        ]);

        Assert.Equal(EsitoDelConfronto.Uguale, e["LI TSA74 Gran Sasso d'Italia Zona \"A\""].Esito);
        Assert.Equal(EsitoDelConfronto.Uguale, e["LI TSA74 Gran Sasso d'Italia Zona \"B\""].Esito);
        var lontano = e["solo: TSA74B"];
        Assert.Equal(20, lontano.Record);
    }

    [Fact]
    public void UnAreaPiccolaDentroUnaGrandeNonELei()
    {
        // Un poligono militare di 1 NM dentro un'area di addestramento grande (SAN MATTEO e AAR BRAVO, 19 ottobre).
        var piccola = Area("SAN MATTEO", Quadrato(37.5, 14.5, 0.02), sezione: "ENR 5.2");
        var e = Confronta([piccola], [Voce("AAR BRAVO", Contorno(38.5, 13.5, 2.0), "italy.danger")]);

        Assert.Equal(EsitoDelConfronto.MancaNelSector, e["SAN MATTEO"].Esito);
        Assert.Equal(EsitoDelConfronto.SoloNelSector, e["solo: AAR BRAVO"].Esito);
    }

    [Fact]
    public void UnCerchioColRaggioDiversoEUnaFormaDiversa_DiMezzoMiglio()
    {
        var centro = new Position(44.698, 8.005);
        var aip = Area("LI P140", new CircleGeometry(centro, new Distance(0.5, DistanceUnit.Nm)));
        var sector = Voce("P140", [.. Enumerable.Range(0, 121).Select(i => Sphere.Destination(centro, i * 3, 1.0))], "italy.prohibit");

        var e = Confronta([aip], [sector]);

        var p140 = e["LI P140"];
        Assert.Equal(EsitoDelConfronto.FormaDiversa, p140.Esito);
        Assert.Equal(0.5, p140.ScartoAip!.Value, 1);
        Assert.Equal(0.5, p140.ScartoSector!.Value, 1);
    }
}
