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
}
