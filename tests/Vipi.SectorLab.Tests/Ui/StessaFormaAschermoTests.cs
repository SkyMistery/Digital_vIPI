using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>Lotto «Subito», slice 8a: nella scheda, la stessa forma in altri record — e il clic porta alla copia.</summary>
public sealed class StessaFormaAschermoTests : IDisposable
{
    private const string Settore = "SectorFiles/Include/IT/DYNAMIC_SEC/prova.tfl";
    private const string Confine = "SectorFiles/Include/IT/HI_AIRSPACE/prova.hartcc";
    private const string Mappe = "SectorFiles/Include/IT/zzzz.str";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    private static string Anello(int spostato) => string.Concat(Enumerable.Range(0, 10).Select(i =>
        $"N041.{i:00}.00.000;E012.{(i % 2 == 0 ? 0 : 5) + (i == spostato ? 1 : 0):00}.00.000;\r\n"));

    public StessaFormaAschermoTests()
    {
        _albero.Scrivi(Settore, "LZZZ_APP;APP;1;APP;1;\r\n" + Anello(-1));
        _albero.Scrivi(Confine, string.Concat(Anello(-1).Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Reverse()
            .Select(v => "T;ZZ CONF;" + v + "\r\n")));
        _albero.Scrivi(Mappe, "ZZZZ;MAPS;ZZZZ CTR;;;;;1;\r\n" + Anello(4));
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
    public async Task LaSchedaDiceLeCopieUgualiEQuelleDiverseEIlClicPortaAllaCopia()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<StessaFormaNellaScheda>(p => p.Add(c => c.File, Settore).Add(c => c.Record, 0));

        Assert.Contains("1 uguali, 1 diverse", pagina.Find("[data-stessa-forma]").TextContent, StringComparison.Ordinal);
        Assert.Equal("si", pagina.Find($"[data-copia-della-forma='{Confine}#0']").GetAttribute("data-uguale"));
        var diversa = pagina.Find($"[data-copia-della-forma='{Mappe}#0']");
        Assert.Equal("no", diversa.GetAttribute("data-uguale"));
        Assert.Contains("1 vertice solo qui, 1 vertice solo là", diversa.TextContent, StringComparison.Ordinal);

        pagina.Find($"[data-copia-della-forma='{Confine}#0']").Click();

        Assert.Equal((Confine, 0), _lab.Scelta);
    }

    [Fact]
    public async Task DopoUnaModificaLaRispostaSiRifa()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        Assert.Equal(2, _lab.StessaFormaDi(Settore, 0).Single().Copie.Count);

        // Il vertice spostato nel MAPS torna al suo posto: le copie ora sono uguali tutte e due.
        Assert.True(_lab.CambiaRigaAMano(Mappe, 6, "N041.04.00.000;E012.00.00.000;"));

