using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Il pannello dei problemi a schermo (carta F3 §2.2 passo 8, slice 10): i conti nella barra e nella linguetta, il
/// filtro, il clic che porta al record e alla riga, e «la tua modifica introduce 1 errore».
/// </summary>
public sealed class ProblemiAschermoTests : IDisposable
{
    private const string Fix = "SectorFiles/Include/IT/NAVAIDS/APT.fix";

    /// <summary>Validare e controllare vanno fuori dal circuito: sul runner della CI più del secondo di base (slice 9).</summary>
    private static readonly TimeSpan Attesa = TimeSpan.FromSeconds(15);

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public ProblemiAschermoTests()
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

    private async Task<IRenderedComponent<Home>> ConIProblemi()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        pagina.Find("[data-linguetta='problemi']").Click();
        pagina.WaitForAssertion(timeout: Attesa, assertion: () => Assert.NotEmpty(pagina.FindAll("[data-conti]")));
        return pagina;
    }

    private int Errori => _lab.ProblemiDellAlbero.Count(p => p.Gravita == Gravita.Errore);

    [Fact]
    public async Task IConti_SonoQuelliDelValidatore_NellaBarraENelPannello()
    {
        var pagina = await ConIProblemi();

        int avvisi = _lab.ProblemiDellAlbero.Count - Errori;
        Assert.True(Errori > 0);
        Assert.Contains($"{Errori} errori · {avvisi} avvisi", pagina.Find("[data-barra='problemi']").TextContent, StringComparison.Ordinal);
        Assert.Contains($"{Errori} errori · {avvisi} avvisi", pagina.Find("[data-conti]").TextContent, StringComparison.Ordinal);
        Assert.Equal(Math.Min(200, _lab.ProblemiDellAlbero.Count), pagina.FindAll("[data-problema]").Count);
    }

    [Fact]
    public async Task IlFiltroStringeLElenco()
    {
        var pagina = await ConIProblemi();

        pagina.Find("[data-campo='filtro-problemi']").Input("MG763");

        pagina.WaitForAssertion(() =>
        {
            var voci = pagina.FindAll("[data-problema]");
            Assert.NotEmpty(voci);
            Assert.All(voci, v => Assert.Contains("MG763", v.TextContent, StringComparison.Ordinal));
        });
    }

    [Fact]
    public async Task IlClicSuUnProblemaConRecord_LoSceglie_ESegnaLaRiga()
    {
        var pagina = await ConIProblemi();
        var problema = _lab.ProblemiDellAlbero.First(p => p.Record is not null);

        pagina.Find($"[data-file='{problema.File}'][data-riga='{problema.Problema.Riga}']").Click();

        Assert.Equal((problema.File, problema.Record!.Value), _lab.Scelta);
        pagina.WaitForAssertion(() =>
        {
            var segnata = pagina.Find($"[data-riga='{problema.Problema.Riga}'][data-segnalata='si']");
            Assert.Contains(problema.Problema.Testo, segnata.TextContent, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task IlClicSuUnaRigaSenzaRecord_MostraLeRigheDelDisco()
    {
        var pagina = await ConIProblemi();
        var illeggibile = _lab.ProblemiDellAlbero.Single(p => p.File == Fix && p.Problema.Regola == Regola.CoordinataIllegibile);

        pagina.Find($"[data-file='{Fix}'][data-riga='{illeggibile.Problema.Riga}']").Click();

        Assert.Null(_lab.Scelta);
        pagina.WaitForAssertion(() =>
        {
            Assert.Equal($"{Fix}:{illeggibile.Problema.Riga}", pagina.Find("[data-ispettore-riga]").GetAttribute("data-ispettore-riga"));
            Assert.Contains("E008-11.31.443", pagina.Find("[data-segnalata='si']").TextContent, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task UnaModificaCheRompeUnaRiga_SiVedeFraIProblemiNuovi()
    {
        var pagina = await ConIProblemi();

        await pagina.InvokeAsync(() => _lab.CambiaCampo(Fix, 3, "Name", "A;B"));
        await _lab.ControlloDelleModifiche;

        pagina.WaitForAssertion(timeout: Attesa, assertion: () =>
        {
            Assert.Contains("1 errore", pagina.Find("[data-nuovi]").TextContent, StringComparison.Ordinal);
            Assert.NotEmpty(pagina.FindAll("[data-segno='nuovi']"));
            Assert.NotEmpty(pagina.FindAll($"[data-fuso='{Fix}']"));
        });

        // Annullata la modifica, non c'è più niente di nuovo.
        await pagina.InvokeAsync(() => _lab.AnnullaTutte());
        await _lab.ControlloDelleModifiche;
        pagina.WaitForAssertion(timeout: Attesa, assertion: () => Assert.Empty(pagina.FindAll("[data-nuovi]")));
    }
}
