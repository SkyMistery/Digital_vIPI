using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Abstractions;
using Vipi.Application.Airspace;
using Vipi.Application.Stats;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Da «chi è in frequenza» a «quali volumi rivendica»: copertura top-down più volumi dei cataloghi.
/// L'albero di prova è quello vero di Roma, semplificato.
/// </summary>
public class SectorVolumeMapTests
{
    private const string Fir = "[[10,40],[14,40],[14,44],[10,44]]";
    private const string Campo = "[[11.5,41.5],[12.5,41.5],[12.5,42.5],[11.5,42.5]]";

    /// <summary>Una riga con un pezzo solo: la forma di IVAO, come la dà la porta unica.</summary>
    private static SectorVolumeRow Riga(string cs, string? padre, SectorType tipo, string? icao,
        string? poligono, int? basso, int? alto) =>
        new(cs, padre, tipo, icao,
            poligono is null
                ? Array.Empty<ShapePart>()
                : new[] { new ShapePart(poligono, basso, alto, AirspaceDatum.Amsl, AirspaceDatum.Amsl, "", "") },
            ShapeSource.Source);

    private static readonly List<SectorVolumeRow> Roma = new()
    {
        Riga("LIRR_NE1_CTR", null, SectorType.Ctr, null, Fir, 0, null),
        Riga("LIRF_TW1_APP", "LIRR_NE1_CTR", SectorType.App, "LIRF", Campo, 0, 19500),
        Riga("LIRF_TWR", "LIRF_TW1_APP", SectorType.Twr, "LIRF", Campo, 0, 3000),
        // ⚠️ DEL e GND non hanno poligono: nel vipi.db reale sono 0 su 5 e 0 su 20.
        Riga("LIRF_GND", "LIRF_TWR", SectorType.Gnd, "LIRF", null, null, null),
        Riga("LIRF_DEL", "LIRF_GND", SectorType.Del, "LIRF", null, null, null),
    };

    private static IReadOnlySet<string> Online(params string[] cs) =>
        new HashSet<string>(cs, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Chi_e_solo_in_frequenza_rivendica_tutto_l_albero_sotto_di_se()
    {
        var claims = SectorVolumeMap.BuildClaims(Roma, Online("LIRR_NE1_CTR"));

        Assert.All(claims, c => Assert.Equal("LIRR_NE1_CTR", c.SessionCallsign));
        Assert.Equal(5, claims.Count);   // anche DEL e GND, che prendono il volume della torre
    }

    [Fact]
    public void Ogni_settore_ha_un_solo_padrone()
    {
        var claims = SectorVolumeMap.BuildClaims(Roma, Online("LIRR_NE1_CTR", "LIRF_TWR"));

        var perSettore = claims.GroupBy(c => c.Volume.Callsign);
        Assert.All(perSettore, g => Assert.Single(g));

        Assert.Equal("LIRF_TWR", claims.Single(c => c.Volume.Callsign == "LIRF_DEL").SessionCallsign);
        Assert.Equal("LIRR_NE1_CTR", claims.Single(c => c.Volume.Callsign == "LIRF_TW1_APP").SessionCallsign);
    }

    [Fact]
    public void La_profondita_cresce_scendendo_la_scaletta()
    {
        var claims = SectorVolumeMap.BuildClaims(Roma, Online("LIRR_NE1_CTR"));
        int Prof(string cs) => claims.Single(c => c.Volume.Callsign == cs).Depth;

        Assert.Equal(0, Prof("LIRR_NE1_CTR"));
        Assert.Equal(1, Prof("LIRF_TW1_APP"));
        Assert.Equal(2, Prof("LIRF_TWR"));
        Assert.Equal(3, Prof("LIRF_GND"));
        Assert.Equal(4, Prof("LIRF_DEL"));
    }

    [Fact]
    public void DEL_e_GND_prendono_in_prestito_il_volume_della_torre_del_loro_campo()
    {
        var claims = SectorVolumeMap.BuildClaims(Roma, Online("LIRF_GND"));

        var gnd = claims.Single(c => c.Volume.Callsign == "LIRF_GND");
        Assert.Equal(0, gnd.Volume.BottomFl);
        Assert.Equal(30, gnd.Volume.TopFl);                       // 3000 ft della torre → FL30
        Assert.True(gnd.Volume.Contains(42.0, 12.0, 0));          // l'aereo fermo sul campo è dentro
    }

    [Fact]
    public void Senza_torre_col_poligono_la_GND_non_rivendica_niente()
    {
        var senzaTorre = Roma.Where(r => r.Type != SectorType.Twr).ToList();
        var claims = SectorVolumeMap.BuildClaims(senzaTorre, Online("LIRF_GND"));

        Assert.DoesNotContain(claims, c => c.Volume.Callsign == "LIRF_GND");
    }

    [Fact]
    public void Chi_non_e_online_non_rivendica()
    {
        Assert.Empty(SectorVolumeMap.BuildClaims(Roma, Online()));
        Assert.Empty(SectorVolumeMap.BuildClaims(new List<SectorVolumeRow>(), Online("LIRF_TWR")));
    }

    [Fact]
    public void Un_ciclo_nell_albero_non_manda_in_stallo_il_calcolo_della_profondita()
    {
        var ciclo = new List<SectorVolumeRow>
        {
            Riga("A_CTR", "B_CTR", SectorType.Ctr, null, Fir, 0, null),
            Riga("B_CTR", "A_CTR", SectorType.Ctr, null, Fir, 0, null),
        };
        var claims = SectorVolumeMap.BuildClaims(ciclo, Online("A_CTR"));
        Assert.NotEmpty(claims);
    }

    [Fact]
    public void Il_giro_completo_manda_l_aereo_a_terra_alla_GND_e_quello_in_quota_all_ACC()
    {
        var claims = SectorVolumeMap.BuildClaims(Roma, Online("LIRR_NE1_CTR", "LIRF_GND"));

        // Fermo al gate di Fiumicino: è della GND (la DEL non è in frequenza).
        Assert.Equal("LIRF_GND", TrafficAttribution.Attribute(claims, 42.0, 12.0, 0, FlightPhase.Parked));
        // In crociera sopra la stessa verticale: è dell'ACC.
        Assert.Equal("LIRR_NE1_CTR", TrafficAttribution.Attribute(claims, 42.0, 12.0, 35_000, FlightPhase.Airborne));
    }
}
