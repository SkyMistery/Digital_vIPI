using System.Text.RegularExpressions;

namespace Aip.Pdf.Italia;

/// <summary>
/// ENR 2.1.1.1-3, quel che delle FIR non sta nelle tabelle delle aree (carta «L'AIP intero», passo 2c): le linee che
/// dividono le aree degli ACC/FIC, scritte nelle note della FIR, e le aree VFR della FIR (Roma: l'area TIBA «Isola di
/// Ponza»). Si legge la colonna italiana; l'inglese a destra dice le stesse cose.
/// </summary>
public static partial class ItalianEnr2Fir
{
    [GeneratedRegex(@"^(?<n>\d+)\.\s+(?<u>[A-Z][A-Za-z]+\s+ACC(?:/FIC)?)\s*:\s*(?<resto>.*)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Singleline)]
    private static partial Regex RxUnita();

    [GeneratedRegex(@"^\d+\.\s", RegexOptions.CultureInvariant)]
    private static partial Regex RxInizioDellaNota();

    // «a nord-ovest della linea congiungente i punti … nello spazio aereo compreso tra SFC e FL 195 (incluso)»; «ad est».
    [GeneratedRegex(@"^ad?\s+(?<lato>[a-z]+(?:-[a-z]+)?)\s+della\s+linea\s+congiungente\s+i\s+punti\s+(?<punti>.+?)(?:\s+nello\s+spazio\s+aereo\s+compreso\s+tra\s+(?<giu>.+?)\s+e\s+(?<su>.+?))?\s*[.;]?$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxFrase();

    // Dove comincia una frase: dopo «;» o «.», «a …» o «ad …».
    [GeneratedRegex(@"(?<=[;.])\s+(?=ad?\s+[a-z])", RegexOptions.CultureInvariant)]
    private static partial Regex RxTraLeFrasi();

    // Un punto: le coordinate (anche «433732N - 0121235E», 2.1.1.1 nel 2611) o un nome di cinque lettere.
    [GeneratedRegex(@"(?<c>\d{6}N\s*-?\s*\d{7}E)|(?<n>\b[A-Z]{5}\b)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxPunto();

    [GeneratedRegex(@"^ENR\s+2\.1\.1\.\d+\.\d+\s", RegexOptions.CultureInvariant)]
    private static partial Regex RxTitoloDellaSezione();

    [GeneratedRegex(@"^\(?\s*\d{2}/\d{2}\s*\)?$|^\d{1,2}\s+[A-Z]{3}\s+\d{4}$|AIP - Italia|ENAV - Roma|AIRAC effective date", RegexOptions.CultureInvariant)]
    private static partial Regex RxTestataOPiede();

    [GeneratedRegex(@"\s*\((?:incluso|escluso)\)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex RxInclusoEscluso();

    [GeneratedRegex(@"^[a-z]\.\s+(?<fir>[A-Z][a-z]+)\s+FIR\s+VFR\s+[""“”](?<nome>[^""“”]+)[""“”]", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxTitoloVfr();

    [GeneratedRegex(@"^[a-z]\.\s", RegexOptions.CultureInvariant)]
    private static partial Regex RxParagrafo();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RxSpazi();

    /// <summary>Le linee fra le aree degli ACC/FIC e le aree VFR di una FIR.</summary>
    public static ImportResult Read(IReadOnlyList<PdfPageText> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var problemi = new List<ImportIssue>();
        var linee = LineeDegliAcc(pages, problemi);
        var aree = AreeVfr(pages, problemi);
        return new ImportResult(aree, [], problemi) { UnitLines = linee };
    }

    // ---- Le linee fra gli ACC ----

    private static List<AtsUnitLine> LineeDegliAcc(IReadOnlyList<PdfPageText> pages, List<ImportIssue> problemi)
    {
        // Le righe italiane delle note: dalla prima «NOTE» al titolo della sezione dopo.
        var righe = new List<(string Testo, PdfPageText Pagina)>();
        var dentro = false;
        foreach (var page in pages)
        {
            var sinistra = page with { Words = page.Words.Where(w => w.X1 <= page.Width / 2 + 5).ToList() };
            foreach (var testo in Table.PlainLines(sinistra).Select(t => t.Trim()))
            {
                if (!dentro)
                {
                    dentro = testo == "NOTE";
                    continue;
                }
                if (RxTitoloDellaSezione().IsMatch(testo))
                    goto fine;
                if (testo.Length == 0 || RxTestataOPiede().IsMatch(testo) || PageInfo.Reference([testo]) is not null)
                    continue;
                righe.Add((testo, page));
            }
        }
        fine:

        // Le note: una riga che comincia con «N.» ne apre una.
        var note = new List<(string Testo, PdfPageText Pagina)>();
        foreach (var (testo, pagina) in righe)
        {
            if (RxInizioDellaNota().IsMatch(testo) || note.Count == 0) note.Add((testo, pagina));
            else note[^1] = (note[^1].Testo + " " + testo, note[^1].Pagina);
        }

        var linee = new List<AtsUnitLine>();
        foreach (var (testo, pagina) in note)
        {
            if (RxUnita().Match(RxSpazi().Replace(testo, " ")) is not { Success: true } u) continue;
            var piane = Table.PlainLines(pagina);
            var foglio = PageInfo.Reference(piane) is { } rif ? new AipPage(rif, PageInfo.Date(piane)) : null;
            var sezione = foglio is null ? "ENR 2.1.1" : PageIndex.SectionOf(foglio.Reference);
            var unita = u.Groups["u"].Value;
            // Le frasi hanno il segno d'elenco davanti («▪ a nord-ovest della linea…»).
            var resto = RxSpazi().Replace(u.Groups["resto"].Value.Replace("▪", " ", StringComparison.Ordinal), " ").Trim();
            foreach (var frase in RxTraLeFrasi().Split(resto))
            {
                var f = frase.Trim().TrimEnd('.', ';').Trim();
                if (RxFrase().Match(f) is not { Success: true } m)
                {
                    problemi.Add(new ImportIssue(ImportIssueKind.UnitLine, unita, pagina.Number, $"frase non letta: «{f}»"));
                    continue;
                }
                var punti = RxPunto().Matches(m.Groups["punti"].Value)
                    .Select(p => p.Groups["c"].Success
                        ? new LinePoint { Position = GeometryText.ReadPoint(p.Value.Replace("-", " ", StringComparison.Ordinal)) }
                        : new LinePoint { Designator = p.Value })
                    .ToList();
                if (punti.Count < 2 || punti.Any(p => p.Designator is null && p.Position is null))
                    problemi.Add(new ImportIssue(ImportIssueKind.UnitLine, unita, pagina.Number, $"punti non letti: «{m.Groups["punti"].Value}»"));
                linee.Add(new AtsUnitLine
                {
                    Section = sezione,
                    Unit = unita,
                    Side = m.Groups["lato"].Value,
                    Points = punti,
                    Lower = m.Groups["giu"].Success ? Limite(m.Groups["giu"].Value) : null,
                    Upper = m.Groups["su"].Success ? Limite(m.Groups["su"].Value) : null,
                    Text = f,
                    Page = foglio,
                });
            }
        }
        return linee;
    }

    private static VerticalLimit Limite(string testo) =>
        LimitText.Parse(RxInclusoEscluso().Replace(testo, "")) with { Text = testo.Trim() };

    // ---- Le aree VFR ----

    // Il titolo «a. Roma FIR VFR "Isola di Ponza"», sotto la tabella a due colonne (nome e limiti laterali · limiti
    // verticali) fino al paragrafo dopo («b.»).
    private static List<Airspace> AreeVfr(IReadOnlyList<PdfPageText> pages, List<ImportIssue> problemi)
    {
        var aree = new List<Airspace>();
        foreach (var page in pages)
        {
            var meta = page.Width / 2;
            var sinistra = Table.PlainLinesWithY(page with { Words = page.Words.Where(w => w.X1 <= meta + 5).ToList() });
            for (var i = 0; i < sinistra.Count; i++)
            {
                if (RxTitoloVfr().Match(sinistra[i].Text.Trim()) is not { Success: true } t) continue;
                var nome = t.Groups["nome"].Value;
                // La riga dei numeri («1  2») apre i dati; il paragrafo dopo li chiude.
                var verticali = page.Words.Where(w => w.Text == "VERTICALI" && w.Baseline > sinistra[i].Y).MinBy(w => w.Baseline);
                var numeri = page.Words.Where(w => w.Text == "1" && w.Baseline > sinistra[i].Y).MinBy(w => w.Baseline);
                var dopo = sinistra.Skip(i + 1).Where(l => numeri is not null && l.Y > numeri.Baseline + 2 && RxParagrafo().IsMatch(l.Text.Trim()))
                    .Select(l => (double?)l.Y).FirstOrDefault();
                if (verticali is null || numeri is null)
                {
                    problemi.Add(new ImportIssue(ImportIssueKind.Geometry, nome, page.Number, "area VFR senza la tabella"));
                    continue;
                }
                var colonna = verticali.X0 - 40;
                var fine = dopo ?? double.MaxValue;
                var dati = page.Words.Where(w => w.Baseline > numeri.Baseline + 2 && w.Baseline < fine - 2).ToList();
                var testo = string.Join(' ', Table.PlainLines(page with { Words = dati.Where(w => w.X1 <= colonna).ToList() }));
                var limiti = Table.PlainLines(page with { Words = dati.Where(w => w.X0 >= colonna).ToList() })
                    .Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
                var geometria = GeometryText.Read(testo);
                foreach (var p in geometria.Issues)
                    problemi.Add(new ImportIssue(ImportIssueKind.Geometry, nome, page.Number, $"{p.Kind}: {p.Detail}"));
                if (geometria.Geometries.Count == 0 || limiti.Count < 2)
                {
                    problemi.Add(new ImportIssue(ImportIssueKind.NoGeometry, nome, page.Number, testo));
                    continue;
                }
                var piane = Table.PlainLines(page);
                aree.Add(new Airspace
                {
                    Section = "ENR 2.1",
                    Type = AirspaceType.Other,
                    Designator = nome,
                    Upper = LimitText.Parse(limiti[0]),
                    Lower = LimitText.Parse(limiti[^1]),
                    Remarks = $"{t.Groups["fir"].Value} FIR VFR",
                    Geometry = geometria.Geometries[0],
                    Page = PageInfo.Reference(piane) is { } rif ? new AipPage(rif, PageInfo.Date(piane)) : null,
                });
            }
        }
        return aree;
    }
}
