using Aip.Pdf.Italia;
using Pagina = Aip.Pdf.Tests.Enr4Tests.Pagina;

namespace Aip.Pdf.Tests;

/// <summary>
/// Il testo intero di uno scalo (carta «L'AIP intero», passo 5): tutte le sottosezioni com'è stampate — le voci numerate
/// anche quando continuano nella pagina dopo, la prosa di una riga, le tabelle riga per riga, le carte di AD 2.24 — e gli
/// eliporti di AD 3. PDF costruiti qui (<see cref="Enr4Tests.Pdf"/>), senza lettere accentate; dati inventati.
/// </summary>
public sealed class AdSectionsTests
{
    private static (double, double, string)[] Testa(string riferimento) =>
        [(450, 5, riferimento), (52, 17, "AIP - Italia AIRAC effective date 11 JUN 2026")];

    private static IReadOnlyList<PdfPageText> Scalo()
    {
        var uno = new Pagina([52, 77, 257, 565], [90, 120, 165],
        [
            .. Testa("AD 2 LIXX 1 - 1"),
            (52, 60, "LIXX AD 2.3 ORARIO DEI SERVIZI"), (52, 72, "LIXX AD 2.3 OPERATIONAL HOURS"),
            (55, 100, "1"), (80, 100, "Amministrazione"), (80, 110, "Administration"), (260, 100, "H24"),
            (55, 130, "2"), (80, 130, "Note"), (260, 130, "1. Prima nota"), (80, 150, "Remarks"), (260, 150, "1. First note"),
        ]);
        // La nota continua nella pagina dopo, senza numero; poi una sottosezione di una riga e l'elenco delle carte.
        var due = new Pagina([52, 77, 257, 565], [50, 80],
        [
            .. Testa("AD 2 LIXX 1 - 2"),
            (260, 60, "2. Seconda nota"),
            (52, 100, "LIXX AD 2.10 OSTACOLI AEROPORTUALI"), (52, 112, "LIXX AD 2.10 AERODROME OBSTACLES"),
            (200, 125, "Vedi AOC in vigore - See AOC in force"),
        ]);
        var tre = new Pagina([52, 330, 565], [60, 75, 90, 105],
        [
            .. Testa("AD 2 LIXX 1 - 3"),
            (52, 30, "LIXX AD 2.24 CARTE AERONAUTICHE RELATIVE ALL'AEROPORTO"), (52, 42, "LIXX AD 2.24 CHARTS RELATED TO THE AERODROME"),
            (60, 63, "Carte - Charts"), (335, 63, "Pagine - Pages"),
            (60, 78, "AERODROME CHART ICAO"), (335, 78, "AD 2 LIXX 2 - 1"),
            (60, 93, "SID RNAV1"), (335, 93, "AD 2 LIXX 6 - 1"),
        ]);
        return PdfText.Read(Enr4Tests.Pdf(uno, due, tre));
    }

    [Fact]
    public void LeSottosezioni_LeVociAncheNellaPaginaDopo_LaProsaDiUnaRiga_LeTabelle()
    {
        var s = Assert.Single(ItalianAd2.Read(Scalo(), "LIXX", "PROVA").Aerodromes);

        Assert.Equal(["AD 2.3", "AD 2.10", "AD 2.24"], s.Sections!.Select(x => x.Number));
        var orari = s.Sections![0];
        Assert.Equal(("ORARIO DEI SERVIZI", "OPERATIONAL HOURS", "AD 2 LIXX 1-1"), (orari.Title, orari.TitleEnglish, orari.Page?.Reference));
        Assert.Equal(
        [
            new AerodromeItem("1", "Amministrazione\nAdministration", "H24"),
            new AerodromeItem("2", "Note\nRemarks", "1. Prima nota\n1. First note\n2. Seconda nota"),
        ], orari.Items);
        // Una riga sola non si divide fra le due lingue.
        Assert.Equal(("Vedi AOC in vigore - See AOC in force", (string?)null), (s.Sections![1].Text, s.Sections[1].English));
        Assert.Equal(3, s.Sections![2].Rows!.Count);
        Assert.Equal(["AERODROME CHART ICAO", "AD 2 LIXX 2 - 1"], s.Sections[2].Rows![1]);
    }

    [Fact]
    public void LeCarteDiAd224_ELaPaginaComeNellIndice()
    {
        var s = Assert.Single(ItalianAd2.Read(Scalo(), "LIXX", "PROVA").Aerodromes);

        Assert.Equal([new AerodromeChart("AERODROME CHART ICAO", "AD 2 LIXX 2-1"), new AerodromeChart("SID RNAV1", "AD 2 LIXX 6-1")], s.Charts);
    }

