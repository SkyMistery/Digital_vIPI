using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Dove l'editor accetta contenuto, e dove no. La domanda «il corpo di questa sezione lo produce la PAGINA?»
/// è <b>una sola</b> — <see cref="SectionCatalog.IsHostRendered"/> — e la fanno l'editor e il viewer allo
/// stesso modo: un editor che accetta una tabella in una sezione da cui il documento non la stampa è una
/// promessa non mantenuta.
///
/// <para>⚠️ Fino al 5 settembre 2026 la domanda la faceva ogni host per conto suo, passandola come
/// parametro: cinque copie, e due (aeroporto e APP) chiedevano in più <c>Depth == 0</c>. Su una SOTTO-sezione
/// con una chiave di catalogo l'editor offriva quindi «+ blocco» e il documento non stampava niente. Nessun
/// documento in archivio ha una sezione così — la divergenza era scritta, non ancora pagata.</para>
/// </summary>
public class SezioniReseDallaPaginaTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    /// <summary>Nessuna mutazione parte in queste prove: si guarda quali comandi ci sono, non che cosa fanno.</summary>
    private sealed class EditingMuto : EditingServiceStub { }

    public SezioniReseDallaPaginaTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddScoped<IEditingService>(_ => new EditingMuto());
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

    /// <summary>
    /// Quattro casi in un documento solo: libera, resa dalla pagina, resa dalla pagina ma <b>figlia</b>
    /// (il caso che divergeva), e «scheda + blocchi».
    /// </summary>
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
            Sez(10, "Sezione libera", 0),
            Sez(20, "Frequenze", 0, "frequencies"),
            Sez(30, "Contenitore", 0, null, Sez(31, "Frequenze annidate", 1, "frequencies")),
            Sez(40, "Validità e revisione", 0, "validity"),
        },
    };

    private IRenderedComponent<DocumentSectionsEditor> Editor() =>
        RenderComponent<DocumentSectionsEditor>(p => p
            .Add(x => x.Doc, Documento())
            .Add(x => x.IsEditing, true)
            .Add(x => x.Profile, SectionProfile.App)
            .Add(x => x.DerivedContent, (RenderFragment<EditableSection>)(s => b =>
            {
                b.OpenElement(0, "div");
                b.AddAttribute(1, "class", "scheda");
                b.AddAttribute(2, "data-sez", s.Id.ToString());
                b.CloseElement();
            }))
            .Add(x => x.Run, (Func<Func<Task>, Task>)(azione => azione())));

    [Fact]
    public void La_scheda_della_pagina_si_disegna_a_QUALUNQUE_profondita()
    {
        var cut = Editor();

        var schede = cut.FindAll(".scheda").Select(e => e.GetAttribute("data-sez")).ToList();
        Assert.Contains("20", schede);
        Assert.Contains("31", schede);   // ⚠️ la figlia: prima la pagina non la disegnava nell'editor
        Assert.Contains("40", schede);   // «scheda + blocchi»: la scheda c'è comunque
        Assert.DoesNotContain("10", schede);
        Assert.DoesNotContain("30", schede);
    }

    [Fact]
    public void Il_tasto_blocco_c_e_solo_dove_il_documento_stampa_i_blocchi()
    {
        var cut = Editor();

        // Un menu per ogni sezione che tiene i propri blocchi: le due libere e «Validità e revisione».
        // ⚠️ Tre, non quattro: la FIGLIA `frequencies` non ne ha più uno — è il difetto che si chiude qui.
        Assert.Equal(3, cut.FindAll("details.blk-add").Count);
    }

    [Fact]
    public void Fuori_dalla_modifica_non_si_aggiunge_niente()
    {
        var cut = RenderComponent<DocumentSectionsEditor>(p => p
            .Add(x => x.Doc, Documento())
            .Add(x => x.IsEditing, false)
            .Add(x => x.Profile, SectionProfile.App)
            .Add(x => x.Run, (Func<Func<Task>, Task>)(azione => azione())));

        Assert.Empty(cut.FindAll("details.blk-add"));
    }
}
