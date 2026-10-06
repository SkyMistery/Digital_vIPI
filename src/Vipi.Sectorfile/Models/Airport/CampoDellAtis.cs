using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>
/// Un campo in più della finestra ATIS di Aurora, da un <c>.fds</c> (sezione <c>[ATISFIELD]</c>, lotto «Subito» slice
/// 18a): <c>Type of Approach;[ARR_TYPE];</c> — l'etichetta che il controllore vede accanto alla casella, e il
/// segnaposto col quale i modelli <c>.atis</c> e <c>.datis</c> citano quel che ci scrive. Il manuale IVAO non
/// descrive la sezione: la forma è quella di <c>atisextra.fds</c>, l'unico del sector.
/// </summary>
public sealed class CampoDellAtis
{
    /// <summary>Il testo accanto alla casella, com'è scritto.</summary>
    public string Etichetta { get; set; } = string.Empty;

    /// <summary>Il nome del segnaposto, senza le parentesi (<c>ARR_TYPE</c>).</summary>
    public string Segnaposto { get; set; } = string.Empty;

    public SourceRef Source { get; set; } = null!;
}
