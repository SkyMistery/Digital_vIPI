using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>Caratterizzazione del catalogo sezioni unificato (doc refactor 08a): natura, membership per profilo, Reconcile, ricorsione.</summary>
public class SectionCatalogTests
{
    private static readonly string[] Universal =
        { "aor", "frequencies", "coordination", "regulated", "operationaltechnique", "validity" };

    [Theory]
    [InlineData("aor", SectionKind.Derived)]
    [InlineData("frequencies", SectionKind.Derived)]
    [InlineData("coordination", SectionKind.Derived)]
    [InlineData("minima", SectionKind.Derived)]
    [InlineData("separations", SectionKind.Editorial)]
    [InlineData("regulated", SectionKind.Editorial)]
    [InlineData("qualcosa-custom", SectionKind.Editorial)]   // sconosciuta = custom editoriale
    public void KindOf_is_single_source(string key, SectionKind expected) =>
        Assert.Equal(expected, SectionCatalog.KindOf(key));

    [Fact]
    public void Universals_present_in_every_profile()
    {
        foreach (SectionProfile p in Enum.GetValues<SectionProfile>())
        {
            var keys = SectionCatalog.For(p).Select(d => d.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var u in Universal)
                Assert.True(keys.Contains(u), $"{p} deve contenere «{u}»");
        }
    }

    [Fact]
    public void Vloa_has_only_the_six_universals()
    {
        var keys = SectionCatalog.For(SectionProfile.Vloa).Select(d => d.Key).ToArray();
        Assert.Equal(Universal.OrderBy(x => x), keys.OrderBy(x => x));
    }

    [Fact]
    public void Profile_specific_membership()
    {
        string[] Keys(SectionProfile p) => SectionCatalog.For(p).Select(d => d.Key).ToArray();

        Assert.Contains("configurations", Keys(SectionProfile.App));   // config aggiunta ad APP
        Assert.Contains("minima", Keys(SectionProfile.AccAppBlock));    // minima aggiunta ad AppBlock
        Assert.Contains("vfr", Keys(SectionProfile.App));
        Assert.DoesNotContain("vfr", Keys(SectionProfile.AccAerovia));  // Aerovia senza VFR
        Assert.DoesNotContain("separations", Keys(SectionProfile.Vloa)); // vLOA senza separazioni
    }

    [Fact]
    public void Reconcile_empty_yields_default_order()
    {
        var order = SectionCatalog.Reconcile(SectionProfile.Vloa, Array.Empty<string>());
        Assert.Equal(new[] { "aor", "frequencies", "coordination", "regulated", "operationaltechnique", "validity" }, order);
    }

    [Fact]
    public void Reconcile_drops_stale_keeps_custom_inserts_missing_fixed()
    {
        // saved contiene una fissa valida (aor), una custom esistente (note1), una chiave stale (obsolete).
        var order = SectionCatalog.Reconcile(
            SectionProfile.Vloa,
            savedOrder: new[] { "aor", "note1", "obsolete" },
            customKeys: new HashSet<string> { "note1" });

        Assert.Equal("aor", order[0]);        // ordine salvato preservato
        Assert.Contains("note1", order);       // custom preservata
        Assert.DoesNotContain("obsolete", order); // stale scartata
        Assert.Contains("validity", order);    // fissa mancante inserita
    }

    [Fact]
    public void Reconcile_App_inserts_missing_fixed_at_default_position()
    {
        // Profilo APP salvato "vecchio" (senza le sezioni nuove del catalogo): Reconcile le inserisce al loro ordine.
        // Ordine salvato coerente con quello di default (minima prima di vfr): le fisse nuove del catalogo
        // (configurations/regulated/operationaltechnique/validity) vengono inserite alle loro posizioni.
        var saved = new[] { "separations", "aor", "frequencies", "minima", "vfr", "coordination" };
        var order = SectionCatalog.Reconcile(SectionProfile.App, saved);

        var expected = SectionCatalog.For(SectionProfile.App).OrderBy(d => d.Order).Select(d => d.Key).ToArray();
        Assert.Equal(expected, order);   // tutte le fisse presenti, in ordine di default
        Assert.Contains("regulated", order);
        Assert.Contains("validity", order);
    }

    [Fact]
    public void Reconcile_App_preserves_custom_and_drops_stale()
    {
        var custom = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "note1" };
        var saved = new[] { "separations", "note1", "ghost", "aor" };
        var order = SectionCatalog.Reconcile(SectionProfile.App, saved, custom);

        Assert.Equal("note1", order[1]);          // custom preservata al suo posto
        Assert.DoesNotContain("ghost", order);     // stale scartata
        Assert.Contains("validity", order);        // fissa mancante inserita
    }

    [Fact]
    public void DocSection_is_recursive_with_empty_defaults()
    {
        var leaf = new DocSection("Foglia", SectionKind.Editorial);
        Assert.Empty(leaf.Blocks);
        Assert.Empty(leaf.SubSections);

        var parent = new DocSection("Padre", SectionKind.Editorial,
            Blocks: new[] { new DocBlock(Vipi.Domain.BlockFormat.Prose, Body: "testo") },
            SubSections: new[] { leaf });

        Assert.Single(parent.Blocks);
        Assert.Equal("Foglia", Assert.Single(parent.SubSections).Title);
    }
}
