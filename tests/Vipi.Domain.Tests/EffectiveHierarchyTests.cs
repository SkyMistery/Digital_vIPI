using Vipi.Domain;
using Vipi.Domain.Services;
using Xunit;

namespace Vipi.Domain.Tests;

/// <summary>
/// L'albero di copertura effettivo, e il caso che l'ha fatto nascere: <b>LIMF, 31 agosto 2026</b>, dove un
/// settore risultava nipote di sé stesso su `atc.it.ivao.aero`.
///
/// <para>Carta <c>docs/feature/2026-08-31-ricaduta-verticale-e-cicli.md</c> §1.</para>
/// </summary>
public class EffectiveHierarchyTests
{
    private static HierarchyCatalogRow App(string cs, string? padre, string icao) =>
        new(cs, padre, icao, SectorType.App, IsHidden: false);
    private static HierarchyCatalogRow Twr(string cs, string? padre, string icao) =>
        new(cs, padre, icao, SectorType.Twr, IsHidden: false);
    private static HierarchyCatalogRow Ctr(string cs, string? padre) =>
        new(cs, padre, null, SectorType.Ctr, IsHidden: false);

    // =====================================================================================================
    //  Il caso di produzione
    // =====================================================================================================

    /// <summary>
    /// La configurazione esatta vista in produzione: l'aeroporto pende da una PROPRIA APP, quell'APP ha come
    /// padre scritto una seconda APP dello stesso scalo, e la seconda non ha padre scritto. Prima della
    /// correzione la seconda usciva su <c>airportParent</c> — cioè su una propria discendente.
    /// </summary>
    [Fact]
    public void LIMF_il_caso_di_produzione_non_produce_piu_un_anello()
    {
        var righe = new[]
        {
            App("LIMF_WW0_APP", padre: null, "LIMF"),              // «inherited» in pagina
            App("LIMF_WN0_APP", padre: "LIMF_WW0_APP", "LIMF"),
            Twr("LIMF_TWR", padre: null, "LIMF"),
            Ctr("LIMM_WS2_CTR", padre: null),
        };
        var padreScalo = new Dictionary<string, string?> { ["LIMF"] = "LIMF_WN0_APP" };

        var mappa = EffectiveHierarchy.ParentMap(righe, padreScalo);

        // WW0 non può pendere da WN0: WN0 pende da lui.
        Assert.Null(mappa["LIMF_WW0_APP"]);
        Assert.Equal("LIMF_WW0_APP", mappa["LIMF_WN0_APP"]);
        // La torre sale di gradino e trova la radice del gruppo APP — non il padre dello scalo: la scaletta
        // continua a funzionare, la correzione tocca solo l'uscita in fondo.
        Assert.Equal("LIMF_WW0_APP", mappa["LIMF_TWR"]);
    }

    /// <summary>Con il padre scritto (com'è ancora in sviluppo) non cambia niente: la correzione non tocca il caso sano.</summary>
    [Fact]
    public void LIMF_con_il_padre_scritto_resta_come_prima()
    {
        var righe = new[]
        {
            App("LIMF_WW0_APP", padre: "LIMM_WS2_CTR", "LIMF"),
            App("LIMF_WN0_APP", padre: "LIMF_WW0_APP", "LIMF"),
            Ctr("LIMM_WS2_CTR", padre: null),
        };
        var padreScalo = new Dictionary<string, string?> { ["LIMF"] = "LIMF_WN0_APP" };

        var mappa = EffectiveHierarchy.ParentMap(righe, padreScalo);

        Assert.Equal("LIMM_WS2_CTR", mappa["LIMF_WW0_APP"]);
    }

    // =====================================================================================================
    //  La regola in generale
    // =====================================================================================================

    /// <summary>Il caso più corto: l'aeroporto pende dalla posizione per cui si sta derivando.</summary>
    [Fact]
    public void Il_padre_dello_scalo_non_puo_essere_la_posizione_stessa()
    {
        var righe = new[] { App("LIPX_ES0_APP", padre: null, "LIPX") };
        var padreScalo = new Dictionary<string, string?> { ["LIPX"] = "LIPX_ES0_APP" };

        Assert.Null(EffectiveHierarchy.ParentMap(righe, padreScalo)["LIPX_ES0_APP"]);
    }

