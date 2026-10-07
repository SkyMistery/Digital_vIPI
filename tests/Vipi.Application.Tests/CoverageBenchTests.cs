using Vipi.Application.Aor;
using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il banco di prova della struttura: «con questi aperti, chi tiene cosa». Il banco non ha un motore suo — passa da
/// <see cref="FallbackChain.Holders"/> — quindi qui si prova quel che è suo: quali settori si considerano aperti,
/// come si raggruppa la risposta, e che cosa si dice quando non raccoglie nessuno. Carta
/// <c>docs/feature/2026-10-04-copertura-unica.md</c>.
/// </summary>
public class CoverageBenchTests
{
    private const string Ws2 = "LIMM_WS2_CTR", Es2 = "LIMM_ES2_CTR", Ws5 = "LIMM_WS5_CTR", Es5 = "LIMM_ES5_CTR";
    private const string Padova = "LIPP_CE1_CTR";
    private static readonly StringComparer OIC = StringComparer.OrdinalIgnoreCase;
    private static readonly string[] Milano = { Ws2, Es2, Ws5, Es5 };

    /// <summary>Milano com'è in produzione, più un centro vicino che dello scenario non fa parte.</summary>
    private static Topology Topo(string? padreDiWs2 = null)
    {
        var padri = new Dictionary<string, string>(OIC) { [Es2] = Ws2, [Ws5] = Ws2, [Es5] = Es2 };
        if (padreDiWs2 is not null) padri[Ws2] = padreDiWs2;
        return new Topology
        {
            Sectors = new[] { Ws2, Es2, Ws5, Es5, Padova },
            Parent = padri,
            Fallbacks = new Dictionary<string, IReadOnlyList<FallbackRow>>(OIC)
            {
                [Es5] = new[] { new FallbackRow(Ws5, BaseFeet: 32500, TopFeet: null) },
            },
            Bands = new Dictionary<string, (int? BaseFeet, int? TopFeet)>(OIC)
            {
                [Ws2] = (0, 32500), [Es2] = (0, 32500), [Ws5] = (32500, null), [Es5] = (32500, null),
            },
        };
    }

    private static string[] Tenuti(IReadOnlyList<BenchGroup> gruppi, string? chi) =>
        gruppi.Single(g => OIC.Equals(g.Holder, chi)).Items.Select(i => i.Sector).ToArray();

    [Fact]
    public void Le_due_configurazioni_di_Milano_dallo_stesso_albero()
    {
        var conWs5 = CoverageBench.Copertura(Topo(), Milano, new[] { Ws2, Es2, Ws5 });
        Assert.Equal(new[] { Es5 }, Tenuti(conWs5, Ws5));
        Assert.Empty(Tenuti(conWs5, Es2));

        var senza = CoverageBench.Copertura(Topo(), Milano, new[] { Ws2, Es2 });
        Assert.Equal(new[] { Es5 }, Tenuti(senza, Es2));
        Assert.Equal(new[] { Ws5 }, Tenuti(senza, Ws2));
    }

    [Fact]
    public void Un_aperto_che_non_assorbe_niente_resta_in_tabella()
    {
        // È un'informazione: «ES2 è aperto e tiene solo il suo». Togliendolo la tabella direbbe che non c'è.
        var gruppi = CoverageBench.Copertura(Topo(), Milano, Milano);

        Assert.Equal(Milano, gruppi.Select(g => g.Holder));
        Assert.All(gruppi, g => Assert.Empty(g.Items));
    }

    [Fact]
    public void I_gruppi_seguono_l_ordine_in_cui_si_sono_aperti_i_settori()
    {
        var gruppi = CoverageBench.Copertura(Topo(), Milano, new[] { Es2, Ws2 });

        Assert.Equal(new[] { Es2, Ws2 }, gruppi.Select(g => g.Holder));
    }

    [Fact]
    public void Tutti_chiusi_e_nessuno_sopra_il_cielo_non_lo_tiene_nessuno()
    {
        var gruppi = CoverageBench.Copertura(Topo(), Milano, Array.Empty<string>());

        var nessuno = Assert.Single(gruppi);
        Assert.Null(nessuno.Holder);
        Assert.Equal(4, nessuno.Items.Count);
    }

    [Fact]
    public void Chi_sta_fuori_dallo_scenario_si_considera_aperto_e_si_dice()
    {
        // WS2 pende da un settore di Padova: chiuso tutto Milano, raccoglie lui — e la tabella dice che è fuori.
        var gruppi = CoverageBench.Copertura(Topo(padreDiWs2: Padova), Milano, Array.Empty<string>());

        var fuori = Assert.Single(gruppi);
        Assert.Equal(Padova, fuori.Holder);
        Assert.True(fuori.Outside);
    }

    [Fact]
    public void Un_callsign_che_non_e_dello_scenario_non_si_puo_aprire_da_qui()
    {
        // Il banco apre e chiude i SOLI settori dell'elenco: un nome in più fra gli aperti non diventa una riga.
        var gruppi = CoverageBench.Copertura(Topo(), Milano, new[] { Ws2, "LIRR_XX_CTR" });

        Assert.DoesNotContain(gruppi, g => g.Holder == "LIRR_XX_CTR");
    }

    [Fact]
    public void L_insieme_online_di_uno_scenario_e_gli_aperti_piu_tutto_il_resto()
    {
        var online = CoverageBench.OnlineDi(Topo().Sectors, Milano, new[] { Ws2 });

        Assert.Equal(new[] { Padova, Ws2 }.OrderBy(x => x), online.OrderBy(x => x));
    }

    [Fact]
    public void Un_settore_diviso_per_quota_porta_la_fascia_accanto_al_nome()
    {
        var t = new Topology
        {
            Sectors = new[] { "XX_CTR", "ALTO_CTR", "PADRE_CTR" },
            Parent = new Dictionary<string, string>(OIC) { ["XX_CTR"] = "PADRE_CTR" },
            Fallbacks = new Dictionary<string, IReadOnlyList<FallbackRow>>(OIC)
            {
                ["XX_CTR"] = new[] { new FallbackRow("ALTO_CTR", BaseFeet: 32500, TopFeet: null) },
            },
            Bands = new Dictionary<string, (int? BaseFeet, int? TopFeet)>(OIC) { ["XX_CTR"] = (0, null) },
        };

        var gruppi = CoverageBench.Copertura(t, new[] { "XX_CTR", "ALTO_CTR", "PADRE_CTR" }, new[] { "PADRE_CTR", "ALTO_CTR" });

        Assert.Equal("SFC–FL325", gruppi.Single(g => g.Holder == "PADRE_CTR").Items.Single().Band);
        Assert.Equal("FL325–UNL", gruppi.Single(g => g.Holder == "ALTO_CTR").Items.Single().Band);
    }
}
