using Vipi.Domain;

namespace Vipi.Application.Routing;

/// <summary>
/// Rotte della vSOP MILITARE d'aeroporto (carta <c>docs/feature/2026-08-27-vsop-militari.md</c> §4-5):
/// keyed sull'ICAO come il gemello civile, sotto un segmento <c>/mil</c> che le tiene separate.
///
/// <para>
/// ⚠️ <b>Non è la stessa pagina con un parametro.</b> Le due edizioni hanno release, cicli AIRAC e
/// contenuti indipendenti: condividere l'indirizzo vorrebbe dire che un collegamento salvato da qualcuno
/// porta a un documento diverso a seconda di come è stato costruito.
/// </para>
/// </summary>
public sealed class AirportMilDocRoutes : IDocKindRoutes
{
    public ReleaseTargetType Target => ReleaseTargetType.AirportMil;

    public string? ViewerUrl(string acc, string key, string? neighbourCode, int releaseId) =>
        $"/services/vsop/{acc}/mil?icao={key}&as=rel:{releaseId}";

    public string? PublicUrl(string acc, string key, string? neighbourCode) =>
        $"/services/vsop/{acc}/mil?icao={key}";

    public string? EditorUrl(string acc, string key, string? neighbourCode, int? documentId) =>
        $"/services/vsop/{acc}/mil/editor?icao={key}";

    public string? DraftUrl(string acc, string key, string? neighbourCode) =>
        $"/services/vsop/{acc}/mil?icao={key}&as=draft";
}

/// <summary>
/// Rotte della vSOP militare di un APP <b>non remotizzato</b>: keyed sul callsign, come il gemello civile.
/// </summary>
public sealed class AppMilDocRoutes : IDocKindRoutes
{
    public ReleaseTargetType Target => ReleaseTargetType.AppMil;

    public string? ViewerUrl(string acc, string key, string? neighbourCode, int releaseId) =>
        $"/services/vsop/{acc}/mil/apps?cs={key}&as=rel:{releaseId}";

    public string? PublicUrl(string acc, string key, string? neighbourCode) =>
        $"/services/vsop/{acc}/mil/apps?cs={key}";

    public string? EditorUrl(string acc, string key, string? neighbourCode, int? documentId) =>
        $"/services/vsop/{acc}/mil/apps/editor?cs={key}";

    public string? DraftUrl(string acc, string key, string? neighbourCode) =>
        $"/services/vsop/{acc}/mil/apps?cs={key}&as=draft";
}
