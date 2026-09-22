using Vipi.Sectorfile.IO;

namespace Vipi.Sectorfile.Tests.IO;

/// <summary>
/// Che cosa carica un <c>.isc</c> (<see cref="CarichiDegliIsc"/>): le tre vie — <c>F;</c>, il codice di uno scalo,
/// un file nominato da un <c>.frq</c>. Stava dentro il validatore dell'albero; da F3 lo chiede anche il catalogo dei
/// punti dell'app, e sta in un posto solo.
/// </summary>
public sealed class CarichiDegliIscTests : IDisposable
{
    private readonly string _radice = Path.Combine(Path.GetTempPath(), "carichi-" + Guid.NewGuid().ToString("N"));

    public CarichiDegliIscTests()
    {
        Scrivi("ITALY.isc", """
            [INFO]
            N041.48.01.000
            E012.14.20.000
            60
            45
            +4.0
            IT

            [NAVAIDS]
            F;NAVAIDS\itfix.fix
            F;NAVAIDS\manca.fix

            [AIRPORT]
            F;OTHER\itap.ap

            [ATC]
            F;OTHER\itfreq.frq
            """);
        Scrivi("Include/IT/NAVAIDS/itfix.fix", "ABCDE;N041.00.00.000;E012.00.00.000;3;");
        Scrivi("Include/IT/OTHER/itap.ap", "LIRF;118.700;N041.48.01.000;E012.14.20.000;0;");
        Scrivi("Include/IT/OTHER/itfreq.frq", "LIRF_TWR;118.700;PREFS\\lirf.cpr;\r\nLIRF_APP;124.350;PREFS\\manca.cpr;");
        Scrivi("Include/IT/PREFS/lirf.cpr", "prova");
        // Entra da sé perché porta il codice di uno scalo di [AIRPORT], senza che nessuno lo citi.
        Scrivi("Include/IT/lirf.sid", "LIRF;16R;OST1E;N041.48.01.000;E012.14.20.000;");
        Scrivi("Include/IT/limc.sid", "LIMC;35L;PIRO1A;N045.37.00.000;E008.43.00.000;");
    }

    public void Dispose() => Directory.Delete(_radice, recursive: true);

    private void Scrivi(string relativo, string testo)
    {
        string percorso = Path.Combine(_radice, relativo.Replace('\\', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
        File.WriteAllText(percorso, testo.ReplaceLineEndings("\r\n"));
    }

    private CaricoDiUnIsc Carico()
        => Assert.Single(CarichiDegliIsc.Leggi(_radice, percorso =>
            percorso.EndsWith("itap.ap", StringComparison.Ordinal) ? ["LIRF"] : []));

    [Fact]
    public void LeTreVie()
    {
        var carico = Carico();
        var caricati = carico.Caricati.Select(p => Path.GetRelativePath(_radice, p).Replace('\\', '/')).ToHashSet(StringComparer.Ordinal);

        Assert.Equal("ITALY.isc", carico.Nome);
        Assert.Equal("IT", carico.CartellaDati);
        Assert.Contains("Include/IT/NAVAIDS/itfix.fix", caricati);   // citato con F;
        Assert.Contains("Include/IT/lirf.sid", caricati);            // per ICAO, da sé
        Assert.Contains("Include/IT/PREFS/lirf.cpr", caricati);      // nominato dal .frq
        Assert.DoesNotContain("Include/IT/limc.sid", caricati);      // LIMC non è fra gli scali
    }

    [Fact]
    public void IFileCitatiEAssentiSiDiconoConLaRigaDoveStanno()
    {
        var mancanti = Carico().Mancanti;

        var isc = Assert.Single(mancanti, m => m.Citato.EndsWith("manca.fix", StringComparison.Ordinal));
        Assert.Equal("ITALY.isc", isc.File);
        Assert.Equal(11, isc.Riga);

        var frq = Assert.Single(mancanti, m => m.Citato.EndsWith("manca.cpr", StringComparison.Ordinal));
        Assert.EndsWith("itfreq.frq", frq.File, StringComparison.Ordinal);
        Assert.Equal(2, frq.Riga);
    }

    [Fact]
    public void TuttiMetteInsiemeICarichiDiPiuMaster()
    {
        File.Copy(Path.Combine(_radice, "ITALY.isc"), Path.Combine(_radice, "LIRR.isc"));
        var carichi = CarichiDegliIsc.Leggi(_radice, _ => []);

        Assert.Equal(2, carichi.Count);
        Assert.Equal(carichi[0].Caricati.Count, CarichiDegliIsc.Tutti(carichi).Count);
    }
}
