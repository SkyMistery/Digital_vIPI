using Vipi.Sectorfile.Validazione;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Lotto «Subito», slice 12a (carta «file per file» H2, I4, R1, R3): i controlli della terra. Uno stand o un'etichetta
/// con l'ICAO diverso da quello del suo file (<c>L3MC</c> in <c>limc.gts</c>, <c>LINB</c> in <c>libn.txi</c>; lo stand di
/// <c>LIBP</c> in <c>libg.gts</c>, a 344 km), lontano dallo scalo, ripetuto; l'etichetta lontana dalla sua taxiway; il
/// tipo di un segmento <c>.geo</c> vuoto (10 in <c>liap.geo</c>) o sconosciuto; il <c>.pol</c> di uno scalo senza <c>.geo</c>.
/// </summary>
public sealed class ControlloDellaTerraTests : IDisposable
{
    private readonly string _radice = Path.Combine(Path.GetTempPath(), "terra-" + Guid.NewGuid().ToString("N"));

    public ControlloDellaTerraTests()
    {
        foreach (string cartella in new[] { "OTHER", "GEO", "COLORS", "GND_LAYOUT" })
            Directory.CreateDirectory(Path.Combine(_radice, "Include", "IT", cartella));
        Scrivi("ITALY.isc", Righe("[INFO]", "N041.48.01.000", "E012.14.20.000", "60", "45", "+4.0", "IT", "",
            "[DEFINE]", @"F;COLORS\colors.def", "[AIRPORT]", @"F;OTHER\itap.ap", "[RUNWAY]", @"F;OTHER\itrw.rw",
            "[GEO]", @"F;GEO\lirf.geo", @"F;RW_MARKINGS\rf_mark.geo",
            "[FILLCOLOR]", @"F;GND_LAYOUT\rf_ad_gnd.pol", @"F;GND_LAYOUT\bp_ad_gnd.pol"));
        Scrivi(@"Include\IT\COLORS\colors.def", Righe("GRASS;#406230;", "MARKING;#DCDCDC;",
            "TAXIWAY;#767587;", "BUILDING;#202000;", "RUNWAY;#333333;"));
        Scrivi(@"Include\IT\OTHER\itap.ap", Righe(
            "LIRF;14;6000;N041.48.01.000;E012.14.20.000;FIUMICINO;",
            "LIBP;48;6000;N042.25.54.000;E014.11.13.000;PESCARA;"));
        // Un asse di taxiway da ovest a est, poco a sud dell'ARP; poi i tipi: uno di colors.def, un colore, uno vuoto, uno sbagliato.
        Scrivi(@"Include\IT\GEO\lirf.geo", Righe(
            "//Taxiway A",
            "N041.48.00.000;E012.14.00.000;N041.48.00.000;E012.15.00.000;TAXI_CENTER;",
            "//Segni",
            "N041.48.02.000;E012.14.00.000;N041.48.02.000;E012.15.00.000;MARKING;",
            "N041.48.03.000;E012.14.00.000;N041.48.03.000;E012.15.00.000;#FF0000;",
            "N041.48.04.000;E012.14.00.000;N041.48.04.000;E012.15.00.000;;",
            "N041.48.05.000;E012.14.00.000;N041.48.05.000;E012.15.00.000;TAXYWAY;",
            // Slice 12d: le marcature. La 05 il .rw non ce l'ha; «rw 16» nomina la 16L e la 16R, e va bene.
            "//designator rw 05",
            "N041.48.06.000;E012.14.00.000;N041.48.06.000;E012.15.00.000;RUNWAY;",
            "//threshold marks rw 16",
            "N041.48.07.000;E012.14.00.000;N041.48.07.000;E012.15.00.000;RUNWAY;",
            "//Taxiway 12"));
        Directory.CreateDirectory(Path.Combine(_radice, "Include", "IT", "RW_MARKINGS"));
        Scrivi(@"Include\IT\OTHER\itrw.rw", Righe("//PISTE",
            "LIRF;16L;34R;14;6;159;339;N041.50.45.490;E012.15.41.380;N041.48.44.800;E012.16.31.890;",
            "LIRF;16R;34L;7;8;159;339;N041.48.55.860;E012.13.34.910;N041.46.55.180;E012.14.25.450;"));
        // Il file delle marcature non porta il nome dello scalo: si riconosce da dove sta.
        Scrivi(@"Include\IT\RW_MARKINGS\rf_mark.geo", Righe(
            "//Runway 22R designator",
            "N041.48.06.000;E012.14.00.000;N041.48.06.000;E012.15.00.000;RUNWAY;",
            "//rwy 16L/34R",
            "N041.48.07.000;E012.14.00.000;N041.48.07.000;E012.15.00.000;RUNWAY;"));
        Scrivi(@"Include\IT\lirf.txi", Righe(
            "A;LIRF;N041.48.00.000;E012.14.30.000;",
            "B;LIRF;N041.48.10.000;E012.14.30.000;",
            "A;LIFR;N041.48.00.000;E012.14.40.000;"));
        Scrivi(@"Include\IT\lirf.gts", Righe(
            "101;LIRF;N041.48.16.944;E012.14.18.365;",
            "101;LIRF;N041.48.16.209;E012.14.15.712;M;",
            "1;LIBP;N042.25.54.000;E014.11.13.000;",
            "900;LIRF;N041.54.00.000;E012.14.20.000;",
            "//102;LIRF;N041.48.16.209;E012.14.15.712;"));
        string erba = Righe("//AD_BOUNDARY_Polygon", "STATIC;GRASS;1;GRASS;",
            "N041.48.00.000;E012.14.00.000;", "N041.48.00.000;E012.15.00.000;", "N041.49.00.000;E012.15.00.000;");
        // Slice 12d: dopo l'erba una pista, poi una taxiway e un edificio (fuori posto: stanno sotto la pista), poi un'altra pista.
        static string Poligono(string riempimento) => Righe($"STATIC;{riempimento};1;{riempimento};",
            "N041.48.00.000;E012.14.00.000;", "N041.48.00.000;E012.15.00.000;", "N041.49.00.000;E012.15.00.000;");
        Scrivi(@"Include\IT\GND_LAYOUT\rf_ad_gnd.pol", erba + Poligono("RUNWAY") + Poligono("TAXIWAY") + Poligono("BUILDING") + Poligono("RUNWAY") + Poligono("MARKING"));
        Scrivi(@"Include\IT\GND_LAYOUT\bp_ad_gnd.pol", Righe("//AD_BOUNDARY_Polygon", "STATIC;GRASS;1;VERDE;",
            "N042.25.50.000;E014.11.00.000;", "N042.25.50.000;E014.11.30.000;", "N042.26.10.000;E014.11.30.000;"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_radice))
            Directory.Delete(_radice, recursive: true);
    }

