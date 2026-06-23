using Vipi.Application.Aor;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Porta di scrittura della topologia: regole di unificazione + relazioni gerarchiche di una FIR.
/// L'anagrafica (posizioni/settori) è sola lettura (viene dalle API IVAO). Impl. EF in Infrastructure.
/// Tutti i metodi prendono il <c>firCode</c> per l'autorizzazione e verificano che l'entità appartenga a quella FIR.
/// </summary>
public interface ITopologyEditingRepository
{
    /// <summary>Carica posizioni (sola lettura), regole e gerarchia di una FIR. Null se la FIR non esiste.</summary>
    Task<TopologyEditData?> LoadAsync(string firCode, CancellationToken ct = default);

    /// <summary>Vocabolario della FIR per la validazione: chiavi settore + callsign posizioni (incl. neighbour). Null se FIR assente.</summary>
    Task<FirVocabulary?> GetVocabularyAsync(string firCode, CancellationToken ct = default);

    Task<int> AddRuleAsync(string firCode, string name, int priority, string conditionJson, string assignmentJson, CancellationToken ct = default);
    Task SetRuleActiveAsync(string firCode, int ruleId, bool active, CancellationToken ct = default);
    Task DeleteRuleAsync(string firCode, int ruleId, CancellationToken ct = default);

    Task<int> AddHierarchyAsync(string firCode, int parentPositionId, int childPositionId, CancellationToken ct = default);
    Task DeleteHierarchyAsync(string firCode, int relationId, CancellationToken ct = default);
}

/// <summary>Vocabolario di una FIR per la validazione semantica delle regole.</summary>
public sealed class FirVocabulary
{
    public required IReadOnlySet<string> SectorKeys { get; init; }   // es. "LIRR-NE"
    public required IReadOnlySet<string> Callsigns { get; init; }    // tutte le posizioni note (qualsiasi FIR)
}
