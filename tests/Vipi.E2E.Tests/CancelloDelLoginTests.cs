using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Vipi.Host;
using Vipi.Host.Auth;
using Xunit;
using Esito = Vipi.Host.Auth.CancelloDelLogin.Esito;

namespace Vipi.E2E.Tests;

/// <summary>
/// Il sito si legge solo dopo il login IVAO (committente, 30 settembre 2026; carta
/// <c>docs/feature/2026-09-30-login-obbligatorio.md</c>). La regola, pura, e poi il cancello montato davvero su un
/// host col login IVAO acceso.
/// </summary>
public sealed class CancelloDelLoginTests
{
    // ─── La regola ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("/services/vsop/libb/vipi")]
    [InlineData("/services/vsop/search")]
    [InlineData("/services/vawos/liba")]
    [InlineData("/services/vsop/media/abc")]
    [InlineData("/services/stats")]
    [InlineData("/services/vsop")]
    public void Una_pagina_chiesta_da_chi_non_e_entrato_va_al_login(string percorso)
    {
        Assert.Equal(Esito.AlLogin, CancelloDelLogin.Decidi(percorso, "GET", entrato: false, accettaHtml: true));
    }

    [Theory]
    [InlineData("/services/vsop/libb/vipi")]
    [InlineData("/_blazor")]
    public void Chi_e_entrato_passa_ovunque(string percorso)
    {
        Assert.Equal(Esito.Passa, CancelloDelLogin.Decidi(percorso, "GET", entrato: true, accettaHtml: true));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/services")]
    [InlineData("/services/")]
    [InlineData("/services/cookies")]
    [InlineData("/services/vsop/auth/login")]
    [InlineData("/services/vsop/auth/accesso-non-riuscito")]
    [InlineData("/signin-oidc")]
    [InlineData("/vsop/health")]
    [InlineData("/vsop/ping")]
    [InlineData("/vsop/api/v1/airports/LIRF/sids")]
    [InlineData("/vsop/api/v1/atc/sessions")]
    [InlineData("/api/rfo/events/prova/state")]
    [InlineData("/api/tabellone/LIRF")]
    [InlineData("/Error")]
    public void Restano_aperti_la_porta_il_login_le_sonde_e_le_API_con_chiave(string percorso)
    {
        Assert.Equal(Esito.Passa, CancelloDelLogin.Decidi(percorso, "GET", entrato: false, accettaHtml: true));
    }

    /// <summary>La porta è «/services» e basta: un prefisso che la contenesse aprirebbe tutto il sito.</summary>
    [Theory]
    [InlineData("/services/vsop")]
    [InlineData("/servicesX")]
    [InlineData("/vsop/healthy")]
    [InlineData("/services/vawos/api/LIBA")]
    public void Un_indirizzo_che_somiglia_a_uno_libero_non_lo_e(string percorso)
    {
        Assert.NotEqual(Esito.Passa, CancelloDelLogin.Decidi(percorso, "GET", entrato: false, accettaHtml: true));
    }

    /// <summary>Il circuito si chiude con un 401, non con un redirect: dentro un circuito si naviga fra le pagine
    /// senza nuove richieste HTTP, e un redirect a una fetch non lo segue nessuno.</summary>
    [Theory]
    [InlineData("/_blazor", "GET", true)]
    [InlineData("/_blazor/negotiate", "POST", false)]
    [InlineData("/services/vsop/libb/vipi", "POST", true)]
    [InlineData("/services/vawos/api/LIBA", "GET", false)]
    public void Il_circuito_le_fetch_e_i_POST_ricevono_401(string percorso, string metodo, bool html)
    {
        Assert.Equal(Esito.NonAutorizzato, CancelloDelLogin.Decidi(percorso, metodo, entrato: false, accettaHtml: html));
    }

    [Fact]
    public void Un_callback_configurato_diverso_resta_aperto()
    {
        Assert.Equal(Esito.Passa, CancelloDelLogin.Decidi("/auth/ritorno", "GET", false, true, callback: "/auth/ritorno"));
    }

    // ─── La misura del profilo: solo nomi ───────────────────────────────────────────

    [Fact]
    public void Dei_campi_del_profilo_si_scrivono_i_nomi_mai_i_valori()
    {
        using var doc = JsonDocument.Parse("""
            { "id": 704798, "email": "segreto@example.com", "isStaff": true,
              "rating": { "atcRating": 5, "pilotRating": 3 },
              "userStaffPositions": [ { "id": "IT-AOA1", "note": "interna" } ], "hours": [] }
            """);

        var nomi = DiagnosticaErrori.NomiDeiCampi(doc.RootElement);

        Assert.Equal("id, email, isStaff, rating{atcRating,pilotRating}, userStaffPositions[]{id,note}, hours[]", nomi);
        Assert.DoesNotContain("704798", nomi);
        Assert.DoesNotContain("segreto", nomi);
        Assert.DoesNotContain("IT-AOA1", nomi);
    }

    // ─── Il cancello montato ────────────────────────────────────────────────────────

    /// <summary>
    /// Un host col login IVAO acceso e il cancello davanti: una pagina, la porta, una sonda, un'API e un modo di
    /// entrare senza passare da IVAO (il cookie lo firma l'host stesso, come farebbe il ritorno del login).
    /// </summary>
    private static async Task<(WebApplication App, HttpClient Browser)> AvviaAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["VipiAuth:Enabled"] = "true",
            ["VipiAuth:ClientId"] = "prova",
            ["VipiAuth:Authority"] = "https://ivao.invalid",
        });
        Assert.True(builder.AddVipiStandaloneAuth());

        var app = builder.Build();
        app.UseAuthentication();
        app.MapVipiStandaloneAuth();
        app.UseCancelloDelLogin(new VipiAuthOptions());

        app.MapGet("/services/vsop/libb/vipi", () => "documento");
        app.MapGet("/services", () => "porta");
        app.MapGet("/vsop/ping", () => "pong");
        app.MapGet("/vsop/api/v1/airports", () => Results.Unauthorized());   // la sua porta, con la chiave
        app.MapPost("/_blazor/negotiate", () => "circuito");
        // L'«entrata» dei test sta sotto un prefisso libero apposta: è il solo modo di firmare un cookie di prova.
        app.MapGet("/services/vsop/auth/prova-entra", async (HttpContext ctx) =>
        {
            var chi = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("id", "704798") }, "prova"));
            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, chi);
            return "dentro";
        });

        await app.StartAsync();
        var browser = app.GetTestServer().CreateClient();
        browser.BaseAddress = new Uri("https://localhost");
        return (app, browser);
    }

    private static HttpRequestMessage Pagina(string percorso)
    {
        var r = new HttpRequestMessage(HttpMethod.Get, percorso);
        r.Headers.Accept.ParseAdd("text/html");
        return r;
    }

    [Fact]
    public async Task Da_fuori_la_pagina_rimanda_al_login_e_ci_torna_la_porta_e_la_sonda_rispondono()
    {
        var (app, browser) = await AvviaAsync();
        await using var _ = app;

        using var pagina = await browser.SendAsync(Pagina("/services/vsop/libb/vipi?as=pub"));
        Assert.Equal(HttpStatusCode.Redirect, pagina.StatusCode);
        Assert.Equal("/services/vsop/auth/login?returnUrl=%2Fservices%2Fvsop%2Flibb%2Fvipi%3Fas%3Dpub",
                     pagina.Headers.Location!.OriginalString);
        Assert.True(pagina.Headers.CacheControl?.NoStore);

        Assert.Equal("porta", await (await browser.SendAsync(Pagina("/services"))).Content.ReadAsStringAsync());
        Assert.Equal("pong", await browser.GetStringAsync("/vsop/ping"));
        // L'API non passa dal login: risponde la sua porta (qui finta), con la chiave che chiede lei.
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/vsop/api/v1/airports")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.PostAsync("/_blazor/negotiate", null)).StatusCode);
    }

    [Fact]
    public async Task Chi_e_entrato_legge_la_pagina_e_apre_il_circuito()
    {
        var (app, browser) = await AvviaAsync();
        await using var _ = app;

        using var entra = await browser.GetAsync("/services/vsop/auth/prova-entra");
        var cookie = entra.Headers.GetValues("Set-Cookie").First().Split(';')[0];

        using var richiesta = Pagina("/services/vsop/libb/vipi");
        richiesta.Headers.Add("Cookie", cookie);
        using var pagina = await browser.SendAsync(richiesta);
        Assert.Equal(HttpStatusCode.OK, pagina.StatusCode);
        Assert.Equal("documento", await pagina.Content.ReadAsStringAsync());

        using var circuito = new HttpRequestMessage(HttpMethod.Post, "/_blazor/negotiate");
        circuito.Headers.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.OK, (await browser.SendAsync(circuito)).StatusCode);
    }
}