    /// <summary>Anche a due salti di distanza: A → B → C, e lo scalo pende da C.</summary>
    [Fact]
    public void Il_padre_dello_scalo_non_puo_essere_un_discendente_lontano()
    {
        var righe = new[]
        {
            App("LIPZ_A_APP", padre: null, "LIPZ"),
            App("LIPZ_B_APP", padre: "LIPZ_A_APP", "LIPZ"),
            App("LIPZ_C_APP", padre: "LIPZ_B_APP", "LIPZ"),
        };
        var padreScalo = new Dictionary<string, string?> { ["LIPZ"] = "LIPZ_C_APP" };

        Assert.Null(EffectiveHierarchy.ParentMap(righe, padreScalo)["LIPZ_A_APP"]);
    }

    /// <summary>Il caso normale — lo scalo pende da un settore d'area — deve continuare a funzionare.</summary>
    [Fact]
    public void Il_padre_dello_scalo_fuori_dallo_scalo_resta_il_ripiego()
    {
        var righe = new[] { App("LIPE_W_APP", padre: null, "LIPE"), Ctr("LIMM_WS2_CTR", padre: null) };
        var padreScalo = new Dictionary<string, string?> { ["LIPE"] = "LIMM_WS2_CTR" };

        Assert.Equal("LIMM_WS2_CTR", EffectiveHierarchy.ParentMap(righe, padreScalo)["LIPE_W_APP"]);
    }

    /// <summary>La scaletta continua a valere: DEL → GND → TWR → APP.</summary>
    [Fact]
    public void La_scaletta_sale_di_gradino()
    {
        var righe = new[]
        {
            new HierarchyCatalogRow("LIPQ_DEL", null, "LIPQ", SectorType.Del, false),
            new HierarchyCatalogRow("LIPQ_GND", null, "LIPQ", SectorType.Gnd, false),
            Twr("LIPQ_TWR", padre: null, "LIPQ"),
            App("LIPQ_APP", padre: null, "LIPQ"),
            Ctr("LIPP_CTR", padre: null),
        };
        var padreScalo = new Dictionary<string, string?> { ["LIPQ"] = "LIPP_CTR" };

        var mappa = EffectiveHierarchy.ParentMap(righe, padreScalo);

        Assert.Equal("LIPQ_GND", mappa["LIPQ_DEL"]);
        Assert.Equal("LIPQ_TWR", mappa["LIPQ_GND"]);
        Assert.Equal("LIPQ_APP", mappa["LIPQ_TWR"]);
        Assert.Equal("LIPP_CTR", mappa["LIPQ_APP"]);
    }

    /// <summary>Una posizione nascosta non è un padre possibile: non deve nemmeno partecipare alla scelta del gradino.</summary>
    [Fact]
    public void Una_posizione_nascosta_non_entra_nella_scelta()
    {
        var righe = new[]
        {
            Twr("LIML_TWR", padre: null, "LIML"),
            new HierarchyCatalogRow("LIML_X_APP", null, "LIML", SectorType.App, IsHidden: true),
            Ctr("LIMM_WS2_CTR", padre: null),
        };
        var padreScalo = new Dictionary<string, string?> { ["LIML"] = "LIMM_WS2_CTR" };

        // L'unica APP dello scalo è nascosta: la torre esce sul padre dello scalo, non su di lei.
        Assert.Equal("LIMM_WS2_CTR", EffectiveHierarchy.ParentMap(righe, padreScalo)["LIML_TWR"]);
    }

    /// <summary>Un settore d'area non ha scaletta: il suo padre è quello scritto e basta.</summary>
    [Fact]
    public void Un_settore_di_area_non_deriva_niente()
    {
        var righe = new[] { Ctr("LIMM_ES5_CTR", padre: "LIMM_ES2_CTR"), Ctr("LIMM_ES2_CTR", padre: null) };
        var mappa = EffectiveHierarchy.ParentMap(righe, new Dictionary<string, string?>());

        Assert.Equal("LIMM_ES2_CTR", mappa["LIMM_ES5_CTR"]);
        Assert.Null(mappa["LIMM_ES2_CTR"]);
    }

    [Theory]
    [InlineData("DEL", SectorType.Del)]
    [InlineData("gnd", SectorType.Gnd)]
    [InlineData("TWR", SectorType.Twr)]
    [InlineData("APP", SectorType.App)]
    [InlineData(null, SectorType.App)]
    public void TypeOfPosition_mappa_il_suffisso(string? position, SectorType atteso) =>
        Assert.Equal(atteso, EffectiveHierarchy.TypeOfPosition(position));
}
