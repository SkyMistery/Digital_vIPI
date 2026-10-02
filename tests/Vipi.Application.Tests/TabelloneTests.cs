using System.Text.Json;
using Vipi.Application.Abstractions;
using Vipi.Application.Tabellone;

namespace Vipi.Application.Tests;

/// <summary>
/// Le regole del tabellone partenze/arrivi (carta docs/feature/2026-10-02-tabellone-partenze-arrivi.md; formato
/// concordato in Dep_arr_board/FORMATO-DATI.md): stati, fusione booking + Whazzup, stand effettivo e GATE CHANGE,
/// dati vecchi, finestra, uscite e ordine. Su risposte salvate (<c>Fixtures/tabellone/</c>: la forma vera del booking
/// e del documento del ponte, con dati inventati), mai sui server veri.
/// </summary>
public class TabelloneTests
{
    private static readonly DateTimeOffset Ora = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private static string Fixture(string nome) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tabellone", nome));

    private static LetturaBooking Booking() => BookingParser.Leggi(Fixture("booking-lirf.json"));

    private static BoardDelPonte.Board Board() => BoardDelPonte.Leggi(Fixture("ponte-board.json"))!;

    private static (string? Citta, string? Iata)? Anagrafica(string icao) => icao switch
    {
        "LIRN" => ("Naples", "NAP"),
        "EGSS" => ("London", "STN"),
        "EDDF" => ("Frankfurt", "FRA"),
        "LIML" => ("Milan", "LIN"),
        "LIPK" => ("Forlì", "FRL"),
        _ => null,
    };

    private static SourcePilotFix Pilota(string callsign, string dep, string arr, string? stato, bool aTerra = true,
        double gs = 0, double? daPartenza = null, double? daArrivo = null, int? partenzaSec = null, int? eet = null,
        int? partitoSec = null, string tipo = "A320") =>
        new(1, 999999, callsign, 41.8, 12.2, aTerra ? 10 : 9000, gs, aTerra, stato, daPartenza, 1, dep, arr, tipo,
            new SourcePilotPlan(daArrivo, eet, partenzaSec, partitoSec, Ora.AddHours(-3)));

    private static RispostaTabellone Calcola(
        IReadOnlyList<SourcePilotFix>? piloti = null,
        DateTimeOffset? adesso = null,
        DateTimeOffset? pilotiAl = null,
        bool conBooking = true,
        LetturaBooking? booking = null,
        DateTimeOffset? bookingLetto = null,
        bool conBoard = true,
        bool eventoRfo = true,
        string voli = RegoleTabellone.VoliTutti,
        MemoriaTabellone? memoria = null)
    {
        var t = adesso ?? Ora;
        var b = conBooking ? booking ?? Booking() : null;
        var board = conBoard ? Board() : null;
        var ingressi = new IngressiTabellone(
            new ScaloTabellone("LIRF", "FCO", "ROMA FIUMICINO", "Europe/Rome"),
            "Roma RFE", voli,
            b, b is null ? null : bookingLetto ?? t.AddSeconds(-30),
            new FotografiaPiloti.Istantanea(piloti ?? Array.Empty<SourcePilotFix>(), pilotiAl ?? t.AddSeconds(-20)),
            board?.Stand, board?.Al, eventoRfo,
            Anagrafica);
        return RegoleTabellone.Calcola(ingressi, memoria ?? new MemoriaTabellone(), t);
    }

    private static RigaTabellone Arrivo(RispostaTabellone r, string id) => Assert.Single(r.Arrivi, x => x.Id == id);
    private static RigaTabellone Partenza(RispostaTabellone r, string id) => Assert.Single(r.Partenze, x => x.Id == id);

    // ---------------------------------------------------------------- booking

