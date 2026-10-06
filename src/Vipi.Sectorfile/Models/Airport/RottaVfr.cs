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
/// <c>licz.vrt</c> hanno due campi in più (<c>…;;1;</c>): per il manuale IVAO il 4° è riservato e il 5° è «Route
/// Military» (lotto «Subito» slice 16a).
/// </remarks>
public sealed class RottaVfr
{
    /// <summary>Il numero della rotta nel file, com'è scritto.</summary>
    public string Numero { get; set; } = string.Empty;

    /// <summary>
    /// Rotta militare: il 5° campo è <c>1</c> su <b>tutte</b> le sue righe (manuale: <c>0</c> o vuoto = no). Lo
    /// scrittore lo mette su ogni riga, anche su quella di un punto nuovo (slice 16a, «file per file» S4).
    /// </summary>
    public bool Militare { get; set; }

    /// <summary>
    /// Il 5° campo è <c>1</c> solo su alcune righe della rotta: il modello tiene un valore solo, e la scheda non lo fa
    /// scrivere finché le righe non dicono la stessa cosa (<c>Regola.RottaMilitareAMeta</c>). Sul fork nessuna.
    /// </summary>
    public bool MilitareAMeta { get; internal set; }

    public IList<Punto> Punti { get; } = new List<Punto>();

    public SourceRef Source { get; set; } = null!;
}
