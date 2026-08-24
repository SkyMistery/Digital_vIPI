using System.Collections.Generic;
using Vipi.Application.Stats;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Un aereo, UNA sessione. I settori italiani si sovrappongono di brutto (prova sul whazzup reale del
/// 24 agosto: lo stesso volo cadeva dentro 6 settori diversi), quindi contare «tutti i settori che
/// contengono il punto» gonfierebbe le statistiche di 5-6 volte. Vince il più specifico.
/// </summary>
public class TrafficAttributionTests
{
    private const string Grande = "[[10,40],[14,40],[14,44],[10,44]]";     // 4°×4°
    private const string Piccolo = "[[11.5,41.5],[12.5,41.5],[12.5,42.5],[11.5,42.5]]";   // 1°×1°, dentro Grande

    private static SectorClaim Claim(string sessione, string settore, string poly, int? lo, int? up, int depth,
        SectorType type = SectorType.Ctr) =>
        new(sessione, SectorVolume.From(settore, poly, lo, up)!, depth, type);

    private static string? Chi(IReadOnlyList<SectorClaim> claims, double lat, double lon, double ft,
        FlightPhase phase = FlightPhase.Airborne) =>
        TrafficAttribution.Attribute(claims, lat, lon, ft, phase);

    [Fact]
    public void Vince_il_settore_piu_profondo_nella_gerarchia()
    {
        var claims = new List<SectorClaim>
        {
            Claim("LIRR_NE1_CTR", "LIRR_NE1_CTR", Grande, null, null, 0),
            Claim("LIRF_TWR", "LIRF_TWR", Piccolo, null, 19500, 3, SectorType.Twr),
        };
        Assert.Equal("LIRF_TWR", Chi(claims, 42.0, 12.0, 3_000));
    }

    [Fact]
    public void Fuori_dal_settore_profondo_torna_al_settore_alto()
    {
        var claims = new List<SectorClaim>
        {
            Claim("LIRR_NE1_CTR", "LIRR_NE1_CTR", Grande, null, null, 0),
            Claim("LIRF_TWR", "LIRF_TWR", Piccolo, null, 19500, 3, SectorType.Twr),
        };
        Assert.Equal("LIRR_NE1_CTR", Chi(claims, 43.5, 13.5, 3_000));
    }

    [Fact]
    public void Sopra_il_tetto_del_piu_profondo_vince_chi_ci_arriva()
    {
        var claims = new List<SectorClaim>
        {
            Claim("LIRR_NE1_CTR", "LIRR_NE1_CTR", Grande, null, null, 0),
            Claim("LIRF_TWR", "LIRF_TWR", Piccolo, null, 19500, 3, SectorType.Twr),
        };
        Assert.Equal("LIRR_NE1_CTR", Chi(claims, 42.0, 12.0, 35_000));
    }

    [Fact]
    public void A_pari_profondita_vince_la_banda_piu_stretta()
    {
        var claims = new List<SectorClaim>
        {
            Claim("LIRR_MIL_CTR", "LIRR_MIL_CTR", Grande, null, null, 0),      // GND→UNL
            Claim("LIRR_FSS", "LIRR_FSS", Grande, null, 19500, 0),             // GND→FL195
        };
        Assert.Equal("LIRR_FSS", Chi(claims, 42.0, 12.0, 10_000));
    }

    [Fact]
    public void A_pari_profondita_e_pari_banda_vince_il_poligono_piu_piccolo()
    {
        var claims = new List<SectorClaim>
        {
            Claim("A_CTR", "A_CTR", Grande, null, null, 0),
            Claim("B_CTR", "B_CTR", Piccolo, null, null, 0),
        };
        Assert.Equal("B_CTR", Chi(claims, 42.0, 12.0, 10_000));
    }

    [Fact]
    public void A_parita_totale_la_scelta_e_stabile_non_casuale()
    {
        var claims = new List<SectorClaim>
        {
            Claim("Z_CTR", "Z_CTR", Grande, null, null, 0),
            Claim("A_CTR", "A_CTR", Grande, null, null, 0),
        };
        Assert.Equal("A_CTR", Chi(claims, 42.0, 12.0, 10_000));
        claims.Reverse();
        Assert.Equal("A_CTR", Chi(claims, 42.0, 12.0, 10_000));
    }

    [Fact]
    public void Un_aereo_fuori_da_tutti_non_e_di_nessuno()
    {
        var claims = new List<SectorClaim> { Claim("LIRF_TWR", "LIRF_TWR", Piccolo, null, 19500, 3, SectorType.Twr) };
        Assert.Null(Chi(claims, 48.0, 2.0, 3_000));
        Assert.Null(Chi(new List<SectorClaim>(), 42.0, 12.0, 3_000));
    }

    [Fact]
    public void Il_settore_ereditato_porta_il_traffico_alla_sessione_che_lo_copre()
    {
        var claims = new List<SectorClaim>
        {
            Claim("LIRR_NE1_CTR", "LIRR_NE1_CTR", Grande, null, null, 0),
            Claim("LIRR_NE1_CTR", "LIRF_TWR", Piccolo, null, 19500, 3, SectorType.Twr),
        };
        Assert.Equal("LIRR_NE1_CTR", Chi(claims, 42.0, 12.0, 0, FlightPhase.Ground));
    }

    // --- La fase del volo: DEL e GND non si distinguono con la geometria (non hanno poligono) ---

    /// <summary>DEL, GND e TWR di Fiumicino tutte in frequenza, tutte sullo stesso volume (il campo).</summary>
    private static List<SectorClaim> Torre_Completa() => new()
    {
        Claim("LIRF_DEL", "LIRF_DEL", Piccolo, null, 19500, 5, SectorType.Del),
        Claim("LIRF_GND", "LIRF_GND", Piccolo, null, 19500, 4, SectorType.Gnd),
        Claim("LIRF_TWR", "LIRF_TWR", Piccolo, null, 19500, 3, SectorType.Twr),
    };

    [Fact]
    public void La_partenza_ferma_al_gate_e_della_DEL()
    {
        Assert.Equal("LIRF_DEL", Chi(Torre_Completa(), 42.0, 12.0, 0, FlightPhase.Parked));
    }

    [Fact]
    public void Chi_rulla_e_della_GND_anche_se_la_DEL_e_piu_in_basso_nella_scaletta()
    {
        Assert.Equal("LIRF_GND", Chi(Torre_Completa(), 42.0, 12.0, 0, FlightPhase.Ground));
    }

    [Fact]
    public void Chi_e_in_volo_e_della_TWR()
    {
        Assert.Equal("LIRF_TWR", Chi(Torre_Completa(), 42.0, 12.0, 2_000, FlightPhase.Airborne));
    }

    [Fact]
    public void Senza_GND_la_DEL_da_sola_si_prende_anche_chi_rulla()
    {
        // Nessuno dichiara la fase «a terra»: resta la copertura, e in frequenza c'è solo lei.
        var solo_del = new List<SectorClaim> { Claim("LIRF_DEL", "LIRF_DEL", Piccolo, null, 19500, 5, SectorType.Del) };
        Assert.Equal("LIRF_DEL", Chi(solo_del, 42.0, 12.0, 0, FlightPhase.Ground));
    }

    [Fact]
    public void La_DEL_non_ruba_il_traffico_in_volo_alla_TWR()
    {
        var claims = new List<SectorClaim>
        {
            Claim("LIRF_DEL", "LIRF_DEL", Piccolo, null, 19500, 5, SectorType.Del),
            Claim("LIRF_TWR", "LIRF_TWR", Piccolo, null, 19500, 3, SectorType.Twr),
        };
        Assert.Equal("LIRF_TWR", Chi(claims, 42.0, 12.0, 1_500, FlightPhase.Airborne));
    }
}

/// <summary>La fase del volo dal tracciato IVAO: gli stati sono quelli osservati sul whazzup reale.</summary>
public class FlightPhaseTests
{
    [Fact]
    public void In_volo_se_non_e_a_terra()
    {
        Assert.Equal(FlightPhase.Airborne, FlightPhases.Of(onGround: false, 420, "En Route", 120));
        Assert.Equal(FlightPhase.Airborne, FlightPhases.Of(onGround: false, 0, "Approach", 300));
    }

    [Fact]
    public void A_terra_in_movimento_e_fase_di_terra()
    {
        Assert.Equal(FlightPhase.Ground, FlightPhases.Of(true, 17, "Departing", 0.3));    // AIQ8725 reale
        Assert.Equal(FlightPhase.Ground, FlightPhases.Of(true, 11, "Landed", 1522));      // ETD002 reale
    }

    [Fact]
    public void Fermo_al_gate_in_partenza_e_fase_di_parcheggio()
    {
        Assert.Equal(FlightPhase.Parked, FlightPhases.Of(true, 0, "Boarding", 0.43879));  // AAF0128 reale
    }

    [Fact]
    public void Fermo_ai_blocchi_a_destinazione_NON_e_una_partenza()
    {
        // VLG028A reale: dep LFBD, arr LEMD, fermo a 289 NM dalla partenza = è arrivato.
        Assert.Equal(FlightPhase.Ground, FlightPhases.Of(true, 0, "On Blocks", 289.3682));
    }

    [Fact]
    public void Fermo_lontano_dal_campo_di_partenza_non_e_parcheggio_neanche_in_Boarding()
    {
        Assert.Equal(FlightPhase.Ground, FlightPhases.Of(true, 0, "Boarding", 50));
    }

    [Fact]
    public void Senza_distanza_dalla_partenza_ci_si_fida_dello_stato()
    {
        Assert.Equal(FlightPhase.Parked, FlightPhases.Of(true, 0, "Boarding", null));
    }

    [Fact]
    public void Ogni_posizione_dichiara_le_proprie_fasi()
    {
        Assert.True(FlightPhases.Handles(SectorType.Del, FlightPhase.Parked));
        Assert.False(FlightPhases.Handles(SectorType.Del, FlightPhase.Ground));
        Assert.False(FlightPhases.Handles(SectorType.Del, FlightPhase.Airborne));

        Assert.True(FlightPhases.Handles(SectorType.Gnd, FlightPhase.Parked));
        Assert.True(FlightPhases.Handles(SectorType.Gnd, FlightPhase.Ground));
        Assert.False(FlightPhases.Handles(SectorType.Gnd, FlightPhase.Airborne));

        Assert.True(FlightPhases.Handles(SectorType.Twr, FlightPhase.Ground));
        Assert.True(FlightPhases.Handles(SectorType.ITwr, FlightPhase.Airborne));
        Assert.False(FlightPhases.Handles(SectorType.Twr, FlightPhase.Parked));

        Assert.True(FlightPhases.Handles(SectorType.App, FlightPhase.Airborne));
        Assert.True(FlightPhases.Handles(SectorType.Ctr, FlightPhase.Airborne));
        Assert.False(FlightPhases.Handles(SectorType.Ctr, FlightPhase.Ground));
    }
}
