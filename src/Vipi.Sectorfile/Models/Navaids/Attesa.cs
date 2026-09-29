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
/// testo, e valgono null se non ha la forma solita. Sul master del 22 settembre 2026 la hanno tutte e 68. Dalla slice
/// 10c del lotto «Subito» (U1) una parte si scrive e ricompone la descrizione: un valore che non va è un
/// <see cref="ArgumentException"/>, una descrizione fuori forma un <see cref="InvalidOperationException"/>.
/// </remarks>
public sealed partial class Attesa
{
    public string Nome { get; set; } = string.Empty;

    /// <summary>Il punto dell'attesa: coordinate, o un nome come ovunque ci sia una coppia lat/lon.</summary>
    public Punto Posizione { get; set; }

    /// <summary>Il quarto campo, com'è scritto: <c>ABBOZ/225R-9000</c>.</summary>
    public string Descrizione { get; set; } = string.Empty;

    /// <summary>Il fix della descrizione (<c>ABBOZ</c>). Scriverlo ricompone la descrizione (slice 10c).</summary>
    public string? Fix
    {
        get => Parti() is { } m ? m.Groups["fix"].Value : null;
        set
        {
            string fix = value?.Trim() ?? string.Empty;
            if (fix.Length == 0 || fix.IndexOfAny(['/', ';', ' ']) >= 0)
                throw new ArgumentException("Il fix è un nome, senza «/», «;» o spazi.", nameof(value));
            Componi(fix, null, null, null);
        }
    }

    /// <summary>La rotta di avvicinamento, in gradi (<c>225</c>); si scrive da 1 a 360, con tre cifre.</summary>
    public int? Rotta
    {
        get => Parti() is { } m ? int.Parse(m.Groups["rotta"].Value, CultureInfo.InvariantCulture) : null;
        set
        {
            if (value is not (>= 1 and <= 360))
                throw new ArgumentException("La rotta va da 1 a 360 gradi.", nameof(value));
            Componi(null, value.Value.ToString("000", CultureInfo.InvariantCulture), null, null);
        }
    }

    /// <summary>Il verso delle virate: <c>L</c> o <c>R</c>.</summary>
    public char? Verso
    {
        get => Parti() is { } m ? m.Groups["verso"].Value[0] : null;
        set
        {
            char verso = char.ToUpperInvariant(value ?? ' ');
            if (verso is not ('L' or 'R'))
                throw new ArgumentException("La virata è L (a sinistra) o R (a destra).", nameof(value));
            Componi(null, null, verso.ToString(), null);
        }
    }

    /// <summary>La quota com'è scritta: <c>9000</c> (piedi), <c>FL105</c>.</summary>
    public string? Quota
    {
        get => Parti() is { } m ? m.Groups["quota"].Value : null;
        set
        {
            string quota = value?.Trim().ToUpperInvariant() ?? string.Empty;
            if (!FormaDellaQuota().IsMatch(quota))
                throw new ArgumentException("La quota si scrive in piedi (9000) o come livello di volo (FL105).", nameof(value));
            Componi(null, null, null, quota);
        }
    }

    public SourceRef Source { get; set; } = null!;

    // Una parte cambiata, le altre come sono: solo se la descrizione ha la forma solita.
    private void Componi(string? fix, string? rotta, string? verso, string? quota)
    {
        if (Parti() is not { } m)
            throw new InvalidOperationException("L'info non ha la forma FIX/rotta+virata-quota (ABBOZ/225R-9000): si scrive tutta nel campo Info.");
        Descrizione = $"{fix ?? m.Groups["fix"].Value}/{rotta ?? m.Groups["rotta"].Value}{verso ?? m.Groups["verso"].Value}-{quota ?? m.Groups["quota"].Value}";
    }

    private Match? Parti()
    {
        var m = FormaDellaDescrizione().Match(Descrizione);
        return m.Success ? m : null;
    }

    [GeneratedRegex(@"^(?<fix>[^/]+)/(?<rotta>[0-9]{3})(?<verso>[LR])-(?<quota>\S+)$")]
    private static partial Regex FormaDellaDescrizione();

    [GeneratedRegex(@"^(FL[0-9]{2,3}|[0-9]{3,5})$")]
    private static partial Regex FormaDellaQuota();
}
