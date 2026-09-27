using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Lotto «Subito», slice 2a: i commenti in coda (carta «file per file» §C). Il pannello dei problemi ne dà un avviso per
/// file col gesto «sposta sopra i commenti», la riga scritta a mano quello per la riga sola; tutti e due sono una voce
/// nelle modifiche, e si annullano.
/// </summary>
public sealed class CommentiInCodaAschermoTests : IDisposable
{
    private const string Prova = "SectorFiles/Include/IT/NAVAIDS/prova.fix";

    private static readonly TimeSpan Attesa = TimeSpan.FromSeconds(15);

    private static readonly string[] Righe =
    [
        "//LIBC",
        "BC404;N039.05.11.290;E017.03.27.750;3; //vicino a Crotone",
        "BC518;N039.05.09.890;E017.12.02.940;3;",
        "BC621;N038.47.00.430;E016.54.47.900;3; //da controllare",
    ];

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public CommentiInCodaAschermoTests()
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

    [Fact]
    public async Task UnAvvisoPerFileETuttiSiSpostanoSopra()
    {
        await Apri();
        var avviso = Assert.Single(_lab.ProblemiDellAlbero, p => p.File == Prova && p.Problema.Regola == Regola.CommentoInCoda);
        Assert.StartsWith("2 commenti in coda (righe 2, 4)", avviso.Problema.Dettaglio, StringComparison.Ordinal);

        Assert.True(_lab.SpostaICommentiSopra(Prova));

        Assert.Equal(["//LIBC", "//vicino a Crotone", "BC404;N039.05.11.290;E017.03.27.750;3;", Righe[2],
            "//da controllare", "BC621;N038.47.00.430;E016.54.47.900;3;"], _lab.RigheDiAdesso(Prova));
        Assert.Equal(3, _lab.Sessione!.File[Prova].Record);
        var voce = Assert.IsType<ModificaDelTesto>(Assert.Single(_lab.Modifiche.Voci));
        Assert.Equal([2, 3, 5, 6], voce.Righe);
        // Spostarli non introduce problemi nuovi: ne toglie uno.
        await _lab.ControlloDelleModifiche;
        Assert.DoesNotContain(_lab.ProblemiDelleModifiche, p => p.Problema.Regola == Regola.CommentoInCoda);

        Assert.True(_lab.Modifiche.Annulla(_lab.Sessione.File[Prova], voce));
        Assert.Equal(Righe, _lab.RigheDiAdesso(Prova));
    }

    [Fact]
    public async Task SoloLaRigaSceltaEIlRecordRestaScelto()
    {
        await Apri();

        Assert.True(_lab.SpostaICommentiSopra(Prova, 2));

        Assert.Equal([Righe[0], "//vicino a Crotone", "BC404;N039.05.11.290;E017.03.27.750;3;", .. Righe[2..]], _lab.RigheDiAdesso(Prova));
        Assert.Equal((Prova, 0), _lab.Scelta);
        // L'avviso del file cambia riga (la prima col commento ora è la 5, com'era a schermo con itawlow.lairway:187),
        // ma i commenti non crescono: niente di nuovo.
        await _lab.ControlloDelleModifiche;
        Assert.Empty(_lab.ProblemiDelleModifiche);
        Assert.False(_lab.SpostaICommentiSopra(Prova, 4));
        Assert.Contains("non ha un commento in coda", _lab.Rifiuto, StringComparison.Ordinal);
    }

    // Un commento in coda scritto a mano, in un file che ne ha già: le modifiche lo introducono.
    [Fact]
    public async Task UnCommentoInCodaInPiuEUnAvvisoNuovo()
    {
        await Apri();

        Assert.True(_lab.CambiaRigaAMano(Prova, 3, Righe[2] + " //scritto a mano"));

        await _lab.ControlloDelleModifiche;
        var nuovo = Assert.Single(_lab.ProblemiDelleModifiche, p => p.Problema.Regola == Regola.CommentoInCoda);
        Assert.StartsWith("3 commenti in coda", nuovo.Problema.Dettaglio, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IlTastoDelPannelloLiSpostaTutti()
    {
        await Apri();
        var pagina = _contesto.RenderComponent<Home>();
        pagina.Find("[data-linguetta='problemi']").Click();
        pagina.WaitForAssertion(timeout: Attesa, assertion: () => Assert.NotEmpty(pagina.FindAll("[data-conti]")));
        pagina.Find("[data-campo='filtro-problemi']").Input("prova.fix");

        pagina.WaitForAssertion(() => pagina.Find("[data-tasto='sposta-commenti']"));
        pagina.Find("[data-tasto='sposta-commenti']").Click();

        pagina.WaitForAssertion(timeout: Attesa, assertion: () =>
        {
            Assert.Equal(1, _lab.Modifiche.Quante);
            Assert.DoesNotContain(_lab.RigheDiAdesso(Prova), r => r.Contains("; //", StringComparison.Ordinal));
        });
    }

    [Fact]
    public async Task LaRigaAManoOffreDiSpostareIlSuoCommento()
    {
        await Apri();
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli(Prova, 0));
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll(".lab-ispettore [data-riga]")));

        pagina.Find(".lab-ispettore [data-riga='2']").Click();
        pagina.WaitForAssertion(() => pagina.Find("[data-commento-in-coda='2']"));
        pagina.Find("[data-tasto='sposta-commento']").Click();

        pagina.WaitForAssertion(() =>
        {
            Assert.Equal(1, _lab.Modifiche.Quante);
            Assert.Equal("//vicino a Crotone", _lab.RigheDiAdesso(Prova)[1]);
            Assert.Empty(pagina.FindAll("[data-commento-in-coda]"));
        });
    }
}
