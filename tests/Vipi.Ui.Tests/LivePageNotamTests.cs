using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Application.Live;
using Vipi.Application.Routing;
using Vipi.Domain;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// I NOTAM nella vista live (committente, 12 ottobre 2026; carta
/// <c>docs/feature/2026-10-12-notam-sui-documenti.md</c> §6): una chip rossa «! NOTAM» in testata, fra il nome della
/// postazione e il campo di ricerca, che c'è solo se un NOTAM riguarda la postazione e apre in fondo alla pagina lo
/// stesso riquadro dei documenti.
/// </summary>
public class LivePageNotamTests : TestContext
{
    private static readonly NotamAvviso Firenze = new(
        4845, "IT-B002/25", new DateTime(2025, 11, 27, 0, 0, 0, DateTimeKind.Utc),
        new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        "A) LIMM LIRR\nE) FIRENZE CTR FIRENZE CTA. APPROACH CONTROL CHG AS FOLLOWS:", false,
        new[] { "LIRQ" }, new[] { "LIRR" });

    private sealed class Torre(params NotamAvviso[] notam) : ILiveViewService
    {
        public string? MyCallsign() => "LIRQ_TWR";
        public OnlineAtcSnapshot Snapshot() => OnlineAtcSnapshot.Empty;

        public Task<LiveViewResult> BuildAsync(string callsign, CancellationToken ct = default) =>
            Task.FromResult(new LiveViewResult(new LiveView
            {
                Callsign = "LIRQ_TWR",
                Title = "Firenze Torre",
                AccCode = "LIRR",
                Type = LiveStationType.Tower,
                Notam = notam,
            }, callsign));

        public Task<IReadOnlyList<LiveStationOption>> ListStationsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LiveStationOption>>(Array.Empty<LiveStationOption>());
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

    private IRenderedComponent<LivePage> Apri(params NotamAvviso[] notam)
    {
        Services.AddSingleton<ILiveViewService>(new Torre(notam));
        Services.AddSingleton<IDocRoutesRegistry>(new DocRoutesRegistry(Array.Empty<IDocKindRoutes>()));
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<IEditAuthorizationService>(new UtenteNormale());
        Services.AddSingleton<StringheDelSito>();
        Services.AddSingleton(new EnglishStrings());
        JSInterop.Mode = JSRuntimeMode.Loose;
        return RenderComponent<LivePage>(p => p.Add(x => x.Callsign, "LIRQ_TWR"));
    }

    /// <summary>Nessun NOTAM per la postazione: niente chip e niente pannello. È il caso di quasi tutte le
    /// postazioni, quasi sempre — e la testata resta com'era.</summary>
    [Fact]
    public void Senza_NOTAM_la_chip_non_c_e()
    {
        var cut = Apri();

        Assert.Empty(cut.FindAll(".notam-chip"));
        Assert.Empty(cut.FindAll("#live-notam"));
    }

    /// <summary>La chip dice «! NOTAM», col punto esclamativo in un cerchio suo, e sta nella testata DOPO il nome
    /// della postazione — dove il committente ha messo la freccia.</summary>
    [Fact]
    public void Con_un_NOTAM_la_chip_sta_in_testata_dopo_la_postazione()
    {
        var cut = Apri(Firenze);

        var chip = cut.Find(".live-top .notam-chip");
        Assert.Equal("!", chip.QuerySelector(".nc-bang")!.TextContent);
        Assert.Equal("! NOTAM", chip.TextContent.Trim());
        Assert.Equal("false", chip.GetAttribute("aria-expanded"));
        Assert.Contains("Live_NotamChipTitle 1", chip.GetAttribute("title"));

        // L'ordine nella riga: titolo, stato della postazione, chip.
        var figli = cut.Find(".live-top").Children.ToList();
        var stato = figli.FindIndex(e => e.ClassList.Contains("live-badge"));
        Assert.InRange(stato, 0, figli.Count - 1);
        Assert.Equal(stato + 1, figli.FindIndex(e => e.ClassList.Contains("notam-chip")));
    }

    /// <summary>Chiusa, il pannello non è nel markup: non un riquadro nascosto col CSS.</summary>
    [Fact]
    public void Finche_non_si_preme_il_pannello_non_c_e()
    {
        var cut = Apri(Firenze);

        Assert.Empty(cut.FindAll("#live-notam"));
        Assert.Empty(cut.FindAll(".notam-box"));
    }

    /// <summary>Premuta, apre in fondo il riquadro dei documenti, coi NOTAM già aperti, e lo porta in vista;
    /// premuta di nuovo lo chiude.</summary>
    [Fact]
    public void La_chip_apre_e_chiude_il_riquadro_dei_documenti()
    {
        var cut = Apri(Firenze);

        cut.Find(".notam-chip").Click();

        var pannello = cut.Find("#live-notam");
        Assert.Equal("IT-B002/25", pannello.QuerySelector(".notam-num")!.TextContent);
        Assert.True(pannello.QuerySelector("details.notam")!.HasAttribute("open"));
        Assert.Contains("FIRENZE CTR FIRENZE CTA", pannello.QuerySelector("pre.notam-text")!.TextContent);
        Assert.Equal("true", cut.Find(".notam-chip").GetAttribute("aria-expanded"));
        Assert.Contains("on", cut.Find(".notam-chip").ClassList);
        // In fondo alla pagina: l'ultimo blocco della vista.
        Assert.Equal("live-notam", cut.Find(".wrap.live").LastElementChild!.Id);
        // E portato in vista, una volta: chi ha premuto in testata non se lo va a cercare.
        var scorri = Assert.Single(JSInterop.Invocations, i => i.Identifier == "vipiScrollToId");
        Assert.Equal("live-notam", scorri.Arguments[0]);

        cut.Find(".notam-chip").Click();

        Assert.Empty(cut.FindAll("#live-notam"));
        Assert.Equal("false", cut.Find(".notam-chip").GetAttribute("aria-expanded"));
        Assert.Single(JSInterop.Invocations, i => i.Identifier == "vipiScrollToId");
    }
}
