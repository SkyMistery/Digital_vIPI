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

    [Fact]
    public void EnsureNoCycle_rejects_when_child_is_ancestor_of_proposed_parent()
    {
        // Catena A → B → C (A padre di B, B padre di C). Rendere C padre di A creerebbe un ciclo.
        var parents = new Dictionary<string, string?> { ["B"] = "A", ["C"] = "B", ["A"] = null };
        Assert.Throws<ValidationException>(() => HierarchyRules.EnsureNoCycle(childCallsign: "A", proposedParent: "C", parents));
    }

    [Fact]
    public void EnsureNoCycle_allows_valid_chain()
    {
        var parents = new Dictionary<string, string?> { ["B"] = "A", ["A"] = null };
        HierarchyRules.EnsureNoCycle(childCallsign: "X", proposedParent: "B", parents);   // nessuna eccezione
    }

    [Fact]
    public void EnsureNoCycle_rejects_self_parenting()
    {
        var parents = new Dictionary<string, string?> { ["A"] = null };
        Assert.Throws<ValidationException>(() => HierarchyRules.EnsureNoCycle("A", "A", parents));
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
