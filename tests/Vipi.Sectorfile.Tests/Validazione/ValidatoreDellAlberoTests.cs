using Vipi.Sectorfile.Validazione;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Carta F2, slice 8b: le regole che vogliono gli <c>.isc</c>, su un albero piccolo costruito apposta con le forme vere
/// (<c>F;IT\colors\…</c> di LIBB.isc, <c>\liml.atis</c> di itfreq.frq, il caricamento per ICAO).
/// </summary>
public sealed class ValidatoreDellAlberoTests : IDisposable
{
    private readonly string _radice = Path.Combine(Path.GetTempPath(), "albero-" + Guid.NewGuid().ToString("N"));

    public ValidatoreDellAlberoTests()
    {
        Scrivi("ITALY.isc",
            "[INFO]", "N041.48.01.000", "E012.14.20.000", "60", "45", "+4.0", "IT", "",
            "[Airport]", "F;OTHER\\itap.ap", "",
            "[ATC]", "F;OTHER\\itfreq.frq", "",
            "[DEFINE]", "F;IT\\colors\\colors.def", "",
            "[FIXES]", "F;NAVAIDS\\itfix.fix", "F;NAVAIDS\\sparito.fix", "",
            "//F;NAVAIDS\\disattivato.fix");
        Scrivi("Include/IT/OTHER/itap.ap", "LIRF;13;0;N041.48.01.000;E012.14.20.000;FIUMICINO;");
        Scrivi("Include/IT/OTHER/itfreq.frq", "LIRF_TWR;118.700;LIRF;PREFS\\TWR.cpr;;0;;\\liml.atis");
        Scrivi("Include/IT/COLORS/colors.def", "[DEFINE]");
        Scrivi("Include/IT/PREFS/TWR.cpr", "[PREFS]");
        Scrivi("Include/IT/liml.atis", "ATIS");
        Scrivi("Include/IT/NAVAIDS/itfix.fix",
            "BULL;N041.50.00.000;E012.10.00.000;0;0;",
            "LEVIS;N041.40.00.000;E012.00.00.000;0;0;",
            "LEVIS;N041.40.00.300;E012.00.00.000;0;0;",
            "TIBER;N041.30.00.000;E012.30.00.000;0;0;",
            "TIBER;N041.40.00.000;E012.30.00.000;0;0;");
        Scrivi("Include/IT/lirf.str", "LIRF;16L;BULL1A; ; ;0;", "BULL;BULL;", "NESSUNO;NESSUNO;", "LIRF;LIRF;");
        Scrivi("Include/IT/zzzz.sid", "ZZZZ;01;ORFA1A;;;;;1;");
        Scrivi("Include/IT/leggimi.txt", "note");
    }

    public void Dispose() => Directory.Delete(_radice, recursive: true);

    private void Scrivi(string relativo, params string[] righe)
    {
        string percorso = Path.Combine(_radice, relativo.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
        File.WriteAllText(percorso, string.Join("\r\n", righe) + "\r\n");
    }

    private List<ProblemaDelSector> Problemi(Regola regola)
        => Validatore.ValidaLAlbero(_radice).Where(p => p.Regola == regola).ToList();

    // Solo sparito.fix: `IT\colors\colors.def` si trova da Include/, `\liml.atis` dalla cartella dei dati, e il F;
    // disattivato non conta.
    [Fact]
    public void UnFileCitatoCheNonCeSiDice()
    {
        var problema = Assert.Single(Problemi(Regola.FileCitatoAssente));
        Assert.Equal(("ITALY.isc", 20), (problema.File, problema.Riga));
        Assert.Contains("sparito.fix", problema.Dettaglio, StringComparison.Ordinal);
    }

    // lirf.str si carica per ICAO (LIRF è in [Airport]); TWR.cpr e liml.atis dal .frq; zzzz.sid da nessuno. I testi no.
    [Fact]
    public void UnFileCheNessunoCaricaEOrfano()
    {
        var orfano = Assert.Single(Problemi(Regola.FileMaiCitato));
        Assert.Equal(Path.Combine("Include", "IT", "zzzz.sid"), orfano.File);
        Assert.Equal(Gravita.Avviso, orfano.Gravita);
    }

    // BULL è un fix, LIRF uno scalo; NESSUNO non c'è in nessun catalogo.
    [Fact]
    public void UnNomeCheNonSiTrovaEUnErrore()
    {
        var problema = Assert.Single(Problemi(Regola.NomeNonRisolto));
        Assert.Equal((Path.Combine("Include", "IT", "lirf.str"), 3), (problema.File, problema.Riga));
        Assert.Contains("«NESSUNO»", problema.Dettaglio, StringComparison.Ordinal);
    }

    // LEVIS due volte a 9 m: un arrotondamento, un avviso. TIBER a 10 NM: quale vale? Un errore.
    [Fact]
    public void UnNomeDoppioLontanoEUnErroreVicinoUnAvviso()
    {
        var duplicato = Assert.Single(Problemi(Regola.NomeDuplicato));
        Assert.Contains("«TIBER»", duplicato.Dettaglio, StringComparison.Ordinal);
        Assert.Contains("a 10.0", duplicato.Dettaglio, StringComparison.Ordinal);

        var ripetuto = Assert.Single(Problemi(Regola.NomeRipetuto));
        Assert.Contains("«LEVIS»", ripetuto.Dettaglio, StringComparison.Ordinal);
    }

    [Fact]
    public void LeRegoleDeiFileCiSono()
    {
        Scrivi("Include/IT/NAVAIDS/itfix.fix", "BULL;N041.50.75.000;E012.10.00.000;0;0;");

        var problema = Assert.Single(Problemi(Regola.CoordinataFuoriCampo));
        Assert.Equal(Path.Combine("Include", "IT", "NAVAIDS", "itfix.fix"), problema.File);
    }
}
