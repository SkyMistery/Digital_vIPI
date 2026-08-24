using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Stats;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Le due domande che rompono un conteggio fatto per solo callsign: il pilota che cade e rientra nello stesso
/// volo (deve restare UN movimento) e il pilota che senza disconnettersi fa due voli (devono essere DUE).
/// </summary>
public class FlightLegResolverTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    private static List<OpenLeg> Aperte(params OpenLeg[] l) => l.ToList();

    [Fact]
    public void Il_pilota_che_cade_e_rientra_resta_la_stessa_tratta()
    {
        var aperte = Aperte(new OpenLeg("AZA123", 900, "LIRF", "LIRN", 1, T0));
        // Cade e rientra dopo 4 minuti: stesso callsign, stesso piano di volo.
        var trovata = FlightLegResolver.Match(aperte, "AZA123", 900, "LIRF", "LIRN", T0.AddMinutes(4));
        Assert.NotNull(trovata);
        Assert.Equal(1, trovata!.Ordinal);
    }

    [Fact]
    public void Un_secondo_volo_nella_stessa_connessione_e_una_tratta_nuova()
    {
        var aperte = Aperte(new OpenLeg("AZA123", 900, "LIRF", "LIRN", 1, T0));
        // Ripartito da Napoli verso Roma: la rotta è cambiata.
        Assert.Null(FlightLegResolver.Match(aperte, "AZA123", 901, "LIRN", "LIRF", T0.AddMinutes(50)));
        Assert.Equal(2, FlightLegResolver.NextOrdinal(aperte, "AZA123"));
    }

    [Fact]
    public void La_stessa_rotta_rifatta_dopo_un_buco_lungo_e_una_tratta_nuova()
    {
        // Navetta che rifà identica la stessa tratta, o circuiti di addestramento.
        var aperte = Aperte(new OpenLeg("AZA123", null, "LIRF", "LIRN", 1, T0));
        Assert.Null(FlightLegResolver.Match(aperte, "AZA123", null, "LIRF", "LIRN", T0.AddMinutes(45)));
        Assert.NotNull(FlightLegResolver.Match(aperte, "AZA123", null, "LIRF", "LIRN", T0.AddMinutes(29)));
    }

    [Fact]
    public void Piloti_diversi_non_si_confondono()
    {
        var aperte = Aperte(
            new OpenLeg("AZA123", 900, "LIRF", "LIRN", 1, T0),
            new OpenLeg("RYR456", 901, "LIRF", "LIRN", 1, T0));
        Assert.Equal("RYR456", FlightLegResolver.Match(aperte, "RYR456", 901, "LIRF", "LIRN", T0.AddMinutes(5))!.PilotCallsign);
        Assert.Equal(1, FlightLegResolver.NextOrdinal(aperte, "ITY999"));
    }

    [Fact]
    public void Un_volo_senza_piano_di_volo_resta_una_tratta_sola_finche_e_in_frequenza()
    {
        var aperte = Aperte(new OpenLeg("IHVMV", null, null, null, 1, T0));
        Assert.NotNull(FlightLegResolver.Match(aperte, "IHVMV", null, null, null, T0.AddMinutes(10)));
        Assert.Null(FlightLegResolver.Match(aperte, "IHVMV", null, "LIRF", "LIRN", T0.AddMinutes(10)));   // FP depositato dopo
    }

    [Fact]
    public void Il_confronto_ignora_maiuscole_e_spazi()
    {
        var aperte = Aperte(new OpenLeg("AZA123", 900, "LIRF", "LIRN", 1, T0));
        Assert.NotNull(FlightLegResolver.Match(aperte, "aza123", null, " lirf ", "lirn", T0.AddMinutes(1)));
    }

    [Fact]
    public void Se_il_poller_resta_fermo_a_lungo_il_volo_non_si_spezza_in_due()
    {
        // Riavvio dell'applicazione, deploy, rete giu': il buco e' NOSTRO, non del pilota. Con lo stesso
        // piano di volo la tratta e' la stessa anche dopo un'ora, altrimenti un deploy conta doppio ogni
        // aereo in volo in quel momento.
        var aperte = Aperte(new OpenLeg("AZA123", 900, "LIRF", "LIRN", 1, T0));
        Assert.NotNull(FlightLegResolver.Match(aperte, "AZA123", 900, "LIRF", "LIRN", T0.AddHours(1)));
    }

    [Fact]
    public void Un_piano_di_volo_nuovo_apre_subito_una_tratta_nuova()
    {
        // Rifilato per la gamba dopo, senza mai disconnettersi: sono due movimenti anche a un minuto di distanza.
        var aperte = Aperte(new OpenLeg("AZA123", 900, "LIRF", "LIRN", 1, T0));
        Assert.Null(FlightLegResolver.Match(aperte, "AZA123", 901, "LIRN", "LIRF", T0.AddMinutes(1)));
    }
}

