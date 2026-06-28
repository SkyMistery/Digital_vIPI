using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;

namespace Vipi.Application.Auth;

/// <summary>
/// Roster degli staffisti IT per il picker permessi. Si popola quando un membro con posizioni staff IT
/// si logga (<see cref="RecordLoginAsync"/>), e viene ri-verificato periodicamente via API IVAO
/// (<see cref="VerifyAllAsync"/>): chi non è più staff IT viene disattivato. Compromesso scelto perché
/// l'enumerazione completa della divisione non è leggibile col token app (endpoint 500); il profilo del
/// singolo UserId (<c>/v2/users/{UserId}</c>) invece sì. Lo staffista deve loggarsi almeno una volta per comparire.
/// </summary>
public interface IStaffRosterService
{
    /// <summary>Registra/aggiorna l'utente al login se ha posizioni staff IT. No-op per i non-staff.</summary>
    Task RecordLoginAsync(CurrentUser user, CancellationToken ct = default);

    /// <summary>Staffisti IT attivi, per il dropdown della pagina permessi.</summary>
    Task<IReadOnlyList<StaffRosterEntry>> ListActiveAsync(CancellationToken ct = default);

    /// <summary>Ri-verifica via API tutti i UserId del roster; disattiva chi non è più staff IT. Ritorna i disattivati.</summary>
    Task<int> VerifyAllAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IStaffRosterService"/>
public sealed class StaffRosterService : IStaffRosterService
{
    private readonly IStaffRosterRepository _repo;
    private readonly IUserDirectory _ivao;
    private readonly string _divPrefix;   // es. "IT-"

    public StaffRosterService(IStaffRosterRepository repo, IUserDirectory ivao, IOptions<DivisionOptions> division)
    {
        _repo = repo;
        _ivao = ivao;
        _divPrefix = $"{division.Value.Code}-";
    }

    private bool IsDivisionStaffCode(string code) =>
        code.StartsWith(_divPrefix, StringComparison.OrdinalIgnoreCase);

    public async Task RecordLoginAsync(CurrentUser user, CancellationToken ct = default)
    {
        var positions = user.StaffPositions.Where(IsDivisionStaffCode).ToList();
        if (positions.Count == 0) return;   // non è staffista della divisione: non entra nel roster
        await _repo.UpsertLoginAsync(user.UserId, user.Name, positions, ct);
    }

    public Task<IReadOnlyList<StaffRosterEntry>> ListActiveAsync(CancellationToken ct = default) =>
        _repo.ListActiveAsync(ct);

    public async Task<int> VerifyAllAsync(CancellationToken ct = default)
    {
        var vids = await _repo.ListAllUserIdsAsync(ct);
        var deactivated = 0;
        foreach (var UserId in vids)
        {
            var info = await _ivao.GetUserAsync(UserId, ct);
            if (info is null) continue;     // errore/transitorio: non modifico lo stato

            var positions = info.StaffPositionCodes.Where(IsDivisionStaffCode).ToList();
            if (info.IsStaff && positions.Count > 0)
            {
                await _repo.UpdateVerifiedAsync(UserId, info.Nickname, info.AtcRating, positions, ct);
            }
            else
            {
                await _repo.DeactivateAsync(UserId, ct);
                deactivated++;
            }
        }
        return deactivated;
    }
}
