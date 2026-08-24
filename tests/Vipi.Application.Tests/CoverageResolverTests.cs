using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Stats;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Discesa della copertura: «quali settori sono miei adesso?». Un settore è coperto da me se io sono il primo
/// antenato online risalendo da lui (me compreso). È il verso opposto di <c>TransferOnlineResolver</c>, che
/// risale per trovare il ricevente. Puro, nessun I/O.
///
/// Albero di prova (quello vero di Roma, semplificato):
///   LIRR_NE1_CTR ← LIRF_TW1_APP ← LIRF_TWR ← LIRF_GND ← LIRF_DEL
/// più un ramo separato: LIRR_NE1_CTR ← LIPZ_APP ← LIPZ_TWR.
/// </summary>
public class CoverageResolverTests
{
    private static readonly List<CoverageNode> Albero = new()
    {
        new("LIRR_NE1_CTR", null),
        new("LIRF_TW1_APP", "LIRR_NE1_CTR"),
        new("LIRF_TWR", "LIRF_TW1_APP"),
        new("LIRF_GND", "LIRF_TWR"),
        new("LIRF_DEL", "LIRF_GND"),
        new("LIPZ_APP", "LIRR_NE1_CTR"),
        new("LIPZ_TWR", "LIPZ_APP"),
    };

    private static IReadOnlySet<string> Online(params string[] cs) =>
        new HashSet<string>(cs, System.StringComparer.OrdinalIgnoreCase);

    private static string[] Covered(string target, params string[] online) =>
        CoverageResolver.CoveredBy(target, Albero, Online(online)).OrderBy(x => x).ToArray();

    [Fact]
    public void L_ultimo_gradino_della_scaletta_copre_solo_se_stesso()
    {
        // La DEL è il fondo: sotto non pende nessuno.
        Assert.Equal(new[] { "LIRF_DEL" }, Covered("LIRF_DEL", "LIRF_DEL"));
        // La GND invece, anche da sola, si porta dietro la DEL che le sta sotto.
        Assert.Equal(new[] { "LIRF_DEL", "LIRF_GND" }, Covered("LIRF_GND", "LIRF_GND"));
    }

    [Fact]
    public void Se_sono_l_unico_online_copro_tutto_l_albero_sotto_di_me()
    {
        Assert.Equal(
            new[] { "LIPZ_APP", "LIPZ_TWR", "LIRF_DEL", "LIRF_GND", "LIRF_TW1_APP", "LIRF_TWR", "LIRR_NE1_CTR" },
            Covered("LIRR_NE1_CTR", "LIRR_NE1_CTR"));
    }

    [Fact]
    public void Un_figlio_online_si_riprende_il_proprio_sottoalbero()
    {
        // La TWR è online: il CTR non la copre più, e non copre nemmeno GND/DEL che stanno sotto di lei.
        Assert.Equal(
            new[] { "LIPZ_APP", "LIPZ_TWR", "LIRF_TW1_APP", "LIRR_NE1_CTR" },
            Covered("LIRR_NE1_CTR", "LIRR_NE1_CTR", "LIRF_TWR"));

        Assert.Equal(
            new[] { "LIRF_DEL", "LIRF_GND", "LIRF_TWR" },
            Covered("LIRF_TWR", "LIRR_NE1_CTR", "LIRF_TWR"));
    }

    [Fact]
    public void Vince_sempre_l_antenato_piu_vicino()
    {
        // CTR + APP + TWR online: il DEL va alla TWR, non al CTR.
        Assert.Equal(new[] { "LIRF_DEL", "LIRF_GND", "LIRF_TWR" },
            Covered("LIRF_TWR", "LIRR_NE1_CTR", "LIRF_TW1_APP", "LIRF_TWR"));
        Assert.Equal(new[] { "LIRF_TW1_APP" },
            Covered("LIRF_TW1_APP", "LIRR_NE1_CTR", "LIRF_TW1_APP", "LIRF_TWR"));
    }

    [Fact]
    public void Chi_non_e_online_non_copre_niente()
    {
        Assert.Empty(Covered("LIRF_TWR", "LIRR_NE1_CTR"));
    }

    [Fact]
    public void Il_confronto_sui_callsign_e_senza_maiuscole()
    {
        Assert.Equal(new[] { "LIRF_DEL", "LIRF_GND" }, Covered("LIRF_GND", "lirf_gnd"));
    }

    [Fact]
    public void Un_callsign_online_fuori_dall_albero_non_disturba()
    {
        // LFMM_CTR è online ma non è nel nostro catalogo: nessun effetto sulla copertura italiana.
        Assert.Equal(new[] { "LIPZ_APP", "LIPZ_TWR", "LIRF_DEL", "LIRF_GND", "LIRF_TW1_APP", "LIRF_TWR", "LIRR_NE1_CTR" },
            Covered("LIRR_NE1_CTR", "LIRR_NE1_CTR", "LFMM_CTR"));
    }

    [Fact]
    public void Un_padre_inesistente_rende_il_nodo_una_radice()
    {
        var albero = new List<CoverageNode> { new("LIBA_APP", "LIRR_NC_CTR"), new("LIBA_TWR", "LIBA_APP") };
        var coperti = CoverageResolver.CoveredBy("LIBA_APP", albero, Online("LIBA_APP")).OrderBy(x => x);
        Assert.Equal(new[] { "LIBA_APP", "LIBA_TWR" }, coperti);
    }

    [Fact]
    public void Un_ciclo_nella_gerarchia_non_manda_in_stallo()
    {
        // Dato sporco possibile in archivio: A→B→A. Deve terminare, non ciclare.
        var ciclo = new List<CoverageNode> { new("A_CTR", "B_CTR"), new("B_CTR", "A_CTR"), new("C_TWR", "A_CTR") };
        var coperti = CoverageResolver.CoveredBy("A_CTR", ciclo, Online("A_CTR")).OrderBy(x => x).ToArray();
        Assert.Contains("A_CTR", coperti);
        Assert.Contains("C_TWR", coperti);
    }

    [Fact]
    public void Owners_dice_per_ogni_settore_chi_lo_sta_coprendo()
    {
        var owners = CoverageResolver.Owners(Albero, Online("LIRR_NE1_CTR", "LIRF_TWR"));
        Assert.Equal("LIRF_TWR", owners["LIRF_DEL"]);
        Assert.Equal("LIRF_TWR", owners["LIRF_TWR"]);
        Assert.Equal("LIRR_NE1_CTR", owners["LIRF_TW1_APP"]);
        Assert.Equal("LIRR_NE1_CTR", owners["LIPZ_TWR"]);
    }

    [Fact]
    public void Senza_nessuno_online_nessun_settore_ha_un_padrone()
    {
        var owners = CoverageResolver.Owners(Albero, Online());
        Assert.All(owners.Values, v => Assert.Null(v));
    }
}
