using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// L'editor dei simboli del <c>.sym</c> (lotto «Subito» slice 17c; «file per file» §20, T1 e T2): la griglia 13×13 dove
/// un clic accende o spegne un pixel, l'anteprima sullo sfondo del radar, il numero nella tavolozza di Aurora, e i
/// due avvisi del formato — il nome senza <c>//</c> e il simbolo senza nome.
/// </summary>
public sealed class SimboliNellaSchedaTests : IDisposable
{
    private const string Sym = "SectorFiles/Include/IT/symbols.sym";
    private const string Fix = "0000000100000;0000001100000;0000010100000;0000100100000;0001000100000;0010000100000;0100000100000;0010000100000;0001000100000;0000100100000;0000010100000;0000001100000;0000000100000;";
    private const string Vuoto = "0000000000000;0000000000000;0000000000000;0000000000000;0000000000000;0000000000000;0000000000000;0000000000000;0000000000000;0000000000000;0000000000000;0000000000000;0000000000000;";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public SimboliNellaSchedaTests()
    {
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        _contesto.Services.AddSingleton(_lab);
        _contesto.JSInterop.Mode = JSRuntimeMode.Loose;
        _contesto.JSInterop.Setup<bool>("sectorlab.mappa.crea", _ => true).SetResult(true);
        string isc = File.ReadAllText(_albero.Percorso("SectorFiles/ITALY.isc"));
        _albero.Scrivi("SectorFiles/ITALY.isc", isc + "\r\n[SYMBOLS]\r\nF;symbols.sym\r\n");
        // Come nel fork: un nome col //, uno senza (AC_comb SEL), uno che non ce l'ha.
        _albero.Scrivi(Sym, "//FIX vuoto\r\n" + Fix + "\r\n\r\nAC_comb SEL\r\n" + Fix + "\r\n\r\n" + Vuoto + "\r\n");
    }

    public void Dispose()
    {
        _contesto.Dispose();
        _albero.Dispose();
    }

