using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Il primo avvio di un AOD (20 ottobre): senza sector aperti prima, «Prendilo dal repo» è già aperto, e il link del
/// repo si incolla com'è dal browser.
/// </summary>
public sealed class PrimoAvvioTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private string Dati => Path.Combine(_albero.Radice, "dati-del-lab");

    public PrimoAvvioTests() => _contesto.JSInterop.Mode = JSRuntimeMode.Loose;

    public void Dispose()
    {
        _contesto.Dispose();
        _albero.Dispose();
    }

    [Fact]
    public async Task PrendiloDalRepoEApertoSoloAlPrimoAvvio()
    {
        var primo = new SessioneDelLab(Dati);
        _contesto.Services.AddSingleton(primo);
        var pagina = _contesto.RenderComponent<Home>();
        Assert.True(pagina.Find("[data-prendi]").HasAttribute("open"));

        Assert.True(await primo.ApriAsync(_albero.Radice));

        using var dopo = new TestContext();
        dopo.JSInterop.Mode = JSRuntimeMode.Loose;
        var secondo = new SessioneDelLab(Dati);
        dopo.Services.AddSingleton(secondo);
        Assert.False(dopo.RenderComponent<Home>().Find("[data-prendi]").HasAttribute("open"));
    }

    [Fact]
    public void IlLinkDelBrowserDiventaIlRepoESiRicorda()
    {
        var lab = new SessioneDelLab(Dati);

        lab.CambiaLIndirizzoDelSector("https://github.com/ivao-italy/it-aurora-sector/tree/master/SectorFiles");
        lab.CambiaLIndirizzoDellAipEspanso("https://github.com/SkyMistery/it-aip-espanso-test");

        var riaperto = new SessioneDelLab(Dati);
        Assert.Equal("https://github.com/ivao-italy/it-aurora-sector.git", riaperto.IndirizzoDelSector);
        Assert.Equal("https://github.com/SkyMistery/it-aip-espanso-test.git", riaperto.IndirizzoDellAipEspanso);
    }
}
