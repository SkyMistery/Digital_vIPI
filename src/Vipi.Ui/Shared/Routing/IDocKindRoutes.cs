using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Ui.Shared.Routing;

/// <summary>
/// Rotte viewer/editor per tipo di documento (doc 09 §3b). Isola la conoscenza per-tipo delle URL, prima duplicata
/// tra <c>VersioniPage.PreviewLink/EditorLink</c> e <c>ReleasePreviewPage</c>. I consumatori consultano il registry:
/// aggiungere un tipo = registrare un descrittore di rotte, nessuno switch di URL toccato.
/// </summary>
public interface IDocKindRoutes
{
    ManagedDocKind Kind { get; }
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
}
