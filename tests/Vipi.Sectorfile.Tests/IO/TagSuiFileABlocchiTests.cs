using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Lotto «Subito», slice 1d: i tag <c>//@</c> sui file a blocchi (carta «file per file» §M) — zone MVA (E1), aerovie
/// (B1, B2, B5), settori dinamici (D5, D9), confini e configurazioni (J6, J7), <c>.artcc</c>, <c>.geo</c> e
/// <c>.pol</c> (I2, H10, G5). Tre regole nuove nel motore:
/// <list type="bullet">
/// <item>un <c>//@</c> chiude sempre il record aperto (prima i lettori di MVA, ARTCC, TFL, confini e POL tenevano ogni
/// commento dentro il blocco aperto, e un <c>//@END</c> senza riga vuota finiva nel record);</item>
/// <item>un blocco <c>//@START</c> … <c>//@END</c> tiene <b>tutti</b> i record che ha dentro, se hanno il suo nome o
/// nessun nome (la zona MVA fatta di più pezzi, il gruppo di segmenti di un <c>.geo</c>, l'aerovia spezzata dai
/// <c>BREAK</c>);</item>
/// <item>un record senza nome proprio (<c>.pol</c>, <c>.geo</c> di scalo, la riga <c>BREAK</c>) prende quello del
/// blocco (§M: «nei file senza nome nelle righe di dati il nome del blocco è il nome del gruppo»).</item>
/// </list>
/// </summary>
public sealed class TagSuiFileABlocchiTests
{
    private readonly CollectingWarnings _warnings = new();

    private static string Righe(params string[] righe) => string.Join("\r\n", righe) + "\r\n";

    private ParseResult<MvaSector> Mva(string testo) => new MvaEnrouteParser(_warnings).Parse(ParserTestHelpers.Read(testo), "limm.mva");

    private ParseResult<MvaSector> MvaDiScalo(string testo) => new MvaAirportParser(_warnings).Parse(ParserTestHelpers.Read(testo), "liba.mva");

    private ParseResult<Airway> Aerovie(string testo) => new AirwayParser(_warnings).Parse(ParserTestHelpers.Read(testo), "itawlow.lairway");

    private ParseResult<TflSector> Tfl(string testo) => new TflParser(_warnings).Parse(ParserTestHelpers.Read(testo), "limmapp.tfl");

    private ParseResult<FicSector> Fic(string testo) => new FicParser(_warnings).Parse(ParserTestHelpers.Read(testo), "limmfic.tfl");

    private ParseResult<StaticBoundaryGroup> Hartcc(string testo) => new HartccParser(_warnings).Parse(ParserTestHelpers.Read(testo), "lirr.hartcc");

    private ParseResult<ElementoArtcc> Artcc(string testo) => new ArtccParser(_warnings).Parse(ParserTestHelpers.Read(testo), "FRA.artcc", new ColorPalette());

    private ParseResult<Line> Geo(string testo) => new GeoParser(_warnings).Parse(ParserTestHelpers.Read(testo), "liba.geo", new ColorPalette());

    private ParseResult<Polygon> Pol(string testo) => new PolParser(_warnings).Parse(ParserTestHelpers.Read(testo), "lirf_ad_gnd.pol");

    private static string? NomeDi<T>(T record) => Metadati.NomeDelRecord(record!);

    private static readonly string[] ZonaTorino =
    [
        "L;LIMM;N045.28.00.000;E008.32.00.000;25;8;",
        "T;LIMM;N045.35.12.000;E008.31.14.000;LIMM;",
        "T;LIMM;N045.33.29.000;E008.28.03.000;LIMM;",
        "T;LIMM;N045.29.58.000;E008.27.45.000;LIMM;",
        "T;DUMMY;N045.29.58.000;E008.27.45.000;",
    ];

    private static readonly string[] ZonaNovara =
    [
        "L;LIMM;N045.30.00.000;E008.40.00.000;30;8;",
        "T;LIMM;N045.36.00.000;E008.38.00.000;LIMM;",
        "T;LIMM;N045.34.00.000;E008.45.00.000;LIMM;",
        "T;LIMM;N045.31.00.000;E008.44.00.000;LIMM;",
        "T;DUMMY;N045.31.00.000;E008.44.00.000;",
    ];

