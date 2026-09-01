using Vipi.Infrastructure.Sectorfile;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// I tre file del sectorfile che descrivono le cose che <b>anche</b> i cataloghi IVAO tengono: posizioni
/// (<c>itfreq.frq</c>), aeroporti (<c>itap.ap</c>), piste (<c>itrw.rw</c>).
///
/// <para>⚠️ Le righe delle fixture sono <b>ritagliate dai file veri</b> del 1 settembre 2026, comprese le
/// stranezze: l'intestazione a barre, la lista di visibilità con le esclusioni <c>-XXX</c>, la TA a zero e
/// le pseudo-piste <c>MAPS</c>. Una fixture ripulita proverebbe il parser contro un formato che non esiste.</para>
///
/// <para>Carta: <c>docs/design/piano-coerenza-sectorfile.md</c>.</para>
/// </summary>
public class AuroraSectorfileFactsParserTests
{
    private const string Frequenze = """
        /////////////////////MILANO//////////////////////////////
        //MILANO ACC
        LIMM_WS2_CTR;135.455;LILA LILE LIMA LIRR_MIL_CTR -LIMC_ASW_APP;PREFS\CTR.cpr;;0;;datis-acc.datis
        LIMM_FSS;124.925;LILA LIMM;PREFS\CTR.cpr;;1;;datis.datis

        //MILANO APP
        LIMC_ANE_APP;126.750;LILE LIMC;PREFS\TMA.cpr;limc.atis;0;;datis-arrdep.datis
        """;

    private const string Aeroporti = """
        LIAA;380;0;N042.34.24.000;E012.35.04.000;(AVIOSUPERFICIE);
        LIBA;182;7000;N041.32.29.010;E015.43.05.100;AMENDOLA;
        LIRF;14;6000;N041.48.01.000;E012.14.20.000;FIUMICINO;
        """;

    private const string Piste = """
        /////////////////////
        //MENU MAPPE

        LIRF;MAPS;;0;0;0;0;N000.00.00.000;E000.00.00.000;N000.00.00.000;E000.00.00.000;
        LIAA;09;27;113;113;095;275;N042.34.24.770;E012.34.54.460;N042.34.23.170;E012.35.19.600;
        LIRF;16L;34R;14;6;158.7;338.7;N041.50.45.490;E012.15.41.380;N041.48.44.800;E012.16.31.890;
        """;

    // ------------------------------------------------------------------------------------- posizioni

    [Fact]
    public void Le_posizioni_portano_callsign_e_frequenza()
    {
        var p = AuroraSectorfileParser.ParseAtcPositions(Frequenze);

        Assert.Equal(3, p.Count);
        Assert.Equal("LIMM_WS2_CTR", p[0].Callsign);
        Assert.Equal("135.455", p[0].Frequency);
        Assert.Equal("126.750", p[2].Frequency);
    }

    /// <summary>⚠️ Le righe di commento intestano i blocchi del file (<c>//MILANO ACC</c>): senza scartarle
    /// diventerebbero posizioni con un callsign fatto di barre — è già successo sui file dei punti, e si è
    /// visto solo quando sono comparse in cima a un elenco a discesa.</summary>
    [Fact]
    public void I_commenti_non_diventano_posizioni()
    {
        var p = AuroraSectorfileParser.ParseAtcPositions(Frequenze);
        Assert.DoesNotContain(p, x => x.Callsign.StartsWith('/'));
    }

    [Fact]
    public void Un_file_assente_non_e_un_errore()
    {
        Assert.Empty(AuroraSectorfileParser.ParseAtcPositions(null));
        Assert.Empty(AuroraSectorfileParser.ParseAirports(null));
        Assert.Empty(AuroraSectorfileParser.ParseRunwayEnds(""));
    }

    // ------------------------------------------------------------------------------------- aeroporti