    private static string Righe(params string[] righe) => string.Join("\r\n", righe) + "\r\n";

    private void Scrivi(string relativo, string testo)
        => File.WriteAllText(Path.Combine(_radice, relativo.Replace('\\', Path.DirectorySeparatorChar)), testo);

    private List<ProblemaDelSector> Di(Regola regola) => Validatore.ValidaLAlbero(_radice).Where(p => p.Regola == regola).ToList();

    [Fact]
    public void UnIcaoDiversoDaQuelloDelFileEUnAvviso_ESeIlPuntoStaNelloScaloSiProponeQuelloDelFile()
    {
        var problemi = Di(Regola.ScaloDiversoDalFile);

        Assert.Equal(2, problemi.Count);
        Assert.All(problemi, p => Assert.Equal(Gravita.Avviso, p.Gravita));
        var etichetta = Assert.Single(problemi, p => p.File.EndsWith("lirf.txi", StringComparison.Ordinal));
        Assert.Equal(3, etichetta.Riga);
        Assert.Equal("A;LIRF;N041.48.00.000;E012.14.40.000;", etichetta.Proposta);
        // Lo stand di LIBP sta a LIBP (come quello di libg.gts sul fork): non è un refuso, è nel file sbagliato.
        var stand = Assert.Single(problemi, p => p.File.EndsWith("lirf.gts", StringComparison.Ordinal));
        Assert.Equal(3, stand.Riga);
        Assert.Null(stand.Proposta);
        Assert.Contains("km", stand.Dettaglio, StringComparison.Ordinal);
    }

    [Fact]
    public void UnoStandColSuoIcaoMaLontanoDalloScaloEUnAvviso()
    {
        var p = Assert.Single(Di(Regola.LontanoDalloScalo));
        Assert.Equal((4, Gravita.Avviso), (p.Riga, p.Gravita));
        Assert.EndsWith("lirf.gts", p.File, StringComparison.Ordinal);
    }

    [Fact]
    public void DueStandColloStessoNomeSonoUnAvviso_QuelloCommentatoNonConta()
    {
        var p = Assert.Single(Di(Regola.StandRipetuto));
        Assert.Equal(2, p.Riga);
        Assert.Contains("riga 1", p.Dettaglio, StringComparison.Ordinal);
    }