    // E1: due zone scritte di fila, senza righe vuote. Prima il //@END della prima e la dichiarazione della seconda
    // finivano dentro il blocco MVA aperto, e le due zone diventavano un record solo.
    [Fact]
    public void DueZoneMvaDiFilaRestanoDueRecordCoiLoroTag()
    {
        var letto = Mva(Righe([
            "//@\"LIMM\" zone=\"Torino\"", "//@START", .. ZonaTorino, "//@END \"LIMM\"",
            "//@\"LIMM\" zone=Novara", "//@START", .. ZonaNovara, "//@END \"LIMM\""]));

        Assert.Equal(2, letto.Records.Count);
        Assert.All(letto.Records, r => Assert.Equal(4, r.Vertices.Count + r.LabelAnchors.Count));
        var metadati = Metadati.Leggi(letto);
        Assert.Empty(metadati.Problemi);
        Assert.Equal(["Torino", "Novara"], metadati.Record.Select(m => Metadati.Testo(m.Chiavi["zone"])));
        Assert.All(metadati.Record, m => Assert.True(m.Delimitato));
    }

    // Il nome d'aggancio di un blocco MVA è il 2° campo della sua prima riga (il gruppo della MVA Selection, o il
    // nome della zona nei .mva di scalo), anche se la riga è commentata; un separatore DUMMY non lo è.
    [Fact]
    public void IlNomeDiUnBloccoMvaEIlSecondoCampoDellaPrimaRiga()
    {
        Assert.Equal("LIMM", Metadati.NomeDelRecord(Mva(Righe(ZonaTorino)).Records.Single()));
        Assert.Equal("CERCHIO-BA", Metadati.NomeDelRecord(MvaDiScalo(Righe(
            "T;DUMMY;N040.18.05.408;E015.39.59.973;",
            "//T;CERCHIO-BA;N040.18.05.408;E015.39.59.973;",
            "T;CERCHIO-BA;N040.18.19.003;E015.31.32.103;")).Records.Single()));
    }

    // E1: «cosa sta nella zona lo decide l'utente» — un blocco può tenere più pezzi, separati da righe vuote.
    [Fact]
    public void UnaZonaMvaPuoTenerePiuPezzi()
    {
        var letto = Mva(Righe(["//@\"LIMM\" zone=Torino", "//@START", .. ZonaTorino, "", .. ZonaNovara, "//@END \"LIMM\""]));

        var metadati = Metadati.Leggi(letto);

        Assert.Empty(metadati.Problemi);
        var zona = Assert.Single(metadati.Record);
        Assert.Equal(letto.Records, zona.Records);
        Assert.Same(zona, metadati.Di(letto.Records[1]));
    }

    [Fact]
    public void UnPezzoDiUnAltroGruppoDentroLaZonaEUnErrore()
    {
        string[] diRoma = [.. ZonaNovara.Select(r => r.Replace("LIMM", "LIRR", StringComparison.Ordinal))];
        var letto = Mva(Righe(["//@\"LIMM\" zone=Torino", "//@START", .. ZonaTorino, "", .. diRoma, "//@END \"LIMM\""]));

        var metadati = Metadati.Leggi(letto);

        Assert.Equal(TipoDiProblemaDeiMetadati.NomeNonCombacia, Assert.Single(metadati.Problemi).Tipo);
        Assert.Null(metadati.Di(letto.Records[1]));
    }

    [Fact]
    public void UnaDichiarazioneDentroUnBloccoEUnErrore()
    {
        var letto = Mva(Righe(["//@\"LIMM\" zone=Torino", "//@START", .. ZonaTorino, "", "//@\"LIMM\" zone=Novara", .. ZonaNovara, "//@END \"LIMM\""]));

        Assert.Contains(Metadati.Leggi(letto).Problemi, p => p.Tipo == TipoDiProblemaDeiMetadati.DichiarazioneNelBlocco && p.EUnErrore);
    }

