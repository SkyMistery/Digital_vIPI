using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

public enum HideTag { Hidden = 1, Shown = 2 }

public enum InstallationType { Airport = 0, Helipad = 1, Military = 2, Private = 3, Uncontrolled = 4, Custom }

/// <summary>
/// One logical airport-info record, potentially sourced from N files
/// (OTHER/itap.ap + every FIR-specific .ap that lists this ICAO).
/// </summary>
public sealed class AirportInfo
{
    public string IcaoCode { get; set; } = string.Empty;
    public int ElevationFt { get; set; }

    /// <summary>0 = not defined.</summary>
    public int TransitionAltFt { get; set; }

    public Coordinate Centre { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>null if the field is absent in the file.</summary>
    public HideTag? HideTag { get; set; }

    public InstallationType InstallationType { get; set; }

    /// <summary>
    /// Verbatim text from the file when <see cref="InstallationType"/> == Custom; null for the
    /// 5 standard enum values. Required for round-trip fidelity (the original free-text value
    /// cannot be recovered from the enum alone).
    /// </summary>
    public string? CustomInstallationTypeText { get; set; }

    /// <summary>true when the record was commented out.</summary>
    public bool IsDisabled { get; set; }

    /// <summary>itap.ap + all FIR-specific .ap files that contain this ICAO code.</summary>
    public IList<SourceRef> Sources { get; } = new List<SourceRef>();

    /// <summary>
    /// True when this record appears in multiple files with divergent field values (ARCHITECTURE §8.2).
    /// Set by AirportLoader; the per-source values live in <c>ISessionService</c>'s conflict store.
    /// </summary>
    public bool HasConflict { get; set; }
}
