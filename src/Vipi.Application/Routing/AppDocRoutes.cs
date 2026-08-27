using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Routing;

/// <summary>Rotte APP standalone (doc 09 §3b): keyed sul callsign dell'APP.</summary>
public sealed class AppDocRoutes : IDocKindRoutes
{
    public ReleaseTargetType Target => ReleaseTargetType.App;

    public string? ViewerUrl(string acc, string key, string? neighbourCode, int releaseId) =>
        $"/services/vsop/{acc}/apps/vipi?app={key}&as=rel:{releaseId}";

    public string? PublicUrl(string acc, string key, string? neighbourCode) => $"/services/vsop/{acc}/apps/vipi?app={key}";

    public string? EditorUrl(string acc, string key, string? neighbourCode, int? documentId) =>
        $"/services/vsop/{acc}/apps/editor?app={key}";

    public string? DraftUrl(string acc, string key, string? neighbourCode) =>
        $"/services/vsop/{acc}/apps/vipi?app={key}&as=draft";
}
