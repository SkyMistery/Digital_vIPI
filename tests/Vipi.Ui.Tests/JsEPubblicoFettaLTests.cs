using System.Text.RegularExpressions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Revisione 3, L11 fetta L (S38): tre difetti piccoli del lato pubblico e del JS. U-208 (vista live senza
/// documento) sta in <see cref="LivePageSenzaDocumentoTests"/>.
/// </summary>
public class JsEPubblicoFettaLTests : TestContext
{
    // ------------------------------------------------------------------ U-209 vipi-boot.js

    /// <summary>
    /// U-209: il segno «caricato» si scriveva prima di appendere lo &lt;script&gt;, e senza <c>onerror</c>: un file
    /// che non arrivava non si chiedeva più. ⚠️ Presidio sul testo: il comportamento (ritenta, poi si ferma
    /// dopo tre) è provato con una simulazione in Node, <c>docs/history/revisione-totale-3/sim-boot.js</c>;
    /// la suite non ha un motore JS.
    /// </summary>
    [Fact]
    public void Un_modulo_che_non_arriva_si_ritenta()
    {
        var js = Regex.Replace(Leggi("wwwroot/vipi-boot.js"), "//.*", "");
        var carica = Regex.Match(js, @"function\s+carica\s*\(m\)\s*\{(?<c>.*?)\n    \}", RegexOptions.Singleline);
        Assert.True(carica.Success, "carica(m) non trovata in vipi-boot.js");
        var c = carica.Groups["c"].Value;

        Assert.Contains("el.onerror", c, StringComparison.Ordinal);
        Assert.Contains("delete caricati[chiave]", c, StringComparison.Ordinal);

        // Il segno «arrivato» lo mette solo onload.
        var arrivato = c.IndexOf("caricati[chiave] = true", StringComparison.Ordinal);
        Assert.True(arrivato > c.IndexOf("el.onload", StringComparison.Ordinal),
            "caricati[chiave] = true si scrive prima dell'arrivo del file: un caricamento fallito non si ritenta più.");
        Assert.Contains("MAX_TENTATIVI", js, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ U-210 landing dell'ACC

    /// <summary>
    /// U-210: aeroporti, APP e vLoA in evidenza erano <c>&lt;li onclick="location.href=…"&gt;</c>. Presidio sul
    /// sorgente: la pagina legge dal proprio scope e montarla vuol dire mezzo contenitore di servizi.
    /// </summary>
    [Fact]
    public void Le_righe_in_evidenza_della_landing_sono_collegamenti_veri()
    {
        // Senza i commenti Razor: raccontano il difetto e nominano `<li onclick>` a parole.
        var razor = Regex.Replace(Leggi("Pages/AccLanding.razor"), @"@\*.*?\*@", "", RegexOptions.Singleline);

        Assert.DoesNotContain("onclick=\"location.href", razor, StringComparison.Ordinal);
        var righe = Regex.Matches(razor, @"<li(?![^>]*class=""empty"")[^>]*>(?<c>.*?)</li>", RegexOptions.Singleline);
        Assert.Equal(3, righe.Count);
        Assert.All(righe, r => Assert.StartsWith("<a href=\"", r.Groups["c"].Value, StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------ U-213 «Cosa è cambiato»

    private sealed class UnaModifica : IChangesService
    {
        public string CurrentCycle => "2610";
        public Task<IReadOnlyList<ChangeRow>> ListChangedAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ChangeRow>>(new[]
            {
                new ChangeRow
                {
                    DocTitle = "vIPI LIRR", Type = DocumentType.Vipi, AccCode = "LIRR", Url = "/services/vsop/lirr/vipi",
                    VersionNumber = 3, PublishedByUserId = 704798,
                    PublishedUtc = new DateTime(2026, 9, 27, 14, 32, 0, DateTimeKind.Unspecified),
                    PrevBlocks = 1, CurrBlocks = 2, PrevSections = 1, CurrSections = 1,
                },
            });
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + string.Concat(arguments.Select(a => " " + a)), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    /// <summary>U-213: l'orario di pubblicazione era UTC senza «Z» e senza <c>data-utc</c> (niente ora locale).</summary>
    [Fact]
    public void L_orario_di_pubblicazione_porta_la_Z_e_l_istante_per_l_ora_locale()
    {
        Services.AddSingleton<IChangesService>(new UnaModifica());
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddSingleton(new EnglishStrings());
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<ChangedPage>();

        var ora = cut.Find(".chg-row [data-utc]");
        Assert.Equal("2026-09-27T14:32:00Z", ora.GetAttribute("data-utc"));
        Assert.EndsWith("14:32Z", ora.TextContent.Trim(), StringComparison.Ordinal);
    }

    private static string Leggi(string relativo)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "src", "Vipi.Ui", relativo.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(c)) return File.ReadAllText(c);
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"{relativo} non trovato risalendo da {AppContext.BaseDirectory}");
    }
}