/// <summary>
/// Il turno: sessioni ATC spezzate da una caduta di linea vanno raccolte, o lo stesso aereo viene contato
/// una volta per ogni pezzo di connessione.
/// </summary>
public class AtcShiftGrouperTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 24, 18, 0, 0, TimeSpan.Zero);

    private static ShiftInput S(long id, DateTimeOffset start, DateTimeOffset? end, string cs = "LIRF_TWR", int vid = 704798) =>
        new(id, vid, cs, start, end);

    [Fact]
    public void Due_pezzi_a_cavallo_di_una_caduta_sono_un_turno_solo()
    {
        var g = AtcShiftGrouper.Group(new[]
        {
            S(1, T0, T0.AddHours(1)),
            S(2, T0.AddHours(1).AddMinutes(3), T0.AddHours(2)),
        });
        Assert.Equal(1, g[1]);
        Assert.Equal(1, g[2]);   // la chiave è la PRIMA sessione del gruppo
    }

    [Fact]
    public void Dopo_una_pausa_lunga_e_un_turno_nuovo()
    {
        var g = AtcShiftGrouper.Group(new[]
        {
            S(1, T0, T0.AddHours(1)),
            S(2, T0.AddHours(3), T0.AddHours(4)),
        });
        Assert.Equal(1, g[1]);
        Assert.Equal(2, g[2]);
    }

    [Fact]
    public void Cambiare_postazione_apre_un_turno_nuovo_anche_subito()
    {
        var g = AtcShiftGrouper.Group(new[]
        {
            S(1, T0, T0.AddHours(1), "LIRF_TWR"),
            S(2, T0.AddHours(1).AddMinutes(2), T0.AddHours(2), "LIRF_APP"),
        });
        Assert.Equal(1, g[1]);
        Assert.Equal(2, g[2]);
    }

    [Fact]
    public void Controllori_diversi_sulla_stessa_postazione_non_si_fondono()
    {
        var g = AtcShiftGrouper.Group(new[]
        {
            S(1, T0, T0.AddHours(1), "LIRF_TWR", vid: 704798),
            S(2, T0.AddHours(1).AddMinutes(2), T0.AddHours(2), "LIRF_TWR", vid: 762032),
        });
        Assert.Equal(1, g[1]);
        Assert.Equal(2, g[2]);
    }

    [Fact]
    public void Una_sessione_ancora_aperta_chiude_il_turno()
    {
        // Senza fine non si può dire che la prossima la continui: niente fusione a indovinare.
        var g = AtcShiftGrouper.Group(new[]
        {
            S(1, T0, null),
            S(2, T0.AddHours(1), T0.AddHours(2)),
        });
        Assert.Equal(1, g[1]);
        Assert.Equal(2, g[2]);
    }

    [Fact]
    public void Tre_pezzi_di_fila_restano_un_turno_solo()
    {
        var g = AtcShiftGrouper.Group(new[]
        {
            S(1, T0, T0.AddMinutes(50)),
            S(2, T0.AddMinutes(52), T0.AddMinutes(100)),
            S(3, T0.AddMinutes(105), T0.AddMinutes(180)),
        });
        Assert.Equal(new long[] { 1, 1, 1 }, new[] { g[1], g[2], g[3] });
    }

    [Fact]
    public void L_ordine_di_arrivo_non_conta()
    {
        var g = AtcShiftGrouper.Group(new[]
        {
            S(3, T0.AddMinutes(105), T0.AddMinutes(180)),
            S(1, T0, T0.AddMinutes(50)),
            S(2, T0.AddMinutes(52), T0.AddMinutes(100)),
        });
        Assert.Equal(new long[] { 1, 1, 1 }, new[] { g[1], g[2], g[3] });
    }

    [Fact]
    public void Senza_sessioni_non_esplode()
    {
        Assert.Empty(AtcShiftGrouper.Group(Array.Empty<ShiftInput>()));
    }
}