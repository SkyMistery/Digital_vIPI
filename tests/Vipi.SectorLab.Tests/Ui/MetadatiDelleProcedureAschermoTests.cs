using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Prova 68 del committente (28 settembre), a schermo: i metadati di una SID con gli editor giusti, e la ricerca dei file.
/// </summary>
public sealed class MetadatiDelleProcedureAschermoTests : IDisposable
{
    private const string Sid = "SectorFiles/Include/IT/lirf.sid";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public MetadatiDelleProcedureAschermoTests()
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

    private async Task<IRenderedComponent<Home>> ConLaSid()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        int indice = _lab.EtichetteDi(Sid).ToList().FindIndex(e => e.Contains("EKLO8R", StringComparison.Ordinal));
        Assert.True(indice >= 0);
        await pagina.InvokeAsync(() => _lab.Scegli(Sid, indice));
        pagina.WaitForAssertion(() => Assert.NotNull(pagina.Find("[data-metadato='wtc']")));
        return pagina;
    }

    private string? Tag => _lab.RigheDiAdesso(Sid).FirstOrDefault(r => r.StartsWith("//@\"EKLO8R\"", StringComparison.Ordinal));

    [Fact]
    public async Task IlFixInteroEUnPuntoDelSector()
    {
        var pagina = await ConLaSid();

        pagina.Find("input[data-scrivi-tag='fix']").Change("PIPPO");
        pagina.WaitForAssertion(() => Assert.Contains("non è un punto del sector", pagina.Find("[data-rifiuto]").TextContent, StringComparison.Ordinal));
        Assert.Null(Tag);

        pagina.Find("input[data-scrivi-tag='fix']").Change("bc404");   // un fix di APT.fix, caricato da ITALY.isc
        pagina.WaitForAssertion(() => Assert.Equal("//@\"EKLO8R\" fix=BC404", Tag));
    }

    [Fact]
    public async Task LaSalitaInizialeEUnaQuotaOLaSpuntaCooApp()
    {
        var pagina = await ConLaSid();

        pagina.Find("input[data-scrivi-tag='initialclimb']").Change("6000");
        pagina.WaitForAssertion(() => Assert.Equal("//@\"EKLO8R\" initialclimb=6000ft", Tag));

        pagina.Find("input[data-coo-app='initialclimb']").Change(true);
        pagina.WaitForAssertion(() => Assert.Equal("//@\"EKLO8R\" initialclimb=\"COO APP\"", Tag));
        Assert.True(pagina.Find("input[data-scrivi-tag='initialclimb']").HasAttribute("disabled"));

        pagina.Find("input[data-coo-app='initialclimb']").Change(false);
        pagina.WaitForAssertion(() => Assert.Null(Tag));
    }

    [Fact]
    public async Task LeCategorieSonoTastiEIlDoppioClicPortaLePrecedenti()
    {
        var pagina = await ConLaSid();

        pagina.Find("[data-lettere='wtc'] [data-lettera='H']").Click(new MouseEventArgs { Detail = 1 });
        pagina.WaitForAssertion(() => Assert.Equal("//@\"EKLO8R\" wtc=H", Tag));
        // Il secondo clic di un doppio clic: L e M prendono lo stato di H (accesa).
        pagina.Find("[data-lettere='wtc'] [data-lettera='H']").Click(new MouseEventArgs { Detail = 2 });
        pagina.WaitForAssertion(() => Assert.Equal("//@\"EKLO8R\" wtc=LMH", Tag));
        Assert.Contains("lab-pista-scelta", pagina.Find("[data-lettere='wtc'] [data-lettera='L']").ClassName, StringComparison.Ordinal);

        // 🔴 Il doppio clic vero: i due clic arrivano sullo stesso tasto disegnato, prima che la scheda si ridisegni.
        var bottoneS = pagina.Find("[data-lettere='wtc'] [data-lettera='S']");
        bottoneS.Click(new MouseEventArgs { Detail = 1 });
        bottoneS.Click(new MouseEventArgs { Detail = 2 });
        pagina.WaitForAssertion(() => Assert.Equal("//@\"EKLO8R\" wtc=LMHS", Tag));

        pagina.Find("[data-lettere='cat'] [data-lettera='C']").Click(new MouseEventArgs { Detail = 1 });
        pagina.WaitForAssertion(() => Assert.Equal("//@\"EKLO8R\" wtc=LMHS cat=C", Tag));
    }

    [Fact]
    public async Task LaRicercaTrovaIFilePerNomeEUnClicLiApre()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();

        pagina.Find("input[data-campo='cerca']").Input("lirf.s");
        pagina.WaitForAssertion(() => Assert.Equal(2, pagina.FindAll("[data-trovato-file]").Count));
        pagina.Find($"[data-trovato-file='{Sid}']").Click();

        pagina.WaitForAssertion(() => Assert.Equal(Sid, _lab.FileScelto));
        Assert.Empty(pagina.FindAll("[data-trovato-file]"));
    }
}
