using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Tests.Modifiche;

/// <summary>
/// La rinomina di un punto (lotto «Subito» slice 7b, «file per file» L2): la dichiarazione, le copie e tutte le righe
/// che lo citano, in una voce sola; le righe che valgono anche per un altro punto le decide l'AOD.
/// </summary>
public sealed class RinominaTests : IDisposable
{
    private const string Prova = "SectorFiles/Include/IT/NAVAIDS/prova.fix";
    private const string Copia = "SectorFiles/Include/IT/NAVAIDS/copia.fix";
    private const string Aerovia = "SectorFiles/Include/IT/AIRWAY/prova.lairway";
    private const string Comune = "SectorFiles/Include/IT/AIRWAY/comune.lairway";
    private const string Attese = "SectorFiles/Include/IT/HOLDENR.hold";
    private const string Sid = "SectorFiles/Include/IT/lirf.sid";

    private readonly AlberoDiProva _albero = new();

    public RinominaTests()
    {
        // Come in ChiLoUsaTests: ITALY.isc carica LUSIL (prova.fix, e la copia a 3 m in copia.fix), LIRR.isc un altro
        // LUSIL lontano; comune.lairway la caricano tutti e due.
        _albero.Scrivi("SectorFiles/ITALY.isc", Isc("""
            [AIRPORT]
            F;OTHER\itap.ap

            [HOLDENR]
            F;HOLDENR.hold

            [FIXES]
            F;NAVAIDS\copia.fix
            F;NAVAIDS\prova.fix

            [LOW AIRWAY]
            F;AIRWAY\prova.lairway
            F;AIRWAY\comune.lairway
            """));
        _albero.Scrivi("SectorFiles/LIRR.isc", Isc("""
            [FIXES]
            F;NAVAIDS\altro.fix

            [LOW AIRWAY]
            F;AIRWAY\comune.lairway
            """));
        _albero.Scrivi(Prova, "LUSIL;N046.02.35.000;E010.07.00.000;1;0;HLD-LUSIL;\r\nTOP;N045.00.00.000;E010.00.00.000;1;0;\r\n");
        _albero.Scrivi(Copia, "LUSIL;N046.02.35.100;E010.07.00.000;1;0;\r\n");
        _albero.Scrivi("SectorFiles/Include/IT/NAVAIDS/altro.fix", "LUSIL;N040.00.00.000;E015.00.00.000;1;0;\r\n");
        _albero.Scrivi(Aerovia, "T;M984;TOP;TOP;\r\nT;M984; LUSIL ;LUSIL;\r\n//T;M999;LUSIL;LUSIL;\r\n");
        _albero.Scrivi(Comune, "T;Z1;TOP;TOP;\r\nT;Z1;lusil;lusil;\r\n");
        _albero.Scrivi(Attese, "HLD-LUSIL;N046.02.35.000;E010.07.00.000;LUSIL/225R-9000;\r\n");
        _albero.Scrivi(Sid, "//@\"LUS1A\" fix=LUSIL\r\nLIRF;07;LUS1A;;;;;1;\r\n");
    }

    public void Dispose() => _albero.Dispose();

    private static string Isc(string sezioni)
        => "[INFO]\r\nN041.48.01.000\r\nE012.14.20.000\r\n60\r\n45\r\n+4.0\r\nIT\r\n\r\n" + sezioni.ReplaceLineEndings("\r\n") + "\r\n";

    private sealed record Aperta(SessioneAperta Sessione, IReadOnlyDictionary<string, CatalogoDeiPunti> Cataloghi, ChiLoUsa Indice, ModificheInSospeso Modifiche)
    {
        public object Prepara(string nuovo, bool? omonimi = null, string file = Prova, int record = 0, string vecchio = "LUSIL")
            => Rinomina.Prepara(Sessione, Indice, Cataloghi, file, record, vecchio, nuovo, omonimi, Modifiche.SporchiDi);

