using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>
/// MVA block. The same class serves two file contexts that differ in L; semantics:
///   AIRPORT .mva (ICAO.mva): AltLabel from L; field 2; exactly ONE L; line per block.
///   ENROUTE .mva (ENRMVA/&lt;fir&gt;.mva): AltLabel from L; field 5 (field 2 = FIR code);
///     ONE OR MORE L; lines per block (multiple label anchors).
/// A block with a commented-out L; but active T; lines is valid (AltLabel empty, LabelAnchors empty);
/// a block with active L; but all T; commented is valid (Vertices empty).
/// DUMMY terminator rows (field 2 == "DUMMY", case-sensitive) are NOT inspected for coordinates.
/// </summary>
public sealed class MvaSector
{
    /// <summary>Altitude label: "FL110", "3000N", "TRL", "70/TRL" … (airport: L; field 2; enroute: L; field 5).</summary>
    public string AltLabel { get; set; } = string.Empty;

    /// <summary>Last field of the L line (display size, e.g. 7 or 8).</summary>
    public int LabelSize { get; set; }

    /// <summary>Label anchor positions; airport = exactly 1, enroute = 1 or more.</summary>
    public IList<Punto> LabelAnchors { get; } = new List<Punto>();

    /// <summary>Ordered boundary vertices (empty if all T; commented).</summary>
    public IList<MvaVertex> Vertices { get; } = new List<MvaVertex>();

    public SourceRef Source { get; set; } = null!;
}

public sealed class MvaVertex
{
    /// <summary>The vertex, by coordinates or by name (<c>T;LIRR;UTENO;UTENO;LIRR;</c>, F2 slice 4).</summary>
    public Punto Position { get; set; }

    /// <summary>
    /// Field 5 of the T line (= field 2 repeated verbatim: AltLabel or FIR code);
    /// null for DUMMY rows; preserved verbatim for round-trip fidelity.
    /// </summary>
    public string? ExtraField { get; set; }
}
