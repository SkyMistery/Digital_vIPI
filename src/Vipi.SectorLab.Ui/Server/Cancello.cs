using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Vipi.SectorLab.Ui.Server;

/// <summary>
/// Il cancello del server locale (carta F3 §3, prova di F0): il server ascolta su <c>127.0.0.1</c>, ma su quella porta
/// può bussare qualunque programma della macchina — anche una pagina aperta nel browser dell'utente. Entra solo chi
/// conosce il SEGRETO, nato a ogni avvio: la WebView2 lo riceve nell'indirizzo della prima pagina.
/// <list type="number">
/// <item>Con <c>?k=&lt;segreto&gt;</c> il cancello mette un cookie <c>HttpOnly</c>, <c>SameSite=Strict</c> e
/// <b>rimanda</b> allo stesso indirizzo senza il segreto: così non resta nella barra, nella cronologia né nei
/// <c>Referer</c>.</item>
/// <item>Col cookie giusto si passa, <b>ovunque</b> — anche su <c>/_blazor</c>. Il prototipo di F0 lasciava passare
/// <c>/_blazor</c> senza chiedere niente: qui no, la WebView2 il cookie lo manda anche lì.</item>
/// <item>Senza, <b>403</b>.</item>
/// </list>
/// </summary>
public sealed class Cancello
{
    internal const string NomeDelCookie = "sectorlab";
    internal const string ParametroDelSegreto = "k";

    private readonly RequestDelegate _avanti;
    private readonly byte[] _segreto;

    public Cancello(RequestDelegate avanti, SegretoDelLab segreto)
    {
        _avanti = avanti;
        _segreto = Encoding.ASCII.GetBytes(segreto.Valore);
    }

    public Task InvokeAsync(HttpContext contesto)
    {
        if (Giusto(contesto.Request.Cookies[NomeDelCookie]))
            return _avanti(contesto);

        if (Giusto(contesto.Request.Query[ParametroDelSegreto]))
        {
            contesto.Response.Cookies.Append(NomeDelCookie, Encoding.ASCII.GetString(_segreto), new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Path = "/",
            });
            contesto.Response.Redirect(IndirizzoSenzaSegreto(contesto.Request));
            return Task.CompletedTask;
        }

        contesto.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    /// <summary>A tempo costante: chi prova il segreto un carattere per volta non impara niente dai tempi.</summary>
    private bool Giusto(string? offerto)
        => offerto is not null
           && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(offerto), _segreto);

    private static string IndirizzoSenzaSegreto(HttpRequest richiesta)
    {
        var resto = richiesta.Query
            .Where(p => !string.Equals(p.Key, ParametroDelSegreto, StringComparison.Ordinal))
            .SelectMany(p => p.Value.Select(v => new KeyValuePair<string, string?>(p.Key, v)));
        return richiesta.PathBase + richiesta.Path + QueryString.Create(resto);
    }
}

/// <summary>Il segreto di un avvio: 32 byte casuali in esadecimale, mai scritto su disco.</summary>
public sealed class SegretoDelLab
{
    public SegretoDelLab() : this(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)))
    {
    }

    internal SegretoDelLab(string valore) => Valore = valore;

    public string Valore { get; }
}
