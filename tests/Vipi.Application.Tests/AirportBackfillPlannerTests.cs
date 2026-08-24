using System;
using System.Collections.Generic;
using Vipi.Application.Stats;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Di chi è un movimento d'aeroporto ricostruito a posteriori. La sorgente dice «a LIRF fra le 18 e le 20
/// sono atterrati questi»: se in quella finestra c'erano TWR e GND insieme, darlo a tutt'e due raddoppierebbe
/// i numeri della divisione.
/// </summary>
public class AirportBackfillPlannerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 24, 18, 0, 0, TimeSpan.Zero);

    private static AirportSessionWindow S(long id, string callsign, int daMinuto, int aMinuto)
    {
        var p = AirportBackfillPlanner.Parse(callsign)!.Value;
        return new AirportSessionWindow(id, callsign, p.Icao, p.Type,
            T0.AddMinutes(daMinuto), T0.AddMinutes(aMinuto));
    }

    [Fact]
    public void Da_sola_una_posizione_si_prende_i_movimenti()
    {
        var gnd = S(1, "LIRF_GND", 0, 120);
        Assert.Equal(1, AirportBackfillPlanner.Owner(gnd, new[] { gnd }));
    }

    [Fact]
    public void Con_la_torre_in_frequenza_i_movimenti_sono_suoi()
    {
        var gnd = S(1, "LIRF_GND", 0, 120);
        var twr = S(2, "LIRF_TWR", 30, 90);
        var tutte = new[] { gnd, twr };

        Assert.Equal(2, AirportBackfillPlanner.Owner(gnd, tutte));    // la GND cede
        Assert.Equal(2, AirportBackfillPlanner.Owner(twr, tutte));
    }

    [Fact]
    public void Se_la_torre_stacca_prima_la_GND_non_eredita_per_magia_i_movimenti_di_prima()
    {
        // Le finestre non si toccano: sono due periodi distinti, ognuno coi suoi movimenti.
        var twr = S(2, "LIRF_TWR", 0, 60);
        var gnd = S(1, "LIRF_GND", 61, 120);
        var tutte = new[] { gnd, twr };

        Assert.Equal(1, AirportBackfillPlanner.Owner(gnd, tutte));
        Assert.Equal(2, AirportBackfillPlanner.Owner(twr, tutte));
    }

    [Fact]
    public void Un_altro_aeroporto_non_c_entra()
    {
        var lirf = S(1, "LIRF_GND", 0, 120);
        var limc = S(2, "LIMC_TWR", 0, 120);

        Assert.Equal(1, AirportBackfillPlanner.Owner(lirf, new[] { lirf, limc }));
    }

    [Fact]
    public void L_ordine_di_competenza_e_TWR_APP_GND_DEL()
    {
        Assert.True(AirportBackfillPlanner.Competence(SectorType.Twr) > AirportBackfillPlanner.Competence(SectorType.App));
        Assert.True(AirportBackfillPlanner.Competence(SectorType.App) > AirportBackfillPlanner.Competence(SectorType.Gnd));
        Assert.True(AirportBackfillPlanner.Competence(SectorType.Gnd) > AirportBackfillPlanner.Competence(SectorType.Del));
        Assert.Equal(0, AirportBackfillPlanner.Competence(SectorType.Ctr));
    }

    [Fact]
    public void Un_settore_d_area_non_prende_movimenti_per_questa_via()
    {
        // Il traffico degli ACC si popola vivendo: la sorgente non racconta i movimenti di un settore d'area.
        var ctr = new AirportSessionWindow(1, "LIRR_NE1_CTR", "LIRR", SectorType.Ctr, T0, T0.AddHours(2));
        Assert.Null(AirportBackfillPlanner.Owner(ctr, new[] { ctr }));
    }

    [Fact]
    public void A_parita_di_grado_la_scelta_e_stabile()
    {
        var a = S(7, "LIRF_TWR", 0, 120);
        var b = S(3, "LIRF_E_TWR", 0, 120);
        var tutte = new[] { a, b };

        Assert.Equal(3, AirportBackfillPlanner.Owner(a, tutte));
        Assert.Equal(3, AirportBackfillPlanner.Owner(b, tutte));
    }

    [Fact]
    public void Il_callsign_dice_aeroporto_e_posizione()
    {
        Assert.Equal(("LIRF", SectorType.Twr), AirportBackfillPlanner.Parse("LIRF_TWR"));
        Assert.Equal(("LIRN", SectorType.App), AirportBackfillPlanner.Parse("LIRN_US0_APP"));
        Assert.Equal(("LIML", SectorType.Twr), AirportBackfillPlanner.Parse("LIML_I_TWR"));   // infisso, non un altro campo
        Assert.Equal(("LIRF", SectorType.App), AirportBackfillPlanner.Parse("LIRF_DEP"));
    }

    [Fact]
    public void Quel_che_non_e_una_posizione_d_aeroporto_non_si_finge_tale()
    {
        Assert.Null(AirportBackfillPlanner.Parse("LIRR_NE1_CTR"));
        Assert.Null(AirportBackfillPlanner.Parse("LIRR_FSS"));
        Assert.Null(AirportBackfillPlanner.Parse("LIRF_ATIS"));
        Assert.Null(AirportBackfillPlanner.Parse("PIPPO"));
        Assert.Null(AirportBackfillPlanner.Parse(""));
    }
}
