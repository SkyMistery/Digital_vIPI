namespace Vipi.Application.Content;

/// <summary>Use-case coordinamenti/trasferimenti: lettura aperta, scrittura ACC-gated + validazione base (soft).</summary>
public interface ITransferService
{
    /// <summary>Flussi (coi punti) di una ACC.</summary>
    Task<IReadOnlyList<TransferFlowRow>> ListFlowsByAccAsync(string accCode, CancellationToken ct = default);

    /// <summary>Flussi della ACC risolti live (vista operativa): mittente e ricevente risalgono la gerarchia
    /// di copertura globale in base a chi è <paramref name="online"/>; terminale = UNICOM.</summary>
    Task<IReadOnlyList<ResolvedTransferFlow>> ResolveForAccAsync(
        string accCode, IReadOnlySet<string> online, CancellationToken ct = default);

    Task<int> AddFlowAsync(string accCode, TransferFlowInput input, CancellationToken ct = default);
    Task UpdateFlowAsync(string accCode, int flowId, TransferFlowInput input, CancellationToken ct = default);
    Task DeleteFlowAsync(string accCode, int flowId, CancellationToken ct = default);

    Task<int> AddPointAsync(string accCode, int flowId, TransferPointInput input, CancellationToken ct = default);
    Task UpdatePointAsync(string accCode, int pointId, TransferPointInput input, CancellationToken ct = default);
    Task DeletePointAsync(string accCode, int pointId, CancellationToken ct = default);

    /// <summary>Sposta un punto su/giù nel suo flusso (scambio Order col vicino). No-op agli estremi.</summary>
    Task MovePointAsync(string accCode, int pointId, bool up, CancellationToken ct = default);
}
