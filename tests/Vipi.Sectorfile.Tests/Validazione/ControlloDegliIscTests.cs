using Vipi.Sectorfile.Validazione;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Lotto «Subito», slice 2b: il controllo degli <c>.isc</c> (carta «file per file» §C, D7, F6, M6, V2), su un albero
/// piccolo coi casi veri del fork: <c>DYNAMIC_SEC\GCI.tfl</c> che sta in <c>OTHER\</c>, <c>lirrctr.tfl</c> incluso due
/// volte, <c>test.artcc</c> vuoto, i <c>.vfi</c> di <c>ENRVFI</c> sotto <c>[VFRENR]</c>, <c>limw.pol</c> orfano e copia
/// di <c>mw_ad_gnd.pol</c>.
/// </summary>
public sealed class ControlloDegliIscTests : IDisposable
{
    private readonly string _radice = Path.Combine(Path.GetTempPath(), "isc-" + Guid.NewGuid().ToString("N"));

    private static readonly string[] Poligono =
    [
        "STATIC;GRASS;1;GRASS;",
        "N045.44.10.000;E007.22.00.000;",
        "N045.44.20.000;E007.22.30.000;",
        "N045.44.00.000;E007.23.00.000;",
    ];

    public ControlloDegliIscTests()
    {
        Scrivi("ITALY.isc",
            "[INFO]", "N041.48.01.000", "E012.14.20.000", "60", "45", "+4.0", "IT", "",
            "[Airport]", "F;OTHER\\itap.ap", "",
            "[FILLCOLOR]",
            "F;DYNAMIC_SEC\\lirrctr.tfl",
            "F;DYNAMIC_SEC\\GCI.tfl",
            "F;DYNAMIC_SEC\\lirrctr.tfl",
            "F;DYNAMIC_SEC\\sparito.tfl",
            "F;GND_LAYOUT\\mw_ad_gnd.pol", "",
            "[ARTCC]", "F;ACC\\test.artcc", "",
            "[VFRENR]", "F;ENRVFI\\limm.vfi", "",
            "[GEO]", "F;NAVAIDS\\itfix.fix", "F;GEO\\limw.geo");
        Scrivi("Include/IT/OTHER/itap.ap", "LIMW;1791;0;N045.44.18.000;E007.22.05.000;AOSTA;");
        Scrivi("Include/IT/DYNAMIC_SEC/lirrctr.tfl", "LIRR_CTR;CTR;1;CTR;1;", "N041.00.00.000;E012.00.00.000;", "N041.10.00.000;E012.10.00.000;", "N041.00.00.000;E012.20.00.000;");
        Scrivi("Include/IT/OTHER/GCI.tfl", "LIRR_GCI;GCI;1;GCI;1;", "N042.00.00.000;E012.00.00.000;", "N042.10.00.000;E012.10.00.000;", "N042.00.00.000;E012.20.00.000;");
        Scrivi("Include/IT/GND_LAYOUT/mw_ad_gnd.pol", [.. Poligono, "STATIC;APRON;1;APRON;", "N045.45.00.000;E007.24.00.000;", "N045.45.10.000;E007.24.10.000;", "N045.45.00.000;E007.24.20.000;"]);
        Scrivi("Include/IT/limw.pol", Poligono);
        Scrivi("Include/IT/ACC/test.artcc");
        Scrivi("Include/IT/ENRVFI/limm.vfi", "LAGO MAGGIORE;MMN1;N045.55.00.000;E008.35.00.000;1;");
        Scrivi("Include/IT/NAVAIDS/itfix.fix", "BULL;N041.50.00.000;E012.10.00.000;0;0;");
        // Le stesse coordinate anche nel bordo dello scalo: a parità, la copia è il file dello stesso formato.
        Scrivi("Include/IT/GEO/limw.geo",
            "N045.44.10.000;E007.22.00.000;N045.44.20.000;E007.22.30.000;COAST;",
            "N045.44.20.000;E007.22.30.000;N045.44.00.000;E007.23.00.000;COAST;");
    }

