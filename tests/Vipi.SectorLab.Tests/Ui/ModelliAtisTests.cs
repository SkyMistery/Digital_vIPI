using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// L'editor dei modelli ATIS e D-ATIS (lotto «Subito» slice 18; «file per file» §22, W1-W3): il modello fatto a pezzi
/// (segnaposto, parti facoltative, parentesi che non tornano), i segnaposto da scegliere, l'anteprima coi valori
/// d'esempio, «Ascolta», e il D-ATIS che le posizioni dei <c>.frq</c> gli mettono accanto.
/// </summary>
public sealed class ModelliAtisTests : IDisposable
{
    private const string Atis = "SectorFiles/Include/IT/default.atis";
    private const string Datis = "SectorFiles/Include/IT/datis-ad.datis";
    private const string Vuoto = "SectorFiles/Include/IT/datis.datis";

    // default.atis del fork, con la sua «]» in più dopo [ARR].
    private const string DelFork = "This is [STATION_NAME] arrival and departure information [ATIS_LETTER] at [ATIS_TIME]. [Type of Approach [ARR_TYPE]] . "
                                   + "[Runway in use [ARR]]]. [Transition level [TL]] . [METAR] . [Q F Echo [QFE]] . [REMARK] . You have received aitis information [ATIS_LETTER].";

    private const string Giusto = "This is [STATION_NAME] information [ATIS_LETTER] at [ATIS_TIME]. [Type of Approach [ARR_TYPE]] [Runway in use [ARR]] [Transition level [TL]] "
                                  + "[METAR] [QFE [QFE]] [REMARK] You have received ATIS information [ATIS_LETTER]";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;
    private readonly VoceFinta _voce = new();

    public ModelliAtisTests()
    {
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab")) { Voce = _voce };
        _contesto.Services.AddSingleton(_lab);
        _contesto.JSInterop.Mode = JSRuntimeMode.Loose;
        _contesto.JSInterop.Setup<bool>("sectorlab.mappa.crea", _ => true).SetResult(true);
        _albero.Scrivi(Atis, DelFork + "\r\n");
        _albero.Scrivi(Datis, Giusto + "\r\n");
        _albero.Scrivi(Vuoto, "");
        _albero.Scrivi("SectorFiles/Include/IT/atisextra.fds", "Type of Approach;[ARR_TYPE];\r\n");
        // Due posizioni usano l'ATIS col D-ATIS degli scali, una col D-ATIS vuoto. L'altro .frq dei campioni non c'entra.
        _albero.Scrivi("SectorFiles/Include/IT/OTHER/itfreq.frq",
            "LIRF_TWR;118.700;LIRF;PREFS\\TWR.cpr;default.atis;1;;datis-ad.datis\r\nLIRF_GND;121.900;LIRF;PREFS\\TWR.cpr;default.atis;1;;datis-ad.datis\r\n"
            + "LIRA_TWR;120.500;LIRA;PREFS\\TWR.cpr;default.atis;1;;datis.datis\r\n");
        _albero.Scrivi("SectorFiles/Include/IT/OTHER/lirr.frq", "//niente\r\n");
    }

    public void Dispose()
    {
        _contesto.Dispose();
        _albero.Dispose();
    }

