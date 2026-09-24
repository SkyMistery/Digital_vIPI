using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// A schermo: la riga scritta a mano (clic, Invio, conferma), i tag //@ col lucchetto, il fix nuovo col nome, la vista.
/// </summary>
public sealed class RigaAManoAschermoTests : IDisposable
{
    private const string Prova = "SectorFiles/Include/IT/NAVAIDS/prova.fix";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public RigaAManoAschermoTests()
    {
        _albero.Scrivi(Prova, "//LIBC\r\n//@BC404 nota=si\r\nBC404;N039.05.11.290;E017.03.27.750;3;\r\nBC;518;N039.05.09.890;E017.12.02.940;3;");
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        _contesto.Services.AddSingleton(_lab);
        _contesto.JSInterop.Mode = JSRuntimeMode.Loose;
        _contesto.JSInterop.Setup<bool>("sectorlab.mappa.crea", _ => true).SetResult(true);
    }

    public void Dispose()
    {
        _contesto.Dispose();
        _albero.Dispose();
    }

    private async Task<IRenderedComponent<Home>> ConIlFixScelto()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli(Prova, 0));
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-riga]")));
        return pagina;
    }

    [Fact]
    public async Task UnaRigaSiScriveAManoSoloDopoLaConferma()
    {
        var pagina = await ConIlFixScelto();

        pagina.Find(".lab-ispettore [data-riga='4']").Click();
        pagina.WaitForAssertion(() => Assert.Equal("BC;518;N039.05.09.890;E017.12.02.940;3;",
            pagina.Find("[data-riga-a-mano='4']").GetAttribute("value")));
        pagina.Find("[data-riga-a-mano='4']").Change("BC518;N039.05.09.890;E017.12.02.940;3;");

        // Prima della conferma non è cambiato niente.
        pagina.WaitForAssertion(() => Assert.NotNull(pagina.Find("[data-conferma-riga='4']")));
        Assert.Equal(0, _lab.Modifiche.Quante);

        pagina.Find("[data-tasto='conferma-riga']").Click();

        pagina.WaitForAssertion(() =>
        {
            Assert.Equal(1, _lab.Modifiche.Quante);
            Assert.Equal(2, _lab.Sessione!.File[Prova].Record);
            Assert.Contains("riga 4 scritta a mano", pagina.Find("[data-modifiche]").TextContent);
        });
    }

    [Fact]
    public async Task LasciaComEraNonScriveNiente()
    {
        var pagina = await ConIlFixScelto();
        pagina.Find(".lab-ispettore [data-riga='4']").Click();
        pagina.WaitForAssertion(() => pagina.Find("[data-riga-a-mano='4']"));
        pagina.Find("[data-riga-a-mano='4']").Change("BC518;N039.05.09.890;E017.12.02.940;3;");
        pagina.WaitForAssertion(() => pagina.Find("[data-tasto='lascia-riga']"));

        pagina.Find("[data-tasto='lascia-riga']").Click();

        pagina.WaitForAssertion(() => Assert.Empty(pagina.FindAll("[data-conferma-riga]")));
        Assert.Equal(0, _lab.Modifiche.Quante);
    }

    [Fact]
    public async Task UnTagHaIlLucchettoENonSiApre()
    {
        var pagina = await ConIlFixScelto();

        var tag = pagina.Find(".lab-ispettore [data-riga='2']");
        Assert.Contains("lab-riga-protetta", tag.ClassName);
        tag.Click();

        Assert.Empty(pagina.FindAll("[data-riga-a-mano]"));
    }

    [Fact]
    public async Task IlFixNuovoChiedeIlNome()
    {
        var pagina = await ConIlFixScelto();

        pagina.Find("[data-tasto='aggiungi-record']").Click();
        pagina.WaitForAssertion(() => pagina.Find("[data-campo='nome-nuovo']"));
        Assert.Equal(0, _lab.Modifiche.Quante);
        pagina.Find("[data-campo='nome-nuovo']").Input("BC300");
        pagina.Find("[data-tasto='aggiungi-con-nome']").Click();

        pagina.WaitForAssertion(() =>
        {
            Assert.Equal(1, _lab.Modifiche.Quante);
            Assert.Equal("BC300", _lab.Scheda()!.Etichetta);
        });
    }

    [Fact]
    public async Task IlTastoDellaVistaLaRiempieEGliStratiLaMostrano()
    {
        var pagina = await ConIlFixScelto();

        pagina.Find("[data-tasto='vista-record']").Click();

        pagina.WaitForAssertion(() =>
        {
            Assert.Equal("1", pagina.Find("[data-vista]").GetAttribute("data-vista"));
            Assert.Contains("In vista", pagina.Find("[data-tasto='vista-record']").TextContent);
        });

        pagina.Find("[data-tasto='vista-tutto']").Click();

        pagina.WaitForAssertion(() => Assert.Empty(pagina.FindAll("[data-vista]")));
    }
}
