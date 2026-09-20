using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain.Entities;
using Vipi.Domain;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La conversione dei testi già scritti dall'editor (§A73, slice 5): il pannello trova le SID scritte a mano e il
/// tasto le salva come riferimenti — UN salvataggio per blocco, anche quando nel blocco le proposte sono più
/// d'una (due salvataggi di fila porterebbero la stessa RowVersion, e il secondo andrebbe in conflitto).
/// </summary>
public class ConversioneSidEditorTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class EditingSpia : EditingServiceStub
    {
        public List<(int Id, BlockEdit Edit)> Salvati { get; } = new();
        public override Task UpdateBlockAsync(int blockId, BlockEdit edit, CancellationToken ct = default)
        {
            Salvati.Add((blockId, edit));
            return Task.CompletedTask;
        }
    }

    private sealed class SidDiLibv : IProcedureReferenceResolver
    {
        public Task<NomiProcedura> PerVistaAsync(IEnumerable<SectionView> sezioni, bool pubblica,
            string? proprioIcao = null, AirportSidView? propriaTabella = null,
            AirportSidView? propriaTabellaStar = null, CancellationToken ct = default) =>
            Task.FromResult(NomiProcedura.Vuoto);
        public Task<NomiProcedura> PerTestiAsync(IEnumerable<string?> testi, CancellationToken ct = default) =>
            Task.FromResult(NomiProcedura.Vuoto);
        public Task<IReadOnlyList<ProceduraCitabile>> ElencoAsync(string icao, ProcedureKind kind = ProcedureKind.Sid, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProceduraCitabile>>(icao == "LIBV"
                ? new[] { new ProceduraCitabile(ProcedureKind.Sid, "LIBV", "CDC6A", "CDC 6A", "14L, 14R"), new ProceduraCitabile(ProcedureKind.Sid, "LIBV", "CDC6B", "CDC 6B", "32L") }
                : Array.Empty<ProceduraCitabile>());
    }

    private readonly EditingSpia _spia = new();

    public ConversioneSidEditorTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddScoped<IEditingService>(_ => _spia);
        Services.AddScoped<IProcedureReferenceResolver, SidDiLibv>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static EditableBlock Prosa(int id, string testo) => new()
    {
        Id = id, Order = id, Format = BlockFormat.Prose, Tier = BlockTier.Extended,
        Visibility = BlockVisibility.Always, Body = testo, RowVersion = "rv" + id,
    };

    private static EditableDocument Documento() => new()
    {
        DocumentId = 1, VersionId = 2, VersionNumber = 3, VersionStatus = DocumentStatus.Draft,
        Title = "vSOP MIL LIBV", Language = Language.En,
        Sections = new[]
        {
            new EditableSection
            {
                Id = 10, Title = "Departures", SectionKey = "custom:0000000a", Depth = 0, Order = 1,
                Blocks = new[]
                {
                    Prosa(100, "RWY 14L/R: Expect CDC6A. RWY 32: Expect CDC6B."),
                    Prosa(101, "Joining via CDC6A/B."),
                },
                Children = Array.Empty<EditableSection>(),
            },
        },
    };

    private IRenderedComponent<DocumentSectionsEditor> Editor() =>
        RenderComponent<DocumentSectionsEditor>(p => p
            .AddCascadingValue("IcaoDelDocumento", "LIBV")
            .Add(x => x.Doc, Documento())
            .Add(x => x.IsEditing, true)
            .Add(x => x.Run, (Func<Func<Task>, Task>)(azione => azione())));

    private static void Apri(IRenderedComponent<DocumentSectionsEditor> c)
    {
        c.FindAll("button").First(b => b.TextContent.Contains("Sid_Conv_Open")).Click();
        c.WaitForAssertion(() => Assert.NotEmpty(c.FindAll(".sidref-conv-list li")));
    }

    [Fact]
    public void Il_pannello_propone_le_SID_e_elenca_le_forme_compatte()
    {
        var c = Editor();
        Apri(c);

        var testo = c.Find(".sidref-conv").TextContent;
        Assert.Contains("«CDC6A» → CDC 6A", testo);
        Assert.Contains("«CDC6B» → CDC 6B", testo);
        Assert.Contains("«CDC6A/B»", testo);        // da sistemare a mano
        Assert.Empty(_spia.Salvati);                 // niente si converte da solo
    }

    [Fact]
    public void Converti_tutte_salva_una_volta_sola_per_blocco()
    {
        var c = Editor();
        Apri(c);

        c.FindAll(".sidref-conv button").First(b => b.TextContent.Contains("Sid_Conv_All")).Click();

        var (id, edit) = Assert.Single(_spia.Salvati);
        Assert.Equal(100, id);
        Assert.Equal("RWY 14L/R: Expect [[SID LIBV CDC6A]]. RWY 32: Expect [[SID LIBV CDC6B]].", edit.Body);
        Assert.Equal("rv100", edit.RowVersion);
    }

    [Fact]
    public void Converti_una_voce_tocca_solo_quella()
    {
        var c = Editor();
        Apri(c);

        var riga = c.FindAll(".sidref-conv-list li").First(li => li.TextContent.Contains("«CDC6B»"));
        riga.QuerySelector("button")!.Click();

        var (_, edit) = Assert.Single(_spia.Salvati);
        Assert.Equal("RWY 14L/R: Expect CDC6A. RWY 32: Expect [[SID LIBV CDC6B]].", edit.Body);
    }
}
