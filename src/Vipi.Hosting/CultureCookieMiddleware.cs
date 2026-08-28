using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Vipi.Application.Content;

namespace Vipi.Hosting;

/// <summary>
/// Ricorda in un cookie la lingua chiesta con <c>?culture=</c>, perché altrimenti vale per una richiesta sola
/// — e in Blazor Server le richieste sono <b>due</b>.
///
/// <para><b>Il difetto, misurato.</b> Con browser in inglese, su <c>/services/vsop?culture=it</c> il prerender
/// scriveva «Documentazione operativa» e subito dopo il circuito <c>InteractiveServer</c> ridisegnava
/// «Operational documentation»: italiano che diventa inglese sotto gli occhi, in una pagina che l'utente ha
/// chiesto in italiano. Vale per <b>ogni</b> pagina interattiva, non per una.</para>
///
/// <para><b>Perché succedeva.</b> <c>UseRequestLocalization</c> risolve la lingua <i>per richiesta</i>, e
/// l'ordine dei provider è stringa di query → cookie → <c>Accept-Language</c>. La richiesta del documento
/// porta <c>?culture=it</c> e vince la stringa di query; la connessione <c>/_blazor</c> che apre il circuito
/// è un'altra richiesta, <b>senza</b> quella stringa, quindi ricade su <c>Accept-Language</c> — cioè sulla
/// lingua del browser. Il circuito nasce con quella cultura e la tiene per tutta la sua vita.</para>
///
/// <para><b>La cura.</b> Quando la lingua è stata chiesta <b>esplicitamente</b> nell'indirizzo, si scrive il
/// cookie standard di <c>CookieRequestCultureProvider</c>: la richiesta successiva — <c>/_blazor</c> compresa
/// — trova il cookie e risolve la stessa lingua. ⚠️ <b>Solo su richiesta esplicita</b>: scrivere il cookie
/// anche quando la lingua arriva da <c>Accept-Language</c> congelerebbe per un anno una scelta che l'utente
/// non ha mai fatto, e cambiare lingua al browser non avrebbe più effetto.</para>
///
/// <para>Va montato <b>dopo</b> <c>UseRequestLocalization</c>: legge la lingua già risolta
/// (<see cref="IRequestCultureFeature"/>) invece di rifare il parse, così cookie e pagina non possono
/// divergere.</para>
/// </summary>
public sealed class CultureCookieMiddleware
{
    private readonly RequestDelegate _next;
    public CultureCookieMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext ctx)
    {
        if (Richiesta(ctx) && ctx.Features.Get<IRequestCultureFeature>()?.RequestCulture is { } cultura)
        {
            var valore = CookieRequestCultureProvider.MakeCookieValue(cultura);
            // Riscrivere lo stesso valore a ogni richiesta e' rumore inutile in ogni risposta.
            if (!string.Equals(ctx.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName], valore,
                    StringComparison.Ordinal))
            {
                ctx.Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName, valore, new CookieOptions
                {
                    Path = "/",
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    // Serve al funzionamento del sito (la lingua scelta): non e' soggetto a consenso.
                    IsEssential = true,
                    // Nessuno script deve leggerlo, e non deve viaggiare su richieste cross-site.
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                });
            }
        }

        return _next(ctx);
    }

    private static bool Richiesta(HttpContext ctx)
    {
        foreach (var chiave in LinguaDiLettura.ChiaviQuery)
            if (ctx.Request.Query.ContainsKey(chiave)) return true;
        return false;
    }
}
