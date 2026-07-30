using Vipi.Application.Content;

namespace Vipi.Application.Abstractions;

/// <summary>Porta (lettura+scrittura) ai coordinamenti strutturati di una ACC: flussi e loro punti. Impl. EF in Infrastructure.</summary>
public interface ITransferRepository
{
    /// <summary>Tutti i flussi di una ACC (per codice), coi loro punti, ordinati per settore/ordine. Vuoto se ACC assente.</summary>
    Task<IReadOnlyList<TransferFlowRow>> ListFlowsByAccAsync(string accCode, CancellationToken ct = default);

    Task<int> AddFlowAsync(string accCode, TransferFlowInput input, CancellationToken ct = default);
    Task UpdateFlowAsync(string accCode, int flowId, TransferFlowInput input, CancellationToken ct = default);
    Task DeleteFlowAsync(string accCode, int flowId, CancellationToken ct = default);

    Task<int> AddPointAsync(string accCode, int flowId, TransferPointInput input, CancellationToken ct = default);
    Task UpdatePointAsync(string accCode, int pointId, TransferPointInput input, CancellationToken ct = default);
    Task DeletePointAsync(string accCode, int pointId, CancellationToken ct = default);

    /// <summary>Sposta un punto su/giù scambiando l'<c>Order</c> col punto adiacente nello stesso flusso. No-op agli estremi.</summary>
    Task MovePointAsync(string accCode, int pointId, bool up, CancellationToken ct = default);

    /// <summary>Sposta un punto in cima (<paramref name="top"/>=true) o in fondo al suo flusso, ricompattando gli <c>Order</c>. No-op se già all'estremo.</summary>
    Task MovePointToEndAsync(string accCode, int pointId, bool top, CancellationToken ct = default);
}
