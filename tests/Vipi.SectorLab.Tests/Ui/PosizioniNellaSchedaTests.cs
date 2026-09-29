using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Lotto «Subito» slice 11a («file per file» M1, N4): la scheda della posizione scrive i trasferimenti in due liste (e la
/// riga esce sempre in ordine), sceglie profilo, ATIS, D-ATIS e LOA fra i file che ci sono e apre il profilo; il nome
/// di una posizione citata si cambia con «Rinomina».
/// </summary>
public sealed class PosizioniNellaSchedaTests : IDisposable
{
    private const string Frq = "SectorFiles/Include/IT/OTHER/prova.frq";
    private const string Twr = "SectorFiles/Include/IT/PREFS/TWR.cpr";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public PosizioniNellaSchedaTests()
    {
        _albero.Scrivi("SectorFiles/ITALY.isc", "[INFO]\r\nN041.48.01.000\r\nE012.14.20.000\r\n60\r\n45\r\n+4.0\r\nIT\r\n\r\n[ATC]\r\nF;OTHER\\prova.frq\r\n");
        _albero.Scrivi(Frq, "LIML_TWR;118.100;LIML LIMM -LIMC_MAR_APP LIRO;PREFS\\TWR.cpr;;1;;\r\nLIMC_MAR_APP;126.750;LIMC -LIML_TWR;;;0;\r\n");
        _albero.Scrivi(Twr, "[Screens]\r\nAircraftHorizontal=1\r\n");
        _albero.Scrivi("SectorFiles/Include/IT/PREFS/APP.cpr", "[Screens]\r\n");

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
    public async Task ITrasferimentiSiScrivonoInDueListeELaRigaEsceInOrdine()
    {
        await Apri();

        Assert.True(_lab.CambiaCampo(Frq, 0, "Inclusi", "LIML LIMM LIRO LIVK"));
        Assert.Contains("LIML_TWR;118.100;LIML LIMM LIRO LIVK -LIMC_MAR_APP;PREFS\\TWR.cpr;;1;;", _lab.RigheDiAdesso(Frq));
        Assert.True(_lab.CambiaCampo(Frq, 0, "Esclusi", "-LIMC_MAR_APP LIMM_WS2_CTR"));
        Assert.Contains("LIML_TWR;118.100;LIML LIMM LIRO LIVK -LIMC_MAR_APP -LIMM_WS2_CTR;PREFS\\TWR.cpr;;1;;", _lab.RigheDiAdesso(Frq));

        Assert.False(_lab.CambiaCampo(Frq, 0, "Inclusi", "LIML -LIMM"));
        Assert.Contains("altra lista", _lab.Rifiuto, StringComparison.Ordinal);

        // Visto a schermo sul banco: i trasferimenti come sono scritti si leggono, non «Vipi.Sectorfile.Models.Transfer».
        var scheda = Vipi.SectorLab.Core.Ispezione.Ispettore.Scheda(_lab.Sessione!.File[Frq], 0, null)!;
        Assert.Equal("LIML LIMM LIRO LIVK -LIMC_MAR_APP -LIMM_WS2_CTR", scheda.Campi.Single(c => c.Nome == "TransferList").Valore);
    }

    [Fact]
    public async Task IProfiliSiScelgonoFraQuelliCheCiSonoESiAprono()
    {
        await Apri();

        var voci = _lab.Elenchi()!.Voci(FonteDellElenco.Profili);
        Assert.Equal(["PREFS\\APP.cpr", "PREFS\\TWR.cpr"], voci);
        Assert.Equal(Twr, _lab.FileCitato("PREFS\\TWR.cpr"));
        Assert.Equal(Twr, _lab.FileCitato("\\PREFS\\twr.cpr"));
        Assert.Null(_lab.FileCitato("PREFS\\LIPC.cpr"));
    }

    [Fact]
    public async Task IlNomeDiUnaPosizioneCitataSiCambiaConRinomina()
    {
        await Apri();

        Assert.False(_lab.CambiaCampo(Frq, 0, "Code", "LIML_ALT"));
        Assert.Contains("Rinomina", _lab.Rifiuto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AScherno_IlProfiloSiApreDallaScheda_EUnFileCheNonCeSiDice()
    {
        await Apri();
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli(Frq, 0));
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-apri-citato='Profile']")));
        Assert.NotEmpty(pagina.FindAll("[data-scrivi='Inclusi']"));
        Assert.NotEmpty(pagina.FindAll("[data-scrivi='Loa']"));

        pagina.Find("[data-apri-citato='Profile']").Click();
        pagina.WaitForAssertion(() => Assert.Equal(Twr, _lab.FileScelto));
        Assert.Null(_lab.Scelta);

        Assert.True(_lab.CambiaCampo(Frq, 0, "Profile", "PREFS\\LIPC.cpr"));
        await pagina.InvokeAsync(() => _lab.Scegli(Frq, 0));
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-citato-assente='Profile']")));
    }
}