    private async Task<IRenderedComponent<Home>> Apri(string file)
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli(file, 0));
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-modello-atis]")));
        return pagina;
    }

    [Fact]
    public async Task IlModelloSiVedeFattoAPezzi_ELaParentesiInPiuSiDice()
    {
        var pagina = await Apri(Atis);

        // Nove segnaposto diversi (la lettera due volte), quattro parti facoltative, e la «]» in più detta col suo posto.
        Assert.Equal(["STATION_NAME", "ATIS_LETTER", "ATIS_TIME", "ARR_TYPE", "ARR", "TL", "METAR", "QFE", "REMARK", "ATIS_LETTER"],
            pagina.FindAll("[data-struttura] [data-segnaposto]").Select(s => s.GetAttribute("data-segnaposto")));
        Assert.Equal(4, pagina.FindAll("[data-struttura] [data-facoltativa]").Count);
        Assert.Contains("141", pagina.Find("[data-parentesi='in-piu']").TextContent, StringComparison.Ordinal);
        Assert.Empty(pagina.FindAll("[data-segnaposto-ignoti]"));
        // ARR_TYPE lo conosce il .fds: è fra quelli da scegliere, col suo campo.
        Assert.Contains("Type of Approach", pagina.Find("[data-inserisci='ARR_TYPE']").GetAttribute("title"), StringComparison.Ordinal);

        // Tolta la parentesi, l'avviso se ne va; un segnaposto che nessuno conosce si dice.
        pagina.Find("[data-scrivi-modello]").Change(DelFork.Replace("[ARR]]].", "[ARR]]. [Vento [WIND_SHEAR]]", StringComparison.Ordinal));

        pagina.WaitForAssertion(() =>
        {
            Assert.Empty(pagina.FindAll("[data-parentesi]"));
            Assert.Contains("[WIND_SHEAR]", pagina.Find("[data-segnaposto-ignoti]").TextContent, StringComparison.Ordinal);
        });
        Assert.Contains("[Runway in use [ARR]]. [Vento [WIND_SHEAR]]", Assert.Single(_lab.RigheDiAdesso(Atis), r => r.Length > 0), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LAnteprimaSiRiempieCoiValoriDEsempio_EUnaParteVuotaSparisce()
    {
        var pagina = await Apri(Datis);

        string prima = pagina.Find("[data-anteprima]").TextContent;
        Assert.StartsWith("This is Roma Fiumicino information A at 1150. Type of Approach ILS Runway in use 16L Transition level 70 LIRF 061150Z", prima, StringComparison.Ordinal);
        Assert.Contains("QFE 1017", prima, StringComparison.Ordinal);
        Assert.DoesNotContain("[", prima, StringComparison.Ordinal);

        pagina.Find("[data-scrivi-valore='ARR']").Change("");
        pagina.Find("[data-scrivi-valore='REMARK']").Change("Bird activity reported");

        pagina.WaitForAssertion(() =>
        {
            string dopo = pagina.Find("[data-anteprima]").TextContent;
            Assert.DoesNotContain("Runway in use", dopo, StringComparison.Ordinal);
            Assert.Contains("QFE 1017 Bird activity reported You have received", dopo, StringComparison.Ordinal);
        });
        // I valori dell'anteprima non sono modifiche del sector.
        Assert.False(_lab.Modifiche.CEQualcosa);
    }

    [Fact]
    public async Task UnSegnapostoSceltoEntraNelModello()
    {
        var pagina = await Apri(Datis);
        Assert.Contains("lab-segnaposto-usato", pagina.Find("[data-inserisci='METAR']").ClassName, StringComparison.Ordinal);
        Assert.DoesNotContain("lab-segnaposto-usato", pagina.Find("[data-inserisci='DEP']").ClassName ?? "", StringComparison.Ordinal);

        // Nei test la pagina non c'è e il cursore nemmeno: il segnaposto va in fondo. Nel Lab va dove sta il cursore.
        await pagina.Find("[data-inserisci='DEP']").ClickAsync(new());

        pagina.WaitForAssertion(() => Assert.EndsWith("information [ATIS_LETTER] [DEP]", Assert.Single(_lab.RigheDiAdesso(Datis), r => r.Length > 0), StringComparison.Ordinal));
        Assert.Contains("lab-segnaposto-usato", pagina.Find("[data-inserisci='DEP']").ClassName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AscoltaLeggeLAnteprimaConLaVoceScelta()
    {
        var pagina = await Apri(Datis);

        pagina.Find("[data-campo='voce']").Change("Seconda");
        pagina.Find("[data-tasto='ascolta']").Click();

        var (testo, voce) = Assert.Single(_voce.Letti);
        Assert.Equal(pagina.Find("[data-anteprima]").TextContent, testo);
        Assert.Equal("Seconda", voce);

        pagina.Find("[data-tasto='ferma-la-voce']").Click();
        Assert.Equal(1, _voce.Fermate);
    }

    [Fact]
    public async Task SenzaUnaVoceAscoltaESpentoColPerche()
    {
        _lab.Voce = new VoceMuta();
        var pagina = await Apri(Datis);

        var tasto = pagina.Find("[data-tasto='ascolta']");

        Assert.True(tasto.HasAttribute("disabled"));
        Assert.Contains("Windows", tasto.GetAttribute("title"), StringComparison.Ordinal);
        Assert.Empty(pagina.FindAll("[data-campo='voce']"));
    }

    [Fact]
    public async Task IlDatisAccantoSiVedeColSuoModello_EUnaDifferenzaSiDice()
    {
        var pagina = await Apri(Atis);

        // I due D-ATIS che le posizioni mettono accanto a default.atis: quello degli scali (due posizioni) e quello vuoto.
        Assert.Equal(["datis-ad.datis", "datis.datis"], pagina.FindAll("[data-compagno]").Select(c => c.GetAttribute("data-compagno")));
        var compagno = pagina.Find("[data-compagno='datis-ad.datis']");
        Assert.Contains("2 posizioni", compagno.TextContent, StringComparison.Ordinal);
        Assert.NotEmpty(compagno.QuerySelectorAll("[data-stessi-segnaposto]"));
        Assert.Contains("This is Roma Fiumicino information A at 1150.", compagno.QuerySelector("[data-anteprima-compagno]")!.TextContent, StringComparison.Ordinal);
        Assert.NotEmpty(pagina.Find("[data-compagno='datis.datis']").QuerySelectorAll("[data-compagno-vuoto]"));

        // Il D-ATIS perde il METAR e guadagna la frequenza: si dice quale dei due ha cosa. Le pronunce non si toccano.
        await pagina.InvokeAsync(() => _lab.CambiaCampo(Datis, 0, "Template", Giusto.Replace("[METAR]", "[DEP_FREQ]", StringComparison.Ordinal)));

        pagina.WaitForAssertion(() =>
        {
            string diversi = pagina.Find("[data-compagno='datis-ad.datis'] [data-segnaposto-diversi]").TextContent;
            Assert.Contains("Solo qui: [METAR]", diversi, StringComparison.Ordinal);
            Assert.Contains("Solo in datis-ad.datis: [DEP_FREQ]", diversi, StringComparison.Ordinal);
        });
        Assert.Contains("aitis information", Assert.Single(_lab.RigheDiAdesso(Atis), r => r.Length > 0), StringComparison.Ordinal);

        // E dall'altra parte: il D-ATIS vede il suo ATIS a voce.
        await pagina.InvokeAsync(() => _lab.Scegli(Datis, 0));
        pagina.WaitForAssertion(() => Assert.Equal(["default.atis"], pagina.FindAll("[data-compagno]").Select(c => c.GetAttribute("data-compagno"))));
    }

    [Fact]
    public async Task UnCampoNuovoDelFdsSiOffreSubitoAiModelli()
    {
        const string Fds = "SectorFiles/Include/IT/atisextra.fds";
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        Assert.Equal(["ARR_TYPE"], _lab.EtichetteDi(Fds));
        Assert.Equal("Modello", Assert.Single(_lab.EtichetteDi(Atis)));

        Assert.True(_lab.AggiungiRecord(Fds, 0));
        Assert.True(_lab.CambiaCampo(Fds, 1, "Segnaposto", "RWY_COND"));
        Assert.True(_lab.CambiaCampo(Fds, 1, "Etichetta", "Runway condition"));

        Assert.Equal(["Type of Approach;[ARR_TYPE];", "Runway condition;[RWY_COND];"], _lab.RigheDiAdesso(Fds).Where(r => r.Length > 0));
        var offerto = Assert.Single(_lab.ModelloAtisDi(Atis, 0)!.Offerti, o => o.Nome == "RWY_COND");
        Assert.Contains("Runway condition", offerto.Significato, StringComparison.Ordinal);
        Assert.False(offerto.Usato);
        // E nell'anteprima parte col valore d'esempio dei campi dei .fds solo ARR_TYPE, che c'era all'apertura.
        Assert.Equal("ILS", _lab.ValoriDellAtis["ARR_TYPE"]);
    }

    [Fact]
    public async Task IProblemiDeiModelliStannoNelPannello()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

        var parentesi = Assert.Single(_lab.ProblemiDellAlbero, p => p.Problema.Regola == Vipi.Sectorfile.Validazione.Regola.ParentesiNonBilanciate && p.File == Atis);

        Assert.Equal(DelFork.Replace("]]].", "]].", StringComparison.Ordinal), parentesi.Problema.Proposta);
        // Il D-ATIS vuoto non è un file vuoto da segnalare.
        Assert.DoesNotContain(_lab.ProblemiDellAlbero, p => p.File == Vuoto);
    }

    [Fact]
    public void LaVoceDiWindowsScriveLaLetturaInUnFile()
    {
        // Senza casse: la lettura va in un .wav. Dove Windows non c'è (la CI) o non ha voci, la prova non ha senso.
        if (VoceDiWindows.Crea() is not { Voci.Count: > 0 } voce)
            return;
        string wav = Path.Combine(_albero.Radice, "prova.wav");
        try
        {
            Assert.NotNull(voce.DiBase);
            Assert.True(voce.ScriviSuFile("This is Roma Fiumicino information A.", wav));
            // Più dell'intestazione di un .wav (44 byte): c'è dell'audio.
            Assert.True(new FileInfo(wav).Length > 2000, $"il file è di {new FileInfo(wav).Length} byte");
        }
        finally
        {
            voce.Dispose();
        }
    }

    private sealed class VoceFinta : IVoce
    {
        public List<(string Testo, string? Voce)> Letti { get; } = [];

        public int Fermate { get; private set; }

        public IReadOnlyList<string> Voci => ["Prima", "Seconda"];

        public string? DiBase => "Prima";

        public string? PercheNo => null;

        public bool Leggi(string testo, string? voce = null)
        {
            Letti.Add((testo, voce));
            return true;
        }

        public void Ferma() => Fermate++;
    }
}
