using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Ui.Components;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Carta «Lavorare su uno scalo o su un settore» (2 novembre 2026), fetta 1: il fuoco (R1) e la mappa sullo scalo (R2),
/// col resto del sector nascosto o in grigio (D1). Lo stesso albero di <see cref="VistaPerScaloTests"/>: LIXA coi suoi
/// file, i disegni senza il suo nome sul suo sedime, e LIXB, che col fuoco su LIXA non deve comparire.
/// </summary>
public sealed class FuocoTests : IDisposable
{
    private const string It = "SectorFiles/Include/IT/";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly string _dati;
    private readonly SessioneDelLab _lab;

    public FuocoTests()
    {
        _albero.Scrivi("SectorFiles/ITALY.isc", "[INFO]\r\nN039.48.01.000\r\nE012.14.20.000\r\n60\r\n45\r\n+4.0\r\nIT\r\n\r\n" +
            "[AIRPORT]\r\nF;OTHER\\prova.ap\r\n[RUNWAY]\r\nF;OTHER\\prova.rw\r\n[ATC]\r\nF;OTHER\\prova.frq\r\n");
        _albero.Scrivi(It + "OTHER/prova.ap", "LIXA;14;6000;N039.48.01.000;E012.14.20.000;PROVA A;\r\nLIXB;48;6000;N038.25.54.000;E014.11.13.000;PROVA B;\r\n");
        _albero.Scrivi(It + "OTHER/prova.rw", "//PISTE\r\n" +
            "LIXA;16L;34R;14;6;159;339;N039.50.45.490;E012.15.41.380;N039.48.44.800;E012.16.31.890;\r\n" +
            "LIXB;04;22;48;40;040;220;N038.25.30.000;E014.10.50.000;N038.26.20.000;E014.11.40.000;\r\n");
        _albero.Scrivi(It + "OTHER/prova.frq", "LIXA_TWR;118.700;LIXA;;;0;\r\nLIXB_TWR;119.100;LIXB;;;0;\r\n");
        _albero.Scrivi(It + "lixa.gts", "101;LIXA;N039.48.16.944;E012.14.18.365;\r\n");
        _albero.Scrivi(It + "GEO/lixa.geo",
            "//Taxiway A\r\nN039.48.00.000;E012.14.00.000;N039.48.00.000;E012.15.00.000;TAXI_CENTER;\r\n");
        _albero.Scrivi(It + "GND_LAYOUT/xa_ad_gnd.pol",
            "STATIC;GRASS;1;GRASS;\r\nN039.48.00.000;E012.14.00.000;\r\nN039.48.00.000;E012.15.00.000;\r\nN039.49.00.000;E012.15.00.000;\r\n");
        _albero.Scrivi(It + "RW_MARKINGS/xb_mark.geo",
            "//designator rw 04\r\nN038.25.50.000;E014.11.00.000;N038.25.50.000;E014.11.30.000;RUNWAY;\r\n");

        _dati = Path.Combine(_albero.Radice, "dati-del-lab");
        _lab = new SessioneDelLab(_dati);
        _contesto.Services.AddSingleton(_lab);
        _contesto.JSInterop.Mode = JSRuntimeMode.Loose;
        _contesto.JSInterop.Setup<bool>("sectorlab.mappa.crea", _ => true).SetResult(true);
    }

    public void Dispose()
    {
        _contesto.Dispose();
        _albero.Dispose();
    }

    private async Task Apri() => Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

