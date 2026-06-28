using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Implementazione EF di <see cref="IStationDirectory"/>: un ACC per ACC presente nel DB.</summary>
public sealed class EfStationDirectory : IStationDirectory
{
    private readonly VipiDbContext _db;
    public EfStationDirectory(VipiDbContext db) => _db = db;

    // Solo ACC non nascosti: l'admin può nascondere ACC importati (non operativi) dalla navigazione pubblica.
    public IReadOnlyList<AccInfo> ListAccs() =>
        _db.Accs.AsNoTracking()
            .Where(f => !f.IsHidden)
            .OrderBy(f => f.Code)
            .Select(f => new AccInfo(f.Code, f.Name))
            .ToList();
}
