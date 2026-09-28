using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Ui;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// «Tutte le schermate» (revisione 3, U-254). La pagina diceva «tutte le pagine» ed era una lista a mano ferma
/// alla mockup v2: 14 indirizzi su 58, con le etichette in italiano anche con l'interfaccia inglese. Ora si
/// genera dalle rotte dell'assembly, e la prova e' che nessuna rotta manchi — quella che si dimentica e' sempre
/// l'ultima arrivata.
/// </summary>
public class ScreensIndexTests
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private static IReadOnlyList<string> RotteDellAssembly() => typeof(ScreensIndex).Assembly.GetTypes()
        .SelectMany(t => t.GetCustomAttributes<RouteAttribute>())
        .Select(a => a.Template)
        .Distinct()
        .ToList();

    private static IRenderedComponent<ScreensIndex> Rendi(TestContext ctx)
    {
        ctx.Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        ctx.Services.AddSingleton(new EnglishStrings());
        return ctx.RenderComponent<ScreensIndex>();
    }

    [Fact]
    public void Ogni_rotta_senza_parametri_ha_il_suo_collegamento()
    {
        using var ctx = new TestContext();
        var cut = Rendi(ctx);

        var collegati = cut.FindAll(".choice-grid a.choice").Select(a => a.GetAttribute("href")).ToHashSet();
        var attese = RotteDellAssembly().Where(r => !r.Contains('{')).ToList();

        Assert.True(attese.Count > 30, $"poche rotte trovate: {attese.Count}");
        Assert.All(attese, r => Assert.Contains(r, collegati));
        // Le pagine che la lista a mano non aveva: statistiche, archivio, vAWOS, convertitore.
        Assert.Contains("/services/stats/world", collegati);
        Assert.Contains("/services/vawos", collegati);
        Assert.Contains("/services/coordinates", collegati);
    }

    [Fact]
    public void Una_rotta_con_parametri_si_mostra_ma_non_si_collega()
    {
        using var ctx = new TestContext();
        var cut = Rendi(ctx);

        Assert.DoesNotContain(cut.FindAll("a.choice"), a => (a.GetAttribute("href") ?? "").Contains('{'));
        var tutte = cut.FindAll(".choice-grid .choice").Count;
        Assert.Equal(RotteDellAssembly().Count, tutte);
    }
}
