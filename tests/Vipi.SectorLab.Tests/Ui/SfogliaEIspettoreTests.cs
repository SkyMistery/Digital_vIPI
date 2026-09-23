using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Sfoglia, cerca, ispeziona (carta F3 §2.2 passi 2 e 3, slice 5), a schermo: l'albero, l'elenco dei record di un
/// file, la scheda col record e le sue righe.
/// </summary>
public sealed class SfogliaEIspettoreTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public SfogliaEIspettoreTests()
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

    private async Task<IRenderedComponent<Home>> Aperta()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        return _contesto.RenderComponent<Home>();
    }

    [Fact]
    public async Task LAlberoMostraLeCartelleEIFile()
    {
        var pagina = await Aperta();

        Assert.Contains(pagina.FindAll("[data-cartella]"), c => c.TextContent.Contains("NAVAIDS"));
        Assert.Contains(pagina.FindAll("[data-file]"), f => f.TextContent.Contains("APT.fix"));
    }

    [Fact]
    public async Task ApertoUnFileSiVedonoISuoiRecord()
    {
        var pagina = await Aperta();

        pagina.FindAll("[data-file]").First(f => f.TextContent.Contains("APT.fix")).Click();

        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-record]")));
        Assert.Contains("APT.fix", pagina.Find("[data-file-aperto]").GetAttribute("data-file-aperto"));
    }

    [Fact]
    public async Task SceltoUnRecordLIspettoreNeMostraICampiELeRighe()
    {
        var pagina = await Aperta();
        pagina.FindAll("[data-file]").First(f => f.TextContent.Contains("APT.fix")).Click();

        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-record]")));
        pagina.FindAll("[data-record]").First().Click();

        pagina.WaitForAssertion(() =>
        {
            Assert.NotEmpty(pagina.FindAll("[data-campo-record]"));
            // Le righe grezze col numero vero: è ciò che si cita a un AOD.
            var righe = pagina.FindAll("[data-riga]");
            var prima = righe.First();
            Assert.NotEmpty(righe);
            Assert.True(int.Parse(prima.GetAttribute("data-riga")!) >= 1);
        });
    }

    [Fact]
    public async Task LaRicercaPortaAlRecordEAccendeIlSuoStrato()
    {
        var pagina = await Aperta();

        pagina.Find("[data-campo='cerca']").Input("BC404");
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-trovato]")));
        pagina.FindAll("[data-trovato]").First(t => t.TextContent.Contains("BC404")).Click();

        pagina.WaitForAssertion(() =>
        {
            Assert.NotNull(_lab.Scelta);
            // Scegliere dalla ricerca accende lo strato: sennò si sceglie qualcosa che sulla mappa non c'è.
            Assert.Contains("punti", _lab.Accesi);
            Assert.Contains("BC404", pagina.Find("[data-ispettore]").TextContent);
        });
    }

    [Fact]
    public async Task CercandoDueLettereONienteNonSiCercaAffatto()
    {
        var pagina = await Aperta();

        pagina.Find("[data-campo='cerca']").Input("B");

        Assert.Empty(pagina.FindAll("[data-trovato]"));
        Assert.Empty(pagina.FindAll("[data-risultati]"));
    }

    [Fact]
    public async Task IlFiltroDeiRecordRestringeLElenco()
    {
        var pagina = await Aperta();
        pagina.FindAll("[data-file]").First(f => f.TextContent.Contains("APT.fix")).Click();
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-record]")));
        int tutti = pagina.FindAll("[data-record]").Count;

        pagina.Find("[data-campo='filtra']").Input("BC404");

        pagina.WaitForAssertion(() =>
        {
            var rimasti = pagina.FindAll("[data-record]");
            Assert.NotEmpty(rimasti);
            Assert.True(rimasti.Count < tutti, $"filtrati {rimasti.Count}, erano {tutti}");
            Assert.All(rimasti, r => Assert.Contains("BC404", r.TextContent));
        });
    }

    [Fact]
    public async Task ScegliereUnaFormaSullaMappaApreIlSuoFileNellAlbero()
    {
        var pagina = await Aperta();
        var forma = _lab.Strati.Single(s => s.Id == "punti").Forme.First(f => f.Punti > 0);

        await pagina.InvokeAsync(() => _lab.Scegli(forma.File, forma.Record));

        pagina.WaitForAssertion(() =>
        {
            Assert.Equal(forma.File, pagina.Find("[data-file-aperto]").GetAttribute("data-file-aperto"));
            Assert.Contains(forma.Etichetta, pagina.Find("[data-ispettore]").TextContent);
        });
    }
}
