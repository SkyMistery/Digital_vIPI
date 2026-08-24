using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Stats;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La griglia ora × giorno: quando c'è copertura e quando resta il buco. Puro, tutto in UTC.
/// Lunedì 24 agosto 2026 è il lunedì di riferimento di questi test.
/// </summary>
public class CoverageGridTests
{
    private static readonly DateTimeOffset Lunedi = new(2026, 8, 24, 0, 0, 0, TimeSpan.Zero);

    private static OnlineSpan Span(int oraInizio, int minutiDurata, int giorniDopo = 0) =>
        new(Lunedi.AddDays(giorniDopo).AddHours(oraInizio),
            Lunedi.AddDays(giorniDopo).AddHours(oraInizio).AddMinutes(minutiDurata));

    private static CoverageCell Cella(IReadOnlyList<CoverageCell> g, int giorno, int ora) =>
        g.Single(c => c.DayOfWeek == giorno && c.Hour == ora);

    [Fact]
    public void La_griglia_ha_sempre_tutte_e_168_le_caselle()
    {
        // Una casella assente non si distinguerebbe da una vuota: la griglia si disegna intera o mente.
        var g = CoverageGrid.Build(Array.Empty<OnlineSpan>(), Lunedi, Lunedi.AddDays(7));

        Assert.Equal(168, g.Count);
        Assert.All(g, c => Assert.Equal(0, c.CoveredMinutes));
        Assert.Equal(7, g.Select(c => c.DayOfWeek).Distinct().Count());
    }

    [Fact]
    public void Una_sessione_a_cavallo_riempie_TUTTE_le_ore_che_attraversa()
    {
        // 20:40 → 23:10 non è «alle 20»: contarlo sull'ora d'inizio sposterebbe la copertura verso
        // l'orario in cui la gente si collega, che è il contrario di quel che si cerca.
        var turno = new OnlineSpan(Lunedi.AddHours(20).AddMinutes(40), Lunedi.AddHours(23).AddMinutes(10));
        var g = CoverageGrid.Build(new[] { turno }, Lunedi, Lunedi.AddDays(1));

        Assert.Equal(20, Cella(g, 1, 20).CoveredMinutes);
        Assert.Equal(60, Cella(g, 1, 21).CoveredMinutes);
        Assert.Equal(60, Cella(g, 1, 22).CoveredMinutes);
        Assert.Equal(10, Cella(g, 1, 23).CoveredMinutes);
    }

    [Fact]
    public void Tre_controllori_insieme_fanno_UN_ora_coperta_non_tre()
    {
        var insieme = new[] { Span(21, 60), Span(21, 60), Span(21, 60) };
        var g = CoverageGrid.Build(insieme, Lunedi, Lunedi.AddDays(1));

        Assert.Equal(60, Cella(g, 1, 21).CoveredMinutes);
        Assert.Equal(1.0, Cella(g, 1, 21).Ratio);
    }

    [Fact]
    public void Due_turni_che_si_accavallano_a_meta_contano_una_volta_sola()
    {
        var g = CoverageGrid.Build(new[] { Span(21, 60), Span(21, 90) }, Lunedi, Lunedi.AddDays(1));

        Assert.Equal(60, Cella(g, 1, 21).CoveredMinutes);
        Assert.Equal(30, Cella(g, 1, 22).CoveredMinutes);
    }

    [Fact]
    public void Due_turni_staccati_nella_stessa_ora_si_sommano()
    {
        var primo = new OnlineSpan(Lunedi.AddHours(21), Lunedi.AddHours(21).AddMinutes(15));
        var secondo = new OnlineSpan(Lunedi.AddHours(21).AddMinutes(40), Lunedi.AddHours(22));

        var g = CoverageGrid.Build(new[] { primo, secondo }, Lunedi, Lunedi.AddDays(1));

        Assert.Equal(35, Cella(g, 1, 21).CoveredMinutes);
    }

    [Fact]
    public void I_minuti_possibili_dicono_quante_volte_quella_casella_esiste()
    {
        // Due settimane: ogni casella capita due volte, quindi 120 minuti possibili.
        var g = CoverageGrid.Build(Array.Empty<OnlineSpan>(), Lunedi, Lunedi.AddDays(14));

        Assert.All(g, c => Assert.Equal(120, c.PossibleMinutes));
    }

    [Fact]
    public void La_domenica_e_il_settimo_giorno_non_il_primo()
    {
        // .NET conta la domenica come 0: qui la settimana comincia di lunedì, come si legge.
        var domenica = CoverageGrid.Build(new[] { Span(10, 60, giorniDopo: 6) }, Lunedi, Lunedi.AddDays(7));

        Assert.Equal(60, Cella(domenica, 7, 10).CoveredMinutes);
        Assert.Equal(0, Cella(domenica, 1, 10).CoveredMinutes);
    }

    [Fact]
    public void Fuori_dalla_finestra_non_si_conta_e_non_si_sfora()
    {
        // Sessione che comincia prima e finisce dopo: si ritaglia, e la casella non supera mai il 100%.
        var lungo = new OnlineSpan(Lunedi.AddDays(-2), Lunedi.AddDays(3));
        var g = CoverageGrid.Build(new[] { lungo }, Lunedi, Lunedi.AddDays(1));

        Assert.Equal(60, Cella(g, 1, 12).CoveredMinutes);
        Assert.All(g, c => Assert.True(c.CoveredMinutes <= c.PossibleMinutes));
        Assert.Equal(0, Cella(g, 2, 12).PossibleMinutes);   // martedì non è nella finestra
    }

    [Fact]
    public void Un_intervallo_al_contrario_o_vuoto_viene_ignorato()
    {
        var storto = new OnlineSpan(Lunedi.AddHours(12), Lunedi.AddHours(10));
        var vuoto = new OnlineSpan(Lunedi.AddHours(12), Lunedi.AddHours(12));

        var g = CoverageGrid.Build(new[] { storto, vuoto }, Lunedi, Lunedi.AddDays(1));
        Assert.All(g, c => Assert.Equal(0, c.CoveredMinutes));
    }

    [Fact]
    public void L_unione_fonde_anche_gli_intervalli_che_si_toccano()
    {
        var a = new OnlineSpan(Lunedi.AddHours(10), Lunedi.AddHours(11));
        var b = new OnlineSpan(Lunedi.AddHours(11), Lunedi.AddHours(12));

        var uniti = CoverageGrid.Unione(new[] { a, b }, Lunedi, Lunedi.AddDays(1));

        var solo = Assert.Single(uniti);
        Assert.Equal(Lunedi.AddHours(10), solo.StartUtc);
        Assert.Equal(Lunedi.AddHours(12), solo.EndUtc);
    }
}
