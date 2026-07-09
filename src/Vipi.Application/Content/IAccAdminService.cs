namespace Vipi.Application.Content;

/// <summary>
/// Use-case di gestione ACC (admin-only). L'import scarica le posizioni center dalla sorgente
/// (porta neutra <see cref="Abstractions.IAccDirectory"/>) e fa upsert su ACC + settori CTR: il sito resta
/// agnostico dalla sorgente e contiene SOLO ciò che la sorgente fornisce. L'admin può nascondere singoli ACC.
/// </summary>
public interface IAccAdminService
{
    Task<IReadOnlyList<AccAdminRow>> ListAccsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AccSectorRow>> ListSubcentersAsync(CancellationToken ct = default);
    Task<AccImportResult> ImportFromSourceAsync(CancellationToken ct = default);
    Task SetHiddenAsync(int accId, bool hidden, CancellationToken ct = default);
    Task SetSubcenterHiddenAsync(int id, bool hidden, CancellationToken ct = default);
    Task SetSubcenterLimitsAsync(int id, int? lower, int? upper, CancellationToken ct = default);
}
