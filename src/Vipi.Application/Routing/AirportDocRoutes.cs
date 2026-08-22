using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Routing;

/// <summary>Rotte vIPI aeroporto (doc 09 §3b): keyed sull'ICAO.</summary>
public sealed class AirportDocRoutes : IDocKindRoutes
{
    public ManagedDocKind Kind => ManagedDocKind.AirportVipi;
    public ReleaseTargetType Target => ReleaseTargetType.Airport;

    public string? ViewerUrl(string acc, string key, string? neighbourCode, int releaseId) =>
        $"/services/vsop/{acc}/airports?icao={key}&as=rel:{releaseId}";

    public string? PublicUrl(string acc, string key, string? neighbourCode) => $"/services/vsop/{acc}/airports?icao={key}";

    public string? EditorUrl(string acc, string key, string? neighbourCode, int? documentId) =>
        $"/services/vsop/{acc}/airports/editor?icao={key}";
}
