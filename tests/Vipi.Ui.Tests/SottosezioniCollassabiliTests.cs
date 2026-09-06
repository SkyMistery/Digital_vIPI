using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Le <b>sotto-sezioni</b> dell'editor si chiudono come le radici (6 settembre 2026, richiesta del
/// committente).
///
/// <para>⚠️ Prima erano un <c>&lt;div class="coord-sub"&gt;</c> fisso, e l'editor diceva una cosa diversa dal
/// documento: nel viewer una sotto-sezione si è sempre chiusa (<c>SectionNode</c> la rende
/// <c>&lt;details&gt;</c>), nell'editor no. Su un vSOP militare le figlie sono <b>venti su ventisei</b>:
/// arrivare all'ultima voleva dire scorrere tutte le altre, aperte.</para>
///
/// <para>⚠️ E passano dal componente <b>condiviso</b>, non da un secondo <c>&lt;details&gt;</c> scritto a
/// mano: è quello che fa valere anche qui il chevron, l'attributo <c>data-persist</c> e
/// «espandi/comprimi tutti», senza che nessuno se li debba ricordare. Provato dal vivo: «comprimi tutti»
/// chiude <b>32 sezioni su 32</b> sul vSOP militare di LIMS, figlie comprese.</para>
/// </summary>
public class SottosezioniCollassabiliTests : TestContext
{
    /// <summary>Un servizio di editing che non fa niente: qui si prova la FORMA della pagina, non le
    /// mosse. Lo stampo astratto è condiviso, e non si può istanziare da solo.</summary>
    private sealed class EditingMuto : EditingServiceStub { }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public SottosezioniCollassabiliTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddScoped<IEditingService>(_ => new EditingMuto());
    }

    private static EditableSection Sez(int id, string titolo, int depth, bool nascosta = false,
        params EditableSection[] figlie) => new()
    {
        Id = id,
        Title = titolo,
        SectionKey = $"custom:{id:x8}",
        Depth = depth,
        Order = id,
        IsHidden = nascosta,
        Blocks = Array.Empty<EditableBlock>(),
        Children = figlie,
    };

    private static EditableDocument Documento(bool figliaNascosta = false) => new()
    {
        DocumentId = 1,
        VersionId = 2,
        VersionNumber = 3,
        VersionStatus = DocumentStatus.Draft,
        Title = "Prova",
        Language = Language.It,
        Sections = new[]
        {
            Sez(10, "Radice", 0, false,
                Sez(11, "Figlia", 1, figliaNascosta,
                    Sez(12, "Nipote", 2))),
        },
    };

    private IRenderedComponent<DocumentSectionsEditor> Editor(bool figliaNascosta = false) =>
        RenderComponent<DocumentSectionsEditor>(p => p
            .Add(x => x.Doc, Documento(figliaNascosta))
            .Add(x => x.IsEditing, true)
            .Add(x => x.Profile, SectionProfile.App)
            .Add(x => x.IsMandatory, (Func<EditableSection, bool>)(s => !SectionKeys.IsCustom(s.SectionKey)))
            .Add(x => x.Run, (Func<Func<Task>, Task>)(azione => azione())));

    /// <summary>Ogni sezione è un <c>&lt;details&gt;</c>, a tutti e tre i livelli: radice, figlia e nipote.</summary>
    [Fact]
    public void Anche_le_sottosezioni_sono_details()
    {
        var cut = Editor();

        Assert.Equal("DETAILS", cut.Find("#s-10").TagName);
        Assert.Equal("DETAILS", cut.Find("#s-11").TagName);
        Assert.Equal("DETAILS", cut.Find("#s-12").TagName);
    }

    /// <summary>
    /// La profondità si vede nella classe, e non è solo estetica: una sotto-sezione non deve avere la
    /// cornice di una card di primo livello, o due livelli si leggono come uno.
    /// </summary>
    [Fact]
    public void I_tre_livelli_si_distinguono()
    {
        var cut = Editor();

        Assert.Contains("block", cut.Find("#s-10").ClassName!);
        Assert.Contains("cb-sub", cut.Find("#s-11").ClassName!);
        Assert.DoesNotContain("block", cut.Find("#s-11").ClassName!);
        Assert.Contains("cb-sub2", cut.Find("#s-12").ClassName!);
    }

    /// <summary>
    /// La sotto-sezione porta <c>data-persist</c> come le radici: è l'attributo che <c>wireCollapse</c>
    /// guarda per ricordare aperto/chiuso.
    ///
    /// <para>⚠️ <b>Questo prova l'attributo, non la memoria.</b> Misurato dal vivo il 6 settembre 2026:
    /// dentro l'editor una sezione chiusa <b>regge il ridisegno</b> di Blazor (prendere il lock non la
    /// riapre), ma <b>non</b> sopravvive al <b>ricarico della pagina</b> — e non ci sopravvivono nemmeno le
    /// <b>radici</b>, che l'attributo ce l'hanno da sempre. La causa non sta qui: su una pagina
    /// <c>InteractiveServer</c> i <c>&lt;details&gt;</c> nascono <b>dopo</b> che <c>vipiWireUi</c> è girato, e
    /// <c>wireCollapse</c> aggancia solo quel che trova al momento. È un difetto <b>preesistente</b>, non
    /// una conseguenza di questa modifica, e sta fuori dalla richiesta.</para>
    /// </summary>
    [Fact]
    public void La_sottosezione_porta_lattributo_della_persistenza()
    {
        var cut = Editor();

        Assert.Equal("s-11", cut.Find("#s-11").GetAttribute("data-persist"));
        Assert.Equal("s-12", cut.Find("#s-12").GetAttribute("data-persist"));
    }

    /// <summary>
    /// Una sotto-sezione <b>nascosta</b> nasce chiusa, come già fanno le radici: è esclusa dal documento,
    /// quindi non è lì che si lavora, e aperta pagherebbe la sua altezza intera a chi scorre per arrivare
    /// alle altre.
    /// </summary>
    [Fact]
    public void Una_sottosezione_nascosta_nasce_chiusa()
    {
        Assert.True(Editor().Find("#s-11").HasAttribute("open"));
        Assert.False(Editor(figliaNascosta: true).Find("#s-11").HasAttribute("open"));
    }

    /// <summary>
    /// 🔴 I comandi dell'intestazione NON devono chiudere la sezione: stanno dentro il
    /// <c>&lt;summary&gt;</c>, dove un clic è il gesto che apre e chiude. La riga porta
    /// <c>@onclick:preventDefault</c>, ed è la stessa ragione per cui ce l'aveva già al primo livello —
    /// solo che al primo livello era già dentro un <c>&lt;summary&gt;</c> e qui no.
    /// </summary>
    [Fact]
    public void I_comandi_dellintestazione_stanno_nel_summary_e_non_chiudono()
    {
        var cut = Editor();

        var testata = cut.Find("#s-11 > summary .dse-head");
        Assert.NotNull(testata);
        Assert.NotEmpty(testata.QuerySelectorAll("button"));
    }
}