    [Fact]
    public void Il_booking_scarta_gli_slot_che_nessuno_ha_preso_e_le_voci_che_non_sono_voli()
    {
        var b = Booking();

        Assert.True(b.Ok);
        Assert.Equal(new[] { "AZA1", "AZA2", "RYR3", "DLH4", "AFR7", "KLM8", "ITY9", "AZA10" }, b.Voli.Select(v => v.Callsign));
        Assert.Null(b.Voli.Single(v => v.Callsign == "RYR3").Gate);                // «TBD»
        Assert.Equal(100001, b.Voli.Single(v => v.Callsign == "AZA2").Vid);         // anche come testo
        Assert.Equal(new DateTimeOffset(2026, 10, 3, 12, 30, 0, TimeSpan.Zero), b.Voli.Single(v => v.Callsign == "AZA1").Eat);
    }

    [Theory]
    [InlineData("<br /><b>Fatal error</b>: Uncaught PDOException")]   // 200 anche sugli errori PHP
    [InlineData("{\"error\":\"unauthorized\"}")]
    [InlineData("")]
    public void Un_booking_che_non_e_un_elenco_di_voli_e_una_lettura_fallita(string corpo)
    {
        Assert.False(BookingParser.Leggi(corpo).Ok);
    }

    // ---------------------------------------------------------------- fusione

    [Fact]
    public void Lo_stesso_callsign_nelle_due_fonti_e_una_riga_sola_col_programmato_del_booking_e_la_stima_di_Whazzup()
    {
        var r = Calcola(new[] { Pilota("AZA1", "LIRN", "LIRF", "Approaching", aTerra: false, gs: 250, daArrivo: 25) });

        var aza = Arrivo(r, "AZA1");
        Assert.Single(r.Arrivi, x => x.Volo == "AZA1");
        Assert.True(aza.Prenotato);
        Assert.True(aza.Online);
        Assert.Equal(Ora.AddMinutes(30), aza.Programmato);
        // 25 NM a 250 kt = 6', più i 6' per le ultime miglia, dall'ora della fotografia (20 s fa, ai secondi).
        Assert.Equal(Ora.AddSeconds(-20).AddMinutes(12), aza.Stimato);
        Assert.Equal(RegoleTabellone.Approaching, aza.Stato);
        Assert.Equal(new AltroScalo("LIRN", "NAP", "NAPLES"), aza.Scalo);
        Assert.Equal("AZA", aza.Compagnia);
        Assert.Equal("A320", aza.Aereo);
    }

    [Fact]
    public void Un_volo_solo_su_Whazzup_prende_il_programmato_dal_piano()
    {
        var r = Calcola(new[]
        {
            Pilota("IABCD", "LIRF", "LIPK", "Boarding", partenzaSec: 12 * 3600 + 20 * 60, tipo: "C172"),
            Pilota("EZY77", "LFPG", "LIRF", "En Route", aTerra: false, gs: 450, daArrivo: 300, partenzaSec: 10 * 3600 + 30 * 60, eet: 7200),
        });

        var privato = Partenza(r, "IABCD");
        Assert.False(privato.Prenotato);
        Assert.Null(privato.Compagnia);                                // una marca, non una compagnia
        Assert.Equal(Ora.AddMinutes(20), privato.Programmato);
        Assert.Equal(new AltroScalo("LIPK", "FRL", "FORLI"), privato.Scalo);
        Assert.Null(privato.Gate);

        var ezy = Arrivo(r, "EZY77");
        Assert.Equal(Ora.AddMinutes(30), ezy.Programmato);             // partenza 10:30 + eet 2 h
        Assert.Equal(RegoleTabellone.Airborne, ezy.Stato);
        Assert.Equal(new AltroScalo("LFPG", null, "LFPG"), ezy.Scalo); // fuori dall'anagrafica: l'ICAO
    }

