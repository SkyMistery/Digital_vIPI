using System.Reflection;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Airspace;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Le correzioni a mano sulla pagina admin degli spazi aerei (carta docs/feature/2026-09-30-correzioni-spazi-aerei.md):
/// la matita apre il modulo sui valori che si vedono, «Salva» manda quel che c'è scritto, la pastiglia dice che il
/// volume è corretto, e il blocco «Da controllare» c'è solo quando serve e i suoi tasti arrivano al catalogo.
/// </summary>
public class CorrezioniSpaziAereiPaginaTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class Editor : IEditAuthorizationService
    {
        public VipiRole Role => VipiRole.Editor;
        public bool IsAdmin => false;
        public int? CurrentUserId => 704798;
        public string? CurrentName => "Chi corregge";
        public void EnsureAdmin() { }
    }

    public class Nessuno : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? m, object?[]? a) =>
            throw new NotSupportedException($"{m?.Name} non doveva essere chiamato.");
    }

    private static AirspaceVolumeRow Volume(string nome, int id, bool corretto = false) =>
        new(id, 1, AirspaceFamily.Ctr, nome, "Control Traffic Region", "D",
            AirspaceDatum.Gnd, 0, "GND", AirspaceDatum.Amsl, 2500, "2500 FT AMSL",
            "[[9,45],[9.5,45],[9.25,45.5]]", 1, 3, $"CTR|{nome}|GND|2500 FT AMSL", 0, 45, 9, 45.5, 9.5, corretto);

    private static readonly AirspaceCorrectionRow Correzione = new(
        5, "CTR|PROVA CTR|GND|2500 FT AMSL", 0, "PROVA CTR", null, false, null, null, "3500 FT AMSL",
        AirspaceFamily.Ctr, "D", "GND", "2500 FT AMSL", DateTime.UtcNow, "Tizio");

    private sealed class CatalogoFinto : IAirspaceCatalog
    {
        public List<AirspaceCorrectionFinding> Revisione { get; } = new();
        public List<(AirspaceVolumeKey Chiave, AirspaceCorrectionInput Input)> Corrette { get; } = new();
        public List<int> Confermate { get; } = new();
        public List<int> Tolte { get; } = new();

        public Task<IReadOnlyList<AirspaceImportRow>> ListImportsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AirspaceImportRow>>(new[]
            {
                new AirspaceImportRow(1, "it.kmz", "abc", 1024, "2610", null, DateTime.UtcNow, "Tizio", 2, 2, 0, 6, true),
            });
        public Task<IReadOnlyList<AirspaceVolumeRow>> ListVolumesAsync(AirspaceVolumeQuery query, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AirspaceVolumeRow>>(new[] { Volume("ALTRO CTR", 1), Volume("PROVA CTR", 2, corretto: true) });
        public Task<IReadOnlyDictionary<AirspaceFamily, int>> CountByFamilyAsync(int? importId = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<AirspaceFamily, int>>(new Dictionary<AirspaceFamily, int> { [AirspaceFamily.Ctr] = 2 });
        public Task<IReadOnlyList<AirspaceIssue>> GetIssuesAsync(int importId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AirspaceIssue>>(Array.Empty<AirspaceIssue>());
        public Task<IReadOnlyList<AirspaceCorrectionRow>> ListCorrectionsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AirspaceCorrectionRow>>(new[] { Correzione });
        public Task<IReadOnlyList<AirspaceCorrectionFinding>> ReviewCorrectionsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AirspaceCorrectionFinding>>(Revisione.ToList());

        public Task CorrectAsync(AirspaceVolumeKey volume, AirspaceCorrectionInput input, int? userId, string? userName,
            DateTime nowUtc, CancellationToken ct = default)
        {
            Corrette.Add((volume, input));
            return Task.CompletedTask;
        }

        public Task RemoveCorrectionAsync(int correctionId, CancellationToken ct = default)
        {
            Tolte.Add(correctionId);
            Revisione.Clear();
            return Task.CompletedTask;
        }

        public Task AcknowledgeCorrectionAsync(int correctionId, int? userId, string? userName, DateTime nowUtc,
            CancellationToken ct = default)
        {
            Confermate.Add(correctionId);
            Revisione.Clear();
            return Task.CompletedTask;
        }

        public Task<AirspaceImportRow?> GetCurrentAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AirspaceImportRow> SaveAsync(NewAirspaceImport nuovo, AirspaceReadResult letto, DateTime quando,
            CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AirspaceVolumeRow>> GetVolumesAsync(IReadOnlyList<int> ids, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<(string FileName, byte[] Content)?> GetFileAsync(int importId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task SetCurrentAsync(int importId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(int importId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class AgganciFinti : ISectorAirspaceBindings
    {
        public Task<IReadOnlyList<SectorAirspaceBindingRow>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SectorAirspaceBindingRow>>(Array.Empty<SectorAirspaceBindingRow>());
        public Task<IReadOnlyDictionary<string, SectorAirspaceBindingRow>> ResolveAsync(
            IReadOnlyList<string> callsigns, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetAsync(SourceCatalog catalog, int sectorId, string callsign, IReadOnlyList<AirspaceVolumeKey> volumes,
            int? userId, string? userName, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FormeFinte : ISectorShapeRepository
    {
        public Task<IReadOnlyList<SectorShapeRow>> ListShapeCandidatesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SectorShapeRow>>(Array.Empty<SectorShapeRow>());
        public Task ApplyShapeAsync(ShapeWrite write, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> PromoteDueShapesAsync(DateTime nowUtc, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private readonly CatalogoFinto _catalogo = new();

    private IRenderedComponent<AdminAirspacePage> Apri()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddSingleton<IEditAuthorizationService>(new Editor());
        Services.AddSingleton<IAirspaceCatalog>(_catalogo);
        Services.AddSingleton<ISectorAirspaceBindings>(new AgganciFinti());
        Services.AddSingleton<ISectorShapeRepository>(new FormeFinte());
        Services.AddSingleton(DispatchProxy.Create<INavaidCatalog, Nessuno>());
        Services.AddSingleton(DispatchProxy.Create<INeighbourImportService, Nessuno>());
        var cut = RenderComponent<AdminAirspacePage>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("td.c-fix button").Count));
        return cut;
    }

    private static AirspaceCorrectionFinding Cambiato() => new(
        AirspaceCorrectionFindingKind.FileChanged, Correzione, Volume("PROVA CTR", 9), true,
        new[] { new AirspaceCorrectionDiff(AirspaceCorrectionField.Top, "2500 FT AMSL", "3000 FT AMSL", "3500 FT AMSL") });

    [Fact]
    public void Il_volume_corretto_porta_la_pastiglia_con_quel_che_diceva_il_file()
    {
        var cut = Apri();

        var pastiglia = Assert.Single(cut.FindAll(".asp-fixed"));
        Assert.Contains("Asp_Fix_FromFile", pastiglia.GetAttribute("title"));
        Assert.Empty(cut.FindAll(".asp-review"));   // niente da controllare: il blocco non c'è
    }

    [Fact]
    public async Task La_matita_apre_il_modulo_sui_valori_che_si_vedono_e_Salva_li_manda()
    {
        var cut = Apri();

        await cut.InvokeAsync(() => cut.FindAll("td.c-fix button").First().Click());   // ALTRO CTR
        var campi = cut.FindAll(".asp-edit input");
        Assert.Equal(new[] { "D", "GND", "2500 FT AMSL" }, campi.Select(c => c.GetAttribute("value")).ToArray());

        await cut.InvokeAsync(() => cut.FindAll(".asp-edit input").ElementAt(2).Input("FL95"));
        await cut.InvokeAsync(() => cut.FindAll(".asp-edit button").First(b => b.TextContent.Contains("Common_Save")).Click());

        var (chiave, input) = Assert.Single(_catalogo.Corrette);
        Assert.Equal("CTR|ALTRO CTR|GND|2500 FT AMSL", chiave.Key);
        Assert.Equal(new AirspaceCorrectionInput(AirspaceFamily.Ctr, "D", "GND", "FL95"), input);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".asp-edit")));
    }

    [Fact]
    public async Task Da_controllare_mostra_la_segnalazione_e_Va_bene_la_conferma()
    {
        _catalogo.Revisione.Add(Cambiato());
        var cut = Apri();

        var riga = Assert.Single(cut.FindAll(".asp-review tbody tr"));
        Assert.Contains("Asp_Fix_Kind_FileChanged", riga.TextContent);
        Assert.Contains("Asp_Fix_KeyChanged", riga.TextContent);
        Assert.Contains("Asp_Fix_Field_Top", riga.TextContent);

        await cut.InvokeAsync(() => cut.FindAll(".asp-review button").First(b => b.TextContent.Contains("Asp_Fix_Ok")).Click());

        Assert.Equal(new[] { 5 }, _catalogo.Confermate);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".asp-review")));
    }

    [Fact]
    public async Task Prendi_il_file_toglie_la_correzione()
    {
        _catalogo.Revisione.Add(Cambiato());
        var cut = Apri();

        await cut.InvokeAsync(() => cut.FindAll(".asp-review button").First(b => b.TextContent.Contains("Asp_Fix_TakeFile")).Click());

        Assert.Equal(new[] { 5 }, _catalogo.Tolte);
        Assert.Empty(_catalogo.Confermate);
    }

    [Fact]
    public void Se_il_file_dice_gia_cosi_non_c_e_Prendi_il_file()
    {
        _catalogo.Revisione.Add(Cambiato() with { Kind = AirspaceCorrectionFindingKind.FileAgrees });
        var cut = Apri();

        Assert.DoesNotContain(cut.FindAll(".asp-review button"), b => b.TextContent.Contains("Asp_Fix_TakeFile"));
        Assert.Contains(cut.FindAll(".asp-review button"), b => b.TextContent.Contains("Asp_Fix_Ok"));
    }
}
