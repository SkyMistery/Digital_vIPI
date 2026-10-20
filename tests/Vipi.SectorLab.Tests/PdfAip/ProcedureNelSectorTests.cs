using Aip;
using Aip.Aurora;
using Vipi.SectorLab.Core.PdfAip;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// Carta F7, slice 7e: le righe dei gesti sulle procedure — il nome nella convenzione del file (D1), il blocco della voce
/// coi tag di quota e velocità (D3), la sezione dello scalo di <c>APT.fix</c>. Dati inventati.
/// </summary>
public sealed class ProcedureNelSectorTests
{
    private static ProcedureLeg T(string pt, string? fix, string? alt = null, string? spd = null, string? nav = "RNAV1/RNP1 (1)") =>
        new() { PathTerminator = pt, Fix = fix, Altitude = alt, Speed = spd, Navigation = nav };

    [Fact]
    public void LeLettereDelFile_LaPiuFrequente()
    {
        Assert.Equal(4, ProcedureNelSector.Lettere([("XIBR5A", "XIBRI 5A"), ("ELKA3A", "ELKAP 3A"), ("IRK7A", "IRKED 7A")]));
        Assert.Equal(int.MaxValue, ProcedureNelSector.Lettere([("BAGIX3R", "BAGIX 3R"), ("CRN2E", "CRN 2E"), ("ARBU1B", "ARBUM 1B")]));
        // Un nome che salta lettere (NOK1K) non dice niente sulla lunghezza; senza coppie vale la base.
        Assert.Equal(3, ProcedureNelSector.Lettere([("NOK1K", "NOBKE 1K"), ("FAR6B", "FARAK 6B")]));
        Assert.Equal(5, ProcedureNelSector.Lettere([]));

        Assert.Equal("XIBR5A", ProcedureNelSector.Nome("XIBRI 5A", 4));
        Assert.Equal("BAGIX3R", ProcedureNelSector.Nome("BAGIX 3R", int.MaxValue));
        Assert.Equal("CRN2E", ProcedureNelSector.Nome("CRN 2E", 4));
        Assert.Equal("3R", ProcedureNelSector.Coda("BAGIX 3R"));
    }

    [Theory]
    [InlineData("+FL110", "+FL110")]
    [InlineData("+4000FT", "+4000")]
    [InlineData("+4000 FT", "+4000")]
    [InlineData("AT 4000", "=4000")]
    [InlineData("-FL170 +FL120", "FL120/FL170")]
    [InlineData("FL180/190*", "FL180/190")]
    [InlineData("+FL 190", "+FL190")]
    [InlineData(null, null)]
    public void LaQuota(string? stampata, string? attesa) => Assert.Equal(attesa, ProcedureNelSector.Quota(stampata));

    [Theory]
    [InlineData("-210", "-210")]
    [InlineData("AT 200", "=200")]
    [InlineData("-220 KT", "-220")]
    [InlineData(null, null)]
    public void LaVelocita(string? stampata, string? attesa) => Assert.Equal(attesa, ProcedureNelSector.Velocita(stampata));

    [Fact]
    public void IlBloccoDiUnaStar_ETantiTagQuanteQuoteEVelocita()
    {
        var star = new Procedure
        {
            Aerodrome = "LIRF", Kind = ProcedureKind.Star, Designator = "ELKAP 3A", Runways = ["16L", "16R"],
            Legs = [T("IF", "ELKAP", alt: "+FL190", spd: "-270"), T("TF", "BIBEK"), T("TF", "SUVOK", alt: "+4000FT", spd: "AT 200")],
        };

        var blocco = ProcedureNelSector.Blocco("ELKA3A", ProcedureNelSector.TestataDellaStar("LIRF", star.Runways, "ELKA3A", ProcedureNelSector.Rnav([star])),
            [star], "AIP 2610", ProcedureNelSector.Coda(star.Designator));

        Assert.Equal(
        [
            "//@\"ELKA3A\" nav=RNAV1/RNP1 note=\"AIP 2610\"",
            "//@START",
            "LIRF;16L:16R;ELKA3A;;;;;1;",
            "//@@\"ELKAP\" alt=+FL190 spd=-270",
            "ELKAP;ELKAP;3A;",
            "BIBEK;BIBEK;",
            "//@@\"SUVOK\" alt=+4000 spd==200",
            "SUVOK;SUVOK;",
            "//@END \"ELKA3A\"",
        ], blocco);
    }

