using Vipi.Sectorfile.Validazione;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Carta F2, slice 8: il validatore, regola per regola. Gli esempi sono quelli veri del sector (master <c>7e761aa</c>);
/// sui campioni si controlla che trovi gli errori noti e non ne inventi.
/// </summary>
public sealed class ValidatoreTests : IDisposable
{
    private readonly string _cartella = Path.Combine(Path.GetTempPath(), "validatore-" + Guid.NewGuid().ToString("N"));

    public ValidatoreTests() => Directory.CreateDirectory(_cartella);

    public void Dispose() => Directory.Delete(_cartella, recursive: true);

    private IReadOnlyList<ProblemaDelSector> Valida(string nome, params string[] righe)
    {
        string percorso = Path.Combine(_cartella, nome);
        File.WriteAllText(percorso, string.Join("\r\n", righe) + "\r\n");
        return Validatore.ValidaIlFile(percorso, nome);
    }

    // itvor.vor: gli errori veri del §1, e l'emisfero minuscolo che la slice 2 ha reso leggibile. La riga 81 (`GRO;;`)
    // era «campo 2 vuoto» fino alla slice 9: è il TACAN di Grosseto, un canale senza frequenza, e si legge.
    [Fact]
    public void IlVorVeroHaGliErroriNoti()
    {
        var problemi = Validatore.ValidaIlFile(RealSectorFiles.Path("NAVAIDS/itvor.vor")!, "NAVAIDS/itvor.vor");

        Assert.Equal(
            new[]
            {
                (Regola.CoordinataFuoriCampo, 109), (Regola.CoordinataFuoriCampo, 109),
                (Regola.EmisferoMinuscolo, 125),
            },
            problemi.Select(p => (p.Regola, p.Riga)));
        Assert.Contains(problemi, p => p.Dettaglio == "«N047.44.75.000»: secondi 75");
        Assert.Equal(Gravita.Avviso, problemi[^1].Gravita);
    }

    [Fact]
    public void UnCampoObbligatorioVuotoSiDiceCampoVuoto()
    {
        var problemi = Valida("ENR.fix", "ABC;;E011.04.38.600;3;");

        var problema = Assert.Single(problemi);
        Assert.Equal((Regola.CampoVuoto, "campo 2 vuoto"), (problema.Regola, problema.Dettaglio));
    }

    // Il refuso del committente (24 settembre, lirn.str «LIRN ATZ»): il primo punto a N041 invece di N040, e la mappa
    // che doveva chiudersi resta aperta di 60 NM. Una cifra sola → avviso sull'intestazione.
    [Fact]
    public void UnaFormaChiusaPerUnaCifraEQuasiChiusa()
    {
        var problemi = Valida("lirn.str",
            "LIRN;MAPS;LIRN ATZ; ; ;5;",
            "N041.52.31.000;E014.07.36.000;",
            "N041.00.28.000;E014.15.47.000;",
            "N040.47.05.000;E014.20.47.000;",
            "N040.52.31.000;E014.07.36.000;");

        var problema = Assert.Single(problemi, p => p.Regola == Regola.FormaQuasiChiusa);
        Assert.Equal((1, Gravita.Avviso), (problema.Riga, problema.Gravita));
        Assert.Contains("una sola cifra", problema.Dettaglio);
        Assert.StartsWith("LIRN ATZ:", problema.Dettaglio, StringComparison.Ordinal);
    }

    [Theory]
    // chiusa davvero
    [InlineData("N040.52.31.000;E014.07.36.000;")]
    // un arrotondamento: 2 m (sul fork 1 598 forme così, chiuse a vista)
    [InlineData("N040.52.31.030;E014.07.36.000;")]
    // aperta di proposito: un punto qualunque
    [InlineData("N040.45.34.000;E014.17.35.000;")]
    public void NonEQuasiChiusaSeChiusaArrotondataOLontana(string ultimo)
        => Assert.DoesNotContain(
            Valida("lirn.str", "LIRN;MAPS;LIRN ATZ; ; ;5;", "N040.52.31.000;E014.07.36.000;", "N041.00.28.000;E014.15.47.000;",
                   "N040.47.05.000;E014.20.47.000;", ultimo),
            p => p.Regola == Regola.FormaQuasiChiusa);

    [Fact]
    public void AncheUnSettoreDelTflSiControlla()
    {
        var problemi = Valida("twrs.tfl",
            "LIRN_TWR;TWR;1;TWR;1;",
            "N040.52.31.000;E014.07.36.000;",
            "N041.00.28.000;E014.15.47.000;",
            "N040.47.05.000;E014.20.47.000;",
            "N040.52.31.000;E015.07.36.000;");

        Assert.Contains(problemi, p => p.Regola == Regola.FormaQuasiChiusa && p.Dettaglio.StartsWith("LIRN_TWR:", StringComparison.Ordinal));
    }

    // La frequenza vuota di un .vor è un TACAN (slice 9): nessun problema.
    [Fact]
    public void UnTacanSenzaFrequenzaNonEUnProblema()
        => Assert.Empty(Valida("itvor.vor", "GRO;;N042.45.37.200;E011.04.38.600;0;3;35Y"));

    [Fact]
    public void UnFixColTrattinoEUnaCoordinataIllegibile()
    {
        var problemi = Validatore.ValidaIlFile(RealSectorFiles.Path("NAVAIDS/APT.fix")!, "NAVAIDS/APT.fix");

        var problema = Assert.Single(problemi);
        Assert.Equal((Regola.CoordinataIllegibile, 294), (problema.Regola, problema.Riga));
    }