    [Fact]
    public void Con_prenotati_i_voli_solo_Whazzup_restano_fuori()
    {
        var piloti = new[] { Pilota("IABCD", "LIRF", "LIPK", "Boarding", partenzaSec: 12 * 3600 + 20 * 60) };

        Assert.Contains(Calcola(piloti).Partenze, x => x.Id == "IABCD");

        var r = Calcola(piloti, voli: "prenotati");
        Assert.Equal("prenotati", r.Voli);
        Assert.DoesNotContain(r.Partenze, x => x.Id == "IABCD");
        Assert.All(r.Partenze.Concat(r.Arrivi), x => Assert.True(x.Prenotato));
    }

    [Fact]
    public void Fuori_dagli_eventi_va_col_solo_Whazzup_e_con_tutti_i_voli()
    {
        var r = Calcola(new[] { Pilota("IABCD", "LIRF", "LIPK", "Boarding", partenzaSec: 12 * 3600 + 20 * 60) },
            conBooking: false, conBoard: false, eventoRfo: false, voli: "prenotati");

        Assert.Null(r.Evento);
        Assert.Equal("tutti", r.Voli);
        Assert.False(r.Fonti.Booking.Ok);
        Assert.False(r.Fonti.GateManager.Ok);
        Assert.True(r.Fonti.Whazzup.Ok);
        Assert.Equal("IABCD", Assert.Single(r.Partenze).Id);
        Assert.Empty(r.Arrivi);
    }

    [Fact]
    public void L_evento_ha_il_nome_della_configurazione_ed_e_attivo_solo_vicino_ai_suoi_voli()
    {
        Assert.Equal(new EventoTabellone("ROMA RFE", true), Calcola().Evento);
        Assert.False(Calcola(adesso: Ora.AddDays(-1)).Evento!.Attivo);   // il booking di domani
    }

    [Fact]
    public void Lo_stesso_callsign_due_volte_nella_giornata_ha_due_righe()
    {
        var b = new LetturaBooking(new[]
        {
            new VoloPrenotato("AZA1", "LIRN", "LIRF", "A320", null, Ora.AddHours(3), "509", 100001),
            new VoloPrenotato("AZA1", "LIRN", "LIRF", "A320", null, Ora.AddMinutes(30), "509", 100001),
        }, null);

        var r = Calcola(booking: b);

        Assert.Equal(new[] { "AZA1", "AZA1#2" }, r.Arrivi.Select(x => x.Id));
        Assert.Equal(Ora.AddMinutes(30), Arrivo(r, "AZA1").Programmato);
    }

    // ---------------------------------------------------------------- stati

    [Fact]
    public void Gli_stati_delle_partenze()
    {
        var r = Calcola(new[] { Pilota("ITY9", "LIRF", "LIML", "Boarding", partenzaSec: 12 * 3600 + 10 * 60) });

        Assert.Equal(RegoleTabellone.Boarding, Partenza(r, "ITY9").Stato);
        Assert.Equal(RegoleTabellone.Scheduled, Partenza(r, "RYR3").Stato);
        Assert.False(Partenza(r, "RYR3").Online);
        Assert.Equal(new AltroScalo("EGSS", "STN", "LONDON"), Partenza(r, "RYR3").Scalo);

        Assert.Equal(RegoleTabellone.Departed,
            Partenza(Calcola(new[] { Pilota("ITY9", "LIRF", "LIML", "Departing", gs: 15) }), "ITY9").Stato);
        Assert.Equal(RegoleTabellone.Departed,
            Partenza(Calcola(new[] { Pilota("ITY9", "LIRF", "LIML", "Initial Climb", aTerra: false, gs: 180) }), "ITY9").Stato);
        // «On Blocks» al parcheggio della partenza: si è appena connesso, sta imbarcando.
        Assert.Equal(RegoleTabellone.Boarding,
            Partenza(Calcola(new[] { Pilota("ITY9", "LIRF", "LIML", "On Blocks", daPartenza: 0.4) }), "ITY9").Stato);
    }

