using Vipi.Sectorfile.Validazione;
using Xunit;

namespace Vipi.Sectorfile.Validazione.Tests;

/// <summary>
/// Lotto «Subito», slice 16b (carta «file per file» F4): i punti VFR dei <c>.vfi</c> contro i loro fix nascosti
/// (<c>NAVAIDS/VFR_NASCOSTI.fix</c>, col codice per nome) — gemello che manca o sta altrove, fix senza punto, codice
/// ripetuto, codice scritto nel campo sbagliato — e le rotte militari a metà dei <c>.vrt</c>.
/// </summary>
public sealed class ControlloDeiVfrTests : IDisposable
{
    private readonly string _radice = Path.Combine(Path.GetTempPath(), "vfr-" + Guid.NewGuid().ToString("N"));

    public ControlloDeiVfrTests()
    {
        Directory.CreateDirectory(Path.Combine(_radice, "Include", "IT", "NAVAIDS"));
        Scrivi("ITALY.isc", Righe("[INFO]", "N041.48.01.000", "E012.14.20.000", "60", "45", "+4.0", "IT", "",
            "[FIXES]", @"F;NAVAIDS\VFR_NASCOSTI.fix"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_radice))
            Directory.Delete(_radice, recursive: true);
    }

    private static string Righe(params string[] righe) => string.Join("\r\n", righe) + "\r\n";

    private void Scrivi(string relativo, string testo)
        => File.WriteAllText(Path.Combine(_radice, relativo.Replace('\\', Path.DirectorySeparatorChar)), testo);

    private void Nascosti(params string[] righe) => Scrivi(@"Include\IT\NAVAIDS\VFR_NASCOSTI.fix", Righe(righe));

    private List<ProblemaDelSector> Di(Regola regola) => Validatore.ValidaLAlbero(_radice).Where(p => p.Regola == regola).ToList();

    [Fact]
    public void UnPuntoColCodiceVuoleIlSuoFixNascosto_NellaStessaPosizione()
    {
        Scrivi(@"Include\IT\lizz.vfi", Righe(
            "ALFA;ZZN1;N0410000000;E0120000000;",       // gemello uguale, scritto in un'altra forma
            "BRAVO;ZZN2;N0411000000;E0120000000;",      // senza gemello
            "CHARLIE;ZZS1;N0412000000;E0120000000;",    // gemello a un primo di latitudine
            "DELTA;ZZ;N0413000000;E0120000000;",        // il 2° campo non è un codice: non chiede un gemello
            "ECHO;2500;N0414000000;E0120000000;"));
        Nascosti(
            "ZZN1;N041.00.00.000;E012.00.00.000;3;",
            "ZZS1;N041.21.00.000;E012.00.00.000;3;",
            "ZZW9;N041.50.00.000;E012.00.00.000;3;");

        var manca = Assert.Single(Di(Regola.GemelloVfrMancante));
        Assert.Equal((2, "BRAVO;ZZN2;N0411000000;E0120000000;"), (manca.Riga, manca.Testo));
        Assert.EndsWith("lizz.vfi", manca.File, StringComparison.Ordinal);
        Assert.Contains("ZZN2", manca.Dettaglio, StringComparison.Ordinal);
        Assert.Equal(Gravita.Avviso, manca.Gravita);

        var diverso = Assert.Single(Di(Regola.GemelloVfrDiverso));
        Assert.Equal(3, diverso.Riga);
        Assert.Contains("VFR_NASCOSTI.fix:2", diverso.Dettaglio, StringComparison.Ordinal);
        Assert.Contains("1 NM", diverso.Dettaglio, StringComparison.Ordinal);

        var orfano = Assert.Single(Di(Regola.FixNascostoSenzaPunto));
        Assert.Equal((3, "ZZW9;N041.50.00.000;E012.00.00.000;3;"), (orfano.Riga, orfano.Testo));
        Assert.EndsWith("VFR_NASCOSTI.fix", orfano.File, StringComparison.Ordinal);
    }

    [Fact]
    public void SenzaIlFileDeiNascostiNonSiChiedonoGemelli()
    {
        // Un sector che non segue la convenzione italiana: il 2° campo è la quota, come nel manuale.
        File.Delete(Path.Combine(_radice, "Include", "IT", "NAVAIDS", "VFR_NASCOSTI.fix"));
        Scrivi(@"Include\IT\lizz.vfi", Righe("ALFA;ZZN1;N0410000000;E0120000000;"));

        Assert.Empty(Di(Regola.GemelloVfrMancante));
    }

