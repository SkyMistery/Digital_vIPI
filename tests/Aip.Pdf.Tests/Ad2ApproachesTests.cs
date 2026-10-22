using Aip.Pdf.Italia;
using Pagina = Aip.Pdf.Tests.Enr4Tests.Pagina;

namespace Aip.Pdf.Tests;

/// <summary>
/// Carta «L'AIP intero», passo 6: gli avvicinamenti del file 5 di uno scalo su pagine costruite qui, con l'impaginazione dei
/// PDF veri (LIRN, LICC, LIBC): il titolo, i segmenti, la tabella dei tratti col numero d'ordine e il VPA/TCH, la FIX/POINT,
/// il retro di una carta, il blocco SBAS FAS. Dati inventati.
/// </summary>
public sealed class Ad2ApproachesTests
{
    private static (double, double, string)[] Testa(string riferimento) =>
        [(450, 12, riferimento), (52, 24, "AIP - Italia AIRAC effective date 11 JUN 2026")];

    // Le x delle colonne, come in LIRN 5: Serial, Path, Waypoint, Fly, Course, Magnetic, Distance, Turn, Altitude, Speed, VPA, Navigation.
    private static readonly double[] X = [40, 70, 110, 160, 200, 245, 290, 330, 375, 425, 455, 495];

    private static (double, double, string)[] Intestazione(double top) =>
    [
        (X[0], top - 9, "Serial"), (X[1], top - 9, "Path"), (X[2], top - 9, "Waypoint"), (X[3], top - 9, "Fly"), (X[4], top - 9, "Course"),
        (X[5], top - 9, "Magnetic"), (X[6], top - 9, "Distance"), (X[7], top - 9, "Turn"), (X[8], top - 9, "Altitude"), (X[9], top - 9, "Speed"),
        (X[10], top - 9, "VPA/"), (X[11], top - 9, "Navigation"),
        (X[0], top, "Number"), (X[1], top, "Terminator"), (X[2], top, "Identifier"), (X[3], top, "Over"), (X[4], top, "°M(°T)"),
        (X[5], top, "Variation"), (X[6], top, "(NM)"), (X[7], top, "Direction"), (X[8], top, "(ft)"), (X[9], top, "(kt)"), (X[10], top, "TCH"),
        (X[11], top, "Specification"),
    ];

    private static (double, double, string)[] Tratto(double top, string numero, string pt, string fix, string quota, string vpa = "-") =>
    [
        (X[0], top, numero), (X[1], top, pt), (X[2], top, fix), (X[3], top, "-"), (X[4], top, "-"), (X[5], top, "-"), (X[6], top, "-"),
        (X[7], top, "-"), (X[8], top, quota), (X[9], top, "-185"), (X[10], top, vpa), (X[11], top, "RNP1"),
    ];

    private static IReadOnlyList<PdfPageText> Pagine()
    {
        var ils = new Pagina([], [],
        [
            .. Testa("AD 2 LIXX 5 - 1"),
            (60, 52, "TABULAR DESCRIPTION"),
            (60, 68, "ILS Z RWY24 – Instrument Approach procedure"),
            (60, 90, "RNAV to ILS TRANSITION - APPROACH FROM BENTO"),
            .. Intestazione(120),
            .. Tratto(140, "010", "IF", "BENTO", "+7000"),
            .. Tratto(160, "020", "TF", "XX714", "+5200"),
            (60, 190, "ILS to RNAV TRANSITION - MISSED APPROACH"),
            .. Intestazione(220),
            .. Tratto(240, "010", "CA", "-", "+750"),
            .. Tratto(260, "020", "DF", "XX706", "-"),
            (60, 300, "Coordinates"), (60, 310, "FIX/POINT"), (60, 320, "(WGS84)"),
            (60, 340, "INT LOC IXXX/9.5NM XXX DME 40°58’26.10”N-014°28’39.44”E"),
            (60, 360, "40°56’21.13”N"), (60, 367, "FAF"), (60, 374, "014°24’13.45”E"),
        ]);
        // Una carta solo immagine, e sul retro la sua FIX/POINT senza titolo.
        var carta = new Pagina([], [], [.. Testa("AD 2 LIXX 5 - 2")]);
        var retro = new Pagina([], [],
        [
            .. Testa("AD 2 LIXX 5 - 3"),
            (60, 100, "Coordinates"), (60, 110, "FIX/POINT"), (60, 120, "(WGS84)"),
            (300, 135, "41°03'38.6''N"), (60, 142, "IAF"), (300, 148, "014°39'48.8''E"),
            (300, 165, "41°01'51.9''N"), (60, 172, "IF"), (300, 179, "014°35'59.6''E"),
            (300, 195, "40°58'41.8''N"), (60, 202, "FAF"), (300, 209, "014°29'13.2''E"),
            (60, 226, "MAPt"), (300, 226, "40°53'56.6''N"), (60, 239, "(LOC+DME)"), (300, 239, "014°19'06.2''E"),
        ]);
        // Il designatore su una riga a sé sotto «TABULAR DESCRIPTION» (LICC), una tabella senza titolo di segmento.
        var rnp = new Pagina([], [],
        [
            .. Testa("AD 2 LIXX 5 - 4"),
            (60, 52, "TABULAR DESCRIPTION"),
            (60, 68, "RNP - Z RWY 08"),
            // Una transizione col suo titolo, poi il finale senza titolo (LIRN RNP Z RWY24): il finale è l'avvicinamento stesso.
            (60, 85, "RNP APPROACH FROM XXONE"),
            .. Intestazione(115),
            .. Tratto(135, "010", "IF", "XXONE", "+5000"),
            .. Intestazione(175),
            .. Tratto(195, "010", "IF", "XX501", "+3000"),
            .. Tratto(215, "020", "TF", "RWY08", "-", "-3.0/15"),
        ]);
        var fas = new Pagina([], [],
        [
            .. Testa("AD 2 LIXX 5 - 5"),
            (60, 60, "SBAS FAS DATA BLOCK LIXX RNP Z RWY 08"),
            (60, 80, "Operation Type 0"), (60, 92, "SBAS Provider 1 (EGNOS)"), (60, 104, "Airport Identifier LIXX"),
            (60, 116, "Runway 08"), (60, 128, "Glidepath Angle (degrees) 3.00"), (60, 140, "Calculated CRC Value 7130EB3C"),
        ]);
        return PdfText.Read(Enr4Tests.Pdf(ils, carta, retro, rnp, fas));
    }

