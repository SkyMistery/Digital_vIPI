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
}