    public void Dispose() => Directory.Delete(_radice, recursive: true);

    private void Scrivi(string relativo, params string[] righe)
    {
        string percorso = Path.Combine(_radice, relativo.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
        File.WriteAllText(percorso, righe.Length == 0 ? string.Empty : string.Join("\r\n", righe) + "\r\n");
    }

    private List<ProblemaDelSector> Problemi(Regola regola)
        => Validatore.ValidaLAlbero(_radice).Where(p => p.Regola == regola).ToList();

    private static string Include(params string[] pezzi) => Path.Combine(["Include", "IT", .. pezzi]);

    // M6, §C: il percorso sbagliato è un avviso — Aurora il file lo trova per nome (GCI.tfl sta in OTHER\).
    [Fact]
    public void UnFileCitatoAltroveEUnAvvisoEAuroraLoTrovaPerNome()
    {
        var citati = Problemi(Regola.FileCitatoAssente);

        Assert.All(citati, p => Assert.Equal(Gravita.Avviso, p.Gravita));
        var gci = Assert.Single(citati, p => p.Dettaglio.Contains("GCI.tfl", StringComparison.Ordinal));
        Assert.Contains($"per nome in {Include("OTHER", "GCI.tfl")}", gci.Dettaglio, StringComparison.Ordinal);
        Assert.Single(citati, p => p.Dettaglio.Contains("sparito.tfl", StringComparison.Ordinal));
        Assert.DoesNotContain(Problemi(Regola.FileMaiCitato), p => p.File == Include("OTHER", "GCI.tfl"));
    }

    // D7: lirrctr.tfl due volte in ITALY.isc.
    [Fact]
    public void UnFileInclusoDueVolte()
    {
        var doppio = Assert.Single(Problemi(Regola.FileInclusoDueVolte));

        Assert.Equal(("ITALY.isc", 15, Gravita.Avviso), (doppio.File, doppio.Riga, doppio.Gravita));
        Assert.Contains("già alla riga 13", doppio.Dettaglio, StringComparison.Ordinal);
    }

    // F6: un .vfi di punti sotto [VFRENR] (che vuole le rotte), un .fix sotto [GEO].
    [Fact]
    public void UnFileSottoLaSezioneSbagliata()
    {
        var fuori = Problemi(Regola.FileNellaSezioneSbagliata);

        Assert.Equal(2, fuori.Count);
        Assert.All(fuori, p => Assert.Equal(Gravita.Avviso, p.Gravita));
        Assert.Contains(fuori, p => p.Riga == 23 && p.Dettaglio.Contains("[VFRFIX]", StringComparison.Ordinal));
        Assert.Contains(fuori, p => p.Riga == 26 && p.Dettaglio.Contains("[FIXES]", StringComparison.Ordinal));
    }

    // A9: test.artcc è incluso ma vuoto.
    [Fact]
    public void UnFileVuoto()
    {
        var vuoto = Assert.Single(Problemi(Regola.FileVuoto));

        Assert.Equal((Include("ACC", "test.artcc"), Gravita.Avviso), (vuoto.File, vuoto.Gravita));
    }

    // V2, §21: limw.pol non lo carica nessuno — il .pol non è fra i file che Aurora carica da sé per nome di scalo, e
    // LIMW è uno scalo — e le sue forme stanno in mw_ad_gnd.pol.
    [Fact]
    public void UnFileOrfanoCopiaDiUnAltro()
    {
        var orfano = Assert.Single(Problemi(Regola.FileMaiCitato), p => p.File == Include("limw.pol"));

        Assert.Contains($"è una copia di {Include("GND_LAYOUT", "mw_ad_gnd.pol")}: le sue 3 coordinate ci stanno tutte",
            orfano.Dettaglio, StringComparison.Ordinal);
    }
}
