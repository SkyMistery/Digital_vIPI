namespace Vipi.Application.Content;

/// <summary>Use-case coordinamenti/trasferimenti: lettura aperta, scrittura ACC-gated + validazione base (soft).</summary>
public interface ITransferService
{
    /// <summary>Flussi (coi punti) di una ACC.</summary>
    Task<IReadOnlyList<TransferFlowRow>> ListFlowsByAccAsync(string accCode, CancellationToken ct = default);

    /// <summary>Flussi della ACC risolti live (vista live): mittente e ricevente risalgono la gerarchia
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

    /// <summary>Sposta un punto in cima o in fondo al suo flusso (ricompattando gli Order).</summary>
    Task MovePointToEndAsync(string accCode, int pointId, bool top, CancellationToken ct = default);

    /// <summary>Sposta un punto (col suo sottoalbero) dove sta un altro punto: il gesto del trascinamento.</summary>
    Task MovePointToAsync(string accCode, int pointId, int targetPointId, CancellationToken ct = default);

    /// <summary>Alternativa pari-grado alla riga (es. «pista 25» accanto a «pista 07»), dopo il suo sottoalbero.</summary>
    Task<int> AddAlternativeAsync(string accCode, int pointId, CancellationToken ct = default);

    /// <summary>Eccezione della riga: un livello più dentro, subito sotto.</summary>
    Task<int> AddExceptionAsync(string accCode, int pointId, CancellationToken ct = default);

    /// <summary>Duplica il gruppo di varianti della riga, con la sua struttura. Ritorna quante righe ha creato.</summary>
    Task<int> DuplicateVariantGroupAsync(string accCode, int pointId, CancellationToken ct = default);

    /// <summary>Cambia il ricevente di più righe in un colpo. Ritorna quante righe ha toccato.</summary>
    Task<int> SetReceiverAsync(string accCode, IReadOnlyList<int> pointIds, int? nextSectorId, CancellationToken ct = default);

    /// <summary>Sfila la riga col suo sottoalbero dal gruppo; scioglie ciò che resta di un gruppo di uno.</summary>
    Task DetachVariantAsync(string accCode, int pointId, CancellationToken ct = default);
}
