using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La tendina «Sposta in…» nell'intestazione di sezione (carta 2026-09-04): c'è per le sezioni <b>libere</b>,
/// non per quelle di catalogo, e quel che chiede al servizio è esattamente la destinazione scelta.
///
/// <para>⚠️ Un comando che si vede e non fa niente è peggio di un comando che non c'è: qui si prova la
/// catena intera — il comando compare, la scelta chiama <c>MoveSectionToParentAsync</c> con quel padre, e la
/// mossa passa dal <c>Run</c> dell'host (che è chi mostra errori e stato di salvataggio).</para>
///
/// <para>⚠️ Era un menu in linea, ed è diventato una TENDINA il 4 settembre 2026: nella riga dei comandi —
/// che è `nowrap` — il menu disegnato da noi usciva dalla card, e aperto ci finivano fuori tutte e trenta le
/// destinazioni. Il pannello di una tendina di sistema non lo ritaglia nessun `overflow`.</para>
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

    /// <summary>La tendina della FIGLIA (id 21): le sue destinazioni sono il primo livello e la sezione di
    /// catalogo — è l'unica che offre «primo livello», perché la radice libera lì ci sta già.</summary>
    private static IElement TendinaDellaFiglia(IRenderedComponent<DocumentSectionsEditor> cut) =>
        cut.FindAll("select.dse-move").First(s => s.TextContent.Contains("Dse_MoveToTop"));

    [Fact]
    public void La_tendina_c_e_per_una_sezione_libera_e_non_per_una_di_catalogo()
    {
        var cut = Editor();

        // Una per la radice libera e una per la sua figlia: due, non tre — la sezione di catalogo non l'ha.
        Assert.Equal(2, cut.FindAll("select.dse-move").Count);
    }

    [Fact]
    public void Fuori_dalla_modifica_la_tendina_non_c_e()
    {
        Assert.Empty(Editor(inModifica: false).FindAll("select.dse-move"));
    }

    /// <summary>La scelta chiede ESATTAMENTE la destinazione, e in coda al gruppo nuovo.</summary>
    [Fact]
    public void La_scelta_chiede_la_destinazione()
    {
        var cut = Editor();
        var tendina = TendinaDellaFiglia(cut);
        var primoLivello = tendina.QuerySelectorAll("option")
            .First(o => o.TextContent.Contains("Dse_MoveToTop")).GetAttribute("value");

        tendina.Change(primoLivello);

        var mossa = Assert.Single(_spia.Mosse);
        Assert.Equal(21, mossa.Section);
        Assert.Null(mossa.Parent);    // primo livello
        Assert.Null(mossa.Before);    // in coda: la posizione si sceglie dopo, con le frecce
    }

    /// <summary>⚠️ Il segnaposto non è una destinazione: sceglierlo non deve chiedere niente.</summary>
    [Fact]
    public void Il_segnaposto_non_muove_niente()
    {
        var cut = Editor();

        TendinaDellaFiglia(cut).Change("");

        Assert.Empty(_spia.Mosse);
    }

    /// <summary>Una sezione non offre sé stessa né il proprio padre: sarebbero due comandi che non fanno nulla.</summary>
    [Fact]
    public void La_tendina_non_offre_il_padre_attuale()
    {
        var tendina = TendinaDellaFiglia(Editor());

        Assert.DoesNotContain("Sezione libera", tendina.TextContent);
        Assert.Contains("Frequenze", tendina.TextContent);
    }

    /// <summary>⚠️ In modifica la riga dei comandi va a capo: senza, a 1280 il comando in coda esce dalla
    /// card e non si può premere. È una classe, e la classe si vede.</summary>
    [Fact]
    public void In_modifica_la_riga_dei_comandi_va_a_capo()
    {
        Assert.All(Editor().FindAll(".dse-head"), h => Assert.Contains("editing", h.ClassName ?? ""));
        Assert.All(Editor(inModifica: false).FindAll(".dse-head"), h => Assert.DoesNotContain("editing", h.ClassName ?? ""));
    }
}
