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

    // Segnalazioni 2611, revisione 4: gli scali minori numerano l'AD 2.2 diversamente (LIDT: la declinazione alla riga 4,
    // l'autorità alla 5, il traffico alla 6): la riga la dice l'etichetta. Prima la declinazione di Trento era l'indirizzo
    // dell'ENAC, e il fax «0461 946665 E-mail» si leggeva 946665° E.
    [Fact]
    public void IDatiDelloScalo_DalleEtichette()
    {
        var dati = new Pagina([52, 77, 257, 565], [100, 130, 160, 190, 220, 250, 280],
        [
            .. Testa("AD 2 LIXX 1 - 1"), (60, 80, "LIXX AD 2.2 DATI AMMINISTRATIVI E GEOGRAFICI DELL'AEROPORTO"),
            (55, 108, "1"), (80, 108, "Coordinate ARP"), (260, 108, "46°01'24''N 011°07'30''E"),
            (55, 138, "3"), (80, 138, "Elevazione/Temperatura di riferimento"), (260, 138, "610 FT / NIL"),
            (55, 168, "4"), (80, 168, "Declinazione magnetica/Variazione annuale"), (260, 168, "1° E (2005.0) / 5'E"),
            (55, 198, "5"), (80, 198, "Autorita amministrativa aeroportuale"), (260, 198, "ENAC fax +39 0461 946665 E-mail"),
            (55, 228, "6"), (80, 228, "Tipo di traffico consentito (IFR/VFR)"), (260, 228, "VFR"),
            (55, 258, "7"), (80, 258, "Note"), (260, 258, "1. AD aperto al traffico turistico"),
        ]);

        var s = Assert.Single(ItalianAd2.Read(PdfText.Read(Enr4Tests.Pdf(dati)), "LIXX", "PROVA").Aerodromes);

        Assert.Equal(("1º E (2005.0)", "5'E", "VFR"), (s.MagneticVariation, s.AnnualChange, s.Traffic));
        Assert.Equal("610 FT", s.Elevation);
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

    // Segnalazioni 2611, B7: in AD 2.17 il nome va a capo col tipo dello spazio («TORINO/Aeritalia» / «ATZ Sector 3»,
    // «Albenga/Riviera» / «Airport ATZ»): è nome, non geometria; e l'area è un'ATZ.
    [Fact]
    public void AD217_IlNomeCheVaACapoColTipo()
    {
        double[] c = [52, 160, 250, 320, 400, 470, 565];
        var pagina = new Pagina(c, [100, 130, 145, 215],
        [
            .. Testa("AD 2 LIXX 1 - 9"), (60, 80, "LIXX AD 2.17 SPAZI AEREI ATS"),
            (56, 110, "Designazione"),
            .. Enumerable.Range(0, 6).Select(i => (c[i] + 4, 132.0, (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))),
            (56, 150, "PROVA/Aeritalia"), (56, 160, "ATZ Sector 3"),
            (56, 170, "45°06'47\"N 007°35'30\"E;"), (56, 180, "45°08'00\"N 007°36'00\"E;"), (56, 190, "45°07'00\"N 007°38'00\"E;"),
            (56, 200, "45°06'47\"N 007°35'30\"E"),
            (164, 150, "3000 FT AMSL"), (164, 160, "SFC"), (254, 150, "G"), (324, 150, "Aeritalia Radio"), (404, 150, "6000 FT"), (474, 150, "NIL"),
        ]);

        var r = ItalianAd2.Read(PdfText.Read(Enr4Tests.Pdf(pagina)), "LIXX", "PROVA");

        var atz = Assert.Single(r.Airspaces);
        Assert.Equal(("PROVA/Aeritalia ATZ Sector 3", AirspaceType.Atz), (atz.Designator, atz.Type));
        Assert.DoesNotContain(r.Issues, i => i.Kind == ImportIssueKind.Geometry);
    }

    /// <summary>
    /// AD 2.19 (slice 5d): un VORTAC coi due apparati, un ILS — la riga «ILS RWY 28 LOC», sotto il DME-P con la sua sigla, il
    /// GP e l'OM senza —, e un DME da solo con un'altra sigla, che chiude l'ILS.
    /// </summary>
    private static ImportResult LeggiAd219()
    {
        double[] c = [52, 112, 152, 212, 262, 362, 412, 492, 565];
        var pagina = new Pagina(c, [100, 130, 145, 215, 245, 265, 290, 315, 335],
        [
            .. Testa("AD 2 LIXX 1 - 12"), (60, 80, "LIXX AD 2.19 RADIOASSISTENZE ALLA NAVIGAZIONE E ALL'ATTERRAGGIO"),
            (56, 110, "Tipo di"),
            .. Enumerable.Range(0, 8).Select(i => (c[i] + 4, 132.0, (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))),
            (56, 150, "VORTAC"), (56, 160, "(3° E-2010.0)"), (116, 150, "CDC"), (156, 150, "117.30"), (156, 160, "MHZ"), (156, 170, "CH 120X"),
            (216, 150, "VOR H24"), (216, 160, "TACAN H24"),
            (266, 150, "VOR"), (266, 160, "38°45'21.1''N"), (266, 170, "016°22'08.9''E"),
            (266, 180, "TACAN"), (266, 190, "38°45'21.1''N"), (266, 200, "016°22'09.0''E"),
            (366, 150, "998 M"), (366, 160, "AMSL"), (416, 150, "200 NM/60000 FT"), (496, 150, "1) MAINT"),
            (56, 218, "ILS RWY 28 LOC"), (56, 226, "CAT I"), (56, 234, "(3° E-2010.0)"), (116, 218, "LAM"), (156, 218, "110.30"), (156, 226, "MHZ"),
            (216, 218, "H24"), (266, 218, "38°54'26.3''N"), (266, 226, "016°13'33.5''E"), (366, 218, "NIL"), (416, 218, "NIL"),
            (496, 218, "1) Fascio posteriore"),
            (56, 248, "DME-P"), (116, 248, "LAM"), (156, 248, "CH 40X"), (216, 248, "H24"),
            (266, 248, "38°54'08.5''N"), (266, 256, "016°15'16.7''E"), (366, 248, "16 M AMSL"), (416, 248, "25 NM/10000 FT"), (496, 248, "NIL"),
            (56, 268, "GP"), (116, 268, "-"), (156, 268, "335.00"), (156, 276, "MHZ"), (216, 268, "H24"),
            (266, 268, "38°54'09.1''N"), (266, 276, "016°15'16.9''E"), (366, 268, "NIL"), (416, 268, "NIL"),
            (496, 268, "Slope 3°"), (496, 276, "RDH: 17 M"),
            (56, 293, "OM"), (116, 293, "-"), (156, 293, "75.00 MHZ"), (216, 293, "H24"),
            (266, 293, "38°53'00.0''N"), (266, 303, "016°20'00.0''E"), (366, 293, "NIL"), (416, 293, "NIL"), (496, 293, "NIL"),
            (56, 318, "DME"), (116, 318, "NOV"), (156, 318, "CH 53Y"), (216, 318, "H24"),
            (266, 318, "45°25'26.7''N"), (266, 326, "008°47'38.7''E"), (366, 318, "137 M AMSL"), (416, 318, "50 NM"), (496, 318, "NIL"),
        ]);
        return ItalianAd2.Read(PdfText.Read(Enr4Tests.Pdf(pagina)), "LIXX", "PROVA");
    }

    [Fact]
    public void LeRadioassistenze_CogliApparatiELaDeclinazione()
    {
        var r = LeggiAd219();
        var s = Assert.Single(r.Aerodromes);

        Assert.Equal(["CDC VORTAC", "NOV DME"], s.NavigationAids.Select(n => n.Key));
        var cdc = s.NavigationAids[0];
        Assert.Equal(("AD 2.19", NavaidKind.Vortac, "3º E-2010.0", "117.30 MHZ", "120X", "998 M AMSL"),
            (cdc.Section, cdc.Kind, cdc.MagneticVariation, cdc.Frequency, cdc.Channel, cdc.Elevation));
        Assert.Equal(["VOR", "TACAN"], cdc.Components.Select(a => a.Equipment));
        Assert.Equal(["H24", "H24"], cdc.Components.Select(a => a.Hours));
        Assert.Equal(16 + 22 / 60.0 + 9.0 / 3600, cdc.Components[1].Position.Lon, 9);
        Assert.Equal(("53Y", (string?)null), (s.NavigationAids[1].Channel, s.NavigationAids[1].Frequency));
        Assert.DoesNotContain(r.Issues, i => i.Kind == ImportIssueKind.Navaid);
    }

    /// <summary>
    /// La carta ATC SMAC: un settore, uno col nome e i rimandi («3000/4000 (5) NORTH-EAST») che continua nella pagina dopo
    /// con la quota ripetuta, e la nota «REMARK» in fondo che non è geometria.
    /// </summary>
    [Fact]
    public void LaSmac_ISettoriConLaQuotaIlNomeEIlSeguito()
    {
        double[] c = [52, 202, 565];
        var uno = new Pagina(c, [100, 130, 145, 210, 260],
        [
            .. Testa("AD 2 LIXX 8 - 3"), (60, 80, "ATC SURVEILLANCE MINIMUM ALTITUDE CHART"), (56, 110, "Minimum Altitudes/Levels"),
            (56, 132, "1"), (206, 132, "2"),
            (56, 150, "2000FT"), (206, 150, "Line joining following points:"), (206, 160, "45°00'00\"N 009°00'00\"E"),
            (206, 170, "45°00'00\"N 009°10'00\"E"), (206, 180, "45°10'00\"N 009°10'00\"E"), (206, 190, "45°00'00\"N 009°00'00\"E"),
            (56, 215, "3000/4000 (5) NORTH-EAST"), (206, 215, "Line joining following points:"), (206, 225, "45°20'00\"N 009°00'00\"E"),
            (206, 235, "45°20'00\"N 009°10'00\"E"),
        ]);
        var due = new Pagina(c, [100, 130, 145, 220],
        [
            .. Testa("AD 2 LIXX 8 - 4"), (56, 110, "Minimum Altitudes/Levels"), (56, 132, "1"), (206, 132, "2"),
            (56, 150, "3000/4000 (5) NORTH-EAST"), (206, 150, "45°30'00\"N 009°10'00\"E"), (206, 160, "45°20'00\"N 009°00'00\"E"),
            (206, 180, "REMARK"), (206, 190, "2000FT EXCLUDED"),
        ]);
        var pagine = PdfText.Read(Enr4Tests.Pdf(uno, due));

        Assert.True(ItalianAd2.ESmac(pagine));
        var r = ItalianAd2.ReadSmac(pagine, "LIXX");

        Assert.Equal(["2000FT", "3000/4000"], r.MinimumAltitudes.Select(m => m.Minimum.Text));
        var ne = r.MinimumAltitudes[1];
        Assert.Equal(("LIXX", "NORTH-EAST", "(5)"), (ne.Aerodrome, ne.Name, ne.NoteRefs));
        Assert.Equal(3, Assert.IsType<RingGeometry>(ne.Geometry).Segments.Count);
        Assert.Equal("AD 2 LIXX 8-3", ne.Page?.Reference);
        Assert.Empty(r.Issues);
    }

    // Segnalazioni 2611, B8: «FL 70/TRL» (LIRA), la seconda quota può essere il livello di transizione.
    [Fact]
    public void LaSmac_LaQuotaConIlLivelloDiTransizione()
    {
        double[] c = [52, 202, 565];
        var uno = new Pagina(c, [100, 130, 145, 210],
        [
            .. Testa("AD 2 LIXX 8 - 3"), (60, 80, "ATC SURVEILLANCE MINIMUM ALTITUDE CHART"), (56, 110, "Minimum Altitudes/Levels"),
            (56, 132, "1"), (206, 132, "2"),
            (56, 150, "FL 70/TRL"), (206, 150, "Line joining following points:"), (206, 160, "45°00'00\"N 009°00'00\"E"),
            (206, 170, "45°00'00\"N 009°10'00\"E"), (206, 180, "45°10'00\"N 009°10'00\"E"), (206, 190, "45°00'00\"N 009°00'00\"E"),
        ]);

        var r = ItalianAd2.ReadSmac(PdfText.Read(Enr4Tests.Pdf(uno)), "LIXX");

        Assert.Equal("FL 70/TRL", Assert.Single(r.MinimumAltitudes).Minimum.Text);
        Assert.Empty(r.Issues);
    }

    [Fact]
    public void LIls_IlLocalizzatoreEGliApparatiSotto_FinoAUnaSiglaDiversa()
    {
        var ils = Assert.Single(Assert.Single(LeggiAd219().Aerodromes).Ils);

        Assert.Equal(("28", "CAT I", "LAM", "3º E-2010.0"), (ils.Runway, ils.Category, ils.Designator, ils.MagneticVariation));
        Assert.Equal(("3º", "17 M"), (ils.Slope, ils.ReferenceDatumHeight));
        Assert.Equal(["LOC", "DME-P", "GP", "OM"], ils.Components.Select(a => a.Type));
        Assert.Equal(("110.30 MHZ", (string?)null), (ils.Components[0].Frequency, ils.Components[0].Channel));
        Assert.Equal(("40X", "16 M AMSL"), (ils.Components[1].Channel, ils.Components[1].Elevation));
        Assert.Equal("335.00 MHZ", ils.Components[2].Frequency);
        Assert.Equal(38 + 54 / 60.0 + 26.3 / 3600, ils.Components[0].Position.Lat, 9);
    }
}
