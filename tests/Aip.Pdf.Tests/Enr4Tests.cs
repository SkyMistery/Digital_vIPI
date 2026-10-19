using Aip.Pdf.Italia;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Aip.Pdf.Tests;

/// <summary>
/// ENR 4 (carta F6, slice 3): radioassistenze, punti significativi, luci, su un PDF costruito qui con le righe verticali
/// <b>e orizzontali</b> della tabella — in ENR 4 di chi è una riga di testo lo dicono i bordi, quindi il PDF finto li deve
/// avere, e il test passa per <see cref="PdfText"/>. Le x delle colonne sono quelle dei PDF veri; dati inventati.
/// </summary>
public sealed class Enr4Tests
{
    private const double Altezza = 842;

    private static readonly double[] Radioassistenze = [52, 113, 139, 170, 200, 262, 313, 449, 500, 565];
    private static readonly double[] Punti = [52, 137, 200, 263, 377, 491, 566];
    private static readonly double[] Luci = [52, 142, 212, 312, 382, 462, 552];

    /// <summary>
    /// Una pagina: le colonne alle x date, i bordi orizzontali alle y date (dall'alto, da un capo all'altro della tabella),
    /// e il testo (x, distanza dall'alto, testo).
    /// </summary>
    private sealed record Pagina(double[] Colonne, double[] Bordi, (double X, double Top, string Testo)[] Testo);

