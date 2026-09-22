using Vipi.Sectorfile.IO;
using Forma = Vipi.Sectorfile.IO.FormaDelPunto.Forma;

namespace Vipi.Sectorfile.Tests.IO;

/// <summary>
/// «Riga come campi» (carta F2 §9.5, slice 3). Le righe sono quelle vere del sector su cui gli scrittori di A
/// perdevano dati (misura «tutto toccato» del 22 settembre 2026): grezza, base (ciò che lo scrittore produce
/// dal record appena letto) e nuova (ciò che produce dal record modificato).
/// </summary>
public sealed class FusioneDelRecordTests
{
    private static List<string> Unisci(string[] grezze, string[] base_, string[] nuove, Forma forma = Forma.Puntata)
        => FusioneDelRecord.Unisci(grezze, base_, nuove, forma);

    [Fact]
    public void ToccatoMaNonCambiatoEsceIdentico()
    {
        var grezze = new[] { "LIZZ;BULL;LEVIS; ; ;3;", "//nota", "T;DUMMY;N000.00.00.000;E000.00.00.000;" };
        var base_ = new[] { "LIZZ;BULL;LEVIS;;;3;" };

        Assert.Equal(grezze, Unisci(grezze, base_, base_));
    }

    // NAVAIDS/itvor.vor: A perdeva il canale TACAN «54Y» appena il VOR veniva toccato.
    [Fact]
    public void UnCampoInCodaCheIlModelloNonConosceResta()
        => Assert.Equal(
            new[] { "AEA;111.65;N040.38.18.400;E008.17.30.400;0;2;54Y;" },
            Unisci(
                new[] { "AEA;111.65;N040.38.17.400;E008.17.30.400;0;2;54Y;" },
                new[] { "AEA;111.65;N040.38.17.400;E008.17.30.400;0;2;" },
                new[] { "AEA;111.65;N040.38.18.400;E008.17.30.400;0;2;" }));

    // NAVAIDS/itfix.fix: il rimando all'attesa «HLD-ABBOZ» si perdeva.
    [Fact]
    public void IlRimandoAllAttesaResta()
        => Assert.Equal(
            new[] { "ABBOZ;N046.02.37.000;E011.07.49.000;1;0;HLD-ABBOZ;" },
            Unisci(
                new[] { "ABBOZ;N046.02.37.000;E011.07.48.000;1;0;HLD-ABBOZ;" },
                new[] { "ABBOZ;N046.02.37.000;E011.07.48.000;1;0;" },
                new[] { "ABBOZ;N046.02.37.000;E011.07.49.000;1;0;" }));

    // OTHER/itrw.rw: «095» diventava «95» anche se nessuno toccava la prua.
    [Fact]
    public void GliZeriDavantiDeiCampiNonToccatiRestano()
        => Assert.Equal(
            new[] { "LIAA;09;27;113;113;095;275;N042.34.24.770;E012.34.54.460;N042.34.23.170;E012.35.19.600;" },
            Unisci(
                new[] { "LIAA;09;27;113;113;095;275;N042.34.24.770;E012.34.54.460;N042.34.23.170;E012.35.19.600;" },
                new[] { "LIAA;09;27;113;113;95;275;N042.34.24.770;E012.34.54.460;N042.34.23.170;E012.35.19.600;" },
                new[] { "LIAA;09;27;113;113;95;275;N042.34.24.770;E012.34.54.460;N042.34.23.170;E012.35.19.600;" }.Select(r => r).ToArray()));

    [Fact]
    public void UnCampoCambiatoAccantoAUnoConGliSpaziToccaSoloIlSuo()
        => Assert.Equal(
            new[] { "LIZZ;BULL;LEVIX; ; ;3;" },
            Unisci(new[] { "LIZZ;BULL;LEVIS; ; ;3;" }, new[] { "LIZZ;BULL;LEVIS;;;3;" }, new[] { "LIZZ;BULL;LEVIX;;;3;" }));

