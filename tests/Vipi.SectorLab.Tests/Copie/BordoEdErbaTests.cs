using Vipi.SectorLab.Tests.Ui;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Copie;

/// <summary>
/// Il bordo in un <c>.geo</c> e il riempimento in un <c>.pol</c> sono la stessa forma (lotto «Subito» slice 8d, «file per
/// file» I2, H10): cambiare l'uno cambia l'altro. La linea del <c>.geo</c> si riscrive a segmenti, e quelli che non
/// cambiano restano le righe di prima.
/// </summary>
public sealed class BordoEdErbaTests : IDisposable
{
    private const string Erba = "SectorFiles/Include/IT/GND_LAYOUT/zz_ad_gnd.pol";
    private const string Geo = "SectorFiles/Include/IT/GEO/lizz.geo";

    private static readonly string[] Bordo =
    [
        "N045.00.00.000;E009.00.00.000;N045.00.10.000;E009.00.00.000;BUILDING;",
        "N045.00.10.000;E009.00.00.000;N045.00.10.000;E009.00.10.000;BUILDING;",
        "N045.00.10.000;E009.00.10.000;N045.00.00.000;E009.00.10.000;BUILDING;",
        "N045.00.00.000;E009.00.10.000;N045.00.00.000;E009.00.00.000;BUILDING;",
    ];

    private readonly AlberoDiProva _albero = new();
    private readonly SessioneDelLab _lab;

    public BordoEdErbaTests()
    {
        _albero.Scrivi(Erba, string.Join("\r\n", "//AD_BOUNDARY_Polygon", "STATIC;GRASS;1;GRASS;", "N045.00.00.000;E009.00.00.000;",
            "N045.00.10.000;E009.00.00.000;", "N045.00.10.000;E009.00.10.000;", "N045.00.00.000;E009.00.10.000;") + "\r\n");
        _albero.Scrivi(Geo, string.Join("\r\n", ["//AD_BOUNDARY", .. Bordo]) + "\r\n");
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
    }

    public void Dispose() => _albero.Dispose();

    // I campioni dell'albero di prova hanno un'erba senza confine loro (rf_ad_gnd.pol, senza lirf.geo): contano i file di qui.
    private static bool DelConfineQui(Vipi.SectorLab.Core.Problemi.ProblemaNelLab p)
        => p.Problema.Regola == Vipi.Sectorfile.Validazione.Regola.ConfineSenzaErba && p.File is Geo or Erba;

    private string[] Righe(string file) => File.ReadAllText(Path.Combine(_albero.Radice, file)).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public async Task UnVerticeDellErbaSpostatoSpostaIlBordoSoloNeiDueSegmentiCheLoToccano()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

        Assert.True(_lab.GestoSuiVertici(Erba, 0, "Vertices", GestoDeiVertici.Cambia, 1, "N045.00.20.000;E009.00.00.000;"));

