using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Routing;

/// <summary>Registro dei descrittori di rotta <see cref="IDocKindRoutes"/>, uno per tipo (doc 09 §3b).</summary>
public interface IDocRoutesRegistry
{
    /// <summary>Rotte per il tipo di documento. ⚠️ Erano due metodi identici, uno per ciascuno dei due enum
    /// paralleli (doc 14 §3h).</summary>
    IDocKindRoutes For(ReleaseTargetType type);
}
