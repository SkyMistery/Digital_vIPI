using Aip;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
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

    private async Task ApriAsync(AipDocument? modello = null)
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
        await File.WriteAllTextAsync(Path.Combine(_espanso, "2611", "li.json"), AipJson.Serialize((modello ?? Modello()) with { Airac = "2611" }));
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

    private string[] Righe(string file) => [.. _lab.RigheDiAdesso("SectorFiles/Include/IT/OTHER/" + file)];

    private DifferenzaDiAd2 Riga(string nome, string file) =>
        _lab.UltimoConfrontoDiAd2!.Value.Esiti.Single(e => e.Nome == nome && (e.File?.EndsWith("/" + file, StringComparison.Ordinal) ?? file.Length == 0));

    [Fact]
    public async Task SostituisciLaPista_SoloICampiDiversi_UnFileAllaVolta()
    {
        await ApriAsync();
        Assert.True(await _lab.ConfrontaAd2Async(ParteDiAd2.Piste), _lab.Rifiuto);

        // LICA 10/28 in itrw.rw: la soglia della 10 al punto dell'AIP; elevazioni e rotte restano (dentro le soglie).
        Assert.True(_lab.SostituisciInAd2(Riga("LICA 10/28", "itrw.rw")), _lab.Rifiuto);
        Assert.Contains("LICA;10;28;20;42;094.5;274.5;N038.54.25.210;E016.13.42.700;N038.54.12.520;E016.15.31.420;", Righe("itrw.rw"));
        Assert.Equal(EsitoDelConfronto.Uguale, Riga("LICA 10/28", "itrw.rw").Esito);
        Assert.StartsWith("LICA 10/28 (itrw.rw) dall'AIP 2611", _lab.DaAnnullare, StringComparison.Ordinal);

        // LIME 30/12: le rotte magnetiche dall'AIP, con un decimale; le soglie restano.
        Assert.True(_lab.SostituisciInAd2(Riga("LIME 30/12", "itrw.rw")), _lab.Rifiuto);
        Assert.Contains("LIME;30;12;802;805;298.4;118.4;N045.40.29.520;E009.42.33.760;N045.40.40.530;E009.42.08.020;", Righe("itrw.rw"));

        // Una riga uguale non si sostituisce; annullare rifà il confronto.
        Assert.False(_lab.SostituisciInAd2(Riga("LICA 10/28", "lirr.rw")));
        _lab.AnnullaTutte();
        Assert.Equal(EsitoDelConfronto.FormaDiversa, Riga("LICA 10/28", "itrw.rw").Esito);
        Assert.Equal(EsitoDelConfronto.FormaDiversa, Riga("LIME 30/12", "itrw.rw").Esito);
    }

    [Fact]
    public async Task SostituisciLoScalo_PuntoElevazioneETransizione()
    {
        await ApriAsync();
        Assert.True(await _lab.ConfrontaAd2Async(ParteDiAd2.Scali), _lab.Rifiuto);

        Assert.True(_lab.SostituisciInAd2(Riga("LIRN NAPOLI Capodichino", "itap.ap")), _lab.Rifiuto);

        Assert.Contains("LIRN;294;8000;N040.53.04.000;E014.17.27.000;CAPODICHINO;", Righe("itap.ap"));
        Assert.Equal(EsitoDelConfronto.Uguale, Riga("LIRN NAPOLI Capodichino", "itap.ap").Esito);
    }

    [Fact]
    public async Task PortaLaPista_InItrwDopoLePisteDelloScalo_DentroLeSezioneDellePiste()
    {
        await ApriAsync();
        Assert.True(await _lab.ConfrontaAd2Async(ParteDiAd2.Piste), _lab.Rifiuto);

        Assert.True(_lab.PortaLaPista(Riga("LIME 10/28", "")), _lab.Rifiuto);

        var righe = Righe("itrw.rw");
        var nuova = Array.FindIndex(righe, r => r.StartsWith("LIME;10;28;781;754;", StringComparison.Ordinal));
        Assert.True(nuova > 0, string.Join(" | ", righe));
        Assert.StartsWith("LIME;30;12;", righe[nuova - 1], StringComparison.Ordinal);
        Assert.EndsWith(";N045.40.13.901;E009.41.33.022;N045.39.52.981;E009.43.25.640;", righe[nuova], StringComparison.Ordinal);
        Assert.Equal(EsitoDelConfronto.Uguale, Riga("LIME 10/28", "itrw.rw").Esito);

        // Le frequenze restano in sola lettura, e una pista che c'è non si porta.
        Assert.False(_lab.PortaLaPista(Riga("LIME 10/28", "itrw.rw")));
    }

    // --- riordino R8c: scali e piste anche nei file delle FIR ---------------------------------------------------------

    private const string ItRwPath = "SectorFiles/Include/IT/OTHER/itrw.rw";
    private const string LirrRwPath = "SectorFiles/Include/IT/OTHER/lirr.rw";
    private const string ItApPath = "SectorFiles/Include/IT/OTHER/itap.ap";
    private const string LirrApPath = "SectorFiles/Include/IT/OTHER/lirr.ap";

    /// <summary>Lamezia con una pista che nessun .rw ha (la 16/34): LICA sta in lirr.ap, la FIR la propone.</summary>
    private static AipDocument ConLa1634() => Modello() with
    {
        Aerodromes =
        [
            Lamezia() with
            {
                Runways =
                [
                    .. Lamezia().Runways,
                    new Runway { Designator = "16", TrueBearing = "160.00°", Threshold = new Position(38.92, 16.23), ThresholdElevation = "40 FT / NIL" },
                    new Runway { Designator = "34", TrueBearing = "340.00°", Threshold = new Position(38.90, 16.24), ThresholdElevation = "30 FT / NIL" },
                ],
            },
            Orio(), Capodichino(),
        ],
    };

    [Fact]
    public async Task PortaLaPista_AncheNellaFir_ProposteAnteprimaEUnGestoSolo()
    {
        await ApriAsync(ConLa1634());
        Assert.True(await _lab.ConfrontaAd2Async(ParteDiAd2.Piste), _lab.Rifiuto);
        var riga = Riga("LICA 16/34", "");
        Assert.Equal([ItRwPath, LirrRwPath], _lab.FileInAd2(piste: true));
        Assert.Equal([ItRwPath, LirrRwPath], _lab.FileProposteInAd2(riga, piste: true));
        // Orio non sta in nessuna FIR: solo itrw.rw.
        Assert.Equal([ItRwPath], _lab.FileProposteInAd2(Riga("LIME 10/28", ""), piste: true));

        var anteprima = _lab.AnteprimaInAd2(riga, [ItRwPath, LirrRwPath], out var perche);
        Assert.Null(perche);
        Assert.Equal(["dopo LICA 04/22", "dopo LICA 10/28"], anteprima.Select(a => a.Dove));
        Assert.All(anteprima, a => Assert.StartsWith("LICA;16;34;40;30;", a.Riga, StringComparison.Ordinal));
        Assert.False(_lab.Modifiche.CEQualcosa);

        Assert.False(_lab.PortaLaPista(riga, [ItApPath]));
        Assert.Contains("non è un file delle piste", _lab.Rifiuto, StringComparison.Ordinal);
        Assert.True(_lab.PortaLaPista(riga, [ItRwPath, LirrRwPath]), _lab.Rifiuto);

        Assert.Contains(Righe("itrw.rw"), r => r.StartsWith("LICA;16;34;", StringComparison.Ordinal));
        var lirr = Righe("lirr.rw");
        Assert.StartsWith("LICA;16;34;", lirr[Array.FindIndex(lirr, r => r.StartsWith("LICA;10;28;", StringComparison.Ordinal)) + 1], StringComparison.Ordinal);
        Assert.StartsWith("LICA 16/34 dall'AIP 2611 in itrw.rw, lirr.rw", _lab.DaAnnullare, StringComparison.Ordinal);
        Assert.Equal(2, _lab.UltimoConfrontoDiAd2!.Value.Esiti.Count(e => e.Nome == "LICA 16/34" && e.Esito == EsitoDelConfronto.Uguale));

        // Un gesto solo: annullarlo toglie la pista da tutti e due.
        _lab.Annulla();
        Assert.False(_lab.Modifiche.CEQualcosa);
    }

    [Fact]
    public async Task PortaLoScalo_NelFileDiTuttaItaliaENellaFirScelta()
    {
        await ApriAsync();
        Assert.True(await _lab.ConfrontaAd2Async(ParteDiAd2.Scali), _lab.Rifiuto);
        var orio = Riga("LIME BERGAMO Orio al Serio", "");
        Assert.Equal([ItApPath], _lab.FileProposteInAd2(orio, piste: false));
        // Fiumicino, nell'AIP di prova, non ha il punto di riferimento: non si porta, e si dice perché.
        Assert.Empty(_lab.AnteprimaInAd2(Riga("LIRF ROMA Fiumicino", ""), [ItApPath], out var perche));
        Assert.Contains("punto di riferimento", perche, StringComparison.Ordinal);

        Assert.True(_lab.PortaLoScalo(orio, [ItApPath, LirrApPath]), _lab.Rifiuto);

        const string nuova = "LIME;782;0;N045.40.08.000;E009.42.01.000;BERGAMO ORIO AL SERIO;";
        var it = Righe("itap.ap");
        Assert.Equal(nuova, it[Array.FindIndex(it, r => r.StartsWith("LICA;", StringComparison.Ordinal)) + 1]);
        Assert.Contains(nuova, Righe("lirr.ap"));
        Assert.Equal(2, _lab.UltimoConfrontoDiAd2!.Value.Esiti.Count(e => e.Scalo == "LIME" && e.Esito == EsitoDelConfronto.Uguale));
    }

    [Fact]
    public async Task SostituisciInTuttiIFile_LaStessaVoceNelleFir()
    {
        // Lamezia a 50 piedi: la riga di itap.ap e quella di lirr.ap sono diverse tutte e due.
        await ApriAsync(Modello() with { Aerodromes = [Lamezia() with { Elevation = "50 FT" }, Orio(), Capodichino()] });
        Assert.True(await _lab.ConfrontaAd2Async(ParteDiAd2.Scali), _lab.Rifiuto);
        var lamezia = Riga("LICA LAMEZIA TERME", "itap.ap");
        Assert.Equal(2, _lab.CopieDiversaInAd2(lamezia).Count);
        Assert.Single(_lab.CopieDiversaInAd2(Riga("LIRN NAPOLI Capodichino", "itap.ap")));

        Assert.True(_lab.SostituisciOvunqueInAd2(lamezia), _lab.Rifiuto);

        Assert.Contains("LICA;50;6000;N038.54.30.000;E016.14.30.000;LAMEZIA TERME;", Righe("itap.ap"));
        Assert.Contains("LICA;50;0;N038.54.30.000;E016.14.30.000;LAMEZIA TERME;", Righe("lirr.ap"));
        Assert.All(_lab.UltimoConfrontoDiAd2!.Value.Esiti.Where(e => e.Scalo == "LICA"), e => Assert.Equal(EsitoDelConfronto.Uguale, e.Esito));
        Assert.Equal(EsitoDelConfronto.FormaDiversa, Riga("LIRN NAPOLI Capodichino", "itap.ap").Esito);
        Assert.StartsWith("LICA LAMEZIA TERME in 2 file", _lab.DaAnnullare, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NelConfronti_PortaLaPista_ConLeCaselleDeiFile()
    {
        await ApriAsync(ConLa1634());
        using var contesto = new Bunit.TestContext();
        contesto.JSInterop.Mode = JSRuntimeMode.Loose;
        contesto.Services.AddSingleton(_lab);
        var pagina = contesto.RenderComponent<Vipi.SectorLab.Ui.Components.ConfrontiVista>();
        pagina.Find("[data-sezione='piste']").Click();
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-tasto='porta-pista']")));

        await pagina.InvokeAsync(() => pagina.Find("[data-riga] [data-tasto='porta-pista']").Click());
        // Proposte: itrw.rw e lirr.rw, perché LICA sta in lirr.ap; Orio (l'altra riga) ha ancora il suo tasto.
        var caselle = pagina.FindAll("[data-casella='file-ad2']");
        Assert.Equal([true, true], caselle.Select(c => c.HasAttribute("checked")));
        Assert.Equal("2", pagina.Find("[data-anteprima-metti]").GetAttribute("data-anteprima-metti"));
        pagina.Find("[data-casella='file-ad2'][value$='lirr.rw']").Change(false);
        pagina.WaitForAssertion(() => Assert.Equal("1", pagina.Find("[data-anteprima-metti]").GetAttribute("data-anteprima-metti")));

        await pagina.InvokeAsync(() => pagina.Find("[data-tasto='metti-nel-file']").Click());

        Assert.Equal([ItRwPath], _lab.Modifiche.Voci.Select(m => m.File).Distinct());
        pagina.WaitForAssertion(() => Assert.Empty(pagina.FindAll("[data-metti-nel-file]")));
        Assert.Empty(pagina.FindAll("[data-confronto-rifiutato]"));
    }

    private static Navaid Aiuto(string sigla, string tipo, NavaidKind genere, double lat, double lon, string? frequenza, string? canale, string sezione = "AD 2.19") => new()
    {
        Section = sezione, Designator = sigla, Name = sigla, Type = tipo, Kind = genere, Frequency = frequenza, Channel = canale,
        Position = new Position(lat, lon), Components = [new NavaidComponent(null, new Position(lat, lon))],
    };

    [Fact]
    public async Task LeRadioassistenzeDiAd219_ComeEnr41_EDoveEnr41LaStampaDiversa()
    {
        // CDC: in AD 2.19 al decimo di secondo, in ENR 4.1 al secondo (21.1" e 21"): non è una discordanza.
        // MMP: in AD 2.19 di due scali, una riga; ENR 4.1 con un'altra frequenza. PNZ: il canale del sector è un altro.
        // GEN: il sector non l'ha.
        var cdc = Aiuto("CDC", "VORTAC", NavaidKind.Vortac, Gradi(38, 45, 21.1), Gradi(16, 22, 8.9), "117.30 MHZ", "120X");
        var mmp = Aiuto("MMP", "VOR/DME", NavaidKind.VorDme, Gradi(45, 38, 26.8), Gradi(8, 44, 5.4), "113.35 MHZ", "80Y");
        var modello = Modello() with
        {
            Aerodromes =
            [
                Lamezia() with { NavigationAids = [cdc] },
                Orio() with { NavigationAids = [mmp, Aiuto("GEN", "VOR/DME", NavaidKind.VorDme, 44.42444, 9.08233, "112.80 MHZ", "75X")], Ils = [new Ils { Runway = "28", Designator = "IBRM", Components = [] }] },
                new Aerodrome { Icao = "LIMC", Name = "MILANO Malpensa", NavigationAids = [mmp] },
                Capodichino() with { NavigationAids = [Aiuto("PNZ", "VORTAC", NavaidKind.Vortac, Gradi(40, 54, 42.7), Gradi(12, 57, 26.7), "114.60 MHZ", "93X")] },
            ],
            Navaids =
            [
                Aiuto("CDC", "VORTAC", NavaidKind.Vortac, Gradi(38, 45, 21), Gradi(16, 22, 9), "117.30 MHZ", "120X", "ENR 4.1"),
                Aiuto("MMP", "VOR/DME", NavaidKind.VorDme, Gradi(45, 38, 26.8), Gradi(8, 44, 5.4), "113.85 MHZ", "80Y", "ENR 4.1"),
            ],
        };
        _albero.Scrivi("SectorFiles/Include/IT/NAVAIDS/itvor.vor",
            "CDC;117.30;N038.45.21.100;E016.22.08.900;0;2;120X;HLD-CDC;\r\n"
            + "MMP;113.35;N045.38.26.800;E008.44.05.400;;;;\r\n"
            + "PNZ;114.60;N040.54.42.700;E012.57.26.700;0;2;87X;\r\n");
        await ApriAsync();
        await File.WriteAllTextAsync(Path.Combine(_espanso, "2611", "li.json"), AipJson.Serialize(modello with { Airac = "2611" }));

        Assert.True(await _lab.ConfrontaAd2Async(ParteDiAd2.Radioassistenze), _lab.Rifiuto);

        var esiti = _lab.UltimoConfrontoDiAd2!.Value.Esiti;
        var per = esiti.ToDictionary(e => e.Nome);
        Assert.Equal(["CDC VORTAC", "GEN VOR/DME", "MMP VOR/DME", "PNZ VORTAC"], per.Keys.Order(StringComparer.Ordinal));
        Assert.Equal((EsitoDelConfronto.Uguale, false), (per["CDC VORTAC"].Esito, per["CDC VORTAC"].DiversaInEnr41));
        Assert.Contains("uguale in ENR 4.1", per["CDC VORTAC"].Dettagli);
        Assert.StartsWith("AD 2.19 di LIMC e LIME: MMP", per["MMP VOR/DME"].Dettagli[0], StringComparison.Ordinal);
        Assert.True(per["MMP VOR/DME"].DiversaInEnr41);
        Assert.Contains("ENR 4.1 la stampa diversa: frequenza 113.85 MHZ", per["MMP VOR/DME"].Dettagli);
        Assert.Equal(EsitoDelConfronto.FormaDiversa, per["PNZ VORTAC"].Esito);
        Assert.Contains("canale AIP 93X, sector 87X", per["PNZ VORTAC"].Dettagli);
        Assert.Equal(EsitoDelConfronto.MancaNelSector, per["GEN VOR/DME"].Esito);
        Assert.Contains("non in ENR 4.1: è dello scalo", per["GEN VOR/DME"].Dettagli);
        Assert.Equal(1, _lab.IlsDellAipDiAd2);
        // «Sostituisci» degli scali e delle piste non vale qui.
        Assert.False(_lab.SostituisciInAd2(per["PNZ VORTAC"]));

        // Slice 5f: i gesti di ENR 4 sulle righe di AD 2.19. PNZ: il canale; GEN: la riga nuova, in ordine di nome.
        Assert.True(_lab.SostituisciLaRadioassistenzaDiAd2(per["PNZ VORTAC"]), _lab.Rifiuto);
        Assert.Contains("PNZ;114.60;N040.54.42.700;E012.57.26.700;0;2;93X;", _lab.RigheDiAdesso("SectorFiles/Include/IT/NAVAIDS/itvor.vor"));
        Assert.True(_lab.PortaLeRadioassistenzeDiAd2(["GEN VOR/DME"]), _lab.Rifiuto);
        var vor = _lab.RigheDiAdesso("SectorFiles/Include/IT/NAVAIDS/itvor.vor").ToList();
        var gen = vor.FindIndex(r => r.StartsWith("GEN;112.80;", StringComparison.Ordinal));
        Assert.True(gen > 0, string.Join(" | ", vor));
        Assert.EndsWith(";0;1;75X;", vor[gen], StringComparison.Ordinal);
        Assert.StartsWith("MMP;", vor[gen + 1], StringComparison.Ordinal);
        Assert.All(_lab.UltimoConfrontoDiAd2!.Value.Esiti, e => Assert.Equal(EsitoDelConfronto.Uguale, e.Esito));
        Assert.False(_lab.PortaLeRadioassistenzeDiAd2(["CDC VORTAC"]));

        _lab.AnnullaTutte();
        Assert.Equal(EsitoDelConfronto.FormaDiversa, _lab.UltimoConfrontoDiAd2!.Value.Esiti.Single(e => e.Nome == "PNZ VORTAC").Esito);
        Assert.Equal(EsitoDelConfronto.MancaNelSector, _lab.UltimoConfrontoDiAd2!.Value.Esiti.Single(e => e.Nome == "GEN VOR/DME").Esito);
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
