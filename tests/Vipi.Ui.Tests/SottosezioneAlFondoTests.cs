using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Sul fondo dell'albero il tasto «+ Sottosezione» è <b>spento</b>, e dice perché (16 settembre 2026).
///
/// <para>⚠️ Prima era sempre acceso: il rifiuto arrivava dal motore
/// (<c>EfEditingRepository.AddSectionAsync</c>), l'editor lo catturava e lo scriveva nel callout d'errore —
/// che sta in <b>cima alla pagina</b>. Su un documento lungo (il vSOP militare di Decimomannu) chi premeva
/// stava tre schermate più in basso e non vedeva niente: premi, non succede nulla. È arrivato dal campo
/// come «il documento è saturo», che è esattamente la diagnosi che si fa quando il programma tace.</para>
///
/// <para>⚠️ E il <c>title</c> porta il MOTIVO, non l'etichetta normale: un <c>disabled</c> muto, in
/// un'interfaccia dove i permessi contano, si legge «non ti è permesso» — e quello si rimedia chiedendo a
/// qualcuno, mentre questo si rimedia mettendo un blocco invece di una sotto-sezione.</para>
///
/// <para>La profondità viene da <see cref="DocumentSection.MaxDepth"/> e non è scritta a mano: alzando il
/// tetto questa prova deve continuare a parlare del FONDO, qualunque numero sia.</para>
/// </summary>
public class SottosezioneAlFondoTests : TestContext
{
    private sealed class EditingMuto : EditingServiceStub { }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public SottosezioneAlFondoTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddScoped<IEditingService>(_ => new EditingMuto());
        Services.AddScoped<IProcedureReferenceResolver, NessunRiferimentoCitato>();
        Services.AddScoped<IRiferimentiResolver, NessunRiferimentoCitato>();
    }

    /// <summary>Una scala di sezioni annidate, da profondità 0 fino al fondo consentito.</summary>
    private static EditableDocument ScalaFinoAlFondo()
    {
        EditableSection? sotto = null;
        for (var profondita = DocumentSection.MaxDepth; profondita >= 0; profondita--)
        {
            sotto = new EditableSection
            {
                Id = 10 + profondita,
                Title = $"Livello {profondita}",
                SectionKey = $"custom:{profondita:x8}",
                Depth = profondita,
                Order = 1,
                Blocks = Array.Empty<EditableBlock>(),
                Children = sotto is null ? Array.Empty<EditableSection>() : new[] { sotto },
            };
        }

        return new EditableDocument
        {
            DocumentId = 1,
            VersionId = 2,
            VersionNumber = 3,
            VersionStatus = DocumentStatus.Draft,
            Title = "Prova",
            Language = Language.It,
            Sections = new[] { sotto! },
        };
    }

    private IRenderedComponent<DocumentSectionsEditor> Editor(bool tutteDiCatalogo) =>
        RenderComponent<DocumentSectionsEditor>(p => p
            .Add(x => x.Doc, ScalaFinoAlFondo())
            .Add(x => x.IsEditing, true)
            .Add(x => x.Profile, SectionProfile.App)
            .Add(x => x.IsMandatory, (Func<EditableSection, bool>)(_ => tutteDiCatalogo))
            .Add(x => x.Run, (Func<Func<Task>, Task>)(azione => azione())));

    /// <summary>Le sezioni LIBERE: il tasto sta nella riga di rinomina.</summary>
    [Theory]
    [InlineData(false)]   // sezione libera  → title "Dse_AddSubsectionTitle"
    [InlineData(true)]    // sezione di catalogo → title "Dse_AddSubsectionExtraTitle"
    public void Il_tasto_sottosezione_e_spento_solo_sul_fondo(bool diCatalogo)
    {
        var cut = Editor(diCatalogo);

        var tasti = cut.FindAll("button")
            .Where(b => b.TextContent.Contains("Dse_Subsection", StringComparison.Ordinal))
            .ToList();

        // Uno per ogni sezione della scala: profondità 0…MaxDepth.
        Assert.Equal(DocumentSection.MaxDepth + 1, tasti.Count);

        // Tutti accesi tranne l'ultimo, che è quello sul fondo.
        var spenti = tasti.Where(b => b.HasAttribute("disabled")).ToList();
        Assert.Single(spenti);
        Assert.Same(tasti[^1], spenti[0]);

        // ⚠️ Lo spento dice PERCHÉ: senza, si legge «permesso mancante».
        Assert.Equal("Dse_AddSubsectionMaxDepth", spenti[0].GetAttribute("title"));

        // E gli accesi portano ancora la loro etichetta, che il motivo non ha mangiato.
        var attesa = diCatalogo ? "Dse_AddSubsectionExtraTitle" : "Dse_AddSubsectionTitle";
        Assert.All(tasti[..^1], b => Assert.Equal(attesa, b.GetAttribute("title")));
    }
}