    [Fact]
    public void Gli_aeroporti_portano_elevazione_ta_e_coordinate()
    {
        var a = AuroraSectorfileParser.ParseAirports(Aeroporti);

        var lirf = Assert.Single(a, x => x.Icao == "LIRF");
        Assert.Equal(14, lirf.ElevationFt);
        Assert.Equal(6000, lirf.TransitionAltitudeFt);
        Assert.Equal(41.8003, lirf.Lat!.Value, 3);
        Assert.Equal(12.2389, lirf.Lon!.Value, 3);
        Assert.Equal("FIUMICINO", lirf.Name);
    }

    /// <summary>
    /// ⚠️ <b>Zero non è una TA.</b> Nel file significa «non dichiarata» — 24 aeroporti su 130 — e se
    /// arrivasse al confronto come il numero 0 ogni aviosuperficie diventerebbe «TA divergente da 6000».
    /// </summary>
    [Fact]
    public void La_ta_a_zero_significa_non_dichiarata()
    {
        var a = AuroraSectorfileParser.ParseAirports(Aeroporti);
        Assert.Null(Assert.Single(a, x => x.Icao == "LIAA").TransitionAltitudeFt);
        Assert.Equal(7000, Assert.Single(a, x => x.Icao == "LIBA").TransitionAltitudeFt);
    }

    // ----------------------------------------------------------------------------------------- piste

    /// <summary>Ogni riga del file descrive <b>due</b> soglie, e diventa due righe.</summary>
    [Fact]
    public void Una_riga_di_pista_fa_due_estremita()
    {
        var r = AuroraSectorfileParser.ParseRunwayEnds(Piste);

        Assert.Equal(4, r.Count);
        var lirf = r.Where(x => x.Icao == "LIRF").ToList();
        Assert.Equal(new[] { "16L", "34R" }, lirf.Select(x => x.Ident));
        Assert.Equal(14, lirf[0].ThresholdElevationFt);
        Assert.Equal(6, lirf[1].ThresholdElevationFt);
        Assert.Equal(41.8460, lirf[0].ThresholdLat!.Value, 3);
    }

    /// <summary>
    /// ⚠️ Le <b>96</b> righe <c>MAPS</c> non sono piste: sono l'hack italiano che costruisce le voci di menu
    /// delle mappe (§6.1 di <c>STATO_SECTORFILE_ITALIANO.md</c>), e hanno coordinate a zero. Chi non le
    /// scarta apre 96 rilievi falsi al primo giro.
    /// </summary>
    [Fact]
    public void Le_pseudo_piste_maps_non_sono_piste()
    {
        var r = AuroraSectorfileParser.ParseRunwayEnds(Piste);
        Assert.DoesNotContain(r, x => x.Ident.StartsWith("MAPS", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(r, x => x.ThresholdLat == 0);
    }

    /// <summary>
    /// ⚠️ Lo zero iniziale c'è nel sectorfile e non nei dati IVAO: <c>09</c> e <c>9</c> sono la stessa pista.
    /// Misurato il 1 settembre 2026: senza normalizzare, ~40 piste risultano «assenti da una parte» per una
    /// cifra, e le dodici divergenze vere spariscono nel rumore.
    /// </summary>
    [Fact]
    public void Lo_zero_iniziale_dell_ident_si_normalizza()
    {
        var r = AuroraSectorfileParser.ParseRunwayEnds(Piste);
        Assert.Contains(r, x => x.Icao == "LIAA" && x.Ident == "9");
        Assert.DoesNotContain(r, x => x.Ident == "09");
    }

    [Theory]
    [InlineData("09", "9")]
    [InlineData("9", "9")]
    [InlineData("03L", "3L")]
    [InlineData("16R", "16R")]
    [InlineData("", "")]
    [InlineData(null, "")]
    // ⚠️ Un ident di una cifra sola resta com'è: «0» non è una pista, ma toglierle lo zero la farebbe
    // sparire del tutto invece di lasciarla visibile a chi guarda.
    [InlineData("0", "0")]
    public void La_normalizzazione_dell_ident(string? dentro, string atteso) =>
        Assert.Equal(atteso, AuroraSectorfileParser.NormalizzaIdentPista(dentro));
}
