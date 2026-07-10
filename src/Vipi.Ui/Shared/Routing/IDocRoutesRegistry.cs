using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Ui.Shared.Routing;

/// <summary>Registro dei descrittori di rotta <see cref="IDocKindRoutes"/>, uno per tipo (doc 09 §3b).</summary>
public interface IDocRoutesRegistry
{
    IDocKindRoutes For(ManagedDocKind kind);
    IDocKindRoutes For(ReleaseTargetType target);
}