    [Fact]
    public void ISegmenti_ColNumeroDOrdine_ELaFixPoint()
    {
        var r = ItalianAip.ReadFile("LI-AD 2 LIXX - PROVA 5.pdf", Pagine());

        Assert.Equal([], r.Issues.Where(i => i.Kind != ImportIssueKind.PageWithoutDate).Select(i => $"{i.Kind} {i.Key} {i.Detail}"));
        var ils = r.Procedures.Where(p => p.Designator == "ILS Z RWY24").ToList();
        Assert.Equal([null, "RNAV to ILS TRANSITION - APPROACH FROM BENTO", "ILS to RNAV TRANSITION - MISSED APPROACH"], ils.Select(p => p.Segment));
        Assert.All(ils, p => Assert.Equal((ProcedureKind.Approach, "24"), (p.Kind, Assert.Single(p.Runways))));
        Assert.Equal(["010 IF BENTO", "020 TF XX714"], ils[1].Legs.Select(t => $"{t.Sequence} {t.PathTerminator} {t.Fix}"));
        Assert.Equal(["010 CA ", "020 DF XX706"], ils[2].Legs.Select(t => $"{t.Sequence} {t.PathTerminator} {t.Fix}"));
        // La FIX/POINT della pagina sta sull'avvicinamento, la definizione com'è e la posizione dalle coordinate.
        Assert.Equal(["INT LOC IXXX/9.5NM XXX DME", "FAF"], ils[0].Fixes!.Select(f => f.Text));
        Assert.Equal(40 + 56 / 60.0 + 21.13 / 3600, ils[0].Fixes![1].Position!.Value.Lat, 9);
    }

    [Fact]
    public void IlRetroDellaCarta_IlDesignatoreDaSolo_IlBloccoFas_EIlConto()
    {
        var pagine = Pagine();
        var r = ItalianAip.ReadFile("LI-AD 2 LIXX - PROVA 5.pdf", pagine);

        // Il retro della carta di p.2: ha per nome la pagina della carta (l'AIP espanso lo cambia nel titolo della carta).
        var retro = r.Procedures.Single(p => p.Designator == "AD 2 LIXX 5-2");
        Assert.Equal(["IAF", "IF", "FAF", "MAPt (LOC+DME)"], retro.Fixes!.Select(f => f.Text));
        Assert.All(retro.Fixes!, f => Assert.NotNull(f.Position));
        Assert.Equal([null, "RNP APPROACH FROM XXONE"], r.Procedures.Where(p => p.Designator == "RNP Z RWY08").Select(p => p.Segment).Order());
        var rnp = r.Procedures.Single(p => p.Designator == "RNP Z RWY08" && p.Segment is null);
        Assert.Equal("08", Assert.Single(rnp.Runways));
        Assert.Equal("-3.0/15", rnp.Legs[1].VerticalPath);
        Assert.Equal(["Operation Type 0", "SBAS Provider 1 (EGNOS)", "Airport Identifier LIXX", "Runway 08", "Glidepath Angle (degrees) 3.00",
            "Calculated CRC Value 7130EB3C"], rnp.DataBlock);
        Assert.Equal([new ExpectedCount("avvicinamenti", 2)], ItalianExpectedCounts.Of("LI-AD 2 LIXX - PROVA 5.pdf", pagine));
        Assert.Equal(2, ItalianExpectedCounts.Read("avvicinamenti", r));
        Assert.Equal("ILS Z RWY24", ItalianAd2Procedures.DesignatoreDellaCarta("ICAO INSTRUMENT APPROACH CHART ILS Z RWY 24"));
    }
}
