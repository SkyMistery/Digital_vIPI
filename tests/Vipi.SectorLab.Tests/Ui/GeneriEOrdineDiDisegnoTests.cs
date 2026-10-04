using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Server;
using Vipi.SectorLab.Ui.Servizi;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Lotto «Subito» slice 12c («file per file» H1, I3): i disegni di terra si accendono per tipo — assi e bordi delle
/// taxiway, edifici, marcature; riempimenti, etichette e stand — e un riempimento dice a che posto si disegna; un
/// poligono nuovo nasce dopo l'ultimo del suo riempimento, dovunque si sia cliccato.
/// </summary>
public sealed class GeneriEOrdineDiDisegnoTests : IDisposable
{
    private const string Geo = "SectorFiles/Include/IT/GEO/liap.geo";
    private const string Marcature = "SectorFiles/Include/IT/RW_MARKINGS/ba_mark.geo";
    private const string Pol = "SectorFiles/Include/IT/GND_LAYOUT/prova.pol";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public GeneriEOrdineDiDisegnoTests()
    {
        // Erba, due taxiway, un edificio, un'altra erba (fuori posto, come capita), una pista: sei riempimenti.
        _albero.Scrivi(Pol, string.Concat(new[] { "GRASS", "TAXIWAY", "TAXIWAY", "BUILDING", "GRASS", "RUNWAY" }.Select((r, i) =>
            $"//Poligono {i + 1}\r\nSTATIC;{r};1;{r};\r\nN041.48.0{i}.000;E012.14.00.000;\r\nN041.48.0{i}.000;E012.15.00.000;\r\nN041.49.0{i}.000;E012.15.00.000;\r\n")));

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

    private async Task Apri() => Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

    [Fact]
    public async Task IDisegniDiTerraHannoIlLoroGenere_ELeMarcatureSonoUnGenereALoro()
    {
        await Apri();
        var geo = _lab.Strati.Single(s => s.Id == "geo");

        // Il 5° campo è il genere; nel file delle marcature è sempre RUNWAY, ma il genere è «marcature».
        Assert.All(geo.Forme.Where(f => f.File == Marcature), f => Assert.Equal(GeneriDellaMappa.Marcature, f.Genere));
        var diLiap = geo.Forme.Where(f => f.File == Geo).Select(f => f.Genere).Distinct().ToList();
        Assert.Contains("TAXIWAY", diLiap);
        Assert.Contains(GeneriDellaMappa.SenzaTipo, diLiap);
        Assert.DoesNotContain(null, diLiap);

        var generi = _lab.GeneriDi("geo");
        Assert.Equal(geo.Forme.Count, generi.Sum(g => g.Forme));
        Assert.Equal("Marcature delle piste", generi.Single(g => g.Id == GeneriDellaMappa.Marcature).Nome);
        Assert.Equal("Senza tipo", generi.Single(g => g.Id == GeneriDellaMappa.SenzaTipo).Nome);

        Assert.Equal([GeneriDellaMappa.Riempimenti, GeneriDellaMappa.Etichette, GeneriDellaMappa.Stand], _lab.GeneriDi("terra").Select(g => g.Id));
        // Le aree P/R/D e lo sfondo non hanno generi: una casella sola non serve.
        Assert.Empty(_lab.GeneriDi("aree"));
        Assert.Empty(_lab.GeneriDi("sfondo"));
    }

    [Fact]
    public async Task UnGenereSpentoPassaAllaMappaFraLeVociSpente_ELaFormaPortaIlSuoGenere()
    {
        await Apri();
        int prima = _lab.VersioneDeiSpenti;

        _lab.AccendiIlGenere("geo", "TAXI_CENTER", false);

        Assert.False(_lab.GenereAcceso("geo", "TAXI_CENTER"));
        Assert.True(_lab.GenereAcceso("geo", "TAXIWAY"));
        Assert.Contains("§geo:TAXI_CENTER", _lab.Spenti);
        Assert.Equal(prima + 1, _lab.VersioneDeiSpenti);

        _lab.AccendiIlGenere("geo", "TAXI_CENTER", true);
        Assert.DoesNotContain("§geo:TAXI_CENTER", _lab.Spenti);
        Assert.Contains("geo", _lab.Accesi);

        // Scegliere un record di un genere spento lo riaccende: sennò sulla mappa non ci sarebbe niente da evidenziare.
        _lab.AccendiIlGenere("terra", GeneriDellaMappa.Stand, false);
        _lab.Scegli("SectorFiles/Include/IT/lirf.gts", 0);
        Assert.True(_lab.GenereAcceso("terra", GeneriDellaMappa.Stand));

        using var flusso = new MemoryStream();
        await MappaDelLab.Scrivi(flusso, "terra", _lab.Strati.Single(s => s.Id == "terra").Forme);
        string json = Encoding.UTF8.GetString(flusso.ToArray());
        Assert.Contains("\"h\":\"POL\"", json, StringComparison.Ordinal);
        Assert.Contains("\"h\":\"GTS\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASchermo_IGeneriCompaionoSottoLoStratoAcceso_ESpegnerneUnoLoDiceAllaMappa()
    {
        await Apri();
        var pagina = _contesto.RenderComponent<Home>();
        Assert.Empty(pagina.FindAll("[data-generi='geo']"));

        await pagina.InvokeAsync(() => _lab.Accendi("geo", true));
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-genere='geo:TAXIWAY']")));

        pagina.Find("[data-genere='geo:TAXIWAY']").Change(false);
        pagina.WaitForAssertion(() => Assert.Contains(_contesto.JSInterop.Invocations["sectorlab.mappa.spenti"],
            i => i.Arguments[0] is string[] chiavi && chiavi.Contains("§geo:TAXIWAY")));
        Assert.False(_lab.GenereAcceso("geo", "TAXIWAY"));
    }

    [Fact]
    public async Task UnRiempimentoDiceAChePostoSiDisegnaECosaHaSopra()
    {
        await Apri();

        var erba = OrdineDiDisegno.Di(_lab.Sessione!.File[Pol], 0)!;
        Assert.Equal((1, 6), (erba.Posto, erba.Quanti));
        Assert.Equal([("TAXIWAY", 2), ("BUILDING", 1), ("GRASS", 1), ("RUNWAY", 1)], erba.Sopra);

        var pista = _lab.PostoNelDisegnoDi(Pol, 5)!;
        Assert.Equal(6, pista.Posto);
        Assert.Empty(pista.Sopra);
        Assert.Null(_lab.PostoNelDisegnoDi(Geo, 0));
    }

    [Fact]
    public async Task UnPoligonoNuovoNasceDopoLUltimoDelSuoRiempimento_DovunqueSiClicchi()
    {
        await Apri();

        // «+ Record come questo» dalla prima erba (record 0), tipo TAXIWAY: va dopo la seconda taxiway (record 2), non
        // sotto l'erba — sennò l'erba di dopo lo coprirebbe… e lui coprirebbe quel che sta fra i due.
        Assert.True(_lab.AggiungiRecord(Pol, 0, tipo: "TAXIWAY"));
        var poligoni = ((Vipi.SectorLab.Core.Sessione.IFileConRecord)_lab.Sessione!.File[Pol]).RecordDelModello.Cast<Polygon>().ToList();
        Assert.Equal(["GRASS", "TAXIWAY", "TAXIWAY", "TAXIWAY", "BUILDING", "GRASS", "RUNWAY"], poligoni.Select(p => p.FillColor));

        // Senza tipo, copiando la prima erba: dopo l'ULTIMA erba del file.
        Assert.True(_lab.AggiungiRecord(Pol, 0));
        poligoni = ((Vipi.SectorLab.Core.Sessione.IFileConRecord)_lab.Sessione!.File[Pol]).RecordDelModello.Cast<Polygon>().ToList();
        Assert.Equal(["GRASS", "TAXIWAY", "TAXIWAY", "TAXIWAY", "BUILDING", "GRASS", "GRASS", "RUNWAY"], poligoni.Select(p => p.FillColor));
    }

    [Fact]
    public async Task ASchermo_LaSchedaDelRiempimentoMostraLOrdineDiDisegno()
    {
        await Apri();
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli(Pol, 1));

        pagina.WaitForAssertion(() => Assert.Equal("2/6", pagina.Find("[data-ordine-di-disegno]").GetAttribute("data-ordine-di-disegno")));
        Assert.Equal("4", pagina.Find("[data-sopra]").GetAttribute("data-sopra"));
        Assert.NotEmpty(pagina.FindAll("[data-scrivi='FillColor']"));
    }
}
