using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Abstractions;
using Vipi.Application.Stats;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Cosa scrive il poller dopo un giro: sessioni aperte, aggiornate, chiuse — e il turno assegnato alla
/// nascita. Puro: l'istante lo passa il chiamante.
/// </summary>
public class AtcSessionSyncTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 24, 18, 0, 0, TimeSpan.Zero);

    private static SourceAtcConnection Conn(long id, DateTimeOffset start, int secondi,
        string callsign = "LIRF_TWR", int vid = 704798) =>
        new(id, vid, callsign, "TWR", "118.700", Rating: 4, StartUtc: start, ConnectedSeconds: secondi);

    private static KnownAtcSession Nota(long id, DateTimeOffset start, DateTimeOffset? fine, long turno,
        string callsign = "LIRF_TWR", int vid = 704798) =>
        new(id, vid, callsign, start, fine, turno);

    [Fact]
    public void Una_connessione_mai_vista_apre_una_sessione_e_un_turno_suo()
    {
        var p = AtcSessionSync.Plan(new[] { Conn(100, T0, 600) }, Array.Empty<KnownAtcSession>(), T0.AddMinutes(10));

        var u = Assert.Single(p.Upserts);
        Assert.True(u.IsNew);
        Assert.Equal(100, u.SessionId);
        Assert.Equal(100, u.ShiftKey);        // apre un turno proprio
        Assert.Equal(600, u.DurationSeconds);
        Assert.Empty(p.Closures);
    }

    [Fact]
    public void Una_sessione_gia_nota_si_aggiorna_e_non_cambia_turno()
    {
        var known = new[] { Nota(100, T0, null, turno: 55) };
        var p = AtcSessionSync.Plan(new[] { Conn(100, T0, 1200) }, known, T0.AddMinutes(20));

        var u = Assert.Single(p.Upserts);
        Assert.False(u.IsNew);
        Assert.Equal(55, u.ShiftKey);         // il turno non si ricalcola a ogni giro
        Assert.Equal(1200, u.DurationSeconds);
    }

    [Fact]
    public void Chi_non_e_piu_in_frequenza_viene_chiuso()
    {
        var known = new[] { Nota(100, T0, null, 100), Nota(101, T0, null, 101, callsign: "LIRF_GND") };
        var ora = T0.AddMinutes(30);

        var p = AtcSessionSync.Plan(new[] { Conn(100, T0, 1800) }, known, ora);

        var c = Assert.Single(p.Closures);
        Assert.Equal(101, c.SessionId);
        Assert.Equal(ora, c.EndUtc);
    }

    [Fact]
    public void Una_sessione_gia_chiusa_non_si_richiude()
    {
        var known = new[] { Nota(100, T0, T0.AddMinutes(10), 100) };
        var p = AtcSessionSync.Plan(Array.Empty<SourceAtcConnection>(), known, T0.AddHours(1));
        Assert.True(p.Nothing);
    }

    // --- Il turno ---------------------------------------------------------------------------------------

    [Fact]
    public void Riconnettersi_subito_dopo_una_caduta_continua_lo_stesso_turno()
    {
        // Misurato sulle sessioni italiane vere: succede al 38% delle connessioni.
        var known = new[] { Nota(100, T0, T0.AddHours(1), turno: 100) };
        var ripresa = T0.AddHours(1).AddMinutes(3);

        var p = AtcSessionSync.Plan(new[] { Conn(101, ripresa, 60) }, known, ripresa);

        Assert.Equal(100, Assert.Single(p.Upserts).ShiftKey);
    }

    [Fact]
    public void Dopo_una_pausa_lunga_il_turno_e_nuovo()
    {
        var known = new[] { Nota(100, T0, T0.AddHours(1), turno: 100) };
        var ripresa = T0.AddHours(3);

        var p = AtcSessionSync.Plan(new[] { Conn(101, ripresa, 60) }, known, ripresa);

        Assert.Equal(101, Assert.Single(p.Upserts).ShiftKey);
    }

    [Fact]
    public void Il_turno_si_eredita_a_catena_su_piu_cadute()
    {
        // Tre spezzoni: il terzo deve tornare al turno del primo, non a quello del secondo.
        var known = new[]
        {
            Nota(100, T0, T0.AddMinutes(50), turno: 100),
            Nota(101, T0.AddMinutes(52), T0.AddMinutes(100), turno: 100),
        };
        var terza = T0.AddMinutes(105);

        var p = AtcSessionSync.Plan(new[] { Conn(102, terza, 60) }, known, terza);

        Assert.Equal(100, Assert.Single(p.Upserts).ShiftKey);
    }

    [Fact]
    public void Un_altro_controllore_sulla_stessa_postazione_non_eredita_il_turno()
    {
        var known = new[] { Nota(100, T0, T0.AddHours(1), turno: 100, vid: 704798) };
        var cambio = T0.AddHours(1).AddMinutes(2);

        var p = AtcSessionSync.Plan(new[] { Conn(101, cambio, 60, vid: 762032) }, known, cambio);

        Assert.Equal(101, Assert.Single(p.Upserts).ShiftKey);
    }

    [Fact]
    public void Cambiare_postazione_apre_un_turno_nuovo()
    {
        var known = new[] { Nota(100, T0, T0.AddHours(1), turno: 100, callsign: "LIRF_TWR") };
        var cambio = T0.AddHours(1).AddMinutes(2);

        var p = AtcSessionSync.Plan(new[] { Conn(101, cambio, 60, callsign: "LIRF_APP") }, known, cambio);

        Assert.Equal(101, Assert.Single(p.Upserts).ShiftKey);
    }

    [Fact]
    public void Una_sessione_ancora_aperta_non_cede_il_turno_a_una_nuova()
    {
        // Doppia connessione, o una sessione che il poller non ha ancora chiuso: non si tira a indovinare.
        var known = new[] { Nota(100, T0, null, turno: 100) };
        var altra = T0.AddMinutes(30);

        var p = AtcSessionSync.Plan(new[] { Conn(100, T0, 1800), Conn(101, altra, 60) }, known, altra);

        var nuova = p.Upserts.Single(u => u.SessionId == 101);
        Assert.Equal(101, nuova.ShiftKey);
    }

    [Fact]
    public void Senza_nessuno_in_frequenza_e_senza_sessioni_aperte_non_si_scrive_niente()
    {
        Assert.True(AtcSessionSync.Plan(
            Array.Empty<SourceAtcConnection>(), Array.Empty<KnownAtcSession>(), T0).Nothing);
    }

    [Fact]
    public void La_marca_fuori_divisione_viaggia_dalla_sorgente_alla_riga()
    {
        // Dal 28 agosto 2026 nel piano finiscono anche le postazioni del resto del mondo. Il pianificatore
        // non le tratta diversamente — aprono, si aggiornano e si chiudono come tutte — ma la marca deve
        // arrivare intatta fino all'archivio, o le righe nuove sarebbero indistinguibili da quelle italiane.
        var estera = Conn(200, T0, 600, callsign: "EDDF_TWR") with { IsOutsideDivision = true };

        var p = AtcSessionSync.Plan(
            new[] { Conn(100, T0, 600), estera }, Array.Empty<KnownAtcSession>(), T0.AddMinutes(10));

        Assert.False(p.Upserts.Single(u => u.SessionId == 100).IsOutsideDivision);
        Assert.True(p.Upserts.Single(u => u.SessionId == 200).IsOutsideDivision);
    }

    [Fact]
    public void Anche_una_postazione_estera_si_chiude_quando_sparisce()
    {
        // ⚠️ È il difetto che si pagherebbe filtrando troppo presto: se la lettura delle sessioni note
        // saltasse il resto del mondo, una connessione straniera resterebbe APERTA per sempre in archivio.
        var known = new[] { Nota(200, T0, null, turno: 200, callsign: "EDDF_TWR") };

        var p = AtcSessionSync.Plan(Array.Empty<SourceAtcConnection>(), known, T0.AddMinutes(20));

        var chiusa = Assert.Single(p.Closures);
        Assert.Equal(200, chiusa.SessionId);
        Assert.Equal(T0.AddMinutes(20), chiusa.EndUtc);
    }
}