    [Fact]
    public async Task LavoraSu_LaMappaMostraSoloLoScalo_EGliStratiDeiSuoiFileSiAccendono()
    {
        await Apri();

        Assert.True(_lab.LavoraSu("lixa"));

        Assert.Equal("LIXA", _lab.Fuoco);
        Assert.Equal("LIXA", _lab.ScaloScelto);
        // Le sue righe dell'.ap e del .rw, i suoi file e i disegni sul suo sedime; niente di LIXB, niente posizioni.
        Assert.Equal(
            [It + "OTHER/prova.ap#0", It + "OTHER/prova.rw#0", It + "lixa.gts#*", It + "GEO/lixa.geo#*", It + "GND_LAYOUT/xa_ad_gnd.pol#*"],
            _lab.ChiaviDelFuoco);
        Assert.Equal(_lab.ChiaviDelFuoco, _lab.VistaDaMostrare(SpazioDiLavoro.Sector));
        Assert.Empty(_lab.VistaDaMostrare(SpazioDiLavoro.Aip));
        Assert.Contains(StratiDellaMappa.DiFile(It + "GEO/lixa.geo")!.Id, _lab.AccesiDi(SpazioDiLavoro.Sector));
        Assert.Contains(StratiDellaMappa.DiFile(It + "GND_LAYOUT/xa_ad_gnd.pol")!.Id, _lab.AccesiDi(SpazioDiLavoro.Sector));
        Assert.Equal(1, _lab.InquadratureDelFuoco);
    }

    [Fact]
    public async Task UnoScaloCheNonCe_SiRifiuta()
    {
        await Apri();

        Assert.False(_lab.LavoraSu("QQQQ"));

        Assert.Null(_lab.Fuoco);
        Assert.Contains("QQQQ", _lab.Rifiuto, StringComparison.Ordinal);
    }

    // «Solo questo sulla mappa» dentro il fuoco stringe ancora; «torna allo scalo» (la vista a mano svuotata) riporta al
    // fuoco; uscendo la mappa torna agli strati accesi.
    [Fact]
    public async Task LaVistaAManoVinceSulFuoco_ESvuotataSiTornaAlloScalo()
    {
        await Apri();
        _lab.LavoraSu("LIXA");

        _lab.CambiaLaVista(It + "GEO/lixa.geo", 0);
        Assert.Equal([It + "GEO/lixa.geo#0"], _lab.VistaDaMostrare(SpazioDiLavoro.Sector));

        _lab.SvuotaLaVista();
        Assert.Equal(_lab.ChiaviDelFuoco, _lab.VistaDaMostrare(SpazioDiLavoro.Sector));

        _lab.EsciDalFuoco();
        Assert.Null(_lab.Fuoco);
        Assert.Empty(_lab.VistaDaMostrare(SpazioDiLavoro.Sector));
        Assert.Equal("LIXA", _lab.ScaloScelto);
    }

    // R2: la mappa va sullo scalo — il riquadro delle sue forme, non di quelle di LIXB.
    [Fact]
    public async Task IlRiquadroEQuelloDelleFormeDelloScalo()
    {
        await Apri();
        _lab.LavoraSu("LIXA");

        var r = Assert.IsType<RiquadroDelFuoco>(_lab.Riquadro());

        Assert.InRange(r.Sud, 39.79, 39.81);
        Assert.InRange(r.Nord, 39.81, 39.85);
        Assert.InRange(r.Ovest, 12.23, 12.24);
        Assert.InRange(r.Est, 12.27, 12.28);
    }

    // R2: gli Strati contano le forme dello scalo.
    [Fact]
    public async Task GliStratiContanoLeFormeDelloScalo()
    {
        await Apri();
        var geo = _lab.Strati.Single(s => s.Tipo.Id == StratiDellaMappa.DiFile(It + "GEO/lixa.geo")!.Id);
        int tutte = geo.Forme.Count;

        _lab.LavoraSu("LIXA");

        Assert.Equal(1, _lab.FormeNelFuoco(geo));
        Assert.True(tutte > 1);
        _lab.EsciDalFuoco();
        Assert.Equal(tutte, _lab.FormeNelFuoco(geo));
    }

