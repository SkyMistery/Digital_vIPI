using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// La scheda di una rotta VFR (lotto «Subito» slice 16; «file per file» S4, S6, F8): militare sì o no su tutte le sue
/// righe, i tratti col verso e le quote nel tag <c>//@@</c> come le aerovie, i punti proposti dal <c>.vfi</c> dello
/// scalo e poi da quelli vicini.
/// </summary>
public sealed class RotteVfrTests : IDisposable
{
    // Scali veri dell'.ap dei campioni: il master carica da sé i .vfi e i .vrt degli scali che dichiara, e solo quelli
    // entrano nel catalogo dei nomi. Ciampino, l'Urbe a dieci miglia, Catania.
    private const string Vrt = "SectorFiles/Include/IT/lira.vrt";
    private const string Vfi = "SectorFiles/Include/IT/lira.vfi";
    private const string Vicino = "SectorFiles/Include/IT/liru.vfi";
    private const string Lontano = "SectorFiles/Include/IT/licc.vfi";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public RotteVfrTests()
    {
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        _contesto.Services.AddSingleton(_lab);
        _contesto.JSInterop.Mode = JSRuntimeMode.Loose;
        _contesto.JSInterop.Setup<bool>("sectorlab.mappa.crea", _ => true).SetResult(true);
        _albero.Scrivi(Vfi, "GUADO ALFA;RAN1;N041.50.00.000;E012.35.00.000;\r\nGUADO BRAVO;RAN2;N041.52.00.000;E012.35.00.000;\r\n"
                            + "CASELLO;RAS1;N041.40.00.000;E012.36.00.000;\r\n");
        // Lo scalo vicino e uno lontano (in Sicilia): tutti e due hanno un «GUADO …» (un nome che nei campioni non c'è: lirf.vfi ha PONTE GALERIA).
        _albero.Scrivi(Vicino, "GUADO VICINO;RUN1;N042.05.00.000;E012.30.00.000;\r\n");
        _albero.Scrivi(Lontano, "GUADO LONTANO;CCN1;N037.30.00.000;E014.00.00.000;\r\n");
        _albero.Scrivi(Vrt, "1;GUADO ALFA;GUADO ALFA;\r\n1;GUADO BRAVO;GUADO BRAVO;\r\n1;CASELLO;CASELLO;\r\n\r\n"
                            + "2;GUADO ALFA;GUADO ALFA;;1;\r\n2;CASELLO;CASELLO;;1;\r\n");
    }

    public void Dispose()
    {
        _contesto.Dispose();
        _albero.Dispose();
    }

    private async Task Apri() => Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

    [Fact]
    public async Task MilitareSiScriveSuTutteLeRigheDellaRotta()
    {
        await Apri();
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli(Vrt, 0));
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-campo-record='Militare']")));

        Assert.True(_lab.CambiaCampo(Vrt, 0, "Militare", "true"));
        Assert.True(_lab.CambiaCampo(Vrt, 1, "Militare", "false"));

        Assert.Equal(["1;GUADO ALFA;GUADO ALFA;;1;", "1;GUADO BRAVO;GUADO BRAVO;;1;", "1;CASELLO;CASELLO;;1;", "",
                      "2;GUADO ALFA;GUADO ALFA;", "2;CASELLO;CASELLO;"], _lab.RigheDiAdesso(Vrt));
    }

    [Fact]
    public async Task UnPuntoAggiuntoAUnaRottaMilitareNasceMilitare()
    {
        await Apri();

        Assert.True(_lab.GestoSuiVertici(Vrt, 1, "Punti", GestoDeiVertici.Aggiungi, 1));

        var righe = _lab.RigheDiAdesso(Vrt).Skip(4).Where(r => r.Length > 0).ToList();
        Assert.Equal(3, righe.Count);
        Assert.All(righe, r => Assert.EndsWith(";;1;", r, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ITrattiDiUnaRottaHannoVersoEQuoteComeLeAerovie()
    {
        await Apri();
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli(Vrt, 0));

        pagina.WaitForAssertion(() => Assert.Equal(2, pagina.FindAll("[data-tratto]").Count));
        Assert.Equal([(0, "GUADO ALFA", "GUADO BRAVO"), (1, "GUADO BRAVO", "CASELLO")], _lab.TrattiDellAeroviaDi(Vrt, 0).Select(t => (t.Ordinale, t.Da, t.A)));

        pagina.Find("[data-scrivi-tratto='0.dir']").Change("fwd");
        pagina.Find("[data-scrivi-tratto='0.upper']").Change("1500ft");

        pagina.WaitForAssertion(() => Assert.Contains("//@@\"GUADO ALFA\" dir=fwd upper=1500ft", _lab.RigheDiAdesso(Vrt)));
        Assert.Equal("GUADO ALFA → GUADO BRAVO: solo da GUADO ALFA a GUADO BRAVO, ? – 1500ft", _lab.FormaScelta()?.Vincoli);
        // Il tag non spezza la rotta: resta una, coi suoi tre punti.
        Assert.Equal(2, _lab.EtichetteDi(Vrt).Count);
    }

    [Fact]
    public async Task AnnullaTuttoToglieAncheITagDeiTratti()
    {
        await Apri();
        Assert.True(_lab.CambiaIlTagDelPunto(Vrt, 0, 0, "dir", "both"));
        Assert.True(_lab.CambiaIlTagDelPunto(Vrt, 0, 0, "lower", "1000ft agl"));
        Assert.True(_lab.Modifiche.CEQualcosa);

        _lab.AnnullaTutte();

        Assert.False(_lab.Modifiche.CEQualcosa);
        Assert.DoesNotContain(_lab.RigheDiAdesso(Vrt), r => r.StartsWith("//@@", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnTrattoColVersoESenzaQuoteEUnAvviso_AncheNelleRotteVfr()
    {
        _albero.Scrivi(Vrt, "//@@\"GUADO ALFA\" dir=both\r\n1;GUADO ALFA;GUADO ALFA;\r\n1;GUADO BRAVO;GUADO BRAVO;\r\n");
        await Apri();

        var problema = Assert.Single(_lab.ProblemiDellAlbero, p => p.Problema.Regola == Regola.TrattoSenzaQuote && p.File == Vrt);

        Assert.Equal(2, problema.Problema.Riga);
        Assert.Contains("GUADO ALFA → GUADO BRAVO", problema.Problema.Dettaglio, StringComparison.Ordinal);
        Assert.Contains("rotta", problema.Problema.Dettaglio, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IPuntiPropostiSonoPrimaQuelliDelloScaloPoiQuelliVicini()
    {
        await Apri();

        var proposti = _lab.SuggerimentiPer(Vrt, "GUADO");

        // Prima il .vfi dello scalo, poi gli altri punti VFR dal più vicino; i fix del master in fondo.
        Assert.Equal(["GUADO ALFA", "GUADO BRAVO", "GUADO VICINO", "GUADO LONTANO"], proposti.Where(p => p.Catalogo == "vrp").Select(p => p.Nome));
        Assert.Equal([Vfi, Vfi, Vicino, Lontano], proposti.Where(p => p.Catalogo == "vrp").Select(p => p.File));
        // Senza scrivere niente: i punti dello scalo, tutti (sono pochi, e sono quelli che servono).
        Assert.Equal(["CASELLO", "GUADO ALFA", "GUADO BRAVO"], _lab.SuggerimentiPer(Vrt, "").Select(p => p.Nome));
        // Fuori da una rotta VFR resta com'era: niente senza testo.
        Assert.Empty(_lab.SuggerimentiPer(Vfi, ""));
    }
}
