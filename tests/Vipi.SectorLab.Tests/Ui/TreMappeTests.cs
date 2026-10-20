using Aip;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Riordino R4 (D14, D15): tre spazi di lavoro — AIP espanso, Sector, Confronti — con una mappa ciascuno sullo stesso
/// motore, ognuna col suo stato, che non si perde cambiando spazio.
/// </summary>
public sealed class TreMappeTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public TreMappeTests()
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

    private async Task ConLAipEspanso()
    {
        string espanso = Path.Combine(_albero.Radice, "aip-espanso");
        Directory.CreateDirectory(Path.Combine(espanso, "2611"));
        await File.WriteAllTextAsync(Path.Combine(espanso, "2611", "li.json"), AipJson.Serialize(new AipDocument { State = "LI", Airac = "2611" }));
        Assert.True(_lab.CambiaLAipEspanso(espanso), _lab.Rifiuto);
        Assert.True(await _lab.MostraLAipAsync(true), _lab.Rifiuto);
    }

    [Fact]
    public async Task OgniSpazioHaGliStratiSuoi()
    {
        Assert.True(await _lab.ApriAsync(_albero.Radice));
        await ConLAipEspanso();
        string aip = Vipi.SectorLab.Core.PdfAip.AipSullaMappa.Tipo.Id;

        // L'AIP espanso: le coste e lo strato dell'AIP. Il sector: i suoi. Il Confronti: tutti e due.
        Assert.Equal([StratiDellaMappa.Sfondo.Id, aip], _lab.StratiDi(SpazioDiLavoro.Aip).Select(s => s.Id));
        Assert.DoesNotContain(_lab.StratiDi(SpazioDiLavoro.Sector), s => s.Id == aip);
        Assert.Contains(_lab.StratiDi(SpazioDiLavoro.Confronti), s => s.Id == aip);
        Assert.Contains(_lab.StratiDi(SpazioDiLavoro.Confronti), s => s.Id == "punti");

        // Lo strato dell'AIP si accende nelle mappe che lo mostrano, non in quella del sector.
        Assert.Contains(aip, _lab.AccesiDi(SpazioDiLavoro.Aip));
        Assert.Contains(aip, _lab.AccesiDi(SpazioDiLavoro.Confronti));
        Assert.DoesNotContain(aip, _lab.AccesiDi(SpazioDiLavoro.Sector));
    }

    [Fact]
    public async Task CambiandoSpazioOgniMappaRestaComEra()
    {
        Assert.True(await _lab.ApriAsync(_albero.Radice));
        Assert.Equal(SpazioDiLavoro.Sector, _lab.SpazioAttivo);
        _lab.Accendi("punti", true);

        _lab.ScegliLaPagina(PaginaDelLab.Aip);
        Assert.Equal(SpazioDiLavoro.Confronti, _lab.SpazioAttivo);
        Assert.DoesNotContain("punti", _lab.Accesi);
        _lab.Accendi("aerovie", true);

        // Consegna e Impostazioni non hanno mappa: lo spazio resta quello di prima.
        _lab.ScegliLaPagina(PaginaDelLab.Consegna);
        Assert.Equal(SpazioDiLavoro.Confronti, _lab.SpazioAttivo);

        _lab.ScegliLaPagina(PaginaDelLab.Sector);
        Assert.Contains("punti", _lab.Accesi);
        Assert.DoesNotContain("aerovie", _lab.Accesi);
        Assert.Contains("aerovie", _lab.AccesiDi(SpazioDiLavoro.Confronti));

        _lab.ScegliLaPagina(PaginaDelLab.AipIndice);
        Assert.Equal(SpazioDiLavoro.Aip, _lab.SpazioAttivo);
        Assert.Equal([StratiDellaMappa.Sfondo.Id], _lab.Accesi);
    }

    [Fact]
    public async Task UnRecordDelSectorSceltoDallAipSiAccendeSullaMappaDelSector()
    {
        Assert.True(await _lab.ApriAsync(_albero.Radice));
        var fix = _lab.Strati.Single(s => s.Id == "punti").Forme[0];
        _lab.ScegliLaPagina(PaginaDelLab.AipIndice);

        _lab.Scegli(fix.File, fix.Record);

        Assert.Contains("punti", _lab.AccesiDi(SpazioDiLavoro.Sector));
        Assert.DoesNotContain("punti", _lab.AccesiDi(SpazioDiLavoro.Aip));
    }

    [Fact]
    public async Task UnaMappaNasceQuandoSiEntraNelSuoSpazioEPoiResta()
    {
        Assert.True(await _lab.ApriAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        Assert.Single(pagina.FindAll("[data-mappa-dello-spazio]"));

        pagina.Find("[data-pagina='aipindice']").Click();

        pagina.WaitForAssertion(() =>
        {
            Assert.Equal(2, pagina.FindAll("[data-mappa-dello-spazio]").Count);
            Assert.Contains("lab-nascosto", pagina.Find("[data-mappa-dello-spazio='sector']").ClassList);
            Assert.DoesNotContain("lab-nascosto", pagina.Find("[data-mappa-dello-spazio='aip']").ClassList);
        });
        Assert.NotNull(pagina.Find("#lab-mappa-aip"));
        // Ogni chiamata porta l'id della sua mappa, per ultimo.
        pagina.WaitForAssertion(() => Assert.Contains(_contesto.JSInterop.Invocations,
            i => i.Identifier == "sectorlab.mappa.strato" && i.Arguments[0] as string == StratiDellaMappa.Sfondo.Id
                 && i.Arguments[2] as string == "lab-mappa-aip"));

        pagina.Find("[data-pagina='sector']").Click();
        pagina.WaitForAssertion(() =>
        {
            Assert.Equal(2, pagina.FindAll("[data-mappa-dello-spazio]").Count);
            Assert.DoesNotContain("lab-nascosto", pagina.Find("[data-mappa-dello-spazio='sector']").ClassList);
            Assert.Contains("lab-nascosto", pagina.Find("[data-mappa-dello-spazio='aip']").ClassList);
        });
    }
}
