namespace Vipi.Host;

/// <summary>
/// Rende <b>riutilizzabili</b> le risposte alle letture anonime dei documenti pubblici: toglie il cookie che
/// nessuno usa e dichiara per quanto la pagina si può tenere.
///
/// <para><b>Il problema, misurato il 27 agosto 2026.</b> Una richiesta anonima a un documento pubblico —
/// cioè a una copia <b>congelata</b>, che cambia solo quando qualcuno ripubblica — rispondeva così:</para>
///
/// <code>
///   Cache-Control: no-cache, no-store, max-age=0
///   Set-Cookie: .AspNetCore.Antiforgery.…
/// </code>
///
/// <para>Le due righe insieme dicono a ogni cache del mondo «non tenermi». Davanti al sito c'è Cloudflare, e
/// il giorno della pubblicazione AIRAC è l'unica cosa che sta fra i lettori e un processo solo, senza
/// backplane: così com'era, non poteva aiutare. Ogni visitatore arrivava fino in fondo e faceva rendere la
/// pagina da capo.</para>
///
/// <para><b>Il cookie.</b> Lo emette l'endpoint dei Razor Component, sempre, perché un modulo che rende
/// moduli non sa se dentro ci sarà un form. Qui dentro si sa: <b>in tutta l'interfaccia non esiste un solo
/// <c>&lt;form method="post"&gt;</c> né un <c>&lt;EditForm&gt;</c></b> — l'unico form è la ricerca in barra,
/// che è <c>method="get"</c> — e login e logout sono richieste GET. Per un anonimo quel token non protegge
/// niente: non c'è niente che possa inviare. ⚠️ È un'affermazione sul codice, non una speranza: la tiene
/// ferma un test (<c>CacheDelleLettureAnonimeTests</c>) che diventa rosso il giorno in cui un form compare.</para>
///
/// <para><b>Perché un elenco di percorsi e non «tutto ciò che è anonimo».</b> Perché la regola dev'essere
/// leggibile e il raggio d'azione dev'essere quello voluto. In sviluppo l'identità è finta e non passa dal
/// <c>ClaimsPrincipal</c>: una regola scritta come «se non è autenticato» tratterebbe l'admin di sviluppo
/// come un anonimo, e la prima volta che qualcuno prova una schermata di amministrazione da un browser che
/// tiene le pagine in cache non capirebbe più niente.</para>
/// </summary>
internal static class CacheDelleLettureAnonime
{
    /// <summary>
    /// Un minuto. Non di più: queste pagine portano anche il conteggio degli ATC online, che è una
    /// fotografia al momento del render. Un minuto di ritardo su quel numero non inganna nessuno — e il
    /// pallino in barra resta vivo lo stesso, perché lo aggiorna lo stream, non l'HTML.
    ///
    /// <para>Non di meno perché il valore sta nel giorno della pubblicazione AIRAC, quando la stessa
    /// pagina viene chiesta da molte persone negli stessi minuti: sessanta secondi bastano a far assorbire
    /// quella folla al bordo invece che al processo.</para>
    /// </summary>
    private const int SecondiDiValidita = 60;

    /// <summary>
    /// I segmenti che <b>escludono</b> una pagina, ognuno per una ragione sua:
    /// <list type="bullet">
    ///   <item><c>/admin</c>, <c>/editor</c>, <c>/new-document</c>, <c>/pending</c>, <c>/versions</c>,
    ///         <c>/tasks</c> — non sono pubbliche: quel che mostrano dipende da chi guarda.</item>
    ///   <item><c>/live</c> — è viva per definizione.</item>
    ///   <item><c>/search</c>, <c>/changed</c> — dipendono dai permessi di chi cerca.</item>
    ///   <item><c>/auth</c> — login e logout non si tengono da parte, mai.</item>
    ///   <item><c>/stats/world</c> — l'archivio delle connessioni è roba di staff, ma vive sotto
    ///         <c>/services/stats</c> e non porta la parola <c>admin</c> nell'indirizzo: senza questa riga
    ///         sarebbe l'unica schermata di staff di cui si terrebbe una copia.</item>
    /// </list>
    /// </summary>
    private static readonly string[] SegmentiEsclusi =
    {
        "/admin", "/editor", "/new-document", "/pending", "/versions", "/tasks",
        "/live", "/search", "/changed", "/auth", "/stats/world",
    };

