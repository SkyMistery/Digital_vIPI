using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Modificare un campo e vedere il pannello delle modifiche (carta F3 §2.2 passo 6 e §2.3, slice 6), a schermo.
/// </summary>
public sealed class ModificaDeiCampiTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public ModificaDeiCampiTests()
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

    private async Task<IRenderedComponent<Home>> ConUnFixScelto()
    {
        Assert.True(await _lab.ApriAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        var forma = _lab.Strati.Single(s => s.Id == "punti").Forme.First(f => f.Etichetta == "BC404");
        await pagina.InvokeAsync(() => _lab.Scegli(forma.File, forma.Record));
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-scrivi]")));
        return pagina;
    }

    [Fact]
    public async Task UnCampoSiScriveEFinisceNelPannello()
    {
        var pagina = await ConUnFixScelto();

        pagina.Find("[data-scrivi='Position']").Change("N041.00.00.000 E012.00.00.000");

        pagina.WaitForAssertion(() =>
        {
            Assert.Equal(1, _lab.Modifiche.Quante);
            var pannello = pagina.Find("[data-modifiche]");
            Assert.Contains("BC404", pannello.TextContent);
            // Il diff vero, dallo scrittore: una riga tolta e una aggiunta.
            Assert.Contains("−1 +1", pagina.Find("[data-diff]").TextContent);
        });
    }

    [Fact]
    public async Task IlDiffMostraLaRigaVecchiaELaNuova()
    {
        var pagina = await ConUnFixScelto();

        pagina.Find("[data-scrivi='Position']").Change("N041.00.00.000 E012.00.00.000");

        pagina.WaitForAssertion(() =>
        {
            string diff = pagina.Find("[data-diff]").TextContent;
            Assert.Contains("− ", diff, StringComparison.Ordinal);
            Assert.Contains("+ ", diff, StringComparison.Ordinal);
            Assert.Contains("N041.00.00.000", diff, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task UnValoreCheNonSiLeggeLoDiceENonCambiaNiente()
    {
        var pagina = await ConUnFixScelto();

        pagina.Find("[data-scrivi='Position']").Change("dove mi pare");

        pagina.WaitForAssertion(() =>
        {
            Assert.Contains("coordinata", pagina.Find("[data-rifiuto]").TextContent, StringComparison.OrdinalIgnoreCase);
            Assert.False(_lab.Modifiche.CEQualcosa);
            Assert.Empty(pagina.FindAll("[data-modifiche]"));
        });
    }

    [Fact]
    public async Task AnnullareUnaModificaSvuotaIlPannello()
    {
        var pagina = await ConUnFixScelto();
        pagina.Find("[data-scrivi='Position']").Change("N041.00.00.000 E012.00.00.000");
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-annulla]")));

        pagina.FindAll("[data-annulla]").First().Click();

        pagina.WaitForAssertion(() =>
        {
            Assert.False(_lab.Modifiche.CEQualcosa);
            Assert.Empty(pagina.FindAll("[data-modifiche]"));
        });
    }

    [Fact]
    public async Task AnnullaTuttoToglieLeModificheDiTuttiIFile()
    {
        var pagina = await ConUnFixScelto();
        pagina.Find("[data-scrivi='Position']").Change("N041.00.00.000 E012.00.00.000");
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-tasto='annulla-tutto']")));

        pagina.Find("[data-tasto='annulla-tutto']").Click();

        pagina.WaitForAssertion(() => Assert.False(_lab.Modifiche.CEQualcosa));
    }

    [Fact]
    public async Task DopoLaModificaLaMappaRiprendeLoStratoToccato()
    {
        var pagina = await ConUnFixScelto();
        int prima = _lab.VersioneDellaGeometria;

        pagina.Find("[data-scrivi='Position']").Change("N041.00.00.000 E012.00.00.000");

        pagina.WaitForAssertion(() =>
        {
            // La geometria del file è rifatta: il punto sulla mappa sta DOVE È ADESSO, non dov'era all'apertura.
            Assert.True(_lab.VersioneDellaGeometria > prima);
            var forma = _lab.Strati.Single(s => s.Id == "punti").Forme.First(f => f.Etichetta == "BC404");
            Assert.Equal(41.0, forma.Tratti[0][0].LatitudeDeg, 3);
        });
    }

    [Fact]
    public async Task UnCampoChePortaUnElencoNonSiScriveInQuestaSlice()
    {
        Assert.True(await _lab.ApriAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        var settore = _lab.Strati.Single(s => s.Id == "settori").Forme
            .First(f => f.Punti > 3 && f.File.EndsWith("libb_es_ctr.tfl", StringComparison.Ordinal));
        await pagina.InvokeAsync(() => _lab.Scegli(settore.File, settore.Record));

        pagina.WaitForAssertion(() =>
        {
            var campi = pagina.FindAll("[data-campo-record]");
            Assert.NotEmpty(campi);
            // I vertici sono la slice 7: qui si leggono, non si scrivono.
            var vertici = campi.First(c => c.GetAttribute("data-campo-record") is "Points" or "Punti" or "Vertices");
            Assert.Empty(vertici.QuerySelectorAll("input"));
        });
    }
}
