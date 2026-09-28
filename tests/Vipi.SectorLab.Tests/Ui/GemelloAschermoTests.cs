using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>Lotto «Subito», slice 8e: il gemello di un punto VFR nella scheda, il tasto che lo crea e la domanda dopo «Togli».</summary>
public sealed class GemelloAschermoTests : IDisposable
{
    private const string Vfi = "SectorFiles/Include/IT/lzzz.vfi";
    private const string Nascosti = "SectorFiles/Include/IT/NAVAIDS/VFR_NASCOSTI.fix";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public GemelloAschermoTests()
    {
        _albero.Scrivi(Vfi, "COLOMBO;ZZS3;N041.42.47.000;E012.21.56.000;\r\nALTRO;ZZN1;N041.50.00.000;E012.20.00.000;\r\n");
        _albero.Scrivi(Nascosti, "AAA1;N0400000000;E0100000000;3;\r\nZZS3;N0414247000;E0122156000;3;\r\n");
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        _contesto.Services.AddSingleton(_lab);
        _contesto.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public void Dispose()
    {
        _contesto.Dispose();
        _albero.Dispose();
    }

    [Fact]
    public async Task LaSchedaMostraIlGemelloEIlClicPortaLi()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<GemelloNellaScheda>(p => p.Add(c => c.File, Vfi).Add(c => c.Record, 0));

        Assert.NotNull(pagina.Find("[data-gemello='ZZS3']"));
        Assert.Contains("stessa posizione", pagina.Markup, StringComparison.Ordinal);
        pagina.Find($"[data-copia-gemella='{Nascosti}#1']").Click();

        Assert.Equal((Nascosti, 1), _lab.Scelta);
    }

    [Fact]
    public async Task IlTastoCreaIlGemelloCheManca()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<GemelloNellaScheda>(p => p.Add(c => c.File, Vfi).Add(c => c.Record, 1));

        pagina.Find("[data-crea-gemello='ZZN1']").Click();

        pagina.WaitForAssertion(() => Assert.Contains("stessa posizione", pagina.Markup, StringComparison.Ordinal));
        Assert.Empty(pagina.FindAll("[data-crea-gemello]"));
    }

    [Fact]
    public async Task DopoTogliLaDomandaCompareERispostaSparisce()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var domanda = _contesto.RenderComponent<DomandaSulGemello>();
        Assert.Empty(domanda.FindAll("[data-domanda-gemello]"));

        Assert.True(_lab.TogliRecord(Vfi, 0));

        domanda.WaitForAssertion(() => Assert.NotNull(domanda.Find("[data-domanda-gemello='ZZS3']")));
        domanda.Find("[data-togli-gemello='si']").Click();

        domanda.WaitForAssertion(() => Assert.Empty(domanda.FindAll("[data-domanda-gemello]")));
        Assert.Equal([Nascosti, Vfi], _lab.Modifiche.FileToccati.Order(StringComparer.Ordinal));
    }
}
