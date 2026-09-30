using Microsoft.AspNetCore.Authentication.Cookies;
using Vipi.Ui;

namespace Vipi.Host.Auth;

/// <summary>
/// Il sito si legge solo dopo il login IVAO. Committente, 30 settembre 2026, su segnalazione delle Public Relations:
/// aperto a tutti, i bot se lo portano via. Entra <b>qualunque</b> account IVAO — le vLOA le leggono anche le
/// divisioni vicine. Carta <c>docs/feature/2026-09-30-login-obbligatorio.md</c>.
///
/// <para><b>Un cancello solo, davanti a tutto</b>, e non un <c>[Authorize]</c> pagina per pagina: la pagina che
/// qualcuno dimenticherebbe di marcare resterebbe aperta in silenzio. Qui è il contrario — è chiuso tutto quello
/// che non è scritto in <see cref="Liberi"/>, e una rotta nuova nasce chiusa.</para>
///
/// <para>⚠️ <b>Anche il circuito</b> (<c>/_blazor</c>). Dentro un circuito si naviga fra le pagine senza nuove
/// richieste HTTP: chiudere le pagine e lasciare aperto il circuito sarebbe chiudere la porta e lasciare la
/// finestra. Chi non è entrato non ne apre (il layout non gli dà isole interattive), quindi non perde niente.</para>
///
/// <para>Restano aperti: la porta d'ingresso (<c>/services</c>, che a chi non è entrato mostra solo «Entra con
/// IVAO»), il giro del login, le sonde, le API con la loro chiave e il ponte RFO con la sua. I file statici non
/// passano di qui: li serve <c>UseStaticFiles</c>, prima.</para>
/// </summary>
public static class CancelloDelLogin
{
    /// <summary>Che cosa fare di una richiesta.</summary>
    public enum Esito
    {
        /// <summary>Si prosegue: è entrato, oppure l'indirizzo è libero.</summary>
        Passa,

        /// <summary>Una pagina chiesta da un browser: al login IVAO, e poi di nuovo qui.</summary>
        AlLogin,

        /// <summary>Tutto il resto (circuito, fetch, POST): 401, niente redirect che nessuno seguirebbe.</summary>
        NonAutorizzato,
    }

    /// <summary>
    /// Gli indirizzi aperti a chi non è entrato. ⚠️ <b>Esatti</b> quelli senza la barra finale, <b>prefissi</b> quelli
    /// con: «/services» è la porta, «/services/vsop» no.
    /// </summary>
    internal static readonly string[] Liberi =
    {
        "/",                                   // rimanda alla porta
        VsopRoutes.ServicesHome,               // la porta d'ingresso
        "/services/vsop/auth/",                // login, logout, «accesso non riuscito»
        "/signin-oidc", "/signout-callback-oidc",
        "/vsop/health", "/vsop/health/ready", "/vsop/ping",
        "/vsop/api/",                          // API per altri programmi: hanno la chiave (ApiRotte)
        "/api/rfo/",                           // ponte RFO: ha le sue chiavi
        "/Error",
        "/_framework/", "/_content/",          // asset, se mai arrivassero fin qui
    };

    /// <summary>La decisione, pura: il test la prova senza server.</summary>
    public static Esito Decidi(string? percorso, string metodo, bool entrato, bool accettaHtml,
                               string? callback = null, string? callbackUscita = null)
    {
        if (entrato || Libero(percorso, callback, callbackUscita)) return Esito.Passa;
        return (HttpMethods.IsGet(metodo) || HttpMethods.IsHead(metodo)) && accettaHtml && !Circuito(percorso)
            ? Esito.AlLogin
            : Esito.NonAutorizzato;
    }

    internal static bool Libero(string? percorso, string? callback = null, string? callbackUscita = null)
    {
        // «/services/» vale «/services»: la barra finale non apre né chiude niente.
        var p = string.IsNullOrEmpty(percorso) ? "/" : percorso.Length > 1 ? percorso.TrimEnd('/') : percorso;
        foreach (var l in Liberi.Append(callback).Append(callbackUscita))
        {
            if (string.IsNullOrEmpty(l)) continue;
            var prefisso = l.Length > 1 && l.EndsWith('/');
            if (prefisso ? p.StartsWith(l, StringComparison.OrdinalIgnoreCase)
                           || string.Equals(p, l.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
                         : string.Equals(p, l, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool Circuito(string? percorso) =>
        percorso?.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Monta il cancello. Va DOPO <c>UseAuthentication</c> (deve sapere chi è entrato) e dopo i file statici.</summary>
    public static IApplicationBuilder UseCancelloDelLogin(this IApplicationBuilder app, VipiAuthOptions opt) =>
        app.Use(async (ctx, next) =>
        {
            var accettaHtml = ctx.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase);
            switch (Decidi(ctx.Request.Path.Value, ctx.Request.Method, ctx.User?.Identity?.IsAuthenticated == true,
                           accettaHtml, opt.CallbackPath ?? "/signin-oidc", opt.SignedOutCallbackPath ?? "/signout-callback-oidc"))
            {
                case Esito.Passa:
                    await next();
                    return;

                case Esito.AlLogin:
                    // Il ritorno è la pagina chiesta, con la sua query: dopo il login si arriva dove si voleva.
                    // SafeReturn, al ritorno, la ricontrolla comunque (solo indirizzi nostri).
                    var ritorno = ctx.Request.PathBase + ctx.Request.Path + ctx.Request.QueryString;
                    ctx.Response.Headers.CacheControl = "no-store";
                    ctx.Response.Redirect($"{VsopRoutes.Prefix}/auth/login?returnUrl={Uri.EscapeDataString(ritorno)}");
                    return;

                default:
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    ctx.Response.Headers.CacheControl = "no-store";
                    return;
            }
        });
}
