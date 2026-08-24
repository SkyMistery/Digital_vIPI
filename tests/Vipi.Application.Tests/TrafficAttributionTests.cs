using System.Collections.Generic;
using Vipi.Application.Stats;
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

    private static SectorClaim Claim(string sessione, string settore, string poly, int? lo, int? up, int depth) =>
        new(sessione, SectorVolume.From(settore, poly, lo, up)!, depth);

    [Fact]
    public void Vince_il_settore_piu_profondo_nella_gerarchia()
    {
        var claims = new List<SectorClaim>
        {
            Claim("LIRR_NE1_CTR", "LIRR_NE1_CTR", Grande, null, null, depth: 0),
            Claim("LIRF_TWR", "LIRF_TWR", Piccolo, null, 19500, depth: 3),
        };
        Assert.Equal("LIRF_TWR", TrafficAttribution.Attribute(claims, 42.0, 12.0, 3_000));
    }

    [Fact]
    public void Fuori_dal_settore_profondo_torna_al_settore_alto()
    {
        var claims = new List<SectorClaim>
        {
            Claim("LIRR_NE1_CTR", "LIRR_NE1_CTR", Grande, null, null, depth: 0),
            Claim("LIRF_TWR", "LIRF_TWR", Piccolo, null, 19500, depth: 3),
        };
        Assert.Equal("LIRR_NE1_CTR", TrafficAttribution.Attribute(claims, 43.5, 13.5, 3_000));
    }

    [Fact]
    public void Sopra_il_tetto_del_piu_profondo_vince_chi_ci_arriva()
    {
        // A FL350 la TWR non c'entra niente, anche se il punto è sopra il suo poligono.
        var claims = new List<SectorClaim>
        {
            Claim("LIRR_NE1_CTR", "LIRR_NE1_CTR", Grande, null, null, depth: 0),
            Claim("LIRF_TWR", "LIRF_TWR", Piccolo, null, 19500, depth: 3),
        };
        Assert.Equal("LIRR_NE1_CTR", TrafficAttribution.Attribute(claims, 42.0, 12.0, 35_000));
    }

    [Fact]
    public void A_pari_profondita_vince_la_banda_piu_stretta()
    {
        // Il caso reale MIL/FSS: due radici che si sovrappongono. Il più «stretto» è il più specifico.
        var claims = new List<SectorClaim>
        {
            Claim("LIRR_MIL_CTR", "LIRR_MIL_CTR", Grande, null, null, depth: 0),      // GND→UNL
            Claim("LIRR_FSS", "LIRR_FSS", Grande, null, 19500, depth: 0),             // GND→FL195
        };
        Assert.Equal("LIRR_FSS", TrafficAttribution.Attribute(claims, 42.0, 12.0, 10_000));
    }

    [Fact]
    public void A_pari_profondita_e_pari_banda_vince_il_poligono_piu_piccolo()
    {
        var claims = new List<SectorClaim>
        {
            Claim("A_CTR", "A_CTR", Grande, null, null, depth: 0),
            Claim("B_CTR", "B_CTR", Piccolo, null, null, depth: 0),
        };
        Assert.Equal("B_CTR", TrafficAttribution.Attribute(claims, 42.0, 12.0, 10_000));
    }

    [Fact]
    public void A_parita_totale_la_scelta_e_stabile_non_casuale()
    {
        var claims = new List<SectorClaim>
        {
            Claim("Z_CTR", "Z_CTR", Grande, null, null, depth: 0),
            Claim("A_CTR", "A_CTR", Grande, null, null, depth: 0),
        };
        // Stesso volume, stessa profondità: conta l'ordine alfabetico, così due giri danno lo stesso esito.
        Assert.Equal("A_CTR", TrafficAttribution.Attribute(claims, 42.0, 12.0, 10_000));
        claims.Reverse();
        Assert.Equal("A_CTR", TrafficAttribution.Attribute(claims, 42.0, 12.0, 10_000));
    }

    [Fact]
    public void Un_aereo_fuori_da_tutti_non_e_di_nessuno()
    {
        var claims = new List<SectorClaim> { Claim("LIRF_TWR", "LIRF_TWR", Piccolo, null, 19500, 3) };
        Assert.Null(TrafficAttribution.Attribute(claims, 48.0, 2.0, 3_000));
        Assert.Null(TrafficAttribution.Attribute(new List<SectorClaim>(), 42.0, 12.0, 3_000));
    }

    [Fact]
    public void Il_settore_ereditato_porta_il_traffico_alla_sessione_che_lo_copre()
    {
        // La TWR è spenta: il CTR la eredita, quindi l'aereo a terra è del CTR — ma via il volume DELLA TWR,
        // che è quello che conta per la specificità.
        var claims = new List<SectorClaim>
        {
            Claim("LIRR_NE1_CTR", "LIRR_NE1_CTR", Grande, null, null, depth: 0),
            Claim("LIRR_NE1_CTR", "LIRF_TWR", Piccolo, null, 19500, depth: 3),
        };
        Assert.Equal("LIRR_NE1_CTR", TrafficAttribution.Attribute(claims, 42.0, 12.0, 0));
    }
}
