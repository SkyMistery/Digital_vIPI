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

        _lab.ScegliLaPagina(PaginaDelLab.Confronti);
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

        // Il clic dentro il dispatcher: il Lab aperto può ridisegnare la barra fra la ricerca del tasto e il clic (rosso
        // intermittente, «no event handler with ID»).
        await pagina.InvokeAsync(() => pagina.Find("[data-pagina='aipindice']").Click());

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

        await pagina.InvokeAsync(() => pagina.Find("[data-pagina='sector']").Click());
        pagina.WaitForAssertion(() =>
        {
            Assert.Equal(2, pagina.FindAll("[data-mappa-dello-spazio]").Count);
            Assert.DoesNotContain("lab-nascosto", pagina.Find("[data-mappa-dello-spazio='sector']").ClassList);
            Assert.Contains("lab-nascosto", pagina.Find("[data-mappa-dello-spazio='aip']").ClassList);
        });
    }

    // Carta «L'Indice rifatto», R1: nell'Indice gli Strati sono un pannello sopra la mappa, dietro un tasto, e si chiudono
    // al clic sulla mappa; nelle altre pagine restano una colonna col suo divisore. Il ground ha un titolo suo.
    [Fact]
    public async Task NellIndiceGliStratiStannoSopraLaMappa()
    {
        Assert.True(await _lab.ApriAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        Assert.Equal("colonna", pagina.Find("[data-strati]").GetAttribute("data-strati"));
        Assert.Empty(pagina.FindAll("[data-tasto=strati]"));
        Assert.Contains("strati", pagina.FindAll("[data-divide]").Select(d => d.GetAttribute("data-divide")));

        await pagina.InvokeAsync(() => pagina.Find("[data-pagina='aipindice']").Click());
        pagina.WaitForAssertion(() => Assert.Equal("chiusi", pagina.Find("[data-strati]").GetAttribute("data-strati")));
        Assert.DoesNotContain("strati", pagina.FindAll("[data-divide]").Select(d => d.GetAttribute("data-divide")));
        Assert.Contains("lab-strati-sopra", pagina.Find(".lab-con-la-mappa").ClassList);

        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=strati]").Click());
        Assert.Equal("aperti", pagina.Find("[data-strati]").GetAttribute("data-strati"));
        Assert.Equal("true", pagina.Find("[data-tasto=strati]").GetAttribute("aria-expanded"));

        // Il ground del sector sotto il suo titolo, e «Piste» dice da che file viene.
        await pagina.InvokeAsync(() => _lab.MostraIlGroundSullaCarta(true));
        pagina.WaitForAssertion(() =>
        {
            var gruppo = pagina.Find("[data-strati-gruppo=ground]");
            Assert.Contains("Ground del sector", gruppo.TextContent, StringComparison.Ordinal);
            Assert.All(gruppo.QuerySelectorAll("[data-strato]"), s => Assert.Contains(s.GetAttribute("data-strato"), SessioneDelLab.StratiDelGround));
            Assert.Equal(_lab.StratiDi(SpazioDiLavoro.Aip).Count(s => SessioneDelLab.StratiDelGround.Contains(s.Id)), gruppo.QuerySelectorAll("[data-strato]").Length);
        });
        if (pagina.FindAll("[data-strati-gruppo=ground] [data-strato=piste]") is { Count: 1 } piste)
            Assert.Contains("(.rw)", piste.First().ParentElement!.TextContent, StringComparison.Ordinal);

        // Un clic sulla mappa li chiude.
        await pagina.InvokeAsync(() => pagina.Find("[data-mappa-dello-spazio='aip']").Click());
        Assert.Equal("chiusi", pagina.Find("[data-strati]").GetAttribute("data-strati"));
    }
}