    // Le etichette di una taxiway lunga si ripetono (95 nomi sul fork): è normale, e non è un avviso.
    [Fact]
    public void UnEtichettaLontanaDalleTaxiwayDelSuoScaloEUnAvviso_QuellaSullAsseNo()
    {
        var p = Assert.Single(Di(Regola.EtichettaLontanaDallaTaxiway));
        Assert.Equal(2, p.Riga);
        Assert.Contains("309 m", p.Dettaglio, StringComparison.Ordinal);
    }

    [Fact]
    public void IlTipoVuotoOSconosciutoDiUnSegmentoEUnAvviso_UnNomeDiColorsDefEUnColoreNo()
    {
        var problemi = Di(Regola.TipoSconosciuto).Where(p => p.File.EndsWith("lirf.geo", StringComparison.Ordinal)).ToList();

        Assert.Equal([6, 7], problemi.Select(p => p.Riga));
        Assert.Contains("vuoto", problemi[0].Dettaglio, StringComparison.Ordinal);
        Assert.Contains("TAXYWAY", problemi[1].Dettaglio, StringComparison.Ordinal);
    }

    [Fact]
    public void IlColoreSconosciutoDiUnPoligonoEUnAvviso()
    {
        var p = Assert.Single(Di(Regola.TipoSconosciuto), p => p.File.EndsWith(".pol", StringComparison.Ordinal));
        Assert.EndsWith("bp_ad_gnd.pol", p.File, StringComparison.Ordinal);
        Assert.Contains("VERDE", p.Dettaglio, StringComparison.Ordinal);
    }

    // Committente, 4 ottobre 2026: vale l'ordine del fork (erba, taxiway, cemento, piazzale, buchi, edifici, pista).
    [Fact]
    public void UnRiempimentoScrittoDopoUnoCheGliStaSopraEUnAvviso()
    {
        var problemi = Di(Regola.OrdineDiDisegno);

        // La taxiway (riga 10) e l'edificio (riga 14) dopo la pista della riga 6; la seconda pista e MARKING (che non è
        // un riempimento di terra) no.
        Assert.Equal([10, 14], problemi.Select(p => p.Riga));
        Assert.All(problemi, p => Assert.Equal(Gravita.Avviso, p.Gravita));
        Assert.Contains("TAXIWAY scritto dopo RUNWAY (riga 6)", problemi[0].Dettaglio, StringComparison.Ordinal);
        Assert.Equal(["GRASS", "TAXIWAY", "CONCRETE", "APRON", "HOLE", "BUILDING", "RUNWAY"], Vipi.Sectorfile.Shared.OrdineDeiRiempimenti.Ordine);
    }

    // Come la 05/23 di LIBR (chiusa e tolta dal .rw) e la 04/22 di LIRP (il .rw ha 03/21).
    [Fact]
    public void UnCommentoDiMarcatureCheNominaUnaPistaCheIlRwNonHaEUnAvviso()
    {
        var problemi = Di(Regola.MarcaturaDiUnaPistaAssente);

        Assert.Equal(2, problemi.Count);
        var delGeo = Assert.Single(problemi, p => p.File.EndsWith("lirf.geo", StringComparison.Ordinal));
        Assert.Equal(8, delGeo.Riga);
        Assert.Contains("pista 05", delGeo.Dettaglio, StringComparison.Ordinal);
        Assert.Contains("16L, 16R, 34L, 34R", delGeo.Dettaglio, StringComparison.Ordinal);
        var delleMarcature = Assert.Single(problemi, p => p.File.EndsWith("rf_mark.geo", StringComparison.Ordinal));
        Assert.Equal(1, delleMarcature.Riga);
        Assert.Contains("LIRF", delleMarcature.Dettaglio, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("//designator rw 05", "05")]
    [InlineData("//Runway 04R designator", "04R")]
    [InlineData("//rwy 18/36", "18,36")]
    [InlineData("//rwy 12-30", "12,30")]
    [InlineData("//Displaced threshold arrow rw35L", "35L")]
    [InlineData("//2", "")]
    [InlineData("//Taxiway 12", "")]
    public void LePisteNominateDaUnCommento(string commento, string attese)
        => Assert.Equal(attese, string.Join(',', ControlloDellaTerra.PisteCitate(commento)));

    // Lo scalo di un .pol si riconosce da dove sta (l'ARP più vicino), non dal nome: `rf_ad_gnd.pol`, `lsza_ad_gnd.pol`.
    [Fact]
    public void IlRiempimentoDiUnoScaloSenzaIlSuoGeoEUnAvviso()
    {
        var p = Assert.Single(Di(Regola.RiempimentoSenzaDisegno));
        Assert.EndsWith("bp_ad_gnd.pol", p.File, StringComparison.Ordinal);
        Assert.Contains("LIBP", p.Dettaglio, StringComparison.Ordinal);
    }
}
