using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// <see cref="LiveBadge"/> è il badge «connesso come …» della topbar. Prima viveva nel layout, che è SSR
/// statico: leggeva la fotografia ATC una volta e cambiava solo ricaricando la pagina. Ora è un'isola
/// interattiva che si ridisegna quando il transport SSE segnala un aggiornamento della cache.
/// Questi test coprono proprio quel passaggio, che dal vivo non si può guidare (richiederebbe di far
/// cambiare stato a IVAO): il round-trip del callback lo verifica il driver del browser.
/// </summary>
public class LiveBadgeTests : TestContext
{
    private sealed class FakeOnline : IOnlineAtcProvider
    {
        public OnlineAtcSnapshot Current { get; set; } = OnlineAtcSnapshot.Empty;
        public OnlineAtcSnapshot GetCurrent() => Current;

        public void SetOnline(params (string Callsign, int UserId)[] atcs) => Current = new OnlineAtcSnapshot
        {
            Callsigns = new HashSet<string>(atcs.Select(a => a.Callsign), StringComparer.OrdinalIgnoreCase),
            Details = atcs.Select(a => new OnlineAtc(a.Callsign, a.UserId, "Tizio", 5)).ToList(),
            AsOf = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>Localizer che rende la chiave stessa: le asserzioni restano stabili al variare delle traduzioni.</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + string.Concat(arguments.Select(a => " " + a)), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            Enumerable.Empty<LocalizedString>();
    }

    private FakeOnline Arrange()
    {
        var online = new FakeOnline();
        Services.AddSingleton<IOnlineAtcProvider>(online);
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        JSInterop.Mode = JSRuntimeMode.Loose;   // vipiLive.subscribe/unsubscribe: qui non c'è browser
        return online;
    }

    [Fact]
    public void Badge_shows_the_callsign_of_the_logged_user_when_online()
    {
        var online = Arrange();
        online.SetOnline(("LIBB_ES_CTR", 704798), ("LIRR_CTR", 111111));

        var cut = RenderComponent<LiveBadge>(p => p.Add(x => x.UserId, 704798));

        Assert.Contains("LIBB_ES_CTR", cut.Markup);
        Assert.Empty(cut.FindAll(".live-badge.off"));
    }

    [Fact]
    public void Badge_is_off_when_the_user_is_not_online_or_not_logged_in()
    {
        var online = Arrange();
        online.SetOnline(("LIRR_CTR", 111111));   // online sì, ma non è il nostro VID

        Assert.NotNull(RenderComponent<LiveBadge>(p => p.Add(x => x.UserId, 704798)).Find(".live-badge.off"));
        Assert.NotNull(RenderComponent<LiveBadge>(p => p.Add(x => x.UserId, (int?)null)).Find(".live-badge.off"));
    }

    /// <summary>Il badge porta alla vista live in ENTRAMBI gli stati: da disconnesso è il solo modo per
    /// arrivarci dal chrome, ed è proprio quando serve (la pagina spiega perché non risulti connesso).</summary>
    [Fact]
    public void Badge_links_to_the_live_view_connected_or_not()
    {
        var online = Arrange();

        var off = RenderComponent<LiveBadge>(p => p.Add(x => x.UserId, 704798));
        Assert.Equal("/services/vsop/live", off.Find("a.live-badge.off").GetAttribute("href"));

        online.SetOnline(("LIBB_ES_CTR", 704798));
        var on = RenderComponent<LiveBadge>(p => p.Add(x => x.UserId, 704798));
        Assert.Equal("/services/vsop/live", on.Find("a.live-badge").GetAttribute("href"));
    }

    [Fact]
    public async Task Badge_switches_to_connected_on_a_live_update_without_reload()
    {
        var online = Arrange();
        var cut = RenderComponent<LiveBadge>(p => p.Add(x => x.UserId, 704798));
        Assert.NotNull(cut.Find(".live-badge.off"));   // parte da disconnesso

        // Ti colleghi in frequenza: il poller aggiorna la cache e la SSE sveglia il componente.
        online.SetOnline(("LIBB_ES_CTR", 704798));
        await cut.InvokeAsync(() => cut.Instance.OnLiveUpdate());

        Assert.Contains("LIBB_ES_CTR", cut.Markup);
        Assert.Empty(cut.FindAll(".live-badge.off"));
    }

    [Fact]
    public async Task Badge_switches_to_disconnected_when_you_leave_the_frequency()
    {
        var online = Arrange();
        online.SetOnline(("LIBB_ES_CTR", 704798));
        var cut = RenderComponent<LiveBadge>(p => p.Add(x => x.UserId, 704798));
        Assert.Contains("LIBB_ES_CTR", cut.Markup);

        online.SetOnline();   // ti sei scollegato
        await cut.InvokeAsync(() => cut.Instance.OnLiveUpdate());

        Assert.NotNull(cut.Find(".live-badge.off"));
        Assert.DoesNotContain("LIBB_ES_CTR", cut.Markup);
    }
}