        public object Fai(RinominaPronta pronta)
            => Modifiche.CambiaInPiuFile([.. pronta.PerFile.Select(f => (Sessione.File[f.File], f.Righe))], $"rinomina {pronta.Vecchio} → {pronta.Nuovo}");

        public IReadOnlyList<string> Righe(string file) => ((IFileConRecord)Sessione.File[file]).RigheDelFile(Modifiche.SporchiDi(file));
    }

    private Aperta Apri()
    {
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        return new(sessione, CatalogoDeiPunti.PerOgniIsc(sessione), ChiLoUsa.Di(sessione), new ModificheInSospeso());
    }

    // --- una riga -------------------------------------------------------------------------------------------------

    [Fact]
    public void NeiCampiCambiaSoloIlCampoETieneGliSpazi()
    {
        Assert.Equal("T;M984; LUSIX ;LUSIX;", Rinomina.NeiCampi("T;M984; LUSIL ;LUSIL;", "LUSIL", "LUSIX"));
        Assert.Equal("HLD-LUSIL;N0;E0;LUSIX/225R-9000;", Rinomina.NeiCampi("HLD-LUSIL;N0;E0;LUSIL/225R-9000;", "LUSIL", "LUSIX", attesa: true));
        Assert.Null(Rinomina.NeiCampi("//T;M999;LUSIL;LUSIL;", "LUSIL", "LUSIX"));
        Assert.Null(Rinomina.NeiCampi("T;LUSIL1A;TOP;TOP;", "LUSIL", "LUSIX"));
    }

    [Fact]
    public void NeiTagCambiaIlPuntoEIValoriEIlRecordSoloDoveEDichiarato()
    {
        Assert.Equal("//@\"LUSIX\" note=x", Rinomina.NeiTag("//@\"LUSIL\" note=x", "LUSIL", "LUSIX", dichiarazione: true));
        Assert.Null(Rinomina.NeiTag("//@\"LUSIL\" note=x", "LUSIL", "LUSIX", dichiarazione: false));
        Assert.Equal("//@@\"LUSIX\" alt=5000ft", Rinomina.NeiTag("//@@\"LUSIL\" alt=5000ft", "LUSIL", "LUSIX", dichiarazione: false));
        Assert.Equal("//@\"LUS1A\" fix=LUSIX trans=\"LUSIX\"",
            Rinomina.NeiTag("//@\"LUS1A\" fix=LUSIL trans=\"LUSIL\"", "LUSIL", "LUSIX", dichiarazione: false));
        Assert.Null(Rinomina.NeiTag("//@\"LUS1A\" fix=LUSILA", "LUSIL", "LUSIX", dichiarazione: false));
    }

    [Theory]
    [InlineData("")]
    [InlineData("LU;SIL")]
    [InlineData("LU/SIL")]
    [InlineData("LU\"SIL")]
    [InlineData("N046.02.35.000")]
    public void UnNomeCheNonVaSiRifiuta(string nuovo) => Assert.NotNull(Rinomina.PercheNonVa(nuovo));

    // --- tutto il sector ------------------------------------------------------------------------------------------

    [Fact]
    public void LeRigheCheValgonoAncheAltroveSiChiedono()
    {
        var aperta = Apri();

        // comune.lairway la carica anche LIRR.isc, dove LUSIL è un altro punto: rinominarla glielo toglierebbe.
        var domanda = Assert.IsType<RinominaDaDecidere>(aperta.Prepara("LUSIX"));
        Assert.Equal(1, domanda.Righe);
        Assert.Contains("fuori da ITALY.isc", domanda.AncheA, StringComparison.Ordinal);
    }

