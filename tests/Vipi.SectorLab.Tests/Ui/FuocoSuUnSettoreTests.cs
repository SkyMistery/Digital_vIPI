using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Core.Sessione;
using Vipi.SectorLab.Ui.Components;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Carta «Lavorare su uno scalo o su un settore» (2 novembre 2026), fetta 5: R7, il fuoco su un settore. Un settore è una
/// posizione ATC (D2): LIXX_NC_CTR ha il suo <c>.tfl</c> (col suo nome), il bordo «XX NC» nel <c>.hartcc</c> e la sua riga
/// nel <c>.frq</c>. LIXX_NW_CTR gli sta accanto, a ovest, in un <c>.tfl</c> comune e nello stesso <c>.hartcc</c>;
/// LIXX_SU_CTR sta lontano, a sud; LIXX_FSS non ha niente da disegnare.
/// </summary>
public sealed class FuocoSuUnSettoreTests : IDisposable
{
    private const string It = "SectorFiles/Include/IT/";
    private const string Tfl = It + "DYNAMIC_SEC/lixx_nc_ctr.tfl";
    private const string Comune = It + "DYNAMIC_SEC/lixxctr.tfl";
    private const string Bordi = It + "HI_AIRSPACE/lixx.hartcc";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly string _dati;
    private readonly SessioneDelLab _lab;