    // R1: il fuoco resta dopo un riavvio del Lab; uscito, non torna.
    [Fact]
    public async Task IlFuocoSiRicordaAllaRiapertura()
    {
        await Apri();
        _lab.LavoraSu("LIXA");

        var dopo = new SessioneDelLab(_dati);
        Assert.True(await dopo.ApriEValidaAsync(_albero.Radice));
        Assert.Equal("LIXA", dopo.Fuoco);
        Assert.Equal(dopo.ChiaviDelFuoco, dopo.VistaDaMostrare(SpazioDiLavoro.Sector));

        dopo.EsciDalFuoco();
        var ancora = new SessioneDelLab(_dati);
        Assert.True(await ancora.ApriEValidaAsync(_albero.Radice));
        Assert.Null(ancora.Fuoco);
    }

    // D1: il resto in grigio vale solo col fuoco e senza una vista a mano.
    [Fact]
    public async Task IlRestoInGrigio_SoloColFuocoESenzaVistaAMano()
    {
        await Apri();
        _lab.MostraIlRestoInGrigio(true);
        Assert.False(_lab.RestoInGrigioDi(SpazioDiLavoro.Sector));

        _lab.LavoraSu("LIXA");
        Assert.True(_lab.RestoInGrigioDi(SpazioDiLavoro.Sector));
        Assert.False(_lab.RestoInGrigioDi(SpazioDiLavoro.Aip));

        _lab.CambiaLaVista(It + "GEO/lixa.geo", 0);
        Assert.False(_lab.RestoInGrigioDi(SpazioDiLavoro.Sector));
    }

    [Fact]
    public async Task ASchermo_LavoraSu_LaStrisciaDelFuoco_EEsci()
    {
        await Apri();
        var colonna = _contesto.RenderComponent<ColonnaDiSinistra>();

        colonna.Find("[data-linguetta='scali']").Click();
        colonna.Find("[data-campo='scalo']").Change("LIXA");
        colonna.WaitForAssertion(() => Assert.Contains("Lavora su LIXA", colonna.Find("[data-tasto='lavora-su']").TextContent, StringComparison.Ordinal));

        colonna.Find("[data-tasto='lavora-su']").Click();

        colonna.WaitForAssertion(() => Assert.Contains("PROVA A", colonna.Find("[data-fuoco='LIXA']").TextContent, StringComparison.Ordinal));
        Assert.Empty(colonna.FindAll("[data-tasto='lavora-su']"));

        colonna.Find("[data-tasto='resto-in-grigio']").Change(true);
        Assert.True(_lab.RestoInGrigio);

        colonna.Find("[data-tasto='esci-dal-fuoco']").Click();
        colonna.WaitForAssertion(() => Assert.Empty(colonna.FindAll("[data-fuoco]")));
        Assert.Null(_lab.Fuoco);
    }

    // La mappa: col fuoco riceve le chiavi dello scalo (e il grigio), e va sul riquadro una volta per richiesta.
    [Fact]
    public async Task ASchermo_LaMappaRiceveLoScalo_ESiInquadra()
    {
        await Apri();
        var mappa = _contesto.RenderComponent<Vipi.SectorLab.Ui.Components.Mappa>(p => p.Add(m => m.Spazio, SpazioDiLavoro.Sector));
        mappa.WaitForAssertion(() => Assert.NotEmpty(_contesto.JSInterop.Invocations["sectorlab.mappa.crea"]));

        _lab.MostraIlRestoInGrigio(true);
        _lab.LavoraSu("LIXA");

        mappa.WaitForAssertion(() => Assert.Single(_contesto.JSInterop.Invocations["sectorlab.mappa.inquadra"]));
        var vista = _contesto.JSInterop.Invocations["sectorlab.mappa.vista"][^1];
        Assert.Equal(_lab.ChiaviDelFuoco, (string[])vista.Arguments[0]!);
        Assert.Equal(true, vista.Arguments[2]);

        _lab.InquadraIlFuoco();
        mappa.WaitForAssertion(() => Assert.Equal(2, _contesto.JSInterop.Invocations["sectorlab.mappa.inquadra"].Count));
    }

    // --- fetta 4: R8 ----------------------------------------------------------------------------------------------------