    [Fact]
    public void LaRinominaCambiaTuttiESoliIPostiGiusti()
    {
        var aperta = Apri();

        var pronta = Assert.IsType<RinominaPronta>(aperta.Prepara("LUSIX", omonimi: false));

        // Il file del punto per primo; la copia; l'aerovia (non la riga commentata); l'attesa; il tag della SID. Non
        // comune.lairway (lasciata), non altro.fix (un altro LUSIL).
        Assert.Equal([Prova, Aerovia, Attese, Copia, Sid], pronta.PerFile.Select(f => f.File));
        Assert.IsType<ModificaDelTesto>(aperta.Fai(pronta));
        Assert.Equal("LUSIX;N046.02.35.000;E010.07.00.000;1;0;HLD-LUSIL;", aperta.Righe(Prova)[0]);
        Assert.Equal("LUSIX;N046.02.35.100;E010.07.00.000;1;0;", aperta.Righe(Copia)[0]);
        Assert.Equal(["T;M984;TOP;TOP;", "T;M984; LUSIX ;LUSIX;", "//T;M999;LUSIL;LUSIL;"], aperta.Righe(Aerovia));
        Assert.Equal("HLD-LUSIL;N046.02.35.000;E010.07.00.000;LUSIX/225R-9000;", aperta.Righe(Attese)[0]);
        Assert.Equal("//@\"LUS1A\" fix=LUSIX", aperta.Righe(Sid)[0]);
        Assert.Equal("T;Z1;lusil;lusil;", aperta.Righe(Comune)[1]);
    }

    [Fact]
    public void ConLeRigheComuniAncheQuelleCambiano()
    {
        var aperta = Apri();

        var pronta = Assert.IsType<RinominaPronta>(aperta.Prepara("LUSIX", omonimi: true));

        Assert.Contains(Comune, pronta.PerFile.Select(f => f.File));
        aperta.Fai(pronta);
        Assert.Equal("T;Z1;LUSIX;LUSIX;", aperta.Righe(Comune)[1]);
    }

    [Fact]
    public void EUnaVoceSolaESiAnnullaTutta()
    {
        var aperta = Apri();
        var prima = aperta.Sessione.File.Keys.Where(f => aperta.Sessione.File[f] is IFileConRecord).ToDictionary(f => f, aperta.Righe);

        aperta.Fai((RinominaPronta)aperta.Prepara("LUSIX", omonimi: false));

        Assert.Equal(1, aperta.Modifiche.Quante);
        var voce = Assert.Single(aperta.Modifiche.Voci);
        Assert.Equal(Prova, voce.File);
        Assert.Equal(4, aperta.Modifiche.PartiDi(voce).Count);
        Assert.Contains("rinomina LUSIL → LUSIX", voce.Descrizione, StringComparison.Ordinal);

        // Annullare una parte annulla la voce: tutti i file tornano com'erano.
        var parte = aperta.Modifiche.PartiDi(voce)[0];
        Assert.True(aperta.Modifiche.Annulla(aperta.Sessione.File[parte.File], parte, f => aperta.Sessione.File.GetValueOrDefault(f)));
        Assert.False(aperta.Modifiche.CEQualcosa);
        Assert.All(prima, f => Assert.Equal(f.Value, aperta.Righe(f.Key)));
    }

    [Fact]
    public void UnNomeCheCEGiaSiRifiuta()
    {
        var rifiuto = Assert.IsType<ModificaRifiutata>(Apri().Prepara("TOP", omonimi: false));
        Assert.Contains("C'è già", rifiuto.Motivo, StringComparison.Ordinal);
    }

    [Fact]
    public void LeMaiuscoleSiCambianoSenzaDireCheCEGia()
        => Assert.IsType<RinominaPronta>(Apri().Prepara("Lusil", omonimi: false));

    [Fact]
    public void UnAttesaSiRinominaColFixCheCiRimanda()
    {
        var aperta = Apri();

        var pronta = Assert.IsType<RinominaPronta>(aperta.Prepara("HLD-LUSIX", file: Attese, vecchio: "HLD-LUSIL"));
        aperta.Fai(pronta);

        Assert.Equal("HLD-LUSIX;N046.02.35.000;E010.07.00.000;LUSIL/225R-9000;", aperta.Righe(Attese)[0]);
        Assert.Equal("LUSIL;N046.02.35.000;E010.07.00.000;1;0;HLD-LUSIX;", aperta.Righe(Prova)[0]);
    }
}
