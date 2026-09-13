using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>EF: alias prefisso-troncato → fix reale (globali, uno per prefisso).
/// <para>⚠️ Il cancello di ruolo sta QUI (T-060, 13 settembre 2026): editor aeroporto e pagina Sorgenti
/// chiamano questa classe senza un servizio in mezzo. Crea l'alias chi edita uno scalo (Editor); lo toglie
/// solo la pagina Sorgenti, che è dell'Admin. Le letture restano libere: l'import delle SID le fa di sfondo.</para>
/// </summary>
internal sealed class EfSidFixAliasRepository : ISidFixAliasRepository
{
    private readonly VipiDbContext _db;
    private readonly IEditAuthorizationService _authz;

    public EfSidFixAliasRepository(VipiDbContext db, IEditAuthorizationService authz)
    {
        _db = db;
        _authz = authz;
    }

    public async Task<IReadOnlyList<SidFixAliasRow>> ListAsync(CancellationToken ct = default) =>
        await _db.SidFixAliases.AsNoTracking().OrderBy(x => x.Prefix)
            .Select(x => new SidFixAliasRow(x.Id, x.Prefix, x.FixName)).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<string, string>> GetMapAsync(CancellationToken ct = default) =>
        (await _db.SidFixAliases.AsNoTracking().ToListAsync(ct))
            .ToDictionary(x => x.Prefix, x => x.FixName, StringComparer.OrdinalIgnoreCase);

    public async Task UpsertAsync(string prefix, string fixName, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);

        prefix = prefix.Trim().ToUpperInvariant();
        fixName = fixName.Trim().ToUpperInvariant();
        if (prefix.Length == 0 || fixName.Length == 0) return;
        var row = await _db.SidFixAliases.FirstOrDefaultAsync(x => x.Prefix == prefix, ct);
        if (row is null) { row = new SidFixAlias { Prefix = prefix }; _db.SidFixAliases.Add(row); }
        row.FixName = fixName;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();

        var row = await _db.SidFixAliases.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return;
        _db.SidFixAliases.Remove(row);
        await _db.SaveChangesAsync(ct);
    }
}
