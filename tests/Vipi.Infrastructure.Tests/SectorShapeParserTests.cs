using Vipi.Application.Abstractions;
using Vipi.Infrastructure.Sectorfile;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il parser dei poligoni di SETTORE (<c>DYNAMIC_SEC/*.tfl</c>: CTR/APP/MIL/FSS). Le forme provate sono
/// quelle vere, copiate dai file del repo <c>ivao-italy/it-aurora-sector</c> il 26 agosto 2026: su 20 692
/// righe ce ne sono esattamente tre — 20 459 vertici DMS, 233 nomi di punto, 112 intestazioni.
/// </summary>
public class SectorShapeParserTests
{
    /// <summary>Un catalogo coi punti che servono ai casi, con coordinate vere (da <c>itfix.fix</c>).</summary>
    private static NavaidCatalog Punti() => new(new[]
    {
        new NavaidName("TUFTE", NavaidKind.Fix, 43.5, 12.5),
        new NavaidName("AMSOR", NavaidKind.Fix, 44.0, 13.0),
        new NavaidName("PAN", NavaidKind.Vor, 36.814444, 11.965750),
        new NavaidName("SENZAPOS", NavaidKind.Fix),          // in catalogo ma senza coordinate
    });

    private const string TreVertici = """
        N044.23.16.000;E011.07.44.000;
        N044.18.17.000;E011.13.04.000;
        N044.14.48.000;E011.16.47.000;
        """;

    [Fact]
    public void Un_blocco_semplice_esce_come_anello()
    {
        var r = AuroraSectorfileParser.ParseSectorShapes(
            "LIRR_NE_CTR;CTR;1;CTR;1;\n" + TreVertici, Punti());

        var anello = Assert.Single(r.Rings);
        Assert.Equal("LIRR_NE_CTR", anello.Key);
        Assert.Equal(3, anello.Value.Count);
        Assert.Equal(44.387778, anello.Value[0].Lat, 5);
        Assert.Equal(11.128889, anello.Value[0].Lon, 5);
        Assert.Empty(r.UnresolvedPoints);
    }

    /// <summary>
    /// La forma che <c>ParseTowerShapes</c> non sa leggere: una shape sola per più enti. Sui file veri
    /// succede 16 volte su 112, fino a cinque callsign (<c>EDMM_CTR EDMM_S_CTR EDMM_FSS EDMM_MIL_CTR</c>).
    /// </summary>
    [Fact]
    public void Un_intestazione_con_piu_callsign_registra_l_anello_per_ognuno()
    {
        var r = AuroraSectorfileParser.ParseSectorShapes(
            "LIBB_ES_CTR LIBB_EU_CTR;CTR;1;CTR;1;\n" + TreVertici, Punti());

        Assert.Equal(new[] { "LIBB_ES_CTR", "LIBB_EU_CTR" }, r.Rings.Keys.OrderBy(k => k));
        Assert.Same(r.Rings["LIBB_ES_CTR"], r.Rings["LIBB_EU_CTR"]);   // è la stessa shape, non una copia
    }

    /// <summary>
    /// ⚠️ Il secondo separatore, e l'ha trovato solo la prova sui file veri: 16 intestazioni usano lo spazio,
    /// 3 i <b>due punti</b>. Leggendo solo lo spazio, quelle tre davano una chiave sola coi due punti dentro —
    /// quattro settori di Milano senza area, senza un errore da nessuna parte.
    /// </summary>
    [Fact]
    public void I_callsign_si_separano_anche_coi_due_punti()
    {
        var r = AuroraSectorfileParser.ParseSectorShapes(
            "LIMM_WS2_CTR:LIMM_WS5_CTR:LIMM_ES2_CTR:LIMM_ES5_CTR;CTR;1;CTR;1;\n" + TreVertici, Punti());

        Assert.Equal(4, r.Rings.Count);
        Assert.Contains("LIMM_ES5_CTR", r.Rings.Keys);
        Assert.DoesNotContain(r.Rings.Keys, k => k.Contains(':'));
    }

    /// <summary>Il commento a fine riga c'è davvero: <c>LIRR_NE_CTR;CTR;1;CTR;1; //NE cnf.1</c>.</summary>
    [Fact]
    public void Il_commento_a_fine_riga_non_entra_nel_callsign()
    {
        var r = AuroraSectorfileParser.ParseSectorShapes(
            "LIRR_NE_CTR;CTR;1;CTR;1; //NE cnf.1\n" + TreVertici, Punti());

        Assert.Equal("LIRR_NE_CTR", Assert.Single(r.Rings).Key);
    }

    // ---- i vertici per nome ---------------------------------------------------------------------------

    [Fact]
    public void Un_vertice_per_nome_si_risolve_col_catalogo()
    {
        var r = AuroraSectorfileParser.ParseSectorShapes(
            "LIRR_NE_CTR;CTR;1;CTR;1;\n" + TreVertici + "\nTUFTE;TUFTE;\nAMSOR;AMSOR;", Punti());

        var anello = r.Rings["LIRR_NE_CTR"];
        Assert.Equal(5, anello.Count);
        Assert.Equal((43.5, 12.5), anello[3]);
        Assert.Equal((44.0, 13.0), anello[4]);
        Assert.Empty(r.UnresolvedPoints);
    }

    /// <summary>
    /// ⚠️ Il caso che giustifica la regola: saltare il punto darebbe un poligono che si disegna benissimo e
    /// mente, con un lato dritto dove il confine gira. Si scarta il blocco INTERO.
    /// </summary>
    [Fact]
    public void Un_punto_che_non_si_risolve_butta_via_l_anello_intero()
    {
        var r = AuroraSectorfileParser.ParseSectorShapes(
            "LIRR_NE_CTR;CTR;1;CTR;1;\n" + TreVertici + "\nGEMLA;GEMLA;", Punti());

        Assert.Empty(r.Rings);
        var (punto, callsigns) = Assert.Single(r.UnresolvedPoints);
        Assert.Equal("GEMLA", punto);
        Assert.Equal("LIRR_NE_CTR", callsigns);
    }

    /// <summary>Un nome in catalogo ma senza coordinate è indistinguibile da uno che non c'è: per chi
    /// disegna sono la stessa cosa, e fingere di sapere dove sia sarebbe peggio.</summary>
    [Fact]
    public void Un_punto_senza_coordinate_conta_come_non_risolto()
    {
        var r = AuroraSectorfileParser.ParseSectorShapes(
            "LIRR_NE_CTR;CTR;1;CTR;1;\n" + TreVertici + "\nSENZAPOS;SENZAPOS;", Punti());

        Assert.Empty(r.Rings);
        Assert.Equal("SENZAPOS", Assert.Single(r.UnresolvedPoints).Point);
    }

    /// <summary>Un blocco rotto non deve portarsi via i vicini: gli altri escono lo stesso.</summary>
    [Fact]
    public void Un_blocco_rotto_non_ferma_gli_altri()
    {
        var tfl = "LIRR_A_CTR;CTR;1;CTR;1;\n" + TreVertici + "\nGEMLA;GEMLA;\n\n"
                + "LIRR_B_CTR;CTR;1;CTR;1;\n" + TreVertici;

        var r = AuroraSectorfileParser.ParseSectorShapes(tfl, Punti());

        Assert.Equal("LIRR_B_CTR", Assert.Single(r.Rings).Key);
        Assert.Equal("LIRR_A_CTR", Assert.Single(r.UnresolvedPoints).Callsigns);
    }

    [Fact]
    public void Senza_catalogo_i_blocchi_con_nomi_si_scartano_e_gli_altri_no()
    {
        var tfl = "LIRR_A_CTR;CTR;1;CTR;1;\n" + TreVertici + "\nTUFTE;TUFTE;\n\n"
                + "LIRR_B_CTR;CTR;1;CTR;1;\n" + TreVertici;

        var r = AuroraSectorfileParser.ParseSectorShapes(tfl, NavaidCatalog.Empty);

        Assert.Equal("LIRR_B_CTR", Assert.Single(r.Rings).Key);
        Assert.Equal("TUFTE", Assert.Single(r.UnresolvedPoints).Point);
    }

    // ---- bordi ----------------------------------------------------------------------------------------

    [Fact]
    public void Un_anello_di_meno_di_tre_punti_si_scarta() =>
        Assert.Empty(AuroraSectorfileParser.ParseSectorShapes(
            "LIRR_NE_CTR;CTR;1;CTR;1;\nN044.23.16.000;E011.07.44.000;\nN044.18.17.000;E011.13.04.000;",
            Punti()).Rings);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Vuoto_non_produce_niente(string? tfl)
    {
        var r = AuroraSectorfileParser.ParseSectorShapes(tfl, Punti());
        Assert.Empty(r.Rings);
        Assert.Empty(r.UnresolvedPoints);
    }

    [Fact]
    public void La_riga_vuota_chiude_il_blocco()
    {
        var tfl = "LIRR_A_CTR;CTR;1;CTR;1;\n" + TreVertici + "\n\nN045.00.00.000;E012.00.00.000;";

        var r = AuroraSectorfileParser.ParseSectorShapes(tfl, Punti());

        Assert.Equal(3, r.Rings["LIRR_A_CTR"].Count);   // il vertice orfano non è entrato
    }

    [Fact]
    public void I_callsign_escono_in_maiuscolo() =>
        Assert.Equal("LIRR_NE_CTR", Assert.Single(AuroraSectorfileParser.ParseSectorShapes(
            "lirr_ne_ctr;CTR;1;CTR;1;\n" + TreVertici, Punti()).Rings).Key);
}

/// <summary>
/// Le <b>coordinate</b> dei navaid: fino al 26 agosto 2026 non si leggevano, e senza di esse i 233 vertici
/// per nome dei poligoni di settore non si risolvono.
/// </summary>
public class NavaidCoordinateTests
{
    /// <summary>⚠️ I tre file mettono la coppia in colonne diverse: nei VOR e negli NDB c'è la frequenza in
    /// mezzo. È il motivo per cui il parser la cerca invece di prenderla a indice fisso.</summary>
    [Fact]
    public void Legge_le_coordinate_da_tutti_e_tre_i_file()
    {
        var cat = AuroraSectorfileParser.ParseNavaids(
            fixText: "ABADI;N040.45.19.000;E018.38.30.000;2;1;",
            vorText: "AEA;111.65;N040.38.17.400;E008.17.30.400;0;2;54Y;",
            ndbText: "PAN;335.0;N036.48.40.900;E011.57.39.700;");

        Assert.True(cat.TryGetPoint("ABADI", out var fix));
        Assert.Equal(40.755278, fix.Lat, 5);
        Assert.Equal(18.641667, fix.Lon, 5);

        Assert.True(cat.TryGetPoint("AEA", out var vor));
        Assert.Equal(40.638167, vor.Lat, 5);

        Assert.True(cat.TryGetPoint("PAN", out var ndb));
        Assert.Equal(36.811361, ndb.Lat, 5);

        Assert.Equal(3, cat.PointsWithPosition);
    }

    [Fact]
    public void Una_riga_senza_coordinate_da_comunque_il_nome()
    {
        var cat = AuroraSectorfileParser.ParseNavaids(fixText: "SOLONOME;", vorText: null);

        Assert.Contains("SOLONOME", cat.Names);          // la completion delle SID funziona lo stesso
        Assert.False(cat.TryGetPoint("SOLONOME", out _));
        Assert.Equal(0, cat.PointsWithPosition);
    }

    [Fact]
    public void I_commenti_non_diventano_punti()
    {
        var cat = AuroraSectorfileParser.ParseNavaids(fixText: "//++++FIX ESTERNI++++\nABADI;N040.45.19.000;E018.38.30.000;", vorText: null);

        Assert.Equal(new[] { "ABADI" }, cat.Names);
    }

    /// <summary>Su un omonimo vince la radioassistenza, ed è la regola di prima: le coordinate la seguono.</summary>
    [Fact]
    public void Su_un_omonimo_vincono_le_coordinate_del_vor()
    {
        var cat = AuroraSectorfileParser.ParseNavaids(
            fixText: "PAN;N010.00.00.000;E010.00.00.000;",
            vorText: "PAN;116.10;N036.48.52.000;E011.57.56.700;");

        Assert.True(cat.TryGetPoint("PAN", out var p));
        Assert.Equal(36.814444, p.Lat, 5);
    }
}
