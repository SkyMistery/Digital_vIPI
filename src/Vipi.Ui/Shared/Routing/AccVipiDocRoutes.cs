using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Ui.Shared.Routing;

/// <summary>Rotte vIPI ACC (doc 09 §3b): unica per ACC, l'editor usa sempre il root primario (nessun ?tree).</summary>
public sealed class AccVipiDocRoutes : IDocKindRoutes
{
    public ManagedDocKind Kind => ManagedDocKind.AccVipi;
    public ReleaseTargetType Target => ReleaseTargetType.AccVipi;

    public string? ViewerUrl(string acc, string key, string? neighbourCode, int releaseId) =>
        $"/vsop/{acc}/vipi?as=rel:{releaseId}";

    public string? PublicUrl(string acc, string key, string? neighbourCode) => $"/vsop/{acc}/vipi";

    public string? EditorUrl(string acc, string key, string? neighbourCode, int? documentId) =>
        $"/vsop/{acc}/editor";
}
