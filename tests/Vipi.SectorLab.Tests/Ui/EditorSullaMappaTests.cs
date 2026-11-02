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

    // --- fetta 4: dopo un gesto, il solo file (R9) -----------------------------------------------------------------

    [Fact]
    public async Task DopoUnGestoLoStratoDiceQualeFileERifatto()
    {
        await ApriEScegliIlSettore();
        int prima = _lab.VersioneDelloStrato("settori");
        Assert.Empty(_lab.FileRifattiDa("settori", prima)!);

        _lab.ModificaSullaMappaDi(true);
        Assert.True(_lab.VerticeDallaMappa("Vertices", 0, 41.1, 12.1));
        Assert.True(_lab.VerticeDallaMappa("Vertices", 2, 41.6, 12.6));

        Assert.True(_lab.VersioneDelloStrato("settori") > prima);
        // Due gesti sullo stesso file: un file solo da riprendere.
        Assert.Equal([Settore], _lab.FileRifattiDa("settori", prima));
        Assert.Empty(_lab.FileRifattiDa("settori", _lab.VersioneDelloStrato("settori"))!);
    }

    [Fact]
    public async Task DopoUnSalvataggioLoStratoSiRiprendeIntero()
    {
        await ApriEScegliIlSettore();
        int prima = _lab.VersioneDelloStrato("settori");
        _lab.ModificaSullaMappaDi(true);
        Assert.True(_lab.VerticeDallaMappa("Vertices", 0, 41.1, 12.1));
        int dopoIlGesto = _lab.VersioneDelloStrato("settori");

        await _lab.SalvaAsync();

        // Il salvataggio rifà i cataloghi: chi aveva disegnato prima riprende tutto.
        Assert.Null(_lab.FileRifattiDa("settori", prima));
        Assert.Null(_lab.FileRifattiDa("settori", dopoIlGesto));
        Assert.Empty(_lab.FileRifattiDa("settori", _lab.VersioneDelloStrato("settori"))!);
    }

    [Fact]
    public async Task LaMappaDopoUnGestoRiprendeIlSoloFileNonLoStrato()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli(Settore, 0));
        pagina.WaitForAssertion(() => Assert.Contains(_contesto.JSInterop.Invocations["sectorlab.mappa.strato"],
            c => Equals(c.Arguments[0], "settori") && Equals(c.Arguments[1], true)));
        int spenti = _contesto.JSInterop.Invocations["sectorlab.mappa.strato"].Count(c => Equals(c.Arguments[1], false));

        await pagina.InvokeAsync(() => _lab.GestoSuiVertici(Settore, 0, "Vertices", GestoDeiVertici.Cambia, 0, "N041.06.00.000 E012.06.00.000"));

        pagina.WaitForAssertion(() =>
        {
            var rifatto = Assert.Single(_contesto.JSInterop.Invocations["sectorlab.mappa.rifaiIlFile"]);
            Assert.Equal("settori", rifatto.Arguments[0]);
            Assert.Equal(Settore, rifatto.Arguments[1]);
        });
        // Lo strato non si è spento per essere ripreso intero.
        Assert.Equal(spenti, _contesto.JSInterop.Invocations["sectorlab.mappa.strato"].Count(c => Equals(c.Arguments[1], false)));
    }

    // --- §9, D3: disegnare a clic (R10-R13, D5-D7) ----------------------------------------------------------------

    private static PuntoCliccato Clic(double lat, double lon) => new(lat, lon, null, 0);

    [Fact]
    public async Task IlDisegnoPrendeIlPostoDeiVerticiInUnGestoSolo()
    {
        await ApriEScegliIlSettore();
        _lab.ModificaSullaMappaDi(true);
        _lab.DisegnaSullaMappa("Vertices", chiudi: false);
        Assert.NotNull(_lab.Disegno);
        // R10: mentre si disegna le maniglie si tolgono.
        Assert.Empty(_lab.Maniglie());

        Assert.True(_lab.FormaDisegnata([Clic(42.0, 12.0), Clic(42.5, 12.5), Clic(42.0, 13.0), Clic(41.5, 12.5)]));

        Assert.Null(_lab.Disegno);
        Assert.Equal(4, _lab.VerticiDi(Settore, 0, "Vertices").Count);
        Assert.Contains("forma disegnata", _lab.Modifiche.Tutte.Single().Descrizione);
        _lab.Annulla();
        Assert.False(_lab.Modifiche.CEQualcosa);
        _lab.Ripeti();
        await _lab.SalvaAsync();
        Assert.Equal(["N042.00.00.000;E012.00.00.000;", "N042.30.00.000;E012.30.00.000;", "N042.00.00.000;E013.00.00.000;",
                      "N041.30.00.000;E012.30.00.000;"], Righe(Settore).Skip(1));
    }

    [Fact]
    public async Task UnaFormaChiusaRipeteIlPrimoPuntoEUnPuntoSulFixNePrendeIlNome()
    {
        await ApriEScegliIlSettore();
        var aea = Forma("AEA");
        _lab.DisegnaSullaMappa("Vertices", chiudi: true);

        // D5: il secondo clic agganciato al VOR AEA, in un .tfl che sa scrivere i nomi.
        Assert.True(_lab.FormaDisegnata([Clic(42.0, 12.0), new PuntoCliccato(40.638, 8.292, aea.File, aea.Record), Clic(41.0, 13.0)]));

        await _lab.SalvaAsync();
        Assert.Equal(["N042.00.00.000;E012.00.00.000;", "AEA;AEA;", "N041.00.00.000;E013.00.00.000;", "N042.00.00.000;E012.00.00.000;"],
                     Righe(Settore).Skip(1));
    }

    [Fact]
    public async Task ConTroppoPochiPuntiSiRifiutaEIlDisegnoResta()
    {
        await ApriEScegliIlSettore();
        _lab.DisegnaSullaMappa("Vertices", chiudi: true);

        Assert.False(_lab.FormaDisegnata([Clic(42.0, 12.0), Clic(42.5, 12.5)]));

        Assert.NotNull(_lab.Disegno);
        Assert.Contains("almeno 3 punti", _lab.Rifiuto);
        Assert.False(_lab.Modifiche.CEQualcosa);
        _lab.LasciaIlDisegno();
        Assert.Null(_lab.Disegno);
    }

    [Fact]
    public async Task InUnElencoDiSoliNomiSiDisegnaSoloSuiPuntiColNome()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        const string Str = "SectorFiles/Include/IT/lirf.str";
        int record = Enumerable.Range(0, 400).First(r => _lab.ElenchiDiVerticiDi(Str, r).Any(e => e.Quanti > 0 && !e.AmmetteCoordinate));
        string campo = _lab.ElenchiDiVerticiDi(Str, record).First(e => e.Quanti > 0 && !e.AmmetteCoordinate).Chiave;
        _lab.Scegli(Str, record);
        _lab.DisegnaSullaMappa(campo, chiudi: false);
        Assert.True(_lab.Disegno!.SoloNomi);
        var aea = Forma("AEA");
        var alb = Forma("ALB");

        Assert.False(_lab.FormaDisegnata([new PuntoCliccato(40.638, 8.292, aea.File, aea.Record), Clic(42.0, 12.0)]));
        Assert.Contains("Il punto 2", _lab.Rifiuto);

        Assert.True(_lab.FormaDisegnata([new PuntoCliccato(40.638, 8.292, aea.File, aea.Record), new PuntoCliccato(44.05, 8.13, alb.File, alb.Record)]));
        Assert.Equal(["AEA", "ALB"], _lab.VerticiDi(Str, record, campo));
    }

    [Fact]
    public async Task NellaLineaDiUnGeoIlDisegnoRifaLaLinea()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        int primo = Enumerable.Range(0, 200).First(r => _lab.LineaDi(Geo, r) is not null);
        _lab.Scegli(Geo, primo);
        _lab.DisegnaSullaMappa(SessioneDelLab.ChiaveDellaLinea, chiudi: false);

        Assert.True(_lab.FormaDisegnata([Clic(42.0, 13.0), Clic(42.1, 13.1), Clic(42.2, 13.0)]));

        var linea = _lab.LineaDi(Geo, primo)!;
        Assert.Equal(3, linea.Punti.Count);
        Assert.Equal(42.1, linea.Punti[1].LatitudeDeg, 6);
    }

    [Fact]
    public async Task ScegliereUnAltroRecordLasciaIlDisegno()
    {
        await ApriEScegliIlSettore();
        _lab.DisegnaSullaMappa("Vertices", chiudi: false);

        var altra = _lab.Strati.Single(s => s.Id == "settori").Forme.First(f => f.File != Settore);
        _lab.Scegli(altra.File, altra.Record);

        Assert.Null(_lab.Disegno);
        Assert.False(_lab.FormaDisegnata([Clic(42.0, 12.0), Clic(42.5, 12.5)]));
    }

    [Fact]
    public async Task IlTastoDisegnaMandaIlDisegnoAllaMappaELasciaStareLoToglie()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli(Settore, 0));
        pagina.WaitForAssertion(() => Assert.NotNull(pagina.Find("[data-disegna='Vertices']")));

        pagina.Find("[data-disegna='Vertices']").Click();

        pagina.WaitForAssertion(() =>
        {
            Assert.NotNull(pagina.Find("[data-disegno='Vertices']"));
            Assert.NotNull(_contesto.JSInterop.Invocations["sectorlab.mappa.disegna"].Last().Arguments[0]);
        });

        pagina.Find("[data-tasto='lascia-il-disegno']").Click();

        pagina.WaitForAssertion(() =>
        {
            Assert.Empty(pagina.FindAll("[data-disegno]"));
            Assert.Null(_contesto.JSInterop.Invocations["sectorlab.mappa.disegna"].Last().Arguments[0]);
        });
    }

    // --- §11, D4: trascinare i punti (R14-R16) -----------------------------------------------------------------------

    /// <summary>Dove il settore di prova disegna il suo vertice ALB (il secondo): segue il VOR se il catalogo è rifatto.</summary>
    private Vipi.Sectorfile.Shared.Coordinate DoveIlSettoreVedeAlb()
        => _lab.Strati.SelectMany(s => s.Forme).Single(f => f.File == Settore && f.Record == 0).Tratti.SelectMany(t => t).ElementAt(1);

    [Fact]
    public async Task UnFixHaUnaManigliaSolaESpostataScriveLaSuaRiga()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var fix = Forma("BC404");
        _lab.Scegli(fix.File, fix.Record);
        _lab.ModificaSullaMappaDi(true);

        var maniglia = Assert.Single(_lab.Maniglie());
        Assert.True(maniglia.Punto);
        Assert.Equal(SessioneDelLab.CampoDellaPosizione, maniglia.Campo);
        Assert.Equal(39.0865, maniglia.Lat, 3);

        Assert.True(_lab.VerticeDallaMappa(SessioneDelLab.CampoDellaPosizione, 0, 41.25, 12.125));

        Assert.Equal(41.25, Assert.Single(_lab.Maniglie()).Lat, 6);
        // Un punto non si toglie dalla mappa.
        Assert.False(_lab.VerticeToltoDallaMappa(SessioneDelLab.CampoDellaPosizione, 0));
        Assert.Contains("Togli questo record", _lab.Rifiuto);
        await _lab.SalvaAsync();
        Assert.Contains("BC404;N041.15.00.000;E012.07.30.000;3;", File.ReadAllText(_albero.Percorso(fix.File)));
    }

    [Fact]
    public async Task SpostatoUnVorChiLoCitaLoSegueSullaMappaETornaConLAnnulla()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var alb = Forma("ALB");
        var prima = DoveIlSettoreVedeAlb();
        Assert.Equal(44.048, prima.LatitudeDeg, 3);
        _lab.Scegli(alb.File, alb.Record);
        _lab.ModificaSullaMappaDi(true);
        Assert.True(_lab.Maniglie()[0].CitatoDa >= 1);

        Assert.True(_lab.VerticeDallaMappa(SessioneDelLab.CampoDellaPosizione, 0, 43.5, 9.0));

        // R15: il settore che cita ALB per nome lo disegna dove è ADESSO, senza salvare.
        Assert.Equal(43.5, DoveIlSettoreVedeAlb().LatitudeDeg, 6);
        _lab.Annulla();
        Assert.Equal(prima, DoveIlSettoreVedeAlb());
        _lab.Ripeti();
        Assert.Equal(43.5, DoveIlSettoreVedeAlb().LatitudeDeg, 6);
        _lab.AnnullaTutte();
        Assert.Equal(prima, DoveIlSettoreVedeAlb());
    }

    [Fact]
    public async Task AncheDalCampoDellaSchedaChiCitaIlPuntoLoSegue()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var alb = Forma("ALB");

        Assert.True(_lab.CambiaCampo(alb.File, alb.Record, SessioneDelLab.CampoDellaPosizione, "N043.30.00.000 E009.00.00.000"));

        Assert.Equal(43.5, DoveIlSettoreVedeAlb().LatitudeDeg, 6);
        // E la calamita riconosce di nuovo il nome dove il VOR è adesso (il catalogo è rifatto).
        Assert.Equal("ALB", _lab.Suggerimenti("ALB").First(p => p.Nome == "ALB").Nome);
        Assert.Equal(43.5, _lab.Suggerimenti("ALB").First(p => p.Nome == "ALB").Posizione.LatitudeDeg, 6);
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
