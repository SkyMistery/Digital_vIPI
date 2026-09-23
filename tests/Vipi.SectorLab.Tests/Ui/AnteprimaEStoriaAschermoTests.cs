using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Le richieste del committente del 23 settembre, a schermo: annulla/ripeti nella barra, l'anteprima dell'incolla che
/// segue il testo e la densità, il «°» attaccato al numero, i divisori delle colonne, il tasto del tema.
/// </summary>
public sealed class AnteprimaEStoriaAschermoTests : IDisposable
{
    private const string TestoAip =
        "44°51'24\" N 008°14'57\" E\n" +
        "then arc of circle in clockwise direction radius 17 NM centred on\n" +
        "44°55'29\" N 007°51'43\" E\n" +
        "till point\n" +
        "44°41'08\" N 008°04'34\" E";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public AnteprimaEStoriaAschermoTests()
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

    private async Task<IRenderedComponent<Home>> ConUnSettoreScelto()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        var forma = _lab.Strati.Single(s => s.Id == "settori").Forme
            .First(f => f.Punti > 3 && f.File.EndsWith("libb_es_ctr.tfl", StringComparison.Ordinal));
        await pagina.InvokeAsync(() => _lab.Scegli(forma.File, forma.Record));
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-vertice]")));
        return pagina;
    }

    [Fact]
    public async Task IlTastoAnnullaDisfaLUltimoGestoERipetiLoRifà()
    {
        var pagina = await ConUnSettoreScelto();
        Assert.True(pagina.Find("[data-tasto='annulla-gesto']").HasAttribute("disabled"));

        pagina.FindAll("[data-vertice]").First().Change("N041.00.00.000 E012.00.00.000");
        pagina.WaitForAssertion(() => Assert.Equal(1, _lab.Modifiche.Quante));
        Assert.Contains("Ctrl+Z", pagina.Find("[data-tasto='annulla-gesto']").GetAttribute("title"));

        pagina.Find("[data-tasto='annulla-gesto']").Click();

        pagina.WaitForAssertion(() =>
        {
            Assert.Equal(0, _lab.Modifiche.Quante);
            Assert.False(pagina.Find("[data-tasto='ripeti-gesto']").HasAttribute("disabled"));
        });

        pagina.Find("[data-tasto='ripeti-gesto']").Click();

        pagina.WaitForAssertion(() => Assert.Equal(1, _lab.Modifiche.Quante));
    }

    [Fact]
    public async Task CtrlZFuoriDaiCampiVaAlLab()
    {
        var pagina = await ConUnSettoreScelto();
        pagina.FindAll("[data-vertice]").First().Change("N041.00.00.000 E012.00.00.000");
        pagina.WaitForAssertion(() => Assert.Equal(1, _lab.Modifiche.Quante));

        // È quel che fa sectorlab.js al Ctrl+Z: chiama Tasto("annulla") sul componente della storia.
        var storia = pagina.FindComponent<Vipi.SectorLab.Ui.Components.Storia>();
        await storia.Instance.Tasto("annulla");

        Assert.Equal(0, _lab.Modifiche.Quante);
    }

    [Fact]
    public async Task ScrivendoIlTestoSiVedeLAnteprimaPrimaDiIncollare()
    {
        var pagina = await ConUnSettoreScelto();
        int vertici = pagina.FindAll("[data-vertice]").Count;

        pagina.Find("[data-campo='incolla']").Input(TestoAip);

        pagina.WaitForAssertion(() =>
        {
            var anteprima = pagina.Find("[data-anteprima-punti]");
            Assert.True(int.Parse(anteprima.GetAttribute("data-anteprima-punti")!) > 3);
            Assert.Contains("1 arco", pagina.Find("[data-anteprima]").TextContent);
            Assert.NotEmpty(pagina.FindAll(".lab-anteprima-nuova"));
            Assert.NotEmpty(pagina.FindAll(".lab-anteprima-oggi"));
        });
        // Solo l'anteprima: il record non è stato toccato.
        Assert.Equal(0, _lab.Modifiche.Quante);
        Assert.Equal(vertici, pagina.FindAll("[data-vertice]").Count);
        Assert.NotNull(_lab.Anteprima);
    }

    [Fact]
    public async Task CambiandoLaDensitaLAnteprimaCambiaSubito()
    {
        var pagina = await ConUnSettoreScelto();
        pagina.Find("[data-campo='incolla']").Input(TestoAip);
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-anteprima-punti]")));

        pagina.Find("[data-campo='gradi-per-punto']").Input("1");
        int fitta = 0;
        pagina.WaitForAssertion(() => fitta = int.Parse(pagina.Find("[data-anteprima-punti]").GetAttribute("data-anteprima-punti")!));

        pagina.Find("[data-campo='gradi-per-punto']").Input("8");

        pagina.WaitForAssertion(() =>
            Assert.True(int.Parse(pagina.Find("[data-anteprima-punti]").GetAttribute("data-anteprima-punti")!) < fitta / 4));
    }

    [Fact]
    public async Task IncollandoLAnteprimaSparisce()
    {
        var pagina = await ConUnSettoreScelto();
        pagina.Find("[data-campo='incolla']").Input(TestoAip);
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-anteprima-punti]")));

        pagina.Find("[data-tasto='incolla']").Click();

        pagina.WaitForAssertion(() =>
        {
            Assert.Equal(1, _lab.Modifiche.Quante);
            Assert.Null(_lab.Anteprima);
            Assert.Empty(pagina.FindAll("[data-anteprima]"));
        });
    }

    [Fact]
    public async Task IlGradoStaAttaccatoAlNumero()
    {
        // Prove del committente, 23 settembre: il «°» andava a capo da solo.
        var pagina = await ConUnSettoreScelto();

        var valore = pagina.Find(".lab-densita-valore");

        Assert.NotNull(valore.QuerySelector("[data-campo='gradi-per-punto']"));
        Assert.Contains("°", valore.TextContent);
    }

    [Fact]
    public async Task LeColonneHannoIDivisoriEDLaBarraIlTema()
    {
        var pagina = await ConUnSettoreScelto();

        var divisori = pagina.FindAll("[data-divide]").Select(d => d.GetAttribute("data-divide")).ToList();

        Assert.Equal(["sfoglia", "ispettore", "strati"], divisori);
        Assert.NotNull(pagina.Find("[data-tasto='tema']"));
    }
}
