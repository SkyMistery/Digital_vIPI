using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Core.Sessione;
using Vipi.SectorLab.Tests.Ui;
using Vipi.SectorLab.Ui.Components;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Sessione;

/// <summary>
/// «Chi lo usa» di un file e dei nomi di colors.def (lotto «Subito» slice 7e, R-1): gli .isc che lo caricano, i .frq
/// che lo citano come profilo, ATIS o D-ATIS; i riempimenti, i bordi e le linee che usano un colore.
/// </summary>
public sealed class ChiUsaIlFileTests : IDisposable
{
    private const string Profilo = "SectorFiles/Include/IT/PREFS/PROVA.cpr";
    private const string Atis = "SectorFiles/Include/IT/prova.atis";
    private const string Def = "SectorFiles/Include/IT/COLORS/prova.def";

    private readonly AlberoDiProva _albero = new();

    public ChiUsaIlFileTests()
    {
        _albero.Scrivi("SectorFiles/ITALY.isc",
            "[INFO]\r\nN041.48.01.000\r\nE012.14.20.000\r\n60\r\n45\r\n+4.0\r\nIT\r\n\r\n"
            + "[ATC]\r\nF;OTHER\\prova.frq\r\n\r\n[DEFINE]\r\nF;IT\\colors\\prova.def\r\n\r\n[GEO]\r\nF;GEO\\prova.geo\r\n");
        _albero.Scrivi("SectorFiles/Include/IT/OTHER/prova.frq",
            "LXXX_APP;120.000;LXXX;PREFS\\PROVA.cpr;\\prova.atis;0;;\r\nLXXX_TWR;118.000;LXXX;PREFS\\PROVA.cpr;;0;;\r\n");
        _albero.Scrivi(Profilo, "PAR_VERTICAL_SCAN=30\r\n");
        _albero.Scrivi(Atis, "[ATIS]\r\n");
        _albero.Scrivi(Def, "ROSSO;#FF0000;\r\nVERDE;#00FF00;\r\n");
        _albero.Scrivi("SectorFiles/Include/IT/GEO/prova.geo",
            "N041.00.00.000;E012.00.00.000;N041.00.01.000;E012.00.01.000;ROSSO;\r\n"
            + "N041.00.01.000;E012.00.01.000;N041.00.02.000;E012.00.02.000;rosso;\r\n");
    }

    public void Dispose() => _albero.Dispose();

    private SessioneAperta Apri() => SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);

    [Fact]
    public void UnProfiloLoCitanoIFrqEUnMasterLoCaricaPerQuesto()
    {
        var usi = ChiUsaIlFile.Di(Apri(), Profilo, _ => []);

        Assert.Equal([("SectorFiles/ITALY.isc", 0, "caricato perché un .frq lo cita"),
                      ("SectorFiles/Include/IT/OTHER/prova.frq", 1, "profilo"),
                      ("SectorFiles/Include/IT/OTHER/prova.frq", 2, "profilo")],
            usi.Select(u => (u.File, u.Riga, u.Come)));
    }

    [Fact]
    public void LaBarraDiTroppoDavantiNonImpedisceDiTrovarlo()
    {
        // `\prova.atis`, come `\liml.atis` in itfreq.frq («file per file» §13).
        var usi = ChiUsaIlFile.Di(Apri(), Atis, _ => []);

        Assert.Contains(usi, u => u.Come == "ATIS" && u.Riga == 1);
    }

    [Fact]
    public void UnFileCitatoConFLoDiceLaSuaRiga()
    {
        var usi = ChiUsaIlFile.Di(Apri(), Def, _ => []);

        var riga = Assert.Single(usi);
        Assert.Equal(("SectorFiles/ITALY.isc", 13, "caricato"), (riga.File, riga.Riga, riga.Come));
    }

    [Fact]
    public void INomiDiUnDefDiconoChiLiUsa()
    {
        var colori = ChiUsaIlFile.Colori(Apri(), Def)!;

        Assert.Equal(["ROSSO", "VERDE"], colori.Select(c => c.Nome));
        // Le maiuscole non contano, come per Aurora.
        Assert.Equal(2, colori[0].Volte);
        Assert.Equal(0, colori[1].Volte);
        Assert.Null(ChiUsaIlFile.Colori(Apri(), Profilo));
    }

    [Fact]
    public async Task NelPannelloDelFileSiVede()
    {
        using var contesto = new TestContext();
        var lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        contesto.Services.AddSingleton(lab);
        contesto.JSInterop.Mode = JSRuntimeMode.Loose;
        Assert.True(await lab.ApriEValidaAsync(_albero.Radice));

        var pagina = contesto.RenderComponent<ChiUsaIlFileVista>(p => p.Add(c => c.Relativo, Def));

        Assert.Equal("1", pagina.Find("[data-chi-usa-il-file]").GetAttribute("data-chi-usa-il-file"));
        Assert.Contains("2 volte", pagina.Find("[data-colore-usato='ROSSO']").TextContent, StringComparison.Ordinal);
        Assert.Contains("mai usato", pagina.Find("[data-colore-usato='VERDE']").TextContent, StringComparison.Ordinal);
    }
}
