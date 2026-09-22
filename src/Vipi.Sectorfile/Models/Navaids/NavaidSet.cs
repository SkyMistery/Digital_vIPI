using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>
/// Container for all navaids. Loaded once in background (Phase 1) and shared by all entities.
/// Implements <see cref="IFixResolver"/> so render-time consumers can resolve fix-reference
/// tokens (HARTCC/LARTCC fix-pair vertices, STR procedure/holding fixes) via
/// <see cref="CoordinateConverter"/> without a Shared→Models dependency.
///
/// HighAirways source: AIRWAY/itawhigh.hairway ([HIGH AIRWAY] ISC section).
/// LowAirways  source: AIRWAY/itawlow.lairway  ([LOW AIRWAY] ISC section).
/// An empty ISC section leaves the corresponding list empty; not an error.
/// </summary>
public sealed class NavaidSet : IFixResolver
{
    private readonly Dictionary<string, Coordinate> _identIndex = new(StringComparer.Ordinal);

    public IList<Vor> Vors { get; } = new List<Vor>();
    public IList<Ndb> Ndbs { get; } = new List<Ndb>();
    public IList<Fix> Fixes { get; } = new List<Fix>();
    public IList<Airway> HighAirways { get; } = new List<Airway>();
    public IList<Airway> LowAirways { get; } = new List<Airway>();

    public void AddVor(Vor vor)
    {
        ArgumentNullException.ThrowIfNull(vor);
        Vors.Add(vor);
        Index(vor.Ident, vor.Position);
    }

    public void AddNdb(Ndb ndb)
    {
        ArgumentNullException.ThrowIfNull(ndb);
        Ndbs.Add(ndb);
        Index(ndb.Ident, ndb.Position);
    }

    public void AddFix(Fix fix)
    {
        ArgumentNullException.ThrowIfNull(fix);
        Fixes.Add(fix);
        Index(fix.Name, fix.Position);
    }

    /// <inheritdoc />
    public bool TryResolve(string ident, out Coordinate position)
    {
        if (ident is not null && _identIndex.TryGetValue(ident, out position))
        {
            return true;
        }

        position = default;
        return false;
    }

    private void Index(string ident, Coordinate position)
    {
        // Last write wins; navaid idents are expected unique across VOR/NDB/Fix.
        if (!string.IsNullOrEmpty(ident))
        {
            _identIndex[ident] = position;
        }
    }
}
