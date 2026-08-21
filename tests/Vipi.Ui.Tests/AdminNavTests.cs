using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La barra che porta da una pagina admin all'altra (<c>AdminNav</c>).
///
/// <para>Presidia la ragione per cui il filtro sta nel COMPONENTE e non in un <c>@if</c> copiato in undici
/// pagine: chi non è admin non deve vedere un elenco di porte chiuse (regola 120), e il giorno che una pagina
/// cambia autorizzazione si cambia una riga sola. Senza questi test la regressione è muta — la barra
/// comparirebbe lo stesso, solo piena di link che rispondono «accesso riservato».</para>
///
/// <para>L'ultimo test è la rete sulle ROTTE: un'etichetta sbagliata si vede, un URL sbagliato no — porta a
/// una pagina bianca, e solo per chi ci clicca sopra.</para>
/// </summary>
public class AdminNavTests : TestContext
{
    /// <summary>Localizer che rende la chiave stessa: le asserzioni parlano di chiavi, non di traduzioni.</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    /// <summary>Autorizzazione finta: qui conta solo <c>IsAdmin</c>, che è ciò che la barra guarda.</summary>
    private sealed class FakeAuthz : IEditAuthorizationService
    {
        public FakeAuthz(bool admin) => IsAdmin = admin;
        public bool IsAdmin { get; }
        public int? CurrentUserId => 704798;
        public string? CurrentName => "Tizio";
        public Task EnsureCanEditAccAsync(string accCode, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsureCanEditDocumentAsync(int documentId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> CanEditAccAsync(string accCode, CancellationToken ct = default) => Task.FromResult(IsAdmin);
        public Task<bool> CanEditDocumentAsync(int documentId, CancellationToken ct = default) => Task.FromResult(IsAdmin);
        public Task<IReadOnlyList<GrantRow>> ListGrantsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GrantRow>>(Array.Empty<GrantRow>());
        public Task<int> AddGrantAsync(int userId, string? displayName, string accCode, CancellationToken ct = default) => Task.FromResult(0);
        public Task RevokeGrantAsync(int grantId, CancellationToken ct = default) => Task.CompletedTask;
        public void EnsureAdmin() { }
    }

    private IRenderedComponent<AdminNav> Render(bool admin, string url = "http://localhost/vsop/admin/audit")
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<IEditAuthorizationService>(new FakeAuthz(admin));
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        return RenderComponent<AdminNav>();
    }

    [Fact]
    public void Un_admin_vede_tutte_le_pagine()
    {
        var cut = Render(admin: true);

        var nav = cut.Find("nav.admin-nav");
        Assert.Equal(11, nav.QuerySelectorAll(".an-link").Length);
        Assert.Contains("/vsop/admin/sectorstructure", cut.Markup);
        Assert.Contains("/vsop/admin/diagnostica", cut.Markup);
    }

    [Fact]
    public void La_pagina_in_cui_sei_e_uno_stato_non_un_comando()
    {
        var cut = Render(admin: true, url: "http://localhost/vsop/admin/audit");

        var corrente = cut.Find(".an-link.on");
        Assert.Equal("span", corrente.TagName, ignoreCase: true);   // niente href: non ti porta dove sei già
        Assert.Equal("page", corrente.GetAttribute("aria-current"));
        Assert.Empty(cut.FindAll("a.an-link[href='/vsop/admin/audit']"));
    }

    /// <summary>
    /// I filtri lavorano sul PERCORSO: i filtri di Versioni riscrivono l'URL a ogni clic, e un confronto
    /// sull'URL intero direbbe «non sei qui» appena si filtra.
    /// </summary>
    [Fact]
    public void La_query_non_fa_perdere_la_pagina_corrente()
    {
        var cut = Render(admin: true, url: "http://localhost/vsop/versioni?q=lirr&tipo=vipi");

        Assert.Equal("Nav_Docs", cut.Find(".an-link.on").TextContent.Trim());
    }

    /// <summary>
    /// Regola 120: a chi non può aprire nessuna di quelle pagine la barra non si mostra affatto. Un non-admin
    /// arriva solo a Versioni — cioè alla pagina in cui è già — e una voce sola non è una navigazione.
    /// </summary>
    [Fact]
    public void Chi_non_e_admin_non_vede_un_elenco_di_porte_chiuse()
    {
        var cut = Render(admin: false, url: "http://localhost/vsop/versioni");

        Assert.Empty(cut.FindAll("nav.admin-nav"));
        Assert.Empty(cut.Markup.Trim());
    }

    /// <summary>
    /// Ogni voce punta a una rotta che esiste davvero. Un URL sbagliato non si vede rileggendo la lista: porta
    /// a una pagina bianca, e solo a chi ci clicca sopra.
    /// </summary>
    [Fact]
    public void Ogni_voce_punta_a_una_rotta_che_esiste()
    {
        var rotte = typeof(AdminNav).Assembly.GetTypes()
            .Where(t => typeof(IComponent).IsAssignableFrom(t))
            .SelectMany(t => t.GetCustomAttributes<RouteAttribute>())
            .Select(r => r.Template.TrimEnd('/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var cut = Render(admin: true);
        var voci = cut.FindAll(".an-link")
            .Select(e => e.GetAttribute("href"))
            .Where(h => h is not null)
            .ToList();

        // La voce corrente non ha href (è uno stato): la si aggiunge a mano, altrimenti sfugge alla rete.
        voci.Add("/vsop/admin/audit");

        Assert.All(voci, url => Assert.Contains(url!.TrimEnd('/'), rotte));
    }
}