    public FuocoSuUnSettoreTests()
    {
        _albero.Scrivi("SectorFiles/ITALY.isc", "[INFO]\r\nN039.48.01.000\r\nE012.14.20.000\r\n60\r\n45\r\n+4.0\r\nIT\r\n\r\n" +
            "[AIRPORT]\r\nF;OTHER\\prova.ap\r\n[ATC]\r\nF;OTHER\\prova.frq\r\n" +
            "[ARTCC HIGH]\r\nF;HI_AIRSPACE\\lixx.hartcc\r\n" +
            "[FILLCOLOR]\r\nF;DYNAMIC_SEC\\lixx_nc_ctr.tfl\r\nF;DYNAMIC_SEC\\lixxctr.tfl\r\n");
        _albero.Scrivi(It + "OTHER/prova.ap", "LIXA;14;6000;N042.30.00.000;E013.30.00.000;PROVA A;\r\n");
        _albero.Scrivi(It + "OTHER/prova.frq",
            "LIXX_NC_CTR;131.200;LIXA;;;0;\r\nLIXX_NW_CTR;124.800;LIXA;;;0;\r\nLIXX_SU_CTR;128.800;LIXA;;;0;\r\nLIXX_FSS;125.750;LIXA;;;0;\r\n");
        _albero.Scrivi(Tfl, "LIXX_NC_CTR;CTR;1;CTR;1;\r\n" + Quadrato(42, 13));
        _albero.Scrivi(Comune, "LIXX_NW_CTR;CTR;1;CTR;1;\r\n" + Quadrato(42, 12) + "LIXX_SU_CTR;CTR;1;CTR;1;\r\n" + Quadrato(38, 15));
        _albero.Scrivi(Bordi, Bordo("XX NC", 42, 13) + "DUMMY;\r\n" + Bordo("XX NW", 42, 12) + "DUMMY;\r\n" + Bordo("XX SU", 38, 15));

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

    // Un quadrato di un grado per lato, dall'angolo a sud-ovest.
    private static string Quadrato(int lat, int lon)
        => $"N0{lat}.00.00.000;E0{lon:00}.00.00.000;\r\nN0{lat + 1}.00.00.000;E0{lon:00}.00.00.000;\r\n" +
           $"N0{lat + 1}.00.00.000;E0{lon + 1:00}.00.00.000;\r\nN0{lat}.00.00.000;E0{lon + 1:00}.00.00.000;\r\n";

    private static string Bordo(string nome, int lat, int lon)
        => string.Concat(Quadrato(lat, lon).Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Select(r => $"T;{nome};{r}\r\n"));

    private async Task Apri() => Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

    // --- il motore: VistaPerSettore ----------------------------------------------------------------------------------

    [Fact]
    public void NomiDelBordo_LoScaloDellAccInteroOSenzaLePrimeDueLettere()
    {
        Assert.Equal(["LIRR NC", "RR NC"], VistaPerSettore.NomiDelBordo("lirr_nc_ctr"));
        Assert.Equal(["LIMM WS2", "MM WS2"], VistaPerSettore.NomiDelBordo("LIMM_WS2_CTR"));
        Assert.Empty(VistaPerSettore.NomiDelBordo("LIRR_FSS"));
    }

    // Le posizioni con qualcosa da disegnare, con la frequenza; LIXX_FSS (solo nel .frq) no. L'albero di prova ha anche il
    // sector d'Italia: qui contano le nostre.
    [Fact]
    public async Task Settori_SoloLePosizioniConQualcosaDaDisegnare()
    {
        await Apri();

        var nostre = _lab.SettoriDellaVista().Where(s => s.Codice.StartsWith("LIXX_", StringComparison.Ordinal));
        Assert.Equal(
            [new SettoreDellaVista("LIXX_NC_CTR", "131.200"), new SettoreDellaVista("LIXX_NW_CTR", "124.800"), new SettoreDellaVista("LIXX_SU_CTR", "128.800")],
            nostre);
    }

    [Fact]
    public async Task LaVistaDelSettore_SettoriBordiFileEPosizioni()
    {
        await Apri();

        _lab.ScegliIlSettore("lixx_nc_ctr");

        var sezioni = _lab.VistaDelSettore();
        Assert.Equal(["settori", "bordi", "file", "posizioni"], sezioni.Select(s => s.Id));
        Assert.Equal((Tfl, 0), (sezioni[0].Voci.Single().File, sezioni[0].Voci.Single().Record!.Value));
        Assert.Equal(("XX NC", Bordi, 0), (sezioni[1].Voci.Single().Titolo, sezioni[1].Voci.Single().File, sezioni[1].Voci.Single().Record!.Value));
        Assert.Equal((Tfl, (int?)null), (sezioni[2].Voci.Single().File, sezioni[2].Voci.Single().Record));
        Assert.StartsWith("131.200", sezioni[3].Voci.Single().Nota, StringComparison.Ordinal);
    }

    // --- la sessione: il fuoco su un settore -------------------------------------------------------------------------

    // Le chiavi: il suo settore e il suo bordo (record), il file col suo nome (intero). Accanto: LIXX_NW_CTR, il settore e
    // il bordo; LIXX_SU_CTR, lontano, no.
    [Fact]
    public async Task LavoraSulSettore_LaMappaMostraIlSettore_EAttenuatiQuelliAccanto()
    {
        await Apri();

        Assert.True(_lab.LavoraSulSettore("lixx_nc_ctr"));

        Assert.Equal("LIXX_NC_CTR", _lab.Fuoco);
        Assert.True(_lab.FuocoSuUnSettore);
        Assert.Equal("settore", _lab.GenereDelFuoco);
        Assert.Equal("LIXX_NC_CTR", _lab.SettoreScelto);
        Assert.Equal([Tfl + "#0", Bordi + "#0", Tfl + "#*"], _lab.ChiaviDelFuoco);
        Assert.Equal(_lab.ChiaviDelFuoco, _lab.VistaDaMostrare(SpazioDiLavoro.Sector));
        // L'albero di prova ha anche i settori d'Italia, e alcuni arrivano qui: contano i nostri.
        Assert.Equal([Comune + "#0", Bordi + "#1"], _lab.ChiaviAccanto.Where(c => c.StartsWith(Comune, StringComparison.Ordinal) || c.StartsWith(Bordi, StringComparison.Ordinal)));
        Assert.Equal(_lab.ChiaviAccanto, _lab.AccantoDaMostrare(SpazioDiLavoro.Sector));
        Assert.Empty(_lab.AccantoDaMostrare(SpazioDiLavoro.Aip));
        Assert.Contains("settori", _lab.AccesiDi(SpazioDiLavoro.Sector));

        // Il riquadro è il suo poligono.
        var r = Assert.IsType<RiquadroDelFuoco>(_lab.Riquadro());
        Assert.Equal((42d, 13d, 43d, 14d), (r.Sud, r.Ovest, r.Nord, r.Est));
    }

    // Con una vista a mano dentro il fuoco, niente accanto: si guarda solo quel che si è scelto.
    [Fact]
    public async Task ConUnaVistaAMano_NienteAccanto()
    {
        await Apri();
        _lab.LavoraSulSettore("LIXX_NC_CTR");

        _lab.CambiaLaVista(Tfl, 0);

        Assert.Empty(_lab.AccantoDaMostrare(SpazioDiLavoro.Sector));
        _lab.SvuotaLaVista();
        Assert.NotEmpty(_lab.AccantoDaMostrare(SpazioDiLavoro.Sector));
    }

    [Fact]
    public async Task UnaPosizioneSenzaNienteDaDisegnare_SiRifiuta()
    {
        await Apri();

        Assert.False(_lab.LavoraSulSettore("LIXX_FSS"));

        Assert.Null(_lab.Fuoco);
        Assert.Contains("LIXX_FSS", _lab.Rifiuto, StringComparison.Ordinal);
    }

    // Il fuoco su un settore si ricorda e torna al riavvio; uno scalo dopo un settore torna scalo.
    [Fact]
    public async Task IlSettoreSiRicorda_ETornaAlRiavvio()
    {
        await Apri();
        _lab.LavoraSulSettore("LIXX_NC_CTR");

        var dopo = new SessioneDelLab(_dati);
        Assert.True(await dopo.ApriEValidaAsync(_albero.Radice));
        Assert.Equal(("LIXX_NC_CTR", true), (dopo.Fuoco, dopo.FuocoSuUnSettore));

        dopo.LavoraSu("LIXA");
        Assert.False(dopo.FuocoSuUnSettore);
        var ancora = new SessioneDelLab(_dati);
        Assert.True(await ancora.ApriEValidaAsync(_albero.Radice));
        Assert.Equal(("LIXA", false), (ancora.Fuoco, ancora.FuocoSuUnSettore));
    }

    // R8 anche per il settore: le modifiche nei file del settore (il .tfl col suo nome) stanno in testa.
    [Fact]
    public async Task LeModificheDelSettore_SonoQuelleNeiSuoiFile()
    {
        await Apri();
        _lab.LavoraSulSettore("LIXX_NC_CTR");

        Assert.True(_lab.EDelFuoco(Tfl));
        Assert.False(_lab.EDelFuoco(Comune));
    }

    // --- a schermo ---------------------------------------------------------------------------------------------------

    // La linguetta Settori: scelta la posizione, «◎ Lavora su»; la striscia dice settore e frequenza, «⌖ Il settore».
    [Fact]
    public async Task ASchermo_DallaLinguettaSettoriSiLavoraSulSettore()
    {
        await Apri();
        var colonna = _contesto.RenderComponent<ColonnaDiSinistra>();

        colonna.Find("[data-linguetta='settori']").Click();
        colonna.Find("[data-campo='settore']").Change("LIXX_NC_CTR");
        colonna.WaitForAssertion(() => Assert.NotEmpty(colonna.FindAll($"[data-voce-scalo='{Bordi}#0']")));
        colonna.Find("[data-tasto='lavora-su']").Click();

        colonna.WaitForAssertion(() => Assert.Contains("131.200", colonna.Find("[data-fuoco='LIXX_NC_CTR']").TextContent, StringComparison.Ordinal));
        Assert.Equal("settore", colonna.Find("[data-fuoco]").GetAttribute("data-fuoco-genere"));
        Assert.Contains("Il settore", colonna.Find("[data-tasto='inquadra-il-fuoco']").TextContent, StringComparison.Ordinal);
        Assert.Empty(colonna.FindAll("[data-tasto='lavora-su']"));

        // Il file col suo nome si apre qui, sotto il settore (R3).
        colonna.Find($"[data-voce-scalo='{Tfl}']").Click();
        colonna.WaitForAssertion(() => Assert.NotEmpty(colonna.FindAll($".lab-scali [data-file-aperto='{Tfl}']")));
        Assert.Contains("lab-linguetta-scelta", colonna.Find("[data-linguetta='settori']").ClassName ?? "", StringComparison.Ordinal);
    }

    // Riaperta col fuoco su un settore, la colonna parte dalla linguetta Settori; una scelta dalla mappa ci riporta.
    [Fact]
    public async Task ASchermo_ColFuocoSulSettoreLaColonnaParteDaiSettori()
    {
        await Apri();
        _lab.LavoraSulSettore("LIXX_NC_CTR");
        var colonna = _contesto.RenderComponent<ColonnaDiSinistra>();

        Assert.Contains("lab-linguetta-scelta", colonna.Find("[data-linguetta='settori']").ClassName ?? "", StringComparison.Ordinal);

        colonna.Find("[data-linguetta='sfoglia']").Click();
        _lab.Scegli(Tfl, 0);
        colonna.WaitForAssertion(() => Assert.Contains("lab-linguetta-scelta", colonna.Find("[data-linguetta='settori']").ClassName ?? "", StringComparison.Ordinal));
    }

    // 🔴 Trovato sul banco: con la colonna già sui Settori, una scelta dalla mappa non ridisegnava l'elenco (la voce scelta e
    // il file aperto restavano quelli di prima, fino a un cambio di linguetta). L'elenco si iscrive da sé ai cambiamenti.
    [Fact]
    public async Task ASchermo_UnaSceltaDallaMappaSiVedeSubitoNellElenco()
    {
        await Apri();
        _lab.LavoraSulSettore("LIXX_NC_CTR");
        var colonna = _contesto.RenderComponent<ColonnaDiSinistra>();
        colonna.Find($"[data-voce-scalo='{Bordi}#0']").Click();
        colonna.WaitForAssertion(() => Assert.Contains("lab-voce-scelta", colonna.Find($"[data-voce-scalo='{Bordi}#0']").ClassName ?? "", StringComparison.Ordinal));

        // Dalla mappa: il settore del .tfl, nel file col suo nome (che si apre lì).
        await colonna.InvokeAsync(() => _lab.Scegli(Tfl, 0));

        colonna.WaitForAssertion(() =>
        {
            Assert.Contains("lab-voce-scelta", colonna.Find($"[data-voce-scalo='{Tfl}#0']").ClassName ?? "", StringComparison.Ordinal);
            Assert.DoesNotContain("lab-voce-scelta", colonna.Find($"[data-voce-scalo='{Bordi}#0']").ClassName ?? "", StringComparison.Ordinal);
            Assert.NotEmpty(colonna.FindAll($".lab-scali [data-file-aperto='{Tfl}']"));
        });
    }

    // La mappa riceve le chiavi del settore e quelle accanto (il 4° argomento), e si inquadra.
    [Fact]
    public async Task ASchermo_LaMappaRiceveIlSettoreEQuelliAccanto()
    {
        await Apri();
        var mappa = _contesto.RenderComponent<Vipi.SectorLab.Ui.Components.Mappa>(p => p.Add(m => m.Spazio, SpazioDiLavoro.Sector));
        mappa.WaitForAssertion(() => Assert.NotEmpty(_contesto.JSInterop.Invocations["sectorlab.mappa.crea"]));

        _lab.LavoraSulSettore("LIXX_NC_CTR");

        mappa.WaitForAssertion(() => Assert.Single(_contesto.JSInterop.Invocations["sectorlab.mappa.inquadra"]));
        var vista = _contesto.JSInterop.Invocations["sectorlab.mappa.vista"][^1];
        Assert.Equal(_lab.ChiaviDelFuoco, (string[])vista.Arguments[0]!);
        Assert.Equal(false, vista.Arguments[2]);
        Assert.Equal(_lab.ChiaviAccanto, (string[])vista.Arguments[3]!);
    }
}
