using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Resa della vLOA (doc 13 §3c/§3j). Presidia due regole che si erano rotte proprio qui: le due direzioni dei
/// coordinamenti si riconoscono dalla CHIAVE (non dalla posizione), e le sotto-sezioni EXTRA della sezione
/// «Coordination» sono contenuto del documento come ovunque — questo ramo era l'unico a non renderle affatto.
/// </summary>
public class VloaDocumentViewTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class NoOneOnline : IOnlineAtcProvider
    {
        public OnlineAtcSnapshot GetCurrent() => OnlineAtcSnapshot.Empty;
    }

    private sealed class NoEditor : IEditAuthorizationService
    {
        public bool IsAdmin => false;
        public VipiRole Role => IsAdmin ? VipiRole.Admin : VipiRole.User;
        public int? CurrentUserId => null;
        public string? CurrentName => null;
        public Task EnsureCanEditAccAsync(string accCode, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsureCanEditDocumentAsync(int documentId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> CanEditAccAsync(string accCode, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> CanEditDocumentAsync(int documentId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> CanEditAnythingAsync(CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<GrantRow>> ListGrantsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GrantRow>>(Array.Empty<GrantRow>());
        public Task<int> AddGrantAsync(int userId, string? displayName, string accCode, CancellationToken ct = default) => Task.FromResult(0);
        public Task RevokeGrantAsync(int grantId, CancellationToken ct = default) => Task.CompletedTask;
        public void EnsureAdmin() { }
    }

    /// <summary>Derivazione finta: due direzioni riconoscibili dal contenuto (un CoP diverso per verso).</summary>
    private sealed class FakeDerivation : IVloaDerivationService, IVloaViewDerivationService
    {
        public static VloaCoordination Coordination { get; } = new("LIBB", "LDZO",
            Direction("USOSA"), Direction("VALKO"));

        private static AccCoordination Direction(string cop) => new()
        {
            Sectors = new[]
            {
                new AccSectorApps("ES", new[]
                {
                    new AccAccAirports("Zagreb",
                        new[]
                        {
                            new AccAirportFlows("LDZA",
                                new[] { new AppCoordRow(cop, "FL195", "LDZO_CTR", TransferFlowKind.Arrival) },
                                Array.Empty<AppCoordRow>()),
                        },
                        Array.Empty<AccExtraFlows>()),
                }),
            },
        };

        public Task<VloaPairMeta?> GetPairMetaAsync(int docId, CancellationToken ct = default) => Task.FromResult<VloaPairMeta?>(null);
        public Task<VloaAorData> DeriveAorAsync(int docId, CancellationToken ct = default) => Task.FromResult(VloaAorData.Empty);
        public Task<VloaFreqData> DeriveFrequenciesAsync(int docId, CancellationToken ct = default) => Task.FromResult(VloaFreqData.Empty);
        public Task<VloaCoordination> DeriveCoordinationAsync(int docId, CancellationToken ct = default) => Task.FromResult(Coordination);
        public Task ToggleAorSectorAsync(int docId, string callsign, CancellationToken ct = default) => Task.CompletedTask;
        public Task ToggleFrequencyAsync(int docId, string callsign, CancellationToken ct = default) => Task.CompletedTask;

        public Task<VloaViewDerived> ResolveForViewAsync(int docId, bool useFrozen, CancellationToken ct = default) =>
            Task.FromResult(new VloaViewDerived(VloaAorData.Empty, VloaFreqData.Empty, Coordination));
    }

    public VloaDocumentViewTests()
    {
        var fake = new FakeDerivation();
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<IOnlineAtcProvider>(new NoOneOnline());
        Services.AddSingleton<IEditAuthorizationService>(new NoEditor());
        Services.AddSingleton<IVloaDerivationService>(fake);
        Services.AddSingleton<IVloaViewDerivationService>(fake);
    }

    private static SectionView Section(string id, string key, string title,
        IReadOnlyList<SectionView>? children = null, IReadOnlyList<BlockView>? blocks = null,
        int depth = 0, bool beforeParentBody = false) => new()
    {
        Id = id,
        Title = title,
        Depth = depth,
        SectionKey = key,
        BeforeParentBody = beforeParentBody,
        Blocks = blocks ?? Array.Empty<BlockView>(),
        Children = children ?? Array.Empty<SectionView>(),
    };

    private static BlockView Prose(int id, string body) => new()
    {
        Id = id, Format = BlockFormat.Prose, State = RenderState.Expanded, Body = body,
    };

    private static DocumentView DocWith(params SectionView[] sections) => new()
    {
        Title = "LIBB ↔ LDZO", AiracCycle = "2609", Sections = sections,
    };

    private IRenderedComponent<VloaDocumentView> Render(DocumentView view) =>
        RenderComponent<VloaDocumentView>(p => p
            .Add(x => x.View, view)
            .Add(x => x.DocId, 7)
            .Add(x => x.HomeAcc, "LIBB")
            .Add(x => x.ForeignAcc, "LDZO"));

    private static SectionView Coordination(params SectionView[] children) =>
        Section("s-1", SectionKeys.Coordination, "Coordination", children);

    private static SectionView Outbound(bool hidden = false) => new()
    {
        Id = "s-2", Title = "LIBB → LDZO", Depth = 1, SectionKey = SectionKeys.CoordinationOut,
        IsHidden = hidden, Blocks = Array.Empty<BlockView>(), Children = Array.Empty<SectionView>(),
    };

    private static SectionView Inbound(bool hidden = false) => new()
    {
        Id = "s-3", Title = "LDZO → LIBB", Depth = 1, SectionKey = SectionKeys.CoordinationIn,
        IsHidden = hidden, Blocks = Array.Empty<BlockView>(), Children = Array.Empty<SectionView>(),
    };

    [Fact]
    public void Both_directions_are_rendered_from_the_parent()
    {
        var cut = Render(DocWith(Coordination(Outbound(), Inbound())));

        Assert.Contains("USOSA", cut.Markup);   // LIBB → LDZO
        Assert.Contains("VALKO", cut.Markup);   // LDZO → LIBB
    }

    [Fact]
    public void Hiding_a_direction_hides_that_direction_and_only_that_one()
    {
        // Riconoscimento per CHIAVE: nascondere la seconda figlia non deve togliere la prima direzione.
        var cut = Render(DocWith(Coordination(Outbound(), Inbound(hidden: true))));

        Assert.Contains("USOSA", cut.Markup);
        Assert.DoesNotContain("VALKO", cut.Markup);
    }

    [Fact]
    public void The_order_of_the_children_does_not_decide_the_direction()
    {
        // Prima la direzione era la figlia [0] / la figlia [1]: con le figlie invertite il documento avrebbe
        // scambiato i due versi senza dirlo.
        var cut = Render(DocWith(Coordination(Inbound(), Outbound(hidden: true))));

        Assert.Contains("VALKO", cut.Markup);
        Assert.DoesNotContain("USOSA", cut.Markup);
    }

    [Fact]
    public void The_children_order_decides_which_direction_comes_first()
    {
        // Le due direzioni si spostano dall'editor come ogni altra sezione del gruppo: il documento pubblicato
        // segue l'ordine delle sotto-sezioni, non una sequenza scritta nel viewer. La CHIAVE resta l'unica cosa
        // che dice QUALE verso è (vedi il test qui sopra): qui cambia solo chi viene prima.
        var cut = Render(DocWith(Coordination(Inbound(), Outbound())));

        Assert.True(cut.Markup.IndexOf("VALKO", StringComparison.Ordinal)
                    < cut.Markup.IndexOf("USOSA", StringComparison.Ordinal));
    }

    [Fact]
    public void Without_direction_subsections_the_canonical_order_holds()
    {
        // Sotto-sezioni assenti (o snapshot storici con la chiave del padre): uscente prima, entrante dopo.
        var cut = Render(DocWith(Coordination()));

        Assert.True(cut.Markup.IndexOf("USOSA", StringComparison.Ordinal)
                    < cut.Markup.IndexOf("VALKO", StringComparison.Ordinal));
    }

    [Fact]
    public void Extra_subsections_of_coordination_are_rendered()
    {
        // doc 11 §3b: se l'editor sa creare una cosa, il viewer la deve rendere. Questo ramo era l'unico a
        // buttare via le sotto-sezioni: si potevano scrivere con «+ sotto-sez» e non comparivano nel documento.
        var extra = Section("s-9", SectionKeys.NewCustom(), "Local arrangements",
            blocks: new[] { Prose(1, "Radar handover at BEBIX.") }, depth: 1);

        var cut = Render(DocWith(Coordination(Outbound(), Inbound(), extra)));

        Assert.Contains("Local arrangements", cut.Markup);
        Assert.Contains("Radar handover at BEBIX.", cut.Markup);
    }

    [Fact]
    public void An_extra_subsection_marked_before_the_body_comes_first()
    {
        // Gli slot (doc 11 §3g) valgono anche qui: «prima del corpo» significa prima delle due direzioni.
        var before = Section("s-9", SectionKeys.NewCustom(), "Preamble",
            blocks: new[] { Prose(1, "Preamble text.") }, depth: 1, beforeParentBody: true);

        var cut = Render(DocWith(Coordination(before, Outbound(), Inbound())));

        Assert.True(cut.Markup.IndexOf("Preamble text.", StringComparison.Ordinal)
                    < cut.Markup.IndexOf("USOSA", StringComparison.Ordinal));
    }

    [Fact]
    public void A_section_the_catalog_declares_collapsed_is_born_closed()
    {
        // doc 11 §3i, decisione owner: vale OVUNQUE, viewer ed editor, tutte e tre le famiglie. La verifica
        // live del doc 13 ha trovato che questo ramo (e quello dell'APP) non lo chiedeva al catalogo:
        // «Aree regolamentate» nasceva aperta su una famiglia e chiusa sulle altre due.
        var regulated = Section("s-4", "regulated", "Military areas coordination and management");
        var frequencies = Section("s-5", "frequencies", "Frequencies");

        var cut = Render(DocWith(regulated, frequencies));

        Assert.False(cut.Find("details#s-4").HasAttribute("open"));
        Assert.True(cut.Find("details#s-5").HasAttribute("open"));
    }

    // ---- snapshot di release ANTERIORI al doc 13: le due direzioni portano ancora la chiave del padre ----
    // La riconciliazione al boot sistema i documenti, non le release già pubblicate — che sono ciò che il
    // pubblico legge. Trovato in verifica live: le direzioni comparivano DUE VOLTE, e la seconda copia portava
    // il paragrafo segnaposto che nessuna vista aveva mai mostrato.

    private static int Occorrenze(string markup, string needle)
    {
        var n = 0;
        for (var i = markup.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = markup.IndexOf(needle, i + needle.Length, StringComparison.Ordinal)) n++;
        return n;
    }

    private static SectionView LegacyDirection(string id, string title, bool hidden = false) => new()
    {
        Id = id, Title = title, Depth = 1, SectionKey = SectionKeys.Coordination, IsHidden = hidden,
        Blocks = new[] { Prose(int.Parse(id.Split('-')[1]), $"**{title}** transfers traffic, as published.") },
        Children = Array.Empty<SectionView>(),
    };

    [Fact]
    public void A_legacy_snapshot_does_not_render_the_directions_twice()
    {
        var cut = Render(DocWith(Coordination(
            LegacyDirection("s-2", "LIBB → LDZO"), LegacyDirection("s-3", "LDZO → LIBB"))));

        // Le direzioni ci sono, e il loro titolo compare UNA volta sola: dal corpo (h4). Se le figlie
        // finissero anche fra le sotto-sezioni, lo stesso titolo tornerebbe nel <summary> di una card.
        Assert.Contains("USOSA", cut.Markup);
        Assert.Contains("VALKO", cut.Markup);
        Assert.Equal(1, Occorrenze(cut.Markup, "LIBB → LDZO"));
        Assert.Equal(1, Occorrenze(cut.Markup, "LDZO → LIBB"));
        Assert.DoesNotContain("transfers traffic, as published.", cut.Markup);
    }

    [Fact]
    public void On_a_legacy_snapshot_hiding_a_direction_still_works_by_position()
    {
        // Quella fotografia non ha altro appiglio: entrambe le figlie portano la chiave del padre.
        var cut = Render(DocWith(Coordination(
            LegacyDirection("s-2", "LIBB → LDZO"), LegacyDirection("s-3", "LDZO → LIBB", hidden: true))));

        Assert.Contains("USOSA", cut.Markup);
        Assert.DoesNotContain("VALKO", cut.Markup);
    }

    [Fact]
    public void A_legacy_snapshot_still_renders_the_extra_subsections()
    {
        var extra = Section("s-9", SectionKeys.NewCustom(), "Local arrangements",
            blocks: new[] { Prose(1, "Radar handover at BEBIX.") }, depth: 1);

        var cut = Render(DocWith(Coordination(
            LegacyDirection("s-2", "LIBB → LDZO"), LegacyDirection("s-3", "LDZO → LIBB"), extra)));

        Assert.Contains("Radar handover at BEBIX.", cut.Markup);
    }
}
