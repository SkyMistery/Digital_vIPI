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

    // ---- Trascinamento: dal gesto al riferimento «prima di questa» -------------------------------------
    // La regola a schermo e' una sola: la sezione lasciata PRENDE IL POSTO di quella su cui la si lascia.
    // Verso il basso e verso l'alto quella stessa frase da' due riferimenti diversi, ed e' qui che si vede.

    private static readonly int[] Sibs = { 10, 20, 30, 40 };

    [Fact]
    public void Drop_going_down_lands_after_the_target()
    {
        // 10 lasciata su 30 -> 20, 30, 10, 40: il riferimento e' il fratello DOPO il bersaglio.
        Assert.True(SectionOrdering.TryDropOnto(Sibs, movedId: 10, targetId: 30, out var before));
        Assert.Equal(40, before);
    }

    [Fact]
    public void Drop_going_down_onto_the_last_appends()
    {
        Assert.True(SectionOrdering.TryDropOnto(Sibs, movedId: 10, targetId: 40, out var before));
        Assert.Null(before);   // in coda: dopo l'ultimo non c'e' nessuno davanti a cui mettersi
    }

    [Fact]
    public void Drop_going_up_lands_on_the_target_place()
    {
        // 40 lasciata su 20 -> 10, 40, 20, 30: il riferimento e' il bersaglio stesso.
        Assert.True(SectionOrdering.TryDropOnto(Sibs, movedId: 40, targetId: 20, out var before));
        Assert.Equal(20, before);
    }

    [Fact]
    public void Drop_of_adjacent_sections_is_the_arrow_move()
    {
        // Su fratelli adiacenti il trascinamento deve dare lo stesso esito delle frecce: 20 su 10 = «20 su».
        Assert.True(SectionOrdering.TryDropOnto(Sibs, movedId: 20, targetId: 10, out var up));
        Assert.Equal(10, up);
        Assert.True(SectionOrdering.TryDropOnto(Sibs, movedId: 20, targetId: 30, out var down));
        Assert.Equal(40, down);
    }

    // ⚠️ Un bersaglio che non e' del gruppo (o la sezione su se stessa) NON e' una mossa: il trascinamento non
    // riparenta, e chi chiama non deve trovarsi un riferimento buono per una sezione di un altro blocco.
    [Fact]
    public void Drop_outside_the_group_or_onto_itself_is_no_move()
    {
        Assert.False(SectionOrdering.TryDropOnto(Sibs, movedId: 10, targetId: 10, out _));
        Assert.False(SectionOrdering.TryDropOnto(Sibs, movedId: 10, targetId: 99, out _));
        Assert.False(SectionOrdering.TryDropOnto(Sibs, movedId: 99, targetId: 10, out _));
    }
}
