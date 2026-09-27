using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Problemi;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Lotto «Subito», slice 2c: la correzione proposta per una coordinata scritta male. La voce del pannello la mostra, il
/// tasto «Correggi la riga» la scrive come una riga a mano (una voce, si annulla), e non la scrive se la riga è già
/// cambiata.
/// </summary>
public sealed class CorrezioniAschermoTests : IDisposable
{
    private const string Prova = "SectorFiles/Include/IT/NAVAIDS/prova.fix";

    private static readonly TimeSpan Attesa = TimeSpan.FromSeconds(15);

    private static readonly string[] Righe =
    [
        "BC404;n039.05.11.290;E017.03.27.750;3;",
        "BC518;N039.05.09.890;E17.12.02.940;3;",
    ];

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public CorrezioniAschermoTests()
    {
        _albero.Scrivi(Prova, string.Join("\r\n", Righe) + "\r\n");
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

    private ProblemaNelLab Problema(Regola regola) => _lab.ProblemiDellAlbero.Single(p => p.File == Prova && p.Problema.Regola == regola);

    [Fact]
    public async Task LaCorrezioneSiScriveESiAnnulla()
    {
        await Apri();
        var minuscolo = Problema(Regola.EmisferoMinuscolo);
        Assert.Equal("BC404;N039.05.11.290;E017.03.27.750;3;", minuscolo.Problema.Proposta);

        Assert.True(_lab.Correggi(minuscolo));

        Assert.Equal("BC404;N039.05.11.290;E017.03.27.750;3;", _lab.RigheDiAdesso(Prova)[0]);
        var voce = Assert.IsType<ModificaDelTesto>(Assert.Single(_lab.Modifiche.Voci));
        await _lab.ControlloDelleModifiche;
        Assert.Empty(_lab.ProblemiDelleModifiche);
        Assert.True(_lab.Modifiche.Annulla(_lab.Sessione!.File[Prova], voce));
        Assert.Equal(Righe, _lab.RigheDiAdesso(Prova));
    }

    [Fact]
    public async Task SeLaRigaECambiataLaCorrezioneNonSiScrive()
    {
        await Apri();
        var gradi = Problema(Regola.CoordinataFuoriForma);
        Assert.True(_lab.CambiaRigaAMano(Prova, 2, "BC518;N039.05.09.890;E017.12.02.000;3;"));

        Assert.False(_lab.Correggi(gradi));

        Assert.Contains("già cambiata", _lab.Rifiuto, StringComparison.Ordinal);
        Assert.Equal("BC518;N039.05.09.890;E017.12.02.000;3;", _lab.RigheDiAdesso(Prova)[1]);
    }

    [Fact]
    public async Task IlTastoDelPannelloCorreggeLaRiga()
    {
        await Apri();
        var pagina = _contesto.RenderComponent<Home>();
        pagina.Find("[data-linguetta='problemi']").Click();
        pagina.WaitForAssertion(timeout: Attesa, assertion: () => Assert.NotEmpty(pagina.FindAll("[data-conti]")));
        pagina.Find("[data-campo='filtro-problemi']").Input("CoordinataFuoriForma");

        pagina.WaitForAssertion(() => Assert.Contains("E017.12.02.940", pagina.Find("[data-proposta='2']").TextContent, StringComparison.Ordinal));
        pagina.Find("[data-tasto='correggi']").Click();

        pagina.WaitForAssertion(timeout: Attesa, assertion: () =>
        {
            Assert.Equal(1, _lab.Modifiche.Quante);
            Assert.Equal("BC518;N039.05.09.890;E017.12.02.940;3;", _lab.RigheDiAdesso(Prova)[1]);
        });
    }
}
