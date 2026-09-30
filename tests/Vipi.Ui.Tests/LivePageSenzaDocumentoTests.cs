using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Live;
using Vipi.Application.Routing;
using Vipi.Domain;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// U-208 (revisione 3, S38): postazione senza documento e senza indirizzo del documento esteso. La frase
/// «apri la vIPI» riceveva <c>href="#"</c>, che Blazor risolve contro <c>&lt;base href="/"&gt;</c>: il clic
/// portava il controllore fuori dalla vista live, all'hub.
/// </summary>
public class LivePageSenzaDocumentoTests : TestContext
{
    private sealed class TorreSenzaDocumento : ILiveViewService
    {
        public string? MyCallsign() => "LIRF_TWR";
        public OnlineAtcSnapshot Snapshot() => OnlineAtcSnapshot.Empty;

        public Task<LiveViewResult> BuildAsync(string callsign, CancellationToken ct = default) =>
            Task.FromResult(new LiveViewResult(new LiveView
            {
                Callsign = "LIRF_TWR",
                Title = "Roma Torre",
                AccCode = "LIRR",
                Type = LiveStationType.Tower,
                NoDocument = true,
                ExtendedDoc = null,
            }, callsign));

        public Task<IReadOnlyList<LiveStationOption>> ListStationsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LiveStationOption>>(Array.Empty<LiveStationOption>());
    }

    private sealed class Chi(VipiRole ruolo) : IEditAuthorizationService
    {
        public VipiRole Role => ruolo;
        public bool IsAdmin => false;
        public int? CurrentUserId => null;
        public string? CurrentName => null;
    }

    private sealed class UtenteNormale : IEditAuthorizationService
    {
        public VipiRole Role => VipiRole.User;
        public bool IsAdmin => false;
        public int? CurrentUserId => null;
        public string? CurrentName => null;
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + string.Concat(arguments.Select(a => " " + a)), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    [Fact]
    public void Senza_indirizzo_la_frase_non_porta_un_collegamento_a_cancelletto()
    {
        Services.AddSingleton<ILiveViewService>(new TorreSenzaDocumento());
        Services.AddSingleton<IDocRoutesRegistry>(new DocRoutesRegistry(Array.Empty<IDocKindRoutes>()));
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<IEditAuthorizationService>(new UtenteNormale());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddSingleton(new EnglishStrings());
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<LivePage>(p => p.Add(x => x.Callsign, "LIRF_TWR"));

        var avviso = cut.Find(".callout.warning");
        Assert.Contains("Live_NoDocBodyNoLink", avviso.TextContent);
        Assert.Empty(avviso.QuerySelectorAll("a"));
        Assert.Empty(cut.FindAll("a[href='#']"));
    }

    private IRenderedComponent<LivePage> Apri(VipiRole ruolo)
    {
        Services.AddSingleton<ILiveViewService>(new TorreSenzaDocumento());
        Services.AddSingleton<IDocRoutesRegistry>(new DocRoutesRegistry(Array.Empty<IDocKindRoutes>()));
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<IEditAuthorizationService>(new Chi(ruolo));
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddSingleton(new EnglishStrings());
        JSInterop.Mode = JSRuntimeMode.Loose;
        return RenderComponent<LivePage>(p => p.Add(x => x.Callsign, "LIRF_TWR"));
    }

    /// <summary>
    /// Committente, 30 settembre 2026: la finestra dei trasferimenti è in sviluppo. Chi non è staff vede la riga col
    /// titolo e l'etichetta «In sviluppo», ma non la può aprire: nel markup non c'è né il &lt;details&gt; né il contenuto.
    /// </summary>
    [Fact]
    public void Chi_non_e_staff_non_apre_i_trasferimenti_e_legge_che_sono_in_sviluppo()
    {
        var cut = Apri(VipiRole.User);
        Assert.Empty(cut.FindAll("details[data-persist='live-xfer']"));
        var riga = cut.Find(".acc-chiuso");
        Assert.Contains("Live_TransfersTitle", riga.TextContent);
        Assert.Contains("Live_InDevelopment", riga.TextContent);
    }

    [Fact]
    public void Lo_staff_di_divisione_apre_i_trasferimenti()
    {
        var cut = Apri(VipiRole.DivisionStaff);
        Assert.Single(cut.FindAll("details[data-persist='live-xfer']"));
        Assert.Empty(cut.FindAll(".acc-chiuso"));
    }
}
