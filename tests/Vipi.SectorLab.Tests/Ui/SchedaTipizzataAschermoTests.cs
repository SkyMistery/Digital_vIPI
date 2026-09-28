using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Core.Sessione;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>La scheda tipizzata (lotto «Subito», slice 3), a schermo.</summary>
public sealed class SchedaTipizzataAschermoTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public SchedaTipizzataAschermoTests()
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

    private async Task<IRenderedComponent<Home>> ConIlRecord(string file, string etichetta)
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        string relativo = "SectorFiles/Include/IT/" + file;
        int indice = _lab.EtichetteDi(relativo).ToList().IndexOf(etichetta);
        Assert.True(indice >= 0, $"{etichetta} non c'è in {file}");
        await pagina.InvokeAsync(() => _lab.Scegli(relativo, indice));
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-campo-record]")));
        return pagina;
    }

    private IReadOnlyList<string> Righe(string file) => _lab.RigheDiAdesso("SectorFiles/Include/IT/" + file);

    [Fact]
    public async Task ICampiHannoIlNomeDellAodEIlSignificatoAlPassaggioDelMouse()
    {
        var pagina = await ConIlRecord("NAVAIDS/APT.fix", "BC404");

        var tipo = pagina.Find("[data-campo-record='DisplayType'] th");
        Assert.StartsWith("Tipo", tipo.TextContent.Trim(), StringComparison.Ordinal);
        Assert.Contains("filtro", tipo.GetAttribute("title"), StringComparison.Ordinal);
        Assert.StartsWith("Fix ·", pagina.Find("[data-tipo-record]").TextContent.Trim(), StringComparison.Ordinal);
        Assert.Empty(pagina.FindAll("[data-sconosciuto]"));
        Assert.Empty(pagina.FindAll("[data-campo-record='Source']"));
    }

    // --- slice 3b: gli editor -------------------------------------------------------------------------------------

    [Fact]
    public async Task UnTipoFissoSiScegliDaUnElencoColSignificatoEScriveSoloQuelCampo()
    {
        var pagina = await ConIlRecord("NAVAIDS/APT.fix", "BC404");

        var tipo = pagina.Find("select[data-scrivi='DisplayType']");
        Assert.Contains(tipo.QuerySelectorAll("option"), o => o.TextContent == "3 · nascosto");
        Assert.Contains(tipo.QuerySelectorAll("option"), o => o.TextContent == "1 · terminale (TERM)");
        tipo.Change("1");

        pagina.WaitForAssertion(() => Assert.Equal(1, _lab.Modifiche.Quante));
        // Il pannello e la storia parlano col nome dell'AOD.
        pagina.WaitForAssertion(() => Assert.Contains("Tipo: 3 → 1", pagina.Find("[data-modifiche]").TextContent, StringComparison.Ordinal));
        // La riga del motore, con il solo 4° campo cambiato: nessun byte in più.
        Assert.Contains("BC404;N039.05.11.290;E017.03.27.750;1;", Righe("NAVAIDS/APT.fix"));
        Assert.DoesNotContain("BC404;N039.05.11.290;E017.03.27.750;3;", Righe("NAVAIDS/APT.fix"));
    }

    [Fact]
    public async Task UnValoreFuoriElencoRestaComEESiVede()
    {
        _albero.Scrivi("SectorFiles/Include/IT/GEO/prova.geo",
            "N041.00.00.000;E012.00.00.000;N041.01.00.000;E012.01.00.000;PIPPO;\r\n");
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli("SectorFiles/Include/IT/GEO/prova.geo", 0));

        pagina.WaitForAssertion(() =>
        {
            var tipo = pagina.Find("select[data-scrivi='Color']");
            Assert.Equal("PIPPO", tipo.GetAttribute("value"));
            Assert.NotNull(tipo.QuerySelector("option[data-fuori-elenco='PIPPO']"));
            Assert.Contains(tipo.QuerySelectorAll("option"), o => o.TextContent == "TAXI_CENTER · asse della taxiway");
        });
        Assert.Equal(0, _lab.Modifiche.Quante);
    }

    [Fact]
    public async Task UnSiNoSiScriveConUnaCasella()
    {
        var pagina = await ConIlRecord("OTHER/itfreq.frq", "LIMM_WS2_CTR");

        pagina.Find("input[type='checkbox'][data-scrivi='BlockCpdlc']").Change(true);

        pagina.WaitForAssertion(() => Assert.Equal(1, _lab.Modifiche.Quante));
        Assert.Contains(Righe("OTHER/itfreq.frq"), r => r.StartsWith("LIMM_WS2_CTR;", StringComparison.Ordinal)
                                                      && r.EndsWith(@";PREFS\CTR.cpr;;1;;datis-acc.datis", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LePisteDellaSidSiScelgonoFraQuelleDelRwDelSuoScalo()
    {
        var pagina = await ConIlRecord("lirf.sid", "OST1E");

        var piste = pagina.FindAll("[data-piste='Runway'] [data-pista]").Select(p => p.GetAttribute("data-pista")).ToList();
        Assert.Equal(["16L", "34R", "16R", "34L", "07", "25", "MAPS"], piste);
        Assert.Contains("lab-pista-scelta", pagina.Find("[data-pista='07']").ClassList);
        pagina.Find("[data-pista='25']").Click();

        pagina.WaitForAssertion(() => Assert.Equal(1, _lab.Modifiche.Quante));
        Assert.Contains("LIRF;07:25;OST1E;;;;;1;", Righe("lirf.sid"));
    }

    [Fact]
    public async Task LaQuotaDiUnaMvaDiAccDiceCosaVuolDireEAccettaIlLivello()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli("SectorFiles/Include/IT/ENRMVA/lirr.mva", 0));
        pagina.WaitForAssertion(() => Assert.Equal("= 10 000 ft", pagina.Find("[data-quota='AltLabel']").TextContent));

        pagina.Find("[data-scrivi='AltLabel']").Change("FL90");

        pagina.WaitForAssertion(() => Assert.Equal("= 9 000 ft", pagina.Find("[data-quota='AltLabel']").TextContent));
        Assert.Contains("L;LIRR;N041.08.58.289;E013.24.48.073;90;8;", Righe("ENRMVA/lirr.mva"));
    }

    [Fact]
    public async Task UnCampoCalcolatoNonSiScrive()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli("SectorFiles/Include/IT/DYNAMIC_SEC/libb_es_ctr.tfl", 0));
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-campo-record]")));

        Assert.NotNull(pagina.Find("[data-campo-record='Type']"));
        Assert.Empty(pagina.FindAll("[data-scrivi='Type']"));
        // Le posizioni del settore si scrivono con le voci dei .frq a portata di mano.
        var posizioni = pagina.Find("[data-scrivi='SectorCode']");
        Assert.NotNull(pagina.Find($"datalist#{posizioni.GetAttribute("list")} option[value='LIMM_WS2_CTR']"));
    }

    [Fact]
    public async Task LaQuotaDiUnaMvaDiScaloNonSiScriveFinoAllaSlice15()
    {
        // Il motore la legge dal 2° campo e la riscrive nel 5°, dove sul fork sta quasi sempre la quota vera.
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli("SectorFiles/Include/IT/liba.mva", 0));
        pagina.WaitForAssertion(() => Assert.NotNull(pagina.Find("[data-campo-record='AltLabel']")));

        Assert.Empty(pagina.FindAll("[data-scrivi='AltLabel']"));
        Assert.Empty(pagina.FindAll("[data-scrivi='LabelSize']"));
        Assert.Contains("slice 15", pagina.Find("[data-campo-record='AltLabel'] th").GetAttribute("title"), StringComparison.Ordinal);
    }

    // --- slice 3c: il punto coi suggerimenti ---------------------------------------------------------------------

    private static IReadOnlyList<string> Proposti(IRenderedComponent<Home> pagina, AngleSharp.Dom.IElement campo)
        => [.. pagina.FindAll($"datalist#{campo.GetAttribute("list")} option").Select(o => o.GetAttribute("value") ?? "")];

    [Fact]
    public async Task IlNavaidDellaSidProponeINomiMentreSiScriveEScriveIlNome()
    {
        var pagina = await ConIlRecord("lirf.sid", "OST1E");

        pagina.Find("[data-scrivi='RelatedFix']").Input("bc40");

        pagina.WaitForAssertion(() => Assert.Contains("BC404", Proposti(pagina, pagina.Find("[data-scrivi='RelatedFix']"))));
        Assert.All(Proposti(pagina, pagina.Find("[data-scrivi='RelatedFix']")), n => Assert.StartsWith("BC40", n, StringComparison.Ordinal));
        pagina.Find("[data-scrivi='RelatedFix']").Change("BC404");

        pagina.WaitForAssertion(() => Assert.Equal(1, _lab.Modifiche.Quante));
        Assert.Contains(Righe("lirf.sid"), r => r.StartsWith("LIRF;07;OST1E;", StringComparison.Ordinal) && r.Contains(";BC404;", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LaPosizioneDellAttesaSiScegliePerNome()
    {
        var pagina = await ConIlRecord("HOLDENR.hold", "HLD-ABBOZ");

        pagina.Find("[data-scrivi='Posizione']").Input("BC40");
        pagina.WaitForAssertion(() => Assert.NotEmpty(Proposti(pagina, pagina.Find("[data-scrivi='Posizione']"))));
        pagina.Find("[data-scrivi='Posizione']").Change("BC404");

        pagina.WaitForAssertion(() => Assert.Equal(1, _lab.Modifiche.Quante));
        // Scelto un nome, il file lo scrive due volte: NOME;NOME; (A2).
        Assert.Equal("HLD-ABBOZ;BC404;BC404;ABBOZ/225R-9000;", Righe("HOLDENR.hold")[0]);
    }

    [Fact]
    public async Task UnVerticeCheAmmetteINomiLiPropone()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli("SectorFiles/Include/IT/DYNAMIC_SEC/libb_es_ctr.tfl", 0));
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-vertice]")));

        pagina.Find("[data-vertice='0']").Input("BC40");

        pagina.WaitForAssertion(() => Assert.Contains("BC404", Proposti(pagina, pagina.Find("[data-vertice='0']"))));
    }

    [Fact]
    public async Task IlTestoDiUnEtichettaSiScriveSoloQuandoLEtichettaMostraUnTesto()
    {
        // Misurato su ogni campo: con l'etichetta che mostra il nome del fix il testo scelto non arriva nella riga.
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        string file = "SectorFiles/Include/IT/ACC/FRA.artcc";
        int etichetta = Enumerable.Range(0, _lab.EtichetteDi(file).Count)
            .First(i => ((IFileConRecord)_lab.Sessione!.File[file]).RecordDelModello[i] is Vipi.Sectorfile.Models.LabelPoint);
        await pagina.InvokeAsync(() => _lab.Scegli(file, etichetta));
        pagina.WaitForAssertion(() => Assert.NotNull(pagina.Find("[data-campo-record='Mode']")));

        Assert.NotNull(pagina.Find("[data-scrivi='FixRef']"));
        Assert.Empty(pagina.FindAll("[data-scrivi='CustomName']"));
        Assert.Contains("Cosa mostra", pagina.Find("[data-non-si-scrive='CustomName']").GetAttribute("title"), StringComparison.Ordinal);

        pagina.Find("select[data-scrivi='Mode']").Change("Custom");

        pagina.WaitForAssertion(() => Assert.NotNull(pagina.Find("[data-scrivi='CustomName']")));
        Assert.Empty(pagina.FindAll("[data-scrivi='FixRef']"));
    }

    [Fact]
    public async Task TransizioneERnavDiUnaVoceStrSiScrivonoDallaSlice9()
    {
        // Slice 9b: lo scrittore degli .str arriva all'8° campo (prima si fermava al 6°, e la scheda li teneva fermi).
        var pagina = await ConIlRecord("lirf.str", "LIRF ELKA3A");

        pagina.Find("select[data-scrivi='IsRnav']").Change("");

        pagina.WaitForAssertion(() => Assert.Equal(1, _lab.Modifiche.Quante));
        Assert.Contains("LIRF;16L:16R;ELKA3A;;;;", Righe("lirf.str"));
        Assert.NotNull(pagina.Find("[data-scrivi='Transition']"));
    }

    [Fact]
    public async Task UnaProceduraDiceLeSueVociENonHaUnNomeDiParteDaCommento()
    {
        // Slice 9b, visto sul banco: il nome della parte di una procedura è il suo nome nei dati; scritto come commento
        // sarebbe finito in cima al file.
        var pagina = await ConIlRecord("lirf.str", "LIRF ELKA3A");

        Assert.Equal("16L · STAR, 16R · STAR", pagina.Find("[data-voci-della-procedura]").GetAttribute("data-voci-della-procedura"));
        Assert.Empty(pagina.FindAll("[data-nome-parte]"));
    }

    [Fact]
    public async Task NelMapsIlTipoSiLeggeComeIlTastoCheAccendeLaMappa()
    {
        var pagina = await ConIlRecord("lirf.str", "LIRF LIRF ATZ");

        Assert.StartsWith("Si accende col tasto", pagina.Find("[data-campo-record='RecordType'] th").TextContent.Trim(), StringComparison.Ordinal);
        Assert.Contains("5 · GA", pagina.Find("select[data-scrivi='RecordType']").TextContent, StringComparison.Ordinal);
    }

    // --- slice 3d: i metadati del catalogo ------------------------------------------------------------------------

    [Fact]
    public async Task IMetadatiDelCatalogoAppaionoNellaSchedaESiScrivonoNelTag()
    {
        var pagina = await ConIlRecord("NAVAIDS/APT.fix", "BC404");

        // I .fix hanno le sole chiavi comuni: bloccato, generato da, nota.
        Assert.Equal(["locked", "gen", "note"], pagina.FindAll("[data-metadato]").Select(r => r.GetAttribute("data-metadato")));
        Assert.NotNull(pagina.Find("[data-metadato-fermo='gen']"));
        pagina.Find("[data-scrivi-tag='note']").Change("da rivedere");

        pagina.WaitForAssertion(() => Assert.Contains("note: — → da rivedere", pagina.Find("[data-modifiche]").TextContent, StringComparison.Ordinal));
        Assert.Contains("//@\"BC404\" note=\"da rivedere\"", Righe("NAVAIDS/APT.fix"));
        pagina.Find("input[type='checkbox'][data-scrivi-tag='locked']").Change(true);

        pagina.WaitForAssertion(() => Assert.Contains(Righe("NAVAIDS/APT.fix"),
            r => r.StartsWith("//@\"BC404\"", StringComparison.Ordinal) && r.Contains("locked=si", StringComparison.Ordinal)));
    }

    // --- slice 3e: il tipo del record nuovo ------------------------------------------------------------------------

    [Fact]
    public async Task IlNuovoRecordDiUnArtccChiedeEtichettaOTracciaPrimaDiTutto()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        string file = "SectorFiles/Include/IT/ACC/FRA.artcc";
        await pagina.InvokeAsync(() => _lab.ApriFile(file));
        int prima = _lab.EtichetteDi(file).Count;
        pagina.WaitForAssertion(() => Assert.NotNull(pagina.Find("[data-tasto='aggiungi-al-file']")));

        pagina.Find("[data-tasto='aggiungi-al-file']").Click();

        // Niente è aggiunto finché non si sceglie: la forma di un record nuovo non si indovina.
        pagina.WaitForAssertion(() => Assert.NotNull(pagina.Find("select[data-campo='tipo-nuovo']")));
        Assert.True(pagina.Find("[data-tasto='aggiungi-con-nome']").HasAttribute("disabled"));
        Assert.Contains(pagina.FindAll("select[data-campo='tipo-nuovo'] option"), o => o.TextContent.StartsWith("T · traccia", StringComparison.Ordinal));
        pagina.Find("select[data-campo='tipo-nuovo']").Change("T");
        pagina.Find("[data-tasto='aggiungi-con-nome']").Click();

        pagina.WaitForAssertion(() => Assert.Equal(prima + 1, _lab.EtichetteDi(file).Count));
        Assert.IsType<Vipi.Sectorfile.Models.StaticBoundaryGroup>(((IFileConRecord)_lab.Sessione!.File[file]).RecordDelModello[^1]);
    }

    [Fact]
    public async Task IlRecordComeQuestoParteDalTipoDelRecordEChiedeIlNome()
    {
        var pagina = await ConIlRecord("NAVAIDS/APT.fix", "BC404");

        pagina.Find("[data-tasto='aggiungi-record']").Click();

        pagina.WaitForAssertion(() => Assert.Equal("3", pagina.Find("select[data-campo='tipo-nuovo']").GetAttribute("value")));
        pagina.Find("select[data-campo='tipo-nuovo']").Change("1");
        pagina.Find("[data-campo='nome-nuovo']").Input("BC405");
        pagina.Find("[data-tasto='aggiungi-con-nome']").Click();

        pagina.WaitForAssertion(() => Assert.Contains(Righe("NAVAIDS/APT.fix"), r => r.StartsWith("BC405;", StringComparison.Ordinal) && r.EndsWith(";1;", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task UnVerticeDiUnFileCheNonSaScrivereINomiNonNePropone()
    {
        // I .pol tengono solo coordinate: lì un nome sarebbe rifiutato, e proporlo sarebbe un inganno.
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli("SectorFiles/Include/IT/GND_LAYOUT/br_ad_gnd.pol", 0));
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-vertice]")));

        Assert.Null(pagina.Find("[data-vertice='0']").GetAttribute("list"));
    }
}