    private static byte[] Pdf(params Pagina[] pagine)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var p in pagine)
        {
            var page = builder.AddPage(595, Altezza);
            if (p.Bordi.Length > 0)
            {
                foreach (var x in p.Colonne)
                    page.DrawLine(new PdfPoint(x, Altezza - p.Bordi[0]), new PdfPoint(x, Altezza - p.Bordi[^1]), 0.5);
                foreach (var y in p.Bordi)
                    page.DrawLine(new PdfPoint(p.Colonne[0], Altezza - y), new PdfPoint(p.Colonne[^1], Altezza - y), 0.5);
            }
            // ⚠️ Il font standard di PdfPig non ha «°»: i gradi sono «º», che il lettore accetta come i PDF veri.
            foreach (var (x, top, testo) in p.Testo)
                page.AddText(testo.Replace('°', 'º'), 7, new PdfPoint(x, Altezza - top - 7), font);
        }
        return builder.Build();
    }

    private static (double, double, string)[] Testata(string riferimento, string data, double[] colonne, int numeri) =>
    [
        (450, 30, riferimento),
        (52, 45, "AIP - Italia AIRAC effective date " + data),
        (60, 140, "Intestazione"),
        .. Enumerable.Range(0, numeri).Select(i => (colonne[i] + 4, 242.0, (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))),
    ];

    private static ImportResult LeRadioassistenze()
    {
        var c = Radioassistenze;
        var uno = new Pagina(c, [130, 240, 255, 400, 500],
        [
            .. Testata("ENR 4.1.1 - 1", "11 JUN 2026", c, 9),
            // Un VOR/DME: la copertura comincia in cima alla riga, il nome sta in mezzo (il testo è centrato nella cella).
            (316, 262, "Settore/Sector 100 NM/50000 FT"), (316, 274, "limitazioni a/limitations at 40 NM"),
            (316, 286, "050°/160° MRA 5500 FT"),
            (503, 280, "1) MAINT:"), (503, 292, "first MON"), (503, 304, "2) Solo per le"), (503, 316, "procedure"),
            (54, 300, "MONTE"), (54, 312, "PROVA"), (54, 324, "VOR/DME"), (54, 336, "(3° E-2020.0)"),
            (115, 318, "MPV"),
            (141, 306, "113.80"), (141, 318, "MHZ"), (141, 330, "CH"), (141, 342, "85X"),
            (172, 306, "VOR"), (172, 318, "H24"), (172, 330, "DME"), (172, 342, "H24"),
            (202, 300, "VOR"), (202, 312, "41°30'00''N"), (202, 324, "012°15'00''E"),
            (202, 336, "DME"), (202, 348, "41°30'36''N"), (202, 360, "012°15'00''E"),
            (264, 318, "443 M"), (264, 330, "AMSL"),
            (452, 312, "FRA(IA):"), (452, 324, "LIRF"),
            // Un locator con la stessa sigla: un'altra radioassistenza.
            (316, 405, "25 NM"),
            (54, 440, "PROVA"), (54, 452, "L"),
            (115, 446, "MPV"), (141, 440, "379.00"), (141, 452, "KHZ"), (172, 446, "H24"),
            (202, 440, "41°35'21''N"), (202, 452, "010°17'49''E"), (264, 446, "NIL"), (452, 446, "NIL"), (503, 446, "NIL"),
        ]);
        var due = new Pagina(c, [130, 240, 255, 360],
        [
            .. Testata("ENR 4.1.1 - 2", "06 AUG 2026", c, 9),
            // Il DVOR di cui l'AIP non stampa le coordinate (Grosseto nel 2609): si dice.
            (54, 290, "SENZA"), (54, 302, "DVOR"), (115, 296, "SNZ"), (141, 296, "109.85"), (141, 308, "MHZ"), (172, 296, "H24"),
        ]);
        var bianca = new Pagina(c, [], [(450, 30, "ENR 4.1.1 - 3"), (52, 45, "AIRAC effective date 06 AUG 2026")]);
        return ItalianEnr4.ReadNavaids(PdfText.Read(Pdf(uno, due, bianca)), military: false);
    }

    [Fact]
    public void IBordiOrizzontaliSiLeggono()
    {
        var c = Radioassistenze;
        var pagina = PdfText.Read(Pdf(new Pagina(c, [130, 240, 255, 400], [])))[0];

        Assert.NotNull(pagina.HorizontalRules);
        Assert.Equal([130, 240, 255, 400], pagina.HorizontalRules!.Select(r => Math.Round(r.Y)).Order());
        Assert.All(pagina.HorizontalRules, r => Assert.Equal((52, 565), (Math.Round(r.X0), Math.Round(r.X1))));
    }

    [Fact]
    public void UnVorDme_DueApparatiConLaLoroPosizioneEIlLoroOrario()
    {
        var r = LeRadioassistenze();
        var mpv = r.Navaids[0];

        Assert.Equal(("MPV", "MONTE PROVA", "VOR/DME", NavaidKind.VorDme), (mpv.Designator, mpv.Name, mpv.Type, mpv.Kind));
        Assert.Equal("ENR 4.1", mpv.Section);
        Assert.False(mpv.Military);
        Assert.Equal("3° E-2020.0", mpv.MagneticVariation?.Replace('º', '°'));
        Assert.Equal(("113.80 MHZ", "85X"), (mpv.Frequency, mpv.Channel));
        Assert.Equal(["VOR", "DME"], mpv.Components.Select(a => a.Equipment));
        Assert.Equal(["H24", "H24"], mpv.Components.Select(a => a.Hours));
        Assert.Equal(41.5, mpv.Position.Lat, 9);
        Assert.Equal(12.25, mpv.Position.Lon, 9);
        Assert.Equal(41.51, mpv.Components[1].Position.Lat, 9);
        Assert.Equal("443 M AMSL", mpv.Elevation);
        Assert.Equal("FRA(IA): LIRF", mpv.Fra);
        // La copertura in cima alla riga è di questa radioassistenza, non di quella sopra; le note una per riga.
        Assert.StartsWith("Settore/Sector 100 NM/50000 FT limitazioni", mpv.Coverage);
        Assert.Equal("1) MAINT: first MON\n2) Solo per le procedure", mpv.Remarks);
        Assert.Equal(new AipPage("ENR 4.1.1-1", new DateOnly(2026, 6, 11)), mpv.Page);
    }

    [Fact]
    public void StessaSiglaAltroTipo_DueRadioassistenze_IlNilNonSiScrive()
    {
        var r = LeRadioassistenze();

        Assert.Equal(["MPV VOR/DME", "MPV L"], r.Navaids.Select(n => n.Key));
        var l = r.Navaids[1];
        Assert.Equal((NavaidKind.Locator, "379.00 KHZ", (string?)null), (l.Kind, l.Frequency, l.Channel));
        Assert.Equal([(null, "H24")], l.Components.Select(a => (a.Equipment, a.Hours)));
        Assert.Equal((null, null, null, "25 NM"), (l.Elevation, l.Fra, l.Remarks, l.Coverage));
    }

    [Fact]
    public void SenzaCoordinateNonEntra_ESiDice_LaPaginaBiancaSiConta()
    {
        var r = LeRadioassistenze();

        Assert.Equal(2, r.Navaids.Count);
        var p = Assert.Single(r.Issues);
        Assert.Equal((ImportIssueKind.Navaid, "SNZ DVOR", 2), (p.Kind, p.Key, p.Page));
        Assert.Equal([true, true, false], r.Pages.Select(x => x.Table));
    }

    [Fact]
    public void ILPuntiSignificativi_RotteProcedureEFra()
    {
        var c = Punti;
        var pagina = new Pagina(c, [130, 175, 190, 205, 250, 295, 340],
        [
            (450, 30, "ENR 4.4 - 1"), (52, 45, "AIP - Italia AIRAC effective date 11 JUN 2026"),
            (60, 140, "CODICE ICAO"),
            (56, 177, "1"), (140, 177, "2"), (203, 177, "2"), (266, 177, "3"), (380, 177, "4"), (494, 177, "5"),
            (140, 192, "LAT"), (203, 192, "LONG"),
            (56, 208, "ABADI"), (140, 208, "40°45'19''N"), (203, 208, "018°38'30''E"), (266, 208, "P92"),
            (380, 208, "BR star, BR sid"), (494, 208, "FRA(I)"),
            (56, 253, "ABESI"), (140, 253, "46°09'35''N"), (203, 253, "009°02'34''E"), (266, 253, "N851"), (266, 265, "L12"),
            (380, 253, "MF sid"), (494, 253, "FRA(X):"), (494, 265, "FRA(D): LIMC,"), (494, 277, "LIME"),
            (56, 298, "AB1"), (140, 298, "46°02'37''N"), (203, 298, "011°07'48''E"), (266, 298, "NIL"),
        ]);

        var r = ItalianEnr4.ReadPoints(PdfText.Read(Pdf(pagina)));

        Assert.Equal(["ABADI", "ABESI"], r.Points.Select(p => p.Designator));
        var abadi = r.Points[0];
        Assert.Equal(40 + 45 / 60.0 + 19 / 3600.0, abadi.Position.Lat, 9);
        Assert.Equal(["P92"], abadi.Routes);
        Assert.Equal(("BR star, BR sid", "FRA(I)"), (abadi.Procedures, abadi.Fra));
        Assert.Equal(["N851", "L12"], r.Points[1].Routes);
        Assert.Equal("FRA(X): FRA(D): LIMC, LIME", r.Points[1].Fra);
        var p = Assert.Single(r.Issues);
        Assert.Equal((ImportIssueKind.SignificantPoint, "AB1"), (p.Kind, p.Key));
    }

    [Fact]
    public void LeLuci_AncheSenzaPosizione_LaLegendaNo()
    {
        var c = Luci;
        var pagina = new Pagina(c, [70, 110, 121, 187, 253, 264, 275],
        [
            (450, 30, "ENR 4.5 - 1"), (52, 45, "AIP - Italia 27 JUL 2023"),
            (60, 80, "Nome"),
            .. Enumerable.Range(0, 6).Select(i => (c[i] + 4, 112.0, (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))),
            (54, 125, "CAPO DI PROVA"), (54, 137, "43°57'17''N"), (54, 149, "008°10'22''E"),
            (144, 125, "Marittimo"), (144, 137, "Maritime"), (214, 125, "3 lampi bianchi"), (214, 137, "ogni 15''"),
            (314, 125, "HN"), (384, 125, "668,8"),
            (54, 191, "FARO (Isola di)"), (144, 191, "Marittimo"), (314, 191, "HN"), (384, 191, "32"), (464, 191, "Settore 35°-85°"),
            (54, 255, "OR (AM) - ottica rotante"), (144, 255, "con pannelli"),
            (54, 266, "OR (PD) - ottica rotante"), (144, 266, "deflettori"),
        ]);

        var r = ItalianEnr4.ReadLights(PdfText.Read(Pdf(pagina)));

        Assert.Equal(["CAPO DI PROVA", "FARO (Isola di)"], r.Lights.Select(l => l.Name));
        Assert.Equal(43 + 57 / 60.0 + 17 / 3600.0, r.Lights[0].Position!.Value.Lat, 9);
        Assert.Equal(("Marittimo / Maritime", "HN", "668,8"), (r.Lights[0].Type, r.Lights[0].Hours, r.Lights[0].Intensity));
        Assert.Null(r.Lights[1].Position);
        Assert.Equal("Settore 35º-85º", r.Lights[1].Remarks);
        Assert.Empty(r.Issues);
        Assert.Equal(new DateOnly(2023, 7, 27), r.Lights[0].Page?.Effective);
    }
}
