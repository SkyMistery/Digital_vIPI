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
/// postazione e il campo di ricerca, che c'è solo se un NOTAM riguarda la postazione; e in fondo alla pagina una
/// sezione NOTAM che c'è sempre, chiusa, con dentro lo stesso riquadro dei documenti. La chip la apre e ci porta.
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

    /// <summary>Nessun NOTAM per la postazione: niente chip. È il caso di quasi tutte le postazioni, quasi sempre —
    /// e la testata resta com'era. La sezione in fondo invece c'è lo stesso, e lo dice.</summary>
    [Fact]
    public void Senza_NOTAM_la_chip_non_c_e_e_la_sezione_dice_che_non_ce_ne_sono()
    {
        var cut = Apri();

        Assert.Empty(cut.FindAll(".notam-chip"));
        var sezione = cut.Find("details#live-notam");
        Assert.Equal("0", sezione.QuerySelector("summary .tag-state")!.TextContent);
        Assert.Contains("Live_NoNotam", sezione.QuerySelector(".body")!.TextContent);
        Assert.Empty(sezione.QuerySelectorAll(".notam-box"));
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
        Assert.Equal("live-notam", chip.GetAttribute("aria-controls"));
        Assert.Contains("Live_NotamChipTitle 1", chip.GetAttribute("title"));

        // L'ordine nella riga: titolo, stato della postazione, chip.
        var figli = cut.Find(".live-top").Children.ToList();
        var stato = figli.FindIndex(e => e.ClassList.Contains("live-badge"));
        Assert.InRange(stato, 0, figli.Count - 1);
        Assert.Equal(stato + 1, figli.FindIndex(e => e.ClassList.Contains("notam-chip")));
    }

    /// <summary>
    /// La sezione NOTAM c'è SEMPRE, in fondo alla pagina, e nasce CHIUSA (committente, 12 ottobre 2026: «sempre
    /// visibile in fondo al documento ma compressa»). ⚠️ Senza <c>open</c> e senza <c>data-persist</c>: lo stato è
    /// del browser, e a ogni apertura della pagina riparte chiusa — a differenza delle altre sezioni della vista.
    /// </summary>
    [Fact]
    public void La_sezione_NOTAM_sta_sempre_in_fondo_e_nasce_chiusa()
    {
        var cut = Apri(Firenze);

        var sezione = cut.Find("details#live-notam");
        Assert.Equal("live-notam", cut.Find(".wrap.live").LastElementChild!.Id);
        Assert.Contains("acc-item", sezione.ClassList);
        Assert.False(sezione.HasAttribute("open"));
        Assert.False(sezione.HasAttribute("data-persist"));
        Assert.Contains("NOTAM", sezione.QuerySelector("summary")!.TextContent);
        Assert.Equal("1", sezione.QuerySelector("summary .tag-state")!.TextContent);
    }

    /// <summary>Dentro la sezione c'è lo stesso riquadro dei documenti, coi NOTAM già aperti: chi ha aperto la
    /// sezione li vuole leggere, non aprirli uno per uno.</summary>
    [Fact]
    public void Dentro_la_sezione_c_e_il_riquadro_dei_documenti_coi_NOTAM_aperti()
    {
        var sezione = Apri(Firenze).Find("details#live-notam");

        Assert.Equal("IT-B002/25", sezione.QuerySelector(".notam-box .notam-num")!.TextContent);
        Assert.True(sezione.QuerySelector("details.notam")!.HasAttribute("open"));
        Assert.Contains("FIRENZE CTR FIRENZE CTA", sezione.QuerySelector("pre.notam-text")!.TextContent);
    }

    /// <summary>
    /// La chip APRE la sezione e ci porta la finestra — lo fa il browser, con la sezione per bersaglio. ⚠️ Apre e
    /// basta, anche alla seconda pressione: non è un interruttore, perché la sezione si chiude dalla sua
    /// intestazione e il circuito non ne conosce lo stato.
    /// </summary>
    [Fact]
    public void La_chip_apre_la_sezione_e_ci_porta_la_finestra()
    {
        var cut = Apri(Firenze);

        cut.Find(".notam-chip").Click();

        var apri = Assert.Single(JSInterop.Invocations, i => i.Identifier == "vipiApriDettaglio");
        Assert.Equal("live-notam", apri.Arguments[0]);

        cut.Find(".notam-chip").Click();

        Assert.Equal(2, JSInterop.Invocations.Count(i => i.Identifier == "vipiApriDettaglio"));
        Assert.All(JSInterop.Invocations.Where(i => i.Identifier == "vipiApriDettaglio"),
            i => Assert.Equal("live-notam", i.Arguments[0]));
    }

    /// <summary>La funzione che la chip chiama esiste, apre il <c>details</c> e scorre: senza, la chip sarebbe un
    /// tasto che in produzione non fa niente e nei test (dove il JS è finto) passa lo stesso.</summary>
    [Fact]
    public void La_funzione_che_apre_la_sezione_esiste_nel_JavaScript_del_sito()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Vipi.Ui")))
            dir = dir.Parent;
        var js = File.ReadAllText(Path.Combine(dir!.FullName, "src", "Vipi.Ui", "wwwroot", "vipi-ui.js"));

        var inizio = js.IndexOf("window.vipiApriDettaglio = function (id)", StringComparison.Ordinal);
        Assert.True(inizio >= 0, "vipiApriDettaglio non è definita in vipi-ui.js");
        // Il corpo della funzione, fino alla sua chiusura: non «i prossimi N caratteri», che al primo commento in
        // più tagliano fuori proprio la riga che si sta cercando.
        var corpo = js[inizio..js.IndexOf("    };", inizio, StringComparison.Ordinal)];
        Assert.Contains("el.open = true;", corpo, StringComparison.Ordinal);
        Assert.Contains("el.scrollIntoView(", corpo, StringComparison.Ordinal);
        // Il margine sopra si misura sulla testata appiccicata, non si scrive fisso: vedi il commento in vipi-ui.js.
        Assert.Contains("el.style.scrollMarginTop", corpo, StringComparison.Ordinal);
    }
}
