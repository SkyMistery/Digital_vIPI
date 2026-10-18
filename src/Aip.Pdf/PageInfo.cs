using System.Globalization;
using System.Text.RegularExpressions;

namespace Aip.Pdf;

/// <summary>
/// Il riferimento di una pagina (<c>ENR 5.1.2 - 1</c>) e la sua data AIRAC (<c>AIRAC effective date 09 JUL 2026</c>):
/// la forma dell'eAIP europeo, comune agli Stati che lo usano. ⚠️ Ogni pagina ha la sua data (carta madre §5): nel
/// ciclo 2609 ENR 5.1.1 p.1 era del 10 luglio 2025 e ENR 5.1.4 del 5 ottobre 2023.
/// </summary>
public static partial class PageInfo
{
    [GeneratedRegex(@"AIRAC\s+effective\s+date\s+(?<d>\d{1,2})\s+(?<m>[A-Z]{3})\s+(?<y>\d{4})", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxData();

    [GeneratedRegex(@"^(?<r>(?:GEN|ENR|AD)\s+[0-9][0-9.]*(?:\s+[A-Z]{4})?(?:\s+[0-9.]+)?\s*-\s*\d+)\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxRiferimento();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RxSpazi();

    public static DateOnly? Date(IEnumerable<string> lines)
    {
        foreach (var l in lines)
        {
            var m = RxData().Match(l);
            if (m.Success
                && DateOnly.TryParseExact($"{m.Groups["d"].Value.PadLeft(2, '0')} {m.Groups["m"].Value} {m.Groups["y"].Value}",
                    "dd MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d;
        }
        return null;
    }

    /// <summary>Il riferimento della pagina, dalla prima riga che ne ha la forma (in testa, a destra o a sinistra).</summary>
    public static string? Reference(IEnumerable<string> lines)
    {
        foreach (var l in lines.Take(4))
        {
            var m = RxRiferimento().Match(l.Trim());
            if (m.Success) return RxSpazi().Replace(m.Groups["r"].Value, " ").Replace(" - ", "-").Replace(" -", "-");
        }
        return null;
    }
}
