using Aip;
using Vipi.SectorLab.Core.PdfAip;
using Vipi.SectorLab.Tests.Ui;
using Vipi.SectorLab.Ui.Servizi;
using Route = Aip.Route;
using RouteDirection = Aip.RouteDirection;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// Carta F6, slice 2b: le rotte di ENR 3.2 contro le aerovie di <c>itawlow.lairway</c> e le attese di ENR 3.4.1 contro
/// <c>HOLDENR.hold</c>, in sola lettura, coi casi della misura del 19 ottobre (Z910 con un lato diverso, L12 che continua
/// in Austria, ABRUS con la rotta d'avvicinamento a un grado, le attese sulle radioassistenze di ENR 3.4.2). Dati inventati.
/// </summary>
public sealed class ConfrontoDiEnr3Tests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly SessioneDelLab _lab;
    private readonly string _espanso;

    public ConfrontoDiEnr3Tests()
    {
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        _espanso = Path.Combine(_albero.Radice, "espanso");
    }

    public void Dispose() => _albero.Dispose();

    private static RoutePoint Punto(string nome, double lat, double lon) => new(nome, nome, new Position(lat, lon), true);

    private static RouteSegment Tratto(RouteDirection dispari = RouteDirection.Forward, RouteDirection pari = RouteDirection.Backward) => new()
    {
        Upper = new VerticalLimit { Text = "FL195" }, Lower = new VerticalLimit { Text = "FL95" }, Odd = dispari, Even = pari,
    };

    // I punti del sector: i fix delle rotte e delle attese inventate, ELKAP che sta nelle STAR di lirf.str.
    private const string Fix = "AAAAA;N045.00.00.000;E009.00.00.000;3;\r\nBBBBB;N045.00.00.000;E009.30.00.000;3;\r\nCCCCC;N045.00.00.000;E010.00.00.000;3;\r\n"
        + "DDDDD;N044.00.00.000;E009.00.00.000;3;\r\nFFFFF;N044.00.00.000;E010.00.00.000;3;\r\nGGGGG;N044.00.00.000;E010.30.00.000;3;\r\n"
        + "XXXXX;N044.10.00.000;E009.10.00.000;3;\r\nELKAP;N042.10.00.000;E012.00.00.000;3;\r\nNUOVO;N043.00.00.000;E011.00.00.000;3;\r\n";

    private async Task ApriConAsync(string aerovie, string attese, AipDocument aip)
    {
        // In coda ai fix che il master carica (APT.fix è nel suo .isc).
        var apt = _albero.Percorso("SectorFiles/Include/IT/NAVAIDS/APT.fix");
        _albero.Scrivi("SectorFiles/Include/IT/NAVAIDS/APT.fix", File.ReadAllText(apt).TrimEnd() + "\r\n" + Fix);
        _albero.Scrivi("SectorFiles/Include/IT/AIRWAY/itawlow.lairway", aerovie);
        _albero.Scrivi("SectorFiles/Include/IT/HOLDENR.hold", attese);
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        Assert.True(_lab.CambiaLAipEspanso(_espanso), _lab.Rifiuto);
        Directory.CreateDirectory(Path.Combine(_espanso, "2611"));
        await File.WriteAllTextAsync(Path.Combine(_espanso, "2611", "li.json"), AipJson.Serialize(aip with { State = "LI", Airac = "2611" }));
    }

    private static readonly string Aerovie = string.Join("\r\n",
    [
        "//Airway tracks",
        "//@\"L1\" locked=si",
        "//@START",
        "//@@\"AAAAA\" dir=fwd lower=FL95 upper=FL195",
        "T;L1;AAAAA;AAAAA;",
        "//@@\"BBBBB\" dir=both lower=FL115 upper=FL195",
        "T;L1;BBBBB;BBBBB;",
        "T;L1;CCCCC;CCCCC;",
        "T;L1;ESTERO;ESTERO;",
        "//@END \"L1\"",
        "T;M2;DDDDD;DDDDD;",
        "T;M2;XXXXX;XXXXX;",
        "T;BREAK;XXXXX;XXXXX;",
        "T;M2;FFFFF;FFFFF;",
        "T;M2;GGGGG;GGGGG;",
        "",
    ]);

    private static AipDocument Modello() => new()
    {
        State = "LI",
        Routes =
        [
            // L1: nell'AIP da AAAAA a CCCCC, solo avanti (dispari) AAAAA-BBBBB, nei due versi BBBBB-CCCCC; il sector va oltre.
            new Route
            {
                Section = "ENR 3.2", Designator = "L1",
                Points = [Punto("AAAAA", 45.0, 9.0), Punto("BBBBB", 45.0, 9.5), Punto("CCCCC", 45.0, 10.0)],
                Segments = [Tratto(pari: RouteDirection.None), Tratto()],
            },
            // M2: l'AIP ha DDDDD-EEEEE (il sector DDDDD-XXXXX) e un'interruzione dove il sector la ha.
            new Route
            {
                Section = "ENR 3.2", Designator = "M2",
                Points = [Punto("DDDDD", 44.0, 9.0), Punto("EEEEE", 44.0, 9.5), Punto("FFFFF", 44.0, 10.0), Punto("GGGGG", 44.0, 10.5)],
                Segments = [Tratto(), new RouteSegment { Discontinuity = true }, Tratto()],
            },
            new Route
            {
                Section = "ENR 3.2", Designator = "Q9",
                Points = [Punto("AAAAA", 45.0, 9.0), Punto("DDDDD", 44.0, 9.0)],
                Segments = [Tratto()],
            },
        ],
        Holdings =
        [
            new Holding { Section = "ENR 3.4.1", Fix = "ABBOZ", Minimum = new VerticalLimit { Text = "9000" }, Turn = TurnDirection.Right, InboundTrack = 225 },
            new Holding { Section = "ENR 3.4.1", Fix = "ABRUS", Minimum = new VerticalLimit { Text = "3000" }, Turn = TurnDirection.Left, InboundTrack = 250 },
            new Holding { Section = "ENR 3.4.1", Fix = "NUOVO", Minimum = new VerticalLimit { Text = "FL110" }, Turn = TurnDirection.Left, InboundTrack = 90 },
            new Holding { Section = "ENR 3.4.1", Fix = "ELKAP", Minimum = new VerticalLimit { Text = "5000" }, Turn = TurnDirection.Right, InboundTrack = 180,
                          Remarks = "RNAV Holding - Outbound Leg 4NM\nIAS MAX 210kt" },
        ],
    };

    private const string Attese = "HLD-ABBOZ;N046.02.37.000;E011.07.48.000;ABBOZ/225R-9000;\r\nHLD-ABRUS;N036.51.40.000;E012.09.54.000;ABRUS/251L-3000;\r\nHLD-ALG;N040.37.41.300;E008.14.38.000;ALG/042L-4000;\r\n";

    [Fact]
    public async Task LeRotte_ILatiITrattiEQuelCheContinuaFuori()
    {
        await ApriConAsync(Aerovie, Attese, Modello());

        Assert.True(await _lab.ConfrontaEnr3Async(rotte: true), _lab.Rifiuto);

        var esiti = _lab.UltimoConfrontoDiEnr3!.Value.Esiti.ToDictionary(e => e.Nome);
        // L1: i lati uguali, ESTERO è oltre l'AIP; AAAAA-BBBBB ha i tag giusti, BBBBB-CCCCC no (FL115 contro FL95) e
        // CCCCC-ESTERO non conta.
        var l1 = esiti["L1"];
        Assert.Equal(EsitoDelConfronto.FormaDiversa, l1.Esito);
        Assert.Contains("il sector continua oltre l'AIP italiano: ESTERO", l1.Dettagli);
        Assert.Contains("tratti: 2 in comune, 1 coi tag uguali all'AIP, 1 diversi, 0 senza tag nel sector", l1.Dettagli);
        Assert.Contains("  BBBBB→CCCCC: AIP both FL95–FL195, sector both FL115–FL195", l1.Dettagli);
        Assert.EndsWith("itawlow.lairway", l1.File, StringComparison.Ordinal);
        // M2: un lato solo nell'AIP, uno solo nel sector; l'interruzione è dove sta nel sector.
        var m2 = esiti["M2"];
        Assert.Equal(EsitoDelConfronto.FormaDiversa, m2.Esito);
        Assert.Contains("lati solo nell'AIP (1): DDDDD–EEEEE", m2.Dettagli);
        Assert.Contains("lati solo nel sector (1): DDDDD–XXXXX", m2.Dettagli);
        Assert.Contains("tratti: 1 in comune, 0 coi tag uguali all'AIP, 0 diversi, 1 senza tag nel sector", m2.Dettagli);
        Assert.Equal(EsitoDelConfronto.MancaNelSector, esiti["Q9"].Esito);
    }

    [Fact]
    public async Task LeAttese_RottaVirataQuota_ELeRadioassistenzeDette()
    {
        await ApriConAsync(Aerovie, Attese, Modello());

        Assert.True(await _lab.ConfrontaEnr3Async(rotte: false), _lab.Rifiuto);

        var esiti = _lab.UltimoConfrontoDiEnr3!.Value.Esiti.ToDictionary(e => e.Nome);
        Assert.Equal(EsitoDelConfronto.Uguale, esiti["ABBOZ"].Esito);
        Assert.Equal(EsitoDelConfronto.FormaDiversa, esiti["ABRUS"].Esito);
        Assert.Contains("rotta d'avvicinamento AIP 250°, sector 251°", esiti["ABRUS"].Dettagli);
        Assert.Equal(EsitoDelConfronto.MancaNelSector, esiti["NUOVO"].Esito);
        Assert.Equal(EsitoDelConfronto.SoloNelSector, esiti["ALG"].Esito);
        Assert.Contains(esiti["ALG"].Dettagli, d => d.Contains("ENR 3.4.2", StringComparison.Ordinal));
        Assert.False(_lab.Modifiche.CEQualcosa);
    }

    // --- slice 2c: i gesti --------------------------------------------------------------------------------------------

    private string[] Righe(string file) => [.. _lab.RigheDiAdesso("SectorFiles/Include/IT/" + file)];

    [Fact]
    public async Task ScriviITratti_NeiTagDelSector_ELeLockedNo()
    {
        await ApriConAsync(Aerovie, Attese, Modello());
        Assert.True(await _lab.ConfrontaEnr3Async(rotte: true), _lab.Rifiuto);

        // L1 è locked=si: l'import non la tocca.
        Assert.False(_lab.ScriviITratti(["L1"]));
        Assert.Contains("locked=si", _lab.Rifiuto, StringComparison.Ordinal);

        Assert.True(_lab.ScriviITratti(["M2"]), _lab.Rifiuto);

        var righe = Righe("AIRWAY/itawlow.lairway");
        var tag = Assert.Single(righe, r => r.StartsWith("//@@\"FFFFF\"", StringComparison.Ordinal));
        Assert.Contains("dir=both", tag, StringComparison.Ordinal);
        Assert.Contains("lower=FL95", tag, StringComparison.Ordinal);
        Assert.Contains("upper=FL195", tag, StringComparison.Ordinal);
        var m2 = _lab.UltimoConfrontoDiEnr3!.Value.Esiti.Single(e => e.Nome == "M2");
        Assert.Equal(0, m2.TrattiDaScrivere);
        Assert.Contains("tratti: 1 in comune, 1 coi tag uguali all'AIP, 0 diversi, 0 senza tag nel sector", m2.Dettagli);
        Assert.StartsWith("tratti di M2", _lab.DaAnnullare, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PortaLaRotta_IlBloccoCoiTratti_NonLocked()
    {
        await ApriConAsync(Aerovie, Attese, Modello());
        Assert.True(await _lab.ConfrontaEnr3Async(rotte: true), _lab.Rifiuto);

        Assert.True(_lab.PortaLaRotta("Q9"), _lab.Rifiuto);

        var righe = Righe("AIRWAY/itawlow.lairway");
        Assert.Contains("//@\"Q9\" note=\"AIP 2611\"", righe);
        Assert.Contains("//@@\"AAAAA\" dir=both lower=FL95 upper=FL195", righe);
        Assert.Contains("T;Q9;AAAAA;AAAAA;", righe);
        Assert.Contains("T;Q9;DDDDD;DDDDD;", righe);
        Assert.Equal(EsitoDelConfronto.Uguale, _lab.UltimoConfrontoDiEnr3!.Value.Esiti.Single(e => e.Nome == "Q9").Esito);
    }

    [Fact]
    public async Task PortaLeAttese_DiProceduraConLOvale_InRottaLaScritta()
    {
        await ApriConAsync(Aerovie, Attese, Modello());
        Assert.True(await _lab.ConfrontaEnr3Async(rotte: false), _lab.Rifiuto);
        var elkap = _lab.ProposteDelleAttese["ELKAP"];
        Assert.Equal("LIRF", elkap.Scali[0].Icao);
        Assert.Equal((4.0, "4 NM dalle note dell'AIP"), (elkap.TrattoNm, elkap.Tratto));
        Assert.Empty(_lab.ProposteDelleAttese["NUOVO"].Scali);

        Assert.True(_lab.PortaLeAttese([new("ELKAP", "LIRF", Ovale: true), new("NUOVO", null, Ovale: false)]), _lab.Rifiuto);

        var str = Righe("lirf.str");
        var i = Array.FindIndex(str, r => r.EndsWith(";HLD-ELKAP; ; ;2;", StringComparison.Ordinal));
        Assert.True(i > 0);
        Assert.StartsWith("LIRF;" + elkap.Scali[0].Piste + ";HLD-ELKAP", str[i], StringComparison.Ordinal);
        Assert.Equal(["//ELKAP", "ELKAP;ELKAP;"], str[(i + 1)..(i + 3)]);
        Assert.StartsWith("N042.10.00.000;E012.00.00.000;", str[i + 3], StringComparison.Ordinal);
        Assert.Equal(str[i + 3], str[i + 3 + 38]);
        var hold = Righe("HOLDENR.hold");
        Assert.Equal("HLD-NUOVO;N043.00.00.000;E011.00.00.000;NUOVO/090L-FL110;", hold[^1]);
        // Rifatto il confronto: NUOVO c'è; ELKAP sta in lirf.str come attesa di scalo.
        var esiti = _lab.UltimoConfrontoDiEnr3!.Value.Esiti.ToDictionary(e => e.Nome);
        Assert.Equal(EsitoDelConfronto.Uguale, esiti["NUOVO"].Esito);
        Assert.Contains(esiti["ELKAP"].Dettagli, d => d.Contains("attesa di scalo in lirf.str", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PortaLeAttese_LOvaleInRotta_NonAncora()
    {
        await ApriConAsync(Aerovie, Attese, Modello());
        Assert.True(await _lab.ConfrontaEnr3Async(rotte: false), _lab.Rifiuto);

        Assert.False(_lab.PortaLeAttese([new("NUOVO", null, Ovale: true)]));

        Assert.Contains("l'ovale in rotta", _lab.Rifiuto, StringComparison.Ordinal);
        Assert.False(_lab.Modifiche.CEQualcosa);
    }

    [Fact]
    public async Task SostituisciLAttesa_LaDescrizioneDellAip()
    {
        await ApriConAsync(Aerovie, Attese, Modello());
        Assert.True(await _lab.ConfrontaEnr3Async(rotte: false), _lab.Rifiuto);

        Assert.True(_lab.SostituisciLAttesa(_lab.UltimoConfrontoDiEnr3!.Value.Esiti.Single(e => e.Nome == "ABRUS")), _lab.Rifiuto);

        Assert.Contains("HLD-ABRUS;N036.51.40.000;E012.09.54.000;ABRUS/250L-3000;", Righe("HOLDENR.hold"));
        Assert.Equal(EsitoDelConfronto.Uguale, _lab.UltimoConfrontoDiEnr3!.Value.Esiti.Single(e => e.Nome == "ABRUS").Esito);
    }

    [Fact]
    public void LOvale_ChiusoColTrattoEIlRaggio()
    {
        var punto = new Position(42.0, 12.0);

        var ovale = AtteseNelSector.Ovale(punto, 180, TurnDirection.Right, 4, 210);

        Assert.Equal(39, ovale.Count);
        Assert.Equal(ovale[0], ovale[^1]);
        Assert.Equal(punto.Lat, ovale[0].Lat, 4);
        // A destra di una rotta verso sud c'è l'ovest: il lato opposto è a 2 raggi (210 kt: 1,11 NM) a ovest.
        var raggio = 210.0 / 60 * 2 / (2 * Math.PI);
        Assert.Equal(2 * raggio, global::Aip.Aurora.Sphere.DistanceNm(punto, ovale[18]), 2);
        Assert.True(ovale[18].Lon < punto.Lon);
        // L'inizio del tratto d'avvicinamento è 4 NM a nord.
        Assert.Equal(4, global::Aip.Aurora.Sphere.DistanceNm(punto, ovale[^2]), 2);
    }

    [Fact]
    public async Task SenzaRotteNellAip_SiDice()
    {
        await ApriConAsync(Aerovie, Attese, Modello() with { Routes = null });

        Assert.False(await _lab.ConfrontaEnr3Async(rotte: true));
        Assert.Contains("non ha rotte", _lab.Rifiuto, StringComparison.Ordinal);
    }
}
