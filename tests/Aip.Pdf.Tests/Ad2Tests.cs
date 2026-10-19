using Aip.Pdf.Italia;
using Pagina = Aip.Pdf.Tests.Enr4Tests.Pagina;

namespace Aip.Pdf.Tests;

/// <summary>
/// AD 2 (carta F6, slice 5a): il testo di uno scalo, una sezione per pagina — AD 2.2 (la tabella numerata senza riga dei
/// numeri), AD 2.12 (le piste, la fine pista nella pagina dopo come riga senza designatore), AD 2.18 (le frequenze, e la
/// tabella che continua nella pagina dopo con le righe interne corte, come LIRF p.26). PDF costruiti qui
/// (<see cref="Enr4Tests.Pdf"/>). Dati inventati.
/// </summary>
public sealed class Ad2Tests
{
    private static (double, double, string)[] Testa(string riferimento, string data = "11 JUN 2026") =>
        [(450, 30, riferimento), (52, 45, "AIP - Italia AIRAC effective date " + data)];

    private static ImportResult Leggi()
    {
        var dati = new Pagina([52, 77, 257, 565], [100, 130, 160, 190, 220],
        [
            .. Testa("AD 2 LIXX 1 - 1"), (60, 80, "LIXX AD 2.2 DATI AMMINISTRATIVI E GEOGRAFICI DELL'AEROPORTO"),
            (55, 108, "1"), (80, 108, "Coordinate ARP"), (260, 108, "45°00'00''N 009°00'00''E"),
            (55, 138, "3"), (80, 138, "Elevazione"), (260, 138, "100 FT / 30.0° C"),
            (55, 168, "5"), (80, 168, "Declinazione"), (260, 168, "3° E (2020.0) / 7'E"),
            (55, 198, "7"), (80, 198, "Traffico"), (260, 198, "IFR/VFR"),
        ]);
        double[] piste = [52, 116, 176, 257, 371, 465, 565];
        var pista = new Pagina(piste, [100, 130, 145, 220, 260],
        [
            .. Testa("AD 2 LIXX 1 - 2"), (60, 80, "LIXX AD 2.12 CARATTERISTICHE FISICHE DELLE PISTE"),
            (60, 110, "Designazione"),
            .. Enumerable.Range(0, 6).Select(i => (piste[i] + 4, 132.0, (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))),
            (60, 150, "10"), (120, 150, "098.49°"), (180, 150, "3015 x 45"), (260, 150, "ASPH"),
            (375, 150, "45°00'00''N"), (375, 162, "009°00'00''E"), (375, 174, "45°00'00''N"), (375, 186, "009°02'00''E"),
            (375, 198, "140.2 FT"), (470, 150, "19.3 FT / NIL"),
            (60, 225, "28"), (120, 225, "278.51°"), (180, 225, "3015 x 45"), (260, 225, "ASPH"),
            (375, 225, "45°00'00''N"), (375, 237, "009°01'30''E"), (470, 225, "41.7 FT / 41.7 FT"),
        ]);
        // La fine della 28 nella pagina dopo: la stessa tabella, la riga senza designatore.
        var seguito = new Pagina(piste, [100, 130, 145, 195],
        [
            .. Testa("AD 2 LIXX 1 - 3"), (60, 110, "Designazione"),
            .. Enumerable.Range(0, 6).Select(i => (piste[i] + 4, 132.0, (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))),
            (375, 150, "45°00'00''N"), (375, 162, "009°00'00''E"), (375, 174, "139.8 FT"),
        ]);
        double[] radio = [52, 129, 231, 319, 421, 565];
        var frequenze = new Pagina(radio, [100, 130, 145, 160, 175],
        [
            .. Testa("AD 2 LIXX 1 - 4"), (60, 80, "LIXX AD 2.18 SERVIZI DI COMUNICAZIONE PER I SERVIZI DI TRAFFICO AEREO"),
            (60, 110, "Servizio"),
            .. Enumerable.Range(0, 5).Select(i => (radio[i] + 4, 132.0, (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))),
            (56, 148, "APP"), (133, 148, "Prova Radar"), (235, 148, "118.800"), (323, 148, "H24"), (425, 148, "NIL"),
            (56, 163, "TWR"), (133, 163, "Prova TWR"), (235, 163, "CH 125.630"), (323, 163, "H24"), (425, 163, "1) 8.33 KHz"),
        ]);
        // La pagina dopo: sfalsata di 20 punti, e disegnata solo coi bordi esterni e la prima riga interna (LIRF p.26).
        var continua = new Pagina([32, 109, 545], [46, 61, 76, 200],
        [
            .. Testa("AD 2 LIXX 1 - 5"),
            (36, 54, "TWR"), (113, 54, "Prova TWR"), (215, 54, "118.700"), (303, 54, "H24"), (405, 54, "NIL"),
            (36, 69, "NOTE ATIS"), (113, 69, "1. Sullo scalo è attivo il D-ATIS"),
        ]);
        return ItalianAd2.Read(PdfText.Read(Enr4Tests.Pdf(dati, pista, seguito, frequenze, continua)), "LIXX", "PROVA");
    }

    [Fact]
    public void IDatiDelloScalo()
    {
        var s = Assert.Single(Leggi().Aerodromes);

        Assert.Equal(("LIXX", "PROVA"), (s.Icao, s.Name));
        Assert.Equal(new Position(45, 9), s.Arp);
        Assert.Equal(("100 FT", "30.0º C"), (s.Elevation, s.ReferenceTemperature));
        Assert.Equal(("3º E (2020.0)", "7'E", "IFR/VFR"), (s.MagneticVariation, s.AnnualChange, s.Traffic));
        Assert.Equal("AD 2 LIXX 1-1", s.Page?.Reference);
    }

    [Fact]
    public void LePiste_LaSogliaLaFineEIlGeoide_AncheNellaPaginaDopo()
    {
        var piste = Assert.Single(Leggi().Aerodromes).Runways;

        Assert.Equal(["10", "28"], piste.Select(p => p.Designator));
        var dieci = piste[0];
        Assert.Equal(("098.49º", "3015 x 45", "ASPH", "19.3 FT / NIL", "140.2 FT"), (dieci.TrueBearing, dieci.Dimensions, dieci.Surface, dieci.ThresholdElevation, dieci.GeoidUndulation));
        Assert.Equal(new Position(45, 9), dieci.Threshold);
        Assert.Equal(9 + 2 / 60.0, dieci.End!.Value.Lon, 9);
        // La 28: la fine e il geoide stanno nella pagina dopo.
        Assert.Equal(new Position(45, 9), piste[1].End);
        Assert.Equal("139.8 FT", piste[1].GeoidUndulation);
    }

    [Fact]
    public void LeFrequenze_AncheDallaTabellaCheContinuaConMenoColonne_LeNoteNo()
    {
        var r = Leggi();
        var f = Assert.Single(r.Aerodromes).Communications;

        Assert.Equal(["118.800", "CH 125.630", "118.700"], f.Select(x => x.Frequency));
        Assert.Equal(("TWR", "Prova TWR", "H24", (string?)null), (f[2].Service, f[2].CallSign, f[2].Hours, f[2].Remarks));
        Assert.Equal("1) 8.33 KHz", f[1].Remarks);
        Assert.Empty(r.Issues);
    }
}
