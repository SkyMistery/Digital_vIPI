using Aip;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Tests.PdfAip;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Carta F6, slice 0c: il riquadro «AIP» nella barra — la cartella dei PDF, «Prendi i PDF da Download» e «Leggi
/// nell'AIP espanso». La cartella Download è finta, nel temporaneo: il test non tocca mai quella del PC.
/// </summary>
public sealed class AipNellaBarraTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;
    private readonly string _fuori = Path.Combine(Path.GetTempPath(), "aip-barra-" + Guid.NewGuid().ToString("N"));

    public AipNellaBarraTests()
    {
        Directory.CreateDirectory(Path.Combine(_fuori, "Downloads"));
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab")) { Download = Path.Combine(_fuori, "Downloads") };
        _contesto.Services.AddSingleton(_lab);
        _contesto.JSInterop.Mode = JSRuntimeMode.Loose;
        _contesto.JSInterop.Setup<bool>("sectorlab.mappa.crea", _ => true).SetResult(true);
    }

    public void Dispose()
    {
        _contesto.Dispose();
        _albero.Dispose();
        Directory.Delete(_fuori, recursive: true);
    }

    [Fact]
    public async Task DaDownloadAllAipEspanso()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        pagina.WaitForAssertion(() => Assert.Equal("— da scegliere", pagina.Find("[data-quanti-pdf]").TextContent.Trim()), TimeSpan.FromSeconds(10));
        Assert.NotNull(pagina.Find("[data-senza-pdf]"));

        // Le cartelle, come dalle Impostazioni: l'AIP espanso nella cartella dei PDF no.
        var pdf = Path.Combine(_fuori, "AIP");
        Assert.False(_lab.CambiaLaCartellaDeiPdf("relativa"));
        Assert.True(_lab.CambiaLaCartellaDeiPdf(pdf), _lab.Rifiuto);
        Assert.False(_lab.CambiaLAipEspanso(pdf));
        Assert.True(_lab.CambiaLAipEspanso(Path.Combine(_fuori, "espanso")), _lab.Rifiuto);

        // In Download un PDF dell'AIP e uno no: si sposta il primo, l'altro resta.
        await File.WriteAllBytesAsync(Path.Combine(_lab.Download, "LI-ENR 5.1.2.pdf"), PdfAipFinto.Enr51("06 AUG 2026", "LI R901 - Prova Uno"));
        await File.WriteAllTextAsync(Path.Combine(_lab.Download, "fattura.pdf"), "mia");
        pagina.WaitForAssertion(() => Assert.NotNull(pagina.Find("[data-tasto=prendi-da-download]")), TimeSpan.FromSeconds(10));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=prendi-da-download]").Click());

        pagina.WaitForAssertion(() =>
        {
            Assert.Contains("Presi 1: 1 nuovi", pagina.Find("[data-presa]").TextContent, StringComparison.Ordinal);
            Assert.Equal("1 PDF", pagina.Find("[data-quanti-pdf]").TextContent.Trim());
        }, TimeSpan.FromSeconds(10));
        Assert.True(File.Exists(Path.Combine(pdf, "LI-ENR 5.1.2.pdf")));
        Assert.True(File.Exists(Path.Combine(_lab.Download, "fattura.pdf")));

        // Il ciclo proposto è quello di oggi; si scrive quello voluto, e si legge.
        Assert.Equal(_lab.CicloDiOggi, pagina.Find("[data-campo=ciclo]").GetAttribute("value"));
        await pagina.InvokeAsync(() => pagina.Find("[data-campo=ciclo]").Change("2611"));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=leggi-aip]").Click());

        pagina.WaitForAssertion(() =>
        {
            var esito = pagina.Find("[data-esito-aip]").TextContent;
            Assert.Contains("Ciclo 2611", esito, StringComparison.Ordinal);
            Assert.Contains("1 aree", esito, StringComparison.Ordinal);
            Assert.Contains("06/08/2026 ×1", pagina.Find("[data-date-aip]").TextContent, StringComparison.Ordinal);
        }, TimeSpan.FromSeconds(20));

        var doc = AipJson.Deserialize(await File.ReadAllTextAsync(Path.Combine(_fuori, "espanso", "2611", "li.json")));
        Assert.Equal("LI R901", Assert.Single(doc.Airspaces).Designator);

        // Un ciclo scritto male non si legge.
        Assert.False(await _lab.LeggiLAipAsync("26"));
        Assert.Contains("non è un ciclo AIRAC", _lab.Rifiuto, StringComparison.Ordinal);
    }

    [Fact]
    public void LeCartelleSiRicordano()
    {
        Assert.True(_lab.CambiaLaCartellaDeiPdf(Path.Combine(_fuori, "AIP")));
        Assert.True(_lab.CambiaLAipEspanso(Path.Combine(_fuori, "espanso")));

        var riaperto = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));

        Assert.Equal(Path.Combine(_fuori, "AIP"), riaperto.CartellaDeiPdf);
        Assert.Equal(Path.Combine(_fuori, "espanso"), riaperto.CartellaDellAipEspanso);
        Assert.True(riaperto.CambiaLaCartellaDeiPdf(""));
        Assert.Null(new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab")).CartellaDeiPdf);
    }
}
