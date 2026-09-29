using Vipi.Sectorfile.Validazione;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Lotto «Subito», slice 11d (carta «file per file» N2, N3): la finestra PAR di un profilo <c>.cpr</c> senza didascalia o
/// con una pista che il <c>.rw</c> non ha; radiale ed elevazione lontane da quelle del <c>.rw</c> (si propone quella del
/// <c>.rw</c>); un profilo con molte impostazioni fuori dai PAR (sovrascrivono l'utente). Numeri veri del fork.
/// </summary>
public sealed class ControlloDeiProfiliTests : IDisposable
{
    private readonly string _radice = Path.Combine(Path.GetTempPath(), "profili-" + Guid.NewGuid().ToString("N"));

    public ControlloDeiProfiliTests()
    {
        Directory.CreateDirectory(Path.Combine(_radice, "Include", "IT", "OTHER"));
        Directory.CreateDirectory(Path.Combine(_radice, "Include", "IT", "PREFS"));
        Scrivi("ITALY.isc", Righe("[INFO]", "N041.48.01.000", "E012.14.20.000", "60", "45", "+4.0", "IT", "", "[RUNWAY]", @"F;OTHER\itrw.rw"));
        Scrivi(@"Include\IT\OTHER\itrw.rw", Righe("//PISTE",
            "LIBV;14L;32R;1124;1186;138;318;N040.46.53.000;E016.54.13.000;N040.45.25.000;E016.56.10.000;",
            "LIPI;06L;24R;162;150;055;235;N046.00.19.000;E012.36.55.000;N046.01.13.000;E012.38.43.000;"));
        Scrivi(@"Include\IT\PREFS\LIBV.cpr", Righe("PAR_VERTICAL_SCAN=30", "[INSET1]",
            "INS1PAR_CAPTION=LIBV RWY14L/2.8°", "INS1PAR_Radial=137", "INS1Par_Elevation=1123",
            "[INSET2]", "INS2PAR_Radial=318", "INS2Par_Elevation=1186",
            "[INSET3]", "INS3RadarRotation=0", "INS3VIEW_TYPE=2", "INS4PAR_CAPTION=LIBV RWY14L/2.5°",
            "[INSET4]", "INS4PAR_CAPTION=LIBV RWY14L/2.5°", "INS4PAR_Radial=137.8", "INS4Par_Elevation=1124"));
        Scrivi(@"Include\IT\PREFS\LIPI.cpr", Righe("[INSET1]", "INS1PAR_CAPTION=LIPI RWY06/2.6°", "INS1PAR_Radial=55", "INS1Par_Elevation=162"));
        Scrivi(@"Include\IT\PREFS\TWR.cpr", Righe(["[PREFS]", .. Enumerable.Range(1, 21).Select(n => $"Chiave{n}=1")]));
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
    public void UnaRadialeLontanaDalRwEUnAvvisoColValoreDelRwProposto()
    {
        // INSET4: 137.8 contro 138 è 0,2 esatto, entro lo scarto.
        var p = Assert.Single(Di(Regola.RadialeDelPar));
        Assert.Equal(Path.Combine("Include", "IT", "PREFS", "LIBV.cpr"), p.File);
        Assert.Equal(4, p.Riga);
        Assert.Equal(Gravita.Avviso, p.Gravita);
        Assert.Equal("INS1PAR_Radial=138", p.Proposta);
    }

    [Fact]
    public void LElevazioneEntroUnPiedeVaBene()
        => Assert.Empty(Di(Regola.ElevazioneDelPar));

    [Fact]
    public void UnaFinestraSenzaDidascaliaOConUnaPistaCheIlRwNonHaEUnAvviso()
    {
        // LIBV INSET3 ha solo VIEW_TYPE=2 (come LIBN.cpr sul fork): è una finestra PAR vuota.
        var problemi = Di(Regola.ParSenzaPista);
        Assert.Equal(3, problemi.Count);
        Assert.Contains(problemi, p => p.Dettaglio.Contains("INSET3", StringComparison.Ordinal));
        Assert.Contains(problemi, p => p.File.EndsWith("LIPI.cpr", StringComparison.Ordinal) && p.Dettaglio.Contains("06L", StringComparison.Ordinal));
        Assert.Contains(problemi, p => p.File.EndsWith("LIBV.cpr", StringComparison.Ordinal) && p.Dettaglio.Contains("INSET2", StringComparison.Ordinal));
    }

    // LIBN.cpr del fork: in [INSET3] ci sono le chiavi INS4…, e INSET3 per Aurora resta senza didascalia.
    [Fact]
    public void UnaChiaveDiUnAltraFinestraNonVale_ELoDiceUnAvviso()
    {
        var p = Assert.Single(Di(Regola.ChiaveFuoriSezione));
        Assert.Contains("[INSET3]", p.Dettaglio, StringComparison.Ordinal);
        Assert.Contains("INS4PAR_CAPTION", p.Dettaglio, StringComparison.Ordinal);
    }

    [Fact]
    public void UnProfiloConMolteImpostazioniEUnAvviso()
    {
        var p = Assert.Single(Di(Regola.ProfiloConMolteImpostazioni));
        Assert.EndsWith("TWR.cpr", p.File, StringComparison.Ordinal);
        Assert.Contains("21", p.Dettaglio);
    }
}
