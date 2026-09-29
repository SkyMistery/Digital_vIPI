using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>
/// Runway pair, parsed from the //PISTE section of .rw files. The //MENU MAPPE and //ACC
/// sections are preserved as RawChunks for round-trip.
/// </summary>
public sealed class Runway
{
    public string IcaoCode { get; set; } = string.Empty;
    public string Designator1 { get; set; } = string.Empty;   // e.g. "16L"
    public string Designator2 { get; set; } = string.Empty;   // e.g. "34R"
    public int ElevThresh1Ft { get; set; }
    public int ElevThresh2Ft { get; set; }
    public float TrueHeading1 { get; set; }

    /// <summary>Nullable — the field may be empty in the file.</summary>
    public float? TrueHeading2 { get; set; }

    public Coordinate Threshold1 { get; set; }
    public Coordinate Threshold2 { get; set; }

    /// <summary>
    /// La rotta VERA dalla soglia del verso primario a quella dell'opposto, in gradi (lotto «Subito» slice 11b, M4); null
    /// se le due soglie coincidono. La rotta scritta nel file è magnetica: in Italia viene da 2 a 5 gradi meno di questa
    /// (misura sul fork); una differenza grande vuol dire soglie invertite.
    /// </summary>
    public double? RottaVeraDalleSoglie
    {
        get
        {
            if (Threshold1.Equals(Threshold2))
                return null;
            double la1 = Threshold1.LatitudeDeg * Math.PI / 180, la2 = Threshold2.LatitudeDeg * Math.PI / 180;
            double dlo = (Threshold2.LongitudeDeg - Threshold1.LongitudeDeg) * Math.PI / 180;
            double y = Math.Sin(dlo) * Math.Cos(la2);
            double x = (Math.Cos(la1) * Math.Sin(la2)) - (Math.Sin(la1) * Math.Cos(la2) * Math.Cos(dlo));
            return Math.Round(((Math.Atan2(y, x) * 180 / Math.PI) + 360) % 360, 1);
        }
    }

    /// <summary>OTHER/itrw.rw + all FIR-specific .rw files that contain this runway pair.</summary>
    public IList<SourceRef> Sources { get; } = new List<SourceRef>();
}