    private static readonly string[] L81 =
    [
        "T;L81;TOMGI;TOMGI;",
        "T;L81;GEMVI;GEMVI;",
        "T;BREAK;GEMVI;GEMVI; //discontinuity (creates a break)",
        "T;L81;MATED;MATED;",
        "T;L81;DOGUS;DOGUS;",
    ];

    // B1 + B5: un blocco per aerovia. Il BREAK la spezza in tre record, ma il blocco li tiene tutti.
    [Fact]
    public void IlBloccoDiUnAeroviaTieneAncheIPezziDopoIlBreak()
    {
        var letto = Aerovie(Righe(["//@\"L81\" locked=si", "//@START", .. L81, "//@END \"L81\"", "T;L613;GARGA;GARGA;"]));

        var metadati = Metadati.Leggi(letto);

        Assert.Empty(metadati.Problemi);
        var l81 = Assert.Single(metadati.Record);
        Assert.Equal(["L81", "BREAK", "L81"], l81.Records.Select(a => a.Name));
        Assert.Null(metadati.Di(letto.Records[^1]));
        Assert.Null(Metadati.NomeDelRecord(letto.Records[1]));
    }

    // B2: il tag del punto che apre il tratto — anche il primo — sta nell'aerovia e non la spezza.
    [Fact]
    public void IlTagDiUnTrattoNonSpezzaLAerovia()
    {
        var letto = Aerovie(Righe(
            "//@@\"TOMGI\" dir=fwd lower=FL95 upper=FL195",
            "T;L81;TOMGI;TOMGI;",
            "//@@\"GEMVI\" dir=both lower=5000ft upper=FL195",
            "T;L81;GEMVI;GEMVI;",
            "T;L81;MATED;MATED;"));

        var aerovia = Assert.Single(letto.Records);
        Assert.Equal(["TOMGI", "GEMVI", "MATED"], aerovia.FixLabels);
        var metadati = Metadati.Leggi(letto);
        Assert.Empty(metadati.Problemi);
        Assert.Equal([("TOMGI", 1), ("GEMVI", 3)], metadati.PuntiDi(aerovia).Select(p => (p.Punto, p.RigaDelPunto)));
    }

    [Fact]
    public void IlTagDiUnTrattoSiScriveSulPrimoPuntoESiToglie()
    {
        string[] righe = ["T;L613;GARGA;GARGA;", "L;L613;N041.00.00.000;E015.00.00.000;"];
        var letto = Aerovie(Righe(righe));

        var scritto = Metadati.ScriviIlPunto(letto, letto.Records[0], 0, new Dictionary<string, string> { ["dir"] = "back", ["upper"] = "FL195" });
        var dopo = new FileSaverOrchestrator().Righe(scritto, new HashSet<Airway>(), new AirwaySaver());

        Assert.Equal(["//@@\"GARGA\" dir=back upper=FL195", righe[0], righe[1]], dopo);
        var riletto = Aerovie(Righe([.. dopo]));
        Assert.Single(riletto.Records);
        Assert.Equal(righe, new FileSaverOrchestrator().Righe(Metadati.TogliIlPunto(riletto, riletto.Records[0], 1),
            new HashSet<Airway>(), new AirwaySaver()));
    }

    [Theory]
    [InlineData("T;L613;GARGA;GARGA;", "GARGA")]
    [InlineData("L;L613;N041.00.00.000;E015.00.00.000;", "N041.00.00.000;E015.00.00.000")]
    [InlineData("T;L613;", null)]
    public void LaChiaveDelPuntoDiUnAeroviaStaNelTerzoEQuartoCampo(string riga, string? chiave)
        => Assert.Equal(chiave, Metadati.ChiaveDelPunto<Airway>(riga));

    private ParseResult<RottaVfr> Vrt(string testo) => new VrtParser(_warnings).Parse(ParserTestHelpers.Read(testo), "liml.vrt");

