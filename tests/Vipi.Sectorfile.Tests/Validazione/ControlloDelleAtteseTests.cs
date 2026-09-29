using Vipi.Sectorfile.Validazione;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Lotto «Subito», slice 10b (carta «file per file» L3, L4, U1): le attese in rotta legate ai fix, VOR e NDB che le
/// citano, nei due versi; i master che non caricano <c>[HOLDENR]</c>; VOR e NDB con lo stesso nome lontani; il tipo di
/// un fix mancante o fuori elenco. Misure del fork nella carta del lotto (§6, slice 10).
/// </summary>
public sealed class ControlloDelleAtteseTests : IDisposable
{
    private readonly string _radice = Path.Combine(Path.GetTempPath(), "attese-" + Guid.NewGuid().ToString("N"));

    public ControlloDelleAtteseTests()
    {
        string it = Path.Combine(_radice, "Include", "IT");
        Directory.CreateDirectory(Path.Combine(it, "NAVAIDS"));
        Scrivi("ITALY.isc", Isc("[HOLDENR]", @"F;HOLDENR.hold", "[VOR]", @"F;NAVAIDS\itvor.vor", "[NDB]", @"F;NAVAIDS\itndb.ndb",
            "[FIXES]", @"F;NAVAIDS\itfix.fix"));
        // Il master di FIR carica i fix, che citano le attese, ma non [HOLDENR] (come LIBB, LIMM, LIPP e LIRR del fork).
        Scrivi("LIRR.isc", Isc("[FIXES]", @"F;NAVAIDS\itfix.fix"));
        Scrivi(@"Include\IT\NAVAIDS\itfix.fix", Righe(
            "ABBOZ;N046.02.37.000;E011.07.48.000;1;0;HLD-ABBOZ;",
            "EKLAP;N045.00.07.000;E011.37.49.000;0;0;HLD-ELKAP;",
            "ELKAP;N042.43.16.000;E010.38.39.000;2;1;",
            "POE1;N045.28.15.000;E010.28.20.000;",
            "MG763;N044.03.11.145;E008.11.31.443;3:;"));
        Scrivi(@"Include\IT\NAVAIDS\itvor.vor", Righe(
            "PIS;112.10;N043.40.00.000;E010.23.00.000;",
            "OST;114.90;N041.48.13.600;E012.14.15.100;;;;HLD-OST;"));
        Scrivi(@"Include\IT\NAVAIDS\itndb.ndb", Righe(
            "PIS;379.0;N043.33.20.000;E010.20.00.000;",
            "OST;321.0;N041.48.14.000;E012.14.15.000;"));
        Scrivi(@"Include\IT\HOLDENR.hold", Righe(
            "HLD-ABBOZ;N046.02.37.000;E011.07.48.000;ABBOZ/225R-9000;",
            "HLD-EKLAP;N045.00.07.000;E011.37.49.000;ELKAP/090R-FL190;",
            "HLD-OST;N041.48.13.600;E012.14.15.100;OST/150L-FL100;"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_radice))
            Directory.Delete(_radice, recursive: true);
    }

    private static string Righe(params string[] righe) => string.Join("\r\n", righe) + "\r\n";

    private static string Isc(params string[] sezioni)
        => Righe(["[INFO]", "N041.48.01.000", "E012.14.20.000", "60", "45", "+4.0", "IT", "", .. sezioni]);

    // La CI gira su Ubuntu: le barre del percorso si mettono quelle del sistema.
    private void Scrivi(string relativo, string testo)
        => File.WriteAllText(Path.Combine(_radice, relativo.Replace('\\', Path.DirectorySeparatorChar)), testo);

    private List<ProblemaDelSector> Di(Regola regola) => Validatore.ValidaLAlbero(_radice).Where(p => p.Regola == regola).ToList();

    [Fact]
    public void UnaAttesaCitataENonDefinitaEUnErroreCheProponeQuellaColNomeDelFix()
    {
        var p = Assert.Single(Di(Regola.AttesaNonDefinita));
        Assert.Equal(Path.Combine("Include", "IT", "NAVAIDS", "itfix.fix"), p.File);
        Assert.Equal(2, p.Riga);
        Assert.Equal(Gravita.Errore, p.Gravita);
        Assert.Contains("HLD-ELKAP", p.Dettaglio);
        Assert.Contains("HLD-EKLAP", p.Dettaglio);
    }

    [Fact]
    public void UnaAttesaCheNessunoCitaEUnAvviso()
    {
        var p = Assert.Single(Di(Regola.AttesaMaiCitata));
        Assert.Equal(Path.Combine("Include", "IT", "HOLDENR.hold"), p.File);
        Assert.Equal(2, p.Riga);
        Assert.Equal(Gravita.Avviso, p.Gravita);
        Assert.Contains("EKLAP", p.Dettaglio);
    }

    [Fact]
    public void LInfoCheNominaUnFixLontanoDallAttesaEUnAvviso()
    {
        var p = Assert.Single(Di(Regola.AttesaFuoriPosto));
        Assert.Equal(Path.Combine("Include", "IT", "HOLDENR.hold"), p.File);
        Assert.Equal(2, p.Riga);
        Assert.Contains("ELKAP", p.Dettaglio);
        Assert.Contains("NM", p.Dettaglio);
    }

    [Fact]
    public void UnMasterCheNonCaricaLeAtteseCitateDaiSuoiFixHaUnAvvisoSolo()
    {
        var p = Assert.Single(Di(Regola.AtteseNonCaricate));
        Assert.Equal("LIRR.isc", p.File);
        Assert.Equal(Gravita.Avviso, p.Gravita);
        Assert.Contains("2 attese", p.Dettaglio);
    }

    [Fact]
    public void UnVorEUnNdbColloStessoNomeLontaniSonoUnAvviso_NelloStessoPuntoNo()
    {
        var p = Assert.Single(Di(Regola.NomeInPiuCataloghi));
        Assert.Equal(Path.Combine("Include", "IT", "NAVAIDS", "itndb.ndb"), p.File);
        Assert.Equal(1, p.Riga);
        Assert.Equal(Gravita.Avviso, p.Gravita);
        Assert.Contains("VOR «PIS»", p.Dettaglio);
    }

    [Fact]
    public void UnFixSenzaTipoOColTipoFuoriElencoEUnAvviso()
    {
        var manca = Assert.Single(Di(Regola.CampoMancante));
        Assert.Equal(4, manca.Riga);
        Assert.Equal(Gravita.Avviso, manca.Gravita);

        var fuori = Assert.Single(Di(Regola.ValoreFuoriElenco));
        Assert.Equal(5, fuori.Riga);
        Assert.Contains("«3:»", fuori.Dettaglio);
    }
}
