using System.Globalization;
using System.Text.RegularExpressions;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// La forma in cui un file scrive i suoi punti, e la riscrittura di una riga toccata in QUELLA forma (carta F2,
/// slice 2).
/// </summary>
/// <remarks>
/// <para>Gli scrittori di A serializzano sempre in DMS puntato (<c>N041.48.01.000</c>). Nel sector convivono
/// però tre forme — puntata (643 160 token sul master del 22 settembre 2026), compatta (42 253:
/// <c>N0414801000</c>, in <c>itgeo.geo</c>, nei <c>.mva</c>, nei <c>.vfi</c>) e decimale (38 righe di
/// <c>.str</c>: <c>41.00850773;16.07432896;</c>) — anche nello stesso file: <c>itgeo.geo</c> ne mescola due.
/// Riscrivere in puntato una riga compatta la farebbe risultare cambiata in ogni suo punto, e il diff che l'AOD
/// rilegge nella PR mentirebbe su cosa è stato toccato.</para>
/// <para>La regola: una riga toccata esce nella forma delle righe da cui era stata letta; se quelle non ne
/// dicono nessuna (un record fatto di nomi di punto), nella forma prevalente del file; se nemmeno il file ne
/// dice una, puntata.</para>
/// </remarks>
internal static class FormaDelPunto
{
    internal enum Forma { Puntata, Compatta, Decimale }

    // Un campo intero: preceduto da inizio riga o `;`, seguito da `;` o fine riga.
    private const string Inizio = @"(?<=^|;)\s*";
    private const string Fine = @"\s*(?=;|$)";

    private static readonly Regex Puntato = new(Inizio + @"[NSEWnsew]\d+\.\d+\.\d+(\.\d+)?" + Fine, RegexOptions.CultureInvariant);
    private static readonly Regex Compatto = new(Inizio + @"[NSEWnsew]\d{8,}" + Fine, RegexOptions.CultureInvariant);
    private static readonly Regex CoppiaDecimale = new(Inizio + @"[-+]?\d{1,3}\.\d+\s*;\s*[-+]?\d{1,3}\.\d+" + Fine, RegexOptions.CultureInvariant);

    // Ciò che gli scrittori producono: puntato a tre cifre di gradi e tre di millesimi (FormatComponent).
    private static readonly Regex PuntatoScritto = new(@"(?<=^|;)([NSEW])(\d{3})\.(\d{2})\.(\d{2})\.(\d{3})(?=;|$)", RegexOptions.CultureInvariant);
    private static readonly Regex CoppiaScritta = new(@"(?<=^|;)([NS]\d{3}\.\d{2}\.\d{2}\.\d{3});([EW]\d{3}\.\d{2}\.\d{2}\.\d{3})(?=;|$)", RegexOptions.CultureInvariant);

    /// <summary>La forma che queste righe dichiarano, o null se non contengono punti per coordinate.</summary>
    internal static Forma? Di(IEnumerable<string> righe)
    {
        int puntati = 0, compatti = 0, decimali = 0;
        foreach (string riga in righe)
        {
            if (riga.TrimStart().StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            puntati += Puntato.Count(riga);
            compatti += Compatto.Count(riga);
            decimali += CoppiaDecimale.Count(riga);
        }

        if (puntati + compatti > 0)
        {
            return compatti > puntati ? Forma.Compatta : Forma.Puntata;
        }

        return decimali > 0 ? Forma.Decimale : null;
    }

    /// <summary>Riscrive nella <paramref name="forma"/> data le righe che uno scrittore ha prodotto in puntato.</summary>
    internal static IEnumerable<string> In(IEnumerable<string> righeScritte, Forma forma) => forma switch
    {
        Forma.Compatta => righeScritte.Select(r => PuntatoScritto.Replace(r, "$1$2$3$4$5")),
        Forma.Decimale => righeScritte.Select(r => CoppiaScritta.Replace(r, InDecimale)),
        _ => righeScritte,
    };

    private static string InDecimale(Match coppia)
    {
        var punto = CoordinateConverter.ParsePair(coppia.Groups[1].Value, coppia.Groups[2].Value);
        return string.Create(CultureInfo.InvariantCulture, $"{punto.LatitudeDeg:F8};{punto.LongitudeDeg:F8}");
    }
}
