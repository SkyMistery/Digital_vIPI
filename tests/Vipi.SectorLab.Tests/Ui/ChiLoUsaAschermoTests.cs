using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>Lotto «Subito», slice 7: nella scheda di un punto, chi lo usa — e il clic porta alla riga che lo cita.</summary>
public sealed class ChiLoUsaAschermoTests : IDisposable
{
    private const string Prova = "SectorFiles/Include/IT/NAVAIDS/prova.fix";
    private const string Aerovia = "SectorFiles/Include/IT/AIRWAY/prova.lairway";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public ChiLoUsaAschermoTests()
    {
        _albero.Scrivi("SectorFiles/ITALY.isc",
            "[INFO]\r\nN041.48.01.000\r\nE012.14.20.000\r\n60\r\n45\r\n+4.0\r\nIT\r\n\r\n"
            + "[FIXES]\r\nF;NAVAIDS\\prova.fix\r\n\r\n[LOW AIRWAY]\r\nF;AIRWAY\\prova.lairway\r\n");
        _albero.Scrivi(Prova, "LUSIL;N046.02.35.000;E010.07.00.000;1;0;\r\nTOP;N045.00.00.000;E010.00.00.000;1;0;\r\n");
        _albero.Scrivi(Aerovia, "T;M984;TOP;TOP;\r\nT;M984;LUSIL;LUSIL;\r\nT;Y740;LUSIL;LUSIL;\r\n");
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        _contesto.Services.AddSingleton(_lab);
        _contesto.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public void Dispose()
    {
        _contesto.Dispose();
        _albero.Dispose();
    }

    [Fact]
    public async Task LaSchedaDiceChiLoUsaEIlClicPortaAllaRiga()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<ChiLoUsaNellaScheda>(p => p.Add(c => c.File, Prova).Add(c => c.Record, 0));

        Assert.Equal("2", pagina.Find("[data-chi-lo-usa]").GetAttribute("data-chi-lo-usa"));
        Assert.Contains("2 righe in 1 file", pagina.Find("[data-chi-lo-usa]").TextContent, StringComparison.Ordinal);

        pagina.Find($"[data-citazione='{Aerovia}:3']").Click();

        Assert.Equal(Aerovia, _lab.Scelta?.File);
        Assert.Equal((Aerovia, 3), _lab.RigaSegnalata);
    }

    [Fact]
    public async Task UnPuntoCheNessunoCitaLoDice()
    {
        _albero.Scrivi(Aerovia, "T;M984;TOP;TOP;\r\nT;M984;TOP;TOP;\r\n");
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<ChiLoUsaNellaScheda>(p => p.Add(c => c.File, Prova).Add(c => c.Record, 0));

        Assert.NotNull(pagina.Find("[data-nessuno-lo-usa]"));
    }

    [Fact]
    public async Task DopoUnaModificaLaRispostaSiRifa()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        Assert.Equal(2, _lab.UsiDi(Prova, 0)!.Citazioni.Count);

        Assert.True(_lab.CambiaRigaAMano(Aerovia, 3, "T;Y740;TOP;TOP;"));

        Assert.Single(_lab.UsiDi(Prova, 0)!.Citazioni);
    }

    // --- slice 7b: la rinomina ------------------------------------------------------------------------------------

    [Fact]
    public async Task RinominatoDallaSchedaIlNomeNuovoSiRisolveEAnnullatoTorna()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<ChiLoUsaNellaScheda>(p => p.Add(c => c.File, Prova).Add(c => c.Record, 0));

        pagina.Find("[data-rinomina-nome='LUSIL']").Change("LUSIX");

        // Una voce sola, due file; i cataloghi rifatti risolvono il nome nuovo, e le citazioni sono ancora sue.
        Assert.Equal(1, _lab.Modifiche.Quante);
        Assert.Equal(2, _lab.Modifiche.FileToccati.Count);
        Assert.True(_lab.Cataloghi["ITALY.isc"].Risolve("LUSIX"));
        Assert.False(_lab.Cataloghi["ITALY.isc"].Risolve("LUSIL"));
        Assert.Equal(2, _lab.UsiDi(Prova, 0)!.Citazioni.Count);
        Assert.Contains("T;M984;LUSIX;LUSIX;", _lab.RigheDiAdesso(Aerovia));

        _lab.Annulla();

        Assert.False(_lab.Modifiche.CEQualcosa);
        Assert.True(_lab.Cataloghi["ITALY.isc"].Risolve("LUSIL"));
        Assert.Contains("T;M984;LUSIL;LUSIL;", _lab.RigheDiAdesso(Aerovia));
    }

    [Fact]
    public async Task UnPuntoUsatoNonSiToglie()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

        Assert.False(_lab.TogliRecord(Prova, 0));

        Assert.Contains("È usato", _lab.Rifiuto, StringComparison.Ordinal);
        Assert.False(_lab.Modifiche.CEQualcosa);
    }

    [Fact]
    public async Task ConUnNdbOmonimoLaRinominaChiedeEPoiFa()
    {
        _albero.Scrivi("SectorFiles/ITALY.isc",
            "[INFO]\r\nN041.48.01.000\r\nE012.14.20.000\r\n60\r\n45\r\n+4.0\r\nIT\r\n\r\n"
            + "[NDB]\r\nF;NAVAIDS\\prova.ndb\r\n\r\n[VOR]\r\nF;NAVAIDS\\prova.vor\r\n\r\n[LOW AIRWAY]\r\nF;AIRWAY\\prova.lairway\r\n");
        _albero.Scrivi("SectorFiles/Include/IT/NAVAIDS/prova.ndb", "TRP;317.5;N037.54.51.600;E012.29.34.700;\r\n");
        _albero.Scrivi("SectorFiles/Include/IT/NAVAIDS/prova.vor", "TRP;108.80;N037.53.45.500;E012.30.47.500;;;;\r\n");
        _albero.Scrivi(Aerovia, "T;L869;TRP;TRP;\r\n");
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        const string Vor = "SectorFiles/Include/IT/NAVAIDS/prova.vor";
        var pagina = _contesto.RenderComponent<ChiLoUsaNellaScheda>(p => p.Add(c => c.File, Vor).Add(c => c.Record, 0));

        pagina.Find("[data-rinomina-nome='TRP']").Change("TRX");

        // Ferma: la riga vale anche per l'NDB, e la scheda chiede.
        Assert.False(_lab.Modifiche.CEQualcosa);
        Assert.Contains("NDB TRP", pagina.Find("[data-rinomina-domanda]").TextContent, StringComparison.Ordinal);

        pagina.Find("[data-rinomina-omonimi='no']").Click();

        // «No, lasciale»: il VOR cambia nome, la riga dell'aerovia resta all'NDB.
        Assert.Equal(["TRX;108.80;N037.53.45.500;E012.30.47.500;;;;"], _lab.RigheDiAdesso(Vor));
        Assert.Equal(["T;L869;TRP;TRP;"], _lab.RigheDiAdesso(Aerovia));
        Assert.Null(_lab.DaDecidere);
    }
}
