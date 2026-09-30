using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Ui;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>«I miei dati» (30 settembre 2026): la propria riga, i rimandi agli altri dati, e la strada per chiedere la
/// cancellazione. Solo vedere: nessun tasto che scriva.</summary>
public class PaginaMieiDatiTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class Chi(int? vid) : IEditAuthorizationService
    {
        public VipiRole Role => VipiRole.User;
        public bool IsAdmin => false;
        public int? CurrentUserId => vid;
        public string? CurrentName => "Prova";
        public void EnsureAdmin() { }
    }

    private sealed class Registro(AccessoAlSitoRiga? riga) : IRegistroAccessi
    {
        public Task RegistraAsync(CurrentUser utente, CancellationToken ct = default) => Task.CompletedTask;
        public Task<ElencoAccessi> ElencoAsync(string? cerca, CancellationToken ct = default) => throw new InvalidOperationException("non qui");
        public Task<AccessoAlSitoRiga?> MieiAsync(CancellationToken ct = default) => Task.FromResult(riga);
        public Task<IReadOnlyDictionary<int, string>> NomiBreviAsync(IReadOnlyCollection<int> userIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string>());
    }

    private IRenderedComponent<MieiDatiPage> Apri(int? vid, AccessoAlSitoRiga? riga)
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<StringheDelSito>();
        Services.AddSingleton<IEditAuthorizationService>(new Chi(vid));
        Services.AddSingleton<IRegistroAccessi>(new Registro(riga));
        return RenderComponent<MieiDatiPage>();
    }

    [Fact]
    public void Mostra_la_propria_riga_i_rimandi_e_la_strada_per_la_cancellazione_senza_tasti()
    {
        var ora = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
        var cut = Apri(704798, new AccessoAlSitoRiga(704798, "Carmine Granato", "IT", "LIRR", ora.AddDays(-3), ora, 2));

        var tabella = cut.Find("table.my-data").TextContent;
        Assert.Contains("Carmine Granato", tabella);
        Assert.Contains("LIRR", tabella);

        var link = cut.FindAll("a").Select(a => a.GetAttribute("href")).ToList();
        Assert.Contains("/services/stats/user/704798", link);
        Assert.Contains("/services/vsop/requests", link);
        Assert.Empty(cut.FindAll("button"));
        Assert.Empty(cut.FindAll("form"));
    }

    [Fact]
    public void Senza_riga_lo_dice_e_senza_login_chiede_di_entrare()
    {
        Assert.Contains("My_NoRow", Apri(704798, null).Markup);
    }

    [Fact]
    public void Chi_non_e_entrato_non_vede_tabelle()
    {
        var cut = Apri(null, null);
        Assert.Contains("My_NotSignedIn", cut.Markup);
        Assert.Empty(cut.FindAll("table"));
    }
}
