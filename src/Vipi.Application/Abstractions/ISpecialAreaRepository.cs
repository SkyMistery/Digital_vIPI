using Vipi.Application.Content;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Sola lettura delle aree speciali/regolamentate importate dalla sorgente (IVAO), condivisa da tutte le famiglie di
/// documento che le attaccano a una sezione <c>regulated</c>: vIPI ACC (blocchi) e vIPI APP non remotizzata. Vive fuori
/// da <see cref="IAccDerivationRepository"/> perché il dato è per-ACC ma il consumatore non è solo l'ACC.
/// </summary>
public interface ISpecialAreaRepository
{
    /// <summary>Aree speciali dell'ACC indicato (picker editor), ordinate per nome.</summary>
    Task<IReadOnlyList<SpecialAreaPick>> ListSpecialAreasByAccAsync(string accCode, CancellationToken ct = default);

    /// <summary>Aree speciali di TUTTI gli altri ACC (picker editor aree extra), ordinate per ACC poi nome.</summary>
    Task<IReadOnlyList<SpecialAreaPick>> ListSpecialAreasExcludingAccAsync(string accCode, CancellationToken ct = default);

    /// <summary>Aree speciali per IvaoId (per il viewer), con shape grezza.</summary>
    Task<IReadOnlyList<SpecialAreaDetail>> GetSpecialAreasByIdsAsync(IReadOnlyList<string> ivaoIds, CancellationToken ct = default);
}
