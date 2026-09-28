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
/// U-162 (revisione totale 3): nella tabella generica due celle scritte in fila, più in fretta del giro salva+ricarica,
/// perdevano la prima. Il secondo gesto componeva il JSON dal blocco VISTO al momento dell'evento (senza la prima
/// cella) e mandava il suo token di concorrenza vecchio: «Il blocco è stato modificato nel frattempo», e la cella
/// mostrava un valore mai salvato. Ora la modifica si applica dentro il tornello, sul blocco più recente.
/// </summary>
public class CelleInFilaTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    /// <summary>Il primo salvataggio si trattiene finché il test non lo lascia andare: è la finestra del DB remoto.</summary>
    private sealed class EditingLento : EditingServiceStub
    {
        public List<BlockEdit> Scritture { get; } = new();
        public TaskCompletionSource Primo { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task UpdateBlockAsync(int blockId, BlockEdit edit, CancellationToken ct = default)
        {
            Scritture.Add(edit);
            if (Scritture.Count == 1) await Primo.Task;
        }
    }

    private readonly EditingLento _svc = new();

    public CelleInFilaTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddScoped<IEditingService>(_ => _svc);
        Services.AddScoped<IProcedureReferenceResolver, NessunRiferimentoCitato>();
        Services.AddScoped<IRiferimentiResolver, NessunRiferimentoCitato>();
    }

    private static EditableDocument Documento(string json, string rowVersion) => new()
    {
        DocumentId = 1, VersionId = 2, VersionNumber = 3, VersionStatus = DocumentStatus.Draft,
        Title = "Prova", Language = Language.It,
        Sections = new[]
        {
            new EditableSection
            {
                Id = 30, Title = "Note", SectionKey = SectionKeys.NewCustom(), Depth = 0, Order = 1,
                Children = Array.Empty<EditableSection>(),
                Blocks = new[]
                {
                    new EditableBlock
                    {
                        Id = 7, Order = 1, Format = BlockFormat.Table, Tier = BlockTier.Extended,
                        Visibility = BlockVisibility.Always, BodyJson = json, RowVersion = rowVersion,
                    },
                },
            },
        },
    };

    [Fact]
    public async Task Due_celle_scritte_in_fila_si_salvano_tutte_e_due_col_token_nuovo()
    {
        var iniziale = TabellaGenerica.Scrivi(new[] { "A", "B" }, new[] { new[] { "", "" } }, new int?[] { null, null });

        // Il tornello dell'host: un'operazione per volta, nell'ordine.
        var fila = new SemaphoreSlim(1, 1);
        IRenderedComponent<DocumentSectionsEditor> cut = null!;
        var versione = 1;
        cut = RenderComponent<DocumentSectionsEditor>(p => p
            .Add(x => x.Doc, Documento(iniziale, "rv1"))
            .Add(x => x.IsEditing, true)
            .Add(x => x.Profile, SectionProfile.App)
            .Add(x => x.IsMandatory, (Func<EditableSection, bool>)(_ => false))
            .Add(x => x.Run, (Func<Func<Task>, Task>)(async azione =>
            {
                await fila.WaitAsync();
                try { await azione(); } finally { fila.Release(); }
            }))
            // Il ricarico dell'host: il blocco torna con quel che il servizio ha scritto per ultimo e un token nuovo.
            .Add(x => x.OnChanged, EventCallback.Factory.Create(this, () =>
            {
                versione++;
                cut.SetParametersAndRender(q => q.Add(x => x.Doc, Documento(_svc.Scritture[^1].BodyJson!, $"rv{versione}")));
            })));

        Task primo = null!, secondo = null!;
        await cut.InvokeAsync(() => { primo = cut.FindAll("td input.app-in").First().ChangeAsync(new ChangeEventArgs { Value = "X" }); });
        await cut.InvokeAsync(() => { secondo = cut.FindAll("td input.app-in").Skip(1).First().ChangeAsync(new ChangeEventArgs { Value = "Y" }); });

        _svc.Primo.SetResult();
        await cut.InvokeAsync(() => Task.WhenAll(primo, secondo));

        Assert.Equal(2, _svc.Scritture.Count);
        var ultima = _svc.Scritture[1];
        Assert.Equal("rv2", ultima.RowVersion);
        var (_, righe) = TabellaGenerica.Leggi(ultima.BodyJson);
        Assert.Equal(new[] { "X", "Y" }, righe[0]);
    }
}
