using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>
/// Airport/FIR index built during Phase 0 (ISC scan). Public lists are read-only;
/// the IscLoader populates via the Add* methods.
/// </summary>
public sealed class Catalog
{
    private readonly List<AirportEntry> _airports = new();
    private readonly List<FirDescriptor> _firs = new();

    public IReadOnlyList<AirportEntry> Airports => _airports;
    public IReadOnlyList<FirDescriptor> Firs => _firs;   // filter/grouping only
    public NavaidEntry Navaids { get; set; } = new();

    public void AddAirport(AirportEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _airports.Add(entry);
    }

    public void AddFir(FirDescriptor fir)
    {
        ArgumentNullException.ThrowIfNull(fir);
        _firs.Add(fir);
    }
}

/// <summary>Lightweight airport index entry — no geometric data yet.</summary>
public sealed class AirportEntry
{
    public string IcaoCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;     // from .ap, available after ISC scan
    public string MetaPath { get; set; } = string.Empty; // path to the .asdx metafile

    /// <summary>
    /// FIR code for catalog grouping (from metafile "primaryFir"; auto-derived from a
    /// FIR-specific .ap file name if absent, e.g. lirr.ap → "LIRR").
    /// </summary>
    public string PrimaryFir { get; set; } = string.Empty;

    public bool IsLoaded { get; set; }
}

/// <summary>Lightweight FIR descriptor — a grouping/filter label, NOT a geometry container.</summary>
public sealed class FirDescriptor
{
    public string FirCode { get; set; } = string.Empty;  // e.g. "LIRR"
    public string Name { get; set; } = string.Empty;     // e.g. "Roma ACC"
}

public sealed class NavaidEntry
{
    public string MetaPath { get; set; } = string.Empty;
    public bool IsLoaded { get; set; }
}
