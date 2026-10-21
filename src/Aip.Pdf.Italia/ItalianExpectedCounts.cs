using System.Text.RegularExpressions;

namespace Aip.Pdf.Italia;

/// <summary>Un conto che l'AIP stampa: che cosa (<c>zone</c>, <c>punti</c>) e quanti.</summary>
public sealed record ExpectedCount(string What, int Count);

/// <summary>
/// I conti che l'AIP italiano stampa e che si possono contare senza leggere l'oggetto (carta «L'AIP intero», passo 2e, «la
/// lettura dice cosa ha letto»): le zone numerate di CTA, TMA e CTR (<c>Zone '7'</c>), i punti di una rotta di ENR 3.2 (▲ e
/// ∆). Chi legge confronta col letto: un conto che non torna vuol dire che il lettore ha perso qualcosa, o che il PDF ha
/// cambiato forma.
/// </summary>
public static partial class ItalianExpectedCounts
{
    // A inizio riga: «Zone '1'», «Zona '5'», «Roma CTR Zona/Zone '1'», «MILANO TMA Zone '8'».
    [GeneratedRegex(@"^(?:[A-Za-z' ]+?\s(?:CTR|CTA|TMA)\s+)?Zon[ae](?:\s*/\s*Zone)?\s*'(?<n>\d+)'", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxZona();

    /// <summary>I conti del file, dal nome (<c>LI-ENR 2.1.1.4.3.pdf</c>) e dalle pagine; vuoto dove l'AIP non ne stampa.</summary>
    public static IReadOnlyList<ExpectedCount> Of(string fileName, IReadOnlyList<PdfPageText> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var nome = Path.GetFileNameWithoutExtension(fileName);
        if (nome.StartsWith("LI-ENR 2.1.1.", StringComparison.Ordinal) || nome.StartsWith("LI-ENR 2.1.2 ", StringComparison.Ordinal))
        {
            var zone = pages.SelectMany(Table.PlainLines).Select(l => RxZona().Match(l.Trim()))
                .Where(m => m.Success).Select(m => m.Groups["n"].Value).Distinct(StringComparer.Ordinal).Count();
            return zone == 0 ? [] : [new ExpectedCount("zone", zone)];
        }
        if (nome.StartsWith("LI-ENR 3.2 ", StringComparison.Ordinal))
        {
            var punti = pages.SelectMany(p => p.Words).Sum(w => w.Text.Count(c => c is '▲' or '∆'));
            return punti == 0 ? [] : [new ExpectedCount("punti", punti)];
        }
        return [];
    }

    /// <summary>Quanti oggetti del letto corrispondono a un conto: le aree con «Zone 'n'» nel nome, i punti delle rotte.</summary>
    public static int Read(string what, ImportResult read)
    {
        ArgumentNullException.ThrowIfNull(read);
        return what switch
        {
            "zone" => read.Airspaces.Count(a => RxZonaNelNome().IsMatch(a.Designator)),
            "punti" => read.Routes.Sum(r => r.Points.Count),
            _ => 0,
        };
    }

    [GeneratedRegex(@"Zon[ae]\s*'\d+'", RegexOptions.CultureInvariant)]
    private static partial Regex RxZonaNelNome();
}