    [Fact]
    public void Gli_stati_degli_arrivi()
    {
        string Stato(SourcePilotFix p) => Arrivo(Calcola(new[] { p }), "AZA1").Stato;

        Assert.Equal(RegoleTabellone.Scheduled, Arrivo(Calcola(), "AZA1").Stato);
        Assert.Equal(RegoleTabellone.Airborne, Stato(Pilota("AZA1", "LIRN", "LIRF", "En Route", aTerra: false, gs: 420, daArrivo: 100)));
        Assert.Equal(RegoleTabellone.Approaching, Stato(Pilota("AZA1", "LIRN", "LIRF", "Approaching", aTerra: false, gs: 250, daArrivo: 20)));
        Assert.Equal(RegoleTabellone.Landed, Stato(Pilota("AZA1", "LIRN", "LIRF", "Landed", gs: 60, daPartenza: 105)));
        Assert.Equal(RegoleTabellone.Landed, Stato(Pilota("AZA1", "LIRN", "LIRF", "On Blocks", daPartenza: 105)));
        // «On Blocks» ancora a Napoli: non è atterrato a Fiumicino, deve partire (misurato il 24/08: è la trappola).
        Assert.Equal(RegoleTabellone.OnTime, Stato(Pilota("AZA1", "LIRN", "LIRF", "On Blocks", daPartenza: 0.3,
            partenzaSec: 11 * 3600 + 30 * 60, eet: 1200)));
    }

    [Fact]
    public void In_ritardo_quando_la_stima_supera_di_15_minuti_il_programmato()
    {
        // A terra a Napoli all'ora del calcolo: parte adesso. Programmato 12:30.
        RigaTabellone Con(int eet) => Arrivo(Calcola(new[]
            { Pilota("AZA1", "LIRN", "LIRF", "Boarding", daPartenza: 0.2, partenzaSec: 11 * 3600 + 30 * 60, eet: eet) }), "AZA1");

        // La fotografia è delle 11:59:40: si parte da lì.
        var inOrario = Con(eet: 20 * 60);         // 11:59:40 + 20' + 6' = 12:25:40
        Assert.Equal(RegoleTabellone.OnTime, inOrario.Stato);
        Assert.Equal(Ora.AddSeconds(-20).AddMinutes(26), inOrario.Stimato);

        Assert.Equal(RegoleTabellone.OnTime, Con(eet: 39 * 60).Stato);    // 12:44:40: 14'40" di ritardo
        Assert.Equal(RegoleTabellone.Delayed, Con(eet: 40 * 60).Stato);   // 12:45:40: 15'40"
    }

    // ---------------------------------------------------------------- stand effettivo

    [Fact]
    public void Lo_stand_e_quello_del_Gate_Manager_e_GATE_CHANGE_solo_se_cambia_quello_prenotato()
    {
        var r = Calcola();

        Assert.True(r.Fonti.GateManager.Ok);
        Assert.Equal(new DateTimeOffset(2026, 10, 3, 11, 58, 0, TimeSpan.Zero), r.Fonti.GateManager.Letto);

        Assert.Equal(("507", true), (Arrivo(r, "AZA1").Gate, Arrivo(r, "AZA1").GateCambiato));        // 509 → 507
        Assert.Equal(("509", false), (Partenza(r, "AZA2").Gate, Partenza(r, "AZA2").GateCambiato));    // confermato
        Assert.Equal(("510", false), (Partenza(r, "RYR3").Gate, Partenza(r, "RYR3").GateCambiato));    // TBD → 510: lo DÀ
        Assert.Equal(("511", false), (Partenza(r, "ITY9").Gate, Partenza(r, "ITY9").GateCambiato));    // non in board
    }

