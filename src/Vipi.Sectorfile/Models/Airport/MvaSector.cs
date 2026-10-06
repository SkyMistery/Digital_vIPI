using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>
/// MVA block. The same class serves two file contexts that differ in L; semantics:
///   AIRPORT .mva (ICAO.mva): AltLabel from L; field 2; exactly ONE L; line per block.
///   ENROUTE .mva (ENRMVA/&lt;fir&gt;.mva): AltLabel from L; field 5 (field 2 = FIR code);
///     ONE OR MORE L; lines per block (multiple label anchors).
/// A block with a commented-out L; but active T; lines is valid (AltLabel empty, LabelAnchors empty);
/// a block with active L; but all T; commented is valid (Vertices empty).
/// DUMMY terminator rows (field 2 == "DUMMY", in any case: <c>T;dummy;</c> is one too, as Aurora reads it — F3 slice 10, 100 lowercase rows in 4 files) are NOT inspected for coordinates.
/// </summary>
public sealed class MvaSector
{
    /// <summary>
    /// Il 2° campo della prima riga <c>L;</c>/<c>T;</c> del blocco che non è un separatore <c>DUMMY</c>, anche
    /// commentata: il gruppo della <i>MVA Selection</i> nei <c>.mva</c> di ACC (<c>LIMM</c>), il nome della zona in
    /// quelli di scalo (<c>CERCHIO-BA</c>). È il nome col quale si aggancia un tag <c>//@</c> (lotto «Subito» slice
    /// 1d, «file per file» E1). Solo lettura: lo scrittore non lo usa.
    /// </summary>
    public string Nome { get; internal set; } = string.Empty;

    /// <summary>Altitude label: "FL110", "3000N", "TRL", "70/TRL" … (airport: L; field 2; enroute: L; field 5).</summary>
    public string AltLabel { get; set; } = string.Empty;

    /// <summary>Last field of the L line (display size, e.g. 7 or 8).</summary>
    public int LabelSize { get; set; }

    /// <summary>
    /// Il blocco ha più righe <c>L;</c> e non dicono tutte lo stesso nome, la stessa quota e lo stesso carattere: è una
    /// raccolta di etichette di zone diverse (in fondo a <c>liba.mva</c>, <c>lirs.mva</c>… 8 blocchi sul fork), e
    /// <see cref="AltLabel"/> è solo la prima. Lo scrittore le riscriverebbe tutte uguali: la scheda non fa scrivere
    /// quota e carattere di un blocco così (lotto «Subito» slice 15a).
    /// </summary>
    public bool EtichetteDiverse { get; internal set; }

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

    /// <summary>
    /// La riga del file non aveva il 5° campo: resta senza. Un vertice che il Lab aggiunge a una zona di ACC (falso)
    /// porta invece il gruppo, come vuole la <i>MVA Selection</i> di Aurora (lotto «Subito» slice 15a, E3).
    /// </summary>
    public bool LettoSenzaGruppo { get; set; }
}