        Assert.All(_lab.StessaFormaDi(Settore, 0).Single().Copie, c => Assert.True(c.Uguale));
    }

    [Fact]
    public async Task UnRecordSenzaCopieNonHaLaSezione()
    {
        _albero.Scrivi(Mappe, "ZZZZ;MAPS;ZZZZ CTR;;;;;1;\r\nN045.00.00.000;E009.00.00.000;\r\nN045.01.00.000;E009.00.00.000;\r\nN045.01.00.000;E009.01.00.000;\r\n");
        _albero.Scrivi(Confine, "T;ZZ CONF;N046.00.00.000;E009.00.00.000;\r\nT;ZZ CONF;N046.01.00.000;E009.00.00.000;\r\nT;ZZ CONF;N046.01.00.000;E009.01.00.000;\r\n");
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<StessaFormaNellaScheda>(p => p.Add(c => c.File, Settore).Add(c => c.Record, 0));

        Assert.Empty(pagina.FindAll("[data-stessa-forma]"));
    }

    // --- slice 8b: la famiglia dichiarata (form=) --------------------------------------------------------------------

    [Fact]
    public async Task DichiarataDallaSchedaLaFamigliaVaSuQuestoESulleCopieUgualiEAnnullataSparisce()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<StessaFormaNellaScheda>(p => p.Add(c => c.File, Settore).Add(c => c.Record, 0));

        Assert.Equal("LZZZ_APP", pagina.Find("[data-nome-famiglia]").GetAttribute("value"));
        pagina.Find("[data-nome-famiglia]").Change("ZZ");
        pagina.Find("[data-scrivi-famiglia]").Click();

        var famiglia = _lab.FamigliaDi(Settore, 0)!;
        Assert.Equal("ZZ", famiglia.Nome);
        Assert.Equal([(Settore, true), (Confine, true)], famiglia.Membri.Select(m => (m.File, m.Uguale)));
        // La copia diversa del MAPS resta fuori: la famiglia è di chi ha la stessa forma.
        Assert.Null(_lab.FamigliaDi(Mappe, 0));
        Assert.Equal("ZZ", pagina.Find("[data-famiglia]").GetAttribute("data-famiglia"));
        Assert.Equal(2, _lab.Modifiche.Voci.Count);

        _lab.Annulla();

        Assert.Null(_lab.FamigliaDi(Settore, 0));
        Assert.False(_lab.Modifiche.CEQualcosa);
    }

    [Fact]
    public async Task AnnullaTuttoToglieLaFamigliaAncheDallaSezioneGiaDisegnata()
    {
        // Trovato sul banco: la sezione ha solo parametri primitivi, e il ridisegno del padre non la raggiungeva.
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<StessaFormaNellaScheda>(p => p.Add(c => c.File, Settore).Add(c => c.Record, 0));
        Assert.Contains("sulla copia uguale", pagina.Find("[data-scrivi-famiglia]").TextContent, StringComparison.Ordinal);

        Assert.True(_lab.ScriviLaFamiglia(Settore, 0, "ZZ", [(Confine, 0)]));
        pagina.WaitForAssertion(() => Assert.NotNull(pagina.Find("[data-famiglia='ZZ']")));

        _lab.AnnullaTutte();

        pagina.WaitForAssertion(() => Assert.Empty(pagina.FindAll("[data-famiglia]")));
    }

    [Fact]
    public async Task UnaCopiaUgualeFuoriDallaFamigliaSiAggiungeColTasto()
    {
        _albero.Scrivi(Settore, "//@\"LZZZ_APP\" form=ZZ\r\n//@START\r\nLZZZ_APP;APP;1;APP;1;\r\n" + Anello(-1) + "//@END \"LZZZ_APP\"\r\n");
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<StessaFormaNellaScheda>(p => p.Add(c => c.File, Settore).Add(c => c.Record, 0));

        pagina.Find("[data-aggiungi-alla-famiglia='1']").Click();

        Assert.Equal("ZZ", _lab.FamigliaDi(Confine, 0)?.Nome);
        Assert.Empty(pagina.FindAll("[data-aggiungi-alla-famiglia]"));
    }

    [Fact]
    public async Task UnaCopiaDiFormaDiversaNellaFamigliaEUnAvvisoDelPannello()
    {
        _albero.Scrivi(Settore, "//@\"LZZZ_APP\" form=ZZ\r\n//@START\r\nLZZZ_APP;APP;1;APP;1;\r\n" + Anello(-1) + "//@END \"LZZZ_APP\"\r\n");
        _albero.Scrivi(Mappe, "//@\"ZZZZ CTR\" form=ZZ\r\n//@START\r\nZZZZ;MAPS;ZZZZ CTR;;;;;1;\r\n" + Anello(4) + "//@END \"ZZZZ CTR\"\r\n");
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<StessaFormaNellaScheda>(p => p.Add(c => c.File, Settore).Add(c => c.Record, 0));

        Assert.NotNull(pagina.Find("[data-famiglia-diversa]"));
        Assert.Equal("no", pagina.Find($"[data-membro-della-famiglia='{Mappe}#0']").GetAttribute("data-uguale"));
        var avviso = Assert.Single(_lab.ProblemiDellAlbero, p => p.Problema.Regola == Vipi.Sectorfile.Validazione.Regola.FormeDiverse);
        // Con due membri e forme diverse non c'è una maggioranza: la forma è quella del primo, e l'avviso va all'altro.
        Assert.Equal((Mappe, 0), (avviso.File, avviso.Record));
        Assert.Empty(pagina.FindAll("[data-questo-diverso]"));
        var dalMaps = _contesto.RenderComponent<StessaFormaNellaScheda>(p => p.Add(c => c.File, Mappe).Add(c => c.Record, 0));
        Assert.NotNull(dalMaps.Find("[data-questo-diverso]"));
    }

    // --- slice 8c: la forma portata sulle copie ------------------------------------------------------------------

    [Fact]
    public async Task AllineaDallaSchedaRendeUgualeLaCopiaDiversaEDiceDoveEAndata()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<StessaFormaNellaScheda>(p => p.Add(c => c.File, Settore).Add(c => c.Record, 0));
        // Sulla copia uguale non c'è niente da allineare.
        Assert.Empty(pagina.FindAll($"[data-allinea='{Confine}#0']"));

        pagina.Find($"[data-allinea='{Mappe}#0']").Click();

        pagina.WaitForAssertion(() => Assert.Equal("si", pagina.Find($"[data-copia-della-forma='{Mappe}#0']").GetAttribute("data-uguale")));
        Assert.Contains("zzzz.str ZZZZ ZZZZ CTR", pagina.Find("[data-forma-portata]").TextContent, StringComparison.Ordinal);
        Assert.Equal([Mappe], _lab.Modifiche.FileToccati);
    }

    [Fact]
    public async Task PrendiLaSuaPortaLaFormaDellaCopiaSuQuestoRecord()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<StessaFormaNellaScheda>(p => p.Add(c => c.File, Settore).Add(c => c.Record, 0));

        pagina.Find($"[data-prendi-la-forma='{Mappe}#0']").Click();

        // Il settore ora ha la forma del MAPS; il confine, che era uguale al settore di prima, adesso è lui il diverso.
        Assert.Equal([Settore], _lab.Modifiche.FileToccati);
        pagina.WaitForAssertion(() =>
        {
            Assert.Equal("si", pagina.Find($"[data-copia-della-forma='{Mappe}#0']").GetAttribute("data-uguale"));
            Assert.Equal("no", pagina.Find($"[data-copia-della-forma='{Confine}#0']").GetAttribute("data-uguale"));
        });
    }

    [Fact]
    public async Task DopoUnGestoSuiVerticiLaSchedaDiceSuQualiCopieEAndataLaForma()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<StessaFormaNellaScheda>(p => p.Add(c => c.File, Settore).Add(c => c.Record, 0));

        Assert.True(_lab.GestoSuiVertici(Settore, 0, "Vertices", GestoDeiVertici.Togli, 2));

        pagina.WaitForAssertion(() => Assert.Contains("prova.hartcc ZZ CONF", pagina.Find("[data-forma-portata]").TextContent, StringComparison.Ordinal));
        Assert.Equal("si", pagina.Find($"[data-copia-della-forma='{Confine}#0']").GetAttribute("data-uguale"));
    }
}
