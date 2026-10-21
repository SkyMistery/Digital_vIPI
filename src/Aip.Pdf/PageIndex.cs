using System.Globalization;

namespace Aip.Pdf;

/// <summary>
/// Una pagina dell'AIP nella cartella dei PDF: il file, la posizione nel file (da 1), il riferimento (<c>ENR 4.4-77</c>)
/// e la data. <c>ByPosition</c>: la pagina non ha una testata leggibile (nel 2611 le pagine vuote di ENR 6.3) e il
/// riferimento viene dalla posizione, perché le altre pagine dello stesso file la confermano.
/// </summary>
public sealed record IndexedPage(string File, int Position, string? Reference, DateOnly? Effective, bool ByPosition = false)
{
    /// <summary>La sezione: il riferimento senza il numero di pagina (<c>ENR 4.4</c>, <c>AD 2 LIRF 5</c>).</summary>
    public string? Section => Reference is { } r ? PageIndex.SectionOf(r) : null;

    /// <summary>Il numero di pagina scritto nel riferimento.</summary>
    public int? Number => Reference is { } r ? PageIndex.NumberOf(r) : null;
}

public enum PageIndexIssueKind
{
    /// <summary>Una pagina senza riferimento, che nemmeno la posizione sa dire.</summary>
    NoReference,

    /// <summary>Una pagina con il riferimento e senza data.</summary>
    NoDate,

    /// <summary>Il numero della pagina non è la sua posizione nel file. <c>Detail</c> = il riferimento.</summary>
    OutOfOrder,

    /// <summary>Un file con le pagine di più sezioni. <c>Detail</c> = le sezioni.</summary>
    MoreSections,

    /// <summary>Lo stesso riferimento in due posti. <c>Detail</c> = il riferimento e l'altro posto.</summary>
    Duplicate,

    /// <summary>🔴 Una pagina con una data dopo l'entrata in vigore del ciclo: è di un ciclo dopo. <c>Detail</c> = la data.</summary>
    AfterCycle,
}

/// <summary>Quel che l'indice delle pagine ha da dire: un codice, il file e la posizione.</summary>
public sealed record PageIndexIssue(PageIndexIssueKind Kind, string File, int? Position, string? Detail = null);

/// <summary>
/// L'indice delle pagine di una cartella dell'AIP (carta «L'AIP intero e gli emendamenti», passo 1): ogni pagina col suo
/// riferimento e la sua data, lette dalla testata come fa <see cref="PageInfo"/>. È la base del controllo del ciclo e
/// degli emendamenti, che sostituiscono pagine per riferimento.
/// </summary>
public static class PageIndex
{
    /// <summary>Le pagine di un file, dalla testata; quelle senza testata dalla posizione, se il file lo permette.</summary>
    public static IReadOnlyList<IndexedPage> Of(string file, IReadOnlyList<PdfPageText> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var lette = new List<IndexedPage>(pages.Count);
        for (var k = 0; k < pages.Count; k++)
        {
            var righe = Table.PlainLines(pages[k]);
            lette.Add(new IndexedPage(file, k + 1, PageInfo.Reference(righe), PageInfo.Date(righe)));
        }

        // Dalla posizione solo se il file è di una sezione sola e ogni pagina letta sta al posto del suo numero: allora
        // la pagina k senza testata è la k della stessa sezione. Altrimenti non si indovina.
        var conRiferimento = lette.Where(p => p.Reference is not null).ToList();
        var sezioni = conRiferimento.Select(p => p.Section).Distinct(StringComparer.Ordinal).ToList();
        if (sezioni.Count != 1 || conRiferimento.Any(p => p.Number != p.Position))
            return lette;
        return lette.Select(p => p.Reference is null
                ? p with { Reference = $"{sezioni[0]}-{p.Position.ToString(CultureInfo.InvariantCulture)}", ByPosition = true }
                : p)
            .ToList();
    }

    /// <summary>
    /// I controlli sull'indice di tutta la cartella: riferimenti e date mancanti, pagine fuori posto, file con più sezioni,
    /// riferimenti doppi, e — se si dà il ciclo — pagine con una data dopo la sua entrata in vigore.
    /// </summary>
    public static IReadOnlyList<PageIndexIssue> Check(IEnumerable<IndexedPage> pages, DateOnly? cycleEffective = null)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var tutte = pages.ToList();
        var problemi = new List<PageIndexIssue>();
        foreach (var file in tutte.GroupBy(p => p.File, StringComparer.Ordinal))
        {
            var sezioni = file.Select(p => p.Section).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
            if (sezioni.Count > 1)
                problemi.Add(new PageIndexIssue(PageIndexIssueKind.MoreSections, file.Key, null, string.Join(", ", sezioni)));
            foreach (var p in file)
            {
                if (p.Reference is null)
                    problemi.Add(new PageIndexIssue(PageIndexIssueKind.NoReference, p.File, p.Position));
                else if (p.Number != p.Position)
                    problemi.Add(new PageIndexIssue(PageIndexIssueKind.OutOfOrder, p.File, p.Position, p.Reference));
                // Una pagina riconosciuta dalla posizione non ha testata: la data non c'è, e non è un difetto della lettura.
                if (p.Reference is not null && !p.ByPosition && p.Effective is null)
                    problemi.Add(new PageIndexIssue(PageIndexIssueKind.NoDate, p.File, p.Position, p.Reference));
                if (cycleEffective is { } ciclo && p.Effective is { } data && data > ciclo)
                    problemi.Add(new PageIndexIssue(PageIndexIssueKind.AfterCycle, p.File, p.Position,
                        $"{p.Reference} {data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"));
            }
        }

        foreach (var doppio in tutte.Where(p => p.Reference is not null).GroupBy(p => p.Reference!, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            var primo = doppio.First();
            foreach (var altro in doppio.Skip(1))
                problemi.Add(new PageIndexIssue(PageIndexIssueKind.Duplicate, altro.File, altro.Position,
                    $"{doppio.Key} (anche in {primo.File} p.{primo.Position.ToString(CultureInfo.InvariantCulture)})"));
        }
        return problemi;
    }

    /// <summary>La sezione di un riferimento: <c>ENR 4.4-77</c> → <c>ENR 4.4</c>.</summary>
    public static string SectionOf(string reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var trattino = reference.LastIndexOf('-');
        return trattino < 0 ? reference : reference[..trattino];
    }

    /// <summary>Il numero di pagina di un riferimento: <c>ENR 4.4-77</c> → 77; null se non c'è.</summary>
    public static int? NumberOf(string reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var trattino = reference.LastIndexOf('-');
        return trattino >= 0 && int.TryParse(reference.AsSpan(trattino + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
    }
}
