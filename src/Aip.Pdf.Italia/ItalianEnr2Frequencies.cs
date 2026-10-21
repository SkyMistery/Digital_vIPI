using System.Globalization;
using System.Text.RegularExpressions;

namespace Aip.Pdf.Italia;

/// <summary>
/// ENR 2.1.1.1-3: le tabelle delle frequenze ACC, SCCAM e FIC delle tre FIR (carta «L'AIP intero», passo 2b). Sette colonne
/// — stazione · servizio · nominativo · emissione · frequenza · orario · note —, con la testata ripetuta in ogni pagina.
/// <para>Una riga della tabella è la fascia fra due bordi orizzontali <b>della colonna del nominativo</b>: stazione e servizio
/// sono celle unite su più righe, col testo in alto (Roma ACC nel 2611: «Servizio di controllo di avvicinamento» per Roma
/// Arrivi, Roma Partenze e Roma Radar), e passano alle righe sotto finché non ne viene uno nuovo. Dentro una riga non ci
/// sono bordi fra una frequenza e l'altra, e gli orari stanno a cavallo (vedi <see cref="AtsUnitFrequencies"/>). Una
/// fascia senza stazione, servizio e nominativo continua la riga prima (Milano ACC va avanti per tre pagine).</para>
/// </summary>
public static partial class ItalianEnr2Frequencies
{
    // «CH 118.805», e anche «CH125.485» (Padova ACC nel 2611).
    [GeneratedRegex(@"^(?<t>(?:CH\s*)?(?<f>\d{3}\.\d{1,3}))(?<r>(?:\s*\(\d+\))*)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxFrequenza();

    [GeneratedRegex(@"^(?:\(\d+\)\s*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex RxRimandi();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RxSpazi();

    /// <summary>Le righe delle tabelle delle frequenze nelle pagine di un PDF di ENR 2.1.1.</summary>
    public static ImportResult Read(IReadOnlyList<PdfPageText> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var righe = new List<AtsUnitFrequencies>();
        var problemi = new List<ImportIssue>();
        foreach (var page in pages)
        {
            var piane = Table.PlainLines(page);
            var riferimento = PageInfo.Reference(piane);
            var pagina = riferimento is null ? null : new AipPage(riferimento, PageInfo.Date(piane));
            var sezione = riferimento is null ? "ENR 2.1.1" : PageIndex.SectionOf(riferimento);
            foreach (var griglia in Table.FindAll(page, 300).Where(g => g.Columns.Count == 8))
            {
                var linee = Table.Lines(page, griglia);
                if (!linee.Any(l => l.Text.Contains("FREQUENZA", StringComparison.Ordinal)) || !linee.Any(l => l.Text.Contains("STAZIONE", StringComparison.Ordinal))) continue;
                // I dati cominciano sotto la riga dei numeri delle colonne («1 2 3 4 5 6 7»).
                var numeri = linee.Where(l => l.Column == 0 && l.Text.Trim() == "1").Select(l => l.Y).DefaultIfEmpty(double.NaN).Min();
                if (double.IsNaN(numeri)) continue;
                var dati = linee.Where(l => l.Y > numeri + 2).ToList();
                var bande = Fasce(page, griglia).Where(b => b.Bottom > numeri + 2).ToList();
                if (bande.Count == 0) bande = [(numeri + 2, griglia.Bottom)];
                foreach (var (alto, basso) in bande)
                {
                    var nella = dati.Where(l => l.Y > alto - 1 && l.Y <= basso + 1).OrderBy(l => l.Y).ThenBy(l => l.Column).ToList();
                    if (nella.Count == 0) continue;
                    string? Testo(int colonna, string giunto = " ") =>
                        nella.Where(l => l.Column == colonna).Select(l => RxSpazi().Replace(l.Text.Trim(), " ")).ToList() is { Count: > 0 } t
                            ? Cuci(t, giunto)
                            : null;

                    var frequenze = new List<AtsFrequency>();
                    foreach (var l in nella.Where(l => l.Column == 4))
                    {
                        var t = l.Text.Trim();
                        if (RxFrequenza().Match(t) is { Success: true } f)
                        {
                            var emissione = nella.Where(e => e.Column == 3 && Math.Abs(e.Y - l.Y) <= 2.5).Select(e => e.Text.Trim()).FirstOrDefault();
                            var rimandi = RxSpazi().Replace(f.Groups["r"].Value, "");
                            frequenze.Add(new AtsFrequency
                            {
                                Text = RxSpazi().Replace(f.Groups["t"].Value, " "),
                                Megahertz = double.Parse(f.Groups["f"].Value, CultureInfo.InvariantCulture),
                                Emission = emissione,
                                NoteRefs = rimandi.Length == 0 ? null : rimandi,
                            });
                        }
                        else if (RxRimandi().IsMatch(t) && frequenze.Count > 0)
                        {
                            // I rimandi sulla riga sotto la frequenza («124.200 (1)» / «134.200» / «(1) (2)»).
                            frequenze[^1] = frequenze[^1] with { NoteRefs = (frequenze[^1].NoteRefs ?? "") + RxSpazi().Replace(t, "") };
                        }
                        else
                        {
                            problemi.Add(new ImportIssue(ImportIssueKind.Frequency, Testo(0), page.Number, t));
                        }
                    }

                    var (stazione, servizio, nominativo) = (Testo(0), Testo(1), Testo(2));
                    var riga = new AtsUnitFrequencies
                    {
                        Section = sezione,
                        Station = stazione,
                        Service = servizio,
                        CallSign = nominativo,
                        Frequencies = frequenze,
                        Hours = Testo(5, "\n"),
                        Remarks = Testo(6, "\n"),
                        Page = pagina,
                    };
                    var primaDellaSezione = righe.Count > 0 && righe[^1].Section == sezione ? righe[^1] : null;
                    // Le celle unite: la stazione passa finché non ne viene una nuova, il servizio anche (non oltre la stazione).
                    if (nominativo is not null && primaDellaSezione is not null)
                        riga = riga with
                        {
                            Station = stazione ?? primaDellaSezione.Station,
                            Service = servizio ?? (stazione is null ? primaDellaSezione.Service : null),
                        };
                    // Una fascia senza stazione, servizio e nominativo continua la riga prima (anche dalla pagina prima).
                    if (stazione is null && servizio is null && nominativo is null && primaDellaSezione is not null)
                    {
                        var prima = righe[^1];
                        righe[^1] = prima with
                        {
                            Frequencies = [.. prima.Frequencies, .. frequenze],
                            Hours = Unisci(prima.Hours, riga.Hours),
                            Remarks = Unisci(prima.Remarks, riga.Remarks),
                        };
                    }
                    else if (frequenze.Count > 0 || riga.Station is not null)
                    {
                        righe.Add(riga);
                    }
                }
            }
        }
        foreach (var r in righe.Where(r => r.Frequencies.Count == 0))
            problemi.Add(new ImportIssue(ImportIssueKind.Frequency, r.Station, null, "riga senza frequenze"));
        return new ImportResult([], [], problemi) { UnitFrequencies = righe };
    }

    private static string? Unisci(string? a, string? b) => a is null ? b : b is null ? a : a + "\n" + b;

    /// <summary>
    /// Le fasce fra i bordi orizzontali che attraversano la colonna del nominativo (la terza): sono le righe più fini della
    /// tabella. I bordi della sola stazione o del solo servizio non ci sono: quelle celle sono unite.
    /// </summary>
    private static List<(double Top, double Bottom)> Fasce(PdfPageText page, TableGrid griglia)
    {
        if (page.HorizontalRules is not { Count: > 0 } bordi) return [];
        var ys = bordi.Where(b => b.X0 <= griglia.Columns[2] + 5 && b.X1 >= griglia.Columns[3] - 5)
            .Where(b => b.Y >= griglia.Top - 2 && b.Y <= griglia.Bottom + 2)
            .Select(b => b.Y).Order().ToList();
        var uniche = new List<double>();
        foreach (var y in ys)
            if (uniche.Count == 0 || y - uniche[^1] > 1.0) uniche.Add(y);
        var fasce = new List<(double, double)>();
        for (var i = 0; i + 1 < uniche.Count; i++) fasce.Add((uniche[i], uniche[i + 1]));
        return fasce;
    }

    /// <summary>Le righe di una cella unite; una parola spezzata a fine riga si ricuce («Infor-» «mazioni/»).</summary>
    private static string Cuci(IReadOnlyList<string> righe, string giunto)
    {
        var testo = new System.Text.StringBuilder(righe[0]);
        for (var i = 1; i < righe.Count; i++)
        {
            if (giunto == " " && testo.Length > 1 && testo[^1] == '-' && char.IsLetter(testo[^2]) && righe[i].Length > 0 && char.IsLower(righe[i][0]))
                testo.Length--;
            else
                testo.Append(giunto);
            testo.Append(righe[i]);
        }
        return testo.ToString();
    }
}
