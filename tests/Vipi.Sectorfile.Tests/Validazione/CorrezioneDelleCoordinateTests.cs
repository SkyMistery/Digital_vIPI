using Vipi.Sectorfile.Validazione;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Lotto «Subito», slice 2c: le coordinate scritte male e la correzione proposta (carta «file per file» G3, L4, Q6, R3,
/// F4). Tutti i casi sono righe vere del fork del 27 settembre 2026.
/// </summary>
public sealed class CorrezioneDelleCoordinateTests : IDisposable
{
    private readonly string _cartella = Path.Combine(Path.GetTempPath(), "correzioni-" + Guid.NewGuid().ToString("N"));

    public CorrezioneDelleCoordinateTests() => Directory.CreateDirectory(_cartella);

    public void Dispose() => Directory.Delete(_cartella, recursive: true);

    [Theory]
    [InlineData("n045.44.52.080", "N045.44.52.080")]      // itvor.vor:125
    [InlineData("E12.30.18.200", "E012.30.18.200")]       // lirz.gts, 12 righe
    [InlineData("N44.18.55.72", "N044.18.55.720")]        // limm.rw, lipp.rw, lirr.rw
    [InlineData("E008-11.31.443", "E008.11.31.443")]      // APT.fix:294
    [InlineData("N043.49.49.00", "N043.49.49.000")]       // lipp.hartcc, lirr.hartcc, FRA.artcc
    [InlineData("E015.37.07.1000", "E015.37.07.100")]     // libf.str, licc.str, lipk.str
    [InlineData("N046.34.25.8735", "N046.34.25.874")]     // lovv.tfl:70, arrotondata al millesimo
    [InlineData("E010.34.072.00", "E010.34.07.200")]      // MIL.fix:96, il punto scivolato
    [InlineData("E017.04.60.000", "E017.05.00.000")]      // lovv.tfl:48, 60 secondi = il minuto dopo
    [InlineData("N047.25.60.000", "N047.26.00.000")]      // lipp.hartcc:2047
    [InlineData("E103441000", "E0103441000")]             // lipx.vfi:7, come lo legge già il motore
    [InlineData("N041131620", "N0411316200")]             // MIL.fix:14: i gradi ci sono, manca una cifra in fondo
    [InlineData("N041.48.01.000", null)]                  // già giusta
    [InlineData("N0414801000", null)]                     // già giusta, compatta
    [InlineData("N047.44.75.000", null)]                  // itvor.vor:109: 75 secondi, non si sa cosa si voleva
    [InlineData("E01221856000", null)]                    // lirf.vfi:6: una cifra in più, non si sa quale
    [InlineData("BULL", null)]
    public void IlTokenScrittoGiusto(string token, string? giusto)
        => Assert.Equal(giusto, CorrezioneDelleCoordinate.DelToken(token));

    [Theory]
    [InlineData("N038.55.55.424 E016.36.08.523;N038.55.53.716;E016.36.15.757;PROHIBIT;P154;", "prohibit",
        "N038.55.55.424;E016.36.08.523;N038.55.53.716;E016.36.15.757;PROHIBIT;P154;")]
    [InlineData("N042.56.13.000;E011.10.10.000 N043.16.53.000;E010.56.32.000;RESTRICT;R107A;", "restrict",
        "N042.56.13.000;E011.10.10.000;N043.16.53.000;E010.56.32.000;RESTRICT;R107A;")]
    [InlineData("ALPHA;40.98618505;13.75008401;0;0;", "fix", "ALPHA;N040.59.10.266;E013.45.00.302;0;0;")]
    [InlineData("A;41.00850773;16.07432896;", "txi", null)]
    [InlineData("101;LIRZ;E12.30.18.200;N043.05.35.000;", "gts", "101;LIRZ;E012.30.18.200;N043.05.35.000;")]
    [InlineData("//BULL;n041.50.00.000;E012.10.00.000;", "fix", null)]
    public void LaRigaScrittaGiusta(string riga, string estensione, string? giusta)
        => Assert.Equal(giusta, CorrezioneDelleCoordinate.DellaRiga(riga, estensione));

    [Fact]
    public void IProblemiDiCoordinatePortanoLaRigaCorretta()
    {
        string[] righe =
        [
            "GRO;n045.44.52.080;E010.20.09.000;0;0;",
            "BULL;N041.50.00.000;E008-11.31.443;0;0;",
            "RNNE1;N40.59.33.000;E014.20.00.000;0;0;",
            "PXSW1;N045.20.00.000;E103441000;0;0;",
            "LANDO;N041.50.00.000;E01221856000;0;0;",
            "BV-VICTOR;N041131620;E0163231800;3;",
            "TRE;N047.44.75.000;E010.20.99.000;0;0;",
        ];
        string file = Path.Combine(_cartella, "prova.fix");
        File.WriteAllText(file, string.Join("\r\n", righe) + "\r\n");

        var problemi = Validatore.ValidaIlFile(file, "prova.fix");

        Assert.Equal("GRO;N045.44.52.080;E010.20.09.000;0;0;", Assert.Single(problemi, p => p.Regola == Regola.EmisferoMinuscolo).Proposta);
        Assert.Equal("BULL;N041.50.00.000;E008.11.31.443;0;0;", Assert.Single(problemi, p => p.Regola == Regola.CoordinataIllegibile).Proposta);
        var fuoriForma = problemi.Where(p => p.Regola == Regola.CoordinataFuoriForma).ToList();
        Assert.Equal(["RNNE1;N040.59.33.000;E014.20.00.000;0;0;", "PXSW1;N045.20.00.000;E0103441000;0;0;"], fuoriForma.Select(p => p.Proposta));
        Assert.All(fuoriForma, p => Assert.Equal(Gravita.Avviso, p.Gravita));
        // Letta altrove: con una cifra in più (in Asia) nessuna proposta; con una in meno in fondo (in Africa) sì.
        var altrove = problemi.Where(p => p.Regola == Regola.CoordinataLettaAltrove).ToList();
        Assert.All(altrove, p => Assert.Equal(Gravita.Errore, p.Gravita));
        Assert.Equal([(5, (string?)null), (6, "BV-VICTOR;N0411316200;E0163231800;3;")], altrove.Select(p => (p.Riga, p.Proposta)));
        Assert.Contains("si legge E122.18.56.000", altrove[0].Dettaglio, StringComparison.Ordinal);
        Assert.Contains("si legge N004.11.31.620", altrove[1].Dettaglio, StringComparison.Ordinal);
        Assert.All(problemi.Where(p => p.Riga == 7), p => Assert.Null(p.Proposta));

        // Scritta la proposta, la riga non ha più problemi di coordinate.
        File.WriteAllText(file, string.Join("\r\n", problemi.Where(p => p.Proposta is not null).Select(p => p.Proposta).Distinct()) + "\r\n");
        Assert.Empty(Validatore.ValidaIlFile(file, "prova.fix"));
    }
}
