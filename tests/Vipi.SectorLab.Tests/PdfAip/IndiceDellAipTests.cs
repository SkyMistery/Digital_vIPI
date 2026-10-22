using Aip;
using Vipi.SectorLab.Core.PdfAip;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>L'indice dell'AIP espanso (riordino delle pagine, R3, D9): le voci dello strato messe come nell'AIP.</summary>
public sealed class IndiceDellAipTests
{
    private static VoceDellAip Area(string chiave, string sezione, string genere) =>
        new(genere, chiave, [("Tipo", genere), ("Sezione", sezione)], [], CorrectionKind.Airspace, chiave);

    private static VoceDellAip Rotta(string nome, string sezione) =>
        new(AipSullaMappa.Rotte, nome, [("Sezione", sezione)], [], CorrectionKind.Route, nome);

    private static VoceDellAip Scalo(string icao, string nome) =>
        new(AipSullaMappa.Scali, icao, [("Sezione", "AD 2"), ("Nome", nome)], [], CorrectionKind.Aerodrome, icao);

    private static VoceDellAip Procedura(string scalo, string genere, string titolo) =>
        new(genere, titolo, [("Scalo", scalo), ("Sezione", "SID RNAV1 RWY 16L - Description")], []);

    private static readonly IReadOnlyList<VoceDellAip> Voci =
    [
        Area("LI R12", "ENR 5.1", "R"),             // 0
        Area("LI P2", "ENR 5.1", "P"),              // 1
        Area("Roma CTR", "ENR 2.1", "CTR"),         // 2
        Rotta("M726", "ENR 3.3"),                   // 3
        Rotta("M72", "ENR 3.3"),                    // 4
        Rotta("A1", "ENR 3.1"),                     // 5
        Scalo("LIRF", "Roma Fiumicino"),            // 6
        Procedura("LIRF", AipSullaMappa.Sid, "LIRF BOL5A RWY 16L"),  // 7
        Procedura("LIRF", AipSullaMappa.Star, "LIRF ELKAP2A"),       // 8
        Scalo("LIBD", "Bari"),                      // 9
        Area("LI R2", "ENR 5.1", "R"),              // 10
    ];

    [Fact]
    public void LeParti_NellOrdineDellAip()
    {
        var indice = IndiceDellAip.Di(Voci);

        Assert.Equal(["ENR 2", "ENR 3", "ENR 5", "AD 2"], indice.Select(p => p.Codice));
        Assert.Equal("Spazi aerei ATS", indice[0].Nome);
        Assert.Equal(Voci.Count, indice.Sum(p => p.Quante));
    }

    [Fact]
    public void LeSezioni_ELeVoci_ConINumeriComeNumeri()
    {
        var enr3 = IndiceDellAip.Di(Voci).Single(p => p.Codice == "ENR 3");

        Assert.Equal(["ENR 3.1", "ENR 3.3"], enr3.Gruppi.Select(g => g.Codice));
        Assert.Equal("", enr3.Gruppi[1].Nome); // le sottosezioni di ENR 3 col solo codice: niente nomi inventati
        // M72 prima di M726: i numeri si confrontano come numeri.
        Assert.Equal(["M72", "M726"], enr3.Gruppi[1].Voci.Select(v => v.Titolo));
        Assert.Equal(4, enr3.Gruppi[1].Voci[0].Indice);
    }

    [Fact]
    public void UnaSezioneConPiuGeneri_UnSottogruppoPerGenere()
    {
        var enr51 = IndiceDellAip.Di(Voci).Single(p => p.Codice == "ENR 5").Gruppi.Single();

        Assert.Equal("ENR 5.1", enr51.Codice);
        Assert.Empty(enr51.Voci);
        Assert.Equal(["R", "P"], enr51.Gruppi.Select(g => g.Nome));
        Assert.Equal(["LI R2", "LI R12"], enr51.Gruppi[0].Voci.Select(v => v.Titolo));
    }

