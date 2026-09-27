using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>La scheda tipizzata (lotto «Subito», slice 3), a schermo.</summary>
public sealed class SchedaTipizzataAschermoTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public SchedaTipizzataAschermoTests()
    {
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

    private async Task<IRenderedComponent<Home>> ConIlRecord(string file, string etichetta)
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        string relativo = "SectorFiles/Include/IT/" + file;
        int indice = _lab.EtichetteDi(relativo).ToList().IndexOf(etichetta);
        Assert.True(indice >= 0, $"{etichetta} non c'è in {file}");
        await pagina.InvokeAsync(() => _lab.Scegli(relativo, indice));
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-campo-record]")));
        return pagina;
    }

    [Fact]
    public async Task ICampiHannoIlNomeDellAodEIlSignificatoAlPassaggioDelMouse()
    {
        var pagina = await ConIlRecord("NAVAIDS/APT.fix", "BC404");

        var tipo = pagina.Find("[data-campo-record='DisplayType'] th");
        Assert.StartsWith("Tipo", tipo.TextContent.Trim(), StringComparison.Ordinal);
        Assert.Contains("filtro", tipo.GetAttribute("title"), StringComparison.Ordinal);
        Assert.StartsWith("Fix ·", pagina.Find("[data-tipo-record]").TextContent.Trim(), StringComparison.Ordinal);
        Assert.Empty(pagina.FindAll("[data-sconosciuto]"));
        Assert.Empty(pagina.FindAll("[data-campo-record='Source']"));
    }
}
