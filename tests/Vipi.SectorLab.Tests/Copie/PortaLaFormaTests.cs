using Vipi.SectorLab.Core.Copie;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Tests.Ui;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Copie;

/// <summary>
/// La forma portata sulle copie (lotto «Subito» slice 8c, D5): un gesto sui vertici va anche sulle copie che erano
/// uguali, che tengono la loro partenza, il loro verso, la chiusura e la scrittura dei vertici; «allinea» e «prendi la
/// sua» portano la forma su una copia diversa.
/// </summary>
public sealed class PortaLaFormaTests : IDisposable
{
    private const string Settore = "SectorFiles/Include/IT/DYNAMIC_SEC/prova.tfl";
    private const string Confine = "SectorFiles/Include/IT/HI_AIRSPACE/prova.hartcc";
    private const string Mappe = "SectorFiles/Include/IT/zzzz.str";
    private const string Erba = "SectorFiles/Include/IT/GND_LAYOUT/prova.pol";

    private readonly AlberoDiProva _albero = new();
    private readonly SessioneDelLab _lab;

    // Dieci vertici a zig-zag: N041.ii, E012.00 o E012.05; «spostato» sposta di un primo il vertice i.
    private static string Vertice(int i, int spostato = -1)
        => $"N041.{i:00}.00.000;E012.{(i % 2 == 0 ? 0 : 5) + (i == spostato ? 1 : 0):00}.00.000;";

    // Lo stesso vertice in forma compatta, come la scrivono tanti .hartcc.
    private static string Compatto(int i) => $"N041{i:00}00000;E012{(i % 2 == 0 ? 0 : 5):00}00000;";

    public PortaLaFormaTests()
    {
        _albero.Scrivi(Settore, "LZZZ_APP;APP;1;APP;1;\r\n" + string.Concat(Enumerable.Range(0, 10).Select(i => Vertice(i) + "\r\n")));
        // Il confine: la stessa forma dal vertice 3, all'indietro, compatta, e chiusa ripetendo il primo.
        int[] giro = [3, 2, 1, 0, 9, 8, 7, 6, 5, 4, 3];
        _albero.Scrivi(Confine, string.Concat(giro.Select(i => "T;ZZ CONF;" + Compatto(i) + "\r\n")));
        // Il MAPS: già diverso (il vertice 4 spostato).
        _albero.Scrivi(Mappe, "ZZZZ;MAPS;ZZZZ CTR;;;;;1;\r\n" + string.Concat(Enumerable.Range(0, 10).Select(i => Vertice(i, spostato: 4) + "\r\n")));
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
    }

    public void Dispose() => _albero.Dispose();

    private string[] Righe(string file) => File.ReadAllText(Path.Combine(_albero.Radice, file)).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public async Task UnVerticeSpostatoVaSullaCopiaUgualeCheTieneIlSuoGiroELaSuaScrittura()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

        // Il vertice 6 del settore (riga 8) si sposta di un primo a est.
        Assert.True(_lab.GestoSuiVertici(Settore, 0, "Vertices", GestoDeiVertici.Cambia, 6, "N041.06.00.000;E012.01.00.000;"));

        var portata = Assert.Single(_lab.FormaPortata!.Portate);
        Assert.Equal(Confine, portata.File);
        Assert.Empty(_lab.FormaPortata.NonPortate);
        // Il MAPS era già diverso: non si tocca.
        Assert.DoesNotContain(Mappe, _lab.Modifiche.FileToccati);
        await _lab.SalvaAsync();
        Assert.False(_lab.Modifiche.CEQualcosa);