    // R8: in Consegna le modifiche dello scalo stanno in testa, e si annullano anche da sole (in un gesto, che Ctrl+Z
    // rimette); le altre restano.
    [Fact]
    public async Task ASchermo_InConsegnaLeModificheDelloScalo_SiAnnullanoDaSole()
    {
        await Apri();
        _lab.LavoraSu("LIXA");
        Assert.True(_lab.CambiaCampo(It + "OTHER/prova.frq", 1, "Code", "LIXB_APP"));
        Assert.True(_lab.CambiaCampo(It + "GEO/lixa.geo", 0, "Color", "TAXIWAY"));
        var pannello = _contesto.RenderComponent<Vipi.SectorLab.Ui.Components.Modifiche>();

        Assert.Contains("Di LIXA: 1 modifica in 1 file", pannello.Find("[data-modifiche-del-fuoco='LIXA']").TextContent, StringComparison.Ordinal);
        Assert.Equal(It + "GEO/lixa.geo", pannello.FindAll("[data-diff]").First().GetAttribute("data-diff"));

        pannello.Find("[data-tasto='annulla-del-fuoco']").Click();

        Assert.Equal(0, _lab.ModificheDelFuoco);
        Assert.Equal([It + "OTHER/prova.frq"], _lab.Modifiche.FileToccati);
        pannello.WaitForAssertion(() => Assert.Empty(pannello.FindAll("[data-tasto='annulla-del-fuoco']")));

        _lab.Annulla();
        Assert.Equal(1, _lab.ModificheDelFuoco);
    }

    // --- fetta 3: R6 ----------------------------------------------------------------------------------------------------

    // R6: col fuoco gli Strati vanno sopra la mappa, dietro il loro tasto, e la mappa prende la loro colonna.
    [Fact]
    public async Task ASchermo_ColFuocoGliStratiStannoSopraLaMappa()
    {
        await Apri();
        var pagina = _contesto.RenderComponent<Vipi.SectorLab.Ui.Components.Pages.Home>();
        Assert.Equal("colonna", pagina.Find("[data-strati]").GetAttribute("data-strati"));

        await pagina.InvokeAsync(() => _lab.LavoraSu("LIXA"));
        pagina.WaitForAssertion(() => Assert.Equal("chiusi", pagina.Find("[data-strati]").GetAttribute("data-strati")));
        Assert.Empty(pagina.FindAll("[data-divide='strati']"));

        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=strati]").Click());
        Assert.Equal("aperti", pagina.Find("[data-strati]").GetAttribute("data-strati"));

