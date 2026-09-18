using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Routing;

namespace Vipi.Host;

/// <summary>
/// Una riga per ogni richiesta servita, in <c>diagnostica/richieste-AAAA-MM-GG.tsv</c>: ora, processo, versione, rotta,
/// percorso, esito, millisecondi, se chi chiedeva era autenticato. Sette giorni, poi i file si cancellano da soli
/// (<see cref="RegistroGiornaliero"/>). Si legge con <c>python tools/registro-del-giorno.py</c>.
///
/// <para><b>Perché esiste</b> (§A59, chiesto dal committente il 17 settembre 2026). Gli altri file di
/// <c>diagnostica/</c> raccontano i guasti; nessuno racconta <b>come va il sistema quando non si rompe</b>: quanto ci mette
/// ogni pagina, quanto restano aperti i circuiti. Senza una misura continua, l'ottimizzazione si fa per ipotesi.</para>
///
/// <para>⚠️ <b>Due connessioni sono LUNGHE per costruzione</b> e i loro millisecondi sono una vita, non un tempo di
/// risposta: <c>GET /_blazor</c> (101, il circuito) e <c>GET /vsop/live/atc</c> (lo stream SSE della vista live). Il
/// primo <c>avvisi-log.txt</c> di produzione mostrava <c>/vsop/live/atc → 200 50045 ms</c>, e sembrava una pagina lenta:
/// la prova dal vivo del 17 settembre 2026 l'ha ridato identico alla vita della pagina. Lo script li tiene a parte.</para>
///
/// <para>⚠️ <b>La rotta dall'endpoint, non dal percorso.</b> <c>/services/vsop/airport/LIRF</c> e <c>…/LIMC</c> sono la
/// stessa pagina: il modello dell'endpoint (<c>RoutePattern.RawText</c>) li raggruppa senza indovinare con le regex.</para>
///
/// <para>⚠️ <b>Che cosa NON entra.</b> La query mai (su <c>/signin-oidc</c> è una credenziale), il VID mai (basta
/// «autenticato»). Ping, file statici e meccanica del circuito si escludono con la stessa regola di
/// <see cref="RegistroAvvisi.DaNonRicordare"/>; <c>GET /_blazor</c> (101) resta, e i suoi millisecondi sono la vita del
/// circuito.</para>
///
/// <para>⚠️ La riga si scrive <b>a risposta finita</b> (<c>Response.OnCompleted</c>): la scrittura non ruba tempo a chi
/// aspetta la pagina.</para>
/// </summary>
public sealed class RegistroRichieste
{
    public const string Prefisso = "richieste";
    public const string Estensione = "tsv";

    internal const string Colonne = "ora\tpid\tversione\tmetodo\trotta\tpercorso\tesito\tms\tautenticato";

    private readonly Action<DateTime, string> _scrivi;
    private readonly string _versione;

    /// <summary>Quello vero: file del giorno in <c>diagnostica/</c>.</summary>
    public RegistroRichieste() : this(Scrittore().Scrivi, VersioneBuild.Leggi().Etichetta) { }

    internal RegistroRichieste(Action<DateTime, string> scrivi, string versione)
    {
        _scrivi = scrivi;
        _versione = versione;
    }

    private static RegistroGiornaliero Scrittore() =>
        new(Prefisso, Estensione, Intestazione, RegistroGiornaliero.CartellaVera);

    /// <summary>Il pezzo di pipeline. Va messo in testa, perché i millisecondi contino tutto il resto.</summary>
    public async Task Misura(HttpContext context, Func<Task> next)
    {
        var percorso = context.Request.Path.Value ?? "/";
        if (DaNonRicordare(percorso))
        {
            await next();
            return;
        }

        var inizio = Stopwatch.GetTimestamp();
        context.Response.OnCompleted(() =>
        {
            try
            {
                if (PollingVuoto(percorso, context.Response.StatusCode)) return Task.CompletedTask;

                var ms = Stopwatch.GetElapsedTime(inizio).TotalMilliseconds;
                var ora = DateTime.UtcNow;
                var rotta = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
                _scrivi(ora, Riga(ora, Environment.ProcessId, _versione, context.Request.Method,
                    rotta, context.Request.PathBase.Value + percorso, context.Response.StatusCode, ms,
                    context.User.Identity?.IsAuthenticated == true));
            }
            catch { /* una riga persa non è un guasto */ }
            return Task.CompletedTask;
        });

        await next();
    }

    internal static bool DaNonRicordare(string percorso) => RegistroAvvisi.DaNonRicordare(percorso);

    /// <summary>
    /// Il «niente di nuovo» del ponte RFO: ogni postazione chiede ogni tre secondi, e con dieci postazioni sono
    /// dodicimila righe l'ora tutte uguali, che riempirebbero il tetto del file a metà evento e zittirebbero il resto
    /// del giorno (committente, 18 settembre 2026). Restano le letture col corpo, le scritture, i 409 e gli errori.
    /// </summary>
    internal static bool PollingVuoto(string percorso, int esito) =>
        esito == StatusCodes.Status304NotModified
        && percorso.StartsWith(Vipi.Hosting.PonteRfo.PrefissoRotta, StringComparison.OrdinalIgnoreCase);

    /// <summary>La riga, separata dall'I/O perché si provi da sola. I tab e gli a capo nei valori diventano spazi.</summary>
    internal static string Riga(DateTime ora, int pid, string versione, string metodo, string? rotta, string percorso,
        int esito, double ms, bool autenticato)
    {
        static string C(string? s) => string.IsNullOrEmpty(s) ? "-" : s.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        var p = percorso.Length <= 160 ? percorso : percorso[..160] + "…";
        return string.Join('\t',
            ora.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
            pid.ToString(CultureInfo.InvariantCulture),
            C(versione), C(metodo), C(rotta), C(p),
            esito.ToString(CultureInfo.InvariantCulture),
            Math.Round(ms).ToString("0", CultureInfo.InvariantCulture),
            autenticato ? "1" : "0");
    }

    private static string Intestazione() =>
        $"""
        # vIPI — richieste servite, una riga per richiesta (§A59). Orari UTC; il giorno è nel nome del file.
        # Si legge con: python tools/registro-del-giorno.py <cartella diagnostica>
        #
        # rotta = il modello della pagina ({"{Icao}"} al posto dell'aeroporto), «-» se nessuna (404). percorso senza query.
        # ora = quando la risposta è FINITA (l'inizio è ora - ms). ms = dall'arrivo alla fine della risposta.
        # ⚠️ Per GET /_blazor (esito 101, il circuito) e GET /vsop/live/atc (stream SSE) ms è la VITA della connessione.
        # Ping (/vsop/health, /vsop/ping), file statici, /_blazor/* e i 304 del ponte RFO non si scrivono. pid: due processi vivi insieme succedono.
        # Il file si tiene {RegistroGiornaliero.GiorniTenuti} giorni; oltre {RegistroGiornaliero.TettoByte / 1024 / 1024} MB il resto del giorno tace.
        {Colonne}

        """.Replace("\r\n", "\n");
}
