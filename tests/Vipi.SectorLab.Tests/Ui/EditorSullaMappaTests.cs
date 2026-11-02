using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// L'editor sulla mappa, fetta 1 (carta «L'editor sulla mappa», R1, R2, R6, R7): le maniglie del record scelto, il
/// vertice lasciato sulla mappa che diventa il gesto «vertice spostato» della Scheda, il vertice col nome che non si
/// trascina, e la Scheda che segue.
/// </summary>
public sealed class EditorSullaMappaTests : IDisposable
{
    private const string Settore = "SectorFiles/Include/IT/DYNAMIC_SEC/prova.tfl";
    private const string Geo = "SectorFiles/Include/IT/GEO/liap.geo";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public EditorSullaMappaTests()
    {
        // Tre vertici: due per coordinate, quello di mezzo col nome di un VOR che il master ITALY risolve (ALB).
        _albero.Scrivi(Settore, "ZZZZ_CTR;CTR;1;CTR;1;\r\nN041.00.00.000;E012.00.00.000;\r\nALB;ALB;\r\nN041.30.00.000;E012.30.00.000;\r\n");
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        _contesto.Services.AddSingleton(_lab);
        _contesto.JSInterop.Mode = JSRuntimeMode.Loose;
        _contesto.JSInterop.Setup<bool>("sectorlab.mappa.crea", _ => true).SetResult(true);
        _contesto.JSInterop.Setup<int>("sectorlab.mappa.maniglie", _ => true).SetResult(3);
    }

    public void Dispose()
    {
        _contesto.Dispose();
        _albero.Dispose();
    }

