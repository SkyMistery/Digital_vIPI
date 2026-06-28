using Vipi.Application.Content;

namespace Vipi.Application.Abstractions;

/// <summary>Porta di accesso (lettura+scrittura) ai trasferimenti strutturati di una ACC. Impl. EF in Infrastructure.</summary>
public interface ITransferRepository
{
    /// <summary>Tutti i trasferimenti di una ACC (per codice), ordinati per relazione/fase/ordine. Vuoto se ACC assente.</summary>
    Task<IReadOnlyList<TransferRow>> ListByAccAsync(string accCode, CancellationToken ct = default);

    Task<int> AddAsync(string accCode, TransferInput input, CancellationToken ct = default);
    Task UpdateAsync(string accCode, int id, TransferInput input, CancellationToken ct = default);
    Task DeleteAsync(string accCode, int id, CancellationToken ct = default);
}
