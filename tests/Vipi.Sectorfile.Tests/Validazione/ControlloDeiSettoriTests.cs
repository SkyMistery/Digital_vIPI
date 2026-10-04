using Vipi.Sectorfile.Validazione;
using Xunit;

namespace Vipi.Sectorfile.Validazione.Tests;

/// <summary>
/// Lotto «Subito», slice 13b (carta «file per file» D4): un settore dinamico italiano si accende solo se la sua
/// posizione è in un <c>.frq</c> — definita, o citata fra i trasferimenti. Le posizioni estere non si guardano.
/// </summary>
public sealed class ControlloDeiSettoriTests : IDisposable
{
    private const string Vertici = "N041.00.00.000;E012.00.00.000;\r\nN042.00.00.000;E012.00.00.000;\r\nN042.00.00.000;E013.00.00.000;";

    private readonly string _radice = Path.Combine(Path.GetTempPath(), "settori-" + Guid.NewGuid().ToString("N"));

    public ControlloDeiSettoriTests()
    {
        Directory.CreateDirectory(Path.Combine(_radice, "Include", "IT", "OTHER"));
        Directory.CreateDirectory(Path.Combine(_radice, "Include", "IT", "DYNAMIC_SEC"));
        Scrivi("ITALY.isc", Righe("[INFO]", "N041.48.01.000", "E012.14.20.000", "60", "45", "+4.0", "IT", "",
            "[ATC]", @"F;OTHER\itfreq.frq", "[FILLCOLOR]", @"F;DYNAMIC_SEC\twrs.tfl"));
        Scrivi(@"Include\IT\OTHER\itfreq.frq", Righe(
            @"LIBC_I_TWR;118.100;LIBC LIBB_CS0_APP;PREFS\TWR.cpr;;1;;",
            @"LIBC_GND;121.700;LIBC;PREFS\TWR.cpr;;1;;",
            @"LIRF_TWR;118.700;LIRF;PREFS\TWR.cpr;;1;;"));
        Scrivi(@"Include\IT\DYNAMIC_SEC\twrs.tfl", Righe(
            "LIRF_TWR;TWR;1;TWR;1;", Vertici, "",
            "LIBC_TWR;TWR;1;TWR;1;", Vertici, "",
            "LIBB_CS0_APP;APP;1;APP;1;", Vertici, "",
            "LIRF_TWR:LIQW_I_TWR LFMM_S_CTR;TWR;1;TWR;", Vertici, "",
            "Static;TWR;1;TWR;1;", Vertici));
    }

    public void Dispose()
    {
        if (Directory.Exists(_radice))
            Directory.Delete(_radice, recursive: true);
    }

    private static string Righe(params string[] righe) => string.Join("\r\n", righe) + "\r\n";

    private void Scrivi(string relativo, string testo)
        => File.WriteAllText(Path.Combine(_radice, relativo.Replace('\\', Path.DirectorySeparatorChar)), testo);

    private List<ProblemaDelSector> Di(Regola regola) => Validatore.ValidaLAlbero(_radice).Where(p => p.Regola == regola).ToList();

    [Fact]
    public void UnaPosizioneItalianaCheNessunFrqConosceEUnAvviso()
    {
        var problemi = Di(Regola.SettoreSenzaPosizione);

        // LIRF_TWR è definita, LIBB_CS0_APP è citata fra i trasferimenti, LFMM_S_CTR è estera, Static non ha posizioni.
        Assert.Equal([6, 16], problemi.Select(p => p.Riga));
        Assert.All(problemi, p => Assert.Equal(Gravita.Avviso, p.Gravita));
        Assert.All(problemi, p => Assert.EndsWith("twrs.tfl", p.File, StringComparison.Ordinal));
        Assert.Equal("LIBC_TWR;TWR;1;TWR;1;", problemi[0].Testo);
    }

    [Fact]
    public void LAvvisoDiceLePosizioniDelloStessoScaloCheCiSono()
    {
        var problemi = Di(Regola.SettoreSenzaPosizione);

        Assert.Contains("«LIBC_TWR»", problemi[0].Dettaglio, StringComparison.Ordinal);
        Assert.Contains("LIBC_GND, LIBC_I_TWR", problemi[0].Dettaglio, StringComparison.Ordinal);
        // Una testa con più posizioni: si nomina solo quella che manca, e uno scalo senza posizioni non ne propone.
        Assert.Contains("«LIQW_I_TWR»", problemi[1].Dettaglio, StringComparison.Ordinal);
        Assert.DoesNotContain("LIRF_TWR", problemi[1].Dettaglio, StringComparison.Ordinal);
    }
}