    [Fact]
    public void Ad2_ScaloPerScalo_ConLoScaloInTestaELeProcedurePerGenere()
    {
        var ad2 = IndiceDellAip.Di(Voci).Single(p => p.Codice == "AD 2");

        Assert.Equal(["LIBD", "LIRF"], ad2.Gruppi.Select(g => g.Codice));
        var lirf = ad2.Gruppi[1];
        Assert.Equal("Roma Fiumicino", lirf.Nome);
        Assert.Equal([6], lirf.Voci.Select(v => v.Indice));
        Assert.Equal([AipSullaMappa.Sid, AipSullaMappa.Star], lirf.Gruppi.Select(g => g.Nome));
        // Bari ha solo la voce dello scalo: niente sottogruppi.
        Assert.Empty(ad2.Gruppi[0].Gruppi);
    }

    [Fact]
    public void IlFiltro_TieneSoloLeVociCheLoContengono()
    {
        var indice = IndiceDellAip.Di(Voci, "m72");

        var parte = Assert.Single(indice);
        Assert.Equal(["M72", "M726"], parte.Gruppi.Single().Voci.Select(v => v.Titolo));
        Assert.Empty(IndiceDellAip.Di(Voci, "nessuna"));

        // In AD 2 si cerca anche nel nome dello scalo: «fiumicino» trova LIRF con le sue procedure.
        var lirf = Assert.Single(Assert.Single(IndiceDellAip.Di(Voci, "fiumicino")).Gruppi);
        Assert.Equal("LIRF", lirf.Codice);
        Assert.Equal(3, lirf.Quante);
    }

    [Fact]
    public void LeAtzDiAd2_VannoNelLoroScaloPerNome_OInUnGruppoConLaSezione()
    {
        IReadOnlyList<VoceDellAip> voci =
        [
            Scalo("LIRF", "ROMA Fiumicino"),
            Scalo("LIRN", "NAPOLI Capodichino"),
            Area("Roma Fiumicino ATZ", "AD 2.17", "ATZ"),
            Area("Napoli ATZ", "AD 2.17", "ATZ"),
            Area("Pippo ATZ", "AD 2.17", "ATZ"),
        ];

        var ad2 = Assert.Single(IndiceDellAip.Di(voci));

        Assert.Equal(["LIRF", "LIRN", "AD 2.17"], ad2.Gruppi.Select(g => g.Codice));
        Assert.Equal(["Roma Fiumicino ATZ"], ad2.Gruppi[0].Gruppi.Single().Voci.Select(v => v.Titolo));
        Assert.Equal(["Napoli ATZ"], ad2.Gruppi[1].Gruppi.Single().Voci.Select(v => v.Titolo));
        Assert.Equal(["Pippo ATZ"], ad2.Gruppi[2].Voci.Select(v => v.Titolo));
    }

    [Fact]
    public void IPuntiRnavDiUnoScalo_VannoNelloScalo_NonInAltro()
    {
        // Nel modello la sezione dei punti RNAV di scalo è «AD 2 LIRF» (F7, 7a): 233 punti su 2610 finivano in «Altro».
        IReadOnlyList<VoceDellAip> voci =
        [
            Scalo("LIRF", "ROMA Fiumicino"),
            new(AipSullaMappa.Punti, "RF401", [("Sezione", "AD 2 LIRF")], [], CorrectionKind.Point, "RF401"),
        ];

        var ad2 = Assert.Single(IndiceDellAip.Di(voci));

        Assert.Equal("AD 2", ad2.Codice);
        Assert.Equal(["RF401"], Assert.Single(ad2.Gruppi).Gruppi.Single().Voci.Select(v => v.Titolo));
    }

