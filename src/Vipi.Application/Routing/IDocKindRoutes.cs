using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Routing;

/// <summary>
/// Rotte viewer/editor per tipo di documento (doc 09 §3b). Isola la conoscenza per-tipo delle URL, prima duplicata
/// tra <c>VersioniPage.PreviewLink/EditorLink</c> e <c>ReleasePreviewPage</c>. I consumatori consultano il registry:
/// aggiungere un tipo = registrare un descrittore di rotte, nessuno switch di URL toccato.
/// </summary>
public interface IDocKindRoutes
{
    /// <summary>Il tipo di documento a cui queste rotte appartengono.</summary>
    ReleaseTargetType Target { get; }

    /// <summary>URL del viewer tipizzato in anteprima release (<c>?as=rel:{id}</c>). <paramref name="acc"/> minuscolo,
    /// <paramref name="key"/> = chiave di release, <paramref name="neighbourCode"/> serve solo alle vLOA.
    /// null se non risolvibile (es. vLOA senza vicino): il chiamante applica il proprio fallback.</summary>
    string? ViewerUrl(string acc, string key, string? neighbourCode, int releaseId);

    /// <summary>URL del viewer PUBBLICO (nessuna anteprima): il documento come lo vede chiunque.
    /// Stessi parametri di <see cref="ViewerUrl"/> senza la release. null se non risolvibile.</summary>
    string? PublicUrl(string acc, string key, string? neighbourCode);

    /// <summary>URL dell'editor del tipo. null se non risolvibile; il chiamante applica il proprio fallback.</summary>
    string? EditorUrl(string acc, string key, string? neighbourCode, int? documentId);

    /// <summary>
    /// URL del viewer in anteprima BOZZA (<c>?as=draft</c>): il documento come sarà, prima di pubblicarlo.
    /// <para>
    /// ⚠️ È la forma che le pagine usano di più — ogni editor ha il suo «vedi la bozza» — ed era l'unica che
    /// questo registro non conosceva: i quattro editor se la componevano a mano, ognuno con la propria
    /// stringa. Il registro esiste per togliere dalle pagine la conoscenza delle URL (doc 09 §3b), e lasciarne
    /// fuori proprio quella la rendeva una promessa a metà (doc 14 §3h).
    /// </para>
    /// </summary>
    string? DraftUrl(string acc, string key, string? neighbourCode);
}
