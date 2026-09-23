using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// La pagina con la mappa (carta F3 §2.2, slice 4): apertura della cartella, caselle degli strati, scelta di una
/// forma. Il JavaScript qui non gira — si guarda che la pagina gli chieda le cose giuste, e nell'ordine giusto.
/// </summary>
public sealed class PaginaConLaMappaTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public PaginaConLaMappaTests()
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
    public void ACartellaChiusaSiChiedeLaCartella()
    {
        var pagina = _contesto.RenderComponent<Home>();

        Assert.NotNull(pagina.Find("[data-campo='cartella']"));
        Assert.Empty(pagina.FindAll("[data-strato]"));
        // La prova d'avvio della slice 1 aspetta questo: se sparisce, --autoprova esce 1 e nessuno sa perché.
        _contesto.JSInterop.VerifyInvoke("sectorlab.pronto");
    }

    [Fact]
    public void UnaCartellaCheNonEUnSectorDiceIlMotivoESiResta()
    {
        var pagina = _contesto.RenderComponent<Home>();

        pagina.Find("[data-campo='cartella']").Input(Path.Combine(_albero.Radice, "cartella-che-non-c-e"));
        pagina.Find("[data-tasto='apri']").Click();

        pagina.WaitForAssertion(() => Assert.Contains("non esiste", pagina.Find("[data-stato='errore']").TextContent));
        Assert.NotNull(pagina.Find("[data-campo='cartella']"));
    }

    [Fact]
    public async Task ApertaLaCartellaCiSonoLeCaselleDegliStratiELaMappa()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();

        Assert.Equal(13, pagina.FindAll("[data-strato]").Count);
        Assert.NotNull(pagina.Find("[data-mappa]"));
        Assert.Contains("record", pagina.Find("[data-barra='numeri']").TextContent);

        // Lo sfondo è acceso e non si spegne: senza coste la mappa è un foglio bianco.
        var sfondo = pagina.Find("[data-strato='sfondo']");
        Assert.True(sfondo.HasAttribute("disabled"));
        Assert.True(sfondo.HasAttribute("checked"));
    }

    [Fact]
    public async Task AccendereUnaCasellaChiedeQuelloStratoAllaMappa()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();

        pagina.Find("[data-strato='punti']").Change(true);

        Assert.Contains("punti", _lab.Accesi);
        // La pagina ridisegna quando il servizio la avvisa: l'asserzione aspetta quel giro, non lo dà per fatto.
        pagina.WaitForAssertion(() =>
        {
            var chiamata = _contesto.JSInterop.Invocations["sectorlab.mappa.strato"].Last();
            Assert.Equal("punti", chiamata.Arguments[0]);
            Assert.Equal(true, chiamata.Arguments[1]);
        });
    }

    [Fact]
    public async Task SpegnereLaCasellaToglieLoStratoDallaMappa()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        pagina.Find("[data-strato='punti']").Change(true);
        pagina.WaitForAssertion(() => Assert.Equal(true, _contesto.JSInterop.Invocations["sectorlab.mappa.strato"].Last().Arguments[1]));

        pagina.Find("[data-strato='punti']").Change(false);

        Assert.DoesNotContain("punti", _lab.Accesi);
        pagina.WaitForAssertion(() =>
        {
            var chiamata = _contesto.JSInterop.Invocations["sectorlab.mappa.strato"].Last();
            Assert.Equal("punti", chiamata.Arguments[0]);
            Assert.Equal(false, chiamata.Arguments[1]);
        });
    }

    [Fact]
    public async Task IlRecordSceltoSiVedeESiEvidenziaSullaMappa()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        var forma = _lab.Strati.Single(s => s.Id == "punti").Forme.First(f => f.Punti > 0);

        await pagina.InvokeAsync(() => _lab.Scegli(forma.File, forma.Record));

        pagina.WaitForAssertion(() => Assert.Contains(forma.Etichetta, pagina.Find(".lab-scelta").TextContent));
        pagina.WaitForAssertion(() =>
        {
            var chiamata = _contesto.JSInterop.Invocations["sectorlab.mappa.evidenzia"].Last();
            Assert.Equal(forma.File, chiamata.Arguments[0]);
            Assert.Equal(forma.Record, chiamata.Arguments[1]);
        });
    }

    [Fact]
    public async Task UnaFormaCheNonSiDisegnaDiceQualeNomeManca()
    {
        _albero.Scrivi("SectorFiles/Include/IT/DYNAMIC_SEC/prova.tfl", """
            PROVA_CTR;CTR;1;CTR;1;
            N041.13.55.000;E014.47.19.000;
            NOMEFINTO;NOMEFINTO;

            """);
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        var rotta = _lab.Strati.Single(s => s.Id == "settori").Forme
            .First(f => f.File.EndsWith("prova.tfl", StringComparison.Ordinal));

        await pagina.InvokeAsync(() => _lab.Scegli(rotta.File, rotta.Record));

        pagina.WaitForAssertion(() => Assert.Contains("NOMEFINTO", pagina.Find(".lab-irrisolti").TextContent));
    }

    [Fact]
    public async Task CambiareCartellaRiportaAllaSchermataDApertura()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();

        pagina.Find("[data-tasto='cambia-cartella']").Click();

        Assert.NotNull(pagina.Find("[data-campo='cartella']"));
        // E il campo si ricorda l'ultima cartella aperta: non si ridigita il percorso ogni volta.
        Assert.Equal(_albero.Radice, _lab.UltimaCartella());
    }
}
