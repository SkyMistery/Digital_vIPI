using Microsoft.EntityFrameworkCore;
using Vipi.Application;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Implementazione EF di <see cref="IStationDirectory"/>: un ACC per ACC presente nel DB.</summary>
public sealed class EfStationDirectory : IStationDirectory
{
    private readonly VipiDbContext _db;
    private readonly IReadOnlyList<string> _prefixes;

    public EfStationDirectory(VipiDbContext db, Microsoft.Extensions.Options.IOptions<DivisionOptions>? division = null)
    {
        _db = db;
        _prefixes = division?.Value.IcaoPrefixes is { Count: > 0 } p ? p : new List<string> { "LI" };
    }

    // Solo ACC domestici non nascosti: l'admin può nascondere ACC importati; gli ACC ESTERI (confinanti, per le
    // vLOA) restano fuori dalla navigazione pubblica (home /services/vsop + header) — servono solo agli editor. "Estero"
    // deciso dai prefissi ICAO della divisione (robusto al flag IsForeign, che può essere stale su vLOA vecchie).
    public IReadOnlyList<AccInfo> ListAccs() =>
        _db.Accs.AsNoTracking()
            .Where(f => !f.IsHidden && !f.IsForeign)
            .OrderBy(f => f.Code)
            .Select(f => new AccInfo(f.Code, f.Name))
            .AsEnumerable()
            .Where(a => _prefixes.Any(p => a.Code.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    // Tutti gli aeroporti col codice ACC di competenza (per il conteggio ATC online d'aeroporto).
    public IReadOnlyList<AirportStation> ListAirports() =>
        _db.Airports.AsNoTracking()
            .Select(a => new AirportStation(a.Icao, a.Acc!.Code, a.HasMilitaryPresence, a.IsMilitaryOnly))
            .ToList();
}
