using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vipi.Application.Auth;

namespace Vipi.Hosting;

/// <summary>
/// Configurazione delle API per altri programmi (sezione «Api»). Carta
/// <c>docs/feature/2026-09-13-chiavi-api.md</c> §7.
/// </summary>
public sealed class ApiOptions
{
    public const string SectionName = "Api";

    /// <summary>
    /// Se l'archivio ATC rifiuta chi non porta una chiave. <b>Default <c>false</c></b>: l'archivio resta aperto
    /// finché i client esistenti non hanno la loro chiave (decisione del committente, §2.3); si accende con un
    /// cambio di configurazione, non con un pacchetto. Il bridge la chiede <b>sempre</b>.
    /// </summary>
    public bool RichiediChiave { get; set; }
}

/// <summary>
/// La porta delle API: <b>un solo punto</b> decide se una richiesta porta una chiave che apre quell'endpoint,
/// e con quale chiave si conta il tetto. Carta <c>docs/feature/2026-09-13-chiavi-api.md</c> §6.
///
/// <list type="bullet">
/// <item>nessuna chiave: 401 se obbligatoria, altrimenti si passa col tetto per IP (l'archivio di oggi);</item>
/// <item>chiave malformata, sconosciuta o revocata: 401;</item>
/// <item>chiave buona ma non per questo endpoint: 403;</item>
/// <item>chiave buona: il tetto si conta <b>per chiave</b>, non per IP.</item>
/// </list>
///
/// <para>⚠️ I rifiuti vanno nel <b>log</b>, non nell'audit: sono richieste di chiunque, e un client rotto in
/// polling riempirebbe il registro delle azioni dello staff (§8).</para>
/// </summary>
public static class PortaDelleApi
{
    public const string CategoriaLog = "Vipi.Api";

    /// <summary>La chiave dalla richiesta: <c>Authorization: Bearer …</c>, altrimenti <c>X-Api-Key</c>. Vince il primo.</summary>
    public static string? ChiavePresentata(HttpRequest richiesta)
    {
        var auth = richiesta.Headers.Authorization.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var valore = auth["Bearer ".Length..].Trim();
            if (valore.Length > 0) return valore;
        }

        var x = richiesta.Headers["X-Api-Key"].ToString().Trim();
        return x.Length > 0 ? x : null;
    }

    /// <summary>
    /// Null se la richiesta passa; altrimenti la risposta da restituire (401, 403 o 429).
    /// </summary>
    public static async Task<IResult?> ControllaAsync(
        HttpContext ctx, string endpoint, bool chiaveObbligatoria, RequestRateLimiter limiter,
        int perChiamante, int totali, int chiamantiTracciati, CancellationToken ct)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "sconosciuto";
        var chiave = ChiavePresentata(ctx.Request);

        if (chiave is null)
        {
            if (chiaveObbligatoria) return NonAutorizzato(ctx, "chiave API mancante");
            return Tetti(ctx, limiter, endpoint, ip, perChiamante, totali, chiamantiTracciati);
        }

        var verifica = await ctx.RequestServices.GetRequiredService<IVerificaChiaveApi>()
            .VerificaAsync(chiave, endpoint, ct).ConfigureAwait(false);

        switch (verifica.Esito)
        {
            case EsitoChiaveApi.Valida:
                return Tetti(ctx, limiter, endpoint, "chiave:" + verifica.Prefisso, perChiamante, totali, chiamantiTracciati);

            case EsitoChiaveApi.NonAbilitata:
                Log(ctx).LogWarning("API {Endpoint}: chiave {Prefisso} ({Nome}) non abilitata, da {Ip}",
                    endpoint, verifica.Prefisso, verifica.Cliente?.Nome, ip);
                return Results.Json(new { error = "chiave API non abilitata a questo endpoint" },
                    statusCode: StatusCodes.Status403Forbidden);

            default:
                // Il tetto per IP vale anche per chi sbaglia chiave: chi le prova a raffica si ferma lì.
                if (Tetti(ctx, limiter, endpoint, ip, perChiamante, totali, chiamantiTracciati) is { } troppe) return troppe;
                Log(ctx).LogWarning("API {Endpoint}: chiave {Prefisso} sconosciuta o revocata, da {Ip}",
                    endpoint, verifica.Prefisso ?? "(malformata)", ip);
                return NonAutorizzato(ctx, "chiave API sconosciuta o revocata");
        }
    }

    private static IResult? Tetti(HttpContext ctx, RequestRateLimiter limiter, string endpoint, string chiamante,
        int perChiamante, int totali, int chiamantiTracciati)
    {
        if (limiter.PassaITetti(endpoint, chiamante, perChiamante, totali, chiamantiTracciati)) return null;
        ctx.Response.Headers.RetryAfter = "60";
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);
    }

    private static IResult NonAutorizzato(HttpContext ctx, string motivo)
    {
        ctx.Response.Headers.WWWAuthenticate = "Bearer realm=\"vipi\"";
        return Results.Json(new { error = motivo }, statusCode: StatusCodes.Status401Unauthorized);
    }

    private static ILogger Log(HttpContext ctx) =>
        ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(CategoriaLog);
}
