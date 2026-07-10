using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Ui.Shared.Routing;

/// <inheritdoc cref="IDocRoutesRegistry"/>
public sealed class DocRoutesRegistry : IDocRoutesRegistry
{
    private readonly Dictionary<ManagedDocKind, IDocKindRoutes> _byKind;
    private readonly Dictionary<ReleaseTargetType, IDocKindRoutes> _byTarget;

    public DocRoutesRegistry(IEnumerable<IDocKindRoutes> routes)
    {
        var list = routes.ToList();
        _byKind = list.ToDictionary(r => r.Kind);
        _byTarget = list.ToDictionary(r => r.Target);
    }

    public IDocKindRoutes For(ManagedDocKind kind) => _byKind[kind];

    public IDocKindRoutes For(ReleaseTargetType target) => _byTarget[target];
}
