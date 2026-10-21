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
    public async Task PrimoAvvioFinchéNonCeLAmbiente()
    {
        var primo = new SessioneDelLab(Dati);
        _contesto.Services.AddSingleton(primo);
        var pagina = _contesto.RenderComponent<Home>();
        Assert.NotEmpty(pagina.FindAll("[data-primo-avvio]"));
        Assert.Empty(pagina.FindAll("[data-prendi]"));

        string lavoro = Path.Combine(_albero.Radice, "IVAO");
        pagina.Find("[data-campo='cartella-di-lavoro']").Input(lavoro);
        Assert.Contains(Path.Combine(lavoro, "pdf-aip"), pagina.Find("[data-cartelle-del-primo-avvio]").TextContent, StringComparison.Ordinal);

        // Aprire un sector (da --cartella) non fa l'ambiente: lo fa solo «Prepara tutto».
        Assert.True(await primo.ApriAsync(_albero.Radice));
        primo.Chiudi();
        // Il ridisegno dopo Chiudi arriva da Cambiata, con InvokeAsync: sul runner della CI non è ancora lì (21 ottobre).
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-primo-avvio]")));

        Directory.CreateDirectory(lavoro);
        File.WriteAllText(Path.Combine(Dati, "cartella-di-lavoro.txt"), lavoro);
        using var dopo = new TestContext();
        dopo.JSInterop.Mode = JSRuntimeMode.Loose;
        dopo.Services.AddSingleton(new SessioneDelLab(Dati));
        var seconda = dopo.RenderComponent<Home>();
        Assert.Empty(seconda.FindAll("[data-primo-avvio]"));
        Assert.NotEmpty(seconda.FindAll("[data-ambiente]"));
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

        // L'ambiente si ricorda; all'avvio dopo si riapre il suo sector.
        var riaperto = new SessioneDelLab(Dati);
        Assert.Equal(lavoro, riaperto.CartellaDiLavoro);
        Assert.Equal(Path.Combine(lavoro, "sector"), riaperto.SectorDaRiaprire());

        // Un altro sector si prende solo nell'ambiente, col nome; si apre ed è il prossimo da riaprire.
        Assert.False(await lab.PrendiUnAltroSectorAsync("aip-espanso"));
        Assert.False(await lab.PrendiUnAltroSectorAsync(@"..\fuori"));
        Assert.True(await lab.PrendiUnAltroSectorAsync("prova"), lab.Errore);
        Assert.Equal([Path.Combine(lavoro, "sector"), Path.Combine(lavoro, "prova")], lab.SectorDellAmbiente());
        Assert.Equal(Path.Combine(lavoro, "prova"), new SessioneDelLab(Dati).SectorDaRiaprire());
    }

    [Fact]
    public async Task ConLAmbienteLeCartelleDeiPdfEDellAipSonoFisse()
    {
        // Committente, 20 ottobre: «blocca anche le cartelle di PDF e AIP espanso»; resta «Apri la cartella» per i PDF.
        string ambiente = Path.Combine(_albero.Radice, "IVAO");
        Directory.CreateDirectory(ambiente);
        Directory.CreateDirectory(Dati);
        File.WriteAllText(Path.Combine(Dati, "cartella-dei-pdf.txt"), Path.Combine(_albero.Radice, "altrove"));
        File.WriteAllText(Path.Combine(Dati, "cartella-di-lavoro.txt"), ambiente);
        var lab = new SessioneDelLab(Dati) { ScegliCartella = (_, _) => Task.FromResult<string?>(null) };

        Assert.True(lab.CartelleDellAipFisse);
        Assert.Equal(Path.Combine(ambiente, "pdf-aip"), lab.CartellaDeiPdf);
        Assert.Equal(Path.Combine(ambiente, "aip-espanso"), lab.CartellaDellAipEspanso);
        Assert.False(lab.CambiaLaCartellaDeiPdf(Path.Combine(_albero.Radice, "altrove")));
        Assert.Contains("suo ambiente", lab.Rifiuto, StringComparison.Ordinal);
        Assert.False(lab.CambiaLAipEspanso(" "));
        Assert.True(lab.CambiaLaCartellaDeiPdf(Path.Combine(ambiente, "pdf-aip")));
        Assert.False(await lab.PrendiLAipEspansoAsync(Path.Combine(_albero.Radice, "altrove")));

        _contesto.Services.AddSingleton(lab);
        var pagina = _contesto.RenderComponent<Vipi.SectorLab.Ui.Components.ImpostazioniDelLab>();
        Assert.NotNull(pagina.Find("[data-cartelle-fisse]"));
        Assert.NotNull(pagina.Find("[data-tasto='apri-cartella-pdf']"));
        Assert.Empty(pagina.FindAll("[data-tasto='sfoglia-pdf']"));
        Assert.Empty(pagina.FindAll("[data-tasto='sfoglia-aip-espanso']"));
        Assert.True(pagina.Find("[data-impostazione='cartella-dei-pdf']").HasAttribute("readonly"));
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
