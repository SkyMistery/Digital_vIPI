using Vipi.Application.Aor;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>Caratterizzazione delle regole pure della gerarchia (doc refactor 06 §4.2): estero, anti-ciclo, confinanti.</summary>
public class HierarchyRulesTests
{
    private static readonly string[] ItalyPrefixes = { "LI" };

    [Theory]
    [InlineData("LIRR", false)]
    [InlineData("LIBB", false)]
    [InlineData("LFFF", true)]
    [InlineData("LOVV", true)]
    public void IsForeignCode_uses_division_prefixes(string code, bool expectedForeign) =>
        Assert.Equal(expectedForeign, HierarchyRules.IsForeignCode(code, ItalyPrefixes));

    // ⚠️ La mappa da passare è quella dei padri EFFETTIVI, GIÀ con la modifica applicata: la guardia valida una
    // simulazione, non lo stato attuale. Vedi `EfHierarchyEditingService.EffectiveParentMapAsync`.

    [Fact]
    public void EnsureNoCycle_rejects_when_node_is_its_own_ancestor()
    {
        // A → B → C → A: chiunque dei tre lo si interroghi, l'anello c'è.
        var parents = new Dictionary<string, string?> { ["A"] = "C", ["B"] = "A", ["C"] = "B" };
        Assert.Throws<ValidationException>(() => HierarchyRules.EnsureNoCycle("A", parents));
        Assert.Throws<ValidationException>(() => HierarchyRules.EnsureNoCycle("C", parents));
    }

    [Fact]
    public void EnsureNoCycle_allows_valid_chain()
    {
        var parents = new Dictionary<string, string?> { ["B"] = "A", ["A"] = null };
        HierarchyRules.EnsureNoCycle("B", parents);   // nessuna eccezione
    }

    [Fact]
    public void EnsureNoCycle_rejects_self_parenting()
    {
        var parents = new Dictionary<string, string?> { ["A"] = "A" };
        Assert.Throws<ValidationException>(() => HierarchyRules.EnsureNoCycle("A", parents));
    }

    /// <summary>Il messaggio deve NOMINARE l'anello: «creerebbe un ciclo» e basta non dice quale nodo staccare.</summary>
    [Fact]
    public void EnsureNoCycle_message_names_the_loop()
    {
        var parents = new Dictionary<string, string?> { ["LIMF_WW0_APP"] = "LIMF_WN0_APP", ["LIMF_WN0_APP"] = "LIMF_WW0_APP" };
        var ex = Assert.Throws<ValidationException>(() => HierarchyRules.EnsureNoCycle("LIMF_WW0_APP", parents));
        Assert.Contains("LIMF_WW0_APP → LIMF_WN0_APP → LIMF_WW0_APP", ex.Message);
    }

    /// <summary>Chi ci ARRIVA senza starci dentro non è nell'anello, ma l'anello lo trova lo stesso.</summary>
    [Fact]
    public void FindCycleThrough_returns_only_the_loop_not_the_approach()
    {
        var parents = new Dictionary<string, string?> { ["X"] = "A", ["A"] = "B", ["B"] = "A" };
        var anello = HierarchyRules.FindCycleThrough("X", parents);
        Assert.NotNull(anello);
        Assert.Equal(new[] { "A", "B" }, anello!);
    }

    [Fact]
    public void FindCycleThrough_returns_null_on_an_acyclic_chain()
    {
        var parents = new Dictionary<string, string?> { ["C"] = "B", ["B"] = "A", ["A"] = null };
        Assert.Null(HierarchyRules.FindCycleThrough("C", parents));
    }

    /// <summary>Un anello si conta UNA volta, non una per ogni nodo che ci finisce dentro.</summary>
    [Fact]
    public void FindAllCycles_reports_each_loop_once()
    {
        var parents = new Dictionary<string, string?>
        {
            ["A"] = "B", ["B"] = "A",          // primo anello
            ["C"] = "D", ["D"] = "E", ["E"] = "C",   // secondo
            ["X"] = "A", ["Y"] = "X",          // ci arrivano ma non ne fanno parte
            ["Z"] = null,
        };

        var anelli = HierarchyRules.FindAllCycles(parents);

        Assert.Equal(2, anelli.Count);
        Assert.Contains(anelli, a => a.Count == 2 && a.Contains("A") && a.Contains("B"));
        Assert.Contains(anelli, a => a.Count == 3 && a.Contains("C") && a.Contains("D") && a.Contains("E"));
    }

    [Fact]
    public void FindAllCycles_is_empty_on_a_healthy_tree()
    {
        var parents = new Dictionary<string, string?> { ["B"] = "A", ["C"] = "A", ["A"] = null, ["D"] = "MANCANTE" };
        Assert.Empty(HierarchyRules.FindAllCycles(parents));
    }

    [Fact]
    public void ComputeConfiningForeignCallsigns_returns_only_adjacent_foreign()
    {
        const string squareA = "[[10,43],[11,43],[11,44],[10,44]]";          // domestico
        const string squareTouching = "[[11,43],[12,43],[12,44],[11,44]]";   // confina con A
        const string squareFar = "[[20,43],[21,43],[21,44],[20,44]]";        // lontano

        var domestic = new[] { squareA };
        var foreign = new[]
        {
            ("LFFF_CTR", (string?)squareTouching),
            ("LFEE_CTR", (string?)squareFar),
            ("LFGG_CTR", (string?)null),           // senza poligono → ignorato
        };

        var result = HierarchyRules.ComputeConfiningForeignCallsigns(domestic, foreign, thresholdNm: 8.0);

        Assert.Contains("LFFF_CTR", result);
        Assert.DoesNotContain("LFEE_CTR", result);
        Assert.DoesNotContain("LFGG_CTR", result);
    }
}