    // DYNAMIC_SEC/limmfic.tfl e GEO/licz.geo: A riattivava le righe commentate. Qui restano commenti, al loro posto.
    [Fact]
    public void UnaRigaCommentataNonTornaAttiva()
        => Assert.Equal(
            new[] { "N045.30.40.658;E010.31.40.447;", "//GARDA", "N045.30.41.000;E010.31.41.000;" },
            Unisci(
                new[] { "N045.30.40.658;E010.31.40.447;", "//GARDA", "N045.30.40.000;E010.31.40.000;" },
                new[] { "N045.30.40.658;E010.31.40.447;", "N045.30.40.000;E010.31.40.000;" },
                new[] { "N045.30.40.658;E010.31.40.447;", "N045.30.41.000;E010.31.41.000;" }));

    // ENRMVA/*.mva: il terminatore T;DUMMY spariva.
    [Fact]
    public void IlTerminatoreChelLoScrittoreNonProduceResta()
        => Assert.Equal(
            new[] { "T;LIPP;N046.37.51.000;E012.48.59.990;LIPP;", "T;DUMMY;N000.00.00.000;E000.00.00.000;" },
            Unisci(
                new[] { "T;LIPP;N046.37.50.344;E012.48.59.990;LIPP;", "T;DUMMY;N000.00.00.000;E000.00.00.000;" },
                new[] { "T;LIPP;N046.37.50.344;E012.48.59.990;LIPP;" },
                new[] { "T;LIPP;N046.37.51.000;E012.48.59.990;LIPP;" }));

    [Fact]
    public void UnVerticeAggiuntoEntraNellaFormaDelRecord()
        => Assert.Equal(
            new[] { "N0414801000;E0121420000;", "N0414802000;E0121421000;", "N0414803000;E0121422000;" },
            Unisci(
                new[] { "N0414801000;E0121420000;", "N0414803000;E0121422000;" },
                new[] { "N041.48.01.000;E012.14.20.000;", "N041.48.03.000;E012.14.22.000;" },
                new[] { "N041.48.01.000;E012.14.20.000;", "N041.48.02.000;E012.14.21.000;", "N041.48.03.000;E012.14.22.000;" },
                Forma.Compatta));

    [Fact]
    public void UnVerticeTolto_ICommentiAttornoRestano()
        => Assert.Equal(
            new[] { "N041.48.01.000;E012.14.20.000;", "//vertice sotto", "N041.48.03.000;E012.14.22.000;" },
            Unisci(
                new[] { "N041.48.01.000;E012.14.20.000;", "//vertice sotto", "N041.48.02.000;E012.14.21.000;", "N041.48.03.000;E012.14.22.000;" },
                new[] { "N041.48.01.000;E012.14.20.000;", "N041.48.02.000;E012.14.21.000;", "N041.48.03.000;E012.14.22.000;" },
                new[] { "N041.48.01.000;E012.14.20.000;", "N041.48.03.000;E012.14.22.000;" }));

    // liba/libd/lict/lire.str: la coppia decimale spostata resta decimale, e la longitudine non toccata
    // resta coi suoi byte.
    [Fact]
    public void UnaCoppiaDecimaleCambiataRestaDecimale()
        => Assert.Equal(
            new[] { "41.00850833;16.07432896;1B 6000;" },
            Unisci(
                new[] { "41.00850773;16.07432896;1B 6000;" },
                new[] { "N041.00.30.627;E016.04.27.584;1B 6000;" },
                new[] { "N041.00.30.630;E016.04.27.584;1B 6000;" },
                Forma.Decimale));

