using Vipi.Application.Abstractions;
using Vipi.Application.Auth;

namespace Vipi.Application.Content;

/// <inheritdoc cref="IAccAdminService"/>
public sealed class AccAdminService : IAccAdminService
{
    private readonly IAccAdminRepository _repo;
    private readonly IAccImportUseCase _import;
    private readonly ISpecialAreaImportUseCase _specialAreas;
    private readonly IEditAuthorizationService _authz;
    private readonly ISectorProjectionService _projection;

    public AccAdminService(IAccAdminRepository repo, IAccImportUseCase import,
        ISpecialAreaImportUseCase specialAreas, IEditAuthorizationService authz, ISectorProjectionService projection)
    {
        _repo = repo;
        _import = import;
        _specialAreas = specialAreas;
        _authz = authz;
        _projection = projection;
    }

    public Task<IReadOnlyList<AccAdminRow>> ListAccsAsync(CancellationToken ct = default) => _repo.ListAccsAsync(ct);

    public Task<IReadOnlyList<AccSectorRow>> ListSubcentersAsync(CancellationToken ct = default) => _repo.ListSubcentersAsync(ct);

    public async Task<AccImportOutcome> ImportFromSourceAsync(CancellationToken ct = default)
    {
        _authz.EnsureAdmin();                        // solo il manual applica il guard
        var result = await _import.RunAsync(ct);     // core ACC + subcenter (condiviso con l'auto)
        var special = await _specialAreas.RunAsync(ct);   // aree speciali: manual = auto, stesso stato DB (doc 02 §4.4)
        // I fallimenti aree speciali per-ACC risalgono alla UI, che li logga (direttiva logging, invariante #7).
        return new AccImportOutcome(result, special.Failures);
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
