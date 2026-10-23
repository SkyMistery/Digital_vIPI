using System.Globalization;
using System.Text.RegularExpressions;

namespace Aip.Pdf.Italia;

/// <summary>
/// Segnalazioni 2611, revisione 5 (committente, 24 ottobre): le coordinate dei punti stampate male nelle tabelle dell'AD 2, che
/// le letture normali non prendono. Si leggono gradi, primi e secondi in ordine, qualunque sia il segno fra loro («38°13°23.05”»,
/// «42’’4.55”», «35’:28.77”», «013:51:24.4115”»); una cifra staccata dai secondi si riattacca («07. 6”», «38”6N» = 38.6″,
/// «42.3” 3N» = 42.33″); l'emisfero che manca è N per la latitudine ed E per la longitudine, come in Italia, e una lettera di
/// troppo («N E 008°…») non conta; 60 secondi sono un primo («12'60.0''» = 13'00″), «024’» sono 24 primi. Chi legge così lo dice
/// («errore di stampa: letto come …»).
/// </summary>
internal static partial class ItalianCoordinateStampate
{
    /// <summary>La posizione e com'è stata letta, o null se nemmeno così (sei numeri, gradi e primi nei limiti).</summary>
    public static (Position Posizione, string Letto)? Leggi(string? stampata)
    {
        if (string.IsNullOrWhiteSpace(stampata)) return null;
        var t = RxCifraDopoIDecimali().Replace(stampata, "$1$2$3");   // «42.3” 3N» → 42.33N
        t = RxCifraDopoLeVirgolette().Replace(t, "$1.$2$3");           // «38”6N» → 38.6N
        t = RxSpazioNeiDecimali().Replace(t, "$1.$2");                 // «07. 6» → 07.6
        var numeri = RxNumero().Matches(t).Select(m => m.Value).ToList();
        if (numeri.Count != 6) return null;
        var lettere = RxEmisfero().Matches(t).Select(m => m.Value).ToList();
        var sud = lettere.Contains("S");
        var ovest = lettere.Contains("W");
        var v = numeri.Select(n => double.Parse(n, CultureInfo.InvariantCulture)).ToArray();
        if (Gradi(v[0], v[1], v[2], Decimali(numeri[2])) is not { } lat || Gradi(v[3], v[4], v[5], Decimali(numeri[5])) is not { } lon) return null;
        if (lat.Gradi is < 35 or > 48 || lon.Gradi is < 5 or > 20) return null;
        var posizione = new Position((sud ? -1 : 1) * lat.Valore, (ovest ? -1 : 1) * lon.Valore);
        return (posizione, $"{Scritta(lat, 2)}{(sud ? "S" : "N")} {Scritta(lon, 3)}{(ovest ? "W" : "E")}");
    }

    private sealed record Gms(int Gradi, int Primi, double Secondi, int Decimali)
    {
        public double Valore => Gradi + Primi / 60.0 + Secondi / 3600;
    }

    private static int Decimali(string numero) => numero.Contains('.') ? numero.Length - numero.IndexOf('.') - 1 : 0;

    private static Gms? Gradi(double g, double p, double s, int decimali)
    {
        if (g % 1 != 0 || p % 1 != 0 || p > 60 || s > 60) return null;
        var (pp, ss) = s >= 60 ? (p + 1, s - 60) : (p, s);
        var (gg, pr) = pp >= 60 ? (g + 1, pp - 60) : (g, pp);
        return new Gms((int)gg, (int)pr, ss, decimali);
    }

    private static string Scritta(Gms x, int cifre) =>
        $"{x.Gradi.ToString(new string('0', cifre), CultureInfo.InvariantCulture)}°{x.Primi:00}'"
        + $"{x.Secondi.ToString(x.Decimali == 0 ? "00" : "00." + new string('0', x.Decimali), CultureInfo.InvariantCulture)}\"";

    [GeneratedRegex(@"(\d+\.\d+)\s*[""”″]\s*(\d)\s*([NSEW])", RegexOptions.CultureInvariant)]
    private static partial Regex RxCifraDopoIDecimali();

    [GeneratedRegex(@"(\d+)\s*[""”″](\d)\s*([NSEW])", RegexOptions.CultureInvariant)]
    private static partial Regex RxCifraDopoLeVirgolette();

    [GeneratedRegex(@"(\d+)\.\s+(\d)", RegexOptions.CultureInvariant)]
    private static partial Regex RxSpazioNeiDecimali();

    [GeneratedRegex(@"\d+(?:\.\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex RxNumero();

    [GeneratedRegex(@"(?<![A-Za-z])[NSEW](?![A-Za-z])", RegexOptions.CultureInvariant)]
    private static partial Regex RxEmisfero();
}