    [Fact]
    public void Con_boardAt_vecchio_di_oltre_10_minuti_si_torna_allo_stand_del_booking()
    {
        var r = Calcola(adesso: new DateTimeOffset(2026, 10, 3, 12, 8, 30, TimeSpan.Zero));   // boardAt 11:58 + 10'30"

        Assert.False(r.Fonti.GateManager.Ok);
        Assert.Equal(("509", false), (Arrivo(r, "AZA1").Gate, Arrivo(r, "AZA1").GateCambiato));
        Assert.Null(Partenza(r, "RYR3").Gate);                                                       // TBD

        Assert.True(Calcola(adesso: new DateTimeOffset(2026, 10, 3, 12, 7, 30, TimeSpan.Zero)).Fonti.GateManager.Ok);
    }

    [Fact]
    public void Senza_evento_RFO_lo_stand_e_quello_del_booking()
    {
        var r = Calcola(eventoRfo: false);

        Assert.False(r.Fonti.GateManager.Ok);
        Assert.Null(r.Fonti.GateManager.Letto);
        Assert.Equal("509", Arrivo(r, "AZA1").Gate);
    }

    [Fact]
    public void Il_documento_del_ponte_senza_board_o_illeggibile_non_e_un_errore()
    {
        var b = BoardDelPonte.Leggi("""{ "schema": 2, "pins": {} }""");
        Assert.NotNull(b);
        Assert.Empty(b!.Stand);
        Assert.Null(b.Al);
        Assert.Null(BoardDelPonte.Leggi("non è json"));
        Assert.False(Board().Stand.ContainsKey("dep:XXX1"));   // stand vuoto: nessuno stand
    }

    // ---------------------------------------------------------------- dati vecchi

    [Fact]
    public void Un_Whazzup_fermo_da_oltre_3_minuti_non_si_usa_piu()
    {
        var piloti = new[] { Pilota("ITY9", "LIRF", "LIML", "Boarding", partenzaSec: 12 * 3600 + 10 * 60) };

        var fermo = Calcola(piloti, pilotiAl: Ora.AddMinutes(-3).AddSeconds(-1));
        Assert.False(fermo.Fonti.Whazzup.Ok);
        Assert.Equal(Ora.AddMinutes(-3).AddSeconds(-1), fermo.Fonti.Whazzup.Letto);
        Assert.Equal(RegoleTabellone.Scheduled, Partenza(fermo, "ITY9").Stato);
        Assert.All(fermo.Partenze.Concat(fermo.Arrivi), x => Assert.False(x.Online));

        var buono = Calcola(piloti, pilotiAl: Ora.AddMinutes(-3));
        Assert.True(buono.Fonti.Whazzup.Ok);
        Assert.Equal(RegoleTabellone.Boarding, Partenza(buono, "ITY9").Stato);
    }

    [Fact]
    public void Un_booking_che_tace_da_oltre_3_minuti_e_segnalato_ma_le_righe_restano()
    {
        var r = Calcola(bookingLetto: Ora.AddMinutes(-5));

        Assert.False(r.Fonti.Booking.Ok);
        Assert.Equal(Ora.AddMinutes(-5), r.Fonti.Booking.Letto);
        Assert.Contains(r.Partenze, x => x.Id == "RYR3");
        Assert.NotNull(r.Evento);
    }

