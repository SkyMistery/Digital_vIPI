using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>
/// Una rotta VFR del <c>.vrt</c> (<c>[VFRROUTE]</c>, carta F2 slice 6): le righe consecutive con lo stesso
/// numero, un punto per riga — <c>1;SAN SEVERO;SAN SEVERO;</c>. I punti sono quasi sempre per nome (i VRP dei
/// <c>.vfi</c>); in <c>libv.vrt</c> per coordinate.
/// </summary>
/// <remarks>
/// Una rotta finisce dove cambia il numero, anche senza una riga vuota in mezzo (15 file su 16 sul master del
/// 22 settembre 2026 ne hanno almeno una così), o a una riga vuota o a un commento. <c>libv.vrt</c> e
/// <c>licz.vrt</c> hanno due campi in più (<c>…;;1;</c>) che nessuna specifica spiega: restano nella riga,
/// sconosciuti al modello.
/// </remarks>
public sealed class RottaVfr
{
    /// <summary>Il numero della rotta nel file, com'è scritto.</summary>
    public string Numero { get; set; } = string.Empty;

    public IList<Punto> Punti { get; } = new List<Punto>();

    public SourceRef Source { get; set; } = null!;
}
