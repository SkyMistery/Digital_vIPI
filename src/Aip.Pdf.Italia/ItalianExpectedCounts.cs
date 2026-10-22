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
        // Il testo di uno scalo, AD 2 e AD 3 (passo 5): i titoli delle sottosezioni («LICA AD 2.3 …»), uno per numero.
        if (RxTestoDiScalo().IsMatch(nome))
        {
            var sottosezioni = pages.SelectMany(Table.PlainLines).Select(l => RxTitoloDiSottosezione().Match(l.Trim()))
                .Where(m => m.Success).Select(m => m.Groups["n"].Value).Distinct(StringComparer.Ordinal).Count();
            return sottosezioni == 0 ? [] : [new ExpectedCount("sottosezioni", sottosezioni)];
        }
        // Le procedure di uno scalo (passo 6): i designatori stampati nei file 4 (STAR) e 6 (SID) di AD 2 e AD 3.
        if (RxProcedureDiScalo().IsMatch(nome))
        {
            var designatori = ItalianAd2Procedures.DesignatoriStampati(pages).Count;
            return designatori == 0 ? [] : [new ExpectedCount("procedure", designatori)];
        }
        // ENR 5.4 (carta «L'AIP intero», passo 4): le sigle T……, P……, L…… sulle pagine girate, fino alla tabella delle note
        // (dove le sigle tornano, anche dentro il testo: «Stesso ostacolo dell'ID: T001673»).
        if (nome.StartsWith("LI-ENR 5.4.", StringComparison.Ordinal))
        {
            var sigle = 0;
            foreach (var girata in pages.Select(p => p.Turned).OfType<PdfPageText>())
            {
                var note = girata.Words.Where(w => w.Text == "NOTE/REMARKS").Select(w => (double?)w.Baseline).Min();
                sigle += girata.Words.Count(w => RxSigla().IsMatch(w.Text) && (note is null || w.Baseline < note));
                if (note is not null) break;
            }
            return sigle == 0 ? [] : [new ExpectedCount("ostacoli", sigle)];
        }
        // ENR 5.5 e 5.6 (passo 4): le coordinate stampate nella prima colonna, una per vertice, centro o punto di corridoio.
        if (nome.StartsWith("LI-ENR 5.5.", StringComparison.Ordinal) || nome.StartsWith("LI-ENR 5.6.1.", StringComparison.Ordinal))
        {
            var coordinate = Coordinate(pages);
            return coordinate == 0 ? [] : [new ExpectedCount("coordinate", coordinate)];
        }
        return [];
    }

    /// <summary>
    /// Le coordinate della prima colonna (a sinistra del 40% della pagina: le note a destra ne citano altre, ENR 5.6.1.7),
    /// come punti di una geometria: un punto stampato due volte di seguito è uno (ENR 5.6.1.10, Gola della Rossa), e il
    /// primo punto riscritto prima di «to point of origin» chiude l'anello, non è un vertice (Monti Sibillini).
    /// </summary>
    private static int Coordinate(IReadOnlyList<PdfPageText> pages)
    {
        var conto = 0;
        var anello = new List<string>();
        void Chiudi(bool origine)
        {
            if (origine && anello.Count > 1 && anello[^1] == anello[0]) anello.RemoveAt(anello.Count - 1);
            conto += anello.Count;
            anello.Clear();
        }
        foreach (var page in pages)
        {
            var sinistra = page with { Words = [.. page.Words.Where(w => w.X0 < page.Width * 0.4)] };
            foreach (var riga in Table.PlainLines(sinistra))
            {
                // Un cerchio o un corridoio comincia una geometria nuova: non è il seguito dell'anello di prima.
                if (riga.Contains("Circular area", StringComparison.OrdinalIgnoreCase) || riga.Contains("Corridor", StringComparison.OrdinalIgnoreCase))
                    Chiudi(origine: false);
                foreach (Match m in RxCoordinata().Matches(riga))
                {
                    var c = RxSpazi().Replace(m.Value, "");
                    if (anello.Count == 0 || anello[^1] != c) anello.Add(c);
                }
                if (riga.Contains("point of origin", StringComparison.OrdinalIgnoreCase)) Chiudi(origine: true);
            }
        }
        Chiudi(origine: false);
        return conto;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex RxSpazi();

    [GeneratedRegex(@"^[TPL]\d{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex RxSigla();

    [GeneratedRegex(@"^LI-AD [23]\s+[A-Z]{4}\b.*\s1$", RegexOptions.CultureInvariant)]
    private static partial Regex RxTestoDiScalo();

    [GeneratedRegex(@"^[A-Z]{4}\s+AD\s+[23]\.(?<n>\d+)\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxTitoloDiSottosezione();

    [GeneratedRegex(@"^LI-AD [23]\s+[A-Z]{4}\b.*\s[46]$", RegexOptions.CultureInvariant)]
    private static partial Regex RxProcedureDiScalo();

    // «45°59'15"N 012°55'21"E», anche con ’’ o ″ per i secondi.
    [GeneratedRegex(@"\d{1,2}[°º˚]\s*\d{1,2}['’′]\s*\d{1,2}(?:[.,]\d+)?\s*(?:""|''|’’|″|”)\s*[NS]\s*,?\s*\d{1,3}[°º˚]\s*\d{1,2}['’′]\s*\d{1,2}(?:[.,]\d+)?\s*(?:""|''|’’|″|”)\s*[EW]", RegexOptions.CultureInvariant)]
    private static partial Regex RxCoordinata();

    /// <summary>I punti stampati di una geometria: un vertice per lato (il centro di un arco in più), il centro di un cerchio, i punti di un corridoio.</summary>
    private static int Punti(Geometry g) => g switch
    {
        RingGeometry r => r.Segments.Count + r.Segments.Count(s => s is ArcSegment),
        CircleGeometry => 1,
        CorridorGeometry c => c.Points.Count,
        _ => 0,
    };

    /// <summary>Quanti oggetti del letto corrispondono a un conto: le aree con «Zone 'n'» nel nome, i punti delle rotte.</summary>
    public static int Read(string what, ImportResult read)
    {
        ArgumentNullException.ThrowIfNull(read);
        return what switch
        {
            "zone" => read.Airspaces.Count(a => RxZonaNelNome().IsMatch(a.Designator)),
            "punti" => read.Routes.Sum(r => r.Points.Count),
            "ostacoli" => read.Obstacles.Count,
            "sottosezioni" => read.Aerodromes.Sum(a => a.Sections?.Count ?? 0),
            "coordinate" => read.Airspaces.Sum(a => Punti(a.Geometry)),
            "procedure" => read.Procedures.Where(p => p.Kind != ProcedureKind.InitialClimb).Select(p => p.Designator).Distinct(StringComparer.Ordinal).Count(),
            _ => 0,
        };
    }

    [GeneratedRegex(@"Zon[ae]\s*'\d+'", RegexOptions.CultureInvariant)]
    private static partial Regex RxZonaNelNome();
}
