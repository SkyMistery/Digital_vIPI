using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// I comandi del blocco AoR sono comandi anche per chi non usa il mouse.
///
/// <para><b>Il difetto che presidia.</b> Fino al 23 agosto 2026 le chip per-settore erano
/// <c>&lt;span&gt;</c> e i «Tutti / Nessuno / Azzera» erano <c>&lt;a&gt;</c> <b>senza href</b>: nessuno dei
/// due entra nel giro del tabulatore, nessuno risponde a Invio o Spazio, e uno screen reader li legge come
/// testo. Non era un dettaglio di rifinitura — sono gli interruttori con cui si sceglie cosa vedere sulla
/// mappa, e stanno sulle pagine <b>pubbliche</b>, quelle che si aprono senza login e dal telefono.</para>
///
/// <para>La regola è scritta per esteso in <c>Chip.razor</c>; il blocco AoR le sfuggiva perché la sua
/// interattività non passa da Blazor ma da JS puro (<c>vipi-aor.js</c>), quindi <c>Chip.razor</c> qui non
/// era utilizzabile e il markup era rimasto quello di prima.</para>
///
/// <para>⚠️ Si verifica anche <c>aria-pressed</c>, non solo il tag: un <c>&lt;button&gt;</c> porta con sé
/// Invio/Spazio e il ruolo, ma non lo STATO. Senza, l'unico modo di sapere quali settori sono accesi
/// resterebbe il colore — cioè di nuovo solo per chi vede la mappa.</para>
/// </summary>
public class AorComandiTastieraTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public AorComandiTastieraTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    private static AppAorPolygon Poly() => new(
        "0 0 100 100", "M0 0L10 0L10 10Z",
        new[] { new[] { 43.0, 10.0 }, new[] { 43.0, 11.0 }, new[] { 44.0, 11.0 } },
        43.0, 10.0, 44.0, 11.0, 43.5, 10.5);

    private static AccAorView Vista() => new(
        new[] { new AccSectorAor("LIRR_NE_CTR", "NE", "#0D2C99", new[] { Poly() }, 245, 355) },
        new[] { new AccConfigSelection("cfg1", "Configurazione 1", new[] { "LIRR_NE_CTR" }) });

    [Fact]
    public void Le_chip_settore_sono_bottoni_e_dicono_il_proprio_stato()
    {
        var cut = RenderComponent<AccAor>(p => p.Add(x => x.View, Vista()));

        var chip = cut.Find(".aor-chip");
        Assert.Equal("BUTTON", chip.TagName);
        Assert.Equal("button", chip.GetAttribute("type"));      // dentro un <form> non deve inviare nulla
        Assert.Equal("true", chip.GetAttribute("aria-pressed")); // nascono tutte accese
    }

    [Fact]
    public void Tutti_nessuno_e_azzera_sono_bottoni_e_non_finti_collegamenti()
    {
        var cut = RenderComponent<AccAor>(p => p.Add(x => x.View, Vista()));

        foreach (var sel in new[] { ".aor-all", ".cfg-clear", ".cfg-btn" })
        {
            var el = cut.Find(sel);
            Assert.True(el.TagName == "BUTTON",
                $"{sel} è <{el.TagName.ToLowerInvariant()}>: un comando che esiste solo per il mouse. " +
                "Vedi Chip.razor.");
        }

        // La configurazione è un interruttore come le chip: nasce spenta e lo dice.
        Assert.Equal("false", cut.Find(".cfg-btn").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Anche_il_viewer_3d_ha_le_stesse_chip()
    {
        // Le chip del 3D sono le stesse del 2D (le pilota lo stesso gestore): se una delle due copie
        // tornasse <span>, il difetto si ripresenterebbe su metà delle pagine.
        var cut = RenderComponent<AccAor3d>(p => p.Add(x => x.View, Vista()));

        var chip = cut.Find(".aor-chip");
        Assert.Equal("BUTTON", chip.TagName);
        Assert.Equal("true", chip.GetAttribute("aria-pressed"));
        Assert.Equal("BUTTON", cut.Find(".aor-all").TagName);
    }
}
