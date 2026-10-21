using System.Globalization;
using System.Text.RegularExpressions;

namespace Aip.Pdf.Italia;

/// <summary>Una riga dell'elenco dell'emendamento: la pagina e la sua data.</summary>
public sealed record AmendmentEntry(string Reference, DateOnly Effective);

/// <summary>
/// Una pagina nuova nel corpo dell'emendamento: la sua posizione nel PDF dell'emendamento (da 1), il riferimento e la data.
/// <c>ByPosition</c>: la pagina non ha testata (nell'A12/26 quattro pagine vuote di ENR 6.3) e riferimento e data vengono
/// dalla riga dell'elenco al suo posto.
/// </summary>
public sealed record AmendmentPage(int Position, string Reference, DateOnly Effective, bool ByPosition = false);

public enum AmendmentIssueKind
{
    /// <summary>La copertina non dice la data di entrata in vigore.</summary>
    NoEffectiveDate,

    /// <summary>Nessuna pagina con l'elenco «pagine da distruggere / da inserire».</summary>
    NoList,

    /// <summary>Una riga dell'elenco che comincia come un riferimento e non si legge. <c>Detail</c> = la riga.</summary>
    ListRowNotRead,

    /// <summary>Il corpo non segue l'elenco: al posto della riga dell'elenco c'è un'altra pagina. <c>Detail</c> = quale e quale.</summary>
    BodyMismatch,

    /// <summary>La pagina del corpo ha un'altra data da quella dell'elenco. <c>Detail</c> = il riferimento e le due date.</summary>
    BodyDate,

    /// <summary>Corpo ed elenco non hanno lo stesso numero di pagine. <c>Detail</c> = i due conti.</summary>
    BodyCount,

    /// <summary>Nel corpo non c'è il GEN 0.4, l'elenco di tutte le pagine valide: il controllo della cartella non si fa.</summary>
    NoChecklist,
}

/// <summary>Quel che il lettore dell'emendamento ha da dire. <c>Page</c>: la pagina del PDF dell'emendamento.</summary>
public sealed record AmendmentIssue(AmendmentIssueKind Kind, int? Page, string? Detail = null);

/// <summary>
/// Un emendamento AIRAC letto: il numero (<c>A12/26</c>), la data di entrata in vigore, le pagine da togliere e da
/// inserire, le pagine nuove nel corpo, e l'elenco di tutte le pagine valide col ciclo nuovo (il GEN 0.4).
/// </summary>
public sealed record AmendmentRead(
    string? Number,
    DateOnly? Effective,
    IReadOnlyList<AmendmentEntry> Destroy,
    IReadOnlyList<AmendmentEntry> Insert,
    IReadOnlyList<AmendmentPage> Pages,
    IReadOnlyDictionary<string, DateOnly> Checklist,
    IReadOnlyList<AmendmentIssue> Issues);

