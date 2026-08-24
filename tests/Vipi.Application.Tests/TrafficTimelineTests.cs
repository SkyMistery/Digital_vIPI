using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Stats;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>La striscia del turno: corsie che non si sovrappongono e punta di traffico simultaneo.</summary>
public class TrafficTimelineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 24, 18, 0, 0, TimeSpan.Zero);

    private static (DateTimeOffset, DateTimeOffset) Barra(int daMin, int aMin) =>
        (T0.AddMinutes(daMin), T0.AddMinutes(aMin));

    [Fact]
    public void Senza_voli_non_c_e_striscia()
    {
        var r = TrafficTimeline.Build(Array.Empty<(DateTimeOffset, DateTimeOffset)>());
        Assert.Empty(r.Bars);
        Assert.Equal(0, r.PeakConcurrent);
        Assert.Null(r.PeakAtUtc);
    }

    [Fact]
    public void Voli_che_non_si_toccano_stanno_sulla_stessa_corsia()
    {
        var r = TrafficTimeline.Build(new List<(DateTimeOffset, DateTimeOffset)>
        {
            Barra(0, 10), Barra(10, 20), Barra(20, 30),
        });

        Assert.Equal(1, r.Lanes);
        Assert.All(r.Bars, b => Assert.Equal(0, b.Lane));
        Assert.Equal(1, r.PeakConcurrent);
    }

    [Fact]
    public void Voli_sovrapposti_prendono_corsie_diverse()
    {
        var r = TrafficTimeline.Build(new List<(DateTimeOffset, DateTimeOffset)>
        {
            Barra(0, 30), Barra(5, 20), Barra(10, 15),
        });

        Assert.Equal(3, r.Lanes);
        Assert.Equal(3, r.PeakConcurrent);
        Assert.Equal(T0.AddMinutes(10), r.PeakAtUtc);
    }

    [Fact]
    public void La_barra_ritrova_la_riga_da_cui_viene()
    {
        var r = TrafficTimeline.Build(new List<(DateTimeOffset, DateTimeOffset)>
        {
            Barra(20, 30),   // indice 0, ma comincia per ultimo
            Barra(0, 10),    // indice 1
        });

        Assert.Equal(new[] { 1, 0 }, r.Bars.OrderBy(b => b.From).Select(b => b.Index));
    }

    [Fact]
    public void Un_volo_che_finisce_quando_un_altro_comincia_non_fa_due()
    {
        var r = TrafficTimeline.Build(new List<(DateTimeOffset, DateTimeOffset)> { Barra(0, 10), Barra(10, 20) });
        Assert.Equal(1, r.PeakConcurrent);
    }

    [Fact]
    public void Un_solo_avvistamento_resta_un_istante_senza_rompere_niente()
    {
        var r = TrafficTimeline.Build(new List<(DateTimeOffset, DateTimeOffset)> { Barra(5, 5) });
        var b = Assert.Single(r.Bars);
        Assert.Equal(b.From, b.To);
        Assert.Equal(1, r.PeakConcurrent);
    }
}
