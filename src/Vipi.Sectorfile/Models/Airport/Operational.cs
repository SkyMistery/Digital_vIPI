using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>Parking stand from ICAO.gts.</summary>
public sealed class Stand
{
    public string Number { get; set; } = string.Empty;
    public string IcaoCode { get; set; } = string.Empty;
    public Coordinate Position { get; set; }

    /// <summary>
    /// Il tipo dello stand (5° campo, facoltativo: manuale IVAO, <c>[GATES]</c>): L leggero, M medio, H pesante, S super,
    /// G aviazione generale. Null se la riga non ce l'ha o è vuoto (lotto «Subito» slice 12b, R2).
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gli slot dello stand (6° campo, «Slots for Gates»): filtri separati da spazi — <c>t_A320</c> tipo di aereo,
    /// <c>c_OAL</c> prefisso del nominativo, <c>d_LIRF</c> scalo di partenza, <c>w_</c> cargo ammessi. Null se non c'è.
    /// </summary>
    public string? Slot { get; set; }

    /// <summary>true when the record is commented out with //.</summary>
    public bool IsDisabled { get; set; }

    public SourceRef Source { get; set; } = null!;
}

/// <summary>Taxiway label from ICAO.txi.</summary>
public sealed class TaxiwayLabel
{
    public string Name { get; set; } = string.Empty;
    public string IcaoCode { get; set; } = string.Empty;
    public Coordinate Position { get; set; }
    public SourceRef Source { get; set; } = null!;
}

/// <summary>VFR point from ICAO.vfi (airport) or ENRVFI/*.vfi (enroute layer).</summary>
public sealed class VfrPoint
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Coordinate Position { get; set; }

    /// <summary>
    /// Optional 5th field: 0=mandatory, 1=VFR, 2=VFR HELI, 3=VFR AREA. Italian files typically
    /// omit it; null = absent.
    /// </summary>
    public int? Type { get; set; }

    public SourceRef Source { get; set; } = null!;
}
