using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Aggiungere e togliere un record a schermo (carta F3 §2.3, slice 8).
/// </summary>
public sealed class RecordAschermoTests : IDisposable
{
    private const string Fix = "SectorFiles/Include/IT/NAVAIDS/APT.fix";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public RecordAschermoTests()
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

    private async Task<IRenderedComponent<Home>> ConUnRecordScelto(int indice = 3)
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli(Fix, indice));
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-tasto='aggiungi-record']")));
        return pagina;
    }

    [Fact]
    public async Task IlTastoAggiungeUnRecordECiPortaSopra()
    {
        var pagina = await ConUnRecordScelto();
        int quanti = _lab.Sessione!.File[Fix].Record;

        pagina.Find("[data-tasto='aggiungi-record']").Click();

        pagina.WaitForAssertion(() =>
        {
            Assert.Equal(quanti + 1, _lab.Sessione!.File[Fix].Record);
            // Ci si ritrova SUL record nuovo: è quello che si va a modificare.
            Assert.Equal((Fix, 4), _lab.Scelta);
            Assert.Contains("1 record aggiunti", pagina.Find("[data-modifica]").TextContent);
            Assert.Contains("−0 +1", pagina.Find("[data-diff]").TextContent);
        });
    }

    [Fact]
    public async Task IlTastoToglieIlRecordScelto()
    {
        var pagina = await ConUnRecordScelto();
        int quanti = _lab.Sessione!.File[Fix].Record;

        pagina.Find("[data-tasto='togli-record']").Click();

        pagina.WaitForAssertion(() =>
        {
            Assert.Equal(quanti - 1, _lab.Sessione!.File[Fix].Record);
            Assert.Contains("1 record tolti", pagina.Find("[data-modifica]").TextContent);
            Assert.Contains("−1 +0", pagina.Find("[data-diff]").TextContent);
        });
    }

    [Fact]
    public async Task AnnullareLaStrutturaDalPannelloRimetteTutto()
    {
        var pagina = await ConUnRecordScelto();
        int quanti = _lab.Sessione!.File[Fix].Record;
        pagina.Find("[data-tasto='aggiungi-record']").Click();
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-annulla]")));

        pagina.FindAll("[data-annulla]").First().Click();

        pagina.WaitForAssertion(() =>
        {
            Assert.Equal(quanti, _lab.Sessione!.File[Fix].Record);
            Assert.False(_lab.Modifiche.CEQualcosa);
        });
    }

    [Fact]
    public async Task IlRecordNuovoSiModificaSubitoDaSchermo()
    {
        var pagina = await ConUnRecordScelto();
        pagina.Find("[data-tasto='aggiungi-record']").Click();
        pagina.WaitForAssertion(() => Assert.Equal((Fix, 4), _lab.Scelta));

        pagina.Find("[data-scrivi='Name']").Change("PROVA9");

        pagina.WaitForAssertion(() =>
        {
            Assert.Contains(_lab.Modifiche.Tutte, m => m is ModificaDiCampo { Dopo: "PROVA9" });
            Assert.Contains("PROVA9", pagina.Find("[data-diff]").TextContent);
        });
    }
}
