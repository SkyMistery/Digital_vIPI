using Vipi.Sectorfile.Validazione;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Lotto «Subito», slice 9a: i controlli delle procedure (carta «file per file» P3, Q6, R-4) sui casi veri del fork —
/// la SID ripetuta, i campi spostati di uno (<c>VICTOR6A; ;0;VICTOR;</c>), il <c>;</c> mancante di <c>limf.sid:28</c>,
/// la voce di LICC in <c>licz.str</c>, <c>06:24</c> a uno scalo che ha 06L/06R, e i gruppi del menu che non sono piste.
/// </summary>
public sealed class ControlloDelleProcedureTests : IDisposable
{
    private readonly string _radice = Path.Combine(Path.GetTempPath(), "procedure-" + Guid.NewGuid().ToString("N"));

    public ControlloDelleProcedureTests()
    {
        Scrivi("ITALY.isc", "[INFO]", "N041.48.01.000", "E012.14.20.000", "60", "45", "+4.0", "IT", "");
        // Solo le righe sotto //PISTE sono piste (le altre sezioni del .rw sono il menu e l'ACC).
        Scrivi("Include/IT/OTHER/itrw.rw", "//PISTE",
            "LIZZ;16L;34R;14;6;158.7;338.7;N041.50.45.490;E012.15.41.380;N041.48.44.800;E012.16.31.890;",
            "LIZZ;16R;34L;7;8;158.7;338.7;N041.48.55.860;E012.13.34.910;N041.46.55.180;E012.14.25.450;",
            "LIYY;08;26;7;8;80.0;260.0;N041.00.00.000;E012.00.00.000;N041.00.00.000;E012.10.00.000;");
    }

    public void Dispose() => Directory.Delete(_radice, recursive: true);

    private void Scrivi(string relativo, params string[] righe)
    {
        string percorso = Path.Combine(_radice, relativo.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
        File.WriteAllText(percorso, string.Join("\r\n", righe) + "\r\n");
    }

    private List<ProblemaDelSector> Problemi(Regola regola)
        => [.. Validatore.ValidaLAlbero(_radice).Where(p => p.Regola == regola)];

    [Fact]
    public void LaStessaSidDueVolteNelloStessoFileEUnAvviso()
    {
        Scrivi("Include/IT/lizz.sid", "LIZZ;16L;NENI7J;;;;;1;", "LIZZ;16R;NENI7J;;;;;1;", "LIZZ;16L;NENI7J;;;;;1;");

        var ripetuta = Assert.Single(Problemi(Regola.ProceduraRipetuta));

        Assert.Equal((3, Gravita.Avviso), (ripetuta.Riga, ripetuta.Gravita));
        Assert.Contains("già alla riga 1", ripetuta.Dettaglio, StringComparison.Ordinal);
    }

    [Fact]
    public void UnaSidColSestoCampoCheNonEIlTipoEUnErrore()
    {
        Scrivi("Include/IT/lizz.sid", "LIZZ;16L;VICTOR6A; ;0;VICTOR;", "LIZZ;16L;SOS5A-ESI8H; ; ;0;ESINO;1;", "LIZZ;16L;SID1; ; ;TOP;");

        var fuori = Problemi(Regola.TipoFuoriPosto);

        Assert.Equal([1, 3], fuori.Select(p => p.Riga));
        Assert.All(fuori, p => Assert.Equal(Gravita.Errore, p.Gravita));
        Assert.Contains("«VICTOR»", fuori[0].Dettaglio, StringComparison.Ordinal);
    }

    [Fact]
    public void UnaVoceDiUnAltroScaloNelFileDiUnoScaloEUnAvviso()
    {
        // Come licz.str con LICC, e limf.sid:28 dove manca il ; dopo lo scalo.
        Scrivi("Include/IT/lizz.str", "LIYY;08;LIBR1V;;;;;1;", "LIZZ;16L;ELKA3A;;;;;1;");
        Scrivi("Include/IT/lizz.sid", "LIZZ16L;NENI7J;;;;;1;");

        var altri = Problemi(Regola.VoceDiUnAltroScalo);

        Assert.Equal(["Include\\IT\\lizz.sid", "Include\\IT\\lizz.str"], altri.Select(p => p.File.Replace('/', '\\')).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void UnaPistaCheLoScaloNonHaEUnAvvisoIGruppiDelMenuNo()
    {
        Scrivi("Include/IT/lizz.str",
            "LIZZ;16:34;HITAC16;;;;;1;",
            "LIZZ;16L:16R;ELKA3A;;;;;1;",
            "LIZZ;16L:MAPS;SOROP;;;;;1;",
            "LIZZ;BULL;LEVIS;;;;;1;",
            "LIZZ;MAPS;LIZZ CTR; ; ;1;");
        // Uno scalo senza piste nei .rw non si controlla (lirr.str, i gruppi NE e SU).
        Scrivi("Include/IT/lirr.str", "LIRR;NE;LIRF STAR 16 (ALL);;;;;1;", "LIRR;16;QUALCOSA;;;;;1;");

        var mancanti = Assert.Single(Problemi(Regola.PistaInesistente));

        Assert.Equal(1, mancanti.Riga);
        Assert.Equal("LIZZ non ha le piste 16, 34: nei .rw ha 16L, 16R, 34L, 34R", mancanti.Dettaglio);
    }
}
