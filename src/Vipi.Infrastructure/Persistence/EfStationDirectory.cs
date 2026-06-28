using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Implementazione EF di <see cref="IStationDirectory"/>: un ACC per FIR presente nel DB.</summary>
public sealed class EfStationDirectory : IStationDirectory
{
    private readonly VipiDbContext _db;
    public EfStationDirectory(VipiDbContext db) => _db = db;

    public IReadOnlyList<AccInfo> ListAccs() =>
        _db.Firs.AsNoTracking()
            .OrderBy(f => f.Code)
            .Select(f => new AccInfo(f.Code, f.Name))
            .ToList();
}