        // Stesso giro (dal 3, all'indietro), chiuso; anche il vertice nuovo è compatto: una riga toccata esce nella forma
        // delle righe del file (FormaDelPunto, F2), e il diff dice solo quel che è cambiato.
        Assert.Equal(
            [.. new[] { 3, 2, 1, 0, 9, 8, 7 }.Select(i => "T;ZZ CONF;" + Compatto(i)), "T;ZZ CONF;N0410600000;E0120100000;",
             .. new[] { 5, 4, 3 }.Select(i => "T;ZZ CONF;" + Compatto(i))],
            Righe(Confine));
    }

    [Fact]
    public async Task UnVerticeAggiuntoVaAlSuoPostoAncheNellaCopiaAllIndietro()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

        // Un vertice nuovo fra il 6 e il 7 del settore.
        Assert.True(_lab.GestoSuiVertici(Settore, 0, "Vertices", GestoDeiVertici.Aggiungi, 7, "N041.06.30.000;E012.02.30.000;"));
        await _lab.SalvaAsync();
        Assert.False(_lab.Modifiche.CEQualcosa);

        var righe = Righe(Confine);
        Assert.Equal(12, righe.Length);
        // All'indietro: dopo il 7 viene il nuovo, poi il 6.
        int sette = Array.IndexOf(righe, "T;ZZ CONF;" + Compatto(7));
        Assert.Equal("T;ZZ CONF;N0410630000;E0120230000;", righe[sette + 1]);
        Assert.Equal("T;ZZ CONF;" + Compatto(6), righe[sette + 2]);
    }

    [Fact]
    public async Task AnnullatoIlGestoTornaComEraAncheLaCopiaERipetutoCiRitorna()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        Assert.True(_lab.GestoSuiVertici(Settore, 0, "Vertices", GestoDeiVertici.Togli, 6));
        Assert.Contains(Confine, _lab.Modifiche.FileToccati);

        _lab.Annulla();
        Assert.False(_lab.Modifiche.CEQualcosa);

        _lab.Ripeti();
        Assert.Equal([Settore, Confine], _lab.Modifiche.FileToccati.Order(StringComparer.Ordinal));
        Assert.All(_lab.StessaFormaDi(Settore, 0).Single().Copie.Where(c => c.Dove.File == Confine), c => Assert.True(c.Uguale));
    }

    [Fact]
    public async Task AllineaPortaLaFormaSuUnaCopiaDiversa()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var parti = _lab.StessaFormaDi(Settore, 0).Single();
        var mappa = parti.Copie.Single(c => c.Dove.File == Mappe);
        Assert.False(mappa.Uguale);

        Assert.True(_lab.CopiaLaForma(parti.Parte, mappa.Dove));

        Assert.All(_lab.StessaFormaDi(Settore, 0).Single().Copie, c => Assert.True(c.Uguale));
        await _lab.SalvaAsync();
        Assert.False(_lab.Modifiche.CEQualcosa);
        Assert.Equal(Vertice(4), Righe(Mappe)[5]);
    }

    [Fact]
    public async Task UnNomeDiventaLaSuaPosizioneDoveLaCopiaTieneSoloCoordinate()
    {
        // Il settore ha un vertice per nome (un fix dei campioni); l'erba in un .pol tiene solo coordinate.
        _albero.Scrivi(Settore, "LZZZ_APP;APP;1;APP;1;\r\nN045.00.00.000;E009.00.00.000;\r\nN045.00.10.000;E009.00.00.000;\r\n"
                                + "N045.00.10.000;E009.00.10.000;\r\nN045.00.00.000;E009.00.10.000;\r\n");
        _albero.Scrivi(Erba, "STATIC;GRASS;1;GRASS;\r\nN045.00.00.000;E009.00.00.000;\r\nN045.00.10.000;E009.00.00.000;\r\n"
                             + "N045.00.10.000;E009.00.10.000;\r\nN045.00.00.000;E009.00.10.000;\r\n");
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var fix = _lab.Cataloghi["ITALY.isc"].Suggerisci("BC4", 1).Single();

        Assert.True(_lab.GestoSuiVertici(Settore, 0, "Vertices", GestoDeiVertici.Cambia, 3, fix.Nome));

        Assert.Equal(Erba, Assert.Single(_lab.FormaPortata!.Portate).File);
        await _lab.SalvaAsync();
        Assert.False(_lab.Modifiche.CEQualcosa);
        Assert.DoesNotContain(fix.Nome, File.ReadAllText(Path.Combine(_albero.Radice, Erba)), StringComparison.Ordinal);
        Assert.Contains(Righe(Settore), r => r.Contains(fix.Nome, StringComparison.Ordinal));
    }
}
