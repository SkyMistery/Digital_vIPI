using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Tests.Git;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Il primo avvio di un AOD (20 ottobre): senza sector aperti prima, la schermata d'apertura ha «Primo avvio» coi due
/// link e la cartella di lavoro; dopo, «Prendilo dal repo» come prima. I link si incollano com'è dal browser.
/// </summary>
public sealed class PrimoAvvioTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private string Dati => Path.Combine(_albero.Radice, "dati-del-lab");

    public PrimoAvvioTests() => _contesto.JSInterop.Mode = JSRuntimeMode.Loose;

    public void Dispose()
    {
        _contesto.Dispose();
        _albero.Dispose();
    }

    [Fact]
    public async Task PrimoAvvioSoloSeNonSiEMaiApertoNiente()
    {
        var primo = new SessioneDelLab(Dati);
        _contesto.Services.AddSingleton(primo);
        var pagina = _contesto.RenderComponent<Home>();
        Assert.NotEmpty(pagina.FindAll("[data-primo-avvio]"));
        Assert.Empty(pagina.FindAll("[data-prendi]"));

        string lavoro = Path.Combine(_albero.Radice, "IVAO");
        pagina.Find("[data-campo='cartella-di-lavoro']").Input(lavoro);
        Assert.Contains(Path.Combine(lavoro, "pdf-aip"), pagina.Find("[data-cartelle-del-primo-avvio]").TextContent, StringComparison.Ordinal);

        Assert.True(await primo.ApriAsync(_albero.Radice));

        using var dopo = new TestContext();
        dopo.JSInterop.Mode = JSRuntimeMode.Loose;
        dopo.Services.AddSingleton(new SessioneDelLab(Dati));
        var seconda = dopo.RenderComponent<Home>();
        Assert.Empty(seconda.FindAll("[data-primo-avvio]"));
        Assert.NotEmpty(seconda.FindAll("[data-prendi]"));
    }

    [Fact]
    public void LaCartellaDiAuroraSiSceglieGiaAlPrimoAvvio()
    {
        string aurora = Path.Combine(_albero.Radice, "AuroraWindows");
        Directory.CreateDirectory(Path.Combine(aurora, "Profiles"));
        File.WriteAllText(Path.Combine(aurora, "Profiles", "LIRF_TWR.cpr"), "");
        var lab = new SessioneDelLab(Dati) { CartellaDiAuroraDiBase = Path.Combine(_albero.Radice, "non-ce") };
        _contesto.Services.AddSingleton(lab);
        var pagina = _contesto.RenderComponent<Home>();
        Assert.Contains("non c'è", pagina.Find("[data-cosa-ce-in-aurora]").TextContent, StringComparison.Ordinal);

        pagina.Find("[data-campo='primo-cartella-di-aurora']").Change(aurora);

        Assert.Equal(aurora, lab.CartellaDiAurora);
        Assert.Contains("1 profili", pagina.Find("[data-cosa-ce-in-aurora]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreparaTuttoMetteLeImpostazioniEApreIlSector()
    {
        // Sul server va un sector che si apre: quello dell'albero di prova (il repo di prova da solo non ha l'.isc).
        using var repo = new RepoDiProva();
        string sectorFiles = Path.Combine(_albero.Radice, "SectorFiles");
        foreach (string file in Directory.EnumerateFiles(sectorFiles, "*", SearchOption.AllDirectories))
        {
            string relativo = Path.GetRelativePath(_albero.Radice, file).Replace(Path.DirectorySeparatorChar, '/');
            repo.Scrivi(relativo, File.ReadAllText(file));
        }
        repo.Esegui(repo.Lavoro, "add", "--", "SectorFiles");
        repo.Esegui(repo.Lavoro, "commit", "--quiet", "-m", "il sector dell'albero di prova");
        repo.Esegui(repo.Lavoro, "push", "--quiet");
        var lab = new SessioneDelLab(Dati);
        lab.CambiaLIndirizzoDelSector(repo.Server);
        lab.CambiaLIndirizzoDellAipEspanso(repo.Server);
        string lavoro = Path.Combine(_albero.Radice, "IVAO");

        Assert.True(await lab.PreparaIlPrimoAvvioAsync(lavoro), lab.Errore);

        Assert.Equal(Path.Combine(lavoro, "pdf-aip"), lab.CartellaDeiPdf);
        Assert.Equal(Path.Combine(lavoro, "aip-espanso"), lab.CartellaDellAipEspanso);
        Assert.Equal(StatoDelLab.Aperta, lab.Stato);
        Assert.Null(lab.PassoDelPrimoAvvio);
    }

    [Fact]
    public void IlLinkDelBrowserDiventaIlRepoESiRicorda()
    {
        var lab = new SessioneDelLab(Dati);

        lab.CambiaLIndirizzoDelSector("https://github.com/ivao-italy/it-aurora-sector/tree/master/SectorFiles");
        lab.CambiaLIndirizzoDellAipEspanso("https://github.com/SkyMistery/it-aip-espanso-test");

        var riaperto = new SessioneDelLab(Dati);
        Assert.Equal("https://github.com/ivao-italy/it-aurora-sector.git", riaperto.IndirizzoDelSector);
        Assert.Equal("https://github.com/SkyMistery/it-aip-espanso-test.git", riaperto.IndirizzoDellAipEspanso);
    }
}