/// <summary>
/// L'emendamento AIRAC dell'AIP italiano (AIP AIRAC AMDT di ENAV; carta «L'AIP intero e gli emendamenti», passo 7). Tre
/// cose dentro: l'elenco «PAGINE DA DISTRUGGERE / PAGINE DA INSERIRE» a due colonne, le pagine nuove nello stesso ordine
/// dell'elenco, e fra queste il GEN 0.4, l'elenco di tutte le pagine valide a tre colonne.
/// <para>🔴 Si legge con la posizione delle parole: col testo a righe (<c>pdftotext</c>) le date finivano sulla riga
/// sbagliata (138 «date diverse» su 450 che non erano vere, prova del 21 ottobre).</para>
/// </summary>
public static partial class ItalianAmendment
{
    [GeneratedRegex(@"^(?<r>(?:GEN|ENR|AD)\s+[0-9][0-9.]*(?:\s+(?:[A-Z]{4}|[A-Z]{1,2}\d{1,4}))?(?:\s+[0-9.]+)?)\s+-\s+(?<n>\d+)\s+(?<d>\d{2}\s+[A-Z]{3}\s+\d{4})$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxRigaDellElenco();

    [GeneratedRegex(@"^(?:GEN|ENR|AD)\s+\d", RegexOptions.CultureInvariant)]
    private static partial Regex RxComeUnRiferimento();

    // Nel GEN 0.4 la parte (GEN, ENR, AD) sta nelle intestazioni della colonna e le righe di GEN ed ENR non la ripetono
    // («2.1.2.38 - 1  06 AUG 2026»); quelle dell'AD sì («AD 2 LIBD 4 - 15 …»).
    [GeneratedRegex(@"^(?<s>.+?)\s+-\s+(?<n>\d+)\s+(?<d>\d{2}\s+[A-Z]{3}\s+\d{4})$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxRigaDellaChecklist();

    [GeneratedRegex(@"^(?<p>GEN|ENR|AD)\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxParte();

    [GeneratedRegex(@"\((?<n>A\d{1,2}/\d{2})\)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxNumero();

    [GeneratedRegex(@"(?<d>\d{2}\s+(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC)\s+\d{4})", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxData();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RxSpazi();

    private static readonly HashSet<string> Mesi = new(["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"], StringComparer.Ordinal);

    /// <summary>Legge l'emendamento dalle sue pagine.</summary>
    public static AmendmentRead Read(IReadOnlyList<PdfPageText> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var problemi = new List<AmendmentIssue>();
        var (numero, inVigore) = Copertina(pages);
        if (inVigore is null)
            problemi.Add(new AmendmentIssue(AmendmentIssueKind.NoEffectiveDate, 1));

        var togli = new List<AmendmentEntry>();
        var metti = new List<AmendmentEntry>();
        var ultimaDellElenco = -1;
        for (var k = 0; k < pages.Count; k++)
        {
            if (!Elenco(pages[k], k + 1, togli, metti, problemi))
                continue;
            ultimaDellElenco = k;
        }
        if (ultimaDellElenco < 0)
        {
            problemi.Add(new AmendmentIssue(AmendmentIssueKind.NoList, null));
            return new AmendmentRead(numero, inVigore, togli, metti, [], new Dictionary<string, DateOnly>(), problemi);
        }

        // Il corpo segue l'elenco «da inserire», pagina per pagina.
        var corpo = new List<AmendmentPage>();
        var checklist = new Dictionary<string, DateOnly>(StringComparer.Ordinal);
        var parte = "GEN";
        var dopo = pages.Count - ultimaDellElenco - 1;
        if (dopo != metti.Count)
            problemi.Add(new AmendmentIssue(AmendmentIssueKind.BodyCount, null,
                string.Create(CultureInfo.InvariantCulture, $"{dopo} pagine nel corpo, {metti.Count} da inserire")));
        for (var i = 0; i < Math.Min(dopo, metti.Count); i++)
        {
            var k = ultimaDellElenco + 1 + i;
            var righe = Table.PlainLines(pages[k]);
            var riferimento = PageInfo.Reference(righe);
            var voce = metti[i];
            if (riferimento is null)
            {
                corpo.Add(new AmendmentPage(k + 1, voce.Reference, voce.Effective, ByPosition: true));
                continue;
            }
            if (riferimento != voce.Reference)
            {
                problemi.Add(new AmendmentIssue(AmendmentIssueKind.BodyMismatch, k + 1, $"{riferimento} al posto di {voce.Reference}"));
                continue;
            }
            var data = PageInfo.Date(righe);
            if (data != voce.Effective)
                problemi.Add(new AmendmentIssue(AmendmentIssueKind.BodyDate, k + 1,
                    $"{riferimento}: {data?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "senza data"} nella pagina, {voce.Effective.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} nell'elenco"));
            corpo.Add(new AmendmentPage(k + 1, riferimento, voce.Effective));
            if (riferimento.StartsWith("GEN 0.4-", StringComparison.Ordinal))
                Checklist(pages[k], checklist, ref parte);
        }
        if (checklist.Count == 0)
            problemi.Add(new AmendmentIssue(AmendmentIssueKind.NoChecklist, null));
        return new AmendmentRead(numero, inVigore, togli, metti, corpo, checklist, problemi);
    }

    // La copertina: «(A12/26)» e, dopo «Data di entrata in vigore/Effective date», la data.
    private static (string? Numero, DateOnly? InVigore) Copertina(IReadOnlyList<PdfPageText> pages)
    {
        if (pages.Count == 0)
            return (null, null);
        var righe = Table.PlainLines(pages[0]);
        var numero = righe.Select(r => RxNumero().Match(r)).FirstOrDefault(m => m.Success)?.Groups["n"].Value;
        for (var i = 0; i < righe.Count; i++)
        {
            // Non «Effective date»: lo dice anche la testata di ogni pagina («AIRAC effective date …»).
            if (!righe[i].Contains("entrata in vigore", StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (var r in righe.Skip(i).Take(3))
                if (RxData().Match(r) is { Success: true } d && Data(d.Groups["d"].Value) is { } data)
                    return (numero, data);
        }
        return (numero, null);
    }

    // Una pagina dell'elenco: due «PAGINE» in testa; la colonna di destra comincia prima della sua intestazione (nell'A12/26
    // le righe a x=301, l'intestazione a 388), quindi si taglia a metà fra le due intestazioni.
    private static bool Elenco(PdfPageText page, int numero, List<AmendmentEntry> togli, List<AmendmentEntry> metti, List<AmendmentIssue> problemi)
    {
        var testata = page.Words.Where(w => w.Text == "PAGINE").OrderBy(w => w.X0).ToList();
        if (testata.Count < 2 || !page.Words.Any(w => w.Text == "DISTRUGGERE"))
            return false;
        var taglio = (testata[0].X0 + testata[1].X0) / 2;
        foreach (var (sinistra, lista) in new[] { (true, togli), (false, metti) })
        {
            foreach (var riga in Righe(page.Words.Where(w => w.X0 < taglio == sinistra)))
            {
                var m = RxRigaDellElenco().Match(riga);
                if (m.Success && Data(m.Groups["d"].Value) is { } data)
                    lista.Add(new AmendmentEntry($"{RxSpazi().Replace(m.Groups["r"].Value, " ")}-{m.Groups["n"].Value}", data));
                else if (RxComeUnRiferimento().IsMatch(riga) && riga.Contains(" - ", StringComparison.Ordinal))
                    problemi.Add(new AmendmentIssue(AmendmentIssueKind.ListRowNotRead, numero, riga));
            }
        }
        return true;
    }

    // Il GEN 0.4: le colonne finiscono con la data; si leggono una dopo l'altra, dall'alto, tenendo la parte dalle
    // intestazioni («GEN 3», «ENR 2», «AD 2 (Altri AD/Other AD)»), che continua da una pagina all'altra.
    private static void Checklist(PdfPageText page, Dictionary<string, DateOnly> checklist, ref string parte)
    {
        var parole = page.Words.Where(w => w.Baseline > 45).ToList();
        var fini = parole.Where(w => w.Text.Length == 4 && w.Text.All(char.IsAsciiDigit)
                                     && parole.Any(m => Mesi.Contains(m.Text) && Math.Abs(m.Baseline - w.Baseline) < 2 && m.X1 <= w.X0 + 1 && w.X0 - m.X1 < 15))
            .Select(w => w.X1).Order().ToList();
        var colonne = new List<double>();
        foreach (var f in fini)
        {
            if (colonne.Count == 0 || f - colonne[^1] > 20) colonne.Add(f);
            else colonne[^1] = Math.Max(colonne[^1], f);
        }
        var inizio = double.MinValue;
        foreach (var fine in colonne)
        {
            var da = inizio;
            foreach (var riga in Righe(parole.Where(w => w.X0 >= da && w.X1 <= fine + 2)))
            {
                var m = RxRigaDellaChecklist().Match(riga);
                if (m.Success && Data(m.Groups["d"].Value) is { } data)
                {
                    var sezione = RxSpazi().Replace(m.Groups["s"].Value, " ");
                    if (!RxParte().IsMatch(sezione))
                        sezione = parte + " " + sezione;
                    checklist[$"{sezione}-{m.Groups["n"].Value}"] = data;
                }
                else if (RxParte().Match(riga) is { Success: true } p)
                {
                    parte = p.Groups["p"].Value;
                }
            }
            inizio = fine + 2;
        }
    }

    // Le parole a righe sulla linea di base, da sinistra a destra.
    private static IEnumerable<string> Righe(IEnumerable<PdfWord> parole)
    {
        var riga = new List<PdfWord>();
        foreach (var w in parole.OrderBy(w => w.Baseline).ThenBy(w => w.X0))
        {
            if (riga.Count > 0 && Math.Abs(w.Baseline - riga[0].Baseline) > 3)
            {
                yield return string.Join(' ', riga.OrderBy(x => x.X0).Select(x => x.Text));
                riga = [];
            }
            riga.Add(w);
        }
        if (riga.Count > 0)
            yield return string.Join(' ', riga.OrderBy(x => x.X0).Select(x => x.Text));
    }

    private static DateOnly? Data(string testo) =>
        DateOnly.TryParseExact(RxSpazi().Replace(testo, " "), "dd MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}
