using Microsoft.AspNetCore.Components;
using Vipi.Application.Content;

namespace Vipi.Ui;

/// <summary>
/// <b>Questa stessa pagina, chiesta in un'altra lingua.</b> Un posto solo, perché ne servono due: il
/// selettore in barra (<c>SopLayout</c>) e i collegamenti <c>rel="alternate"</c> nel <c>&lt;head&gt;</c>
/// (<c>App.razor</c>, nell'host).
///
/// <para>
/// ⚠️ <b>Si riparte dall'indirizzo VERO, query compresa.</b> Un link fisso a <c>?culture=en</c> perderebbe
/// il resto — su <c>/airports?icao=LIBC</c> riporterebbe all'elenco degli aeroporti, e su un'anteprima di
/// release al documento pubblico: cambiare lingua butterebbe via dove sei.
/// </para>
///
/// <para>
/// ⚠️ Le chiavi di cultura già presenti si <b>tolgono</b>, non si aggiungono: lasciarne due accanto
/// vorrebbe dire far decidere all'ordine.
/// </para>
///
/// <para>
/// ⚠️ <b>Percorso assoluto ma senza schema né host.</b> <c>GetUriWithQueryParameters</c> torna
/// l'indirizzo completo, e in produzione davanti c'è Cloudflare: un link che si porta dietro schema e host
/// visti dall'applicazione manderebbe il lettore sull'origine, o su http. <c>PathAndQuery</c> tiene anche
/// l'eventuale percorso di base, che serve quando il modulo è montato dentro un altro sito.
/// </para>
/// </summary>
public static class LinkLingua
{
    /// <summary>L'indirizzo corrente con la lingua chiesta, come percorso assoluto.</summary>
    public static string Url(NavigationManager nav, string lingua)
    {
        var parametri = LinguaDiLettura.ChiaviQuery
            .ToDictionary(k => k, _ => (object?)null, StringComparer.OrdinalIgnoreCase);
        parametri[LinguaDiLettura.ChiaveQuery] = lingua;
        return new Uri(nav.GetUriWithQueryParameters(parametri)).PathAndQuery;
    }
}
