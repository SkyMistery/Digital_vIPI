using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>Lotto «Subito», slice 8a: nella scheda, la stessa forma in altri record — e il clic porta alla copia.</summary>
public sealed class StessaFormaAschermoTests : IDisposable
{
    private const string Settore = "SectorFiles/Include/IT/DYNAMIC_SEC/prova.tfl";
    private const string Confine = "SectorFiles/Include/IT/HI_AIRSPACE/prova.hartcc";
    private const string Mappe = "SectorFiles/Include/IT/zzzz.str";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public StessaFormaAschermoTests()
    {
        string Anello(int spostato) => string.Concat(Enumerable.Range(0, 10).Select(i =>
            $"N041.{i:00}.00.000;E012.{(i % 2 == 0 ? 0 : 5) + (i == spostato ? 1 : 0):00}.00.000;\r\n"));
        _albero.Scrivi(Settore, "LZZZ_APP;APP;1;APP;1;\r\n" + Anello(-1));
        _albero.Scrivi(Confine, string.Concat(Anello(-1).Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Reverse()
            .Select(v => "T;ZZ CONF;" + v + "\r\n")));
        _albero.Scrivi(Mappe, "ZZZZ;MAPS;ZZZZ CTR;;;;;1;\r\n" + Anello(4));
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
    public async Task LaSchedaDiceLeCopieUgualiEQuelleDiverseEIlClicPortaAllaCopia()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<StessaFormaNellaScheda>(p => p.Add(c => c.File, Settore).Add(c => c.Record, 0));

        Assert.Contains("1 uguali, 1 diverse", pagina.Find("[data-stessa-forma]").TextContent, StringComparison.Ordinal);
        Assert.Equal("si", pagina.Find($"[data-copia-della-forma='{Confine}#0']").GetAttribute("data-uguale"));
        var diversa = pagina.Find($"[data-copia-della-forma='{Mappe}#0']");
        Assert.Equal("no", diversa.GetAttribute("data-uguale"));
        Assert.Contains("1 vertice solo qui, 1 vertice solo là", diversa.TextContent, StringComparison.Ordinal);

        pagina.Find($"[data-copia-della-forma='{Confine}#0']").Click();

        Assert.Equal((Confine, 0), _lab.Scelta);
    }

    [Fact]
    public async Task DopoUnaModificaLaRispostaSiRifa()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        Assert.Equal(2, _lab.StessaFormaDi(Settore, 0).Single().Copie.Count);

        // Il vertice spostato nel MAPS torna al suo posto: le copie ora sono uguali tutte e due.
        Assert.True(_lab.CambiaRigaAMano(Mappe, 6, "N041.04.00.000;E012.00.00.000;"));

        Assert.All(_lab.StessaFormaDi(Settore, 0).Single().Copie, c => Assert.True(c.Uguale));
    }

    [Fact]
    public async Task UnRecordSenzaCopieNonHaLaSezione()
    {
        _albero.Scrivi(Mappe, "ZZZZ;MAPS;ZZZZ CTR;;;;;1;\r\nN045.00.00.000;E009.00.00.000;\r\nN045.01.00.000;E009.00.00.000;\r\nN045.01.00.000;E009.01.00.000;\r\n");
        _albero.Scrivi(Confine, "T;ZZ CONF;N046.00.00.000;E009.00.00.000;\r\nT;ZZ CONF;N046.01.00.000;E009.00.00.000;\r\nT;ZZ CONF;N046.01.00.000;E009.01.00.000;\r\n");
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<StessaFormaNellaScheda>(p => p.Add(c => c.File, Settore).Add(c => c.Record, 0));

        Assert.Empty(pagina.FindAll("[data-stessa-forma]"));
    }
}
