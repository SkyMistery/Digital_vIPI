using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Lotto «Subito», slice 6: sotto il file le voci della selezione di Aurora, e la casella che le spegne sulla mappa (una
/// voce intera o una parte sola).
/// </summary>
public sealed class VociAschermoTests : IDisposable
{
    private const string Fra = "SectorFiles/Include/IT/ACC/FRA.artcc";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public VociAschermoTests()
    {
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
    public async Task LeVociSiVedonoESiSpengono()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<VociDelFile>(p => p.Add(v => v.File, Fra));

        Assert.Equal(7, pagina.FindAll("[data-voce]").Count);
        pagina.Find("[data-accendi-voce='NPZ']").Change(false);

        var npz = _lab.VociDi(Fra)!.Single(v => v.Nome == "NPZ");
        Assert.All(npz.Parti, p => Assert.Contains($"{Fra}#{p.Chiave}", _lab.Spenti));
        Assert.False(_lab.Acceso(Fra, SessioneDelLab.PartiDi(npz)));

        pagina.Find($"[data-accendi-parte='{npz.Parti[0].Chiave}']").Change(true);

        Assert.DoesNotContain($"{Fra}#{npz.Parti[0].Chiave}", _lab.Spenti);
        Assert.Contains($"{Fra}#{npz.Parti[1].Chiave}", _lab.Spenti);
    }

    [Fact]
    public async Task IlFiltroValeAncheSuiNomiDellePartiEdIlNomeSceglieIlRecord()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<VociDelFile>(p => p.Add(v => v.File, Fra).Add(v => v.Filtro, "VEKEN"));

        var voce = Assert.Single(pagina.FindAll("[data-voce]"));
        Assert.Equal("NPZ", voce.GetAttribute("data-voce"));

        pagina.Find("[data-parte] .lab-voce-nome").Click();

        Assert.Equal(Fra, _lab.Scelta?.File);
    }

    // --- slice 9b: le procedure per pista e tipo (Q1), la procedura nuova nel gruppo della sua pista (P2) -----------

    [Fact]
    public async Task UnaSidNuovaNascaNelGruppoDellaSuaPista()
    {
        const string Sid = "SectorFiles/Include/IT/lirf.sid";
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<VociDelFile>(p => p.Add(v => v.File, Sid));
        Assert.Contains("per pista e tipo", pagina.Markup, StringComparison.Ordinal);

        pagina.Find("[data-nuova-nella-voce='07 · SID']").Click();

        // lirf.sid: le tre SID della 07, una riga vuota, poi la 25. La nuova è la quarta del gruppo della 07.
        var righe = _lab.RigheDiAdesso(Sid);
        Assert.Equal(["LIRF;07;OST1E;;;;;1;", "LIRF;07;OST1D; ; ;", "LIRF;07;RATI1D; ; ;", "LIRF;07;RATI1D; ; ;", ""], righe.Take(5));
        Assert.Equal(3, _lab.Scelta?.Record);
        Assert.Equal(4, _lab.VociDi(Sid)!.Single(v => v.Nome == "07 · SID").Parti.Count);
    }
}
