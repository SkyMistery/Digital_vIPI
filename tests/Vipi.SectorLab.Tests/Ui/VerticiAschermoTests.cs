using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// I vertici a schermo (carta F3 §2.3, slice 7): l'elenco, il +, il −, e «incolla da testo».
/// </summary>
public sealed class VerticiAschermoTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public VerticiAschermoTests()
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
    public async Task IVerticiSiVedonoUnoPerRiga()
    {
        var pagina = await ConUnSettoreScelto();

        var vertici = pagina.FindAll("[data-vertice]");
        Assert.True(vertici.Count > 3);
        Assert.Matches(@"[NS]\d{3}\.\d{2}\.\d{2}\.\d{3}|[A-Z]{3,}", vertici.First().GetAttribute("value")!);
    }

    [Fact]
    public async Task SpostareUnVerticeFinisceNelPannelloColDiff()
    {
        var pagina = await ConUnSettoreScelto();

        pagina.FindAll("[data-vertice]").First().Change("N041.00.00.000 E012.00.00.000");

        pagina.WaitForAssertion(() =>
        {
            Assert.Equal(1, _lab.Modifiche.Quante);
            Assert.Contains("−1 +1", pagina.Find("[data-diff]").TextContent);
            Assert.Contains("vertice spostato", pagina.Find("[data-modifica]").TextContent);
        });
    }

    [Fact]
    public async Task IlPiuAggiungeUnVerticeEIlMenoLoToglie()
    {
        var pagina = await ConUnSettoreScelto();
        int prima = pagina.FindAll("[data-vertice]").Count;

        pagina.FindAll("[data-aggiungi]").First().Click();

        pagina.WaitForAssertion(() => Assert.Equal(prima + 1, pagina.FindAll("[data-vertice]").Count));

        pagina.FindAll("[data-togli]").First().Click();

        pagina.WaitForAssertion(() =>
        {
            Assert.Equal(prima, pagina.FindAll("[data-vertice]").Count);
            // Tornati come all'apertura: il pannello si svuota da sé.
            Assert.False(_lab.Modifiche.CEQualcosa);
        });
    }

    [Fact]
    public async Task IncollareUnTestoAipConUnArcoRifaLElenco()
    {
        var pagina = await ConUnSettoreScelto();

        pagina.Find("[data-campo='incolla']").Input(
            "44°51'24\" N 008°14'57\" E\n" +
            "then arc of circle in clockwise direction radius 17 NM centred on\n" +
            "44°55'29\" N 007°51'43\" E\n" +
            "till point\n" +
            "44°41'08\" N 008°04'34\" E");
        pagina.Find("[data-tasto='incolla']").Click();

        pagina.WaitForAssertion(() =>
        {
            Assert.Contains("vertici incollati", pagina.Find("[data-modifica]").TextContent);
            Assert.True(pagina.FindAll("[data-vertice]").Count > 4);
        });
    }

    [Fact]
    public async Task UnVerticeCheNonSiLeggeLoDiceENonCambiaNiente()
    {
        var pagina = await ConUnSettoreScelto();
        int prima = pagina.FindAll("[data-vertice]").Count;

        pagina.FindAll("[data-vertice]").First().Change("N041.99.99.999 QUALCOSA");

        pagina.WaitForAssertion(() =>
        {
            Assert.NotNull(pagina.Find("[data-rifiuto]"));
            Assert.False(_lab.Modifiche.CEQualcosa);
            Assert.Equal(prima, pagina.FindAll("[data-vertice]").Count);
        });
    }

    [Fact]
    public async Task UnaZonaAPiuTrattiMostraUnElencoPerTratto()
    {
        // Slice 7-bis: le zone dei .str tengono i punti dentro i segmenti, e a schermo sono «Tratto 1», «Tratto 2»…
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        string zona = "SectorFiles/Include/IT/lirf.str";
        int quale = Enumerable.Range(0, _lab.Sessione!.File[zona].Record)
            .First(i => _lab.ElenchiDiVerticiDi(zona, i).Count(e => e.Quanti > 1) > 1);

        await pagina.InvokeAsync(() => _lab.Scegli(zona, quale));

        pagina.WaitForAssertion(() =>
        {
            var elenchi = pagina.FindAll("[data-elenco]");
            Assert.True(elenchi.Count > 1, $"elenchi a schermo: {elenchi.Count}");
            Assert.Contains("Tratto 1", elenchi.First().TextContent);
            Assert.NotEmpty(pagina.FindAll("[data-vertice]"));
        });
    }

    [Fact]
    public async Task UnPuntoDiUnTrattoSiSpostaDaSchermo()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        string zona = "SectorFiles/Include/IT/lirf.str";
        int quale = Enumerable.Range(0, _lab.Sessione!.File[zona].Record)
            .First(i => _lab.ElenchiDiVerticiDi(zona, i).Count(e => e.Quanti > 1) > 1);
        await pagina.InvokeAsync(() => _lab.Scegli(zona, quale));
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-vertice]")));

        pagina.FindAll("[data-vertice]").First().Change("N041.00.00.000 E012.00.00.000");

        pagina.WaitForAssertion(() =>
        {
            Assert.Equal(1, _lab.Modifiche.Quante);
            Assert.Contains("−1 +1", pagina.Find("[data-diff]").TextContent);
        });
    }

    [Fact]
    public async Task UnFixNonHaVerticiEIlPezzoNonCompare()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        var fix = _lab.Strati.Single(s => s.Id == "punti").Forme.First(f => f.Etichetta == "BC404");

        await pagina.InvokeAsync(() => _lab.Scegli(fix.File, fix.Record));

        pagina.WaitForAssertion(() =>
        {
            Assert.NotEmpty(pagina.FindAll("[data-campo-record]"));
            Assert.Empty(pagina.FindAll("[data-vertice]"));
        });
    }
}
