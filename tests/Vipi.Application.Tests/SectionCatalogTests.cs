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
    [InlineData("minima", SectionKind.Editorial)]   // doc 13 §3b: le MVA si scrivono a mano
    [InlineData("separations", SectionKind.Editorial)]
    [InlineData("regulated", SectionKind.Editorial)]
    [InlineData("qualcosa-custom", SectionKind.Editorial)]   // sconosciuta = custom editoriale
    public void KindOf_is_single_source(string key, SectionKind expected) =>
        Assert.Equal(expected, SectionCatalog.KindOf(key));

    // Il toggle Live/Frozen (doc 10 §3a) vale solo per le sezioni derivate: la regola stava ripetuta nei tre
    // editor, ora è qui. Ogni chiave del catalogo deve rispondere in modo coerente con la propria natura.
    [Theory]
    [InlineData("aor", true)]
    [InlineData("frequencies", true)]
    [InlineData("coordination", true)]
    [InlineData("minima", false)]   // doc 13 §3b: editoriale, niente da congelare
    [InlineData("sids", true)]
    [InlineData("separations", false)]
    [InlineData("vfr", false)]
    [InlineData("validity", false)]
    [InlineData("una-sezione-custom", false)]   // chiave ignota = editoriale = niente toggle
    public void IsRenderModeToggleable_only_for_derived(string key, bool expected) =>
        Assert.Equal(expected, SectionCatalog.IsRenderModeToggleable(key));

    [Fact]
    public void IsRenderModeToggleable_agrees_with_KindOf_on_every_catalog_key()
    {
        // Invariante di coerenza: le due porte non possono divergere.
        foreach (SectionProfile p in Enum.GetValues<SectionProfile>())
            foreach (var d in SectionCatalog.For(p))
                Assert.Equal(SectionCatalog.KindOf(d.Key) == SectionKind.Derived,
                    SectionCatalog.IsRenderModeToggleable(d.Key));
    }

    // ---- doc 13 §3a: chi rende il corpo lo dice il catalogo, per profilo ----

    // Rete di regressione contro il ritorno degli HashSet di pagina: se una pagina ricomincia a decidere da sé,
    // questa lista e la sua smettono di combaciare e il difetto si vede qui, non in produzione.
    [Fact]
    public void Host_rendered_sections_are_declared_per_profile()
    {
        string[] Host(SectionProfile p) => SectionCatalog.For(p)
            .Where(d => d.BodySource == SectionBodySource.Host).Select(d => d.Key).OrderBy(k => k).ToArray();

        Assert.Equal(
            new[] { "aor", "configurations", "coordination", "frequencies", "regulated", "separations", "vfr" },
            Host(SectionProfile.App));
        Assert.Equal(
            new[] { "aor", "configurations", "coordination", "frequencies", "regulated", "separations" },
            Host(SectionProfile.AccAerovia));   // l'Aerovia non ha il VFR
        Assert.Equal(
            new[] { "aor", "configurations", "coordination", "frequencies", "regulated", "separations", "vfr" },
            Host(SectionProfile.AccAppBlock));
        Assert.Equal(
            new[] { "aor", "coordination", "frequencies" },
            Host(SectionProfile.Vloa));   // sulla vLOA «regulated» è testo bilaterale, non un picker
    }

    [Fact]
    public void The_same_key_can_be_host_rendered_in_one_profile_and_not_in_another()
    {
        // È il motivo per cui BodySource è per profilo e non globale come KindOf.
        Assert.True(SectionCatalog.IsHostRendered(SectionProfile.App, "regulated"));
        Assert.False(SectionCatalog.IsHostRendered(SectionProfile.Vloa, "regulated"));
    }

    [Fact]
    public void A_derived_section_is_always_host_rendered()
    {
        // Invariante: non esiste una sezione calcolata live il cui corpo venga dai blocchi salvati.
        foreach (SectionProfile p in Enum.GetValues<SectionProfile>())
            foreach (var d in SectionCatalog.For(p).Where(d => d.Kind == SectionKind.Derived))
                Assert.True(SectionCatalog.IsHostRendered(p, d.Key), $"{p}/{d.Key}");
    }

    [Fact]
    public void Custom_sections_are_never_host_rendered_nor_fixed()
    {
        foreach (SectionProfile p in Enum.GetValues<SectionProfile>())
        {
            Assert.False(SectionCatalog.IsHostRendered(p, "custom:9f3a1c07"));
            Assert.False(SectionCatalog.IsFixed(p, "custom:9f3a1c07"));
        }
    }

    [Fact]
    public void ProfileOfAccBlock_maps_the_two_block_kinds()
    {
        Assert.Equal(SectionProfile.AccAerovia, SectionCatalog.ProfileOfAccBlock(AccBlockKind.Aerovia));
        Assert.Equal(SectionProfile.AccAppBlock, SectionCatalog.ProfileOfAccBlock(AccBlockKind.AppGroup));
    }

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

    [Fact]
    public void Regulated_Opens_Collapsed_In_The_Document()
    {
        // doc 11 §3i: «Aree regolamentate» su una ACC sono decine di aree con mappa — la sezione nasce chiusa.
        Assert.True(SectionCatalog.IsInitiallyCollapsed("regulated"));
        Assert.False(SectionCatalog.IsInitiallyCollapsed("aor"));
        Assert.False(SectionCatalog.IsInitiallyCollapsed("coordination"));
        Assert.False(SectionCatalog.IsInitiallyCollapsed("custom:aaaa1111"));
    }
}
