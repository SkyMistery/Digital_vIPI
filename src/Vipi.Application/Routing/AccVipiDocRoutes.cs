using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Routing;

/// <summary>Rotte vIPI ACC (doc 09 §3b): unica per ACC, l'editor usa sempre il root primario (nessun ?tree).</summary>
public sealed class AccVipiDocRoutes : IDocKindRoutes
{
    public ReleaseTargetType Target => ReleaseTargetType.AccVipi;

    public string? ViewerUrl(string acc, string key, string? neighbourCode, int releaseId) =>
        $"/services/vsop/{acc}/vipi?as=rel:{releaseId}";

    public string? PublicUrl(string acc, string key, string? neighbourCode) => $"/services/vsop/{acc}/vipi";

    public string? EditorUrl(string acc, string key, string? neighbourCode, int? documentId) =>
        $"/services/vsop/{acc}/editor";

    public string? DraftUrl(string acc, string key, string? neighbourCode) => $"/services/vsop/{acc}/vipi?as=draft";
}
