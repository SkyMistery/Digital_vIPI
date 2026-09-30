using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>La pagina «Accessi al sito» (30 settembre 2026): solo agli amministratori, e la ricerca arriva al servizio.</summary>
public class PaginaAccessiTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class Livello(VipiRole ruolo) : IEditAuthorizationService
    {
        public VipiRole Role => ruolo;
        public bool IsAdmin => ruolo >= VipiRole.Admin;
        public int? CurrentUserId => 1;
        public string? CurrentName => "Prova";
        public void EnsureAdmin() { if (!IsAdmin) throw new EditNotAllowedException(); }
    }

    private sealed class Registro : IRegistroAccessi
    {
        public string? Cercato;
        public int Letture;
        public Task RegistraAsync(CurrentUser utente, CancellationToken ct = default) => Task.CompletedTask;
        public Task<ElencoAccessi> ElencoAsync(string? cerca, CancellationToken ct = default)
        {
            Letture++;
            Cercato = cerca;
            var ora = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
            return Task.FromResult(new ElencoAccessi(new[]
            {
                new AccessoAlSitoRiga(704798, "Carmine Granato", "IT", "LIRR", ora.AddDays(-20), ora, 7),
                new AccessoAlSitoRiga(123456, "Jean Dupont", null, null, ora.AddDays(-1), ora.AddDays(-1), 1),
            }, 2));
        }
    }

    private (IRenderedComponent<StatsAccessiPage> Pagina, Registro Registro) Apri(VipiRole ruolo, string? q = null)
    {
        var registro = new Registro();
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<StringheDelSito>();
        Services.AddSingleton<IEditAuthorizationService>(new Livello(ruolo));
        Services.AddSingleton<IRegistroAccessi>(registro);
        // Il parametro di query si passa com'è nella realtà: nell'indirizzo.
        var nav = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        nav.NavigateTo(q is null ? "/services/stats/logins" : $"/services/stats/logins?q={Uri.EscapeDataString(q)}");
        var pagina = RenderComponent<StatsAccessiPage>();
        return (pagina, registro);
    }

    [Theory]
    [InlineData(VipiRole.User)]
    [InlineData(VipiRole.Editor)]
    [InlineData(VipiRole.DivisionStaff)]
    public void Chi_non_e_amministratore_non_vede_l_elenco_e_il_servizio_non_si_interroga(VipiRole ruolo)
    {
        var (pagina, registro) = Apri(ruolo);

        Assert.Empty(pagina.FindAll("table"));
        Assert.Contains("Logins_AdminOnly", pagina.Markup);
        Assert.Equal(0, registro.Letture);
    }

    [Fact]
    public void L_amministratore_vede_nome_VID_divisione_ACC_e_giorni_e_la_ricerca_arriva_al_servizio()
    {
        var (pagina, registro) = Apri(VipiRole.Admin, q: "carmine");

        Assert.Equal("carmine", registro.Cercato);
        var righe = pagina.FindAll("tbody tr").ToList();
        Assert.Equal(2, righe.Count);
        var prima = righe[0].TextContent;
        Assert.Contains("Carmine Granato", prima);
        Assert.Contains("704798", prima);
        Assert.Contains("LIRR", prima);
        Assert.Contains("7", prima);
        // Chi è entrato con un cookie di prima non ha la divisione: un trattino, non un vuoto.
        Assert.Contains("—", righe[1].TextContent);
        Assert.Equal("carmine", pagina.Find("input[name=q]").GetAttribute("value"));
    }
}
