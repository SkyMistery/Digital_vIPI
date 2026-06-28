using Vipi.Application.Aor;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Porta di scrittura della topologia: regole di unificazione + contenimento (padre) dei settori di una ACC.
/// L'anagrafica dei settori è sola lettura qui (CRUD in <see cref="IStructureEditingRepository"/>).
/// Tutti i metodi prendono il <c>accCode</c> per l'autorizzazione e verificano che l'entità appartenga a quella ACC.
/// </summary>
public interface ITopologyEditingRepository
{
    /// <summary>Carica settori (sola lettura), regole e contenimento di una ACC. Null se la ACC non esiste.</summary>
    Task<TopologyEditData?> LoadAsync(string accCode, CancellationToken ct = default);

    /// <summary>Vocabolario della ACC per la validazione: callsign dei settori (incl. neighbour). Null se ACC assente.</summary>
    Task<AccVocabulary?> GetVocabularyAsync(string accCode, CancellationToken ct = default);

    Task<int> AddRuleAsync(string accCode, string name, int priority, string conditionJson, string assignmentJson, CancellationToken ct = default);
    Task SetRuleActiveAsync(string accCode, int ruleId, bool active, CancellationToken ct = default);
    Task DeleteRuleAsync(string accCode, int ruleId, CancellationToken ct = default);

    /// <summary>Imposta il padre (contenimento) di un settore. <paramref name="parentSectorId"/> null = radice.</summary>
    Task SetParentAsync(string accCode, int childSectorId, int? parentSectorId, CancellationToken ct = default);
}

/// <summary>Vocabolario di una ACC per la validazione semantica delle regole.</summary>
public sealed class AccVocabulary
{
    public required IReadOnlySet<string> Callsigns { get; init; }    // callsign dei settori noti (qualsiasi ACC)
}
