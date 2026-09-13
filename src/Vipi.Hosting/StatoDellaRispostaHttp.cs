using Microsoft.AspNetCore.Http;
using Vipi.Ui.Components;

namespace Vipi.Hosting;

/// <summary>
/// <see cref="IStatoDellaRisposta"/> sulla richiesta HTTP vera: la pagina resa in SSR che dice «non esiste»
/// risponde 404 invece di 200 (T-084, revisione del 13 settembre 2026).
///
/// <para>⚠️ Tocca la risposta solo se <b>non è ancora partita</b>. Dentro un circuito interattivo la richiesta
/// non c'è (o è quella vecchia dell'avvio) e le intestazioni sono già andate: lì non c'è niente da fare, e
/// non si deve sollevare per questo.</para>
///
/// <para>Il 404 basta anche per la cache: <c>CacheDelleLettureAnonime</c> scrive <c>public</c> solo sui 200,
/// e la cache interna non tiene le risposte che non sono 200.</para>
/// </summary>
internal sealed class StatoDellaRispostaHttp : IStatoDellaRisposta
{
    private readonly IHttpContextAccessor _http;
    public StatoDellaRispostaHttp(IHttpContextAccessor http) => _http = http;

    public void NonTrovato()
    {
        if (_http.HttpContext is { Response.HasStarted: false } ctx)
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
    }
}
