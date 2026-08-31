using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La parte «B»: la geometria propone le righe di ripiego. Stesso scenario della carta — Milano divisa in due
/// su due strati (<c>docs/feature/2026-08-31-ricaduta-verticale-e-cicli.md</c> §2).
/// </summary>
public class FallbackSuggestionsTests
{
    private const string Ws2 = "LIMM_WS2_CTR", Es2 = "LIMM_ES2_CTR", Ws5 = "LIMM_WS5_CTR", Es5 = "LIMM_ES5_CTR";

    private static readonly SectorBand[] Milano =
    {
        new(Ws2, BaseFeet: null, TopFeet: 30500),
        new(Es2, BaseFeet: null, TopFeet: 30500),
        new(Ws5, BaseFeet: 30500, TopFeet: null),
        new(Es5, BaseFeet: 30500, TopFeet: null),
    };

    private static IReadOnlySet<string> Antenati(params string[] cs) =>
        new HashSet<string>(cs, StringComparer.OrdinalIgnoreCase);

    // =====================================================================================================

    /// <summary>Il caso della carta: per ES5, B propone WS5 — l'altro settore dello stesso STRATO.</summary>
    [Fact]
    public void Per_ES5_propone_WS5_con_la_sua_fascia()
    {
        var p = FallbackSuggestions.For(Es5, Milano, Antenati(Es2, Ws2));

        var sola = Assert.Single(p);
        Assert.Equal(Ws5, sola.TargetCallsign);
        Assert.Equal(30500, sola.BaseFeet);
        Assert.Null(sola.TopFeet);
    }

    /// <summary>
    /// ⚠️ Il punto della regola: ES2 è il PADRE di ES5 e sta sotto di lui in pianta, ma non condivide un
    /// piede di quota — non è un sostituto. Una proposta basata sulla vicinanza in pianta lo metterebbe primo.
    /// </summary>
    [Fact]
    public void Il_settore_sottostante_non_e_un_sostituto()
    {
        var p = FallbackSuggestions.For(Es5, Milano, Antenati());

        Assert.DoesNotContain(p, x => x.TargetCallsign == Es2);
        Assert.DoesNotContain(p, x => x.TargetCallsign == Ws2);
    }

    /// <summary>Gli antenati non si propongono: sono già la coda della catena, senza che nessuno li scriva.</summary>
    [Fact]
    public void Gli_antenati_restano_fuori()
    {
        var bande = Milano.Append(new SectorBand("LIMM_CTR", null, null)).ToList();

        var p = FallbackSuggestions.For(Es5, bande, Antenati(Es2, Ws2, "LIMM_CTR"));

        Assert.DoesNotContain(p, x => x.TargetCallsign == "LIMM_CTR");
    }

    /// <summary>Il settore non propone sé stesso.</summary>
    [Fact]
    public void Non_propone_se_stesso() =>
        Assert.DoesNotContain(FallbackSuggestions.For(Es5, Milano, Antenati()), x => x.TargetCallsign == Es5);

    /// <summary>Chi si sovrappone di più viene per primo: è l'ordine in cui l'admin li legge.</summary>
    [Fact]
    public void Le_proposte_escono_dalla_piu_sovrapposta()
    {
        var bande = new SectorBand[]
        {
            new(Es5, 30500, null),
            new("MOLTO", 30500, null),      // tutta la banda
            new("POCO", 30500, 32000),      // 1500 piedi soli
        };

        var p = FallbackSuggestions.For(Es5, bande, Antenati());

        Assert.Equal(new[] { "MOLTO", "POCO" }, p.Select(x => x.TargetCallsign));
    }

    /// <summary>La fascia proposta è l'INTERSEZIONE: solo il cielo che il sostituto può davvero prendere.</summary>
    [Fact]
    public void La_fascia_proposta_e_l_intersezione()
    {
        var bande = new SectorBand[] { new("A", 20000, 40000), new("B", 30000, 50000) };

        var sola = Assert.Single(FallbackSuggestions.For("A", bande, Antenati()));

        Assert.Equal(30000, sola.BaseFeet);
        Assert.Equal(40000, sola.TopFeet);
        Assert.Equal(10000, sola.OverlapFeet);
    }

    /// <summary>Due bande che si toccano soltanto (tetto dell'una = piede dell'altra) non si sovrappongono.</summary>
    [Fact]
    public void Due_bande_che_si_toccano_non_bastano()
    {
        var bande = new SectorBand[] { new("A", null, 30500), new("B", 30500, null) };

        Assert.Empty(FallbackSuggestions.For("A", bande, Antenati()));
    }

    [Fact]
    public void Un_settore_che_non_ha_banda_non_produce_proposte() =>
        Assert.Empty(FallbackSuggestions.For("SCONOSCIUTO", Milano, Antenati()));

    // =====================================================================================================
    //  BandOf — l'unione dei pezzi
    // =====================================================================================================

    [Fact]
    public void BandOf_prende_il_piede_piu_basso_e_il_tetto_piu_alto()
    {
        var b = FallbackSuggestions.BandOf("X", new (int?, int?)[] { (5000, 20000), (18000, 45000) });

        Assert.Equal(5000, b.BaseFeet);
        Assert.Equal(45000, b.TopFeet);
    }

    /// <summary>⚠️ Un solo pezzo aperto apre tutta la banda da quel lato: nell'unione il null vince.</summary>
    [Fact]
    public void BandOf_un_pezzo_aperto_apre_tutta_la_banda()
    {
        var giu = FallbackSuggestions.BandOf("X", new (int?, int?)[] { (5000, 20000), (null, 10000) });
        var su = FallbackSuggestions.BandOf("X", new (int?, int?)[] { (5000, 20000), (18000, null) });

        Assert.Null(giu.BaseFeet);
        Assert.Equal(20000, giu.TopFeet);
        Assert.Equal(5000, su.BaseFeet);
        Assert.Null(su.TopFeet);
    }

    [Fact]
    public void BandOf_senza_pezzi_e_tutta_aperta()
    {
        var b = FallbackSuggestions.BandOf("X", Array.Empty<(int?, int?)>());

        Assert.Null(b.BaseFeet);
        Assert.Null(b.TopFeet);
    }
}