    // Carta «L'AIP intero», passo 4: gli ostacoli per regione, la sottosezione dell'AIP col nome della regione; ENR 5.5 col
    // nome dell'ICAO.
    [Fact]
    public void GliOstacoliPerRegione_EIlNomeDellaRegione()
    {
        static VoceDellAip Ostacolo(string sigla, string sezione, string regione) =>
            new(AipSullaMappa.Ostacoli, sigla, [("Sezione", sezione), ("Regione", regione)], []);
        VoceDellAip[] voci =
        [
            Ostacolo("T000002", "ENR 5.4.1.13", "PUGLIA"), Ostacolo("T000001", "ENR 5.4.1.2", "BASILICATA"),
            Ostacolo("T000003", "ENR 5.4.1.13", "PUGLIA"), Area("ACRO", "ENR 5.5", "Sport"),
        ];

        var enr5 = IndiceDellAip.Di(voci).Single(p => p.Codice == "ENR 5");

        Assert.Equal(["ENR 5.4.1.2", "ENR 5.4.1.13", "ENR 5.5"], enr5.Gruppi.Select(g => g.Codice));
        Assert.Equal(["BASILICATA", "PUGLIA", "Attività aeree sportive e ricreative"], enr5.Gruppi.Select(g => g.Nome));
        Assert.Equal(["T000002", "T000003"], enr5.Gruppi[1].Voci.Select(v => v.Titolo));
    }

    // Passo 4: una sigla di ostacolo in due regioni si dice (nel 2611 T001645, in Puglia e in Sicilia).
    [Fact]
    public void UnaSiglaInDueRegioni_SiDice()
    {
        static Obstacle O(string sigla, string regione) => new()
        {
            Section = "ENR 5.4.1", Kind = ObstacleKind.Point, Designator = sigla, Region = regione, Position = new Position(40, 15),
            Page = new AipPage($"ENR 5.4.1-{regione.Length}", null),
        };

        var doppie = AipEspanso.SigleDoppie([O("T001645", "PUGLIA"), O("T000001", "PUGLIA"), O("T001645", "SICILIA")]).ToList();

        var d = Assert.Single(doppie);
        Assert.Equal(("T001645", "la stessa sigla 2 volte: PUGLIA ENR 5.4.1-6, SICILIA ENR 5.4.1-7"), (d.Problema.Key, d.Problema.Detail));
    }

    // Passo 5: un eliporto (la sua sezione è «AD 3») ha la parte sua, «AD 3 Eliporti», dopo gli aeroporti.
    [Fact]
    public void GliEliporti_NellaParteAd3()
    {
        VoceDellAip[] voci = [Scalo("LIRF", "Roma Fiumicino"), new(AipSullaMappa.Scali, "LIBI", [("Sezione", "AD 3"), ("Nome", "Vieste")], [], CorrectionKind.Aerodrome, "LIBI")];

        var indice = IndiceDellAip.Di(voci);

        Assert.Equal(["AD 2", "AD 3"], indice.Select(p => p.Codice));
        Assert.Equal(("Eliporti", "LIBI", "Vieste"), (indice[1].Nome, indice[1].Gruppi[0].Codice, indice[1].Gruppi[0].Nome));
    }

    // Passo 6: i punti, le procedure e le attese di un eliporto stanno in AD 3, sotto l'eliporto.
    [Fact]
    public void LeVociDiUnEliporto_InAd3()
    {
        VoceDellAip[] voci =
        [
            Scalo("LIRF", "Roma Fiumicino"),
            new(AipSullaMappa.Scali, "LIBI", [("Sezione", "AD 3"), ("Nome", "Vieste")], [], CorrectionKind.Aerodrome, "LIBI"),
            new(AipSullaMappa.Punti, "BI106", [("Sezione", "AD 3 LIBI")], [], CorrectionKind.Point, "BI106"),
            new(AipSullaMappa.Attese, "ESCOB FL105 L 145°", [("Sezione", "AD 3 LIBI")], [], CorrectionKind.Holding, "AD 3 LIBI ESCOB 145 left"),
            new(AipSullaMappa.Attese, "LORNI 3000 R 120°", [("Sezione", "AD 2 LIRF")], [], CorrectionKind.Holding, "AD 2 LIRF LORNI 120 right"),
        ];

        var indice = IndiceDellAip.Di(voci);

        Assert.Equal(["AD 2", "AD 3"], indice.Select(p => p.Codice));
        var libi = Assert.Single(indice[1].Gruppi);
        Assert.Equal("LIBI", libi.Codice);
        Assert.Equal(["BI106", "ESCOB FL105 L 145°"], libi.Gruppi.SelectMany(g => g.Voci).Select(v => v.Titolo));
        Assert.Equal("LORNI 3000 R 120°", Assert.Single(indice[0].Gruppi.Single().Gruppi.Single(g => g.Nome == AipSullaMappa.Attese).Voci).Titolo);
    }

