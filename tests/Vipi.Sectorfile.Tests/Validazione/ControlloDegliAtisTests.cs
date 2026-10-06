using Vipi.Sectorfile.Validazione;
using Xunit;

namespace Vipi.Sectorfile.Validazione.Tests;

/// <summary>
/// Lotto «Subito», slice 18a (carta «file per file» §22, W3 e W4): i controlli dei modelli ATIS e D-ATIS — segnaposto
/// che Aurora non conosce, parentesi che non tornano, campo di un <c>.fds</c> che nessun modello usa, e il modello a
/// voce che non ha gli stessi segnaposto del suo D-ATIS.
/// </summary>
public sealed class ControlloDegliAtisTests : IDisposable
{
    private readonly string _radice = Path.Combine(Path.GetTempPath(), "atis-" + Guid.NewGuid().ToString("N"));

    public ControlloDegliAtisTests()
    {
        Directory.CreateDirectory(Path.Combine(_radice, "Include", "IT", "OTHER"));
        Scrivi("ITALY.isc", Righe("[INFO]", "N041.48.01.000", "E012.14.20.000", "60", "45", "+4.0", "IT", "",
            "[ATIS]", "F;default.atis", "[ATISFIELD]", "F;atisextra.fds", "[ATC]", @"F;OTHER\itfreq.frq"));
        Scrivi(@"Include\IT\atisextra.fds", Righe("Type of Approach;[ARR_TYPE];"));
        Scrivi(@"Include\IT\OTHER\itfreq.frq", Righe(@"LIRF_TWR;118.700;LIRF;;default.atis;1;;datis-ad.datis"));
        Scrivi(@"Include\IT\default.atis", Righe("This is [STATION_NAME] information [ATIS_LETTER]. [Type of Approach [ARR_TYPE]] . [METAR] ."));
        Scrivi(@"Include\IT\datis-ad.datis", Righe("This is [STATION_NAME] information [ATIS_LETTER]. [Type of Approach [ARR_TYPE]] [METAR]"));
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
    public void ModelliAPostoNonDannoAvvisi()
    {
        var problemi = Validatore.ValidaLAlbero(_radice).Where(p => p.Regola is Regola.SegnapostoSconosciuto or Regola.ParentesiNonBilanciate
            or Regola.CampoDellAtisMaiUsato or Regola.AtisEDatisDiversi).ToList();

        Assert.Empty(problemi);
    }

    [Fact]
    public void UnSegnapostoCheNessunoConosceEUnAvviso()
    {
        // ARR_TYPE lo dichiara il .fds; QFE e CPDLC sono di Aurora anche se ATIS Creator non li ha; WIND_SHEAR no.
        Scrivi(@"Include\IT\default.atis", Righe("This is [STATION_NAME] [ATIS_LETTER]. [Type [ARR_TYPE]] [Q F Echo [QFE]] [CPDLC] [Attenzione [WIND_SHEAR]] [METAR]"));

        var problema = Assert.Single(Di(Regola.SegnapostoSconosciuto));

        Assert.EndsWith("default.atis", problema.File, StringComparison.Ordinal);
        Assert.Equal(1, problema.Riga);
        Assert.Contains("[WIND_SHEAR]", problema.Dettaglio, StringComparison.Ordinal);
        Assert.Equal(Gravita.Avviso, problema.Gravita);
    }

    [Fact]
    public void UnaChiusaInPiuHaLaRigaCorretta_UnaInMenoNo()
    {
        // default.atis del fork.
        Scrivi(@"Include\IT\default.atis", Righe("This is [STATION_NAME] [ATIS_LETTER]. [Type [ARR_TYPE]] [Runway in use [ARR]]]. [METAR] ."));
        Scrivi(@"Include\IT\datis-ad.datis", Righe("This is [STATION_NAME] [ATIS_LETTER]. [Type [ARR_TYPE]] [Runway in use [ARR] [METAR]"));

        var problemi = Di(Regola.ParentesiNonBilanciate);

        Assert.Equal(["datis-ad.datis", "default.atis"], problemi.Select(p => Path.GetFileName(p.File)));
        Assert.Null(problemi[0].Proposta);
        Assert.Contains("non si chiude", problemi[0].Dettaglio, StringComparison.Ordinal);
        Assert.Equal("This is [STATION_NAME] [ATIS_LETTER]. [Type [ARR_TYPE]] [Runway in use [ARR]]. [METAR] .", problemi[1].Proposta);
        Assert.Contains("in più", problemi[1].Dettaglio, StringComparison.Ordinal);
    }

    [Fact]
    public void UnCampoDelFdsCheNessunModelloUsaEUnAvviso()
    {
        Scrivi(@"Include\IT\atisextra.fds", Righe("Type of Approach;[ARR_TYPE];", "Pista chiusa;[RWY_CLOSED];"));

        var problema = Assert.Single(Di(Regola.CampoDellAtisMaiUsato));

        Assert.EndsWith("atisextra.fds", problema.File, StringComparison.Ordinal);
        Assert.Equal((2, "Pista chiusa;[RWY_CLOSED];"), (problema.Riga, problema.Testo));
    }

    [Fact]
    public void UnAtisEIlSuoDatisHannoGliStessiSegnaposto()
    {
        // Il D-ATIS ha perso il METAR: le posizioni che usano tutti e due leggerebbero due informazioni diverse.
        Scrivi(@"Include\IT\datis-ad.datis", Righe("This is [STATION_NAME] information [ATIS_LETTER]. [Type of Approach [ARR_TYPE]] [REMARK]"));

        var problema = Assert.Single(Di(Regola.AtisEDatisDiversi));

        Assert.EndsWith("default.atis", problema.File, StringComparison.Ordinal);
        Assert.Contains("datis-ad.datis", problema.Dettaglio, StringComparison.Ordinal);
        Assert.Contains("[METAR]", problema.Dettaglio, StringComparison.Ordinal);
        Assert.Contains("[REMARK]", problema.Dettaglio, StringComparison.Ordinal);
    }

    [Fact]
    public void IlNomeDettoAVoceEIlCpdlcDelSoloDatisNonSonoUnaDifferenza()
    {
        // limc.atis ↔ datis-arrdep.datis sul fork: l'ATIS dice il nome com'è pronunciato («mlpainsa») invece di
        // [STATION_NAME], e [CPDLC] sta solo nel D-ATIS, che è un testo.
        Scrivi(@"Include\IT\default.atis", Righe("This is mlpainsa aitis information [ATIS_LETTER]. [Type of Approach [ARR_TYPE]] . [METAR] ."));
        Scrivi(@"Include\IT\datis-ad.datis", Righe("This is [STATION_NAME] information [ATIS_LETTER]. [Type of Approach [ARR_TYPE]] [METAR] [CPDLC]"));

        Assert.Empty(Di(Regola.AtisEDatisDiversi));
    }

    [Fact]
    public void UnDatisVuotoNonEUnAvviso()
    {
        // datis.datis sul fork: vuoto, citato da 180 posizioni — voluto (solo LIRF e LIMC hanno il D-ATIS).
        Scrivi(@"Include\IT\datis.datis", "");
        Scrivi(@"Include\IT\OTHER\itfreq.frq", Righe(@"LIRF_TWR;118.700;LIRF;;default.atis;1;;datis-ad.datis", @"LIRA_TWR;120.500;LIRA;;default.atis;1;;datis.datis"));

        var problemi = Validatore.ValidaLAlbero(_radice);

        Assert.DoesNotContain(problemi, p => p.Regola == Regola.FileVuoto && p.File.EndsWith("datis.datis", StringComparison.Ordinal));
        Assert.DoesNotContain(problemi, p => p.Regola == Regola.AtisEDatisDiversi);
    }
}
