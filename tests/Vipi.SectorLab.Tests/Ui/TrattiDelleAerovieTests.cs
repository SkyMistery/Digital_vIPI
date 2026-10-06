using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// I tratti di un'aerovia (lotto «Subito» slice 14c, «file per file» B2): ogni tratto ha il suo verso e le sue quote,
/// come nel PDF, nel tag <c>//@@</c> sul punto che lo apre — Aurora lo legge come un commento.
/// </summary>
public sealed class TrattiDelleAerovieTests : IDisposable
{
    private const string Aerovie = "SectorFiles/Include/IT/AIRWAY/prova14.lairway";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public TrattiDelleAerovieTests()
    {
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        _contesto.Services.AddSingleton(_lab);
        _contesto.JSInterop.Mode = JSRuntimeMode.Loose;
        _contesto.JSInterop.Setup<bool>("sectorlab.mappa.crea", _ => true).SetResult(true);
        // Tre punti veri dei campioni, un'interruzione, e le etichette in fondo come nel file vero.
        _albero.Scrivi(Aerovie, string.Join("\r\n",
            "//Airway tracks",
            "T;ZZ1;ELKAP;ELKAP;", "T;ZZ1;BIBEK;BIBEK;", "T;ZZ1;GOPOL;GOPOL;",
            "T;BREAK;GOPOL;GOPOL;",
            "T;ZZ1;GIXOM;GIXOM;", "T;ZZ1;USIRU;USIRU;",
            "",
            "//Airway labels",
            "L;ZZ1;N041.50.00.000;E012.10.00.000;") + "\r\n");
    }

    public void Dispose()
    {
        _contesto.Dispose();
        _albero.Dispose();
    }

    private async Task Apri() => Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

    [Fact]
    public async Task UnPezzoDiAeroviaHaUnTrattoPerOgniCoppiaDiPunti()
    {
        await Apri();

        Assert.Equal([(0, "ELKAP", "BIBEK"), (1, "BIBEK", "GOPOL")], _lab.TrattiDellAeroviaDi(Aerovie, 0).Select(t => (t.Ordinale, t.Da, t.A)));
        // L'interruzione non è un'aerovia; il pezzo dopo ha i suoi tratti; le etichette non ne hanno.
        Assert.Empty(_lab.TrattiDellAeroviaDi(Aerovie, 1));
        Assert.Equal([(0, "GIXOM", "USIRU")], _lab.TrattiDellAeroviaDi(Aerovie, 2).Select(t => (t.Ordinale, t.Da, t.A)));
        Assert.Empty(_lab.TrattiDellAeroviaDi(Aerovie, 3));
    }

    [Fact]
    public async Task VersoEQuoteDiUnTrattoVannoNelTagDelPuntoCheLoApre()
    {
        await Apri();

        Assert.True(_lab.CambiaIlTagDelPunto(Aerovie, 0, 1, "dir", "FWD"));
        Assert.True(_lab.CambiaIlTagDelPunto(Aerovie, 0, 1, "lower", "fl 95"));
        Assert.True(_lab.CambiaIlTagDelPunto(Aerovie, 0, 1, "upper", "fl195"));

        var righe = _lab.RigheDiAdesso(Aerovie);
        Assert.Equal(["T;ZZ1;ELKAP;ELKAP;", "//@@\"BIBEK\" dir=fwd lower=FL95 upper=FL195", "T;ZZ1;BIBEK;BIBEK;", "T;ZZ1;GOPOL;GOPOL;"], righe.Skip(1).Take(4));
        var tratto = _lab.TrattiDellAeroviaDi(Aerovie, 0)[1];
        Assert.Equal(("fwd", "FL95", "FL195"), (tratto.Verso, tratto.Inferiore, tratto.Superiore));

        // Un verso che non c'è e una quota che non è una quota si rifiutano col perché.
        Assert.False(_lab.CambiaIlTagDelPunto(Aerovie, 0, 0, "dir", "avanti"));
        Assert.Contains("both", _lab.Rifiuto, StringComparison.Ordinal);
        Assert.False(_lab.CambiaIlTagDelPunto(Aerovie, 0, 0, "lower", "basso"));
    }

    [Fact]
    public async Task ITrattiSiVedonoNellaSchedaESullaMappa()
    {
        await Apri();
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli(Aerovie, 0));

        pagina.WaitForAssertion(() => Assert.Equal(2, pagina.FindAll("[data-tratto]").Count));
        Assert.Contains("ELKAP", pagina.Find("[data-tratto='0'] th").TextContent, StringComparison.Ordinal);
        Assert.Contains("BIBEK", pagina.Find("[data-tratto='0'] th").TextContent, StringComparison.Ordinal);

        pagina.Find("[data-scrivi-tratto='0.dir']").Change("both");
        pagina.Find("[data-scrivi-tratto='0.upper']").Change("FL195");

        pagina.WaitForAssertion(() => Assert.Contains("//@@\"ELKAP\" dir=both upper=FL195", _lab.RigheDiAdesso(Aerovie)));
        // Il passaggio del mouse sulla mappa: il tratto col suo verso e le sue quote.
        Assert.Equal("ELKAP → BIBEK: nei due versi, ? – FL195", _lab.FormaScelta()?.Vincoli);
    }
}
