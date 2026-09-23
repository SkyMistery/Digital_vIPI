using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Prova 12 di F3-bis (committente, 23 settembre): «selezionando il file lirr.ap non mi si apre nient'altro».
/// Scegliere un file di famiglia deve mostrare i suoi record, e scegliere un record la sua scheda.
/// </summary>
public sealed class SceltaDiUnFileApTests : IDisposable
{
    private const string ApFir = "SectorFiles/Include/IT/OTHER/lirr.ap";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public SceltaDiUnFileApTests()
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
    public async Task ScegliereLirrApMostraISuoiRecordELaScheda()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();

        await pagina.InvokeAsync(() => _lab.ApriFile(ApFir));

        pagina.WaitForAssertion(() =>
        {
            var elenco = pagina.Find($"[data-file-aperto='{ApFir}']");
            Assert.NotEmpty(elenco.QuerySelectorAll("[data-record]"));
            // 🔴 Subito sotto il file scelto, nell'albero: in fondo alla colonna finiva fuori dallo schermo.
            Assert.Equal(ApFir, elenco.PreviousElementSibling?.GetAttribute("data-file"));
        });

        // Le voci di un .ap si chiamano col codice ICAO (LIAA, LIAF…).
        var lirf = pagina.FindAll("[data-record]").First(b => b.TextContent.Trim().EndsWith("LIRF", StringComparison.Ordinal));
        lirf.Click();

        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-scrivi='ElevationFt']")));
    }

    // Secondo clic sul file: si chiude l'elenco dei suoi record, ma la cartella resta aperta (committente, 23 settembre).
    [Fact]
    public async Task TogliereLaSceltaDelFileNonChiudeLaCartella()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();

        pagina.Find($"[data-file='{ApFir}']").Click();
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll($"[data-file-aperto='{ApFir}']")));

        pagina.Find($"[data-file='{ApFir}']").Click();

        pagina.WaitForAssertion(() =>
        {
            Assert.Empty(pagina.FindAll("[data-file-aperto]"));
            Assert.Equal("si", Other(pagina).GetAttribute("data-aperta"));
            Assert.NotEmpty(pagina.FindAll($"[data-file='{ApFir}']"));
        });

        // E si chiude, e si riapre, col clic sul suo nome.
        pagina.Find("[data-cartella='OTHER']").Click();
        pagina.WaitForAssertion(() => Assert.Null(Other(pagina).GetAttribute("data-aperta")));
        pagina.Find("[data-cartella='OTHER']").Click();
        pagina.WaitForAssertion(() => Assert.Equal("si", Other(pagina).GetAttribute("data-aperta")));
    }

    private static AngleSharp.Dom.IElement Other(IRenderedComponent<Home> pagina)
        => pagina.Find("[data-cartella='OTHER']").ParentElement!;
}