    private async Task<IRenderedComponent<Home>> Apri(int record)
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli(Sym, record));
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-simbolo]")));
        return pagina;
    }

    private string[] Righe() => File.ReadAllLines(_albero.Percorso(Sym));

    [Fact]
    public async Task LaGrigliaHaIPixelDelSimbolo_ColNumeroNellaTavolozza()
    {
        var pagina = await Apri(0);

        Assert.Equal("1", pagina.Find("[data-simbolo]").GetAttribute("data-simbolo"));
        Assert.Contains("1° di 3", pagina.Find("[data-simbolo]").TextContent, StringComparison.Ordinal);
        Assert.Equal(169, pagina.FindAll("[data-pixel]").Count);
        // Il FIX: la base del triangolo è la riga y 7, da un bordo all'altro; la punta in alto al centro (x 6, y 1).
        Assert.Equal("true", pagina.Find("[data-pixel='6,1']").GetAttribute("aria-pressed"));
        Assert.Equal("false", pagina.Find("[data-pixel='0,0']").GetAttribute("aria-pressed"));
        Assert.All(Enumerable.Range(0, 13), x => Assert.Equal("true", pagina.Find($"[data-pixel='{x},7']").GetAttribute("aria-pressed")));
        // L'anteprima vera e quella ingrandita hanno gli stessi pixel accesi.
        int accesi = Fix.Count(c => c == '1');
        Assert.Equal(accesi, pagina.FindAll("[data-anteprima-simbolo='vera'] .lab-simbolo-acceso").Count);
        Assert.Equal(accesi, pagina.FindAll("[data-anteprima-simbolo='ingrandita'] .lab-simbolo-acceso").Count);
        // Nell'elenco il nome (il posto lo dice già l'indice del record); il simbolo senza nome si dice.
        Assert.Equal(["FIX vuoto", "AC_comb SEL", "senza nome"], _lab.EtichetteDi(Sym));
    }

    [Fact]
    public async Task UnClicAccendeIlPixel_SiSalvaNellaSuaColonna_ESiAnnulla()
    {
        var pagina = await Apri(0);

        pagina.Find("[data-pixel='0,0']").Click();

        pagina.WaitForAssertion(() => Assert.Equal("true", pagina.Find("[data-pixel='0,0']").GetAttribute("aria-pressed")));
        _lab.Annulla();
        pagina.WaitForAssertion(() => Assert.Equal("false", pagina.Find("[data-pixel='0,0']").GetAttribute("aria-pressed")));

        pagina.Find("[data-pixel='12,12']").Click();
        await _lab.SalvaAsync();

        // Solo l'ultima colonna cambia, all'ultima cifra; il nome e gli altri simboli restano com'erano.
        Assert.Equal("//FIX vuoto", Righe()[0]);
        Assert.Equal(Fix[..^2] + "1;", Righe()[1]);
        Assert.Equal(["", "AC_comb SEL", Fix, "", Vuoto], Righe()[2..]);
    }

    [Fact]
    public async Task IlNomeSenzaBarreSiDiceESiCorreggeColCommento()
    {
        var pagina = await Apri(1);

        Assert.NotEmpty(pagina.FindAll("[data-nome-senza-barre]"));
        Assert.Contains(_lab.ProblemiDellAlbero, p => p.Problema.Regola == Regola.NomeDelSimboloSenzaBarre);

        Assert.True(_lab.CambiaCampo(Sym, 1, "NomeSenzaBarre", "false"));
        await _lab.SalvaAsync();

        Assert.Equal("//AC_comb SEL", Righe()[3]);
        Assert.Equal(Fix, Righe()[4]);
    }

    [Fact]
    public async Task UnSimboloSenzaNomePrendeIlSuoCommento()
    {
        var pagina = await Apri(2);

        Assert.NotEmpty(pagina.FindAll("[data-senza-nome]"));

        Assert.True(_lab.CambiaCampo(Sym, 2, "Nome", "CROCE"));
        await _lab.SalvaAsync();

        Assert.Equal(["//CROCE", Vuoto], Righe()[6..]);
    }

    [Fact]
    public async Task UnSimboloSiSpostaDiUnPosto_LeRigheVuoteRestano_ESiAnnulla()
    {
        // Slice 17d (committente, 17 ottobre): l'ordine serve alla tavolozza di Aurora, e si sposta di proposito.
        var pagina = await Apri(0);
        Assert.True(pagina.Find("[data-sposta-simbolo='su']").HasAttribute("disabled"));

        pagina.Find("[data-sposta-simbolo='giu']").Click();

        // La scelta segue il simbolo, che ora è il 2°; la tavolozza ha AC_comb SEL per primo.
        pagina.WaitForAssertion(() => Assert.Equal("2", pagina.Find("[data-simbolo]").GetAttribute("data-simbolo")));
        Assert.Equal(["AC_comb SEL", "FIX vuoto", "senza nome"], _lab.EtichetteDi(Sym));
        _lab.Annulla();
        Assert.Equal(["FIX vuoto", "AC_comb SEL", "senza nome"], _lab.EtichetteDi(Sym));

        // Un simbolo di una riga sola (senza nome) scambiato con uno di due: le righe vuote fra loro restano al loro posto.
        await pagina.InvokeAsync(() => _lab.Scegli(Sym, 2));
        Assert.True(_lab.SpostaIlSimbolo(Sym, 2, -1));
        Assert.False(_lab.SpostaIlSimbolo(Sym, 0, -1));
        await _lab.SalvaAsync();

        Assert.Equal(["//FIX vuoto", Fix, "", Vuoto, "", "AC_comb SEL", Fix], Righe());
    }

    [Fact]
    public void IlSimboloHaLaSuaDescrizione()
    {
        var descrizione = DescrizioniDeiCampi.Di(new Vipi.Sectorfile.Models.Simbolo());

        Assert.Equal("Simbolo", descrizione!.Nome);
        Assert.Equal(["Nome", "NomeSenzaBarre", "Pixel"], descrizione.Campi.Select(c => c.Proprieta));
    }
}
