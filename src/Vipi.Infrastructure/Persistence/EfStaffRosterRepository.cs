using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Implementazione EF di <see cref="IStaffRosterRepository"/> (roster staffisti IT).</summary>
public sealed class EfStaffRosterRepository : IStaffRosterRepository
{
    private readonly VipiDbContext _db;
    public EfStaffRosterRepository(VipiDbContext db) => _db = db;

    public async Task UpsertLoginAsync(int UserId, string? displayName, IReadOnlyList<string> positions, CancellationToken ct = default)
    {
        var csv = string.Join(',', positions);
        var now = DateTime.UtcNow;
        var sm = await _db.StaffMembers.FirstOrDefaultAsync(x => x.UserId == UserId, ct);
        if (sm is null)
        {
            _db.StaffMembers.Add(new StaffMember
            {
                UserId = UserId,
                DisplayName = displayName,
                StaffPositionsCsv = csv,
                IsActive = true,
                FirstSeenUtc = now,
                LastLoginUtc = now,
            });
        }
        else
        {
            sm.DisplayName = displayName ?? sm.DisplayName;
            sm.StaffPositionsCsv = csv;
            sm.IsActive = true;             // un nuovo login riattiva
            sm.LastLoginUtc = now;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<StaffRosterEntry>> ListActiveAsync(CancellationToken ct = default)
    {
        var rows = await _db.StaffMembers
            .Where(x => x.IsActive)
            .OrderBy(x => x.UserId)
            .AsNoTracking()
            .ToListAsync(ct);

        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<int>> ListAllUserIdsAsync(CancellationToken ct = default) =>
        await _db.StaffMembers.Select(x => x.UserId).ToListAsync(ct);

    public async Task UpdateVerifiedAsync(int UserId, string? displayName, string? atcRating, IReadOnlyList<string> positions, CancellationToken ct = default)
    {
        var sm = await _db.StaffMembers.FirstOrDefaultAsync(x => x.UserId == UserId, ct);
        if (sm is null) return;
        // Il nickname riempie il nome SOLO se il nome manca. La ri-verifica passa da /v2/users/{vid} col
        // token APPLICATIVO, che il nome vero non ce l'ha: dà `publicNickname`, che nel payload reale vale
        // "Carmine (704798)". Sovrascrivere significherebbe far oscillare l'elenco — «Carmine Granato»
        // dopo il login, «Carmine (704798)» dopo la ri-verifica notturna, e avanti così.
        // Il nome autorevole viene dal LOGIN (scope `profile`, firstName+lastName); qui si resta indietro
        // di un cambio di nickname, e va benissimo: ora c'è di meglio da mostrare.
        if (string.IsNullOrWhiteSpace(sm.DisplayName) && !string.IsNullOrWhiteSpace(displayName))
            sm.DisplayName = displayName;
        sm.AtcRating = atcRating;
        sm.StaffPositionsCsv = string.Join(',', positions);
        sm.IsActive = true;
        sm.LastVerifiedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeactivateAsync(int UserId, CancellationToken ct = default)
    {
        var sm = await _db.StaffMembers.FirstOrDefaultAsync(x => x.UserId == UserId, ct);
        if (sm is null) return;
        sm.IsActive = false;
        sm.LastVerifiedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyDictionary<int, string>> GetDisplayNamesAsync(IReadOnlyCollection<int> userIds, CancellationToken ct = default)
    {
        if (userIds.Count == 0) return new Dictionary<int, string>();
        var ids = userIds.Distinct().ToList();
        var rows = await _db.StaffMembers.AsNoTracking()
            .Where(x => ids.Contains(x.UserId) && x.DisplayName != null && x.DisplayName != "")
            .Select(x => new { x.UserId, x.DisplayName })
            .ToListAsync(ct);
        return rows.ToDictionary(x => x.UserId, x => x.DisplayName!);
    }

    public async Task<StaffRosterEntry?> FindAsync(int userId, CancellationToken ct = default)
    {
        // ⚠️ Nessun filtro su IsActive: chi ha firmato una release resta il revisore di quella release anche dopo
        // aver lasciato lo staff.
        var x = await _db.StaffMembers.AsNoTracking().FirstOrDefaultAsync(m => m.UserId == userId, ct);
        return x is null ? null : Map(x);
    }

    private static StaffRosterEntry Map(StaffMember x) => new(
        UserId: x.UserId,
        DisplayName: x.DisplayName,
        AtcRating: x.AtcRating,
        StaffPositions: string.IsNullOrWhiteSpace(x.StaffPositionsCsv)
            ? Array.Empty<string>()
            : x.StaffPositionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        LastLoginUtc: x.LastLoginUtc);
}
