using Vipi.Domain;

namespace Vipi.Application.Routing;

/// <inheritdoc cref="IDocRoutesRegistry"/>
public sealed class DocRoutesRegistry : IDocRoutesRegistry
{
    private readonly Dictionary<ReleaseTargetType, IDocKindRoutes> _byTarget;

    public DocRoutesRegistry(IEnumerable<IDocKindRoutes> routes) =>
        _byTarget = routes.ToDictionary(r => r.Target);

    public IDocKindRoutes For(ReleaseTargetType type) => _byTarget[type];
}