    public static IApplicationBuilder UseVipiCacheDelleLettureAnonime(this WebApplication app)
        => app.Use(async (context, next) =>
        {
            if (Riutilizzabile(context))
            {
                // OnStarting e non qui: il cookie non c'è ancora: lo scrive l'endpoint, che gira dopo
                // `next()`. Questa richiamata è l'ultimo momento in cui si può ancora toccare le
                // intestazioni — dopo, la risposta è già partita.
                context.Response.OnStarting(static stato =>
                {
                    var risposta = ((HttpContext)stato).Response;

                    // Solo le pagine, non i redirect e non gli errori: una risposta che non è la pagina
                    // non è la cosa di cui si è deciso che si può tenere una copia.
                    if (risposta.StatusCode != StatusCodes.Status200OK) return Task.CompletedTask;

                    ViaIlCookieAntiforgery(risposta);
                    risposta.Headers.CacheControl = $"public, max-age={SecondiDiValidita}";

                    // ⚠️ Vary: Cookie NON è decorativo. Senza, una cache condivisa potrebbe servire questa
                    // copia anonima a chi è entrato col proprio VID — che vedrebbe la pagina di un altro,
                    // senza i propri tasti. È la riga che rende innocuo tutto il resto.
                    risposta.Headers.Vary = "Accept-Encoding, Cookie";
                    return Task.CompletedTask;
                }, context);
            }

            await next();
        });

    /// <summary>
    /// Se di questa richiesta si può tenere una copia. Pubblico per i test: è una decisione con sette
    /// clausole, e ognuna è un modo diverso di sbagliare.
    /// </summary>
    internal static bool Riutilizzabile(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method)) return false;

        // Chi è entrato vede una pagina sua — col tasto «Modifica», col proprio nome in barra. Quella copia
        // non si tiene e non si presta a nessuno.
        if (context.User?.Identity?.IsAuthenticated == true) return false;
        if (context.Request.Cookies.Count > 0) return false;

        var percorso = context.Request.Path.Value;
        if (string.IsNullOrEmpty(percorso)) return false;
        if (!percorso.StartsWith("/services", StringComparison.OrdinalIgnoreCase)) return false;

        foreach (var escluso in SegmentiEsclusi)
            if (percorso.Contains(escluso, StringComparison.OrdinalIgnoreCase)) return false;

        // ⚠️ «?as=» è l'anteprima di una bozza o di una release non ancora effettiva: è materiale di
        // lavorazione, lo vede solo chi può modificare, e una copia tenuta da parte lo mostrerebbe a chi
        // arriva dopo con lo stesso indirizzo.
        if (context.Request.Query.ContainsKey("as")) return false;

        return true;
    }

    /// <summary>
    /// Toglie il <c>Set-Cookie</c> dell'antiforgery, lasciando gli altri se mai ce ne fossero.
    /// ⚠️ Si filtra per PREFISSO del nome del cookie e non si svuota l'intestazione: svuotarla vorrebbe
    /// dire che il giorno in cui qualcosa mettesse un cookie legittimo su una pagina pubblica lo si
    /// perderebbe in silenzio.
    /// </summary>
    private static void ViaIlCookieAntiforgery(HttpResponse risposta)
    {
        var cookie = risposta.Headers.SetCookie;
        if (cookie.Count == 0) return;

        var restano = cookie
            .Where(c => c is not null && !c.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal))
            .ToArray();

        if (restano.Length == cookie.Count) return;

        if (restano.Length == 0) risposta.Headers.Remove("Set-Cookie");
        else risposta.Headers.SetCookie = restano;
    }
}