        Assert.Equal(Geo, Assert.Single(_lab.FormaPortata!.Portate).File);
        await _lab.SalvaAsync();
        Assert.False(_lab.Modifiche.CEQualcosa);
        Assert.Equal(
            ["//AD_BOUNDARY",
             "N045.00.00.000;E009.00.00.000;N045.00.20.000;E009.00.00.000;BUILDING;",
             "N045.00.20.000;E009.00.00.000;N045.00.10.000;E009.00.10.000;BUILDING;",
             Bordo[2], Bordo[3]],
            Righe(Geo));
    }

    [Fact]
    public async Task UnVerticeAggiuntoAllErbaAggiungeUnSegmentoAlBordo()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

        Assert.True(_lab.GestoSuiVertici(Erba, 0, "Vertices", GestoDeiVertici.Aggiungi, 2, "N045.00.15.000;E009.00.05.000;"));
        await _lab.SalvaAsync();

        Assert.Equal(
            ["//AD_BOUNDARY", Bordo[0],
             "N045.00.10.000;E009.00.00.000;N045.00.15.000;E009.00.05.000;BUILDING;",
             "N045.00.15.000;E009.00.05.000;N045.00.10.000;E009.00.10.000;BUILDING;",
             Bordo[2], Bordo[3]],
            Righe(Geo));
    }

    [Fact]
    public async Task UnPuntoDelBordoSpostatoSpostaLErba()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

        // Il punto 2 della linea (la fine del secondo segmento, l'inizio del terzo).
        Assert.True(_lab.CambiaPuntoDellaLinea(Geo, 0, 2, "N045.00.12.000;E009.00.12.000;"));

        Assert.Equal(Erba, Assert.Single(_lab.FormaPortata!.Portate).File);
        await _lab.SalvaAsync();
        Assert.Equal("N045.00.12.000;E009.00.12.000;", Righe(Erba)[4]);
        Assert.All(_lab.StessaFormaDi(Erba, 0).Single().Copie, c => Assert.True(c.Uguale));
    }

    [Fact]
    public async Task AnnullatoIlGestoTornanoComeEranoErbaEBordo()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        Assert.True(_lab.GestoSuiVertici(Erba, 0, "Vertices", GestoDeiVertici.Togli, 1));
        Assert.Equal([Geo, Erba], _lab.Modifiche.FileToccati.Order(StringComparer.Ordinal));

        _lab.Annulla();

        Assert.False(_lab.Modifiche.CEQualcosa);
        _lab.Ripeti();
        Assert.All(_lab.StessaFormaDi(Erba, 0).Single().Copie, c => Assert.True(c.Uguale));
    }

    [Fact]
    public async Task UnBordoInFormaCompattaRestaCompatto()
    {
        _albero.Scrivi(Geo, "N0450000000;E0090000000;N0450010000;E0090000000;COAST;\r\n"
                            + "N0450010000;E0090000000;N0450010000;E0090010000;COAST;\r\n"
                            + "N0450010000;E0090010000;N0450000000;E0090010000;COAST;\r\n"
                            + "N0450000000;E0090010000;N0450000000;E0090000000;COAST;\r\n");
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

        Assert.True(_lab.GestoSuiVertici(Erba, 0, "Vertices", GestoDeiVertici.Cambia, 1, "N045.00.20.000;E009.00.00.000;"));
        await _lab.SalvaAsync();

        Assert.Equal("N0450000000;E0090000000;N0450020000;E0090000000;COAST;", Righe(Geo)[0]);
    }
    // --- I2: l'uscita che manca; H10: il confine e la sua erba ---------------------------------------------------

    [Fact]
    public async Task UnaFormaConTuttEDueLeUsciteNonNeProponeAltre()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

        Assert.Null(_lab.UscitaDi(Erba, 0, out string? perche));
        Assert.Null(perche);
        Assert.Null(_lab.UscitaDi(Geo, 2, out _));
        Assert.DoesNotContain(_lab.ProblemiDellAlbero, DelConfineQui);
    }

    [Fact]
    public async Task LErbaSenzaBordoRiceveIlBordoInFondoAlGeoDelloScalo()
    {
        _albero.Scrivi(Geo, "//ALTRO\r\nN046.00.00.000;E009.00.00.000;N046.00.10.000;E009.00.00.000;TAXIWAY;\r\n");
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        // H10: l'erba del confine senza il suo confine è un avviso.
        var avviso = Assert.Single(_lab.ProblemiDellAlbero, DelConfineQui);
        Assert.Equal((Erba, 0), (avviso.File, avviso.Record));

        var uscita = _lab.UscitaDi(Erba, 0, out _)!;
        Assert.Equal((true, Geo, "BUILDING"), (uscita.Bordo, uscita.File, uscita.Tipo));
        Assert.True(_lab.AggiungiLUscita(Erba, 0));

        Assert.Equal((Geo, 1), _lab.Scelta);
        Assert.Contains(_lab.StessaFormaDi(Erba, 0).Single().Copie, c => c.Uguale && c.Dove.File == Geo);
        await _lab.SalvaAsync();
        Assert.Equal(["//ALTRO", "N046.00.00.000;E009.00.00.000;N046.00.10.000;E009.00.00.000;TAXIWAY;", "//AD_BOUNDARY", .. Bordo], Righe(Geo));
    }

    [Fact]
    public async Task IlConfineSenzaErbaRiceveLErbaInFondoAlPolDelloScalo()
    {
        _albero.Scrivi(Erba, "//ALTRO_Polygon\r\nSTATIC;CONCRETE;1;CONCRETE;\r\nN046.00.00.000;E009.00.00.000;\r\nN046.00.10.000;E009.00.00.000;\r\nN046.00.10.000;E009.00.10.000;\r\n");
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        Assert.Contains(_lab.ProblemiDellAlbero, p => p.Problema.Regola == Vipi.Sectorfile.Validazione.Regola.ConfineSenzaErba && p.File == Geo);

        // Dal terzo segmento: vale per la sua linea.
        var uscita = _lab.UscitaDi(Geo, 2, out _)!;
        Assert.Equal((false, Erba, "GRASS"), (uscita.Bordo, uscita.File, uscita.Tipo));
        Assert.True(_lab.AggiungiLUscita(Geo, 2));
        await _lab.SalvaAsync();
        await _lab.Validazione;

        Assert.Equal(["//ALTRO_Polygon", "STATIC;CONCRETE;1;CONCRETE;", "N046.00.00.000;E009.00.00.000;", "N046.00.10.000;E009.00.00.000;",
                      "N046.00.10.000;E009.00.10.000;", "//AD_BOUNDARY_Polygon", "STATIC;GRASS;1;GRASS;", "N045.00.00.000;E009.00.00.000;",
                      "N045.00.10.000;E009.00.00.000;", "N045.00.10.000;E009.00.10.000;", "N045.00.00.000;E009.00.10.000;"],
            Righe(Erba));
        Assert.DoesNotContain(_lab.ProblemiDellAlbero, DelConfineQui);
    }

    [Fact]
    public async Task SenzaUnFileDelloScaloDoveMetterlaLoDice()
    {
        File.Delete(Path.Combine(_albero.Radice, Geo));
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

        Assert.Null(_lab.UscitaDi(Erba, 0, out string? perche));
        Assert.Contains("per il futuro", perche, StringComparison.Ordinal);
    }

    // --- la stessa linea due volte nello stesso .geo (misura 8d: liaa.geo) ------------------------------------------

    // Separate da un segmento di un altro tipo, come in liaa.geo: attaccate e dello stesso tipo, la mappa le cucirebbe in
    // una linea sola (la cucitura non guarda i commenti: 34 linee su 5 599 nel fork, quasi tutte la costa di itgeo.geo).
    private const string Altro = "N046.00.00.000;E009.00.00.000;N046.00.10.000;E009.00.00.000;TAXIWAY;";

    private void DueVolte() => _albero.Scrivi(Geo, string.Join("\r\n", ["//AD_BOUNDARY", .. Bordo, "//ALTRO", Altro, "//DOPPIO", .. Bordo]) + "\r\n");

    [Fact]
    public async Task UnVerticeAggiuntoVaSuTuttEDueLeCopieDelloStessoFile()
    {
        DueVolte();
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

        Assert.True(_lab.GestoSuiVertici(Erba, 0, "Vertices", GestoDeiVertici.Aggiungi, 2, "N045.00.15.000;E009.00.05.000;"));
        Assert.Equal(2, _lab.FormaPortata!.Portate.Count);
        await _lab.SalvaAsync();

        var righe = Righe(Geo);
        Assert.Equal(14, righe.Length);
        Assert.Equal(righe[1..6], righe[9..14]);
        Assert.Contains("N045.00.15.000;E009.00.05.000;", righe[2], StringComparison.Ordinal);
    }

    [Fact]
    public async Task LaSceltaSegueIlSuoRecordQuandoUnaCopiaSopraCambiaDiSegmenti()
    {
        DueVolte();
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        _lab.Scegli(Geo, 5);

        // Il primo punto della seconda linea: la linea non si chiude più, e le sue copie hanno un lato in più.
        Assert.True(_lab.CambiaPuntoDellaLinea(Geo, 5, 0, "N044.59.50.000;E009.00.00.000;"));

        Assert.Equal((Geo, 6), _lab.Scelta);
        Assert.Equal(2, _lab.FormaPortata!.Portate.Count);
        Assert.All(_lab.StessaFormaDi(Geo, 6).Single().Copie, c => Assert.True(c.Uguale));
    }
}
