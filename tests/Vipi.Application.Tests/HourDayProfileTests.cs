using Vipi.Application.Stats;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Le abitudini lette dalla griglia: a che ora, che giorno, e la fascia in cui sta metà del tempo.
///
/// <para>⚠️ Il caso che conta è quello a cavallo della mezzanotte: chi controlla dalle 21 all'una ha
/// un'abitudine sola, e una finestra non circolare gliela spezzerebbe ai due bordi del giorno facendo
/// scrivere alla pagina «di solito fra le 00 e le 23» — vero e inutile.</para>
/// </summary>
public class HourDayProfileTests
{
    /// <summary>Una casella piena: <paramref name="minuti"/> minuti quel giorno a quell'ora.</summary>
    private static CoverageCell C(int giorno, int ora, int minuti) => new(giorno, ora, minuti, 60);

    [Fact]
    public void Somma_i_minuti_per_ora_e_per_giorno()
    {
        var p = HourDayProfileBuilder.Build(new[]
        {
            C(4, 20, 60), C(5, 20, 30), C(4, 21, 45),
        });

        Assert.Equal(90, p.PerHour[20]);        // giovedì + venerdì alle 20
        Assert.Equal(45, p.PerHour[21]);
        Assert.Equal(105, p.PerDay[3]);         // indice 3 = giovedì
        Assert.Equal(30, p.PerDay[4]);
        Assert.Equal(135, p.TotalMinutes);
        Assert.Equal(4, p.BusiestDay);
    }

    [Fact]
    public void La_fascia_tipica_e_la_piu_corta_che_tiene_meta_del_tempo()
    {
        var p = HourDayProfileBuilder.Build(new[]
        {
            C(1, 19, 60), C(1, 20, 60), C(1, 21, 60),   // 180 minuti la sera
            C(1, 8, 20), C(1, 9, 20), C(1, 14, 20),     // 60 sparsi di giorno
        });

        // 240 minuti in tutto: 19 e 20 ne tengono 120, cioè ESATTAMENTE la metà — e tanto basta, perché
        // la domanda è «dove sta metà del tuo tempo», non «dove ne sta più della metà».
        Assert.Equal(19, p.PeakFromHour);
        Assert.Equal(21, p.PeakToHour);              // «dalle 19 alle 21», l'ultima esclusa
        Assert.Equal(2, p.PeakHours);
    }

    /// <summary>⚠️ Il caso della mezzanotte: 21-22-23-00 è UNA fascia, non due ai bordi opposti.</summary>
    [Fact]
    public void La_fascia_puo_scavalcare_la_mezzanotte()
    {
        var p = HourDayProfileBuilder.Build(new[]
        {
            C(6, 22, 60), C(6, 23, 60), C(7, 0, 60), C(7, 1, 60),
            C(3, 12, 10),
        });

        Assert.Equal(22, p.PeakFromHour);
        Assert.Equal(1, p.PeakToHour);               // scavalca la mezzanotte: 22, 23, 00
        Assert.Equal(3, p.PeakHours);
    }

    [Fact]
    public void Senza_niente_in_archivio_non_si_inventa_un_abitudine()
    {
        var p = HourDayProfileBuilder.Build(new[] { new CoverageCell(1, 10, 0, 60) });

        Assert.False(p.HasPeak);
        Assert.Equal(0, p.BusiestDay);
        Assert.Equal(0, p.TotalMinutes);
    }

    /// <summary>
    /// ⚠️ Si contano i minuti, non le caselle accese: cinque connessioni lampo sparse in cinque ore diverse
    /// non devono pesare come cinque ore piene.
    /// </summary>
    [Fact]
    public void Contano_i_minuti_non_le_caselle()
    {
        var p = HourDayProfileBuilder.Build(new[]
        {
            C(1, 0, 1), C(1, 1, 1), C(1, 2, 1), C(1, 3, 1), C(1, 4, 1),
            C(1, 20, 120),
        });

        Assert.Equal(20, p.PeakFromHour);
        Assert.Equal(21, p.PeakToHour);
    }
}
