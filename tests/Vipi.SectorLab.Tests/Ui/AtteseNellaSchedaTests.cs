using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Lotto «Subito» slice 10c («file per file» L1, L3, U1): la scheda di un fix porta alla sua attesa in rotta (o dice che
/// manca e propone quella col suo nome), la scheda dell'attesa scrive l'info a campi, il nome di un punto non si cambia
/// nel suo campo se qualcuno lo cita, né diventa quello di un altro punto.
/// </summary>
public sealed class AtteseNellaSchedaTests : IDisposable
{
    private const string Fix = "SectorFiles/Include/IT/NAVAIDS/prova.fix";
    private const string Attese = "SectorFiles/Include/IT/HOLDENR.hold";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public AtteseNellaSchedaTests()
    {
        _albero.Scrivi("SectorFiles/ITALY.isc", Isc("""
            [HOLDENR]
            F;HOLDENR.hold

            [FIXES]
            F;NAVAIDS\prova.fix

            [LOW AIRWAY]
            F;AIRWAY\prova.lairway
            """));
        _albero.Scrivi(Fix, "LUSIL;N046.02.35.000;E010.07.00.000;1;0;HLD-LUSIL;\r\nLIBERO;N045.00.00.000;E010.00.00.000;1;0;\r\n");
        _albero.Scrivi("SectorFiles/Include/IT/AIRWAY/prova.lairway", "T;M984;LUSIL;LUSIL;\r\nT;M984;N045.10.00.000;E010.10.00.000;\r\n");
        _albero.Scrivi(Attese, "HLD-LUSIL;N046.02.35.000;E010.07.00.000;LUSIL/225R-9000;\r\n");

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

    private static string Isc(string sezioni)
        => "[INFO]\r\nN041.48.01.000\r\nE012.14.20.000\r\n60\r\n45\r\n+4.0\r\nIT\r\n\r\n" + sezioni.ReplaceLineEndings("\r\n") + "\r\n";

    private async Task Apri() => Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

    [Fact]
    public async Task IlFixPortaAllaSuaAttesa_EQuandoNonCeProponeQuellaColSuoNome()
    {
        await Apri();

        var attesa = _lab.AttesaDi(Fix, 0)!;
        Assert.Equal((Attese, 0, "LUSIL/225R-9000"), (attesa.File, attesa.Indice, attesa.Info));
        Assert.Null(_lab.AttesaDi(Fix, 1));

        Assert.True(_lab.CambiaCampo(Fix, 0, "NomeDellAttesa", "HLD-LUSLI"));
        var assente = _lab.AttesaDi(Fix, 0)!;
        Assert.Null(assente.File);
        Assert.Equal("HLD-LUSIL", assente.Proposta);
    }

    [Fact]
    public async Task LInfoDellAttesaSiScriveAPartiEUnaParteSbagliataSiRifiutaColPerche()
    {
        await Apri();

        Assert.True(_lab.CambiaCampo(Attese, 0, "Rotta", "90"));
        Assert.True(_lab.CambiaCampo(Attese, 0, "Verso", "L"));
        Assert.True(_lab.CambiaCampo(Attese, 0, "Quota", "FL100"));
        Assert.Contains("HLD-LUSIL;N046.02.35.000;E010.07.00.000;LUSIL/090L-FL100;", _lab.RigheDiAdesso(Attese));

        Assert.False(_lab.CambiaCampo(Attese, 0, "Quota", "9000ft"));
        Assert.Contains("piedi", _lab.Rifiuto, StringComparison.Ordinal);
        Assert.False(_lab.CambiaCampo(Attese, 0, "Rotta", "400"));
        Assert.Contains("360", _lab.Rifiuto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IlNomeDiUnPuntoCitatoSiCambiaConRinomina_EUnNomeCheCeGiaNonVa()
    {
        await Apri();

        Assert.False(_lab.CambiaCampo(Fix, 0, "Name", "LUSIX"));
        Assert.Contains("Rinomina", _lab.Rifiuto, StringComparison.Ordinal);
        Assert.False(_lab.CambiaCampo(Fix, 1, "Name", "LUSIL"));
        Assert.Contains("C'è già", _lab.Rifiuto, StringComparison.Ordinal);
        Assert.False(_lab.CambiaCampo(Attese, 0, "Nome", "HLD-ALTRO"));
        Assert.Contains("Rinomina", _lab.Rifiuto, StringComparison.Ordinal);

        // Un punto che nessuno cita si chiama nel suo campo (un fix appena nato).
        Assert.True(_lab.CambiaCampo(Fix, 1, "Name", "NUOVO"));
        Assert.Contains("NUOVO;N045.00.00.000;E010.00.00.000;1;0;", _lab.RigheDiAdesso(Fix));
    }

    [Fact]
    public async Task AScherno_LaSchedaDelFixMostraLAttesaEUnClicLaSceglie()
    {
        await Apri();
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli(Fix, 0));

        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-vai-all-attesa='HLD-LUSIL']")));
        Assert.Contains("LUSIL/225R-9000", pagina.Find("[data-vai-all-attesa]").TextContent, StringComparison.Ordinal);
        pagina.Find("[data-vai-all-attesa]").Click();

        pagina.WaitForAssertion(() => Assert.Equal((Attese, 0), _lab.Scelta));
        // La scheda dell'attesa: le parti dell'info si scrivono (la virata da un elenco).
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("select[data-scrivi='Verso']")));
    }

    [Fact]
    public async Task AScherno_UnAttesaCheNonCeSiDiceESiCorreggeConUnClic()
    {
        await Apri();
        Assert.True(_lab.CambiaCampo(Fix, 0, "NomeDellAttesa", "HLD-LUSLI"));
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli(Fix, 0));

        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-attesa-assente='HLD-LUSLI']")));
        pagina.Find("[data-usa-attesa='HLD-LUSIL']").Click();

        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-vai-all-attesa='HLD-LUSIL']")));
        Assert.Contains("LUSIL;N046.02.35.000;E010.07.00.000;1;0;HLD-LUSIL;", _lab.RigheDiAdesso(Fix));
    }
}
