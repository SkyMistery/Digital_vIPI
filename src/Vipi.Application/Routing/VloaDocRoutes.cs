using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Routing;

/// <summary>Rotte vLOA (doc 09 §3b): editate/anteprima per coppia Home↔vicino, keyed sul codice ACC vicino.</summary>
public sealed class VloaDocRoutes : IDocKindRoutes
{
    public ManagedDocKind Kind => ManagedDocKind.Vloa;
    public ReleaseTargetType Target => ReleaseTargetType.Vloa;

    public string? ViewerUrl(string acc, string key, string? neighbourCode, int releaseId) =>
        neighbourCode is { Length: > 0 } n
            ? $"/services/vsop/{acc}/vloa?acc={n.ToUpperInvariant()}&as=rel:{releaseId}"
            : null;

    public string? PublicUrl(string acc, string key, string? neighbourCode) =>
        neighbourCode is { Length: > 0 } n ? $"/services/vsop/{acc}/vloa?acc={n.ToUpperInvariant()}" : null;

    // Senza il vicino la coppia non è identificabile: si torna null e il chiamante applica il suo fallback.
    // Il ripiego storico era «/services/vsop/{acc}/editor?doc={id}», che è l'editor della vIPI ACC e ignora ?doc:
    // portava l'editore su un documento di un'altra famiglia.
    public string? EditorUrl(string acc, string key, string? neighbourCode, int? documentId) =>
        neighbourCode is { Length: > 0 } n ? $"/services/vsop/{acc}/vloa/editor?acc={n.ToUpperInvariant()}" : null;
}
