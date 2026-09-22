using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>
/// An ATC position aggregated from all .frq files (global itfreq.frq + FIR-specific).
/// Duplicate position codes across files are merged via <see cref="Sources"/>.
/// </summary>
public sealed class AtcPosition
{
    public string Code { get; set; } = string.Empty;       // e.g. "LIRR_NE_CTR"
    public decimal FrequencyMhz { get; set; }
    public IList<Transfer> TransferList { get; } = new List<Transfer>();
    public string? Profile { get; set; }                   // .cpr file reference; null when the line stops at the transfer list
    public string? AtisFile { get; set; }
    public bool BlockCpdlc { get; set; }
    public string? DatisFile { get; set; }

    /// <summary>itfreq.frq + all FIR-specific .frq files that contain this position code.</summary>
    public IList<SourceRef> Sources { get; } = new List<SourceRef>();

    /// <summary>True when divergent values for this position exist across .frq files (ARCHITECTURE §8.2).</summary>
    public bool HasConflict { get; set; }
}

/// <summary>A transfer-list entry; <see cref="IsNegative"/> when prefixed with '-' in the file.</summary>
public sealed class Transfer
{
    public string PositionCode { get; set; } = string.Empty;
    public bool IsNegative { get; set; }
}
