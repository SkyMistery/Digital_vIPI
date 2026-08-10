using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Routing;

/// <summary>Rotte APP standalone (doc 09 §3b): keyed sul callsign dell'APP.</summary>
public sealed class AppDocRoutes : IDocKindRoutes
{
    public ManagedDocKind Kind => ManagedDocKind.AppVipi;
    public ReleaseTargetType Target => ReleaseTargetType.App;

    public string? ViewerUrl(string acc, string key, string? neighbourCode, int releaseId) =>
        $"/vsop/{acc}/apps/vipi?app={key}&as=rel:{releaseId}";

    public string? PublicUrl(string acc, string key, string? neighbourCode) => $"/vsop/{acc}/apps/vipi?app={key}";

    public string? EditorUrl(string acc, string key, string? neighbourCode, int? documentId) =>
        $"/vsop/{acc}/apps/editor?app={key}";
}