    [Fact]
    public void UnaSidColSuoTracciato_ELaTransizioneDopo()
    {
        var sid = new Procedure
        {
            Aerodrome = "LIRF", Kind = ProcedureKind.Sid, Designator = "SOSAK 5A", Runways = ["25"],
            Legs = [T("CA", null, alt: "+410"), T("CF", "RF601", spd: "-210"), T("TF", "SOSAK", alt: "+3000")],
        };
        var trans = new Procedure
        {
            Aerodrome = "LIRF", Kind = ProcedureKind.Sid, Designator = "ESINO 8H",
            Legs = [T("IF", "SOSAK"), T("TF", "ESINO", alt: "+FL110")],
        };

        Assert.Equal(["RF601", "SOSAK", "ESINO"], ProcedureNelSector.Punti([sid, trans]));
        Assert.Equal("LIRF;25;SOS5A-ESI8H; ; ;0;ESINO;1;", ProcedureNelSector.TestataDellaSid("LIRF", sid.Runways, "SOS5A-ESI8H", "ESINO", ProcedureNelSector.Rnav([sid, trans])));
        // SOSAK ha la quota della SID (il primo tratto che ci arriva), non quella vuota della transizione; il CA non ha posto.
        Assert.Equal(
        [
            "//@@\"RF601\" spd=-210", "RF601;RF601;",
            "//@@\"SOSAK\" alt=+3000", "SOSAK;SOSAK;",
            "//@@\"ESINO\" alt=+FL110", "ESINO;ESINO;",
        ], ProcedureNelSector.Corpo([sid, trans], null));
    }

    // Slice 7b: una STAR dalla prosa — i punti calcolati come coordinate, l'arco DME un punto ogni 5° attorno alla stazione;
    // fra i punti con nome (da cercare e portare) quelli calcolati non ci sono.
    [Fact]
    public void UnaStarDallaProsa_ICalcolatiComeCoordinate_ELArcoAPezzi()
    {
        var stazione = new Position(39.0, 16.0);
        var inizio = new Position(39.25, 16.0);                                     // 15 NM a nord
        var fine = new Position(39.0, 16.0 + 0.25 / Math.Cos(39 * Math.PI / 180)); // 15 NM a est
        var star = new Procedure
        {
            Aerodrome = "LICA", Kind = ProcedureKind.Star, Designator = "ARBUM 4F", Runways = ["10"], LegsFromDescription = true,
            Legs =
            [
                new ProcedureLeg { PathTerminator = "IF", Fix = "ARBUM" },
                new ProcedureLeg { PathTerminator = "CF", Computed = "RDL 357/15 NM LMT", Position = inizio },
                new ProcedureLeg { PathTerminator = "AF", Computed = "RDL 087/15 NM LMT", Position = fine, ArcCenter = "LMT", ArcRadiusNm = 15, Turn = "R" },
                new ProcedureLeg { PathTerminator = "CF", Fix = "SUGEP" },
            ],
        };

        Assert.Equal(["ARBUM", "SUGEP"], ProcedureNelSector.Punti([star]));
        var corpo = ProcedureNelSector.Corpo([star], "4F", n => n == "LMT" ? stazione : null);
        Assert.Equal("ARBUM;ARBUM;4F;", corpo[0]);
        Assert.Equal("N039.15.00.000;E016.00.00.000;", corpo[1]);
        Assert.Equal("SUGEP;SUGEP;", corpo[^1]);
        // Un quarto di giro: 17 punti in mezzo, poi la fine.
        Assert.Equal(1 + 1 + 17 + 1 + 1, corpo.Count);
        Assert.Equal(AuroraText.Coordinates(fine) + ";", corpo[^2]);
        Assert.Matches(@"^N039\.\d{2}\.\d{2}\.\d{3};E016\.\d{2}\.\d{2}\.\d{3};$", corpo[10]);
        // Senza chi trova la stazione, l'arco è solo la sua fine.
        Assert.Equal(4, ProcedureNelSector.Corpo([star], "4F").Count);
    }

    [Fact]
    public void IPuntiDiScalo_NellaSezioneDelloScalo_OInFondo()
    {
        IReadOnlyList<string> righe =
        [
            "//LIRF", "RF601;N041.46.15.680;E012.14.42.000;3;", "RF603;N041.42.19.240;E012.11.14.300;3;", "",
            "//LIRN", "RN601;N040.51.28.880;E014.13.47.070;3;",
        ];

        var nella = ProcedureNelSector.NellaSezioneDelloScalo(righe, "LIRF", ["RF604;N041.44.01.660;E012.13.49.990;3;"]);
        Assert.Equal([3], nella.Keys);
        Assert.Equal(["RF603;N041.42.19.240;E012.11.14.300;3;", "RF604;N041.44.01.660;E012.13.49.990;3;"], nella[3]);

        var nuova = ProcedureNelSector.NellaSezioneDelloScalo(righe, "LICA", ["CA501;N038.54.00.000;E016.14.00.000;3;"]);
        Assert.Equal(["RN601;N040.51.28.880;E014.13.47.070;3;", "", "//LICA", "CA501;N038.54.00.000;E016.14.00.000;3;"], nuova[6]);

        Assert.Equal("RN601;N040.51.28.880;E014.13.47.070;3;",
            ProcedureNelSector.RigaDelPuntoDiScalo(new SignificantPoint { Section = "AD 2 LIRN", Designator = "RN601", Position = new Position(40 + 51 / 60.0 + 28.88 / 3600, 14 + 13 / 60.0 + 47.07 / 3600) }));
    }
}
