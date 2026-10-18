using System.Globalization;

namespace Aip.Aurora;

/// <summary>Come Aurora e la carta §M scrivono coordinate, quote e valori.</summary>
public static class AuroraText
{
    /// <summary>
    /// <c>N045.30.12.340</c>: gradi a tre cifre, primi, secondi e millesimi, sempre col punto (carta «file per file»
    /// G6: le coordinate tutte nella stessa forma). Il millesimo di secondo è ~3 cm.
    /// </summary>
    public static string Latitude(double deg) => Dms(deg, 'N', 'S');

    public static string Longitude(double deg) => Dms(deg, 'E', 'W');

    public static string Coordinates(Position p) => Latitude(p.Lat) + ";" + Longitude(p.Lon);

    private static string Dms(double deg, char positive, char negative)
    {
        var millesimi = (long)Math.Round(Math.Abs(deg) * 3_600_000, MidpointRounding.AwayFromZero);
        var gradi = millesimi / 3_600_000;
        var primi = millesimi / 60_000 % 60;
        var secondi = millesimi / 1000 % 60;
        var resto = millesimi % 1000;
        return string.Create(CultureInfo.InvariantCulture,
            $"{(deg < 0 && millesimi > 0 ? negative : positive)}{gradi:000}.{primi:00}.{secondi:00}.{resto:000}");
    }

    /// <summary>
    /// Un limite verticale come valore di <c>lower</c>/<c>upper</c> (§M regola 8, in piedi come nel PDF): <c>GND</c>,
    /// <c>SFC</c>, <c>UNL</c>, <c>FL195</c>, <c>"2500ft AMSL"</c>, <c>"1000ft AGL"</c>. Una forma che l'importer non
    /// ha riconosciuto esce col testo stampato, fra virgolette se ha spazi.
    /// </summary>
    public static string Limit(VerticalLimit limit)
    {
        var testo = limit.Text.Trim();
        var v = limit.Value is { } n ? n.ToString("0.###", CultureInfo.InvariantCulture) : null;
        var unita = limit.Unit switch { VerticalUnit.M => "m", _ => "ft" };
        return limit.Reference switch
        {
            VerticalReference.Ground => testo.Equals("SFC", StringComparison.OrdinalIgnoreCase) ? "SFC" : "GND",
            VerticalReference.Unlimited => "UNL",
            VerticalReference.FlightLevel when v is not null => "FL" + v,
            VerticalReference.Msl when v is not null => Value($"{v}{unita} AMSL"),
            VerticalReference.Agl when v is not null => Value($"{v}{unita} AGL"),
            _ => Value(testo),
        };
    }

    /// <summary>Un valore di un tag (§M regola 5): fra virgolette se ha spazi; le virgolette dentro diventano apostrofi.</summary>
    public static string Value(string text)
    {
        var pulito = text.Replace('"', '\'');
        return pulito.Any(char.IsWhiteSpace) || pulito.Length == 0 ? "\"" + pulito + "\"" : pulito;
    }
}
