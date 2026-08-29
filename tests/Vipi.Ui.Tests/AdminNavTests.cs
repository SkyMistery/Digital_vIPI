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

    /// <summary>Autorizzazione finta: un livello, che è ciò che la barra guarda dal 29 agosto 2026.</summary>
    private sealed class FakeAuthz : IEditAuthorizationService
    {
        public FakeAuthz(VipiRole livello) => Role = livello;
        public VipiRole Role { get; }
        public bool IsAdmin => Role >= VipiRole.Admin;
        public int? CurrentUserId => 704798;
        public string? CurrentName => "Tizio";
        public void EnsureAdmin() { }
    }

    private IRenderedComponent<AdminNav> Render(VipiRole livello, string url = "http://localhost/services/vsop/admin/audit")
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<IEditAuthorizationService>(new FakeAuthz(livello));
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        return RenderComponent<AdminNav>();
    }

    [Fact]
    public void Un_admin_vede_tutte_le_pagine()
    {
        var cut = Render(VipiRole.Admin);

        var nav = cut.Find("nav.admin-nav");
        // 16 dal 29 agosto 2026: la biblioteca allegati (15 dal 30 agosto, le radioassistenze).
        Assert.Equal(16, nav.QuerySelectorAll(".an-link").Length);
        Assert.Contains("/services/vsop/admin/sector-structure", cut.Markup);
        Assert.Contains("/services/vsop/admin/translations", cut.Markup);
        Assert.Contains("/services/vsop/admin/glossary", cut.Markup);
        Assert.Contains("/services/vsop/admin/navaids", cut.Markup);
        Assert.Contains("/services/vsop/admin/attachments", cut.Markup);
        Assert.Contains("/services/vsop/admin/pending", cut.Markup);
        Assert.Contains("/services/vsop/admin/diagnostics", cut.Markup);
    }

    [Fact]
    public void La_pagina_in_cui_sei_e_uno_stato_non_un_comando()
    {
        var cut = Render(VipiRole.Admin, url: "http://localhost/services/vsop/admin/audit");

        var corrente = cut.Find(".an-link.on");
        Assert.Equal("span", corrente.TagName, ignoreCase: true);   // niente href: non ti porta dove sei già
        Assert.Equal("page", corrente.GetAttribute("aria-current"));
        Assert.Empty(cut.FindAll("a.an-link[href='/services/vsop/admin/audit']"));
    }

    /// <summary>
    /// I filtri lavorano sul PERCORSO: i filtri di Versioni riscrivono l'URL a ogni clic, e un confronto
    /// sull'URL intero direbbe «non sei qui» appena si filtra.
    /// </summary>
    [Fact]
    public void La_query_non_fa_perdere_la_pagina_corrente()
    {
        var cut = Render(VipiRole.Admin, url: "http://localhost/services/vsop/versions?q=lirr&tipo=vipi");

        Assert.Equal("Nav_Docs", cut.Find(".an-link.on").TextContent.Trim());
    }

    /// <summary>
    /// Regola 120: a chi non può aprire nessuna di quelle pagine la barra non si mostra affatto. Un socio, o
    /// uno staffista di divisione che non edita, non deve trovarsi davanti un elenco di porte chiuse.
    /// </summary>
    [Theory]
    [InlineData(VipiRole.User)]
    [InlineData(VipiRole.IvaoStaff)]
    [InlineData(VipiRole.DivisionStaff)]
    public void Chi_non_edita_non_vede_un_elenco_di_porte_chiuse(VipiRole livello)
    {
        var cut = Render(livello, url: "http://localhost/services/vsop/versions");

        Assert.Empty(cut.FindAll("nav.admin-nav"));
        Assert.Empty(cut.Markup.Trim());
    }

    /// <summary>
    /// ⚠️ <b>Il cancello, pagina per pagina.</b> È la rete della slice 5: se domani qualcuno abbassa (o alza)
    /// una voce senza volerlo, qui si vede — e si vede <b>quale</b>. Le undici voci dell'Editor sono il
    /// contenuto documentale; le cinque dell'admin toccano import, sicurezza e diagnosi.
    /// </summary>
    [Theory]
    [InlineData("/services/vsop/admin/sector-structure", VipiRole.Editor)]
    [InlineData("/services/vsop/admin/acc", VipiRole.Editor)]
    [InlineData("/services/vsop/admin/airports", VipiRole.Editor)]
    [InlineData("/services/vsop/admin/neighbours", VipiRole.Editor)]
    [InlineData("/services/vsop/admin/transfers", VipiRole.Editor)]
    [InlineData("/services/vsop/versions", VipiRole.Editor)]
    [InlineData("/services/vsop/admin/pending", VipiRole.Editor)]
    [InlineData("/services/vsop/admin/translations", VipiRole.Editor)]
    [InlineData("/services/vsop/admin/glossary", VipiRole.Editor)]
    [InlineData("/services/vsop/admin/navaids", VipiRole.Editor)]
    [InlineData("/services/vsop/admin/attachments", VipiRole.Editor)]
    [InlineData("/services/vsop/admin/sources", VipiRole.Admin)]
    [InlineData("/services/vsop/admin/tasks", VipiRole.Admin)]
    [InlineData("/services/vsop/admin/audit", VipiRole.Admin)]
    [InlineData("/services/vsop/admin/diagnostics", VipiRole.Admin)]
    [InlineData("/services/vsop/admin/permissions", VipiRole.Admin)]
    public void Ogni_voce_compare_dal_suo_livello_in_su_e_non_prima(string url, VipiRole minimo)
    {
        // ⚠️ Un TestContext per render: bUnit congela il contenitore al primo render, quindi due livelli
        // nello stesso contesto darebbero due volte la stessa risposta — e il test passerebbe sempre.
        // Al livello giusto c'è…
        Assert.Contains(url, Markup(minimo));

        // …e al livello immediatamente sotto no. È la metà che conta: un cancello che non chiude non è un
        // cancello, e allargarsi di un livello è il modo silenzioso in cui i permessi scappano.
        Assert.DoesNotContain(url, Markup((VipiRole)((int)minimo - 1)));
    }

    /// <summary>Il markup della barra a un dato livello, in un contesto tutto suo.</summary>
    private static string Markup(VipiRole livello)
    {
        using var ctx = new TestContext();
        ctx.Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        ctx.Services.AddSingleton<IEditAuthorizationService>(new FakeAuthz(livello));
        // ⚠️ Un indirizzo che NON è nessuna delle voci: la voce della pagina corrente è uno <span> senza
        // href, e cercandola per URL non la si troverebbe.
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/services/vsop/guide");
        return ctx.RenderComponent<AdminNav>().Markup;
    }

    /// <summary>Un editor vede le sue undici voci e nessuna delle cinque dell'admin.</summary>
    [Fact]
    public void Un_editor_vede_undici_voci()
    {
        var cut = Render(VipiRole.Editor, url: "http://localhost/services/vsop/versions");

        // 11 dal 29 agosto 2026: la biblioteca allegati, che è contenuto documentale come le
        // radioassistenze (che avevano portato a 10 il 30 agosto).
        Assert.Equal(11, cut.Find("nav.admin-nav").QuerySelectorAll(".an-link").Length);
        Assert.DoesNotContain("/services/vsop/admin/permissions", cut.Markup);
        Assert.DoesNotContain("/services/vsop/admin/diagnostics", cut.Markup);
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

        var cut = Render(VipiRole.Admin);
        var voci = cut.FindAll(".an-link")
            .Select(e => e.GetAttribute("href"))
            .Where(h => h is not null)
            .ToList();

        // La voce corrente non ha href (è uno stato): la si aggiunge a mano, altrimenti sfugge alla rete.
        voci.Add("/services/vsop/admin/audit");

        Assert.All(voci, url => Assert.Contains(url!.TrimEnd('/'), rotte));
    }
}
