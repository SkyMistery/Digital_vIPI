using System.Globalization;

namespace Vipi.Sectorfile.Shared;

/// <summary>
/// Converts between the four coordinate formats used in Aurora sector files and the
/// canonical decimal-degree representation.
///
/// Supported read formats:
///   • Dotted DMS:  N041.48.01.000   (cardinal + DDD.MM.SS.fff)
///   • Compact DMS: N0414801000      (cardinal + DDD MM SS fff, no dots)
///   • Decimal:     41.80027778      (positive = N/E, negative = S/W)
///   • Fix ref:     ABDAB            (resolved via IFixResolver; render-time only)
///
/// On WRITE, coordinates are always serialised in dotted DMS with 3-digit, zero-padded
/// degrees for BOTH axes (ARCHITECTURE §3.1 / INTERFACE_CONTRACTS C6) and 3-digit
/// milli-arcsecond precision. <c>decimal</c> is used for the seconds arithmetic to keep
/// round-trip precision well under 1 mm (NFR-04).
///
/// All methods are pure functions: no shared state, safe to call from concurrent threads.
/// </summary>
public static class CoordinateConverter
{
    /// <summary>
    /// Parses a single coordinate token. A token carries one axis: a cardinal of N/S sets
    /// latitude, E/W sets longitude; a plain decimal is treated as latitude. The opposite
    /// axis is left at 0 — file parsers parse the lat and lon tokens separately and combine.
    /// </summary>
    /// <param name="token">Raw token from the file. Trimmed before parsing.</param>
    /// <param name="resolver">
    /// Required only for the fix-reference format. File parsers pass <c>null</c>; resolution
    /// happens at render time with a populated resolver (INTERFACE_CONTRACTS §7).
    /// </param>
    /// <exception cref="CoordinateParseException">
    /// Thrown when the token is null/empty, matches no known format, or is a fix reference
    /// that cannot be resolved (null or missing in <paramref name="resolver"/>).
    /// </exception>
    public static Coordinate Parse(string token, IFixResolver? resolver = null)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new CoordinateParseException("Coordinate token is null or empty.");
        }

        token = token.Trim();
        char first = token[0];

        // DMS (dotted or compact): cardinal letter immediately followed by a digit.
        if (IsCardinal(first) && token.Length > 1 && char.IsAsciiDigit(token[1]))
        {
            return token.Contains('.')
                ? ParseDottedDms(token)
                : ParseCompactDms(token);
        }

        // Decimal degrees: starts with a digit or a sign.
        if (char.IsAsciiDigit(first) || first is '-' or '+')
        {
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var dec))
            {
                // Bare decimal carries no cardinal: treat as latitude axis.
                return new Coordinate(dec, 0d);
            }

            throw new CoordinateParseException($"Unrecognised decimal coordinate: {token}");
        }

        // Fix reference: a navaid/fix identifier (e.g. ABDAB, BC404, EA400), resolved via
        // IFixResolver at render time. Idents are uppercase letters and digits; pure-numeric and
        // cardinal+digit tokens were already handled above as decimal / DMS, so anything reaching
        // here begins with a letter.
        if (IsFixReference(token))
        {
            if (resolver is not null && resolver.TryResolve(token, out var resolved))
            {
                return resolved;
            }

            throw new CoordinateParseException($"Unresolved fix reference: {token}");
        }

        throw new CoordinateParseException($"Unrecognised coordinate token: {token}");
    }

    /// <summary>
    /// Serialises a coordinate to dotted DMS for both axes, space-separated:
    /// e.g. "N041.48.01.000 E012.14.20.000". For single-axis serialisation (the common
    /// case in file savers) use <see cref="LatitudeToDottedDms"/> / <see cref="LongitudeToDottedDms"/>.
    /// </summary>
    public static string ToDottedDms(Coordinate coord)
        => $"{LatitudeToDottedDms(coord.LatitudeDeg)} {LongitudeToDottedDms(coord.LongitudeDeg)}";

    /// <summary>Serialises a latitude (decimal degrees) to dotted DMS, e.g. "N041.48.01.000".</summary>
    public static string LatitudeToDottedDms(double latitudeDeg)
        => FormatComponent(latitudeDeg, latitudeDeg < 0 ? 'S' : 'N');

    /// <summary>Serialises a longitude (decimal degrees) to dotted DMS, e.g. "E012.14.20.000".</summary>
    public static string LongitudeToDottedDms(double longitudeDeg)
        => FormatComponent(longitudeDeg, longitudeDeg < 0 ? 'W' : 'E');

    private static Coordinate ParseDottedDms(string token)
    {
        char cardinal = token[0];
        string[] parts = token[1..].Split('.');
        if (parts.Length != 4
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int deg)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int min)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int sec)
            || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int millis))
        {
            throw new CoordinateParseException($"Malformed dotted DMS coordinate: {token}");
        }

        return BuildAxis(cardinal, deg, min, sec, millis, token);
    }

    private static Coordinate ParseCompactDms(string token)
    {
        char cardinal = token[0];
        string digits = token[1..];

        // Layout from the right: ...DD(min) DD(sec) DDD(millis); leading remainder = degrees.
        if (digits.Length < 8 || !digits.All(char.IsAsciiDigit))
        {
            throw new CoordinateParseException($"Malformed compact DMS coordinate: {token}");
        }

        int degLen = digits.Length - 7;
        int deg = int.Parse(digits[..degLen], CultureInfo.InvariantCulture);
        int min = int.Parse(digits.Substring(degLen, 2), CultureInfo.InvariantCulture);
        int sec = int.Parse(digits.Substring(degLen + 2, 2), CultureInfo.InvariantCulture);
        int millis = int.Parse(digits.Substring(degLen + 4, 3), CultureInfo.InvariantCulture);

        return BuildAxis(cardinal, deg, min, sec, millis, token);
    }

    private static Coordinate BuildAxis(char cardinal, int deg, int min, int sec, int millis, string token)
    {
        if (min > 59 || sec > 59)
        {
            throw new CoordinateParseException($"Minutes/seconds out of range in coordinate: {token}");
        }

        decimal value = deg + (min / 60m) + ((sec + (millis / 1000m)) / 3600m);
        double signed = (double)(IsNegativeCardinal(cardinal) ? -value : value);

        return IsLatitudeCardinal(cardinal)
            ? new Coordinate(signed, 0d)
            : new Coordinate(0d, signed);
    }

    private static string FormatComponent(double degrees, char cardinal)
    {
        decimal abs = Math.Abs((decimal)degrees);

        int deg = (int)decimal.Floor(abs);
        decimal totalSeconds = (abs - deg) * 3600m;

        int min = (int)decimal.Floor(totalSeconds / 60m);
        decimal secRemainder = totalSeconds - (min * 60m);
        int sec = (int)decimal.Floor(secRemainder);
        int millis = (int)decimal.Round((secRemainder - sec) * 1000m, MidpointRounding.AwayFromZero);

        // Propagate carry produced by rounding (e.g. 999.6 ms → 1000 ms).
        if (millis >= 1000) { millis -= 1000; sec++; }
        if (sec >= 60) { sec -= 60; min++; }
        if (min >= 60) { min -= 60; deg++; }

        return string.Create(CultureInfo.InvariantCulture, $"{cardinal}{deg:000}.{min:00}.{sec:00}.{millis:000}");
    }

    private static bool IsCardinal(char c) => c is 'N' or 'S' or 'E' or 'W';

    private static bool IsLatitudeCardinal(char c) => c is 'N' or 'S';

    private static bool IsNegativeCardinal(char c) => c is 'S' or 'W';

    private static bool IsFixReference(string token)
    {
        // Idents are uppercase letters and digits (e.g. ABDAB, BC404, EA400). At least one letter
        // is required — pure-numeric tokens are decimal coordinates, handled before this point.
        bool hasLetter = false;
        foreach (char c in token)
        {
            if (c is >= 'A' and <= 'Z')
            {
                hasLetter = true;
            }
            else if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return hasLetter;
    }
}