    // Lo spazio al posto del `;` (slice 6): una regola sola, non anche «riga illeggibile».
    [Fact]
    public void LoSpazioAlPostoDelPuntoEVirgolaSiDiceUnaVolta()
    {
        var problemi = Valida("italy.prohibit", "N038.55.55.424 E016.36.08.523;N038.55.53.716;E016.36.15.757;PROHIBIT;P154;");

        Assert.Equal(Regola.SeparatoreSbagliato, Assert.Single(problemi).Regola);
    }

    // Le aerovie si chiamano N503, W36-Z636, S1: nomi, non coordinate (132 falsi errori nella prima misura).
    [Fact]
    public void IlNomeDiUnAeroviaNonEUnaCoordinata()
    {
        Assert.Empty(Valida("itawlow.lairway", "L;N503;N045.00.00.000;E010.00.00.000;", "T;W36-Z636;BRADA;BRADA;", "T;S1;BRADA;BRADA;"));
    }

    [Theory]
    [InlineData("lirr.hartcc", "T;RR;N043.49.49.00;E011.00.00.000;", Regola.FrazioneAmbigua)]
    [InlineData("lipp.hartcc", "T;PP CE;N047.25.60.000;E009.38.40.556;", Regola.CoordinataFuoriCampo)]
    [InlineData("lirf.geo", "N041.00.00.000;12.50000000;N041.00.01.000;E012.00.01.000;COAST;", Regola.DmsEDecimaleMescolati)]
    [InlineData("liba.str", "LIBA;MAPS;ZONA; ; ;0;", Regola.CoppiaDecimale, "41.00850773;16.07432896;")]
    public void UnaRegolaDeiCampi(string file, string riga, Regola attesa, string? seconda = null)
    {
        var problemi = seconda is null ? Valida(file, riga) : Valida(file, riga, seconda);

        var problema = Assert.Single(problemi);
        Assert.Equal(attesa, problema.Regola);
    }

    // I .txi sono decimali per natura: lì una coppia decimale è la forma del file.
    [Fact]
    public void NeiTxiLaCoppiaDecimaleEDiCasa()
    {
        Assert.DoesNotContain(Validatore.ValidaIlFile(RealSectorFiles.Path("lirf.txi")!, "lirf.txi"), p => p.Regola == Regola.CoppiaDecimale);
    }

    // Il refuso di lied.str (`ALPHA SOUTH;ALPHA SUOTH`): la riga esatta, non quella dell'intestazione.
    [Fact]
    public void DueNomiDiversiSulLaLoroRiga()
    {
        var problemi = Valida("x.tfl", "LIXX_CTR;CTR;1;CTR;0;", "AMSOR;AMSOR;", "ALPHA SOUTH;ALPHA SUOTH;", "N041.00.00.000;E012.00.00.000;");

        var problema = Assert.Single(problemi);
        Assert.Equal((Regola.DueNomiDiversi, 3, Gravita.Avviso), (problema.Regola, problema.Riga, problema.Gravita));
    }

    [Fact]
    public void UnSettoreConDueVerticiNonEUnPoligono()
    {
        var problemi = Valida("x.tfl", "LIXX_CTR;CTR;1;CTR;0;", "ABREG;ABREG;", "N046.10.53.000;E009.11.40.000;");

        Assert.Equal((Regola.PoligonoConPochiVertici, 1), (Assert.Single(problemi).Regola, problemi[0].Riga));
    }

    [Fact]
    public void ITagRottiSonoErroriEQuelliFuoriCatalogoAvvisi()
    {
        var problemi = Valida("lirf.sid", "//@BANA6W initialclimb=5000", "LIRF;25;SOSA5A;;;;;1;", "//@XIBR5A colore=rosso", "LIRF;25;XIBR5A;;;;;1;");

        Assert.Equal(new[] { (Regola.TagNonValido, 1, Gravita.Errore), (Regola.TagFuoriCatalogo, 3, Gravita.Avviso) },
            problemi.Select(p => (p.Regola, p.Riga, p.Gravita)));
    }

    // lipp.hartcc:2047: A ne faceva in silenzio un vertice «per nome» chiamato N047.25.60.000. Ora il lettore lo dice.
    [Fact]
    public void IlLettoreDiceIlVerticeTCheNonSiLegge()
    {
        string percorso = Path.Combine(_cartella, "lipp.hartcc");
        File.WriteAllText(percorso, "T;PP CE;N047.25.22.020;E009.38.59.337;\r\nT;PP CE;N047.25.60.000;E009.38.40.556;\r\nT;PP CE;NILTO;NILTO;\r\n");
        var avvisi = new CollectingWarnings();

        new HartccParser(avvisi).Parse(percorso, new Vipi.Sectorfile.Shared.ColorPalette());

        var avviso = Assert.Single(avvisi.Snapshot());
        Assert.Equal(("Unparseable T; vertex", 2), (avviso.Message, avviso.LineNumber));
    }

    [Fact]
    public void IFileCheIlMotoreNonInterpretaNonSiGuardano()
    {
        Assert.Empty(Valida("note.txt", "N047.44.75.000;;;"));
    }

    // Campioni puliti: il validatore non inventa errori (solo avvisi dove il sector ne ha davvero).
    [Theory]
    [InlineData("lirf.sid")]
    [InlineData("lirf.str")]
    [InlineData("DYNAMIC_SEC/limmfic.tfl")]
    [InlineData("OTHER/itap.ap")]
    [InlineData("AIRWAY/itawhigh.hairway")]
    [InlineData("HOLDENR.hold")]
    public void UnCampionePulitoNonHaErrori(string campione)
    {
        Assert.DoesNotContain(Validatore.ValidaIlFile(RealSectorFiles.Path(campione)!, campione), p => p.Gravita == Gravita.Errore);
    }
}
