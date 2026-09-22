
namespace Vipi.Sectorfile.Models;

/// <summary>All .tfl sectors of a category (dynamic sectors, GCA, ATZ shapes).</summary>
public sealed class TflLayer
{
    private readonly List<TflSector> _sectors = new();
    public IReadOnlyList<TflSector> Sectors => _sectors;
    public void Add(TflSector sector) => _sectors.Add(sector);
}

/// <summary>All .hartcc boundary groups.</summary>
public sealed class HartccLayer
{
    private readonly List<StaticBoundaryGroup> _groups = new();
    public IReadOnlyList<StaticBoundaryGroup> Groups => _groups;
    public void Add(StaticBoundaryGroup group) => _groups.Add(group);
}

/// <summary>All .lartcc boundary groups.</summary>
public sealed class LartccLayer
{
    private readonly List<StaticBoundaryGroup> _groups = new();
    public IReadOnlyList<StaticBoundaryGroup> Groups => _groups;
    public void Add(StaticBoundaryGroup group) => _groups.Add(group);
}

/// <summary>All ATC positions merged from every .frq file.</summary>
public sealed class AtcFrequencyLayer
{
    private readonly List<AtcPosition> _positions = new();
    public IReadOnlyList<AtcPosition> Positions => _positions;
    public void Add(AtcPosition position) => _positions.Add(position);
}

/// <summary>All enroute MVA blocks from ENRMVA/*.mva (grouped by FIR prefix in the UI).</summary>
public sealed class EnrMvaLayer
{
    private readonly List<MvaSector> _sectors = new();
    public IReadOnlyList<MvaSector> Sectors => _sectors;
    public void Add(MvaSector sector) => _sectors.Add(sector);
}

/// <summary>All enroute VFR points from ENRVFI/*.vfi (grouped by FIR prefix in the UI).</summary>
public sealed class EnrVfrLayer
{
    private readonly List<VfrPoint> _points = new();
    public IReadOnlyList<VfrPoint> Points => _points;
    public void Add(VfrPoint point) => _points.Add(point);
}

/// <summary>All *fic.tfl FIC reference shapes + FSS perimeter.</summary>
public sealed class FicLayer
{
    private readonly List<FicSector> _sectors = new();
    public IReadOnlyList<FicSector> Sectors => _sectors;
    public void Add(FicSector sector) => _sectors.Add(sector);
}

/// <summary>
/// Symbol definitions from the .sym file. Placeholder for Phase 2 (the .sym parser and the
/// concrete symbol model are defined in a later phase); present so SectorPackage compiles.
/// </summary>
public sealed class SymbolSet;
