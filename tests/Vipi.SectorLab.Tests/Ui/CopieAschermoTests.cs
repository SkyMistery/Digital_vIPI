using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Core.Sessione;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Carta F3-bis, slice 2, a schermo: la modifica portata sulle copie gemelle è una voce sola nel pannello, con il diff
/// di tutti e due i file; la copia già diversa si dice e si allinea col suo tasto.
/// </summary>
public sealed class CopieAschermoTests : IDisposable
{
    private const string Ap = "SectorFiles/Include/IT/OTHER/itap.ap";
    private const string ApFir = "SectorFiles/Include/IT/OTHER/lirr.ap";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public CopieAschermoTests()
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

    private int Indice(string file, string icao)
        => ((IFileConRecord)_lab.Sessione!.File[file]).RecordDelModello.Select((r, i) => (r, i))
            .First(v => v.r is AirportInfo a && a.IcaoCode == icao && !a.IsDisabled).i;

    private async Task<IRenderedComponent<Home>> ConLaQuotaCambiata(string icao, string quota)
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => Assert.True(_lab.CambiaCampo(ApFir, Indice(ApFir, icao), "ElevationFt", quota)));
        return pagina;
    }

    [Fact]
    public async Task UnaVoceSolaConIlDiffDiTuttiEDueIFile()
    {
        var pagina = await ConLaQuotaCambiata("LIRF", "15");

        pagina.WaitForAssertion(() =>
        {
            Assert.Equal("1", pagina.Find("[data-modifiche]").GetAttribute("data-modifiche"));
            Assert.Contains("1 modifiche in 2 file", pagina.Find("[data-modifiche] h2").TextContent, StringComparison.Ordinal);
            Assert.Equal([Ap, ApFir], pagina.FindAll("[data-diff]").Select(d => d.GetAttribute("data-diff")));
            Assert.All(pagina.FindAll("[data-diff] summary"), s => Assert.Contains("−1 +1", s.TextContent, StringComparison.Ordinal));
            Assert.Contains("anche in itap.ap", pagina.Find($"[data-diff='{ApFir}'] .lab-una-modifica").TextContent, StringComparison.Ordinal);
            Assert.Contains("come in lirr.ap", pagina.Find("[data-copia='si']").TextContent, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task AnnullareDallaRigaDellaCopiaTogliLaVoceIntera()
    {
        var pagina = await ConLaQuotaCambiata("LIRF", "15");
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-copia='si']")));

        pagina.Find("[data-copia='si'] [data-annulla]").Click();

        pagina.WaitForAssertion(() =>
        {
            Assert.False(_lab.Modifiche.CEQualcosa);
            Assert.Empty(pagina.FindAll("[data-modifiche]"));
        });
    }

    [Fact]
    public async Task LaCopiaGiaDiversaSiDiceESiAllinea()
    {
        var pagina = await ConLaQuotaCambiata("LIBA", "186");
        pagina.WaitForAssertion(() =>
        {
            Assert.Contains("in itap.ap ha 182: non cambiato", pagina.Find($"[data-lasciata='{Ap}']").TextContent, StringComparison.Ordinal);
            Assert.Equal([ApFir], pagina.FindAll("[data-diff]").Select(d => d.GetAttribute("data-diff")));
        });

        pagina.Find("[data-allinea]").Click();

        pagina.WaitForAssertion(() =>
        {
            Assert.Empty(pagina.FindAll("[data-lasciata]"));
            Assert.Equal([Ap, ApFir], pagina.FindAll("[data-diff]").Select(d => d.GetAttribute("data-diff")));
            Assert.Equal(1, _lab.Modifiche.Quante);
        });
    }
}