    [Fact]
    public void Mai_mai_mai_un_VID_nel_JSON()
    {
        var r = Calcola(new[] { Pilota("AZA1", "LIRN", "LIRF", "En Route", aTerra: false, gs: 420, daArrivo: 100) });
        var json = JsonSerializer.Serialize(r, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.DoesNotContain("10000", json);    // i VID del booking salvato: 100001…100007
        Assert.DoesNotContain("999999", json);   // il VID del pilota
        Assert.DoesNotContain("vid", json, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- finestra e uscite

    [Fact]
    public void Una_partenza_sparisce_10_minuti_dopo_DEPARTED()
    {
        var memoria = new MemoriaTabellone();
        RispostaTabellone Al(int minuti, string stato) => Calcola(
            new[] { Pilota("ITY9", "LIRF", "LIML", stato, aTerra: stato == "Departing", gs: 150) },
            adesso: Ora.AddMinutes(minuti), memoria: memoria);

        Assert.Equal(RegoleTabellone.Departed, Partenza(Al(0, "Departing"), "ITY9").Stato);
        Assert.Equal(RegoleTabellone.Departed, Partenza(Al(9, "Initial Climb"), "ITY9").Stato);
        Assert.DoesNotContain(Al(11, "En Route").Partenze, x => x.Id == "ITY9");
    }

    [Fact]
    public void Una_partenza_decollata_da_tempo_vista_adesso_per_la_prima_volta_non_resta_altri_10_minuti()
    {
        // Sito appena riavviato: ITY9 è decollato alle 11:40 (partenza effettiva del piano), lo vediamo alle 12:00.
        var r = Calcola(new[] { Pilota("ITY9", "LIRF", "LIML", "En Route", aTerra: false, gs: 400, partitoSec: 11 * 3600 + 40 * 60) });
        Assert.DoesNotContain(r.Partenze, x => x.Id == "ITY9");

        // Decollato alle 11:55: c'è ancora, fino alle 12:05.
        var appena = Calcola(new[] { Pilota("ITY9", "LIRF", "LIML", "Initial Climb", aTerra: false, gs: 180, partitoSec: 11 * 3600 + 55 * 60) });
        Assert.Equal(Ora.AddMinutes(-5), Partenza(appena, "ITY9").Stimato);
    }

    [Fact]
    public void Una_partenza_decollata_e_poi_disconnessa_resta_DEPARTED()
    {
        var memoria = new MemoriaTabellone();
        Calcola(new[] { Pilota("ITY9", "LIRF", "LIML", "Initial Climb", aTerra: false, gs: 180) }, memoria: memoria);

        var dopo = Partenza(Calcola(adesso: Ora.AddMinutes(5), memoria: memoria), "ITY9");
        Assert.Equal(RegoleTabellone.Departed, dopo.Stato);
        Assert.False(dopo.Online);
    }

    [Fact]
    public void Un_arrivo_sparisce_20_minuti_dopo_LANDED()
    {
        var memoria = new MemoriaTabellone();
        RispostaTabellone Al(int minuti) => Calcola(
            new[] { Pilota("AZA1", "LIRN", "LIRF", "Landed", gs: 20, daPartenza: 105) },
            adesso: Ora.AddMinutes(minuti), memoria: memoria);

        var atterrato = Arrivo(Al(0), "AZA1");
        Assert.Equal(RegoleTabellone.Landed, atterrato.Stato);
        Assert.Equal(Ora.AddSeconds(-20), atterrato.Stimato);   // l'ora in cui è stato visto atterrato
        Assert.Contains(Al(19).Arrivi, x => x.Id == "AZA1");
        Assert.DoesNotContain(Al(21).Arrivi, x => x.Id == "AZA1");
    }

    [Fact]
    public void Un_prenotato_mai_visto_online_sparisce_60_minuti_dopo_il_programmato()
    {
        // AFR7: arrivo programmato alle 10:40.
        Assert.Equal(RegoleTabellone.Scheduled, Arrivo(Calcola(adesso: Ora.AddMinutes(-81)), "AFR7").Stato);   // 10:39
        Assert.Contains(Calcola(adesso: Ora.AddMinutes(-20)).Arrivi, x => x.Id == "AFR7");                    // 11:40
        Assert.DoesNotContain(Calcola(adesso: Ora.AddMinutes(-19)).Arrivi, x => x.Id == "AFR7");              // 11:41
    }

    [Fact]
    public void La_finestra_arriva_a_6_ore()
    {
        // KLM8 alle 20:00.
        Assert.DoesNotContain(Calcola().Partenze, x => x.Id == "KLM8");
        Assert.Contains(Calcola(adesso: Ora.AddHours(2)).Partenze, x => x.Id == "KLM8");
    }

    [Fact]
    public void Chi_si_prenota_da_solo_ha_solo_l_eobt_che_per_un_arrivo_e_l_ora_d_arrivo()
    {
        var dlh = Arrivo(Calcola(), "DLH4");
        Assert.Equal(Ora.AddMinutes(45), dlh.Programmato);
        Assert.Equal(new AltroScalo("EDDF", "FRA", "FRANKFURT"), dlh.Scalo);
        Assert.Equal("512", dlh.Gate);
    }

    // ---------------------------------------------------------------- ordine

    [Fact]
    public void L_ordine_e_per_programmato_e_non_cambia_con_la_stima()
    {
        var senza = Calcola();
        Assert.Equal(new[] { "ITY9", "RYR3", "AZA2" }, senza.Partenze.Select(x => x.Id));
        Assert.Equal(new[] { "AZA1", "DLH4" }, senza.Arrivi.Select(x => x.Id));

        // DLH4 in volo con una stima PRIMA di AZA1: le righe non saltano.
        var con = Calcola(new[] { Pilota("DLH4", "EDDF", "LIRF", "Approaching", aTerra: false, gs: 250, daArrivo: 10) });
        Assert.True(Arrivo(con, "DLH4").Stimato < Arrivo(con, "AZA1").Programmato);
        Assert.Equal(new[] { "AZA1", "DLH4" }, con.Arrivi.Select(x => x.Id));
    }

    [Fact]
    public void Al_massimo_40_righe_per_lista()
    {
        var piloti = Enumerable.Range(1, 50)
            .Select(i => Pilota($"TST{i:000}", "LIRF", "LIML", "Boarding", partenzaSec: 12 * 3600 + i * 60))
            .ToList();

        var r = Calcola(piloti, conBooking: false);
        Assert.Equal(40, r.Partenze.Count);
        Assert.Equal("TST001", r.Partenze[0].Id);
    }

    // ---------------------------------------------------------------- testi e orari

    [Theory]
    [InlineData("Forlì", 16, "FORLI")]
    [InlineData("São Paulo/Guarulhos!", 16, "SAO PAULO/GUARUL")]
    [InlineData("  roma   fiumicino ", 32, "ROMA FIUMICINO")]
    [InlineData("Reggio (Calabria)", 16, "REGGIO CALABRIA")]
    [InlineData(null, 4, "")]
    public void I_testi_sono_pronti_per_le_palette(string? grezzo, int massimo, string atteso)
    {
        Assert.Equal(atteso, RegoleTabellone.Palette(grezzo, massimo));
    }

    [Theory]
    [InlineData("AZA123", "AZA")]
    [InlineData("EZY45AB", "EZY")]
    [InlineData("IABCD", null)]
    [InlineData("N123AB", null)]
    public void La_compagnia_solo_per_un_callsign_di_compagnia(string callsign, string? atteso)
    {
        Assert.Equal(atteso, RegoleTabellone.Compagnia(callsign));
    }

    [Fact]
    public void Gli_orari_del_piano_prendono_il_giorno_da_createdAt()
    {
        var sera = new DateTimeOffset(2026, 10, 2, 23, 30, 0, TimeSpan.Zero);
        Assert.Equal(new DateTimeOffset(2026, 10, 3, 0, 30, 0, TimeSpan.Zero), RegoleTabellone.OrarioDalPiano(1800, sera, null));

        var notte = new DateTimeOffset(2026, 10, 3, 0, 10, 0, TimeSpan.Zero);
        Assert.Equal(new DateTimeOffset(2026, 10, 2, 23, 50, 0, TimeSpan.Zero), RegoleTabellone.OrarioDalPiano(85800, notte, null));

        Assert.Null(RegoleTabellone.OrarioDalPiano(null, sera, null));
        Assert.Null(RegoleTabellone.OrarioDalPiano(1800, null, null));
    }
}
