using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Il punto VFR nuovo a schermo (lotto «Subito» slice 16c, F3), sul <c>lirf.vfi</c> vero dei campioni.
/// 🔴 Preso sul banco: «Aggiungi» restava spento finché non si sceglieva un tipo (0-3), ma il tipo di un punto VFR è
/// facoltativo e sul fork non lo scrive nessuno dei 586 punti — il nuovo sarebbe stato l'unico ad averlo.
/// </summary>
public sealed class PuntoVfrNuovoAschermoTests : IDisposable
{
    private const string Vfi = "SectorFiles/Include/IT/lirf.vfi";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public PuntoVfrNuovoAschermoTests()
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
    public async Task IlNuovoChiedeNomeECodice_EIlTipoPuoRestareNonScritto()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        int colombo = _lab.EtichetteDi(Vfi).ToList().IndexOf("COLOMBO");
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli(Vfi, colombo));
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-tasto='aggiungi-record']")));

        pagina.Find("[data-tasto='aggiungi-record']").Click();

        // Il codice è già scritto: le lettere di COLOMBO (RFS3) e il primo numero libero dopo il suo.
        string proposto = pagina.Find("[data-campo='codice-nuovo']").GetAttribute("value")!;
        Assert.Equal(_lab.CodiceProposto(Vfi, colombo), proposto);
        Assert.StartsWith("RFS", proposto, StringComparison.Ordinal);
        Assert.NotEqual("RFS3", proposto);

        pagina.Find("[data-campo='nome-nuovo']").Input("PROVA DEL LAB");

        // Senza scegliere un tipo «Aggiungi» si preme, e la riga nasce come le altre: quattro campi.
        Assert.False(pagina.Find("[data-tasto='aggiungi-con-nome']").HasAttribute("disabled"));
        pagina.Find("[data-tasto='aggiungi-con-nome']").Click();

        pagina.WaitForAssertion(() =>
        {
            string nuova = Assert.Single(_lab.RigheDiAdesso(Vfi), r => r.StartsWith("PROVA DEL LAB;", StringComparison.Ordinal));
            Assert.StartsWith($"PROVA DEL LAB;{proposto};", nuova, StringComparison.Ordinal);
            Assert.Equal(4, nuova.TrimEnd(';').Split(';').Length);
        });
    }
}
