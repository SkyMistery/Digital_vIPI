using Vipi.Application.Aor;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Porta di scrittura della topologia: regole di unificazione + contenimento (padre) dei settori di una FIR.
/// L'anagrafica dei settori è sola lettura qui (CRUD in <see cref="IStructureEditingRepository"/>).
/// Tutti i metodi prendono il <c>firCode</c> per l'autorizzazione e verificano che l'entità appartenga a quella FIR.
/// </summary>
public interface ITopologyEditingRepository
{
    /// <summary>Carica settori (sola lettura), regole e contenimento di una FIR. Null se la FIR non esiste.</summary>
    Task<TopologyEditData?> LoadAsync(string firCode, CancellationToken ct = default);

    /// <summary>Vocabolario della FIR per la validazione: callsign dei settori (incl. neighbour). Null se FIR assente.</summary>
    Task<FirVocabulary?> GetVocabularyAsync(string firCode, CancellationToken ct = default);

    Task<int> AddRuleAsync(string firCode, string name, int priority, string conditionJson, string assignmentJson, CancellationToken ct = default);
    Task SetRuleActiveAsync(string firCode, int ruleId, bool active, CancellationToken ct = default);
    Task DeleteRuleAsync(string firCode, int ruleId, CancellationToken ct = default);

    /// <summary>Imposta il padre (contenimento) di un settore. <paramref name="parentSectorId"/> null = radice.</summary>
    Task SetParentAsync(string firCode, int childSectorId, int? parentSectorId, CancellationToken ct = default);
}

/// <summary>Vocabolario di una FIR per la validazione semantica delle regole.</summary>
public sealed class FirVocabulary
{
    public required IReadOnlySet<string> Callsigns { get; init; }    // callsign dei settori noti (qualsiasi FIR)
}
