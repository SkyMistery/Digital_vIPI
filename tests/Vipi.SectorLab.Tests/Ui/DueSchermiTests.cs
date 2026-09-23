using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Due schermi (committente, 23 settembre): i pannelli in un'altra finestra, la mappa in quella principale. Lo stato è
/// uno solo (SessioneDelLab): quel che si sceglie da una parte si vede dall'altra.
/// </summary>
public sealed class DueSchermiTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public DueSchermiTests()
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
    public async Task IlTastoChiedeLAltraFinestra()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();

        pagina.Find("[data-tasto='stacca']").Click();

        _contesto.JSInterop.VerifyInvoke("sectorlab.staccaIPannelli");
    }

    // Mentre i pannelli sono nell'altra finestra, qui restano mappa e strati; tornati, tornano anche qui.
    [Fact]
    public async Task ConIPannelliFuoriQuiRestaLaMappa()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        Assert.NotEmpty(pagina.FindAll("[data-linguetta]"));

        await pagina.InvokeAsync(() => _lab.PannelliAperti(true));

        pagina.WaitForAssertion(() =>
        {
            Assert.Empty(pagina.FindAll("[data-linguetta]"));
            Assert.Empty(pagina.FindAll(".lab-colonna-ispettore"));
            Assert.NotEmpty(pagina.FindAll("[data-strato]"));
            Assert.NotEmpty(pagina.FindAll("[data-tasto='riporta']"));
        });

        pagina.Find("[data-tasto='riporta']").Click();
        _contesto.JSInterop.VerifyInvoke("sectorlab.riportaIPannelli");

        // È il guscio, chiudendo la finestra, a dire che i pannelli sono di nuovo qui.
        await pagina.InvokeAsync(() => _lab.PannelliAperti(false));
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-linguetta]")));
    }

    // Una forma scelta sulla mappa (nella finestra principale) si apre nella finestra dei pannelli.
    [Fact]
    public async Task UnaSceltaSullaMappaSiApreNeiPannelli()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pannelli = _contesto.RenderComponent<Pannelli>();
        Assert.NotEmpty(pannelli.FindAll("[data-pannelli]"));
        Assert.NotEmpty(pannelli.FindAll("[data-linguetta='sfoglia']"));

        var forma = _lab.Strati.Single(s => s.Id == "punti").Forme.First(f => f.Etichetta == "BC404");
        await pannelli.InvokeAsync(() => _lab.Scegli(forma.File, forma.Record));

        pannelli.WaitForAssertion(() =>
            Assert.Equal($"{forma.File}#{forma.Record}", pannelli.Find("[data-ispettore]").GetAttribute("data-ispettore")));
    }

    [Fact]
    public void SenzaCartellaIPannelliRimandanoAllaFinestraPrincipale()
    {
        var pannelli = _contesto.RenderComponent<Pannelli>();

        Assert.NotEmpty(pannelli.FindAll("[data-stato='senza-cartella']"));
    }
}
