using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Scostamento dall'ordine standard di catalogo: l'editor lo mostra accanto al titolo della sezione, così chi
/// scrive vede di quanti posti l'ha spostata rispetto a dove il catalogo la metterebbe.
/// </summary>
public class SectionOrderingTests
{
    private static EditableSection Sec(int id, string key) => new()
    {
        Id = id, Title = key, SectionKey = key, Depth = 0, Order = id,
        Blocks = new List<EditableBlock>(), Children = new List<EditableSection>(),
    };

    private static IReadOnlyList<EditableSection> Group(params string[] keys) =>
        keys.Select((k, i) => Sec(i + 1, k)).ToList();

    [Fact]
    public void Standard_order_has_no_offsets()
    {
        var g = Group("separations", "configurations", "aor", "frequencies");
        Assert.Empty(SectionOrdering.OffsetsFromStandard(SectionProfile.App, g));
    }

    // Una sezione portata in cima si vede a -2 (due posti sopra) e le due che ha scavalcato a +1 ciascuna:
    // e' cio' che l'editor scrive nelle pill (↑2 su una, ↓1 sulle altre due).
    [Fact]
    public void Moving_one_section_up_shows_both_sides_of_the_swap()
    {
        var g = Group("aor", "separations", "configurations", "frequencies");
        var off = SectionOrdering.OffsetsFromStandard(SectionProfile.App, g);

        Assert.Equal(-2, off[g[0].Id]);   // aor: due posti piu' in alto
        Assert.Equal(1, off[g[1].Id]);    // separations: uno piu' in basso
        Assert.Equal(1, off[g[2].Id]);    // configurations: uno piu' in basso
        Assert.False(off.ContainsKey(g[3].Id));
    }

    // ⚠️ Le sezioni LIBERE non hanno una posizione standard: infilarne una in testa non deve far apparire uno
    // scostamento su tutte le fisse che la seguono — non le ha spostate nessuno.
    [Fact]
    public void Free_sections_do_not_shift_the_fixed_ones()
    {
        var g = Group("custom:aaaa1111", "separations", "configurations", "aor");
        var off = SectionOrdering.OffsetsFromStandard(SectionProfile.App, g);

        Assert.Empty(off);
    }

    // Il confronto e' fra le sezioni fisse PRESENTI: una sezione di catalogo che il documento non ha
    // (i blocchi Aerovia non hanno il VFR) non lascia un buco che sposti le altre.
    [Fact]
    public void Missing_catalog_sections_leave_no_hole()
    {
        var g = Group("separations", "aor", "validity");
        Assert.Empty(SectionOrdering.OffsetsFromStandard(SectionProfile.App, g));
    }

    // Documento senza catalogo (l'aeroporto): si sposta lo stesso, ma non c'e' uno standard da confrontare.
    [Fact]
    public void No_profile_no_offsets()
    {
        var g = Group("aor", "separations");
        Assert.Empty(SectionOrdering.OffsetsFromStandard(null, g));
    }

    // Le sotto-sezioni fisse (le due direzioni dei coordinamenti vLOA) stanno nel ChildRegistry: Find le vede,
    // quindi anche loro hanno uno standard da cui scostarsi.
    [Fact]
    public void Fixed_child_sections_count_too()
    {
        var g = Group(SectionKeys.CoordinationIn, SectionKeys.CoordinationOut);
        var off = SectionOrdering.OffsetsFromStandard(SectionProfile.Vloa, g);

        Assert.Equal(-1, off[g[0].Id]);
        Assert.Equal(1, off[g[1].Id]);
    }

    [Fact]
    public void A_single_fixed_section_can_not_be_out_of_order()
    {
        var g = Group("custom:aaaa1111", "aor", "custom:bbbb2222");
        Assert.Empty(SectionOrdering.OffsetsFromStandard(SectionProfile.App, g));
    }
}
