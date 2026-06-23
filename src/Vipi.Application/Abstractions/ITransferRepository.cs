using Vipi.Application.Content;

namespace Vipi.Application.Abstractions;

/// <summary>Porta di accesso (lettura+scrittura) ai trasferimenti strutturati di una FIR. Impl. EF in Infrastructure.</summary>
public interface ITransferRepository
{
    /// <summary>Tutti i trasferimenti di una FIR (per codice), ordinati per relazione/fase/ordine. Vuoto se FIR assente.</summary>
    Task<IReadOnlyList<TransferRow>> ListByFirAsync(string firCode, CancellationToken ct = default);

    Task<int> AddAsync(string firCode, TransferInput input, CancellationToken ct = default);
    Task UpdateAsync(string firCode, int id, TransferInput input, CancellationToken ct = default);
    Task DeleteAsync(string firCode, int id, CancellationToken ct = default);
}
