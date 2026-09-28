using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>EF: alias prefisso-troncato → fix reale, uno per prefisso e scalo (U-031; quelli senza scalo valgono per tutti).
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
        await _db.SidFixAliases.AsNoTracking().OrderBy(x => x.Prefix).ThenBy(x => x.Icao)
            .Select(x => new SidFixAliasRow(x.Id, x.Icao, x.Prefix, x.FixName)).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<string, string>> GetMapAsync(string icao, CancellationToken ct = default)
    {
        icao = icao.Trim().ToUpperInvariant();
        var righe = await _db.SidFixAliases.AsNoTracking()
            .Where(x => x.Icao == null || x.Icao == icao).ToListAsync(ct);

        // Prima quelli di tutti, poi quelli dello scalo: a parità di prefisso il suo scrive sopra.
        var mappa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in righe.OrderBy(x => x.Icao is null ? 0 : 1)) mappa[r.Prefix] = r.FixName;
        return mappa;
    }

    public async Task UpsertAsync(string icao, string prefix, string fixName, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);

        icao = icao.Trim().ToUpperInvariant();
        prefix = prefix.Trim().ToUpperInvariant();
        fixName = fixName.Trim().ToUpperInvariant();
        if (icao.Length == 0 || prefix.Length == 0 || fixName.Length == 0) return;
        // ⚠️ Solo l'alias DELLO SCALO: uno di tutti con lo stesso prefisso resta com'è — vale per gli altri, e
        // a questo scalo il suo passa davanti (GetMapAsync).
        var row = await _db.SidFixAliases.FirstOrDefaultAsync(x => x.Icao == icao && x.Prefix == prefix, ct);
        if (row is null) { row = new SidFixAlias { Icao = icao, Prefix = prefix }; _db.SidFixAliases.Add(row); }
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
