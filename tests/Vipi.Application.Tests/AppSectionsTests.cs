using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>Riconciliazione ordine sezioni APP col registry: inserimento mancanti, scarto stale, custom preservate.</summary>
public class AppSectionsTests
{
    private static readonly string[] AllKeys = { "separations", "aor", "frequencies", "vfr", "minima", "coordination" };

    [Fact]
    public void Empty_Order_Yields_All_Fixed_In_Default_Order()
    {
        var r = AppSections.Reconcile(Array.Empty<string>());
        Assert.Equal(AllKeys, r);
    }

    [Fact]
    public void Missing_Fixed_Sections_Are_Inserted_At_Default_Position()
    {
        // Profilo vecchio senza "minima" né "coordination": vanno inserite dopo "vfr" (DefaultOrder 5,6).
        var saved = new[] { "separations", "aor", "frequencies", "vfr" };
        var r = AppSections.Reconcile(saved);
        Assert.Equal(AllKeys, r);
    }

    [Fact]
    public void Missing_Middle_Section_Inserted_Between_Neighbours()
    {
        // "aor" mancante (DefaultOrder 2) → inserita tra "separations" (1) e "frequencies" (3).
        var saved = new[] { "separations", "frequencies", "vfr", "minima", "coordination" };
        var r = AppSections.Reconcile(saved);
        Assert.Equal(AllKeys, r);
    }

    [Fact]
    public void Preserves_Reordered_Saved_Order()
    {
        var saved = new[] { "coordination", "frequencies", "aor", "vfr", "minima", "separations" };
        var r = AppSections.Reconcile(saved);
        Assert.Equal(saved, r);   // tutte presenti → ordine salvato intatto
    }

    [Fact]
    public void Drops_Stale_Unknown_Keys()
    {
        var saved = new[] { "separations", "ghost", "aor", "frequencies", "vfr", "minima", "coordination" };
        var r = AppSections.Reconcile(saved);
        Assert.DoesNotContain("ghost", r);
        Assert.Equal(AllKeys, r);
    }

    [Fact]
    public void Custom_Keys_Preserved_In_Place_And_Appended_When_Missing()
    {
        var custom = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "custom-a", "custom-b" };
        var saved = new[] { "separations", "custom-a", "aor", "frequencies", "vfr", "minima", "coordination" };
        var r = AppSections.Reconcile(saved, custom);

        Assert.Equal("custom-a", r[1]);              // custom-a preservata al suo posto
        Assert.Equal("custom-b", r[^1]);             // custom-b mancante → appesa in coda
        Assert.All(AllKeys, k => Assert.Contains(k, r));
    }
}
