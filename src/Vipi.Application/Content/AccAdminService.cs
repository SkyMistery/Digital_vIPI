using Vipi.Application.Abstractions;
using Vipi.Application.Auth;

namespace Vipi.Application.Content;

/// <summary>Riga ACC (center area) per la pagina di gestione: codice, nome, militare, nascosto.</summary>
public sealed record AccAdminRow(int Id, string Code, string Name, bool IsMilitary, bool IsHidden);

/// <summary>
/// Riga settore ACC (subcenter) per la pagina: chiave naturale + dati sorgente + limiti admin.
/// <paramref name="IsHidden"/> = flag proprio; <paramref name="AccHidden"/> = ACC di appartenenza nascosto
/// (un settore è effettivamente nascosto se IsHidden o AccHidden).
/// </summary>
public sealed record AccSectorRow(
    int Id, string ComposePosition, string CenterId, string? Position, string? MiddleIdentifier,
    string? Frequency, int? LowerLimit, int? UpperLimit, bool IsHidden, bool HasPolygon, bool AccHidden);

/// <summary>Esito dell'import ACC + settori ATC dalla sorgente.</summary>
public sealed record AccImportResult(int AccsCreated, int AccsUpdated, int SubcentersCreated, int SubcentersUpdated);

/// <summary>
/// Use-case di gestione ACC (admin-only). L'import scarica le posizioni center dalla sorgente
/// (porta neutra <see cref="IAccDirectory"/>) e fa upsert su ACC + settori CTR: il sito resta agnostico
/// dalla sorgente e contiene SOLO ciò che la sorgente fornisce. L'admin può nascondere singoli ACC.
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

/// <inheritdoc cref="IAccAdminService"/>
public sealed class AccAdminService : IAccAdminService
{
    private readonly IAccAdminRepository _repo;
    private readonly IAccImportUseCase _import;
    private readonly IEditAuthorizationService _authz;
    private readonly ISectorProjectionService _projection;

    public AccAdminService(IAccAdminRepository repo, IAccImportUseCase import, IEditAuthorizationService authz,
        ISectorProjectionService projection)
    {
        _repo = repo;
        _import = import;
        _authz = authz;
        _projection = projection;
    }

    public Task<IReadOnlyList<AccAdminRow>> ListAccsAsync(CancellationToken ct = default) => _repo.ListAccsAsync(ct);

    public Task<IReadOnlyList<AccSectorRow>> ListSubcentersAsync(CancellationToken ct = default) => _repo.ListSubcentersAsync(ct);

    public Task<AccImportResult> ImportFromSourceAsync(CancellationToken ct = default)
    {
        _authz.EnsureAdmin();               // solo il manual applica il guard
        return _import.RunAsync(ct);        // core condiviso con l'auto (hosted service)
    }

    public async Task SetHiddenAsync(int accId, bool hidden, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        await _repo.SetHiddenAsync(accId, hidden, ct);
        await _projection.SyncFromCatalogsAsync(ct);   // nascondere un ACC disattiva i suoi settori proiettati
    }

    public async Task SetSubcenterHiddenAsync(int id, bool hidden, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        await _repo.SetSubcenterHiddenAsync(id, hidden, ct);
        await _projection.SyncFromCatalogsAsync(ct);   // nascondere un settore lo disattiva nella proiezione
    }

    public async Task SetSubcenterLimitsAsync(int id, int? lower, int? upper, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        await _repo.SetSubcenterLimitsAsync(id, lower, upper, ct);
    }
}
