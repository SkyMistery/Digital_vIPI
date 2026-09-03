using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il menu «Sposta in…» nell'intestazione di sezione (carta 2026-09-04): c'è per le sezioni <b>libere</b>, non
/// per quelle di catalogo, e quel che chiede al servizio è esattamente la destinazione scelta.
///
/// <para>⚠️ Un comando che si vede e non fa niente è peggio di un comando che non c'è: qui si prova la
/// catena intera — il tasto compare, il clic chiama <c>MoveSectionToParentAsync</c> con quel padre, e la
/// mossa passa dal <c>Run</c> dell'host (che è chi mostra errori e stato di salvataggio).</para>
/// </summary>
public class MenuSpostaInTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    /// <summary>Servizio di editing che registra la sola mossa che ci interessa e rifiuta tutto il resto.</summary>
    private sealed class EditingSpia : EditingServiceStub
    {
        public List<(int Section, int? Parent, int? Before)> Mosse { get; } = new();

        public override Task MoveSectionToParentAsync(int sectionId, int? newParentSectionId, int? beforeSectionId, CancellationToken ct = default)
        {
            Mosse.Add((sectionId, newParentSectionId, beforeSectionId));
            return Task.CompletedTask;
        }
    }

    private readonly EditingSpia _spia = new();

    public MenuSpostaInTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddScoped<IEditingService>(_ => _spia);
    }

    private static EditableSection Sez(int id, string titolo, int depth, string? chiave = null,
        params EditableSection[] figlie) => new()
    {
        Id = id,
        Title = titolo,
        SectionKey = chiave ?? $"custom:{id:x8}",
        Depth = depth,
        Order = id,
        Blocks = Array.Empty<EditableBlock>(),
        Children = figlie,
    };

    /// <summary>Due radici: una di catalogo e una libera con una figlia libera.</summary>
    private static EditableDocument Documento() => new()
    {
        DocumentId = 1,
        VersionId = 2,
        VersionNumber = 3,
        VersionStatus = DocumentStatus.Draft,
        Title = "Prova",
        Language = Language.It,
        Sections = new[]
        {
            Sez(10, "Frequenze", 0, "frequencies"),
            Sez(20, "Sezione libera", 0, null, Sez(21, "Figlia libera", 1)),
        },
    };

    private IRenderedComponent<DocumentSectionsEditor> Editor(bool inModifica = true) =>
        RenderComponent<DocumentSectionsEditor>(p => p
            .Add(x => x.Doc, Documento())
            .Add(x => x.IsEditing, inModifica)
            .Add(x => x.Profile, SectionProfile.App)
            .Add(x => x.IsMandatory, (Func<EditableSection, bool>)(s => !SectionKeys.IsCustom(s.SectionKey)))
            .Add(x => x.Run, (Func<Func<Task>, Task>)(azione => azione())));

    [Fact]
    public void Il_menu_c_e_per_una_sezione_libera_e_non_per_una_di_catalogo()
    {
        var cut = Editor();

        // Un menu per la radice libera e uno per la sua figlia: due, non tre — la sezione di catalogo non l'ha.
        Assert.Equal(2, cut.FindAll("details.blk-add > summary[title='Dse_MoveToTitle']").Count);
    }

    [Fact]
    public void Fuori_dalla_modifica_il_menu_non_c_e()
    {
        Assert.Empty(Editor(inModifica: false).FindAll("summary[title='Dse_MoveToTitle']"));
    }

    /// <summary>Il clic chiede ESATTAMENTE la destinazione scelta, e in coda al gruppo nuovo.</summary>
    [Fact]
    public void Il_clic_chiede_la_destinazione_scelta()
    {
        var cut = Editor();

        // Il menu della FIGLIA (id 21): le sue destinazioni sono il primo livello e la sezione di catalogo.
        var menu = cut.FindAll("details.blk-add").First(d => d.QuerySelector("summary[title='Dse_MoveToTitle']") is not null
                                                             && d.TextContent.Contains("Dse_MoveToTop"));
        var voce = menu.QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Dse_MoveToTop");
        voce.Click();

        var mossa = Assert.Single(_spia.Mosse);
        Assert.Equal(21, mossa.Section);
        Assert.Null(mossa.Parent);    // primo livello
        Assert.Null(mossa.Before);    // in coda: la posizione si sceglie dopo, con le frecce
    }

    /// <summary>Una sezione non offre sé stessa né il proprio padre: sarebbero due comandi che non fanno nulla.</summary>
    [Fact]
    public void Il_menu_non_offre_il_padre_attuale()
    {
        var cut = Editor();
        var menuFiglia = cut.FindAll("details.blk-add")
            .First(d => d.QuerySelector("summary[title='Dse_MoveToTitle']") is not null
                        && d.TextContent.Contains("Dse_MoveToTop"));

        Assert.DoesNotContain("Sezione libera", menuFiglia.TextContent);
        Assert.Contains("Frequenze", menuFiglia.TextContent);
    }
}
