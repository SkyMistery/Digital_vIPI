
namespace Vipi.Sectorfile.Models;

/// <summary>
/// Aggregates all data belonging to a single aerodrome. Loaded on demand (Phase 2).
/// For Info, AtcPositions and Runways the global file (itap.ap / itfreq.frq / itrw.rw) is the
/// authoritative source; FIR-specific occurrences are merged via each record's SourceRef[].
/// </summary>
public sealed class Airport
{
    public string IcaoCode { get; set; } = string.Empty;

    /// <summary>One logical record, potentially N source files (itap.ap + FIR-specific .ap).</summary>
    public AirportInfo Info { get; set; } = new();

    /// <summary>Single source file: GND_LAYOUT/xx_ad_gnd.pol.</summary>
    public AirportGroundLayout GroundLayout { get; } = new();

    /// <summary>Line geometry: GEO/lixx.geo + RW_MARKINGS/xx_mark.geo.</summary>
    public AirportLines Lines { get; } = new();

    public IList<Stand> Stands { get; } = new List<Stand>();                 // ICAO.gts
    public IList<TaxiwayLabel> TaxiwayLabels { get; } = new List<TaxiwayLabel>(); // ICAO.txi
    public IList<VfrPoint> VfrPoints { get; } = new List<VfrPoint>();        // ICAO.vfi
    public IList<MvaSector> MvaSectors { get; } = new List<MvaSector>();     // ICAO.mva (local APP/TWR only)
    public IList<SidProcedure> Sids { get; } = new List<SidProcedure>();     // ICAO.sid
    public IList<StrRecord> Maps { get; } = new List<StrRecord>();           // ICAO.str

    /// <summary>ICAO.atis; null if the file is absent.</summary>
    public AtisData? Atis { get; set; }

    /// <summary>From twrs.tfl, filtered by ICAO (shared file; editing writes back to twrs.tfl).</summary>
    public IList<TflSector> AtzShape { get; } = new List<TflSector>();

    /// <summary>TWR / GND / DEL / ATIS positions; each lists every .frq file containing it.</summary>
    public IList<AtcPosition> AtcPositions { get; } = new List<AtcPosition>();

    public IList<Runway> Runways { get; } = new List<Runway>();
}