    // F8 + S6 (slice 1e): le rotte VFR di scalo hanno verso e quote per tratto come le aerovie; il //@@ non le spezza.
    [Fact]
    public void IlTagDiUnTrattoNonSpezzaLaRottaVfr()
    {
        var letto = Vrt(Righe(
            "//@\"1\" note=\"rotta nord\"",
            "//@@\"ROGOREDO\" dir=fwd upper=1500ft",
            "1;ROGOREDO;ROGOREDO;",
            "//@@\"ROZZANO\" dir=both lower=1000ft upper=1500ft",
            "1;ROZZANO;ROZZANO;",
            "1;N045.20.00.000;E009.10.00.000;",
            "2;SPINO D'ADDA;SPINO D'ADDA;"));

        Assert.Equal(["1", "2"], letto.Records.Select(r => r.Numero));
        Assert.Equal(3, letto.Records[0].Punti.Count);
        var metadati = Metadati.Leggi(letto);
        Assert.Empty(metadati.Problemi);
        Assert.Equal("1", metadati.Record.Single().Nome);
        Assert.Equal([("ROGOREDO", 1), ("ROZZANO", 3)], metadati.PuntiDi(letto.Records[0]).Select(p => (p.Punto, p.RigaDelPunto)));
    }

    [Fact]
    public void IlTagDiUnTrattoVfrSiScriveSulPrimoPuntoESiToglie()
    {
        string[] righe = ["2;SPINO D'ADDA;SPINO D'ADDA;", "2;IDROSCALO;IDROSCALO;"];
        var letto = Vrt(Righe(righe));

        var scritto = Metadati.ScriviIlPunto(letto, letto.Records[0], 0, new Dictionary<string, string> { ["lower"] = "1000ft" });
        var dopo = new FileSaverOrchestrator().Righe(scritto, new HashSet<RottaVfr>(), new VrtSaver());

        Assert.Equal(["//@@\"SPINO D'ADDA\" lower=1000ft", righe[0], righe[1]], dopo);
        var riletto = Vrt(Righe([.. dopo]));
        Assert.Single(riletto.Records);
        Assert.Equal(righe, new FileSaverOrchestrator().Righe(Metadati.TogliIlPunto(riletto, riletto.Records[0], 1),
            new HashSet<RottaVfr>(), new VrtSaver()));
    }

    [Theory]
    [InlineData("1;ROGOREDO;ROGOREDO;", "ROGOREDO")]
    [InlineData("1;N045.20.00.000;E009.10.00.000;", "N045.20.00.000;E009.10.00.000")]
    [InlineData("1;ROGOREDO;", null)]
    public void LaChiaveDelPuntoDiUnaRottaVfrStaDopoIlNumero(string riga, string? chiave)
        => Assert.Equal(chiave, Metadati.ChiaveDelPunto<RottaVfr>(riga));

    [Fact]
    public void UnaRottaVfrHaIlCatalogoDelleAerovie()
    {
        Assert.NotNull(Metadati.ProblemiDi(Vrt(Righe("1;ROGOREDO;ROGOREDO;"))));
        Assert.Equal(TipoDiProblemaDeiMetadati.ChiaveSconosciuta,
            Assert.Single(Metadati.Leggi(Vrt(Righe("//@@\"ROGOREDO\" alt=+1500", "1;ROGOREDO;ROGOREDO;"))).Problemi).Tipo);
        MettiETogli(Vrt(Righe("//rotte", "1;ROGOREDO;ROGOREDO;", "1;ROZZANO;ROZZANO;", "2;TEANO;TEANO;")), new VrtSaver());
    }

    private static readonly string[] SettoreLipx =
    [
        "LIPX_ES0_APP;APP;1;APP;1;",
        "N045.33.57.000;E010.21.30.000;",
        "N045.33.40.000;E010.56.28.000;",
        "N045.29.57.000;E011.08.00.000;",
    ];

    // D5 + D9: la famiglia di forme, i limiti e la classe del settore; il //@END subito prima della testa dopo.
    [Fact]
    public void UnSettoreDinamicoPortaFormaLimitiEClasse()
    {
        string[] dopo = [.. SettoreLipx.Select(r => r.Replace("ES0", "WS0", StringComparison.Ordinal))];
        var letto = Tfl(Righe(["//@\"LIPX_ES0_APP\" form=LIPX-ES lower=FL95 upper=FL195 class=D", "//@START", .. SettoreLipx, "//@END \"LIPX_ES0_APP\"", .. dopo]));

        Assert.Equal(2, letto.Records.Count);
        Assert.All(letto.Records, s => Assert.Equal(3, s.Vertices.Count));
        var metadati = Metadati.Leggi(letto);
        Assert.Empty(metadati.Problemi);
        Assert.Equal(("LIPX-ES", "D"), (metadati.Record.Single().Chiavi["form"], metadati.Record.Single().Chiavi["class"]));
    }

