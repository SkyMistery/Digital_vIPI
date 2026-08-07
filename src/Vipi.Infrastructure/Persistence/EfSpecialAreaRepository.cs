using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Lettura EF delle aree speciali/regolamentate importate dalla sorgente. Estratta da
/// <see cref="EfAccDerivationRepository"/> quando anche la vIPI APP non remotizzata ha iniziato a selezionarle:
/// il dato è per-ACC (<c>SpecialAreaCenters</c>) ma il consumatore non è più solo l'ACC.
/// <para>
/// «Proprie» e «di altri ACC» si decidono sui legami: un'area elencata da due centri è propria per entrambi, e per
/// nessuno dei due compare fra le altrui. Prima, con un solo <c>CenterId</c> sulla riga, apparteneva all'ultimo
/// import in ordine alfabetico e per gli altri spariva dalle proprie.
/// </para>
/// </summary>
public sealed class EfSpecialAreaRepository : ISpecialAreaRepository
{
    private readonly VipiDbContext _db;
    public EfSpecialAreaRepository(VipiDbContext db) => _db = db;

    public Task<IReadOnlyList<SpecialAreaPick>> ListSpecialAreasByAccAsync(string accCode, CancellationToken ct = default) =>
        PicksAsync(a => a.Centers.Any(c => c.CenterId == accCode), ct);

    public Task<IReadOnlyList<SpecialAreaPick>> ListSpecialAreasExcludingAccAsync(string accCode, CancellationToken ct = default) =>
        PicksAsync(a => !a.Centers.Any(c => c.CenterId == accCode), ct);

    // Una sola query con i legami inclusi: l'ordinamento finale è per nome (il picker mostra i primi N e conta il resto).
    private async Task<IReadOnlyList<SpecialAreaPick>> PicksAsync(
        System.Linq.Expressions.Expression<Func<Domain.Entities.SpecialArea, bool>> filter, CancellationToken ct)
    {
        var rows = await _db.SpecialAreas.AsNoTracking()
            .Where(filter)
            .OrderBy(a => a.Name)
            .Select(a => new
            {
                a.IvaoId, a.Name, a.Type, a.MinimumAlt, a.MaximumAlt,
                Centers = a.Centers.Select(c => c.CenterId).OrderBy(c => c).ToList(),
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new SpecialAreaPick(r.IvaoId, r.Name, r.Type, r.MinimumAlt, r.MaximumAlt, r.Centers))
            .ToList();
    }

    public async Task<IReadOnlyList<SpecialAreaDetail>> GetSpecialAreasByIdsAsync(IReadOnlyList<string> ivaoIds, CancellationToken ct = default)
    {
        if (ivaoIds.Count == 0) return Array.Empty<SpecialAreaDetail>();
        var ids = ivaoIds.ToList();
        return await _db.SpecialAreas.AsNoTracking()
            .Where(s => ids.Contains(s.IvaoId))
            .Select(s => new SpecialAreaDetail(s.IvaoId, s.Name, s.Type, s.Description, s.ActivationDetails,
                s.MinimumAlt, s.MaximumAlt, s.RegionMapPolygon))
            .ToListAsync(ct);
    }
}
