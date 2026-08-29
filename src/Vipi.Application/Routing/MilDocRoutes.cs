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
///
/// <para>
/// ⚠️ <b>OGGI TORNANO TUTTE NULL, ed è la cosa giusta.</b> Le pagine <c>/services/vsop/{acc}/mil/apps</c> e
/// <c>.../mil/apps/editor</c> <b>non esistono</b>, e nessuna porta crea un documento <c>AppMil</c>: il
/// profilo, il descrittore di release e il valore d'enum ci sono (carta vSOP militari §1c, §4), il
/// documento no. Un indirizzo restituito qui sarebbe una <b>promessa falsa</b> — è esattamente il difetto
/// che questa famiglia ha già pagato una volta, quando <c>MilDocRoutes</c> dichiarava un <c>EditorUrl</c>
/// verso una pagina che non era stata scritta (§6 della carta).
/// </para>
/// <para>
/// <c>null</c> è un valore previsto dal contratto e i chiamanti lo trattano già — <c>EfChangesRepository</c>
/// fa <c>if (url is null) continue;</c>, la vLOA lo restituisce da sempre quando manca il vicino. Quindi il
/// descrittore resta <b>registrato</b> (così <c>DocRoutes.For(AppMil)</c> non esplode) e non mente.
/// </para>
/// <para>
/// ⚠️ <b>Quando le pagine si scriveranno</b>, di qui passano quattro indirizzi <i>e</i> tre cose che oggi
/// sono deliberatamente assenti: l'<c>.Include(d =&gt; d.MilSectors)</c> nei tre elenchi generici
/// (<c>EfDocumentAdminRepository</c>), un <c>IFrozenSectionProvider</c> per <c>AppMil</c>, e la voce nel
/// conteggio di <c>RegistrazioniPerFamigliaTests</c>.
/// </para>
/// </summary>
public sealed class AppMilDocRoutes : IDocKindRoutes
{
    public ReleaseTargetType Target => ReleaseTargetType.AppMil;

    public string? ViewerUrl(string acc, string key, string? neighbourCode, int releaseId) => null;

    public string? PublicUrl(string acc, string key, string? neighbourCode) => null;

    public string? EditorUrl(string acc, string key, string? neighbourCode, int? documentId) => null;

    public string? DraftUrl(string acc, string key, string? neighbourCode) => null;
}