    [Fact]
    public void IlConto_LeSottosezioniStampateEQuelleLette()
    {
        var pagine = Scalo();

        Assert.Equal([new ExpectedCount("sottosezioni", 3)], ItalianExpectedCounts.Of("LI-AD 2  LIXX - PROVA 1.pdf", pagine));
        Assert.Equal(3, ItalianExpectedCounts.Read("sottosezioni", ItalianAd2.Read(pagine, "LIXX", "PROVA")));
        // Il nome del file con due spazi dopo «AD 2» (52 scali su 97 nel 2611) si riconosce.
        Assert.Equal("PROVA", Assert.Single(ItalianAip.ReadFile("LI-AD 2  LIXX - PROVA 1.pdf", pagine).Aerodromes).Name);
    }

    // AD 3: il nome sotto il titolo di AD 3.1, il titolo italiano che va a capo, l'ARP «41°51'59'' N - 016°09'54'' E», la declinazione al 4 e il traffico al 6, le
    // tabelle «NIL» dello spazio aereo e delle radioassistenze che non sono righe, e niente «senza piste».
    [Fact]
    public void UnEliporto()
    {
        var pagina = new Pagina([52, 77, 257, 565], [100, 115, 130, 145],
        [
            .. Testa("AD 3 LIXH 1 - 1"),
            (52, 50, "LIXH AD 3.1 INDICATORE DI LOCALITA E NOME"), (52, 62, "LIXH AD 3.1 HELIPAD LOCATION INDICATOR AND NAME"),
            (200, 67, "LIXH - Prova Eli"),
            (52, 76, "LIXH AD 3.2 DATI AMMINISTRATIVI"), (52, 83, "E GEOGRAFICI"), (52, 90, "LIXH AD 3.2 HELIPAD DATA"),
            (55, 105, "1"), (80, 105, "Coordinate"), (260, 105, "41°51'59'' N - 016°09'54'' E"),
            (55, 120, "4"), (80, 120, "Variazione magnetica"), (260, 120, "4° E (2020) / 7'E"),
            (55, 135, "6"), (80, 135, "Traffico"), (260, 135, "VFR"),
        ]);
        double[] spazi = [52, 140, 220, 300, 380, 460, 565];
        var nil = new Pagina(spazi, [60, 75],
        [
            .. Testa("AD 3 LIXH 1 - 2"),
            (52, 30, "LIXH AD 3.16 SPAZIO AEREO"), (52, 42, "LIXH AD 3.16 AIR TRAFFIC SERVICES AIRSPACE"),
            .. spazi.SkipLast(1).Select(x => (x + 4, 68.0, "NIL")),
        ]);

        // AD 3.18: un radiogoniometro VDF, col nome nella colonna della sigla.
        double[] radio = [52, 110, 200, 260, 300, 400, 440, 480, 565];
        var vdf = new Pagina(radio, [55, 90],
        [
            .. Testa("AD 3 LIXH 1 - 3"),
            (52, 30, "LIXH AD 3.18 RADIOASSISTENZE"), (52, 42, "LIXH AD 3.18 RADIO NAVIGATION AND LANDING AIDS"),
            (56, 68, "VDF"), (114, 68, "Prova Gonio Homer"), (204, 68, "118.500 MHZ"), (264, 68, "H24"),
            (304, 68, "45°00'00''N 009°00'00''E"), (404, 68, "NIL"), (444, 68, "NIL"), (484, 68, "NIL"),
        ]);

        var letto = ItalianAip.ReadFile("LI-AD 3 LIXH 1.pdf", PdfText.Read(Enr4Tests.Pdf(pagina, nil, vdf)));

        var s = Assert.Single(letto.Aerodromes);
        Assert.Equal([], letto.Issues.Where(i => i.Kind != ImportIssueKind.PageWithoutDate).Select(i => $"{i.Kind} {i.Key} {i.Detail}"));
        var gonio = Assert.Single(s.NavigationAids);
        Assert.Equal(("Prova Gonio Homer", NavaidKind.Vdf, "AD 3.18"), (gonio.Designator, gonio.Kind, gonio.Section));
        Assert.Equal(("Prova Eli", true), (s.Name, s.Heliport));
        Assert.Equal(41 + 51 / 60d + 59 / 3600d, s.Arp!.Value.Lat, 6);
        Assert.Equal(("4º E (2020)", "7'E", "VFR"), (s.MagneticVariation, s.AnnualChange, s.Traffic));
        Assert.Empty(letto.Airspaces);
        Assert.DoesNotContain(letto.Issues, i => i.Kind != ImportIssueKind.PageWithoutDate);
        Assert.Equal(["AD 3.1", "AD 3.2", "AD 3.16", "AD 3.18"], s.Sections!.Select(x => x.Number));
        Assert.Equal(("DATI AMMINISTRATIVI E GEOGRAFICI", "HELIPAD DATA", (string?)null), (s.Sections![1].Title, s.Sections[1].TitleEnglish, s.Sections[1].Text));
    }
}
