using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Abstractions;

/// <summary>Registro dei descrittori <see cref="IReleaseTarget"/>, uno per tipo. Consultato dai motori generici
/// del flusso di pubblicazione al posto degli switch per-tipo (doc 09 §3a).</summary>
public interface IReleaseTargetRegistry
{
    /// <summary>Descrittore per il tipo di documento. Lancia se il tipo non è registrato.
    /// <para>⚠️ Erano DUE metodi identici — uno «per il tipo di release», l'altro «per il tipo dell'elenco
    /// unificato» — perché i due enum erano due (doc 14 §3h). Erano lo stesso insieme con nomi diversi.</para></summary>
    IReleaseTarget For(ReleaseTargetType type);

    /// <summary>Descrittori ordinati per <see cref="IReleaseTarget.DescribeOrder"/> (per l'attribuzione shape→tipo).</summary>
    IReadOnlyList<IReleaseTarget> ByDescribeOrder { get; }
}
