using Vipi.SectorLab.Core.Copie;
using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Tests.Copie;

/// <summary>
/// Il file giusto per un confine (lotto «Subito» slice 13e, «file per file» J5; committente, 5 ottobre): i nomi delle
/// voci di <c>.hartcc</c>/<c>.lartcc</c> non sono posizioni, quindi la voce si lega al settore dinamico della stessa
/// forma — un settore di aerovia (<c>_CTR</c>, <c>_FSS</c>) sta in <c>HI_AIRSPACE</c>, uno di avvicinamento
/// (<c>_APP</c>) in <c>LOW_AIRSPACE</c>.
/// </summary>
public sealed class PostoDelConfineTests : IDisposable
{
    private const string Settori = "SectorFiles/Include/IT/DYNAMIC_SEC/prova.tfl";
    private const string Alta = "SectorFiles/Include/IT/HI_AIRSPACE/prova.hartcc";
    private const string Bassa = "SectorFiles/Include/IT/LOW_AIRSPACE/prova.lartcc";

    private static readonly string[] Quadrato =
        ["N041.00.00.000;E012.00.00.000;", "N041.10.00.000;E012.00.00.000;", "N041.10.00.000;E012.10.00.000;", "N041.00.00.000;E012.10.00.000;"];

    private static readonly string[] Triangolo =
        ["N043.00.00.000;E012.00.00.000;", "N043.10.00.000;E012.00.00.000;", "N043.10.00.000;E012.10.00.000;"];

    private static readonly string[] Rombo =
        ["N045.00.00.000;E012.00.00.000;", "N045.10.00.000;E012.10.00.000;", "N045.00.00.000;E012.20.00.000;", "N044.50.00.000;E012.10.00.000;"];

    private readonly AlberoDiProva _albero = new();

    public void Dispose() => _albero.Dispose();

    private void Scrivi(string file, params string[] righe) => _albero.Scrivi(file, string.Join("\r\n", righe) + "\r\n");

    private static IEnumerable<string> Voce(string nome, string[] forma) => forma.Select(v => $"T;{nome};{v}");

    private (SessioneAperta Sessione, FormeUguali Forme) Leggi()
    {
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        return (sessione, FormeUguali.Di(StratiDellaMappa.DiSessione(sessione, CatalogoDeiPunti.PerOgniIsc(sessione)["ITALY.isc"])));
    }

    private void ScriviISettori()
        => Scrivi(Settori, ["LZZZ_NE_CTR LZZZ_FSS;CTR;1;CTR;1;", .. Quadrato, "", "LZZA_APP;APP;1;APP;1;", .. Triangolo, "",
            "LZZA_TWR;TWR;1;TWR;1;", .. Rombo]);

    [Theory]
    [InlineData("LZZZ_NE_CTR", "HI_AIRSPACE")]
    [InlineData("LZZZ_FSS LZZZ_MIL_CTR", "HI_AIRSPACE")]
    [InlineData("LZZA_APP", "LOW_AIRSPACE")]
    [InlineData("LZZA_E_APP:LZZA_W_APP", "LOW_AIRSPACE")]
    [InlineData("LZZA_TWR", null)]
    [InlineData("LZZZ_CTR LZZA_APP", null)]
    [InlineData("Static", null)]
    public void LaCartellaVieneDalTipoDellePosizioni(string testa, string? attesa)
        => Assert.Equal(attesa, PostoDelConfine.CartellaPer(new Vipi.Sectorfile.Models.TflSector { SectorCode = testa }));

    [Fact]
    public void UnConfineNelSuoFileNonEUnProblema()
    {
        ScriviISettori();
        Scrivi(Alta, [.. Voce("ZZ NE", Quadrato)]);
        Scrivi(Bassa, [.. Voce("LZZA A0", Triangolo), "", .. Voce("LZZA TORRE", Rombo)]);

        var (sessione, forme) = Leggi();

        Assert.Empty(PostoDelConfine.Problemi(sessione, forme));
    }

    [Fact]
    public void UnAvvicinamentoInAltaEUnAerovieInBassaSonoNelFileSbagliato()
    {
        ScriviISettori();
        Scrivi(Alta, [.. Voce("LZZA A0", Triangolo)]);
        Scrivi(Bassa, [.. Voce("ZZ NE", Quadrato)]);

        var (sessione, forme) = Leggi();
        var problemi = PostoDelConfine.Problemi(sessione, forme).ToList();

        Assert.Equal([(Alta, 1), (Bassa, 1)], problemi.Select(p => (p.File, p.Riga)));
        Assert.All(problemi, p => Assert.Equal(Regola.ConfineNelFileSbagliato, p.Regola));
        Assert.All(problemi, p => Assert.Equal(Gravita.Avviso, p.Gravita));
        Assert.Contains("LZZA_APP", problemi[0].Dettaglio, StringComparison.Ordinal);
        Assert.Contains("LOW_AIRSPACE", problemi[0].Dettaglio, StringComparison.Ordinal);
        Assert.Contains("HI_AIRSPACE", problemi[1].Dettaglio, StringComparison.Ordinal);
    }

    [Fact]
    public void LaSchedaDelSettoreDiceDoveVaIlSuoConfineEQualE()
    {
        ScriviISettori();
        Scrivi(Alta, [.. Voce("ZZ NE", Quadrato), "", .. Voce("LZZA A0", Triangolo)]);

        var (sessione, forme) = Leggi();

        var aerovie = PostoDelConfine.Di(sessione, forme, Settori, 0)!;
        Assert.Equal("HI_AIRSPACE", aerovie.Cartella);
        Assert.Equal([(Alta, 0, true)], aerovie.Confini.Select(c => (c.File, c.Record, c.AlSuoPosto)));

        var avvicinamento = PostoDelConfine.Di(sessione, forme, Settori, 1)!;
        Assert.Equal("LOW_AIRSPACE", avvicinamento.Cartella);
        Assert.Equal([(Alta, 1, false)], avvicinamento.Confini.Select(c => (c.File, c.Record, c.AlSuoPosto)));

        // Una torre non ha un confine da cercare; e nemmeno un record che non è un settore.
        Assert.Null(PostoDelConfine.Di(sessione, forme, Settori, 2));
        Assert.Null(PostoDelConfine.Di(sessione, forme, Alta, 0));
    }
}
