using Vipi.Sectorfile.Validazione;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Lotto «Subito», slice 11a (carta «file per file» M2): nei <c>.frq</c> un include dopo un escluso (Aurora non lo legge:
/// errore, col riordino proposto — committente, 29 settembre), una posizione italiana citata e mai definita, la stessa
/// posizione due volte nello stesso file.
/// </summary>
public sealed class ControlloDellePosizioniTests : IDisposable
{
    private readonly string _radice = Path.Combine(Path.GetTempPath(), "posizioni-" + Guid.NewGuid().ToString("N"));

    public ControlloDellePosizioniTests()
    {
        Directory.CreateDirectory(Path.Combine(_radice, "Include", "IT", "OTHER"));
        Scrivi("ITALY.isc", Righe("[INFO]", "N041.48.01.000", "E012.14.20.000", "60", "45", "+4.0", "IT", "", "[ATC]", @"F;OTHER\itfreq.frq"));
        Scrivi(@"Include\IT\OTHER\itfreq.frq", Righe(
            @"LIML_TWR;118.100;LIML LIMM -LIMC_MAR_APP -LIMF_WN0_APP LIRO LIVK;PREFS\TWR.cpr;;1;;",
            @"LIMC_MAR_APP;126.750;LIMC LIMM LFMM_S_CTR -LIMM_WN4_CTR;PREFS\APP.cpr;;1;;",
            @"LIMF_WN0_APP;129.275;LIMF LIMA;PREFS\TMA.cpr;;1;;",
            @"LIMF_WN0_APP;129.275;LIMF;PREFS\APP.cpr;;1;;"));
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
    public void UnIncludeDopoUnEsclusoEUnErroreColRiordinoProposto()
    {
        var p = Assert.Single(Di(Regola.IncludeDopoEscluso));
        Assert.Equal(1, p.Riga);
        Assert.Equal(Gravita.Errore, p.Gravita);
        Assert.Contains("LIRO LIVK", p.Dettaglio);
        Assert.Equal(@"LIML_TWR;118.100;LIML LIMM LIRO LIVK -LIMC_MAR_APP -LIMF_WN0_APP;PREFS\TWR.cpr;;1;;", p.Proposta);
    }

    [Fact]
    public void UnaPosizioneItalianaCitataEMaiDefinitaEUnAvviso_UnaStranieraNo()
    {
        var p = Assert.Single(Di(Regola.PosizioneNonDefinita));
        Assert.Equal(2, p.Riga);
        Assert.Equal(Gravita.Avviso, p.Gravita);
        Assert.Contains("LIMM_WN4_CTR", p.Dettaglio);
    }

    [Fact]
    public void LaStessaPosizioneDueVolteNelloStessoFileEUnAvviso()
    {
        var p = Assert.Single(Di(Regola.PosizioneRipetuta));
        Assert.Equal(4, p.Riga);
        Assert.Contains("riga 3", p.Dettaglio);
    }
}
