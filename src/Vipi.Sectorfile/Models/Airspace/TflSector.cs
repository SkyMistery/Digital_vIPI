using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>
/// SectorType is inferred from the TFL header FillColor/category AND the SectorCode name:
///   - SectorCode ends with "FSS" (case-insensitive) → Fss
///   - otherwise the FillColor/category string is mapped (APP → App, CTR → Ctr, …)
/// This is necessary because FSS sectors use FillColor = "CTR" in Italian files.
/// </summary>
public enum SectorType { Ctr, App, Fss, Tma, Uir, Atz, Gca }

/// <summary>
/// Pure inference of <see cref="SectorType"/> — kept out of constructors so the model
/// classes stay side-effect-free data bags (DEVELOPMENT_PLAN Fase 2 note).
/// </summary>
public static class SectorTypeInference
{
    public static SectorType Infer(string? sectorCode, string? fillColor)
    {
        if (sectorCode is not null && sectorCode.EndsWith("FSS", StringComparison.OrdinalIgnoreCase))
        {
            return SectorType.Fss;
        }

        return fillColor?.Trim().ToUpperInvariant() switch
        {
            "APP" => SectorType.App,
            "TMA" => SectorType.Tma,
            "UIR" => SectorType.Uir,
            "ATZ" => SectorType.Atz,
            "GCA" => SectorType.Gca,
            _ => SectorType.Ctr,
        };
    }
}

/// <summary>A single TFL sector block (one header + N coordinate lines in a .tfl file).</summary>
public class TflSector
{
    /// <summary>Opaque identifier; may be colon-separated for multi-position activation — stored verbatim.</summary>
    public string SectorCode { get; set; } = string.Empty;

    /// <summary>Palette name OR hex string (e.g. #0C0C0C).</summary>
    public string FillColor { get; set; } = string.Empty;

    public int LineWeight { get; set; }

    /// <summary>Palette name OR hex string.</summary>
    public string StrokeColor { get; set; } = string.Empty;

    public int Flags { get; set; }

    /// <summary>Inferred from <see cref="SectorCode"/> + <see cref="FillColor"/>; see <see cref="SectorTypeInference"/>.</summary>
    public SectorType Type => SectorTypeInference.Infer(SectorCode, FillColor);

    /// <summary>Implicitly closed polygon. A vertex may be given by name (<c>AMSOR;AMSOR;</c>, F2 slice 4).</summary>
    public IList<Punto> Vertices { get; } = new List<Punto>();

    public SourceRef Source { get; set; } = null!;
}
