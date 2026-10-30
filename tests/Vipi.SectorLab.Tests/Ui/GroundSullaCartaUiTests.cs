using Aip;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Core.PdfAip;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Carta «Gli aeroporti nel Lab», S5: nella scheda Ground le carte d'aerodromo da posare e il ground del sector sopra la carta
/// sulla mappa dell'AIP espanso; sotto i controlli, le eccezioni dello scalo (controllo 4) in <c>eccezioni\LIXX.json</c>.
/// </summary>
public sealed class GroundSullaCartaUiTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;
    private readonly string _fuori = Path.Combine(Path.GetTempPath(), "ground-carta-" + Guid.NewGuid().ToString("N"));
    private string Espanso => Path.Combine(_fuori, "espanso");

    public GroundSullaCartaUiTests()
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

    private async Task<IRenderedComponent<Home>> LaSchedaGroundDiLixxAsync()
    {
        // La 34R del sector è a circa 333 m da AD 2.12; la taxiway B dell'AIP il sector non l'ha.
        _albero.Scrivi("SectorFiles/Include/IT/OTHER/itrw.rw",
            "//PISTE\r\nLIXX;16L;34R;14;14;160;340;N041.51.00.000;E012.15.36.000;N041.48.46.800;E012.16.48.000;\r\n");
        _albero.Scrivi("SectorFiles/Include/IT/lixx.txi", "A;LIXX;N0415100000;E0121536000;\r\nc;LIXX;N0415102000;E0121538000;\r\n");
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        Assert.True(_lab.CambiaLAipEspanso(Espanso), _lab.Rifiuto);
        Directory.CreateDirectory(Path.Combine(Espanso, "2611"));
        var data = new DateOnly(2026, 10, 29);
        await File.WriteAllTextAsync(IndiceDellePagine.File(Espanso, "2611"), IndiceDellePagine.Testo("2611",
            [new Aip.Pdf.IndexedPage("LI-AD 2 LIXX 2.pdf", 1, "AD 2 LIXX 2-1", data, false)]));
        await File.WriteAllTextAsync(Path.Combine(Espanso, "2611", "li.json"), AipJson.Serialize(new AipDocument
        {
            State = "LI",
            Airac = "2611",
            Airspaces = [],
            Aerodromes =
            [
                new Aerodrome
                {
                    Icao = "LIXX", Name = "PROVA Aeroporto", Arp = new Position(41.83, 12.27),
                    Runways = [new Runway { Designator = "16L", Threshold = new Position(41.85, 12.26) }, new Runway { Designator = "34R", Threshold = new Position(41.81, 12.28) }],
                    Sections = [new AerodromeSection { Number = "AD 2.8", Rows = [["2", "Larghezza, superficie e resistenza delle TWY", "Identificativo della TWY Designator of TWY A B C", "23 M"]] }],
                    Charts =
                    [
                        new AerodromeChart("AERODROME CHART - ICAO", "AD 2 LIXX 2-1", data),
                        new AerodromeChart("AERODROME OBSTACLE CHART - TYPE B ICAO", "AD 2 LIXX 3-1", data),
                    ],
                },
            ],
        }));
        _lab.ScegliLaPagina(PaginaDelLab.AipIndice);
        var pagina = _contesto.RenderComponent<Home>();
        pagina.WaitForAssertion(() => Assert.NotNull(pagina.Find("[data-scalo=LIXX] button")), TimeSpan.FromSeconds(10));
        await pagina.InvokeAsync(() => pagina.Find("[data-scalo=LIXX] button").Click());
        pagina.WaitForAssertion(() => Assert.NotNull(pagina.Find("[data-pagina-scalo]")), TimeSpan.FromSeconds(10));
        await pagina.InvokeAsync(() => pagina.Find("[data-scheda-scalo=Ground]").Click());
        return pagina;
    }

    [Fact]
    public async Task La_carta_d_aerodromo_si_posa_dal_ground_e_il_sector_le_sta_sopra()
    {
        var pagina = await LaSchedaGroundDiLixxAsync();

        // Solo la carta d'aerodromo: gli ostacoli non disegnano il ground.
        var carta = Assert.Single(pagina.FindAll("[data-carta-ground]"));
        Assert.Equal("AD 2 LIXX 2-1", carta.GetAttribute("data-carta-ground"));
        Assert.Contains("posa…", carta.TextContent, StringComparison.Ordinal);

        // Il ground del sector sulla mappa dell'AIP espanso: fra le coste e l'AIP, e spento torna com'era.
        Assert.DoesNotContain(_lab.StratiDi(SpazioDiLavoro.Aip), s => s.Id == "terra");
        await pagina.InvokeAsync(() => pagina.Find("[data-campo=ground-sulla-carta] input").Change(true));
        Assert.True(_lab.GroundSullaCarta);
        Assert.Superset(new HashSet<string>(SessioneDelLab.StratiDelGround), _lab.AccesiDi(SpazioDiLavoro.Aip).ToHashSet());
        var strati = _lab.StratiDi(SpazioDiLavoro.Aip).Select(s => s.Id).ToList();
        Assert.Equal(["sfondo", "geo", "terra", "piste", AipSullaMappa.Tipo.Id], strati);
        // Il ground resta anche con «Solo lo scalo sulla mappa»: è quel che si guarda sulla carta.
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=solo-lo-scalo]").Click());
        Assert.Contains("piste", _lab.AccesiDi(SpazioDiLavoro.Aip));

        // La carta si apre da qui, e torna al Ground; la casella c'è anche lì.
        await pagina.InvokeAsync(() => pagina.Find("[data-posa-dal-ground='AD 2 LIXX 2-1']").Click());
        Assert.Contains("LIXX · Ground", pagina.Find("[data-chiudi-carta]").TextContent, StringComparison.Ordinal);
        Assert.NotNull(pagina.Find("[data-foglio] [data-campo=ground-sulla-carta]"));
        await pagina.InvokeAsync(() => pagina.Find("[data-foglio] [data-campo=ground-sulla-carta] input").Change(false));
        Assert.False(_lab.GroundSullaCarta);
        Assert.DoesNotContain("terra", _lab.AccesiDi(SpazioDiLavoro.Aip));
        await pagina.InvokeAsync(() => pagina.Find("[data-chiudi-carta]").Click());
        Assert.NotNull(pagina.Find("[data-ground-carte]"));
    }

    [Fact]
    public async Task Un_eccezione_dello_scalo_salta_la_differenza_e_dice_il_perche()
    {
        var pagina = await LaSchedaGroundDiLixxAsync();
        Assert.Contains("lab-stato-parziale", pagina.Find("[data-soglia-ground='34R']").ClassName ?? "", StringComparison.Ordinal);
        Assert.Equal("B", pagina.Find("[data-taxiway-mancano]").TextContent);

        // La soglia: «Un'eccezione…», la voce fra quelle diverse, il perché.
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=eccezione-soglia]").Click());
        var voci = pagina.FindAll("[data-modulo-eccezione=soglia] option").Select(o => o.TextContent).ToList();
        Assert.Equal(["34R"], voci);
        Assert.True(pagina.Find("[data-tasto=salva-eccezione]").HasAttribute("disabled"));
        await pagina.InvokeAsync(() => pagina.Find("[data-campo=perche-eccezione]").Input("Soglia spostata dai lavori, il sector segue il NOTAM"));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=salva-eccezione]").Click());
        Assert.Null(_lab.Rifiuto);
        var scritta = Assert.Single(EccezioniDelGround.Leggi(Espanso, "LIXX"));
        Assert.Equal((ControlloDelGround.Soglia, "34R"), (scritta.Controllo, scritta.Voce));
        Assert.DoesNotContain("lab-stato-parziale", pagina.Find("[data-soglia-ground='34R']").ClassName ?? "", StringComparison.Ordinal);
        Assert.Contains("eccezione", pagina.Find("[data-soglia-ground='34R']").TextContent, StringComparison.Ordinal);
        Assert.Contains("Soglia spostata dai lavori", pagina.Find("[data-eccezioni=soglia] [data-eccezione='34R']").TextContent, StringComparison.Ordinal);
        // Tutte le soglie diverse hanno l'eccezione: il tasto non c'è più.
        Assert.Empty(pagina.FindAll("[data-tasto=eccezione-soglia]"));

        // La taxiway B: esce da «Mancano nel sector».
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=eccezione-taxiway]").Click());
        await pagina.InvokeAsync(() => pagina.Find("[data-campo=perche-eccezione]").Input("Chiusa"));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=salva-eccezione]").Click());
        Assert.Empty(pagina.FindAll("[data-taxiway-mancano]"));
        Assert.Equal(2, EccezioniDelGround.Leggi(Espanso, "LIXX").Count);

        // «togli»: il controllo torna a dire la differenza.
        await pagina.InvokeAsync(() => pagina.Find("[data-togli-eccezione='B']").Click());
        Assert.Equal("B", pagina.Find("[data-taxiway-mancano]").TextContent);
        Assert.Single(EccezioniDelGround.Leggi(Espanso, "LIXX"));
    }
}