    // Il nome della forma di un .fic è il commento che accompagna il blocco: mai una riga di tag.
    [Fact]
    public void LaRigaDiTagNonDiventaIlNomeDellaFormaFic()
    {
        var letto = Fic(Righe(["//GARDA", "//@\"LIPX_ES0_APP\" form=GARDA", "//@START", .. SettoreLipx, "//@END \"LIPX_ES0_APP\""]));

        Assert.Equal("GARDA", Assert.Single(letto.Records).ShapeLabel);
        Assert.Empty(Metadati.Leggi(letto).Problemi);
    }

    // J6 + J7: la configurazione composta dai settori, coi nomi fra virgolette.
    [Fact]
    public void UnaConfigurazioneDeiConfiniPortaLeSueParti()
    {
        var letto = Hartcc(Righe(
            "//@\"RR CONF2\" compose=\"RR NE\",\"RR TS\" lower=FL195 upper=UNL class=C",
            "//@START",
            "T;RR CONF2;N044.23.17.000;E011.07.44.000;",
            "T;RR CONF2;TIPNI;TIPNI;",
            "//@END \"RR CONF2\"",
            "T;RR NE;N044.23.17.000;E011.07.44.000;",
            "T;RR NE;OTNUN;OTNUN;"));

        Assert.Equal(["RR CONF2", "RR NE"], letto.Records.Select(g => g.Name));
        var metadati = Metadati.Leggi(letto);
        Assert.Empty(metadati.Problemi);
        Assert.Equal(["RR NE", "RR TS"], Metadati.ElencoDellaComposta(metadati.Record.Single().Chiavi[Metadati.Compose])!.Select(v => v.Nome));
    }

    [Fact]
    public void IConfiniDiUnArtccHannoIlLoroBlocco()
    {
        var letto = Artcc(Righe(
            "//@\"FRA BDRY\" locked=si",
            "//@START",
            "T;FRA BDRY;N044.23.17.000;E011.07.44.000;",
            "T;FRA BDRY;LUSIL;LUSIL;",
            "//@END \"FRA BDRY\"",
            "L;ABDAB;N037.53.21.000;E010.37.43.000;8;"));

        Assert.Equal(2, letto.Records.Count);
        var metadati = Metadati.Leggi(letto);
        Assert.Empty(metadati.Problemi);
        Assert.IsType<StaticBoundaryGroup>(metadati.Record.Single().Record);
        Assert.Equal("ABDAB", Metadati.NomeDelRecord(letto.Records[1]));
    }

    private static readonly string[] Confine =
    [
        "//ad_boun",
        "N041.32.53.399;E015.41.51.996;N041.32.50.417;E015.42.03.400;BUILDING;",
        "N041.32.50.417;E015.42.03.400;N041.32.53.992;E015.42.14.118;BUILDING;",
        "N041.32.53.992;E015.42.14.118;N041.32.38.088;E015.43.05.992;BUILDING;",
    ];

    // I2/H10: in un .geo il blocco è il gruppo di segmenti, col nome del gruppo; si scrive e si toglie intero.
    [Fact]
    public void IlGruppoDiUnGeoSiDichiaraIntero()
    {
        var letto = Geo(Righe(["", .. Confine, ""]));

        var scritto = Metadati.ScriviIlBlocco(letto, letto.Records[0], letto.Records[^1], NomeDi, "ad_boun",
            new Dictionary<string, string> { ["form"] = "LIBA-AD" });
        var dopo = new FileSaverOrchestrator().Righe(scritto, new HashSet<Line>(), new GeoSaver());

        Assert.Equal(["", Confine[0], "//@\"ad_boun\" form=LIBA-AD", "//@START", Confine[1], Confine[2], Confine[3], "//@END \"ad_boun\"", ""], dopo);
        var riletto = Geo(Righe([.. dopo]));
        var metadati = Metadati.Leggi(riletto);
        Assert.Empty(metadati.Problemi);
        Assert.All(riletto.Records, r => Assert.Same(metadati.Record.Single(), metadati.Di(r)));
        Assert.Equal(["", .. Confine, ""], new FileSaverOrchestrator().Righe(Metadati.Togli(riletto, riletto.Records[1], NomeDi),
            new HashSet<Line>(), new GeoSaver()));
    }