        await pagina.InvokeAsync(_lab.EsciDalFuoco);
        pagina.WaitForAssertion(() => Assert.Equal("colonna", pagina.Find("[data-strati]").GetAttribute("data-strati")));
    }

    // --- fetta 2: R3, R4 ------------------------------------------------------------------------------------------------

    // R3: col fuoco un file dello scalo si apre nella linguetta Scali, coi suoi record; non si salta a Sfoglia.
    [Fact]
    public async Task ASchermo_ColFuocoIlFileSiApreSottoLoScalo()
    {
        await Apri();
        _lab.LavoraSu("LIXA");
        var colonna = _contesto.RenderComponent<ColonnaDiSinistra>();

        // La colonna parte dallo scalo.
        Assert.Contains("lab-linguetta-scelta", colonna.Find("[data-linguetta='scali']").ClassName ?? "", StringComparison.Ordinal);
        colonna.Find($"[data-voce-scalo='{It}GEO/lixa.geo']").Click();

        colonna.WaitForAssertion(() => Assert.NotEmpty(colonna.FindAll($".lab-scali [data-file-aperto='{It}GEO/lixa.geo']")));
        Assert.Contains("lab-linguetta-scelta", colonna.Find("[data-linguetta='scali']").ClassName ?? "", StringComparison.Ordinal);

        // Un record dell'elenco si sceglie, e il file resta aperto lì.
        colonna.Find(".lab-scali [data-record='0']").Click();
        Assert.Equal((It + "GEO/lixa.geo", 0), _lab.Scelta);
        colonna.WaitForAssertion(() => Assert.NotEmpty(colonna.FindAll($".lab-scali [data-file-aperto='{It}GEO/lixa.geo']")));

        // Un secondo clic sul file lo chiude.
        colonna.Find($"[data-voce-scalo='{It}GEO/lixa.geo']").Click();
        colonna.WaitForAssertion(() => Assert.Empty(colonna.FindAll("[data-file-aperto]")));
    }

    // R3: una scelta nuova in un file dello scalo (dalla mappa) riporta la colonna sullo scalo, col file aperto. Fuori dal
    // fuoco la colonna non si muove.
    [Fact]
    public async Task ASchermo_UnaSceltaDallaMappaRiportaLaColonnaSulloScalo()
    {
        await Apri();
        _lab.LavoraSu("LIXA");
        var colonna = _contesto.RenderComponent<ColonnaDiSinistra>();
        colonna.Find("[data-linguetta='sfoglia']").Click();

        _lab.Scegli(It + "GND_LAYOUT/xa_ad_gnd.pol", 0);

        colonna.WaitForAssertion(() => Assert.NotEmpty(colonna.FindAll($".lab-scali [data-file-aperto='{It}GND_LAYOUT/xa_ad_gnd.pol']")));

        // Un record di LIXB (fuori dal fuoco): la colonna resta dov'è.
        colonna.Find("[data-linguetta='sfoglia']").Click();
        _lab.Scegli(It + "RW_MARKINGS/xb_mark.geo", 0);
        colonna.WaitForAssertion(() => Assert.Contains("lab-linguetta-scelta", colonna.Find("[data-linguetta='sfoglia']").ClassName ?? "", StringComparison.Ordinal));
    }

    // R4: col fuoco la scelta chiede alla mappa di muoversi solo se il record è fuori vista; «⌖ Portami lì» lo inquadra.
    [Fact]
    public async Task ASchermo_ColFuocoLaSceltaNonSpostaLaMappa_PortamiLiSi()
    {
        await Apri();
        var mappa = _contesto.RenderComponent<Vipi.SectorLab.Ui.Components.Mappa>(p => p.Add(m => m.Spazio, SpazioDiLavoro.Sector));
        mappa.WaitForAssertion(() => Assert.NotEmpty(_contesto.JSInterop.Invocations["sectorlab.mappa.crea"]));

        // Fuori dal fuoco: inquadra, come sempre.
        _lab.Scegli(It + "GEO/lixa.geo", 0);
        mappa.WaitForAssertion(() => Assert.NotEmpty(_contesto.JSInterop.Invocations["sectorlab.mappa.evidenzia"]));
        var fuori = _contesto.JSInterop.Invocations["sectorlab.mappa.evidenzia"][^1];
        Assert.Equal((true, false), ((bool)fuori.Arguments[2]!, (bool)fuori.Arguments[4]!));

        _lab.LavoraSu("LIXA");
        Assert.True(_lab.SceltaSenzaZoom);
        int prima = _contesto.JSInterop.Invocations["sectorlab.mappa.evidenzia"].Count;
        _lab.Scegli(It + "GND_LAYOUT/xa_ad_gnd.pol", 0);
        mappa.WaitForAssertion(() => Assert.True(_contesto.JSInterop.Invocations["sectorlab.mappa.evidenzia"].Count > prima));
        var dentro = _contesto.JSInterop.Invocations["sectorlab.mappa.evidenzia"][^1];
        Assert.Equal((true, true), ((bool)dentro.Arguments[2]!, (bool)dentro.Arguments[4]!));

        prima = _contesto.JSInterop.Invocations["sectorlab.mappa.evidenzia"].Count;
        _lab.PortamiLi();
        mappa.WaitForAssertion(() => Assert.True(_contesto.JSInterop.Invocations["sectorlab.mappa.evidenzia"].Count > prima));
        var portami = _contesto.JSInterop.Invocations["sectorlab.mappa.evidenzia"][^1];
        Assert.Equal((true, false), ((bool)portami.Arguments[2]!, (bool)portami.Arguments[4]!));
    }
}
