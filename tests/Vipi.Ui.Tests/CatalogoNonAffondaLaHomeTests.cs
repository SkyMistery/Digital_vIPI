using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Services;
using Vipi.Ui;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// L'ingresso del sito (<c>/services/vsop</c>) non muore se l'elenco delle ACC non si legge.
///
/// <para>🔴 <b>Perché esiste questo test.</b> Il 31 agosto 2026 la home ha risposto 500 — la pagina «This
/// page did not open», codice <c>00-c4cd7224…</c> delle 11:40:17 UTC — e la riga colpevole era una sola,
/// nuda: <c>protected override void OnInitialized() =&gt; Stations.Prewarm();</c>. Cioè una lettura del
/// database messa nel ciclo di vita senza rete sotto.</para>
///
/// <para>⚠️ <b>La parte istruttiva è che il rimedio c'era già, e stava un piano sopra.</b> La barra
/// (<c>SopLayout.LeggiCatalogo</c>) quella lettura la protegge dal 24 agosto: se fallisce, ingoia, scrive un
/// avviso e la barra esce senza i collegamenti alle ACC. Ma ingoiare lascia la cache del resolver
/// <b>vuota</b>, e la pagina sotto ritenta la stessa lettura sullo stesso contesto rotto — dove rete non ce
/// n'era. Il presidio della barra proteggeva la barra e non ciò che le sta dentro.</para>
///
/// <para>La regola, che è la stessa di <c>barra-non-affonda-la-pagina</c>: <b>il contorno si spegne, il
/// contenuto esce</b>. Un elenco di collegamenti non decide se una pagina esiste.</para>
/// </summary>
public class CatalogoNonAffondaLaHomeTests : TestContext
{
    /// <summary>Il resolver che non riesce a leggere: è l'intoppo del database, visto da dentro la pagina.</summary>
    private sealed class ResolverRotto : IStationResolver
    {
        public int Tentativi { get; private set; }

        private Exception Rompi()
        {
            Tentativi++;
            // È letteralmente l'eccezione del 31 agosto 2026, quella di MySqlConnector.
            return new InvalidOperationException("Cannot Open when State is Connecting.");
        }

        public IReadOnlyList<AccInfo> Accs => throw Rompi();
        public AccInfo? Resolve(string accCode) => throw Rompi();
        public AccInfo? ResolveByCallsign(string callsign) => throw Rompi();
        public AirportStation? Airport(string? icao) => throw Rompi();
        public AirportStation? AirportOfCallsign(string? callsign) => throw Rompi();
        public void Prewarm() => throw Rompi();
    }

    private sealed class ResolverSano : IStationResolver
    {
        private readonly List<AccInfo> _accs =
            new() { new AccInfo("LIBB", "Brindisi"), new AccInfo("LIRR", "Roma") };

        public IReadOnlyList<AccInfo> Accs => _accs;
        public AccInfo? Resolve(string accCode) =>
            _accs.FirstOrDefault(a => a.Code.Equals(accCode, StringComparison.OrdinalIgnoreCase));
        public AccInfo? ResolveByCallsign(string callsign) =>
            Resolve(callsign.Contains('_') ? callsign[..callsign.IndexOf('_')] : callsign);
        public AirportStation? Airport(string? icao) => null;
        public AirportStation? AirportOfCallsign(string? callsign) => null;
        public void Prewarm() { }
    }

    private sealed class OnlineFinto : IOnlineAtcProvider
    {
        public OnlineAtcSnapshot GetCurrent() => new()
        {
            Callsigns = new HashSet<string>(new[] { "LIBB_ES_CTR" }, StringComparer.OrdinalIgnoreCase),
            Details = new List<OnlineAtc> { new("LIBB_ES_CTR", 704798, "Tizio", 5) },
            AsOf = DateTimeOffset.UtcNow,
        };
    }

    private sealed class AuthzFinto : IEditAuthorizationService
    {
        public VipiRole Role => VipiRole.User;
        public bool IsAdmin => false;
        public int? CurrentUserId => null;
        public string? CurrentName => null;
        public void EnsureAdmin() { }
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + string.Concat(arguments.Select(a => " " + a)), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            Enumerable.Empty<LocalizedString>();
    }

    private void Arrangia(IStationResolver stations)
    {
        Services.AddLogging();
        Services.AddSingleton<IAiracService>(new AiracService());
        Services.AddSingleton(stations);
        Services.AddSingleton<IEditAuthorizationService>(new AuthzFinto());
        Services.AddSingleton<IOnlineAtcProvider>(new OnlineFinto());
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton(new EnglishStrings());
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>
    /// Il caso del guasto: il catalogo lancia, e la pagina esce lo stesso — con l'avviso al posto delle
    /// schede ACC. Senza la rete questo <c>RenderComponent</c> lancia, che a schermo è il 500.
    /// </summary>
    [Fact]
    public void Se_il_catalogo_non_si_legge_la_home_esce_lo_stesso()
    {
        var rotto = new ResolverRotto();
        Arrangia(rotto);

        var cut = RenderComponent<SopHome>();

        Assert.Contains("Home_CatalogDownTitle", cut.Markup);
        Assert.Empty(cut.FindAll(".acc-card"));

        // Il resto della pagina — gli strumenti, che col catalogo non c'entrano — è tutto lì.
        var indirizzi = cut.FindAll("a.choice").Select(a => a.GetAttribute("href")).ToList();
        Assert.Contains("/services/vsop/mil", indirizzi);
        Assert.Contains("/services/stats", indirizzi);
    }

    /// <summary>
    /// ⚠️ E l'avviso deve dire la cosa GIUSTA. Il vuoto da intoppo e il vuoto da archivio senza ACC si
    /// somigliano a schermo e non sono la stessa notizia: «il database è vuoto» detto a chi ha solo avuto un
    /// singhiozzo manda a cercare un guasto che non c'è.
    /// </summary>
    [Fact]
    public void Il_catalogo_illeggibile_non_si_racconta_come_archivio_vuoto()
    {
        Arrangia(new ResolverRotto());

        var cut = RenderComponent<SopHome>();

        Assert.DoesNotContain("Home_DbEmpty", cut.Markup);
        Assert.DoesNotContain("Home_NoAccTitle", cut.Markup);
    }

    /// <summary>Con il catalogo sano non cambia niente: le schede ci sono e i conteggi pure.</summary>
    [Fact]
    public void Con_il_catalogo_sano_le_schede_ci_sono()
    {
        Arrangia(new ResolverSano());

        var cut = RenderComponent<SopHome>();

        Assert.DoesNotContain("Home_CatalogDownTitle", cut.Markup);
        Assert.Equal(2, cut.FindAll(".acc-card").Count);
        Assert.Single(cut.FindAll(".acc-card.on"));   // LIBB ha un ATC collegato, LIRR no
    }
}