    // G5: nelle aree P/R/D il nome c'è (6° campo), e il blocco deve averlo.
    [Fact]
    public void UnAreaDiUnAltroNomeNonEntraNelBlocco()
    {
        var letto = Geo(Righe(
            "//@\"D5A\" lower=SFC upper=FL195",
            "//@START",
            "N043.42.07.000;E007.50.15.000;N043.57.00.000;E008.20.00.000;DANGER;D5A;",
            "N043.57.00.000;E008.20.00.000;N043.56.27.000;E008.37.28.000;DANGER;D5B;",
            "//@END \"D5A\""));

        var metadati = Metadati.Leggi(letto);

        Assert.Equal(TipoDiProblemaDeiMetadati.NomeNonCombacia, Assert.Single(metadati.Problemi).Tipo);
        Assert.Single(metadati.Record.Single().Records);
    }

    // Un //@ subito dopo la testa di un poligono non è il nome del poligono: chiude il blocco (prima ci restava dentro).
    [Fact]
    public void UnPoligonoPortaLaSuaFamigliaDiForme()
    {
        var letto = Pol(Righe(
            "//AD_BOUNDARY_Polygon",
            "//@\"AD_BOUNDARY_Polygon\" form=LIBA-AD",
            "//@START",
            "STATIC;GRASS;1;GRASS;",
            "N042.34.23.105;E012.34.58.263;",
            "N042.34.17.477;E012.34.57.622;",
            "N042.34.17.168;E012.35.02.364;",
            "//@END \"AD_BOUNDARY_Polygon\"",
            "STATIC;TAXIWAY;1;TAXIWAY;",
            "//@\"X\" note=prova",
            "N042.34.23.105;E012.34.58.263;"));

        Assert.Equal(3, letto.Records[0].Vertices.Count);
        Assert.Empty(letto.Records[1].Vertices);
        var metadati = Metadati.Leggi(letto);
        Assert.Equal("LIBA-AD", metadati.Di(letto.Records[0])!.Chiavi["form"]);
        Assert.Null(Metadati.NomeDelRecord(letto.Records[0]));
    }

    [Fact]
    public void UnRecordSenzaNomeSiDichiaraSoloColNomeDelBlocco()
    {
        var letto = Pol(Righe("STATIC;GRASS;1;GRASS;", "N042.34.23.105;E012.34.58.263;"));

        Assert.Throws<InvalidOperationException>(() => Metadati.Scrivi(letto, letto.Records[0], NomeDi, new Dictionary<string, string> { ["form"] = "X" }));
        var scritto = Metadati.ScriviIlBlocco(letto, letto.Records[0], letto.Records[0], NomeDi, "erba", new Dictionary<string, string> { ["form"] = "X" });
        Assert.Equal("erba", Metadati.Leggi(scritto).Di(scritto.Records[0])!.Nome);
    }

    [Fact]
    public void UnBloccoNonPrendeRecordDiUnAltroNome()
    {
        var letto = Mva(Righe([.. ZonaTorino, "", .. ZonaNovara.Select(r => r.Replace("LIMM", "LIRR", StringComparison.Ordinal))]));

        Assert.Throws<ArgumentException>(() => Metadati.ScriviIlBlocco(letto, letto.Records[0], letto.Records[1], NomeDi, "LIMM",
            new Dictionary<string, string> { ["zone"] = "Torino" }));
    }

