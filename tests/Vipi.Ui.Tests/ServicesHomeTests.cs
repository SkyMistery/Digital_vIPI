using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Ui;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// L'hub è fatto di collegamenti, quindi è dei collegamenti che ci si deve fidare: un'etichetta sbagliata si
/// vede, un indirizzo sbagliato no — porta a una pagina bianca, e solo per chi ci clicca sopra. Stessa rete
/// che <c>AdminNavTests</c> tiene sulla barra admin, per la stessa ragione.
/// </summary>
public class ServicesHomeTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private IRenderedComponent<ServicesHome> Render()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        return RenderComponent<ServicesHome>();
    }

    [Fact]
    public void Elenca_i_servizi_con_gli_indirizzi_giusti()
    {
        var cut = Render();
        var indirizzi = cut.FindAll("a.choice").Select(a => a.GetAttribute("href")).ToList();

        Assert.Contains("/services/vsop", indirizzi);
        Assert.Contains("/services/profile-swapper", indirizzi);
    }

    /// <summary>
    /// I figli di <c>/services</c> sono tutti servizi, allo stesso livello: è la regola che rende la forma
    /// delle URL leggibile senza spiegarla. Se un giorno qualcuno annidasse uno strumento sotto un altro —
    /// o sotto la documentazione — questo test lo direbbe.
    /// </summary>
    [Fact]
    public void Ogni_servizio_e_figlio_diretto_di_services()
    {
        var cut = Render();

        foreach (var href in cut.FindAll("a.choice").Select(a => a.GetAttribute("href")!))
        {
            Assert.StartsWith("/services/", href);
            Assert.Equal(2, href.Trim('/').Split('/').Length);
        }
    }
}
