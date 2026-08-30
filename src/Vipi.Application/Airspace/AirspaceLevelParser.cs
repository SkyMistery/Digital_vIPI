using System.Globalization;
using System.Text.RegularExpressions;
using Vipi.Domain;

namespace Vipi.Application.Airspace;

/// <summary>
/// Legge una quota come la scrive il file dell'AIP. Sono <b>quattro forme sole</b>, e non è una stima: contate
/// su tutte e 3 072 le quote del file del 15 luglio 2026 (base e tetto di 1 536 volumi) sono
/// <c>GND</c> (1 139), <c>N FT AGL</c> (702), <c>FLN</c> (632), <c>N FT AMSL</c> (599). Le altre grafie che si
/// riconoscono (<c>SFC</c>, <c>UNL</c>, <c>MSL</c>) costano una riga e tolgono un guasto il giorno che un file
/// diverso le usa.
///
/// <para>⚠️ <b>La quota non si legge dalle coordinate.</b> Nel KML un volume con base <c>GND</c> ha i vertici
/// alla quota del <b>terreno</b>, e uno con base staccata ha il pavimento alla sua: leggere lì dentro darebbe
/// una misura che somiglia a quella giusta ed è un'altra cosa. La verità sta in questi due campi di testo.</para>
///
/// <para>⚠️ <b>Sopra l'UNL convenzionale non c'è un livello di volo: c'è l'illimitato.</b> Il file lo scrive
/// in tre modi diversi — <c>FL999</c> (16 quote), <c>FL980</c> (1) e <c>FL2000</c> (1) — e prenderli alla
/// lettera vorrebbe dire un'area alta 200 000 piedi. La soglia è <see cref="FlIllimitato"/>: sotto ci sta il
/// più alto livello vero del file, <c>FL600</c>, sopra ci stanno solo le tre scritture dell'illimitato.</para>
/// </summary>
public static class AirspaceLevelParser
{
    /// <summary>
    /// Da questo FL in su non c'è un livello di volo vero, ma la scrittura convenzionale dell'illimitato.
    /// Sta sopra l'UNL convenzionale (FL 660, <see cref="Vipi.Application.Aor.AorFlBand.Unlimited"/>) e sopra
    /// il più alto livello vero misurato nel file, che è FL600.
    /// </summary>
    public const int FlIllimitato = 700;

    private static readonly Regex Piedi = new(@"^(?<n>\d+(?:[.,]\d+)?)\s*(?:FT|FEET)?\s*(?<rif>AMSL|MSL|AGL|GND|SFC)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Fl = new(@"^(?:FL|FLIGHT\s*LEVEL)\s*(?<n>\d+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// La quota, o <c>null</c> se il testo non si riconosce — e allora chi chiama lo segnala invece di
    /// inventare un numero. Il testo di partenza si conserva sempre in <see cref="AirspaceLevel.Raw"/>: è
    /// quello che il documento stampa, ed è l'unica forma di cui si è certi.
    /// </summary>
    public static AirspaceLevel? Parse(string? testo)
    {
        var t = (testo ?? "").Trim();
        if (t.Length == 0) return null;

        var normale = Regex.Replace(t, @"\s+", " ").ToUpperInvariant();

        if (normale is "GND" or "SFC" or "GROUND" or "SURFACE" or "0")
            return new AirspaceLevel(AirspaceDatum.Gnd, 0, t);

        if (normale is "UNL" or "UNLIM" or "UNLIMITED")
            return new AirspaceLevel(AirspaceDatum.Unlimited, null, t);

        if (Fl.Match(normale) is { Success: true } fl)
        {
            var livello = int.Parse(fl.Groups["n"].Value, CultureInfo.InvariantCulture);
            return livello >= FlIllimitato
                ? new AirspaceLevel(AirspaceDatum.Unlimited, null, t)
                : new AirspaceLevel(AirspaceDatum.FlightLevel, livello * 100, t);
        }

        if (Piedi.Match(normale) is { Success: true } ft)
        {
            var valore = double.Parse(ft.Groups["n"].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            var piedi = (int)Math.Round(valore);
            var datum = ft.Groups["rif"].Value.ToUpperInvariant() switch
            {
                "AGL" => AirspaceDatum.Agl,
                "GND" or "SFC" => AirspaceDatum.Agl,
                _ => AirspaceDatum.Amsl,   // senza riferimento, l'AIP intende il livello del mare
            };
            // «0 FT AGL» e «0 FT GND» sono il suolo, e chiamarli piedi sul terreno sarebbe una distinzione
            // che nessuno fa a voce.
            return piedi == 0 && datum == AirspaceDatum.Agl
                ? new AirspaceLevel(AirspaceDatum.Gnd, 0, t)
                : new AirspaceLevel(datum, piedi, t);
        }

        return null;
    }
}
