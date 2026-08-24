using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Stats;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>Settimane consecutive con almeno un turno.</summary>
public class ControllerStreakTests
{
    /// <summary>Lunedì 24 agosto 2026, ore 18:00Z.</summary>
    private static readonly DateTimeOffset Adesso = new(2026, 8, 24, 18, 0, 0, TimeSpan.Zero);

    private static IEnumerable<DateTimeOffset> SettimaneFa(params int[] quante) =>
        quante.Select(n => Adesso.AddDays(-7 * n));

    [Fact]
    public void Senza_turni_non_c_e_striscia()
    {
        var s = ControllerStreak.Build(Array.Empty<DateTimeOffset>(), Adesso);
        Assert.Equal(0, s.CurrentWeeks);
        Assert.Equal(0, s.BestWeeks);
        Assert.Null(s.LastSessionUtc);
    }

    [Fact]
    public void Quattro_settimane_di_fila_fanno_quattro()
    {
        var s = ControllerStreak.Build(SettimaneFa(0, 1, 2, 3), Adesso);
        Assert.Equal(4, s.CurrentWeeks);
        Assert.Equal(4, s.BestWeeks);
    }

    [Fact]
    public void Piu_turni_nella_stessa_settimana_contano_per_una()
    {
        var s = ControllerStreak.Build(
            new[] { Adesso, Adesso.AddHours(-5), Adesso.AddDays(-1), Adesso.AddDays(-7) }, Adesso);

        Assert.Equal(2, s.CurrentWeeks);
    }

    [Fact]
    public void La_striscia_resta_viva_se_questa_settimana_non_e_ancora_cominciata()
    {
        // ⚠️ Senza questa regola, ogni lunedì mattina la striscia di tutti tornerebbe a zero.
        var s = ControllerStreak.Build(SettimaneFa(1, 2, 3), Adesso);
        Assert.Equal(3, s.CurrentWeeks);
    }

    [Fact]
    public void Saltata_una_settimana_la_striscia_e_finita()
    {
        var s = ControllerStreak.Build(SettimaneFa(2, 3, 4), Adesso);
        Assert.Equal(0, s.CurrentWeeks);
        Assert.Equal(3, s.BestWeeks);       // la migliore resta
    }

    [Fact]
    public void La_migliore_puo_essere_piu_vecchia_di_quella_in_corso()
    {
        var s = ControllerStreak.Build(SettimaneFa(0, 1, 10, 11, 12, 13), Adesso);
        Assert.Equal(2, s.CurrentWeeks);
        Assert.Equal(4, s.BestWeeks);
    }

    [Fact]
    public void Il_capodanno_non_spezza_la_striscia()
    {
        // Settimane a cavallo fra il 2025 e il 2026: contate come giorni dal lunedì dell'epoca, non come
        // «settimana 52» seguita da «settimana 1».
        var capodanno = new DateTimeOffset(2026, 1, 8, 12, 0, 0, TimeSpan.Zero);
        var s = ControllerStreak.Build(
            new[] { capodanno, capodanno.AddDays(-7), capodanno.AddDays(-14) }, capodanno);

        Assert.Equal(3, s.CurrentWeeks);
    }
}
