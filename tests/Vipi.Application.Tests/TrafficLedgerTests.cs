using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Stats;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il registro delle tratte in corso: conta i minuti per giro, riconosce le tratte, e scrive solo quando
/// serve (tratta nuova o checkpoint) invece di riversare tutto nel database ogni minuto.
/// </summary>
public class TrafficLedgerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 24, 18, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Check = TimeSpan.FromMinutes(10);

    private static bool Vedi(TrafficLedger l, DateTimeOffset quando, string pilota = "AZA123",
        string? dep = "LIRF", string? arr = "LIRN", long? fp = 900, FlightPhase fase = FlightPhase.Airborne,
        long sessione = 100, double quota = 24000) =>
        l.Observe(sessione, new LegObservation(pilota, 785031, fp, dep, arr, "B38M", fase, quota), quando);

    [Fact]
    public void Un_aereo_nuovo_apre_una_tratta_e_chiede_di_scrivere_subito()
    {
        var l = new TrafficLedger();
        Assert.True(Vedi(l, T0));                       // true = tratta nuova

        var flush = l.Take(T0, Check, new HashSet<long> { 100 });
        var riga = Assert.Single(flush.Legs);
        Assert.Equal("AZA123", riga.PilotCallsign);
        Assert.Equal(1, riga.LegOrdinal);
        Assert.Equal(1, riga.SeenMinutes);
    }

    [Fact]
    public void I_minuti_si_contano_per_giro_non_come_differenza_fra_primo_e_ultimo()
    {
        var l = new TrafficLedger();
        Vedi(l, T0);                    // dentro
        Vedi(l, T0.AddMinutes(1));      // dentro
        // ...esce per venti minuti e rientra: quei venti minuti NON sono suoi.
        Vedi(l, T0.AddMinutes(21));

        var riga = Assert.Single(l.TakeAll(T0.AddMinutes(21)).Legs);
        Assert.Equal(3, riga.SeenMinutes);
        Assert.Equal(T0, riga.FirstSeenUtc);
        Assert.Equal(T0.AddMinutes(21), riga.LastSeenUtc);
    }

    [Fact]
    public void Il_pilota_che_cade_e_rientra_resta_una_tratta_sola()
    {
        var l = new TrafficLedger();
        Vedi(l, T0);
        Assert.False(Vedi(l, T0.AddMinutes(4)));        // niente tratta nuova
        Assert.Single(l.TakeAll(T0.AddMinutes(4)).Legs);
    }

    [Fact]
    public void Un_secondo_volo_nella_stessa_connessione_apre_una_tratta_nuova()
    {
        var l = new TrafficLedger();
        Vedi(l, T0);
        Assert.True(Vedi(l, T0.AddMinutes(50), dep: "LIRN", arr: "LIRF", fp: 901));

        var righe = l.TakeAll(T0.AddMinutes(50)).Legs.OrderBy(r => r.LegOrdinal).ToList();
        Assert.Equal(new[] { 1, 2 }, righe.Select(r => r.LegOrdinal));
    }

    [Fact]
    public void Un_buco_di_osservazione_si_segna_sulla_riga_di_quel_volo()
    {
        var l = new TrafficLedger();
        Vedi(l, T0);
        Vedi(l, T0.AddHours(1));        // poller fermo: stesso piano di volo, tratta intatta ma incompleta

        var riga = Assert.Single(l.TakeAll(T0.AddHours(1)).Legs);
        Assert.True(riga.HasObservationGap);
        Assert.Equal(2, riga.SeenMinutes);
    }

    [Fact]
    public void Un_aereo_sempre_fermo_e_una_presenza_ma_non_un_movimento()
    {
        var l = new TrafficLedger();
        Vedi(l, T0, fase: FlightPhase.Parked);
        Vedi(l, T0.AddMinutes(1), fase: FlightPhase.Parked);
        l.EndPoll(100, true);

        var flush = l.TakeAll(T0.AddMinutes(1));
        Assert.False(flush.Legs.Single().SawMovement);

        var contatori = flush.Counters.Single();
        Assert.Equal(1, contatori.TrafficCount);       // presenza
        Assert.Equal(0, contatori.MovementCount);      // ma nessun movimento
    }

    [Fact]
    public void Appena_si_muove_diventa_un_movimento_e_non_torna_indietro()
    {
        var l = new TrafficLedger();
        Vedi(l, T0, fase: FlightPhase.Parked);
        Vedi(l, T0.AddMinutes(1), fase: FlightPhase.Ground);
        Vedi(l, T0.AddMinutes(2), fase: FlightPhase.Parked);

        Assert.True(l.TakeAll(T0.AddMinutes(2)).Legs.Single().SawMovement);
    }

    [Fact]
    public void Fra_un_checkpoint_e_l_altro_non_si_scrive_niente()
    {
        var l = new TrafficLedger();
        Vedi(l, T0);
        l.Take(T0, Check, new HashSet<long> { 100 });     // scritta subito perché nuova

        Vedi(l, T0.AddMinutes(1));
        Assert.True(l.Take(T0.AddMinutes(1), Check).Nothing);      // troppo presto
        Assert.True(l.Take(T0.AddMinutes(9), Check).Nothing);

        Vedi(l, T0.AddMinutes(11));
        Assert.False(l.Take(T0.AddMinutes(11), Check).Nothing);    // checkpoint
    }

    [Fact]
    public void I_minuti_occupato_contano_i_giri_con_traffico_non_gli_aerei()
    {
        var l = new TrafficLedger();
        Vedi(l, T0, pilota: "AZA123", fp: 900);
        Vedi(l, T0, pilota: "RYR456", fp: 901, dep: "EBBR", arr: "LIRF");
        l.EndPoll(100, true);
        l.EndPoll(100, false);          // giro senza nessuno
        l.EndPoll(100, true);

        var c = l.TakeAll(T0).Counters.Single();
        Assert.Equal(2, c.TrafficCount);      // due aerei
        Assert.Equal(2, c.TrafficMinutes);    // due minuti con traffico
    }

    [Fact]
    public void Dopo_un_riavvio_il_registro_riparte_da_quel_che_c_e_in_archivio()
    {
        var l = new TrafficLedger();
        l.Hydrate(100, new[]
        {
            new TrafficLegRow(100, "AZA123", 1, 785031, 900, "LIRF", "LIRN", "B38M",
                T0, T0.AddMinutes(20), SeenMinutes: 21, SawMovement: true, HasObservationGap: false),
        }, trafficMinutes: 21);

        Assert.True(l.Knows(100));
        Assert.False(Vedi(l, T0.AddMinutes(21)));      // continua la tratta, non ne apre una nuova

        var riga = Assert.Single(l.TakeAll(T0.AddMinutes(21)).Legs);
        Assert.Equal(22, riga.SeenMinutes);            // 21 già contati + questo giro
        Assert.Equal(21, l.TakeAll(T0).Counters.Single().TrafficMinutes);   // i minuti non tornano indietro
    }

    [Fact]
    public void Una_sessione_dimenticata_non_pesa_piu_sul_registro()
    {
        var l = new TrafficLedger();
        Vedi(l, T0);
        l.Forget(100);
        Assert.False(l.Knows(100));
        Assert.True(l.TakeAll(T0).Nothing);
    }

    [Fact]
    public void Prima_e_ultima_fase_raccontano_l_arrivo()
    {
        var l = new TrafficLedger();
        Vedi(l, T0, fase: FlightPhase.Airborne);
        Vedi(l, T0.AddMinutes(1), fase: FlightPhase.Ground);
        Vedi(l, T0.AddMinutes(2), fase: FlightPhase.Parked);

        var riga = Assert.Single(l.TakeAll(T0.AddMinutes(2)).Legs);
        Assert.Equal(FlightPhase.Airborne, riga.FirstPhase);
        Assert.Equal(FlightPhase.Parked, riga.LastPhase);
        Assert.True(riga.SawAirborne);          // in mezzo l'abbiamo visto volare: è un arrivo, non un rullaggio
    }

    [Fact]
    public void Chi_non_ha_mai_volato_non_risulta_atterrato()
    {
        var l = new TrafficLedger();
        Vedi(l, T0, fase: FlightPhase.Parked);
        Vedi(l, T0.AddMinutes(1), fase: FlightPhase.Ground);

        var riga = Assert.Single(l.TakeAll(T0.AddMinutes(1)).Legs);
        Assert.False(riga.SawAirborne);
        Assert.Equal(FlightPhase.Ground, riga.LastPhase);
    }

    [Fact]
    public void Le_quote_dicono_ingresso_uscita_e_massimo()
    {
        var l = new TrafficLedger();
        Vedi(l, T0, quota: 12000);
        Vedi(l, T0.AddMinutes(1), quota: 24000);
        Vedi(l, T0.AddMinutes(2), quota: 18000);

        var riga = Assert.Single(l.TakeAll(T0.AddMinutes(2)).Legs);
        Assert.Equal(12000, riga.EntryAltitudeFt);
        Assert.Equal(18000, riga.ExitAltitudeFt);
        Assert.Equal(24000, riga.MaxAltitudeFt);
    }

    [Fact]
    public void Una_quota_negativa_non_si_scrive()
    {
        // La sorgente dà quote sotto zero agli aerei al suolo con pressione bassa: «−200 ft» in una scheda
        // di volo sembra un errore nostro.
        var l = new TrafficLedger();
        Vedi(l, T0, quota: -200, fase: FlightPhase.Parked);

        var riga = Assert.Single(l.TakeAll(T0).Legs);
        Assert.Null(riga.EntryAltitudeFt);
        Assert.Null(riga.MaxAltitudeFt);
    }

    [Fact]
    public void La_consegna_si_scrive_su_tutte_e_due_le_sessioni()
    {
        var l = new TrafficLedger();
        Vedi(l, T0, sessione: 100);
        Vedi(l, T0.AddMinutes(1), sessione: 200);
        l.NoteHandoff(100, 200, "AZA123");

        var righe = l.TakeAll(T0.AddMinutes(1)).Legs.ToDictionary(r => r.SessionId);
        Assert.Equal(200, righe[100].HandoffToSessionId);
        Assert.Equal(100, righe[200].HandoffFromSessionId);
        Assert.Null(righe[100].HandoffFromSessionId);
    }

    [Fact]
    public void Una_consegna_verso_una_sessione_sconosciuta_non_scrive_niente()
    {
        // Meglio una consegna mancante che una attribuita a caso.
        var l = new TrafficLedger();
        Vedi(l, T0, sessione: 100);
        l.NoteHandoff(100, 999, "AZA123");

        var riga = Assert.Single(l.TakeAll(T0).Legs);
        Assert.Null(riga.HandoffToSessionId);
    }

    [Fact]
    public void La_consegna_va_sulla_tratta_in_corso_non_sulla_prima_del_turno()
    {
        var l = new TrafficLedger();
        Vedi(l, T0, sessione: 100, dep: "LIRF", arr: "LIRN", fp: 900);
        Vedi(l, T0.AddMinutes(1), sessione: 100, dep: "LIRN", arr: "LIRF", fp: 901);   // seconda tratta
        Vedi(l, T0.AddMinutes(2), sessione: 200, dep: "LIRN", arr: "LIRF", fp: 901);
        l.NoteHandoff(100, 200, "AZA123");

        var uscenti = l.TakeAll(T0.AddMinutes(2)).Legs.Where(r => r.SessionId == 100).ToList();
        Assert.Equal(2, uscenti.Count);
        Assert.Null(uscenti.Single(r => r.LegOrdinal == 1).HandoffToSessionId);
        Assert.Equal(200, uscenti.Single(r => r.LegOrdinal == 2).HandoffToSessionId);
    }
}
