using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Lotto «Subito» slice 12b («file per file» R2, R2b, R6): la scheda dello stand scrive tipo e slot (5° e 6° campo) coi
/// valori del manuale, i metadati di stand e taxiway hanno i loro valori chiusi, e da codice, uso e compagnie il Lab
/// propone tipo e slot, che si scrivono con un clic in una voce sola della storia.
/// </summary>
public sealed class StandNellaSchedaTests : IDisposable
{
    private const string Gts = "SectorFiles/Include/IT/lirx.gts";
    private const string Txi = "SectorFiles/Include/IT/lirx.txi";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public StandNellaSchedaTests()
    {
        _albero.Scrivi(Gts, "101;LIRX;N041.48.16.944;E012.16.18.365;\r\n102;LIRX;N041.48.16.209;E012.16.15.712;M;t_A320;\r\n");
        _albero.Scrivi(Txi, "A;LIRX;N041.48.16.000;E012.16.18.000;\r\n");

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
    public async Task TipoESlotSiScrivonoCoiValoriDelManuale_EVuotiNonSiScrivono()
    {
        await Apri();

        Assert.True(_lab.CambiaCampo(Gts, 0, "Type", "h"));
        Assert.Contains("101;LIRX;N041.48.16.944;E012.16.18.365;H;", _lab.RigheDiAdesso(Gts));
        Assert.False(_lab.CambiaCampo(Gts, 0, "Type", "X"));
        Assert.Contains("L, M, H, S o G", _lab.Rifiuto, StringComparison.Ordinal);

        Assert.True(_lab.CambiaCampo(Gts, 0, "Slot", "t_a320 w_ t_A320"));
        Assert.Contains("101;LIRX;N041.48.16.944;E012.16.18.365;H;t_A320 w_;", _lab.RigheDiAdesso(Gts));
        Assert.False(_lab.CambiaCampo(Gts, 0, "Slot", "A320"));
        Assert.Contains("non è un filtro", _lab.Rifiuto, StringComparison.Ordinal);

        // Tolti tutti e due, la riga torna com'era: nessun `;;` in coda.
        Assert.True(_lab.CambiaCampo(Gts, 0, "Slot", ""));
        Assert.True(_lab.CambiaCampo(Gts, 0, "Type", ""));
        Assert.Contains("101;LIRX;N041.48.16.944;E012.16.18.365;", _lab.RigheDiAdesso(Gts));
        Assert.Empty(_lab.Modifiche.Tutte);
    }

    [Fact]
    public async Task LoSlotSenzaTipoLasciaIlQuintoCampoVuoto_EQuelCheLoStandHaGiaResta()
    {
        await Apri();

        Assert.True(_lab.CambiaCampo(Gts, 0, "Slot", "w_"));
        Assert.Contains("101;LIRX;N041.48.16.944;E012.16.18.365;;w_;", _lab.RigheDiAdesso(Gts));
        Assert.Contains("102;LIRX;N041.48.16.209;E012.16.15.712;M;t_A320;", _lab.RigheDiAdesso(Gts));
    }

    [Fact]
    public async Task IMetadatiDiStandETaxiwayHannoIValoriChiusi()
    {
        await Apri();

        Assert.True(_lab.CambiaIlMetadato(Gts, 0, "code", "c"));
        Assert.True(_lab.CambiaIlMetadato(Gts, 0, "use", "cargo, schengen"));
        Assert.True(_lab.CambiaIlMetadato(Gts, 0, "airlines", "dhk ity"));
        Assert.True(_lab.CambiaIlMetadato(Gts, 0, "kind", "Remote"));
        // Le chiavi escono nell'ordine del catalogo di §M, qualunque sia l'ordine in cui si scrivono.
        Assert.Contains("//@\"101\" code=C kind=remote use=schengen,cargo airlines=DHK,ITY", _lab.RigheDiAdesso(Gts));

        Assert.False(_lab.CambiaIlMetadato(Gts, 0, "use", "merci"));
        Assert.Contains("non è un uso", _lab.Rifiuto, StringComparison.Ordinal);
        Assert.False(_lab.CambiaIlMetadato(Gts, 0, "airlines", "ALITALIA"));
        Assert.False(_lab.CambiaIlMetadato(Gts, 0, "code", "G"));

        var metadati = _lab.MetadatiDi(Gts, 0);
        Assert.True(metadati.Single(m => m.Chiave == "push").SiNo);
        Assert.Equal(EditorDelMetadato.Scelta, metadati.Single(m => m.Chiave == "pushdir").Editor);

        Assert.True(_lab.CambiaIlMetadato(Txi, 0, "oneway", "e"));
        Assert.True(_lab.CambiaIlMetadato(Txi, 0, "code", "D"));
        Assert.Contains("//@\"A\" code=D oneway=E", _lab.RigheDiAdesso(Txi));
        Assert.False(_lab.CambiaIlMetadato(Txi, 0, "oneway", "est"));
    }

    [Fact]
    public async Task DaCodiceUsoECompagnieIlLabProponeTipoESlot_ESiScrivonoInUnaVoceSola()
    {
        await Apri();
        Assert.Null(_lab.PropostaDelloStandDi(Gts, 0));

        Assert.True(_lab.CambiaIlMetadato(Gts, 0, "code", "C"));
        Assert.True(_lab.CambiaIlMetadato(Gts, 0, "use", "cargo"));
        Assert.True(_lab.CambiaIlMetadato(Gts, 0, "airlines", "DHK"));

        var proposta = _lab.PropostaDelloStandDi(Gts, 0)!;
        Assert.Equal(("M", "w_ c_DHK"), (proposta.Tipo, proposta.Slot));

        Assert.True(_lab.ApplicaLaPropostaDelloStand(Gts, 0));
        Assert.Contains("101;LIRX;N041.48.16.944;E012.16.18.365;M;w_ c_DHK;", _lab.RigheDiAdesso(Gts));
        Assert.Contains("tipo e slot proposti", _lab.DaAnnullare, StringComparison.Ordinal);
        Assert.Null(_lab.PropostaDelloStandDi(Gts, 0));

        // Una voce sola: un «annulla» toglie tipo e slot insieme, e i metadati restano.
        _lab.Annulla();
        Assert.Contains("101;LIRX;N041.48.16.944;E012.16.18.365;", _lab.RigheDiAdesso(Gts));
        Assert.NotNull(_lab.PropostaDelloStandDi(Gts, 0));
    }

    [Fact]
    public void LaPropostaTieneGliSlotCheLoStandHaGia_ELAviazioneGeneraleVinceSulCodice()
    {
        var stand = new Stand { Number = "102", IcaoCode = "LIRX", Type = "M", Slot = "t_A320" };

        var cargo = PropostaDelloStand.Di(stand, new Dictionary<string, string> { ["code"] = "C", ["use"] = "schengen,cargo" })!;
        Assert.Equal(((string?)null, "t_A320 w_"), (cargo.Tipo, cargo.Slot));

        var ga = PropostaDelloStand.Di(stand, new Dictionary<string, string> { ["code"] = "B", ["use"] = "ga" })!;
        Assert.Equal(("G", (string?)null), (ga.Tipo, ga.Slot));

        Assert.Equal("S", PropostaDelloStand.Di(new Stand(), new Dictionary<string, string> { ["code"] = "F" })!.Tipo);
        Assert.Null(PropostaDelloStand.Di(stand, new Dictionary<string, string> { ["code"] = "C" }));
        Assert.Null(PropostaDelloStand.Di(stand, new Dictionary<string, string> { ["apron"] = "100" }));
    }

    [Fact]
    public async Task ASchermo_LaSchedaDelloStandHaTipoSlotELaProposta()
    {
        await Apri();
        Assert.True(_lab.CambiaIlMetadato(Gts, 1, "use", "cargo"));
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli(Gts, 1));

        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-proposta-stand]")));
        Assert.NotEmpty(pagina.FindAll("[data-scrivi='Type']"));
        Assert.NotEmpty(pagina.FindAll("[data-scrivi='Slot']"));
        Assert.Contains("tipo di aereo A320", pagina.Find("[data-filtro='t_A320']").TextContent, StringComparison.Ordinal);
        Assert.NotEmpty(pagina.FindAll("[data-senza-cargo]"));
        Assert.NotEmpty(pagina.FindAll("select[data-scrivi-tag='code']"));

        pagina.Find("[data-applica-proposta='stand']").Click();
        pagina.WaitForAssertion(() => Assert.Empty(pagina.FindAll("[data-proposta-stand]")));
        Assert.Contains("102;LIRX;N041.48.16.209;E012.16.15.712;M;t_A320 w_;", _lab.RigheDiAdesso(Gts));
        Assert.Empty(pagina.FindAll("[data-senza-cargo]"));
    }
}
