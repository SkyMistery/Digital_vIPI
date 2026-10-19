using Aip;
using Vipi.SectorLab.Core.PdfAip;
using Vipi.SectorLab.Tests.Ui;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// Carta F6, slice 5b: il testo di AD 2 contro i <c>.ap</c>, i <c>.rw</c> e i <c>.frq</c>, in sola lettura, coi casi della
/// misura del 19 ottobre (la soglia della 10 di LICA a un centinaio di metri, la 12/30 di Orio con la rotta di 1,6° fuori,
/// una FIR diversa da <c>itrw.rw</c>, l'avvicinamento di Orio in una posizione di Malpensa, le frequenze che il sector non
/// ha). Dati inventati sulla forma di quelli veri.
/// </summary>
public sealed class ConfrontoDiAd2Tests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly SessioneDelLab _lab;
    private readonly string _espanso;

    public ConfrontoDiAd2Tests()
    {
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        _espanso = Path.Combine(_albero.Radice, "espanso");
    }

    public void Dispose() => _albero.Dispose();

    private const string ItAp =
        "LICA;46;6000;N038.54.30.000;E016.14.30.000;LAMEZIA TERME;\r\n"
        + "LIRN;290;7000;N040.54.04.000;E014.17.27.000;CAPODICHINO;\r\n";

    private const string LirrAp =
        "LICA;46;0;N038.54.30.000;E016.14.30.000;LAMEZIA TERME;\r\n";

    private const string ItRw =
        "//MENU MAPPE\r\n"
        + "LICA;MAPS;;0;0;0;0;N000.00.00.000;E000.00.00.000;N000.00.00.000;E000.00.00.000;\r\n"
        + "//PISTE\r\n"
        + "LICA;10;28;20;42;094.5;274.5;N038.54.24.710;E016.13.46.940;N038.54.12.520;E016.15.31.420;\r\n"
        + "LICA;04;22;30;30;040;220;N038.54.00.000;E016.14.00.000;N038.55.00.000;E016.15.00.000;\r\n"
        + "LIME;30;12;802;805;300;120;N045.40.29.520;E009.42.33.760;N045.40.40.530;E009.42.08.020;\r\n";

    private const string LirrRw =
        "///////PISTE\r\n"
        + "LICA;10;28;20;42;094.0;274.0;N038.54.25.210;E016.13.42.700;N038.54.12.520;E016.15.31.420;\r\n";

    private const string ItFreq =
        "LICA_TWR;125.900;LICA LIRR_SU_CTR;PREFS\\TWR.cpr;lica.atis;0;;datis-ad.datis\r\n"
        + "LICA_ES0_APP;118.800;LICA LIBC;PREFS\\TMA.cpr;default.atis;1;;datis-ad.datis\r\n"
        + "LICA_GND;121.700;LICA;PREFS\\TWR.cpr;lica.atis;0;;datis-ad.datis\r\n"
        + "LIMC_ANE_APP;126.750;LIMC LIME;PREFS\\TMA.cpr;limc.atis;0;;datis-arrdep.datis\r\n"
        + "LIME_TWR;125.875;LIME LIMM;PREFS\\TWR.cpr;default.atis;1;;datis-ad.datis\r\n";

    private const string LirrFrq =
        "LICA_TWR;125.900;LICA LIRR_SU_CTR;PREFS\\TWR.cpr;lica.atis;0;;datis-ad.datis\r\n";

    private static double Gradi(int g, int p, double s) => g + p / 60.0 + s / 3600.0;

    private static Aerodrome Lamezia() => new()
    {
        Icao = "LICA", Name = "LAMEZIA TERME", Arp = new Position(Gradi(38, 54, 30), Gradi(16, 14, 30)), Elevation = "46 FT",
        MagneticVariation = "4° E (2020.0)", AnnualChange = "6'E",
        Runways =
        [
            new Runway { Designator = "10", TrueBearing = "098.49°", Threshold = new Position(Gradi(38, 54, 25.21), Gradi(16, 13, 42.70)), ThresholdElevation = "19.3 FT / NIL" },
            new Runway { Designator = "28", TrueBearing = "278.51°", Threshold = new Position(Gradi(38, 54, 12.52), Gradi(16, 15, 31.42)), ThresholdElevation = "41.7 FT / 41.7 FT" },
        ],
        Communications =
        [
            new CommunicationFacility("Emergenza", null, "121.500", "H24"),
            new CommunicationFacility("APP", "Roma Radar", "118.800", "H24"),
            new CommunicationFacility("TWR", "Lamezia TWR", "122.100", "H24"),
            new CommunicationFacility("TWR", "Lamezia TWR", "125.900", "H24"),
            new CommunicationFacility("ATIS", "Lamezia Terminal Information", "119.930", "H24"),
        ],
    };

    private static Aerodrome Orio() => new()
    {
        Icao = "LIME", Name = "BERGAMO Orio al Serio", Arp = new Position(Gradi(45, 40, 8), Gradi(9, 42, 1)), Elevation = "782 FT",
        MagneticVariation = "3° E (2020.0)",
        Runways =
        [
            new Runway { Designator = "10", TrueBearing = "104.82°", Threshold = new Position(45.670528, 9.692506), ThresholdElevation = "780.5 FT / 782.0 FT" },
            new Runway { Designator = "28", TrueBearing = "284.85°", Threshold = new Position(45.664717, 9.723789), ThresholdElevation = "754.4 FT / 772.8 FT" },
            new Runway { Designator = "12", TrueBearing = "121.38°", Threshold = new Position(Gradi(45, 40, 40.53), Gradi(9, 42, 8.02)), ThresholdElevation = "804.7 FT / NIL" },
            new Runway { Designator = "30", TrueBearing = "301.38°", Threshold = new Position(Gradi(45, 40, 29.52), Gradi(9, 42, 33.76)), ThresholdElevation = "802.1 FT / NIL" },
        ],
        Communications =
        [
            new CommunicationFacility("APP", "Milano Radar", "CH 126.750", "H24", "1) Spaziatura 8.33 KHz"),
            new CommunicationFacility("TWR", "Orio TWR", "125.875", "H24"),
        ],
    };

    private static Aerodrome Capodichino() => new()
    {
        Icao = "LIRN", Name = "NAPOLI Capodichino", Arp = new Position(Gradi(40, 53, 4), Gradi(14, 17, 27)), Elevation = "294 FT",
    };

    private static Airspace Atz(string designatore, string pagina, string transizione) => new()
    {
        Section = "AD 2.17", Type = AirspaceType.Atz, Designator = designatore, TransitionAltitude = transizione,
        Upper = new VerticalLimit { Text = "2000 FT AMSL" }, Lower = new VerticalLimit { Text = "NIL" },
        Geometry = new CircleGeometry(new Position(Gradi(38, 54, 17), Gradi(16, 14, 30)), new Distance(5, DistanceUnit.Nm)),
        Page = new AipPage(pagina),
    };

    private static AipDocument Modello() => new()
    {
        State = "LI",
        Aerodromes = [Lamezia(), Orio(), Capodichino(), new Aerodrome { Icao = "LIRF", Name = "ROMA Fiumicino" }],
        Airspaces = [Atz("Lamezia Terme ATZ", "AD 2 LICA 1-11", "6000 FT"), Atz("Napoli ATZ", "AD 2 LIRN 1-9", "8000 FT")],
    };

    private async Task ApriAsync()
    {
        _albero.Scrivi("SectorFiles/Include/IT/OTHER/itap.ap", ItAp);
        _albero.Scrivi("SectorFiles/Include/IT/OTHER/lirr.ap", LirrAp);
        _albero.Scrivi("SectorFiles/Include/IT/OTHER/itrw.rw", ItRw);
        _albero.Scrivi("SectorFiles/Include/IT/OTHER/lirr.rw", LirrRw);
        _albero.Scrivi("SectorFiles/Include/IT/OTHER/itfreq.frq", ItFreq);
        _albero.Scrivi("SectorFiles/Include/IT/OTHER/lirr.frq", LirrFrq);
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        Assert.True(_lab.CambiaLAipEspanso(_espanso), _lab.Rifiuto);
        Directory.CreateDirectory(Path.Combine(_espanso, "2611"));
        await File.WriteAllTextAsync(Path.Combine(_espanso, "2611", "li.json"), AipJson.Serialize(Modello() with { Airac = "2611" }));
    }

    [Fact]
    public async Task GliScali_UnaRigaPerFile_PuntoElevazioneETransizione()
    {
        await ApriAsync();

        Assert.True(await _lab.ConfrontaAd2Async(ParteDiAd2.Scali), _lab.Rifiuto);

        var esiti = _lab.UltimoConfrontoDiAd2!.Value.Esiti;
        // LICA in itap.ap e in lirr.ap: due righe; in lirr.ap la transizione non è scritta (0), e non è una differenza.
        var lamezia = esiti.Where(e => e.Scalo == "LICA").ToList();
        Assert.Equal(2, lamezia.Count);
        Assert.All(lamezia, e => Assert.Equal(EsitoDelConfronto.Uguale, e.Esito));
        Assert.Contains(lamezia, e => e.File!.EndsWith("OTHER/lirr.ap", StringComparison.Ordinal));

        var napoli = Assert.Single(esiti, e => e.Scalo == "LIRN");
        Assert.Equal(EsitoDelConfronto.FormaDiversa, napoli.Esito);
        Assert.Contains(napoli.Dettagli, d => d.StartsWith("punto di riferimento a 1.00 NM", StringComparison.Ordinal));
        Assert.Contains("elevazione AIP 294 FT, sector 290 FT", napoli.Dettagli);
        Assert.Contains("altitudine di transizione AIP 8000 FT, sector 7000 FT", napoli.Dettagli);

        // Orio e Fiumicino il sector non li ha (nei .ap di questa prova).
        Assert.Equal(["LIME BERGAMO Orio al Serio", "LIRF ROMA Fiumicino"],
            esiti.Where(e => e.Esito == EsitoDelConfronto.MancaNelSector).Select(e => e.Nome));
        Assert.False(_lab.Modifiche.CEQualcosa);
    }

    [Fact]
    public async Task LePiste_SogliaRottaMagnetica_LeFirELeTestateCheMancano()
    {
        await ApriAsync();

        Assert.True(await _lab.ConfrontaAd2Async(ParteDiAd2.Piste), _lab.Rifiuto);

        var esiti = _lab.UltimoConfrontoDiAd2!.Value.Esiti;
        // La 10 di LICA: in itrw.rw a un centinaio di metri; in lirr.rw al punto dell'AIP, con la rotta di 0,5° sotto (dentro).
        var it = Assert.Single(esiti, e => e.Nome == "LICA 10/28" && e.File!.EndsWith("itrw.rw", StringComparison.Ordinal));
        Assert.Equal(EsitoDelConfronto.FormaDiversa, it.Esito);
        Assert.Contains("10: soglia del sector a 103 m da quella dell'AIP", it.Dettagli);
        Assert.DoesNotContain(it.Dettagli, d => d.StartsWith("28:", StringComparison.Ordinal));
        var lirr = Assert.Single(esiti, e => e.Nome == "LICA 10/28" && e.File!.EndsWith("lirr.rw", StringComparison.Ordinal));
        Assert.Equal(EsitoDelConfronto.Uguale, lirr.Esito);

        // Orio 30/12: 301.38° vera − 3° = 298.4°, il sector scrive 300.
        var orio = Assert.Single(esiti, e => e.Nome == "LIME 30/12");
        Assert.Equal(EsitoDelConfronto.FormaDiversa, orio.Esito);
        Assert.Contains("30: rotta magnetica dall'AIP 301.38° vera − 3° E (2020.0) = 298.4°, sector 300.0° (scarto 1.6°)", orio.Dettagli);

        // La 10/28 di Orio nessun .rw l'ha; la 04/22 di LICA l'AIP non l'ha.
        Assert.Equal(EsitoDelConfronto.MancaNelSector, Assert.Single(esiti, e => e.Nome == "LIME 10/28").Esito);
        var sola = Assert.Single(esiti, e => e.Esito == EsitoDelConfronto.SoloNelSector);
        Assert.Equal("LICA 04/22", sola.Nome);
        Assert.Contains("AD 2.12 non ha la 04 e 22", sola.Dettagli);
    }

    [Fact]
    public async Task LeFrequenze_PerPosizioneDelloScalo_EmergenzaEAtisNo()
    {
        await ApriAsync();

        Assert.True(await _lab.ConfrontaAd2Async(ParteDiAd2.Frequenze), _lab.Rifiuto);

        var esiti = _lab.UltimoConfrontoDiAd2!.Value.Esiti;
        var per = esiti.ToDictionary(e => e.Nome);
        Assert.Equal(EsitoDelConfronto.Uguale, per["LICA 125.900 Lamezia TWR"].Esito);
        Assert.EndsWith("OTHER/itfreq.frq", per["LICA 125.900 Lamezia TWR"].File, StringComparison.Ordinal);
        Assert.Contains("sector: LICA_TWR (itfreq.frq, lirr.frq)", per["LICA 125.900 Lamezia TWR"].Dettagli);
        Assert.Equal(EsitoDelConfronto.Uguale, per["LICA 118.800 Roma Radar"].Esito);
        Assert.Equal(EsitoDelConfronto.MancaNelSector, per["LICA 122.100 Lamezia TWR"].Esito);
        Assert.Equal(EsitoDelConfronto.SoloNelSector, per["LICA_GND 121.700"].Esito);
        // Orio: l'avvicinamento, stampato come canale a 8,33 kHz, è una posizione di Malpensa.
        Assert.Contains("sector: LIMC_ANE_APP (itfreq.frq)", per["LIME 126.750 Milano Radar"].Dettagli);
        // Emergenza e ATIS non si confrontano.
        Assert.DoesNotContain(esiti, e => e.Nome.Contains("121.500", StringComparison.Ordinal) || e.Nome.Contains("119.930", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SenzaScaliNellAip_IlConfrontoSiRifiuta()
    {
        await ApriAsync();
        await File.WriteAllTextAsync(Path.Combine(_espanso, "2611", "li.json"), AipJson.Serialize(new AipDocument { State = "LI", Airac = "2611" }));

        Assert.False(await _lab.ConfrontaAd2Async(ParteDiAd2.Piste));
        Assert.Contains("non ha scali", _lab.Rifiuto, StringComparison.Ordinal);
    }

    [Fact]
    public void LAtzDiAd217_VaColSuoScalo_SenzaDueVolteAtz()
    {
        var gruppi = ConfrontoDelleForme.Gruppi(FamigliaDelConfronto.Atz, [Atz("Lamezia Terme ATZ", "AD 2 LICA 1-11", "6000 FT")]);

        Assert.Equal("Lamezia Terme ATZ LICA", Assert.Single(gruppi).Nome);
    }

    [Theory]
    [InlineData("4° E (2020.0)", 4.0)]
    [InlineData("3º E (2015.0)", 3.0)]
    [InlineData("1°30' W", -1.5)]
    public void LaDeclinazione_EstPositiva(string stampata, double attesa) =>
        Assert.Equal(attesa, ConfrontoDiAd2.Declinazione(stampata)!.Value, 6);

    [Theory]
    [InlineData("16L", "34R")]
    [InlineData("07", "25")]
    [InlineData("6", "24")]
    [InlineData("18", "36")]
    [InlineData("36", "18")]
    [InlineData("17C", "35C")]
    public void LaTestataOpposta(string testata, string opposta) => Assert.Equal(opposta, ConfrontoDiAd2.Opposta(testata));
}
