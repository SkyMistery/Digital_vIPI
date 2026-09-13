using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// I cookie che il sito emette da sé viaggiano solo in HTTPS quando la richiesta è HTTPS, e HSTS dura un anno.
///
/// <para>🔴 <b>Perché (T-085, revisione del 13 settembre 2026).</b> Misurato in produzione: il cookie
/// antiforgery e quello della lingua uscivano <b>senza <c>Secure</c></b> su una pagina servita in HTTPS, e
/// HSTS era al default di 30 giorni. Nessuno dei due porta un'identità, ma un cookie senza <c>Secure</c> si
/// lascia leggere e riscrivere da chiunque stia fra il browser e il sito la prima volta che qualcuno scrive
/// l'indirizzo senza <c>https://</c>.</para>
///
/// <para>⚠️ «Come la richiesta» e non «sempre»: in sviluppo l'host ascolta in HTTP, e un cookie <c>Secure</c>
/// su HTTP il browser lo butta — la lingua scelta non resterebbe. In produzione la richiesta arriva HTTPS
/// grazie a <c>X-Forwarded-Proto</c> di nginx, che l'host accetta dal solo loopback.</para>
/// </summary>
public sealed class CookieSicuriTests : IClassFixture<SmokeTests.VipiAppFactory>
{
    private readonly SmokeTests.VipiAppFactory _factory;
    public CookieSicuriTests(SmokeTests.VipiAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Il_cookie_della_lingua_in_https_e_secure()
    {
        var cookie = await SetCookie("/services/vsop?culture=en", ".AspNetCore.Culture");
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Il_cookie_antiforgery_in_https_e_secure()
    {
        // La ricerca è esclusa dalla cache anonima: lì l'antiforgery non viene tolto e si può guardare.
        var cookie = await SetCookie("/services/vsop/search", ".AspNetCore.Antiforgery.");
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Hsts_dura_un_anno()
    {
        var hsts = _factory.Services.GetRequiredService<IOptions<HstsOptions>>().Value;
        Assert.Equal(TimeSpan.FromDays(365), hsts.MaxAge);
    }

    private async Task<string> SetCookie(string indirizzo, string prefisso)
    {
        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        using var resp = await client.GetAsync(indirizzo);
        var trovati = resp.Headers.TryGetValues("Set-Cookie", out var v) ? v.ToList() : new List<string>();
        var cookie = trovati.FirstOrDefault(c => c.StartsWith(prefisso, StringComparison.Ordinal));
        Assert.True(cookie is not null,
            $"nessun cookie {prefisso} su {indirizzo} (stato {(int)resp.StatusCode}); arrivati: {string.Join(" | ", trovati)}");
        return cookie!;
    }
}