    [Fact]
    public void LoStessoCodiceSuDuePuntiEUnAvvisoSulSecondo()
    {
        // limj.vfi: PASSO DEL TURCHINO e ROSSIGLIONE, tutti e due MJNW1.
        Scrivi(@"Include\IT\lizz.vfi", Righe("ALFA;ZZN1;N0410000000;E0120000000;", "BRAVO;ZZN1;N0411000000;E0120000000;"));
        Scrivi(@"Include\IT\liyy.vfi", Righe("CHARLIE;ZZN1;N0410000000;E0120000000;"));
        Nascosti("ZZN1;N041.00.00.000;E012.00.00.000;3;");

        var problemi = Di(Regola.CodiceVfrRipetuto);

        Assert.Equal([("liyy.vfi", 1), ("lizz.vfi", 2)], problemi.Select(p => (Path.GetFileName(p.File), p.Riga)).Order());
        Assert.All(problemi, p => Assert.Contains("lizz.vfi:1", p.Dettaglio, StringComparison.Ordinal));
        // Con un codice su più punti non si sa quale va col fix: del gemello non si dice niente.
        Assert.Empty(Di(Regola.GemelloVfrDiverso));
    }

    [Fact]
    public void IlCodiceNelCampoSbagliatoHaLaRigaCorretta()
    {
        Scrivi(@"Include\IT\lizz.vfi", Righe(
            "ZZSW1;CONEGLIANO;N0410000000;E0120000000;",            // nome e codice scambiati (lipa.vfi)
            "MAZARA DEL VALLOZZSE3;;N0411000000;E0120000000;",      // manca il «;» fra nome e codice (lict.vfi)
            "CAORLE - ZZE2;;N0412000000;E0120000000;",              // il codice in coda al nome (liph.vfi)
            "SENZA NIENTE;;N0413000000;E0120000000;",               // nessun codice da nessuna parte: non si inventa
            "IP31;ZZ;N0414000000;E0120000000;"));                   // un punto che si chiama IP31 (lipl.vfi): non è un codice di qui
        Nascosti(
            "ZZSW1;N041.00.00.000;E012.00.00.000;3;",
            "ZZSE3;N041.10.00.000;E012.00.00.000;3;",
            "ZZE2;N041.20.00.000;E012.00.00.000;3;");

        var problemi = Di(Regola.CodiceVfrFuoriPosto);

        Assert.Equal([1, 2, 3], problemi.Select(p => p.Riga));
        Assert.Equal(
            ["CONEGLIANO;ZZSW1;N0410000000;E0120000000;", "MAZARA DEL VALLO;ZZSE3;N0411000000;E0120000000;", "CAORLE;ZZE2;N0412000000;E0120000000;"],
            problemi.Select(p => p.Proposta));
        // I tre fix hanno il loro punto, anche se scritto male: non sono orfani.
        Assert.Empty(Di(Regola.FixNascostoSenzaPunto));
        Assert.Empty(Di(Regola.GemelloVfrMancante));
    }

    [Fact]
    public void UnaRottaMilitareSoloSuAlcuneRigheEUnAvviso()
    {
        Scrivi(@"Include\IT\lizz.vfi", Righe("ALFA;ZZN1;N0410000000;E0120000000;", "BRAVO;ZZN2;N0411000000;E0120000000;", "CHARLIE;ZZN3;N0412000000;E0120000000;"));
        Scrivi(@"Include\IT\lizz.vrt", Righe(
            "1;ALFA;ALFA;;1;", "1;BRAVO;BRAVO;;1;", "",
            "2;ALFA;ALFA;;1;", "2;BRAVO;BRAVO;", "2;CHARLIE;CHARLIE;;1;", "",
            "3;ALFA;ALFA;", "3;CHARLIE;CHARLIE;;0;"));

        var problema = Assert.Single(Di(Regola.RottaMilitareAMeta));

        Assert.Equal((5, "2;BRAVO;BRAVO;", "2;BRAVO;BRAVO;;1;"), (problema.Riga, problema.Testo, problema.Proposta));
    }
}
