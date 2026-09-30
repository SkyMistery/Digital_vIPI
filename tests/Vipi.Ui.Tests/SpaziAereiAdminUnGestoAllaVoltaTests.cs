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
/// 🔴 U-049 (revisione totale 3): la pagina admin degli spazi aerei prende i servizi dal circuito, e i suoi gesti
/// («Aggancia», «Confronta», «Sgancia», «Metti in vigore», «Elimina») alzavano <c>_busy</c> senza mai guardarlo in
/// ingresso, con un <c>finally</c> e basta. Un doppio clic = due scritture sullo stesso DbContext; un guasto del
/// database = eccezione fuori dal gestore. In tutti e due i casi il circuito cadeva.
/// </summary>
public class SpaziAereiAdminUnGestoAllaVoltaTests : TestContext
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
        public string? CurrentName => "Chi carica";
        public void EnsureAdmin() { }
    }

    /// <summary>Un servizio che nessuno deve chiamare in questi test.</summary>
    public class Nessuno : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? m, object?[]? a) =>
            throw new NotSupportedException($"{m?.Name} non doveva essere chiamato.");
    }

    private sealed class CatalogoFinto : IAirspaceCatalog
    {
        public int MesseInVigore { get; private set; }
        public TaskCompletionSource? Trattieni { get; set; }
        public Exception? Lancia { get; set; }

        private static AirspaceImportRow Riga(int id, bool corrente) =>
            new(id, $"italia-{id}.kmz", "abc", 1024, "2609", null, DateTime.UtcNow, "Tizio", 10, 10, 0, 100, corrente);

        public Task<IReadOnlyList<AirspaceImportRow>> ListImportsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AirspaceImportRow>>(new[] { Riga(1, true), Riga(2, false), Riga(3, false) });

        public async Task SetCurrentAsync(int importId, CancellationToken ct = default)
        {
            MesseInVigore++;
            if (Trattieni is not null) await Trattieni.Task;
            if (Lancia is not null) throw Lancia;
        }

        public Task<IReadOnlyList<AirspaceVolumeRow>> ListVolumesAsync(AirspaceVolumeQuery query, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AirspaceVolumeRow>>(Array.Empty<AirspaceVolumeRow>());
        public Task<IReadOnlyDictionary<AirspaceFamily, int>> CountByFamilyAsync(int? importId = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<AirspaceFamily, int>>(new Dictionary<AirspaceFamily, int>());
        public Task<IReadOnlyList<AirspaceIssue>> GetIssuesAsync(int importId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AirspaceIssue>>(Array.Empty<AirspaceIssue>());

        public Task<AirspaceImportRow?> GetCurrentAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AirspaceImportRow> SaveAsync(NewAirspaceImport nuovo, AirspaceReadResult letto, DateTime quando, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<AirspaceVolumeRow>> GetVolumesAsync(IReadOnlyList<int> ids, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<(string FileName, byte[] Content)?> GetFileAsync(int importId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task DeleteAsync(int importId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Vipi.Application.Airspace.AirspaceCorrectionRow>> ListCorrectionsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Vipi.Application.Airspace.AirspaceCorrectionRow>>([]);
        public Task<IReadOnlyList<Vipi.Application.Airspace.AirspaceCorrectionFinding>> ReviewCorrectionsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Vipi.Application.Airspace.AirspaceCorrectionFinding>>([]);
        public Task CorrectAsync(Vipi.Application.Airspace.AirspaceVolumeKey volume, Vipi.Application.Airspace.AirspaceCorrectionInput input,
            int? userId, string? userName, DateTime nowUtc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RemoveCorrectionAsync(int correctionId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AcknowledgeCorrectionAsync(int correctionId, int? userId, string? userName, DateTime nowUtc,
            CancellationToken ct = default) => throw new NotSupportedException();
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
        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll("button"), b => b.TextContent.Contains("Asp_SetCurrent")));
        return cut;
    }

    private static AngleSharp.Dom.IElement MettiInVigore(IRenderedComponent<AdminAirspacePage> cut, int riga = 0) =>
        cut.FindAll("button").Where(b => b.TextContent.Trim() == "Asp_SetCurrent").ElementAt(riga);

    [Fact]
    public async Task Il_doppio_clic_su_Metti_in_vigore_scrive_una_volta_e_non_fa_cadere_il_circuito()
    {
        _catalogo.Trattieni = new TaskCompletionSource();
        var cut = Apri();

        // Due righe e non due clic sullo stesso tasto: bUnit smaltisce subito i gestori di un elemento ridisegnato, il
        // server no finché il browser non conferma. Il secondo gesto arriva con il primo ancora in volo.
        var primo = MettiInVigore(cut, 0).ClickAsync(new());
        var secondo = MettiInVigore(cut, 1).ClickAsync(new());
        _catalogo.Trattieni.SetResult();
        await Task.WhenAll(primo, secondo);

        var caduta = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(300));
        if (caduta == Renderer.UnhandledException) Assert.Fail("Circuito caduto: " + await Renderer.UnhandledException);
        Assert.Equal(1, _catalogo.MesseInVigore);
    }

    [Fact]
    public async Task Un_guasto_del_database_resta_un_messaggio()
    {
        _catalogo.Lancia = new InvalidOperationException("database giù");
        var cut = Apri();

        await MettiInVigore(cut).ClickAsync(new());

        var caduta = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(300));
        if (caduta == Renderer.UnhandledException) Assert.Fail("Circuito caduto: " + await Renderer.UnhandledException);
        cut.WaitForAssertion(() => Assert.Contains("database giù", cut.Markup));
    }
}
