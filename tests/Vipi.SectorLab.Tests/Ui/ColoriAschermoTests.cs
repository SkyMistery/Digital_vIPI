using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Il selettore dei colori nella scheda (lotto «Subito» slice 4, D2 e I1), a schermo: i nomi di colors.def del master,
/// il selettore, l'opacità con l'avviso su Smooth Drawing. Ognuno scrive il solo campo toccato.
/// </summary>
public sealed class ColoriAschermoTests : IDisposable
{
    private const string Pol = "SectorFiles/Include/IT/GND_LAYOUT/prova.pol";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public ColoriAschermoTests()
    {
        // ITALY.isc carica colors.def dei campioni: sono i nomi che la scheda propone.
        string isc = File.ReadAllText(_albero.Percorso("SectorFiles/ITALY.isc"));
        _albero.Scrivi("SectorFiles/ITALY.isc", isc + "\r\n[DEFINE]\r\nF;COLORS\\colors.def\r\n");
        _albero.Scrivi(Pol, "STATIC;GRASS;1;GRASS;\r\nN041.00.00.000;E012.00.00.000;\r\nN042.00.00.000;E013.00.00.000;\r\nN041.00.00.000;E013.00.00.000;\r\n");

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

    private async Task<IRenderedComponent<Home>> ConIlPoligono()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        await _lab.ScegliIscAsync("ITALY.isc");
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli(Pol, 0));
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-campo-record='FillColor'] [data-selettore-colore]")));
        return pagina;
    }

    private string Testa => _lab.RigheDiAdesso(Pol)[0];

    [Fact]
    public async Task UnNomeDiColorsDefSiScegliColSuoColoreESignificato()
    {
        var pagina = await ConIlPoligono();

        var riempimento = pagina.Find("[data-campo-record='FillColor']");
        var erba = riempimento.QuerySelector("[data-nome-colore='GRASS']")!;
        Assert.Contains("lab-nome-colore-scelto", erba.ClassName, StringComparison.Ordinal);
        Assert.Contains("erba", erba.GetAttribute("title"), StringComparison.Ordinal);

        riempimento.QuerySelector("[data-nome-colore='TAXIWAY']")!.Click();

        pagina.WaitForAssertion(() => Assert.Equal("STATIC;TAXIWAY;1;GRASS;", Testa));
        Assert.Equal(1, _lab.Modifiche.Quante);
    }

    [Fact]
    public async Task IlSelettoreScriveRRGGBBELOpacitaAARRGGBBConLAvviso()
    {
        var pagina = await ConIlPoligono();

        pagina.Find("[data-campo-record='FillColor'] [data-selettore-colore]").Change("#ff0000");
        pagina.WaitForAssertion(() => Assert.Equal("STATIC;#FF0000;1;GRASS;", Testa));

        pagina.WaitForAssertion(() => Assert.False(pagina.Find("[data-campo-record='FillColor'] [data-opacita-colore]").HasAttribute("disabled")));
        pagina.Find("[data-campo-record='FillColor'] [data-opacita-colore]").Change("50");
        pagina.WaitForAssertion(() => Assert.Equal("STATIC;#80FF0000;1;GRASS;", Testa));
        pagina.WaitForAssertion(() => Assert.Contains("Smooth Drawing",
            pagina.Find("[data-campo-record='FillColor'] [data-avviso-colore]").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnNomeCheColorsDefNonHaSiDice()
    {
        var pagina = await ConIlPoligono();

        pagina.Find("[data-campo-record='LineColor'] input.lab-campo").Change("COAST");

        pagina.WaitForAssertion(() => Assert.Equal("STATIC;GRASS;1;COAST;", Testa));
        pagina.WaitForAssertion(() => Assert.Contains("non è un nome di colors.def",
            pagina.Find("[data-campo-record='LineColor'] [data-avviso-colore]").TextContent, StringComparison.Ordinal));
    }
}
