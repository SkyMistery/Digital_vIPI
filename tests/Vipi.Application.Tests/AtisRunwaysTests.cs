using Vipi.Application.Stats;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Le piste in uso lette dall'ATIS. Le frasi di questi test sono <b>vere</b>: prese dal whazzup del
/// 24 agosto 2026, dove 48 ATC su 71 nominano una pista nel testo.
/// </summary>
public class AtisRunwaysTests
{
    [Fact]
    public void Fiumicino_dichiara_arrivi_e_partenze_diverse()
    {
        var r = AtisRunways.Leggi(new[]
        {
            "ts-1.eu-west-2.ivao.aero/LIRF_TWR",
            "This is Fiumicino ATIS arrival and departure information CHARLIE at 1310. Arrival runway 16L 16R " +
            "departure runway 25 Transition level 70 LIRF 241250Z 24012KT CAVOK",
        });

        Assert.Equal("16L/16R", r.Arrival);
        Assert.Equal("25", r.Departure);
        Assert.Equal("16L/16R → 25", r.ToString());
    }

    [Fact]
    public void Venezia_dichiara_una_pista_sola_per_tutti_e_due_i_versi()
    {
        var r = AtisRunways.Leggi(new[]
        {
            "This is Venice ATIS arrival and departure information CHARLIE at 1302. Runway in use 04R " +
            "Transition level 070 LIPZ 241250Z 04008KT",
        });

        Assert.Equal("04R", r.Arrival);
        Assert.Equal("04R", r.Departure);
        Assert.Equal("04R", r.ToString());     // una sola: non si scrive «04R → 04R»
    }

    [Fact]
    public void Un_ATIS_che_non_nomina_piste_non_ne_inventa()
    {
        // Anche questo è reale: l'ATIS di LIRF_TW1_APP porta solo livello di transizione e METAR.
        var r = AtisRunways.Leggi(new[]
        {
            "This is Fiumicino ATIS arrival and departure information CHARLIE at 1248. Transition level 70 " +
            "LIRF 241250Z 24012KT CAVOK 30/19 Q1014",
        });

        Assert.True(r.Vuoto);
        Assert.Equal("", r.ToString());
    }

    [Fact]
    public void Il_solo_indirizzo_del_server_non_e_una_pista()
    {
        var r = AtisRunways.Leggi(new[] { "ts-1.eu-west-2.ivao.aero/UKLU_TWR" });
        Assert.True(r.Vuoto);
    }

    [Fact]
    public void Niente_ATIS_niente_piste()
    {
        Assert.True(AtisRunways.Leggi(null).Vuoto);
        Assert.True(AtisRunways.Leggi(new string[0]).Vuoto);
        Assert.True(AtisRunways.Leggi(new[] { "", "   " }).Vuoto);
    }

    [Fact]
    public void La_dichiarazione_generica_non_appiattisce_i_due_versi()
    {
        // ⚠️ Se «in use» si leggesse per primo, questa frase darebbe la stessa pista in arrivo e in partenza.
        var r = AtisRunways.Leggi(new[] { "Arrival runway 34L departure runway 34R runway in use 34" });

        Assert.Equal("34L", r.Arrival);
        Assert.Equal("34R", r.Departure);
    }

    [Fact]
    public void Le_piste_ripetute_si_dicono_una_volta_sola()
    {
        var r = AtisRunways.Leggi(new[] { "Arrival runway 16L 16L 16R departure runway 25" });
        Assert.Equal("16L/16R", r.Arrival);
    }

    [Fact]
    public void Il_confronto_fra_due_letture_dice_se_la_configurazione_e_cambiata()
    {
        // È il modo in cui il poller decide se scrivere una riga nuova.
        var prima = AtisRunways.Leggi(new[] { "Arrival runway 16L departure runway 25" });
        var uguale = AtisRunways.Leggi(new[] { "Arrival runway 16L departure runway 25 information DELTA" });
        var dopo = AtisRunways.Leggi(new[] { "Arrival runway 34R departure runway 34L" });

        Assert.Equal(prima, uguale);          // cambia la lettera, non la configurazione
        Assert.NotEqual(prima, dopo);
    }
}
