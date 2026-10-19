using Aip;
using Aip.Aurora;
using Vipi.SectorLab.Core.PdfAip;
using Vipi.SectorLab.Tests.Ui;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// Carta F6, slice 4c: i gesti per le aree di ENR 5 nei file <c>italy.*</c> — «Dividi» (una voce che vale più aree,
/// committente: «dividi»), «Sostituisci» (il cerchio col raggio dell'AIP, committente: «porta 0,5»), «Porta nel sector»
/// (le militari in <c>italy.restrict</c>, committente), e il rifiuto per un'area scritta male (R107A nel 2610). Dati inventati.
/// </summary>
public sealed class AreeEnr5GestiTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly SessioneDelLab _lab;
    private readonly string _espanso;

    public AreeEnr5GestiTests()
    {
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        _espanso = Path.Combine(_albero.Radice, "espanso");
    }

    public void Dispose() => _albero.Dispose();

    private static string Lati(string nome, string tipo, params (double Lat, double Lon)[] punti)
    {
        var righe = new List<string> { "//" + nome };
        for (var i = 0; i < punti.Length; i++)
        {
            var (a, b) = (punti[i], punti[(i + 1) % punti.Length]);
            righe.Add($"{AuroraText.Coordinates(new Position(a.Lat, a.Lon))};{AuroraText.Coordinates(new Position(b.Lat, b.Lon))};{tipo};{nome};");
        }
        return string.Join("\r\n", righe) + "\r\n";
    }

    private static Airspace Area(string id, AirspaceType tipo, Geometry g, string sezione = "ENR 5.1") => new()
    {
        Section = sezione, Type = tipo, Designator = id,
        Upper = new VerticalLimit { Text = "FL95" }, Lower = new VerticalLimit { Text = "GND" }, Geometry = g,
    };

    private static RingGeometry Quadrato(double lat, double lon) =>
        new([new LineSegment(new(lat, lon)), new LineSegment(new(lat, lon + 0.2)), new LineSegment(new(lat - 0.2, lon + 0.2)), new LineSegment(new(lat - 0.2, lon))]);

    private static readonly Position CentroP140 = new(44.7, 8.0);

    private async Task ApriAsync()
    {
        // R300AB: le due aree A e B in una voce (il contorno di tutte e due); R107A scritta male (uno spazio al posto del «;»).
        var restrict = Lati("R300AB", "RESTRICT", (41, 15), (41, 15.4), (40.8, 15.4), (40.8, 15))
            + "//R107A\r\nN042.56.13.000;E011.10.10.000 N043.16.53.000;E010.56.32.000;RESTRICT;R107A;\r\n";
        // P140: un cerchio di 1 NM, l'AIP lo dà di 0,5.
        var cerchio = Enumerable.Range(0, 120).Select(i => Sphere.Destination(CentroP140, i * 3, 1.0)).Select(p => (p.Lat, p.Lon)).ToArray();
        _albero.Scrivi("SectorFiles/Include/IT/GEO/italy.restrict", restrict);
        _albero.Scrivi("SectorFiles/Include/IT/GEO/italy.prohibit", Lati("P140", "PROHIBIT", cerchio));
        _albero.Scrivi("SectorFiles/Include/IT/GEO/italy.danger", Lati("D1", "DANGER", (39, 9), (39, 9.2), (38.8, 9.2)));
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        Assert.True(_lab.CambiaLAipEspanso(_espanso), _lab.Rifiuto);
        var aip = new AipDocument
        {
            State = "LI", Airac = "2611",
            Airspaces =
            [
                Area("LI R300/A", AirspaceType.Restricted, Quadrato(41, 15)),
                Area("LI R300/B", AirspaceType.Restricted, Quadrato(41, 15.2)),
                Area("LI R107/A", AirspaceType.Restricted, Quadrato(43, 11)),
                Area("LI P140", AirspaceType.Prohibited, new CircleGeometry(CentroP140, new Distance(0.5, DistanceUnit.Nm))),
                Area("LI D1", AirspaceType.Danger, new RingGeometry([new LineSegment(new(39, 9)), new LineSegment(new(39, 9.2)), new LineSegment(new(38.8, 9.2))])),
                Area("ALBA Corridor", AirspaceType.Military, new CorridorGeometry(new Distance(10, DistanceUnit.Nm), [new(41, 18.5), new(40.9, 18.95)]), "ENR 5.2"),
            ],
        };
        Directory.CreateDirectory(Path.Combine(_espanso, "2611"));
        await File.WriteAllTextAsync(Path.Combine(_espanso, "2611", "li.json"), AipJson.Serialize(aip));
        Assert.True(await _lab.ConfrontaAsync(FamigliaDelConfronto.AreeEnr5), _lab.Rifiuto);
    }

    private string[] Righe(string file) => [.. _lab.RigheDiAdesso("SectorFiles/Include/IT/GEO/" + file)];

    private DifferenzaDiForma Riga(string aip) => _lab.UltimoConfronto!.Value.Esiti.Single(e => e.Aip == aip);

    [Fact]
    public async Task Dividi_UnaVocePerArea_ColCommentoEIlTipo()
    {
        await ApriAsync();
        var r300 = Riga("LI R300/A + LI R300/B");
        Assert.Equal(2, r300.Zone);

        Assert.True(_lab.SostituisciAreaEnr5(r300), _lab.Rifiuto);

        var righe = Righe("italy.restrict");
        Assert.DoesNotContain(righe, r => r.Contains(";R300AB;", StringComparison.Ordinal) || r == "//R300AB");
        Assert.Contains("//R300A", righe);
        Assert.Contains("//R300B", righe);
        Assert.Equal(4, righe.Count(r => r.EndsWith(";RESTRICT;R300A;", StringComparison.Ordinal)));
        // La riga scritta male di R107A non si tocca.
        Assert.Contains(righe, r => r.Contains(";RESTRICT;R107A;", StringComparison.Ordinal));
        Assert.Equal(EsitoDelConfronto.Uguale, Riga("LI R300/A").Esito);
        Assert.Equal(EsitoDelConfronto.Uguale, Riga("LI R300/B").Esito);
        Assert.StartsWith("R300AB diviso in 2 dall'AIP 2611", _lab.DaAnnullare, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sostituisci_IlCerchioColRaggioDellAip()
    {
        await ApriAsync();
        Assert.Equal(EsitoDelConfronto.FormaDiversa, Riga("LI P140").Esito);

        Assert.True(_lab.SostituisciAreaEnr5(Riga("LI P140")), _lab.Rifiuto);

        Assert.Equal(EsitoDelConfronto.Uguale, Riga("LI P140").Esito);
        // A 3 gradi, come il sector: 120 lati.
        Assert.Equal(120, Righe("italy.prohibit").Count(r => r.EndsWith(";PROHIBIT;P140;", StringComparison.Ordinal)));
        _lab.Annulla();
        Assert.Equal(EsitoDelConfronto.FormaDiversa, Riga("LI P140").Esito);
    }

    [Fact]
    public async Task PortaNelSector_LaMilitareInItalyRestrict_LaScrittaMaleNo()
    {
        await ApriAsync();
        Assert.Equal(EsitoDelConfronto.MancaNelSector, Riga("ALBA Corridor").Esito);

        Assert.True(_lab.PortaAreeEnr5(["ALBA Corridor", "LI R107/A"]), _lab.Rifiuto);

        var righe = Righe("italy.restrict");
        Assert.Contains("//ALBA CORRIDOR", righe);
        // Il contorno del corridoio: due punti per lato.
        Assert.Equal(4, righe.Count(r => r.EndsWith(";RESTRICT;ALBA CORRIDOR;", StringComparison.Ordinal)));
        Assert.Contains("R107A, scritta male (righe 7)", _lab.Rifiuto, StringComparison.Ordinal);
        Assert.Single(righe, r => r == "//R107A");
        Assert.Equal(EsitoDelConfronto.Uguale, Riga("ALBA Corridor").Esito);
    }

    [Fact]
    public void IlFileIlNomeEIlTipo()
    {
        Assert.Equal(("italy.prohibit", "PROHIBIT"), (AreeEnr5NelSector.File(Area("LI P1", AirspaceType.Prohibited, Quadrato(1, 1))), AreeEnr5NelSector.Tipo("italy.prohibit")));
        Assert.Equal("italy.restrict", AreeEnr5NelSector.File(Area("AT EMILIA", AirspaceType.Military, Quadrato(1, 1), "ENR 5.2")));
        Assert.Equal("TSA421B", AreeEnr5NelSector.Nome(Area("LI TSA421B", AirspaceType.Tsa, Quadrato(1, 1))));
        Assert.Equal("R107A", AreeEnr5NelSector.Nome(Area("LI R107/A", AirspaceType.Restricted, Quadrato(1, 1))));
        Assert.Equal("INDIA2", AreeEnr5NelSector.Nome(Area("INDIA 2", AirspaceType.Military, Quadrato(1, 1), "ENR 5.2")));
        Assert.Equal("AAR BRAVO", AreeEnr5NelSector.Nome(Area("AAR BRAVO", AirspaceType.Military, Quadrato(1, 1), "ENR 5.2")));
    }
}
