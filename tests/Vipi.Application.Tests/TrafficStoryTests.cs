using System;
using System.Linq;
using Vipi.Application.Stats;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Le targhette di una riga di traffico: quel che abbiamo VISTO, non quel che il piano di volo prometteva.
/// </summary>
public class TrafficStoryTests
{
    private static TrafficFacts Volo(
        string? dep = "LIRF", string? arr = "LIRN", int leg = 1, bool mosso = true, bool volato = true,
        bool buco = false, bool ricostruito = false,
        FlightPhase? prima = FlightPhase.Ground, FlightPhase? ultima = FlightPhase.Airborne,
        bool consegnato = false) =>
        new(dep, arr, leg, mosso, volato, buco, ricostruito, prima, ultima, consegnato);

    [Fact]
    public void Un_arrivo_al_mio_campo_che_ho_visto_toccare_terra_e_atterrato()
    {
        var t = TrafficStory.Tags(
            Volo(dep: "LIMC", arr: "LIRF", prima: FlightPhase.Airborne, ultima: FlightPhase.Ground),
            "LIRF");

        Assert.Contains(TrafficTag.Arrival, t);
        Assert.Contains(TrafficTag.Landed, t);
        Assert.DoesNotContain(TrafficTag.TookOff, t);
    }

    [Fact]
    public void Chi_esce_dall_area_ancora_in_volo_non_e_atterrato_nemmeno_se_veniva_da_noi()
    {
        // È la regola che il committente ha chiesto: la targhetta dice quel che si è visto.
        var t = TrafficStory.Tags(
            Volo(dep: "LIMC", arr: "LIRF", prima: FlightPhase.Airborne, ultima: FlightPhase.Airborne),
            "LIRF");

        Assert.Contains(TrafficTag.Arrival, t);
        Assert.DoesNotContain(TrafficTag.Landed, t);
        Assert.Contains(TrafficTag.LeftAirborne, t);
    }

    [Fact]
    public void Una_partenza_vista_staccare_e_consegnata_dice_tutte_e_due_le_cose()
    {
        var t = TrafficStory.Tags(
            Volo(dep: "LIRF", arr: "LIMC", prima: FlightPhase.Parked, ultima: FlightPhase.Airborne,
                consegnato: true),
            "LIRF");

        Assert.Contains(TrafficTag.Departure, t);
        Assert.Contains(TrafficTag.TookOff, t);
        Assert.Contains(TrafficTag.HandedOff, t);
        Assert.DoesNotContain(TrafficTag.LeftAirborne, t);
    }

    [Fact]
    public void Chi_ha_volato_e_finisce_fermo_e_arrivato_al_gate()
    {
        var t = TrafficStory.Tags(
            Volo(prima: FlightPhase.Airborne, ultima: FlightPhase.Parked), "LIRF");

        Assert.Contains(TrafficTag.AtGate, t);
        Assert.DoesNotContain(TrafficTag.Landed, t);
    }

    [Fact]
    public void Chi_non_ha_mai_volato_e_rullaggio_soltanto()
    {
        var t = TrafficStory.Tags(
            Volo(volato: false, prima: FlightPhase.Parked, ultima: FlightPhase.Ground), "LIRF");

        Assert.Contains(TrafficTag.TaxiOnly, t);
        Assert.DoesNotContain(TrafficTag.Landed, t);
    }

    [Fact]
    public void Chi_non_si_e_mosso_resta_una_presenza()
    {
        var t = TrafficStory.Tags(
            Volo(mosso: false, volato: false, prima: FlightPhase.Parked, ultima: FlightPhase.Parked), "LIRF");

        Assert.Contains(TrafficTag.Parked, t);
    }

    [Fact]
    public void La_riga_ricostruita_non_racconta_fasi_che_non_ha_visto()
    {
        var t = TrafficStory.Tags(
            Volo(ricostruito: true, prima: null, ultima: null, volato: false), "LIRF");

        Assert.Contains(TrafficTag.Rebuilt, t);
        Assert.DoesNotContain(TrafficTag.Parked, t);
        Assert.DoesNotContain(TrafficTag.TaxiOnly, t);
    }

    [Fact]
    public void Un_volo_che_non_tocca_la_divisione_e_un_sorvolo()
    {
        var t = TrafficStory.Tags(Volo(dep: "EDDM", arr: "LGAV"), stationIcao: null);
        Assert.Contains(TrafficTag.Overflight, t);
    }

    [Fact]
    public void Un_volo_italiano_sotto_un_ACC_non_e_un_sorvolo()
    {
        var t = TrafficStory.Tags(Volo(dep: "LIRF", arr: "LIMC"), stationIcao: null);
        Assert.DoesNotContain(TrafficTag.Overflight, t);
    }

    [Fact]
    public void Senza_piano_di_volo_lo_dice()
    {
        var t = TrafficStory.Tags(Volo(dep: null, arr: null), "LIRF");
        Assert.Contains(TrafficTag.NoFlightPlan, t);
        Assert.DoesNotContain(TrafficTag.Overflight, t);
    }

    [Theory]
    [InlineData("LIRF_TWR", "LIRF")]
    [InlineData("LIRF_APP", "LIRF")]
    [InlineData("LIML_GND", "LIML")]
    [InlineData("LIRR_NE1_CTR", null)]     // prefisso di FIR, non un aeroporto
    [InlineData("LIRR_CTR", null)]
    [InlineData("", null)]
    public void L_ICAO_della_postazione_si_legge_solo_dove_esiste(string callsign, string? atteso)
    {
        Assert.Equal(atteso, TrafficStory.StationIcao(callsign));
    }

    [Fact]
    public void La_seconda_tratta_e_il_buco_restano_in_coda()
    {
        var t = TrafficStory.Tags(Volo(leg: 2, buco: true), "LIRF").ToList();

        Assert.Contains(TrafficTag.SecondLeg, t);
        Assert.Contains(TrafficTag.Gap, t);
        Assert.Equal(TrafficTag.Gap, t[^1]);
    }
}
