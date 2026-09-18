using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// L'editor dei documenti e le SID citate (§A73): in cima l'avviso di quelle da ricontrollare (slice 4), e fuori
/// dalla modifica i blocchi col nome di oggi (slice 3). La pagina pubblica non segnala niente, apposta: è qui,
/// dove si può rimediare, che si deve sapere.
/// </summary>
public class AvvisoSidCitateTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class EditingMuto : EditingServiceStub { }

    /// <summary>Il risolutore con la tabella di LIRF data dal test: la bozza, come nell'editor.</summary>
    private sealed class TabellaDiLirf : ISidReferenceResolver
    {
        private readonly string[] _nomi;
        public TabellaDiLirf(params string[] nomi) => _nomi = nomi;

        private NomiSid Nomi => new(new Dictionary<string, AirportSidView>
        {
            ["LIRF"] = new(_nomi.Select(n => new AirportSidRowView("16L", "OSTIA", n, "—", "—", "—", "—", "—", "—")).ToList()),
        });

        public Task<NomiSid> PerVistaAsync(IEnumerable<SectionView> sezioni, bool pubblica,
            string? proprioIcao = null, AirportSidView? propriaTabella = null, CancellationToken ct = default) =>
            Task.FromResult(Nomi);

        public Task<NomiSid> PerTestiAsync(IEnumerable<string?> testi, CancellationToken ct = default) =>
            Task.FromResult(Nomi);

        public Task<IReadOnlyList<SidCitabile>> ElencoAsync(string icao, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SidCitabile>>(Array.Empty<SidCitabile>());
    }

    public AvvisoSidCitateTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddScoped<IEditingService>(_ => new EditingMuto());
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static EditableDocument Documento() => new()
    {
        DocumentId = 1, VersionId = 2, VersionNumber = 3, VersionStatus = DocumentStatus.Draft,
        Title = "Prova", Language = Language.En,
        Sections = new[]
        {
            new EditableSection
            {
                Id = 10, Title = "Remarks", SectionKey = "custom:0000000a", Depth = 0, Order = 1,
                Blocks = new[]
                {
                    new EditableBlock
                    {
                        Id = 100, Order = 1, Format = BlockFormat.Prose, Tier = BlockTier.Extended,
                        Visibility = BlockVisibility.Always, Body = "Expect [[SID LIRF OST1E]] after departure.",
                    },
                },
                Children = Array.Empty<EditableSection>(),
            },
        },
    };

    private IRenderedComponent<DocumentSectionsEditor> Editor(bool inModifica) =>
        RenderComponent<DocumentSectionsEditor>(p => p
            .Add(x => x.Doc, Documento())
            .Add(x => x.IsEditing, inModifica)
            .Add(x => x.Run, (Func<Func<Task>, Task>)(azione => azione())));

    [Fact]
    public void Una_SID_che_non_c_e_piu_si_segnala_in_cima_con_la_sua_sezione()
    {
        Services.AddScoped<ISidReferenceResolver>(_ => new TabellaDiLirf("RATI1D"));
        var c = Editor(inModifica: true);

        var avviso = c.Find(".sidref-check");
        Assert.Contains("LIRF OST1E", avviso.TextContent);
        Assert.Contains("Sid_Check_Missing", avviso.TextContent);
        Assert.Contains("(Remarks)", avviso.TextContent);
    }

    [Fact]
    public void Una_SID_che_si_trova_non_si_segnala_e_l_anteprima_dice_il_nome_di_oggi()
    {
        Services.AddScoped<ISidReferenceResolver>(_ => new TabellaDiLirf("OST2E"));
        var c = Editor(inModifica: false);

        Assert.Empty(c.FindAll(".sidref-check"));
        Assert.Contains("Expect OSTIA 2E after departure.", c.Markup);
        Assert.DoesNotContain("[[SID", c.Markup);
    }
}
