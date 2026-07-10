using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Abstractions;

/// <summary>Registro dei descrittori <see cref="IReleaseTarget"/>, uno per tipo. Consultato dai motori generici
/// del flusso di pubblicazione al posto degli switch per-tipo (doc 09 §3a).</summary>
public interface IReleaseTargetRegistry
{
    /// <summary>Descrittore per il tipo di release. Lancia se il tipo non è registrato.</summary>
    IReleaseTarget For(ReleaseTargetType type);

    /// <summary>Descrittore per il tipo dell'elenco unificato. Lancia se il tipo non è registrato.</summary>
    IReleaseTarget For(ManagedDocKind kind);

    /// <summary>Descrittori ordinati per <see cref="IReleaseTarget.DescribeOrder"/> (per l'attribuzione shape→tipo).</summary>
    IReadOnlyList<IReleaseTarget> ByDescribeOrder { get; }
}
