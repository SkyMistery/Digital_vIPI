using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Dalle osservazioni del committente del 24 settembre: (c) SID, STAR e punti che non sparivano spegnendo la casella,
/// (b) le sezioni che si chiudono, (a) gli archi a un punto ogni 5 gradi di base.
/// </summary>
public sealed class StratiSezioniEGradiTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public StratiSezioniEGradiTests()
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
    public async Task UnoStratoChiestoMentreArrivaNonSiChiedeDueVolte()
    {
        // Il difetto: scegliere un fix accende «punti» e fa partire più avvisi di fila; due sincronizzazioni insieme
        // chiedevano lo strato due volte, e la seconda copia restava sulla mappa anche spegnendo la casella. Qui la fetch
        // dei punti non finisce mai (è lenta): il secondo avviso deve aspettare, non chiedere di nuovo.
        var lenta = _contesto.JSInterop.Setup<int>("sectorlab.mappa.strato", i => i.Arguments[0] as string == "punti" && i.Arguments[1] is true);
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var mappa = _contesto.RenderComponent<Vipi.SectorLab.Ui.Components.Mappa>();
        var forma = _lab.Strati.Single(s => s.Id == "punti").Forme.First();

        await mappa.InvokeAsync(() => _lab.Scegli(forma.File, forma.Record));
        await mappa.InvokeAsync(() => _lab.Accendi("settori", true));
        await mappa.InvokeAsync(() => _lab.Accendi("settori", false));

        Assert.Single(_contesto.JSInterop.Invocations, i => i.Identifier == "sectorlab.mappa.strato"
                                                            && i.Arguments[0] as string == "punti" && i.Arguments[1] is true);

        // Arrivata la fetch, la fila riparte: e lo spegnimento chiesto nel frattempo arriva alla mappa.
        await mappa.InvokeAsync(() => _lab.Accendi("punti", false));
        lenta.SetResult(1);
        mappa.WaitForAssertion(() => Assert.Contains(_contesto.JSInterop.Invocations, i => i.Identifier == "sectorlab.mappa.strato"
                                                                                           && i.Arguments[0] as string == "punti" && i.Arguments[1] is false));
    }

    private async Task<IRenderedComponent<Home>> ConUnSettoreScelto()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        var forma = _lab.Strati.Single(s => s.Id == "settori").Forme.First(f => f.Punti > 3 && f.File.EndsWith("libb_es_ctr.tfl", StringComparison.Ordinal));
        await pagina.InvokeAsync(() => _lab.Scegli(forma.File, forma.Record));
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-vertice]")));
        return pagina;
    }

    [Fact]
    public async Task UnaSezioneSiChiudeERestaChiusaSulRecordDopo()
    {
        var pagina = await ConUnSettoreScelto();
        Assert.NotEmpty(pagina.FindAll(".lab-ispettore [data-riga]"));

        pagina.Find("[data-apri-sezione='scheda-righe']").Click();

        pagina.WaitForAssertion(() => Assert.Empty(pagina.FindAll(".lab-ispettore [data-riga]")));
        Assert.NotEmpty(pagina.FindAll("[data-vertice]"));

        // Un altro record: le righe restano chiuse (lo stato è per sezione, non per record).
        var altro = _lab.Strati.Single(s => s.Id == "settori").Forme.First(f => f.File != _lab.Scelta!.Value.File);
        await pagina.InvokeAsync(() => _lab.Scegli(altro.File, altro.Record));
        pagina.WaitForAssertion(() => Assert.Equal(altro.File, pagina.Find("[data-ispettore]").GetAttribute("data-ispettore")!.Split('#')[0]));
        Assert.Empty(pagina.FindAll(".lab-ispettore [data-riga]"));

        pagina.Find("[data-apri-sezione='scheda-righe']").Click();
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll(".lab-ispettore [data-riga]")));
    }

    [Fact]
    public async Task UnTrattoSiChiudeDaSolo()
    {
        var pagina = await ConUnSettoreScelto();

        pagina.Find("h2[data-elenco] .lab-sezione-tasto").Click();

        pagina.WaitForAssertion(() => Assert.Empty(pagina.FindAll("[data-vertice]")));
        // Il titolo resta, con quanti vertici ha; le righe del file no, non si toccano.
        Assert.NotNull(pagina.Find("h2[data-elenco]"));
        Assert.NotEmpty(pagina.FindAll(".lab-ispettore [data-riga]"));
    }

    [Fact]
    public async Task GliArchiSonoAUnPuntoOgni5GradiDiBase()
    {
        var pagina = await ConUnSettoreScelto();

        Assert.Equal("5", pagina.Find("[data-campo='gradi-per-punto']").GetAttribute("value"));
    }
}
