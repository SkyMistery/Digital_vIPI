using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Ui.Shared.Routing;

/// <summary>Rotte vLOA (doc 09 §3b): editate/anteprima per coppia Home↔vicino, keyed sul codice ACC vicino.</summary>
public sealed class VloaDocRoutes : IDocKindRoutes
{
    public ManagedDocKind Kind => ManagedDocKind.Vloa;
    public ReleaseTargetType Target => ReleaseTargetType.Vloa;

    public string? ViewerUrl(string acc, string key, string? neighbourCode, int releaseId) =>
        neighbourCode is { Length: > 0 } n
            ? $"/vsop/{acc}/vloa?acc={n.ToUpperInvariant()}&as=rel:{releaseId}"
            : null;

    public string? EditorUrl(string acc, string key, string? neighbourCode, int? documentId) =>
        neighbourCode is { Length: > 0 } n
            ? $"/vsop/{acc}/vloa/editor?acc={n.ToUpperInvariant()}"
            : documentId is int id ? $"/vsop/{acc}/editor?doc={id}" : null;
}
