using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Lotto «Subito» slice 11b («file per file» M4): la scheda della pista mostra la rotta dalle soglie accanto a quella
/// scritta; il pannello corregge una riga o tutte quelle del file con lo stesso problema, in una voce sola.
/// </summary>
public sealed class PisteNelLabTests : IDisposable
{
    private const string Rw = "SectorFiles/Include/IT/OTHER/prova.rw";

    private static readonly TimeSpan Attesa = TimeSpan.FromSeconds(15);

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public PisteNelLabTests()
    {
        _albero.Scrivi(Rw, string.Join("\r\n",
            "//PISTE",
            "LIBA;11L;29R;178;182;109.5;289.5;N041.32.39.760;E015.41.57.420;N041.32.05.070;E015.43.42.470;",
            "LIBD;07;25;162;158;065.49;245.49;N041.08.07.620;E016.45.37.240;N041.08.49.590;E016.47.53.030;",
            "LIRF;16L;34R;13;14;163;343;N041.50.54.690;E012.13.50.980;N041.49.26.330;E012.14.47.370;") + "\r\n");
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

    private async Task Apri() => Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

    [Fact]
    public async Task LaSchedaDellaPistaHaLaRottaDalleSoglie()
    {
        await Apri();
        var scheda = Ispettore.Scheda(_lab.Sessione!.File[Rw], 0, null)!;
        var campo = scheda.Campi.Single(c => c.Nome == "RottaVeraDalleSoglie");
        Assert.Equal("Rotta dalle soglie", campo.NomeDaMostrare);
        Assert.StartsWith("11", campo.Valore, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CorreggiTutteArrotondaOgniRigaDelFileInUnaVoce()
    {
        await Apri();
        var primo = _lab.ProblemiDellAlbero.First(p => p.File == Rw && p.Problema.Regola == Regola.RottaConDecimali);
        Assert.Equal(2, _lab.ConLaStessaCorrezione(primo));

        Assert.True(_lab.CorreggiTutte(primo));

        Assert.Equal("LIBA;11L;29R;178;182;110;290;N041.32.39.760;E015.41.57.420;N041.32.05.070;E015.43.42.470;", _lab.RigheDiAdesso(Rw)[1]);
        Assert.Equal("LIBD;07;25;162;158;065;245;N041.08.07.620;E016.45.37.240;N041.08.49.590;E016.47.53.030;", _lab.RigheDiAdesso(Rw)[2]);
        Assert.Single(_lab.Modifiche.Voci);
    }

    [Fact]
    public async Task AScherno_IlPannelloOffreCorreggiTutte()
    {
        await Apri();
        var pagina = _contesto.RenderComponent<Home>();
        pagina.Find("[data-linguetta='problemi']").Click();
        pagina.WaitForAssertion(timeout: Attesa, assertion: () => Assert.NotEmpty(pagina.FindAll("[data-conti]")));
        pagina.Find("[data-campo='filtro-problemi']").Input("RottaConDecimali");

        // Anche l'itrw.rw del campione ha le sue 44: il tasto è uno per voce, col suo file.
        pagina.WaitForAssertion(() => Assert.Contains(pagina.FindAll("[data-tasto='correggi-tutte']"),
            t => t.TextContent.Contains("Correggi tutte le 2 di prova.rw", StringComparison.Ordinal)));
        pagina.FindAll("[data-tasto='correggi-tutte']").First(t => t.TextContent.Contains("prova.rw", StringComparison.Ordinal)).Click();

        pagina.WaitForAssertion(timeout: Attesa, assertion: () => Assert.Equal("LIBD;07;25;162;158;065;245;N041.08.07.620;E016.45.37.240;N041.08.49.590;E016.47.53.030;", _lab.RigheDiAdesso(Rw)[2]));
    }
}
