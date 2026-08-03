using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
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
    private readonly IDocumentReviewService _review;

    public AccAdminService(IAccAdminRepository repo, IAccImportUseCase import,
        ISpecialAreaImportUseCase specialAreas, IEditAuthorizationService authz, ISectorProjectionService projection,
        IDocumentReviewService review)
    {
        _repo = repo;
        _import = import;
        _specialAreas = specialAreas;
        _authz = authz;
        _projection = projection;
        _review = review;
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

    public async Task<SpecialAreaImportResult> ImportSpecialAreasAsync(string accCode, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        var result = await _specialAreas.RunForAccAsync(accCode, ct);

        // Abilita solo se ha davvero portato a casa qualcosa: un ACC acceso ma con la fetch fallita entrerebbe nel
        // giro periodico senza aree, e il prossimo import lo ritenterebbe in silenzio.
        if (result.Failures.Count == 0 && (result.Created + result.Updated) > 0)
        {
            var acc = (await _repo.ListAccsAsync(ct))
                .FirstOrDefault(a => string.Equals(a.Code, accCode, StringComparison.OrdinalIgnoreCase));
            if (acc is not null && !acc.SpecialAreasEnabled)
                await _repo.SetSpecialAreasEnabledAsync(acc.Id, true, ct);
        }
        return result;
    }

    public Task<int> SetSpecialAreasEnabledAsync(int accId, bool enabled, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        return _repo.SetSpecialAreasEnabledAsync(accId, enabled, ct);
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

        // Regola 1: una RADICE (settore senza padre) non si può nascondere finché ha figli visibili — li
        // orfanerebbe senza un nonno a cui riappenderli. I figli di un settore NON-radice risalgono invece al
        // padre (Regola 2, gestita dalla proiezione), quindi lì l'occultamento è consentito.
        SubcenterHideContext? ctx = null;
        if (hidden)
        {
            ctx = await _repo.GetSubcenterHideContextAsync(id, ct);
            if (ctx is { IsRoot: true, HasVisibleChildren: true })
                throw new ValidationException(
                    "Non puoi nascondere un settore radice con figli visibili: nascondi prima i figli o assegnagli un padre.");
        }

        await _repo.SetSubcenterHiddenAsync(id, hidden, ct);
        await _projection.SyncFromCatalogsAsync(ct);   // nascondere un settore lo disattiva nella proiezione

        // Regola 3: segnala la revisione ai documenti (ACC/APP/vLOA) dove il settore compariva.
        if (hidden && ctx is not null)
            await _review.FlagForHiddenSectorAsync(ctx.ComposePosition, ctx.CenterId, ct);
    }

    public async Task SetSubcenterLimitsAsync(int id, int? lower, int? upper, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        await _repo.SetSubcenterLimitsAsync(id, lower, upper, ct);
    }
}
