using Aip;
using Aip.Pdf;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;
using Vipi.SectorLab.Core.PdfAip;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Carta «Gli aeroporti nel Lab», S3: la carta posata. Dalla carta di avvicinamento (o da una carta qualsiasi della scheda
/// Carte) «Posa sulla mappa»: l'immagine della pagina fatta da PDFium nella cache del Lab, i punti cliccati sulla carta col
/// loro posto (per nome, o dalla mappa), la carta sulla mappa dal secondo punto, «Salva la posa» in <c>posa\LIXX.json</c>.
/// </summary>
public sealed class PosaDellaCartaUiTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;
    private readonly string _fuori = Path.Combine(Path.GetTempPath(), "posa-carta-" + Guid.NewGuid().ToString("N"));
    private string Espanso => Path.Combine(_fuori, "espanso");
    private string Pdf => Path.Combine(_fuori, "pdf");

    public PosaDellaCartaUiTests()
    {
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab")) { ApriIlPdfAllaPagina = _ => { } };
        _contesto.Services.AddSingleton(_lab);
        _contesto.JSInterop.Mode = JSRuntimeMode.Loose;
        _contesto.JSInterop.Setup<bool>("sectorlab.mappa.crea", _ => true).SetResult(true);
    }

    public void Dispose()
    {
        _contesto.Dispose();
        _albero.Dispose();
        if (Directory.Exists(_fuori))
            Directory.Delete(_fuori, recursive: true);
    }

    /// <summary>Un PDF di una pagina A5 con un segno nero: basta che PDFium la disegni.</summary>
    private static byte[] UnaPagina()
    {
        var builder = new PdfDocumentBuilder();
        var pagina = builder.AddPage(420, 595);
        pagina.DrawRectangle(new PdfPoint(100, 400), 20, 20, 1, fill: true);
        return builder.Build();
    }

    private async Task<IRenderedComponent<Home>> LaPaginaDiLixxAsync()
    {
        _albero.Scrivi("SectorFiles/Include/IT/lirf.str", "//MENU MAPS\r\n");
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var cicloDeiPdf = Path.Combine(Pdf, "AIP 2611", "AD 2", "LIXX");
        Directory.CreateDirectory(cicloDeiPdf);
        await File.WriteAllBytesAsync(Path.Combine(cicloDeiPdf, "LI-AD 2 LIXX 5.pdf"), UnaPagina());
        await File.WriteAllBytesAsync(Path.Combine(cicloDeiPdf, "LI-AD 2 LIXX 2.pdf"), UnaPagina());
        Assert.True(_lab.CambiaLaCartellaDeiPdf(Pdf), _lab.Rifiuto);
        Assert.True(_lab.CambiaLAipEspanso(Espanso), _lab.Rifiuto);
        Directory.CreateDirectory(Path.Combine(Espanso, "2611"));
        var data = new DateOnly(2026, 10, 29);
        await File.WriteAllTextAsync(IndiceDellePagine.File(Espanso, "2611"), IndiceDellePagine.Testo("2611",
            [new IndexedPage("LI-AD 2 LIXX 2.pdf", 1, "AD 2 LIXX 2-1", data, false), new IndexedPage("LI-AD 2 LIXX 5.pdf", 1, "AD 2 LIXX 5-3", data, false)]));
        await File.WriteAllTextAsync(Path.Combine(Espanso, "2611", "li.json"), AipJson.Serialize(new AipDocument
        {
            State = "LI",
            Airac = "2611",
            Airspaces = [],
            Points = [new SignificantPoint { Section = "AD 2 LIXX", Designator = "UNO", Position = new Position(41.85, 12.26) }],
            Aerodromes =
            [
                new Aerodrome
                {
                    Icao = "LIXX", Name = "PROVA Aeroporto", Arp = new Position(41.8, 12.25),
                    Runways = [new Runway { Designator = "16L", Threshold = new Position(41.82, 12.24) }, new Runway { Designator = "34R" }],
                    Charts = [new AerodromeChart("AERODROME CHART - ICAO", "AD 2 LIXX 2-1", data), new AerodromeChart("ICAO INSTRUMENT APPROACH CHART ILS Z RWY 16L", "AD 2 LIXX 5-3", data)],
                },
            ],
        }));
        _lab.ScegliLaPagina(PaginaDelLab.AipIndice);
        var pagina = _contesto.RenderComponent<Home>();
        pagina.WaitForAssertion(() => Assert.NotNull(pagina.Find("[data-scalo=LIXX] button")), TimeSpan.FromSeconds(10));
        await pagina.InvokeAsync(() => pagina.Find("[data-scalo=LIXX] button").Click());
        pagina.WaitForAssertion(() => Assert.NotNull(pagina.Find("[data-pagina-scalo]")), TimeSpan.FromSeconds(10));
        return pagina;
    }

    [Fact]
    public async Task La_carta_si_posa_con_due_punti_e_si_salva_nel_repo_dell_aip()
    {
        var pagina = await LaPaginaDiLixxAsync();
        await pagina.InvokeAsync(() => pagina.Find("[data-apri-carta='AD 2 LIXX 5-3']").Click());
        Assert.Contains("Non è posata", pagina.Find("[data-stato-posa]").TextContent, StringComparison.Ordinal);

        // «Posa sulla mappa»: l'immagine nella cache del Lab, a 200 dpi (A5: 1 167 × 1 653 pixel).
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=posa-carta]").Click());
        Assert.Null(_lab.Rifiuto);
        var posa = _lab.Posa!;
        Assert.StartsWith(_lab.CacheDelleCarte, posa.Immagine.File, StringComparison.Ordinal);
        Assert.Equal((1167, 1653), Png.Misura(await File.ReadAllBytesAsync(posa.Immagine.File)));
        Assert.Equal(420, posa.Immagine.LarghezzaInPunti, 0);
        Assert.Equal(_lab.IndirizzoDellaCarta, pagina.Find("[data-immagine-carta]").GetAttribute("src"));
        Assert.Null(_lab.CartaSullaMappa);

        // Il primo punto: un clic sulla carta (a 1× è larga 400 px: 200 px = metà, 210 pt), poi il nome.
        await pagina.InvokeAsync(() => pagina.Find("[data-immagine-carta]").Click(new MouseEventArgs { OffsetX = 200, OffsetY = 100 }));
        Assert.NotNull(pagina.Find("[data-segno-posa=attesa]"));
        Assert.True(_lab.PrendeUnPuntoDallaMappa);
        await pagina.InvokeAsync(() => pagina.Find("[data-campo=nome-del-punto]").Change("nessuno"));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=trova-il-punto]").Click());
        Assert.Contains("«nessuno» non è un punto", pagina.Find("[data-errore-posa]").TextContent, StringComparison.Ordinal);
        await pagina.InvokeAsync(() => pagina.Find("[data-campo=nome-del-punto]").Change("rwy 16l"));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=trova-il-punto]").Click());
        var primo = Assert.Single(_lab.Posa!.Punti);
        // La misura in punti viene dai pixel (1 167 × 72 / 200 = 420,12): mezzo punto basta.
        Assert.Equal((210, 105, 41.82, 12.24, "RWY 16L"), (Math.Round(primo.X), Math.Round(primo.Y), primo.Lat, primo.Lon, primo.Nome));
        Assert.False(_lab.PrendeUnPuntoDallaMappa);
        Assert.Contains("1 punto: ne serve un altro", pagina.Find("[data-stato-posa]").TextContent, StringComparison.Ordinal);

        // Il secondo dalla mappa (il clic su un punto dell'AIP: la mappa manda le sue coordinate e il nome).
        await pagina.InvokeAsync(() => pagina.Find("[data-immagine-carta]").Click(new MouseEventArgs { OffsetX = 300, OffsetY = 40 }));
        await pagina.InvokeAsync(() => _lab.PuntoDallaMappa(41.85, 12.26, "UNO"));
        pagina.WaitForAssertion(() => Assert.Equal(2, pagina.FindAll("[data-punto-posa]").Count));
        Assert.Contains("2 punti · in scala e girata · 1 cm della carta =", pagina.Find("[data-stato-posa]").TextContent, StringComparison.Ordinal);
        var sullaMappa = _lab.CartaSullaMappa!;
        Assert.Equal(3, sullaMappa.Angoli.Count);
        Assert.Equal(0.6, sullaMappa.Opacita);
        await pagina.InvokeAsync(() => pagina.Find("[data-campo=opacita-della-carta]").Change("40"));
        Assert.Equal(0.4, _lab.CartaSullaMappa!.Opacita, 3);

        // «Salva la posa»: posa\LIXX.json nel repo dell'AIP espanso.
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=salva-la-posa]").Click());
        Assert.NotNull(pagina.Find("[data-posa-salvata]"));
        var salvata = Assert.Single(PoseDelleCarte.Leggi(Espanso, "LIXX"));
        Assert.Equal(("AD 2 LIXX 5-3", new DateOnly(2026, 10, 29), 2), (salvata.Pagina, salvata.Data, salvata.Punti.Count));
        Assert.True(pagina.Find("[data-tasto=salva-la-posa]").HasAttribute("disabled"));

        // Tolta dalla mappa e riaperta: i punti salvati tornano, e la mappa ci si porta sopra.
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=chiudi-la-posa]").Click());
        Assert.Null(_lab.Posa);
        Assert.Contains("Posata con 2 punti.", pagina.Find("[data-stato-posa]").TextContent, StringComparison.Ordinal);
        var inquadrature = _lab.InquadratureDellaCarta;
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=posa-carta]").Click());
        Assert.Equal(2, _lab.Posa!.Punti.Count);
        Assert.Equal(inquadrature + 1, _lab.InquadratureDellaCarta);

        // Un punto tolto è un cambio da salvare; «Togli la posa» toglie anche il file.
        await pagina.InvokeAsync(() => pagina.Find("[data-togli-punto='2']").Click());
        Assert.NotNull(pagina.Find("[data-posa-da-salvare]"));
        Assert.Null(_lab.CartaSullaMappa);
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=togli-la-posa]").Click());
        Assert.False(File.Exists(PoseDelleCarte.File(Espanso, "LIXX")));
        Assert.Empty(_lab.Posa!.Punti);
    }

    [Fact]
    public async Task Una_carta_d_aerodromo_si_posa_dalla_scheda_carte()
    {
        var pagina = await LaPaginaDiLixxAsync();
        await pagina.InvokeAsync(() => pagina.Find("[data-scheda-scalo=Carte]").Click());
        Assert.Equal(["posa…", "posa…"], pagina.FindAll("[data-posa-dalla-tabella]").Select(b => b.TextContent.Trim()));

        await pagina.InvokeAsync(() => pagina.Find("[data-posa-dalla-tabella='AD 2 LIXX 2-1']").Click());
        Assert.Equal("AD 2 LIXX 2-1", pagina.Find("[data-foglio]").GetAttribute("data-foglio"));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=posa-carta]").Click());
        Assert.Equal("AD 2 LIXX 2-1", _lab.Posa!.Carta.Page);

        // Due punti per nome (l'ARP e la soglia), salvati; la tabella dice «posata».
        foreach (var (x, nome) in new[] { (100.0, "ARP"), (300.0, "RWY 16L") })
        {
            await pagina.InvokeAsync(() => pagina.Find("[data-immagine-carta]").Click(new MouseEventArgs { OffsetX = x, OffsetY = x }));
            await pagina.InvokeAsync(() => pagina.Find("[data-campo=nome-del-punto]").Change(nome));
            await pagina.InvokeAsync(() => pagina.Find("[data-tasto=trova-il-punto]").Click());
        }
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=salva-la-posa]").Click());
        await pagina.InvokeAsync(() => pagina.Find("[data-chiudi-carta]").Click());
        Assert.Equal(["posata", "posa…"], pagina.FindAll("[data-posa-dalla-tabella]").Select(b => b.TextContent.Trim()));
    }

    [Fact]
    public async Task Senza_la_pagina_nell_indice_la_carta_non_si_posa_e_lo_dice()
    {
        await LaPaginaDiLixxAsync();
        Assert.False(_lab.ApriLaPosa("LIXX", new AerodromeChart("PARKING", "AD 2 LIXX 2-9")));
        Assert.Equal("La pagina AD 2 LIXX 2-9 non è nell'indice del 2611.", _lab.Rifiuto);
        Assert.Null(_lab.Posa);
    }
}
