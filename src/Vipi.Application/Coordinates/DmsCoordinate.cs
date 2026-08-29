using System.Globalization;

namespace Vipi.Application.Coordinates;

/// <summary>
/// Il DMS del sectorfile Aurora: lettura e scrittura, in un posto solo.
///
/// <para>La lettura viveva in <c>AuroraSectorfileParser.TryParseDms</c> (in <c>Vipi.Infrastructure</c>), dove
/// serviva solo all'import. Dal 29 agosto 2026 serve anche al convertitore di coordinate, che sta nella UI e
/// non può vedere l'infrastruttura: è traslocata qui, e il parser del sectorfile <b>delega</b>. Riscriverla
/// avrebbe prodotto due verità sullo stesso formato, che è il modo in cui i formati divergono.</para>
/// </summary>
public static class DmsCoordinate
{
    /// <summary>Le due forme in cui Aurora scrive una coordinata DMS.</summary>
    public enum Forma
    {
        /// <summary>Coi punti: <c>N041.37.28.965</c>. È quella di <c>italy.restrict</c> e dei <c>.geo</c> recenti.</summary>
        Puntata,

        /// <summary>Compatta: <c>N0413728965</c>. È quella di <c>itgeo.geo</c>, dei <c>.mva</c> e dei <c>.vfi</c>.</summary>
        Compatta,
    }

    /// <summary>
    /// Converte una coordinata DMS Aurora in gradi decimali con segno (S/W negativi). Accetta <b>entrambe</b> le
    /// forme che convivono nel sectorfile italiano: quella coi punti (<c>N041.37.28.965</c>) e quella
    /// <b>compatta</b> (<c>N0463144000</c> = 046°31'44.000"), usata da <c>liph.mva</c>, <c>itgeo.geo</c> e dai
    /// <c>.vfi</c>. False se malformata.
    /// </summary>
    /// <remarks>La forma compatta si legge da destra: 3 cifre di millisecondi, 2 di secondi, 2 di primi, il resto
    /// gradi — così vale sia per la latitudine sia per la longitudine, che ha un grado in più.</remarks>
    public static bool TryParse(string? token, out double degrees)
    {
        degrees = 0;
        if (string.IsNullOrWhiteSpace(token)) return false;
        token = token.Trim();
        var hemi = char.ToUpperInvariant(token[0]);
        if (hemi is not ('N' or 'S' or 'E' or 'W')) return false;

        var body = token[1..];
        if (!body.Contains('.')) return TryParseCompact(body, hemi, out degrees);

        var parts = body.Split('.');
        if (parts.Length < 3) return false;
        // Secondi = "SS.sss": parts[2] interi + eventuale parts[3] frazione.
        var secText = parts.Length >= 4 ? parts[2] + "." + parts[3] : parts[2];
        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var deg)) return false;
        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var min)) return false;
        if (!double.TryParse(secText, NumberStyles.Float, CultureInfo.InvariantCulture, out var sec)) return false;

        var value = deg + min / 60.0 + sec / 3600.0;
        degrees = hemi is 'S' or 'W' ? -value : value;
        return true;
    }

    /// <summary>Forma compatta <c>DDD MM SS sss</c> senza separatori, letta da destra. Serve almeno una cifra di
    /// gradi oltre alle 7 fisse (3 millisecondi + 2 secondi + 2 primi).</summary>
    private static bool TryParseCompact(string body, char hemi, out double degrees)
    {
        degrees = 0;
        if (body.Length < 8) return false;
        foreach (var c in body) if (!char.IsAsciiDigit(c)) return false;

        var frac = body[^3..];
        var sec = body[^5..^3];
        var min = body[^7..^5];
        var deg = body[..^7];

        if (!int.TryParse(deg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var d)) return false;
        if (!int.TryParse(min, NumberStyles.Integer, CultureInfo.InvariantCulture, out var m)) return false;
        if (!double.TryParse(sec + "." + frac, NumberStyles.Float, CultureInfo.InvariantCulture, out var s)) return false;

        var value = d + m / 60.0 + s / 3600.0;
        degrees = hemi is 'S' or 'W' ? -value : value;
        return true;
    }

    /// <summary>
    /// Gradi decimali → token DMS Aurora. <paramref name="isLatitudine"/> sceglie l'emisfero (N/S contro E/W);
    /// i gradi si scrivono su <b>tre</b> cifre in entrambi i casi, che è ciò che i file veri contengono
    /// (<c>N042.00.28.000</c>, non <c>N42…</c>).
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>L'arrotondamento si fa sui millisecondi, non sui secondi</b>: arrotondare i secondi e poi
    /// formattare la frazione separatamente produce <c>…59.60.000</c>, cioè un DMS che non esiste. Qui si
    /// arrotonda una volta sola, in millisecondi interi, e si riporta il resto su primi e gradi.
    /// </remarks>
    public static string Format(double degrees, bool isLatitudine, Forma forma = Forma.Puntata)
    {
        var hemi = isLatitudine
            ? (degrees < 0 ? 'S' : 'N')
            : (degrees < 0 ? 'W' : 'E');

        // Tutto in millisecondi d'arco interi: un solo arrotondamento, nessun riporto da inventare dopo.
        var totalMs = (long)Math.Round(Math.Abs(degrees) * 3_600_000.0, MidpointRounding.AwayFromZero);
        var ms = totalMs % 1000; totalMs /= 1000;
        var sec = totalMs % 60; totalMs /= 60;
        var min = totalMs % 60;
        var deg = totalMs / 60;

        return forma == Forma.Puntata
            ? string.Create(CultureInfo.InvariantCulture, $"{hemi}{deg:000}.{min:00}.{sec:00}.{ms:000}")
            : string.Create(CultureInfo.InvariantCulture, $"{hemi}{deg:000}{min:00}{sec:00}{ms:000}");
    }
}
