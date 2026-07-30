using Vipi.Application.Diagnostics;

namespace Vipi.Application.Abstractions;

/// <summary>Carica la fotografia di sola lettura dei dati con soft-ref, per il report di consistenza.</summary>
public interface IConsistencyReportRepository
{
    Task<ConsistencyDataset> LoadAsync(CancellationToken ct = default);
}
