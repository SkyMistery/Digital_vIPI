using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>Carta F3-bis, slice 5, a schermo: «Composta da» nella scheda di una mappa, una casella per procedura.</summary>
public sealed class CompostaAschermoTests : IDisposable
{
    private const string Lime = "SectorFiles/Include/IT/lime.str";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public CompostaAschermoTests()
    {
        _albero.Scrivi(Lime, string.Join("\r\n",
            "LIME;MAPS;STAR RNAV(ALL);;;;;1;",
            "ODINA;ODINA;<br>", "ODINA;ODINA;4E;", "ME872;ME872;", "OBFUL;OBFUL;", "TIXUM;TIXUM;", "",
            "EKLIB;EKLIB;<br>", "EKLIB;EKLIB;4E;", "ME768;ME768;", "OBFUL;OBFUL;", "",
            "LIME;28:10;ODIN4E;;;;;1;", "ODINA;ODINA;4E;", "ME872;ME872;", "OBFUL;OBFUL;", "TIXUM;TIXUM;", "",
            "LIME;28:10;EKLI4E;;;;;1;", "EKLIB;EKLIB;4E;", "ME768;ME768;", "OBFUL;OBFUL;", "TIXUM;TIXUM;") + "\r\n");
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

    private async Task<IRenderedComponent<Home>> ConLaMappaScelta(int record = 0)
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli(Lime, record));
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-ispettore]")));
        return pagina;
    }

    [Fact]
    public async Task LaSchedaDiUnaMappaHaLeCaselleDelleProcedure()
    {
        var pagina = await ConLaMappaScelta();

        pagina.WaitForAssertion(() =>
        {
            Assert.Equal("0", pagina.Find("[data-composta]").GetAttribute("data-composta"));
            Assert.Equal(["ODIN4E", "EKLI4E"], pagina.FindAll("[data-procedura]").Select(c => c.GetAttribute("data-procedura")));
            Assert.Empty(pagina.FindAll("[data-intere]"));   // senza elenco non c'è niente da disegnare intero
        });
    }

    [Fact]
    public async Task SpuntareUnaProceduraMetteLElencoNelPannello()
    {
        var pagina = await ConLaMappaScelta();

        pagina.Find("[data-procedura='ODIN4E']").Change(true);

        pagina.WaitForAssertion(() =>
        {
            Assert.Equal("1", pagina.Find("[data-composta]").GetAttribute("data-composta"));
            Assert.Contains("composta da: — → ODIN4E", pagina.Find("[data-modifiche]").TextContent, StringComparison.Ordinal);
            Assert.NotEmpty(pagina.FindAll("[data-intere]"));
        });
    }

    [Fact]
    public async Task LaSchedaDiUnaProceduraNonHaCaselle()
    {
        var pagina = await ConLaMappaScelta(record: 1);

        Assert.Empty(pagina.FindAll("[data-composta]"));
    }
}
