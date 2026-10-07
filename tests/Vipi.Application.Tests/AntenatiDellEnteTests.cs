using Vipi.Application.Aor;
using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// I genitori di copertura di un ente con più posizioni, in coda alle frequenze della vIPI APP (revisione degli
/// enti ATC, S52): dal più vicino al più lontano, anche quando le posizioni stanno in rami diversi.
/// </summary>
public class AntenatiDellEnteTests
{
    private static Topology Albero(params (string Figlio, string Padre)[] archi) => new()
    {
        Sectors = archi.SelectMany(a => new[] { a.Figlio, a.Padre }).Distinct().ToList(),
        Parent = archi.ToDictionary(a => a.Figlio, a => a.Padre, StringComparer.OrdinalIgnoreCase),
    };

    [Fact]
    public void Due_posizioni_in_rami_diversi_danno_prima_i_ctr_vicini_poi_la_radice()
    {
        // 🔴 Posizione per posizione usciva A, ROOT, B: la radice prima di un CTR vicino.
        var topo = Albero(("P1", "CTR_A"), ("CTR_A", "ROOT"), ("P2", "CTR_B"), ("CTR_B", "ROOT"));

        var antenati = AppDocumentService.AntenatiDi(topo, new[] { "P1", "P2" },
            new HashSet<string>(new[] { "P1", "P2" }, StringComparer.OrdinalIgnoreCase));

        Assert.Equal(new[] { "CTR_A", "CTR_B", "ROOT" }, antenati);
    }

    [Fact]
    public void Chi_e_nel_dominio_non_entra_e_non_si_ripete()
    {
        // La torre dell'ente sta sotto il suo stesso APP: l'APP è nel dominio, e il CTR comune esce una volta.
        var topo = Albero(("TWR", "APP"), ("APP", "CTR"));

        var antenati = AppDocumentService.AntenatiDi(topo, new[] { "APP", "TWR" },
            new HashSet<string>(new[] { "APP", "TWR" }, StringComparer.OrdinalIgnoreCase));

        Assert.Equal(new[] { "CTR" }, antenati);
    }
}
