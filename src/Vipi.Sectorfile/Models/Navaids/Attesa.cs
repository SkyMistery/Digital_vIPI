using System.Globalization;
using System.Text.RegularExpressions;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>
/// Un'attesa di rotta del <c>.hold</c> (<c>[HOLDENR]</c>, carta F2 slice 6):
/// <c>HLD-ABBOZ;N046.02.37.000;E011.07.48.000;ABBOZ/225R-9000;</c> — nome, punto, descrizione. Il nome è quello
/// a cui rimanda il sesto campo dei <c>.fix</c>; la descrizione è il fix, la rotta di avvicinamento col verso
/// delle virate (<c>L</c>/<c>R</c>) e la quota (<c>9000</c>, <c>FL105</c>).
/// </summary>
/// <remarks>
/// La descrizione resta il dato scritto (<see cref="Descrizione"/>): le sue parti sono una LETTURA di quel
/// testo, e valgono null se non ha la forma solita. Sul master del 22 settembre 2026 la hanno tutte e 68.
/// </remarks>
public sealed partial class Attesa
{
    public string Nome { get; set; } = string.Empty;

    /// <summary>Il punto dell'attesa: coordinate, o un nome come ovunque ci sia una coppia lat/lon.</summary>
    public Punto Posizione { get; set; }

    /// <summary>Il quarto campo, com'è scritto: <c>ABBOZ/225R-9000</c>.</summary>
    public string Descrizione { get; set; } = string.Empty;

    /// <summary>Il fix della descrizione (<c>ABBOZ</c>).</summary>
    public string? Fix => Parti() is { } m ? m.Groups["fix"].Value : null;

    /// <summary>La rotta di avvicinamento, in gradi (<c>225</c>).</summary>
    public int? Rotta => Parti() is { } m ? int.Parse(m.Groups["rotta"].Value, CultureInfo.InvariantCulture) : null;

    /// <summary>Il verso delle virate: <c>L</c> o <c>R</c>.</summary>
    public char? Verso => Parti() is { } m ? m.Groups["verso"].Value[0] : null;

    /// <summary>La quota com'è scritta: <c>9000</c>, <c>FL105</c>.</summary>
    public string? Quota => Parti() is { } m ? m.Groups["quota"].Value : null;

    public SourceRef Source { get; set; } = null!;

    private Match? Parti()
    {
        var m = FormaDellaDescrizione().Match(Descrizione);
        return m.Success ? m : null;
    }

    [GeneratedRegex(@"^(?<fix>[^/]+)/(?<rotta>[0-9]{3})(?<verso>[LR])-(?<quota>\S+)$")]
    private static partial Regex FormaDellaDescrizione();
}
