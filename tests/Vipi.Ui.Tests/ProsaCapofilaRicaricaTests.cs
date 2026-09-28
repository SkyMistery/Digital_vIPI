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
/// 🔴 U-167 (revisione totale 3): «¶ Prosa capofila/distesa» salvava la scelta e non ricaricava. L'etichetta restava
/// quella di prima e il secondo clic, che doveva tornare indietro, riscriveva lo stesso valore. Ogni altro comando
/// della riga (nascondi, modo di resa, sposta) chiama <c>OnChanged</c> dopo la scrittura; questo no.
/// </summary>
public class ProsaCapofilaRicaricaTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class EditingSpia : EditingServiceStub
    {
        public List<(int Section, bool Capofila)> Scritture { get; } = new();

        public override Task SetSectionLeadSentenceAsync(int sectionId, bool leadSentence, CancellationToken ct = default)
        {
            Scritture.Add((sectionId, leadSentence));
            return Task.CompletedTask;
        }
    }

    private readonly EditingSpia _spia = new();

    public ProsaCapofilaRicaricaTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddScoped<IEditingService>(_ => _spia);
        Services.AddScoped<IProcedureReferenceResolver, NessunRiferimentoCitato>();
        Services.AddScoped<IRiferimentiResolver, NessunRiferimentoCitato>();
    }

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
            new EditableSection
            {
                Id = 30, Title = "Coordinamenti", SectionKey = "coordination", Depth = 0, Order = 1,
                Blocks = Array.Empty<EditableBlock>(), Children = Array.Empty<EditableSection>(),
            },
        },
    };

    [Fact]
    public async Task Commutare_la_prosa_ricarica_il_documento()
    {
        var ricarichi = 0;
        var cut = RenderComponent<DocumentSectionsEditor>(p => p
            .Add(x => x.Doc, Documento())
            .Add(x => x.IsEditing, true)
            .Add(x => x.Profile, SectionProfile.App)
            .Add(x => x.IsMandatory, (Func<EditableSection, bool>)(_ => false))
            .Add(x => x.Run, (Func<Func<Task>, Task>)(azione => azione()))
            .Add(x => x.OnChanged, EventCallback.Factory.Create(this, () => ricarichi++)));

        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.TextContent.Contains("Dse_ProseSpread")).Click());

        Assert.Equal(new[] { (30, true) }, _spia.Scritture);
        Assert.Equal(1, ricarichi);
    }
}