    private async Task ApriEScegliIlSettore()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        _lab.Scegli(Settore, 0);
    }

    private string[] Righe(string file) => File.ReadAllText(_albero.Percorso(file)).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public async Task LeManiglieSonoIVerticiDelRecordSceltoQuelloColNomeAlPostoDelSuoPunto()
    {
        await ApriEScegliIlSettore();
        Assert.Empty(_lab.Maniglie());

        _lab.ModificaSullaMappaDi(true);

        Assert.True(_lab.ModificaSullaMappa);
        var maniglie = _lab.Maniglie();
        Assert.Equal([0, 1, 2], maniglie.Select(m => m.Posizione));
        Assert.All(maniglie, m => Assert.Equal("Vertices", m.Campo));
        Assert.Equal(41.0, maniglie[0].Lat, 6);
        Assert.False(maniglie[0].PerNome);
        // ALB è a N044.02.53.400: la maniglia sta lì, ed è di un nome.
        Assert.True(maniglie[1].PerNome);
        Assert.Equal(44.0481667, maniglie[1].Lat, 5);
    }

    [Fact]
    public async Task ScegliereUnAltroRecordSpegneLeManiglie()
    {
        await ApriEScegliIlSettore();
        _lab.ModificaSullaMappaDi(true);

        var altra = _lab.Strati.Single(s => s.Id == "settori").Forme.First(f => f.File != Settore);
        _lab.Scegli(altra.File, altra.Record);

        Assert.False(_lab.ModificaSullaMappa);
        Assert.Empty(_lab.Maniglie());
        // Tornando al settore restano spente: il modo vale per la scelta in cui è stato acceso.
        _lab.Scegli(Settore, 0);
        Assert.False(_lab.ModificaSullaMappa);
    }

    [Fact]
    public async Task SenzaSceltaNonSiAccende()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

        _lab.ModificaSullaMappaDi(true);

        Assert.False(_lab.ModificaSullaMappa);
    }

    [Fact]
    public async Task UnaManigliaLasciataSpostaIlVerticeComeLaSchedaEAnnullaLoRimette()
    {
        await ApriEScegliIlSettore();
        _lab.ModificaSullaMappaDi(true);

        Assert.True(_lab.VerticeDallaMappa("Vertices", 2, 41.25, 12.125));

        Assert.Equal(1, _lab.Modifiche.Quante);
        Assert.Equal(41.25, _lab.Maniglie()[2].Lat, 6);
        Assert.Equal(new GestoDallaMappa(Settore, 0, "Vertices", 2, null), _lab.UltimoDallaMappa);
        // Le maniglie restano accese dopo il gesto: si continua a lavorare.
        Assert.True(_lab.ModificaSullaMappa);

        _lab.Annulla();
        Assert.False(_lab.Modifiche.CEQualcosa);
        Assert.Equal(41.5, _lab.Maniglie()[2].Lat, 6);

        _lab.Ripeti();
        await _lab.SalvaAsync();
        Assert.Equal("N041.15.00.000;E012.07.30.000;", Righe(Settore)[3]);
    }

    [Fact]
    public async Task IlVerticeColNomeNonSiTrascinaEIlMotivoStaAccantoAlSuoElenco()
    {
        await ApriEScegliIlSettore();
        _lab.ModificaSullaMappaDi(true);

        Assert.False(_lab.VerticeDallaMappa("Vertices", 1, 44.0, 8.0));

        Assert.False(_lab.Modifiche.CEQualcosa);
        Assert.Contains("nome di un punto", _lab.Rifiuto);
        Assert.Equal("Vertices", _lab.UltimoDallaMappa?.Campo);
        Assert.Same(_lab.Rifiuto, _lab.UltimoDallaMappa?.Rifiuto);
    }

    [Fact]
    public async Task UnPostoFuoriDalMondoSiRifiuta()
    {
        await ApriEScegliIlSettore();
        _lab.ModificaSullaMappaDi(true);

        Assert.False(_lab.VerticeDallaMappa("Vertices", 0, 41.0, 372.0));

        Assert.False(_lab.Modifiche.CEQualcosa);
        Assert.NotNull(_lab.Rifiuto);
    }

    [Fact]
    public async Task NellaLineaDiUnGeoLeManiglieSonoIntornoAlSegmentoESpostarneUnaSpostaIDueSegmenti()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        // Il quinto segmento della prima linea del file: ha punti prima e dopo.
        int primo = Enumerable.Range(0, 200).First(r => _lab.LineaDi(Geo, r) is not null);
        var linea = _lab.LineaDi(Geo, primo)!;
        int record = linea.Record[4];
        _lab.Scegli(Geo, record);
        _lab.ModificaSullaMappaDi(true);

        var maniglie = _lab.Maniglie();
        Assert.All(maniglie, m => Assert.Equal(SessioneDelLab.ChiaveDellaLinea, m.Campo));
        Assert.True(maniglie.Count <= 2 * SessioneDelLab.IntornoDellaLinea + 2);
        Assert.Contains(maniglie, m => m.Posizione == 5);

        // Il punto 5 è la fine del segmento 4 e l'inizio del 5: tutti e due cambiano.
        Assert.True(_lab.VerticeDallaMappa(SessioneDelLab.ChiaveDellaLinea, 5, 42.5, 13.5));
        var dopo = _lab.LineaDi(Geo, record)!;
        Assert.Equal(42.5, dopo.Punti[5].LatitudeDeg, 6);
        Assert.Equal(linea.Punti.Count, dopo.Punti.Count);
    }

    // --- fetta 2: aggiungere e togliere dalla mappa (R3, R4) -----------------------------------------------------

    [Fact]
    public async Task LaManigliaDiMezzoAggiungeUnVerticeColSuoPostoInUnGestoSolo()
    {
        await ApriEScegliIlSettore();
        _lab.ModificaSullaMappaDi(true);
        Assert.All(_lab.Maniglie(), m => Assert.True(m.Aggiungibile));

        // Fra il vertice 0 e ALB.
        Assert.True(_lab.VerticeAggiuntoDallaMappa("Vertices", 1, 42.5, 10.25));

        var maniglie = _lab.Maniglie();
        Assert.Equal(4, maniglie.Count);
        Assert.Equal(42.5, maniglie[1].Lat, 6);
        Assert.True(maniglie[2].PerNome);
        Assert.Equal(1, _lab.Modifiche.Quante);
        Assert.Equal(new GestoDallaMappa(Settore, 0, "Vertices", 1, null), _lab.UltimoDallaMappa);

        // Un gesto solo: un annulla lo toglie tutto.
        _lab.Annulla();
        Assert.Equal(3, _lab.Maniglie().Count);
        Assert.False(_lab.Modifiche.CEQualcosa);

        _lab.Ripeti();
        await _lab.SalvaAsync();
        Assert.Equal("N042.30.00.000;E010.15.00.000;", Righe(Settore)[2]);
    }

    [Fact]
    public async Task UnaManigliaDiMezzoVecchiaSiRifiuta()
    {
        await ApriEScegliIlSettore();
        _lab.ModificaSullaMappaDi(true);

        // Dopo l'ultimo vertice non c'è un tratto: nessuna maniglia di mezzo lì.
        Assert.False(_lab.VerticeAggiuntoDallaMappa("Vertices", 3, 42.0, 12.0));

        Assert.False(_lab.Modifiche.CEQualcosa);
        Assert.Contains("la forma è cambiata", _lab.Rifiuto);
    }

    [Fact]
    public async Task IlClicDestroToglieAncheUnVerticeColNomeMaNonLUltimo()
    {
        await ApriEScegliIlSettore();
        _lab.ModificaSullaMappaDi(true);

        Assert.True(_lab.VerticeToltoDallaMappa("Vertices", 1));
        Assert.Equal(2, _lab.Maniglie().Count);
        Assert.DoesNotContain(_lab.Maniglie(), m => m.PerNome);
        Assert.True(_lab.VerticeToltoDallaMappa("Vertices", 1));

        Assert.False(_lab.VerticeToltoDallaMappa("Vertices", 0));
        Assert.Contains("ultimo vertice", _lab.Rifiuto);
        Assert.Same(_lab.Rifiuto, _lab.UltimoDallaMappa?.Rifiuto);
        Assert.Single(_lab.Maniglie());
    }

    [Fact]
    public async Task NellaLineaDiUnGeoLaManigliaDiMezzoAggiungeIlPuntoDoveLasciata()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        int primo = Enumerable.Range(0, 200).First(r => _lab.LineaDi(Geo, r) is not null);
        var linea = _lab.LineaDi(Geo, primo)!;
        int record = linea.Record[4];
        _lab.Scegli(Geo, record);
        _lab.ModificaSullaMappaDi(true);

        Assert.True(_lab.VerticeAggiuntoDallaMappa(SessioneDelLab.ChiaveDellaLinea, 5, 42.5, 13.5));

        var dopo = _lab.LineaDi(Geo, linea.Record[0])!;
        Assert.Equal(linea.Punti.Count + 1, dopo.Punti.Count);
        Assert.Equal(42.5, dopo.Punti[5].LatitudeDeg, 6);
        Assert.Equal(linea.Punti[5], dopo.Punti[6]);

        // Un gesto solo, non «aggiungi» e poi «sposta».
        _lab.Annulla();
        Assert.False(_lab.Modifiche.CEQualcosa);
    }

    [Fact]
    public async Task IlPiuDellaLineaNellaSchedaFaAncoraUnaCopiaDelPunto()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        int primo = Enumerable.Range(0, 200).First(r => _lab.LineaDi(Geo, r) is not null);
        var linea = _lab.LineaDi(Geo, primo)!;

        Assert.True(_lab.GestoSuiVertici(Geo, linea.Record[0], SessioneDelLab.ChiaveDellaLinea, GestoDeiVertici.Aggiungi, 2));

        var dopo = _lab.LineaDi(Geo, linea.Record[0])!;
        Assert.Equal(dopo.Punti[2], dopo.Punti[3]);
    }

    // --- fetta 3: la calamita (R5, R6) ---------------------------------------------------------------------------

    private Vipi.SectorLab.Core.Mappa.FormaDellaMappa Forma(string etichetta)
        => _lab.Strati.SelectMany(s => s.Forme).First(f => f.Etichetta == etichetta);

    [Fact]
    public async Task AgganciatoAUnVorIlVerticePrendeLaSuaCoordinataEsattaNonQuellaDelRilascio()
    {
        await ApriEScegliIlSettore();
        _lab.ModificaSullaMappaDi(true);
        var aea = Forma("AEA");

        // Rilasciato un po' accanto (come fa la calamita entro 8 px): conta il punto, non il rilascio.
        Assert.True(_lab.VerticeDallaMappa("Vertices", 0, 40.6381, 8.2918, aea.File, aea.Record));

        await _lab.SalvaAsync();
        // AEA è N040.38.17.400 E008.17.30.400; il vertice resta una coordinata (R5: ne prende le coordinate).
        Assert.Equal("N040.38.17.400;E008.17.30.400;", Righe(Settore)[1]);
    }

    [Fact]
    public async Task UnVerticeDiUnaFormaVicinaSiPrendeComeIlFileLoScrive()
    {
        await ApriEScegliIlSettore();
        _lab.ModificaSullaMappaDi(true);
        var vicina = _lab.Strati.SelectMany(s => s.Forme).First(f => f.File.EndsWith("libb_es_ctr.tfl", StringComparison.Ordinal) && f.Record == 0);

        // Il primo vertice di LIBB_ES_CTR è N041.13.55.000 E014.47.19.000: rilasciato a qualche metro.
        Assert.True(_lab.VerticeDallaMappa("Vertices", 2, 41.23195, 14.78862, vicina.File, vicina.Record));

        await _lab.SalvaAsync();
        Assert.Equal("N041.13.55.000;E014.47.19.000;", Righe(Settore)[3]);
    }

    [Fact]
    public async Task IlVerticeColNomeAgganciatoAUnAltroPuntoDiventaIlSuoNome()
    {
        await ApriEScegliIlSettore();
        _lab.ModificaSullaMappaDi(true);
        var aea = Forma("AEA");

        Assert.True(_lab.VerticeDallaMappa("Vertices", 1, 40.638, 8.292, aea.File, aea.Record));

        Assert.True(_lab.Maniglie()[1].PerNome);
        await _lab.SalvaAsync();
        Assert.Equal("AEA;AEA;", Righe(Settore)[2]);
    }

    [Fact]
    public async Task IlVerticeColNomeSuUnVerticeSenzaNomeSiRifiuta()
    {
        await ApriEScegliIlSettore();
        _lab.ModificaSullaMappaDi(true);
        var vicina = _lab.Strati.SelectMany(s => s.Forme).First(f => f.File.EndsWith("libb_es_ctr.tfl", StringComparison.Ordinal) && f.Record == 0);

        Assert.False(_lab.VerticeDallaMappa("Vertices", 1, 41.23195, 14.78862, vicina.File, vicina.Record));

        Assert.False(_lab.Modifiche.CEQualcosa);
        Assert.Contains("resta un nome", _lab.Rifiuto);
    }

    [Fact]
    public async Task UnaFormaCheNonCePiuNonSiAggancia()
    {
        await ApriEScegliIlSettore();
        _lab.ModificaSullaMappaDi(true);

        Assert.False(_lab.VerticeDallaMappa("Vertices", 0, 41.0, 12.0, "SectorFiles/non-c-e.fix", 3));

        Assert.False(_lab.Modifiche.CEQualcosa);
        Assert.Contains("non c'è più", _lab.Rifiuto);
    }

    [Fact]
    public async Task LaManigliaDiMezzoAgganciataAggiungeLaCoordinataEsatta()
    {
        await ApriEScegliIlSettore();
        _lab.ModificaSullaMappaDi(true);
        var aea = Forma("AEA");

        Assert.True(_lab.VerticeAggiuntoDallaMappa("Vertices", 1, 40.6381, 8.2918, aea.File, aea.Record));

        await _lab.SalvaAsync();
        Assert.Equal("N040.38.17.400;E008.17.30.400;", Righe(Settore)[2]);
    }

    [Fact]
    public async Task IlTastoDellaSchedaMandaLeManiglieEIlMouseSulVerticeLIndica()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli(Settore, 0));
        pagina.WaitForAssertion(() => Assert.Equal("spenta", pagina.Find("[data-modifica-sulla-mappa]").GetAttribute("data-modifica-sulla-mappa")));

        pagina.Find("[data-modifica-sulla-mappa]").Click();

        pagina.WaitForAssertion(() =>
        {
            Assert.Equal("accesa", pagina.Find("[data-modifica-sulla-mappa]").GetAttribute("data-modifica-sulla-mappa"));
            var mandate = Assert.IsAssignableFrom<IReadOnlyList<ManigliaDelVertice>>(
                _contesto.JSInterop.Invocations["sectorlab.mappa.maniglie"].Last().Arguments[0]);
            Assert.Equal(3, mandate.Count);
        });

        // 🔴 AngleSharp in bUnit: niente [i] su FindAll.
        pagina.FindAll(".lab-vertice").Skip(2).First().TriggerEvent("onmouseenter", new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        pagina.WaitForAssertion(() =>
        {
            var indica = _contesto.JSInterop.Invocations["sectorlab.mappa.indica"].Last();
            Assert.Equal("Vertices", indica.Arguments[0]);
            Assert.Equal(2, indica.Arguments[1]);
        });

        // Un vertice lasciato sulla mappa: nell'elenco si vede quale, e l'elenco ci si porta.
        await pagina.InvokeAsync(() => _lab.VerticeDallaMappa("Vertices", 0, 41.1, 12.1));
        pagina.WaitForAssertion(() =>
        {
            Assert.Single(pagina.FindAll(".lab-vertice-dalla-mappa"));
            Assert.NotEmpty(_contesto.JSInterop.Invocations["sectorlab.inVista"]);
        });

        // Il rifiuto di un vertice col nome sta accanto all'elenco.
        await pagina.InvokeAsync(() => _lab.VerticeDallaMappa("Vertices", 1, 44.0, 8.0));
        pagina.WaitForAssertion(() => Assert.Contains("nome di un punto", pagina.Find("[data-rifiuto-elenco='Vertices']").TextContent));
    }
}
