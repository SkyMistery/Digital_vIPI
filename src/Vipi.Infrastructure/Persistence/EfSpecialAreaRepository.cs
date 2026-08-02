using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Lettura EF delle aree speciali/regolamentate importate dalla sorgente. Estratta da
/// <see cref="EfAccDerivationRepository"/> quando anche la vIPI APP non remotizzata ha iniziato a selezionarle:
/// il dato è per-ACC (<c>SpecialArea.CenterId</c>) ma il consumatore non è più solo l'ACC.
/// </summary>
public sealed class EfSpecialAreaRepository : ISpecialAreaRepository
{
    private readonly VipiDbContext _db;
    public EfSpecialAreaRepository(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<SpecialAreaPick>> ListSpecialAreasByAccAsync(string accCode, CancellationToken ct = default) =>
        await _db.SpecialAreas.AsNoTracking()
            .Where(s => s.CenterId == accCode)
            .OrderBy(s => s.Name)
            .Select(s => new SpecialAreaPick(s.IvaoId, s.Name, s.Type, s.MinimumAlt, s.MaximumAlt, s.CenterId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SpecialAreaPick>> ListSpecialAreasExcludingAccAsync(string accCode, CancellationToken ct = default) =>
        await _db.SpecialAreas.AsNoTracking()
            .Where(s => s.CenterId != accCode)
            .OrderBy(s => s.CenterId).ThenBy(s => s.Name)
            .Select(s => new SpecialAreaPick(s.IvaoId, s.Name, s.Type, s.MinimumAlt, s.MaximumAlt, s.CenterId))
            .ToListAsync(ct);

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
