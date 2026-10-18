using System.Globalization;
using System.Text.RegularExpressions;

namespace Aip.Pdf;

/// <summary>
/// Un limite verticale dal testo stampato: le forme dell'ICAO, comuni agli Stati (<c>FL 195</c>, <c>UNL</c>,
/// <c>GND</c>, <c>SFC</c>, <c>2000ft AMSL</c>, <c>1000 FT AGL</c>, <c>300 M AGL</c>). 🔴 Il testo resta sempre: una forma
/// non riconosciuta (<c>2000ft AMSL or 1000ft AGL whichever is higher</c>) ha solo quello, e il riferimento
/// <see cref="VerticalReference.Unknown"/>.
/// </summary>
public static partial class LimitText
{
    [GeneratedRegex(@"^FL\s*(?<v>\d{1,3})$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase)]
    private static partial Regex RxFl();

    [GeneratedRegex(@"^(?<v>\d+(?:[.,]\d+)?)\s*(?<u>FT|M)\s*(?<r>AMSL|MSL|AGL|ASFC|SFC|GND)?$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase)]
    private static partial Regex RxQuota();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RxSpazi();

    public static VerticalLimit Parse(string text)
    {
        var testo = RxSpazi().Replace(text, " ").Trim();
        var t = testo.ToUpperInvariant();

        if (t is "GND" or "SFC")
            return new VerticalLimit { Text = testo, Reference = VerticalReference.Ground };
        if (t is "UNL" or "UNLIMITED")
            return new VerticalLimit { Text = testo, Reference = VerticalReference.Unlimited };

        var fl = RxFl().Match(t);
        if (fl.Success)
            return new VerticalLimit
            {
                Text = testo,
                Reference = VerticalReference.FlightLevel,
                Value = int.Parse(fl.Groups["v"].Value, CultureInfo.InvariantCulture),
                Unit = VerticalUnit.Fl,
            };

        var q = RxQuota().Match(t);
        if (q.Success)
        {
            var riferimento = q.Groups["r"].Value switch
            {
                "AMSL" or "MSL" => VerticalReference.Msl,
                "AGL" or "ASFC" or "SFC" or "GND" => VerticalReference.Agl,
                // Un numero senza riferimento: nell'AIP italiano le altitudini si intendono sul mare, ma è una
                // regola dello Stato, non della forma. Resta il testo.
                _ => VerticalReference.Unknown,
            };
            if (riferimento != VerticalReference.Unknown)
                return new VerticalLimit
                {
                    Text = testo,
                    Reference = riferimento,
                    Value = double.Parse(q.Groups["v"].Value.Replace(',', '.'), CultureInfo.InvariantCulture),
                    Unit = q.Groups["u"].Value == "M" ? VerticalUnit.M : VerticalUnit.Ft,
                };
        }

        return new VerticalLimit { Text = testo };
    }
}