    // Scrivere su un record di un blocco a più pezzi cambia la sola dichiarazione; togliere toglie il blocco intero.
    [Fact]
    public void UnBloccoAPiuPezziSiCambiaESiToglieIntero()
    {
        string[] prima = [.. ZonaTorino, "", .. ZonaNovara];
        var letto = Mva(Righe(prima));
        var zona = Metadati.ScriviIlBlocco(letto, letto.Records[0], letto.Records[1], NomeDi, "LIMM", new Dictionary<string, string> { ["zone"] = "Torino" });

        var cambiata = Metadati.Scrivi(zona, zona.Records[1], NomeDi, new Dictionary<string, string> { ["zone"] = "\"Torino e Novara\"" });
        var righe = new FileSaverOrchestrator().Righe(cambiata, new HashSet<MvaSector>(), new MvaSaver(enroute: true));

        Assert.Equal(["//@\"LIMM\" zone=\"Torino e Novara\"", "//@START", .. prima, "//@END \"LIMM\""], righe);
        var riletta = Mva(Righe([.. righe]));
        Assert.Equal(prima, new FileSaverOrchestrator().Righe(Metadati.Togli(riletta, riletta.Records[0], NomeDi),
            new HashSet<MvaSector>(), new MvaSaver(enroute: true)));
    }

    // Ogni tipo ha il suo catalogo (§M): una chiave di un altro file si legge, ma è un avviso.
    [Fact]
    public void OgniFileABlocchiHaIlSuoCatalogo()
    {
        Assert.Equal(TipoDiProblemaDeiMetadati.ChiaveSconosciuta, Assert.Single(Metadati.Leggi(Tfl(Righe(["//@\"LIPX_ES0_APP\" zone=Torino", .. SettoreLipx]))).Problemi).Tipo);
        Assert.Equal(TipoDiProblemaDeiMetadati.ChiaveSconosciuta, Assert.Single(Metadati.Leggi(Mva(Righe(["//@\"LIMM\" form=X", .. ZonaTorino]))).Problemi).Tipo);
        Assert.Empty(Metadati.Leggi(Mva(Righe(["//@\"LIMM\" zone=Torino locked=si note=\"dalla carta\"", .. ZonaTorino]))).Problemi);
        Assert.NotNull(Metadati.ProblemiDi(Pol(Righe("STATIC;GRASS;1;GRASS;"))));
        Assert.NotNull(Metadati.ProblemiDi(Artcc(Righe("L;ABDAB;N037.53.21.000;E010.37.43.000;8;"))));
    }

    // Su ogni file a blocchi: un tag messo e tolto lascia il file com'era, byte per byte.
    [Fact]
    public void MettereETogliereUnTagLasciaIlFileComEra()
    {
        MettiETogli(Mva(Righe(["//MVA MILANO", .. ZonaTorino, "", .. ZonaNovara])), new MvaSaver(enroute: true));
        MettiETogli(Aerovie(Righe(["//KY139", .. L81, "", "L;L81;N041.00.00.000;E015.00.00.000;"])), new AirwaySaver());
        MettiETogli(Tfl(Righe([.. SettoreLipx, "//fine", .. SettoreLipx])), new TflSaver());
        MettiETogli(Hartcc(Righe("T;RR NE;N044.23.17.000;E011.07.44.000;", "//RR CONF2", "T;RR NE;OTNUN;OTNUN;", "", "T;RR TS;OTNUN;OTNUN;")), new HartccSaver());
        MettiETogli(Artcc(Righe("L;ABDAB;N037.53.21.000;E010.37.43.000;8;", "T;FRA BDRY;LUSIL;LUSIL;", "T;FRA BDRY;OTNUN;OTNUN;")), new ArtccSaver());
    }

    private static void MettiETogli<T>(ParseResult<T> letto, IFileSaver<T> scrittore)
        where T : class
    {
        var prima = new FileSaverOrchestrator().Righe(letto, new HashSet<T>(), scrittore);
        foreach (var record in letto.Records.Where(r => NomeDi(r) is not null))
        {
            var con = Metadati.Scrivi(letto, record, NomeDi, new Dictionary<string, string> { ["note"] = "prova" });
            var tolto = Metadati.Togli(con, record, NomeDi);
            Assert.Equal(prima, new FileSaverOrchestrator().Righe(tolto, new HashSet<T>(), scrittore));
        }
    }
}