    // OTHER/libb.ap e GEO/licz.geo: un record disattivato. Lo scrittore di A lo riscrive senza `//` (o con gli
    // zeri tolti): toccato, deve restare commentato e cambiare solo il campo toccato.
    [Fact]
    public void UnRecordDisattivatoToccatoRestaCommentato()
        => Assert.Equal(
            new[] { "//LIBB;00;00;N040.56.23.001;E016.26.04.000;Brindisi ACC;" },
            Unisci(
                new[] { "//LIBB;00;00;N040.56.23.000;E016.26.04.000;Brindisi ACC;" },
                new[] { "LIBB;0;0;N040.56.23.000;E016.26.04.000;Brindisi ACC;" },
                new[] { "LIBB;0;0;N040.56.23.001;E016.26.04.000;Brindisi ACC;" }));

    // La versione vecchia commentata sopra quella nuova: la modifica va sulla riga attiva, mai sul commento.
    [Fact]
    public void LaVersioneVecchiaCommentataNonPrendeLaModifica()
        => Assert.Equal(
            new[] { "//N041.48.01.000;E012.14.20.000;", "N041.48.01.001;E012.14.20.000;" },
            Unisci(
                new[] { "//N041.48.01.000;E012.14.20.000;", "N041.48.01.000;E012.14.20.000;" },
                new[] { "N041.48.01.000;E012.14.20.000;" },
                new[] { "N041.48.01.001;E012.14.20.000;" }));

    // ENRMVA/limm.mva: lo scrittore deduce il secondo campo dell'etichetta («50») dove il file ha «LIMM».
    [Fact]
    public void UnCampoDedottoDalloScrittoreNonSostituisceQuelloDelFile()
        => Assert.Equal(
            new[] { "", "L;LIMM;N045.08.12.701;E009.51.12.700;50;8;", "T;DUMMY;N000.00.00.000;E000.00.00.000;" },
            Unisci(
                new[] { "", "L;LIMM;N045.08.12.700;E009.51.12.700;50;8;", "T;DUMMY;N000.00.00.000;E000.00.00.000;" },
                new[] { "L;50;N045.08.12.700;E009.51.12.700;50;8;" },
                new[] { "L;50;N045.08.12.701;E009.51.12.700;50;8;" }));

    // lipe.mva: un commento in coda alla riga, senza `;` finale, che lo scrittore sposta nel campo del nome.
    [Fact]
    public void UnCommentoInCodaRestaInCoda()
        => Assert.Equal(
            new[] { "T;3500SE;N044.00.03.001;E012.35.24.000; //3500 SE" },
            Unisci(
                new[] { "T;3500SE;N044.00.03.000;E012.35.24.000; //3500 SE" },
                new[] { "T; //3500 SE;N044.00.03.000;E012.35.24.000; //3500 SE;" },
                new[] { "T; //3500 SE;N044.00.03.001;E012.35.24.000; //3500 SE;" }));

    [Theory]
    [InlineData("LIZZ;BULL;LEVIS; ; ;3;", "LIZZ;BULL;LEVIS;;;3;")]
    [InlineData("LIAA;09;27;113;113;095;275;", "LIAA;09;27;113;113;95;275;")]
    [InlineData("T;PP CE;N043.49.49.00;E011.43.11.000;", "T;PP CE;N043.49.49.000;E011.43.11.000;")]
    [InlineData("AEA;111.65;N040.38.17.400;E008.17.30.400;0;2;54Y;", "AEA;111.65;N040.38.17.400;E008.17.30.400;0;2;")]
    public void RigheCheDiconoLaStessaCosa(string grezza, string base_)
        => Assert.True(FusioneDelRecord.Equivalenti(grezza, base_));

    [Theory]
    [InlineData("//N037.24.22.533;E014.54.59.095;", "N037.24.22.533;E014.54.59.095;")]
    [InlineData("N041.48.01.000;E012.14.20.000;", "N041.48.02.000;E012.14.20.000;")]
    [InlineData("LIRF;07;OST1E;", "LIRF;07;OST1E;;;1;")]
    public void RigheCheNonDiconoLaStessaCosa(string grezza, string base_)
        => Assert.False(FusioneDelRecord.Equivalenti(grezza, base_));
}
