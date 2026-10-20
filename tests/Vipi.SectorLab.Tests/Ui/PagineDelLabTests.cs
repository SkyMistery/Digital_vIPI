using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Il riordino delle pagine (carta del 20 ottobre 2026, R1): quattro pagine scelte con le linguette della barra, la
/// mappa che non si smonta, la barra uguale nelle due finestre, «Salva (N)» sempre a vista, i Problemi dalla barra.
/// </summary>
public sealed class PagineDelLabTests : IDisposable
{
    private const string Fix = "SectorFiles/Include/IT/NAVAIDS/APT.fix";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public PagineDelLabTests()
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

    [Fact]
    public async Task LeLinguetteCambianoPagina_ELaMappaResta()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();

        Assert.Equal(4, pagina.FindAll("[data-pagina]").Count(e => e.TagName == "BUTTON"));
        Assert.Equal("true", pagina.Find("[data-pagina='sector']").GetAttribute("aria-selected"));
        Assert.NotEmpty(pagina.FindAll("[data-linguetta='sfoglia']"));
        Assert.Equal("no", pagina.Find("[data-mappa-nascosta]").GetAttribute("data-mappa-nascosta"));

        pagina.Find("[data-pagina='aip']").Click();
        Assert.NotEmpty(pagina.FindAll("[data-riquadro-aip]"));
        Assert.Empty(pagina.FindAll("[data-linguetta='sfoglia']"));
        Assert.Equal("no", pagina.Find("[data-mappa-nascosta]").GetAttribute("data-mappa-nascosta"));

        pagina.Find("[data-pagina='consegna']").Click();
        Assert.NotEmpty(pagina.FindAll("[data-pagina-aperta='consegna']"));
        Assert.NotEmpty(pagina.FindAll("[data-riquadro-aurora]"));
        // La mappa c'è ancora (non si rifà, D2): è solo nascosta.
        Assert.NotNull(pagina.Find("[data-mappa]"));
        Assert.Equal("si", pagina.Find("[data-mappa-nascosta]").GetAttribute("data-mappa-nascosta"));

        pagina.Find("[data-pagina='impostazioni']").Click();
        Assert.NotEmpty(pagina.FindAll("[data-impostazioni]"));
        Assert.Contains("record", pagina.Find("[data-barra='numeri']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ErroriEAvvisiNellaBarra_PortanoAiProblemiDaUnAltraPagina()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        _lab.ScegliLaPagina(PaginaDelLab.Consegna);
        var pagina = _contesto.RenderComponent<Home>();

        pagina.Find("[data-barra='problemi']").Click();

        Assert.Equal(PaginaDelLab.Sector, _lab.Pagina);
        pagina.WaitForAssertion(() => Assert.Contains("lab-linguetta-scelta", pagina.Find("[data-linguetta='problemi']").ClassName, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SalvaNellaBarra_DiceQuanteESalva()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        Assert.True(pagina.Find("[data-tasto='salva-barra']").HasAttribute("disabled"));

        await pagina.InvokeAsync(() => Assert.True(_lab.CambiaCampo(Fix, 0, "Name", "PIPPO")));
        var salva = pagina.Find("[data-tasto='salva-barra']");
        Assert.Equal("Salva (1)", salva.TextContent.Trim());

        salva.Click();

        pagina.WaitForAssertion(() => Assert.Equal(0, _lab.Modifiche.Quante), TimeSpan.FromSeconds(10));
        Assert.Contains("PIPPO", File.ReadAllText(Path.Combine(_albero.Radice, Fix.Replace('/', Path.DirectorySeparatorChar))), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CtrlS_Salva()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var storia = _contesto.RenderComponent<Storia>();
        Assert.True(_lab.CambiaCampo(Fix, 0, "Name", "PIPPO"));

        await storia.Instance.Tasto("salva");

        Assert.Equal(0, _lab.Modifiche.Quante);
    }

    [Fact]
    public async Task LaFinestraDeiPannelli_HaLaStessaBarra_ESegueLaPagina()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        _lab.PannelliAperti(true);
        var principale = _contesto.RenderComponent<Home>();
        var pannelli = _contesto.RenderComponent<Pannelli>();

        // La principale tiene solo la mappa; i pannelli di Sector sono di là, con le linguette.
        Assert.Empty(principale.FindAll("[data-linguetta]"));
        Assert.NotEmpty(pannelli.FindAll("[data-linguetta='sfoglia']"));
        Assert.Equal(4, pannelli.FindAll("button[data-pagina]").Count);

        pannelli.Find("[data-pagina='aip']").Click();

        pannelli.WaitForAssertion(() => Assert.NotEmpty(pannelli.FindAll("[data-riquadro-aip]")));
        principale.WaitForAssertion(() => Assert.Equal("true", principale.Find("[data-pagina='aip']").GetAttribute("aria-selected")));
        Assert.Equal("no", principale.Find("[data-mappa-nascosta]").GetAttribute("data-mappa-nascosta"));
    }
}
