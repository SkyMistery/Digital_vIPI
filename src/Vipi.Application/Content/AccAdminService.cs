using Vipi.Application.Abstractions;
using Vipi.Application.Auth;

namespace Vipi.Application.Content;

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