    // Passo 6: un avvicinamento stampato senza titolo ha per nome la pagina della carta; prende il designatore dal titolo della
    // carta in AD 2.24, e una FIX/POINT da sola va nell'avvicinamento che c'è già.
    [Fact]
    public void GliAvvicinamentiSenzaTitolo_IlNomeDallaCarta()
    {
        var scalo = new Aerodrome
        {
            Icao = "LIXX", Name = "PROVA",
            Charts =
            [
                new AerodromeChart("ICAO INSTRUMENT APPROACH CHART ILS Z RWY 24", "AD 2 LIXX 5-1"),
                new AerodromeChart("ICAO INSTRUMENT APPROACH CHART VOR Y RWY 06", "AD 2 LIXX 5-5"),
            ],
        };
        var fissi = new List<ApproachFix> { new("IAF", new Position(41, 14)) };
        List<Procedure> procedure =
        [
            new() { Aerodrome = "LIXX", Kind = ProcedureKind.Approach, Designator = "ILS Z RWY24", Legs = [new ProcedureLeg { PathTerminator = "IF", Fix = "BENTO" }] },
            new() { Aerodrome = "LIXX", Kind = ProcedureKind.Approach, Designator = "AD 2 LIXX 5-1", Section = "FIX/POINT", Fixes = fissi },
            new() { Aerodrome = "LIXX", Kind = ProcedureKind.Approach, Designator = "AD 2 LIXX 5-5", Section = "FIX/POINT", Fixes = fissi },
            new() { Aerodrome = "LIXX", Kind = ProcedureKind.Approach, Designator = "AD 2 LIXX 5-9", Section = "FIX/POINT", Fixes = fissi },
        ];

        var nomi = AipEspanso.NomiDegliAvvicinamenti(procedure, [scalo]);

        Assert.Equal(["ILS Z RWY24", "VOR Y RWY06", "AD 2 LIXX 5-9"], nomi.Select(p => p.Designator));
        Assert.Equal(fissi, nomi[0].Fixes);
        Assert.Equal("ICAO INSTRUMENT APPROACH CHART VOR Y RWY 06", nomi[1].Section);
    }

    // Passo 5: le carte di AD 2.24 prendono la data della loro pagina dall'indice delle pagine; una carta senza la pagina si dice.
    [Fact]
    public void LeCarte_LaDataDallIndiceDellePagine()
    {
        var scalo = new Aerodrome
        {
            Icao = "LIXX", Name = "PROVA",
            Charts = [new AerodromeChart("AERODROME CHART ICAO", "AD 2 LIXX 2-1"), new AerodromeChart("SID RNAV1", "AD 2 LIXX 6-1")],
        };
        var problemi = new List<ProblemaDellImport>();

        var con = AipEspanso.DateDelleCarte([scalo], [new Aip.Pdf.IndexedPage("LI-AD 2 LIXX - PROVA 2.pdf", 1, "AD 2 LIXX 2-1", new DateOnly(2026, 6, 11))], problemi);

        Assert.Equal(new DateOnly(2026, 6, 11), con[0].Charts![0].Effective);
        Assert.Null(con[0].Charts![1].Effective);
        Assert.Equal("carta elencata senza la sua pagina fra i PDF: SID RNAV1 (AD 2 LIXX 6-1)", Assert.Single(problemi).Problema.Detail);
    }
}
