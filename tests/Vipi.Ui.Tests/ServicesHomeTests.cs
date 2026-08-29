using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Auth;
using Vipi.Domain;
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

    /// <summary>Autorizzazione finta: dal 29 agosto 2026 l'hub chiede il LIVELLO, perché una scheda è chiusa.</summary>
    private sealed class FakeAuthz(VipiRole livello) : IEditAuthorizationService
    {
        public VipiRole Role { get; } = livello;
        public bool IsAdmin => Role >= VipiRole.Admin;
        public int? CurrentUserId => 704798;
        public string? CurrentName => "Tizio";
        public void EnsureAdmin() { }
    }

    private IRenderedComponent<ServicesHome> Render(VipiRole livello = VipiRole.DivisionStaff)
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<IEditAuthorizationService>(new FakeAuthz(livello));
        return RenderComponent<ServicesHome>();
    }

    [Fact]
    public void Elenca_i_servizi_con_gli_indirizzi_giusti()
    {
        var cut = Render();
        var indirizzi = cut.FindAll("a.choice").Select(a => a.GetAttribute("href")).ToList();

        Assert.Contains("/services/vsop", indirizzi);
        Assert.Contains("/services/profile-swapper", indirizzi);
        Assert.Contains("/services/coordinates", indirizzi);
    }

    /// <summary>
    /// Il convertitore è per lo staff di divisione, e chi non lo è non deve nemmeno vedere la porta: un elenco
    /// di porte chiuse è la stessa cosa che la barra admin evita da sempre (regola 120).
    /// </summary>
    [Theory]
    [InlineData(VipiRole.User, false)]
    [InlineData(VipiRole.IvaoStaff, false)]
    [InlineData(VipiRole.DivisionStaff, true)]
    [InlineData(VipiRole.Admin, true)]
    public void Il_convertitore_si_vede_solo_dallo_staff_di_divisione(VipiRole livello, bool atteso)
    {
        var cut = Render(livello);
        var indirizzi = cut.FindAll("a.choice").Select(a => a.GetAttribute("href")).ToList();

        Assert.Equal(atteso, indirizzi.Contains("/services/coordinates"));
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
