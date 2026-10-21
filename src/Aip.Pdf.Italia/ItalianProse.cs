using System.Globalization;
using System.Text.RegularExpressions;

namespace Aip.Pdf.Italia;

/// <summary>
/// I testi in prosa dell'AIP italiano (carta «L'AIP intero», passo 4): due colonne, l'italiano a sinistra e l'inglese a
/// destra, fra la testata e il piè di pagina. La colonna inglese comincia sempre alla stessa x (le righe sono allineate a
/// sinistra): quella x è la più frequente fra le parole a metà pagina. Le righe si uniscono in paragrafi; un paragrafo
/// nuovo comincia dopo uno spazio più largo dell'interlinea, a un numero («2.1.», «3 »), a un punto elenco, a «NOTA».
/// </summary>
public static partial class ItalianProse
{
    // Il numero apre un paragrafo se dopo c'è una maiuscola: «2.1. Quando», «3 Zona pericolosa»; non «15 m (per es.» (5.3.4).
    [GeneratedRegex(@"^(?:\d+(?:\.\d+)*\.?\s+[A-ZÀ-Ý]|[•▪\-–]\s?|\(?[a-z]\)\s|NOTA\b|NOTE\b|REMARKS?\b)", RegexOptions.CultureInvariant)]
    private static partial Regex RxInizioDiParagrafo();

    // Un titolo numerato: «3 Procedure per gli aeromobili…», «10 ATTRAVERSAMENTO…» (il numero senza punto).
    [GeneratedRegex(@"^\d+\s+[A-ZÀ-Ý]", RegexOptions.CultureInvariant)]
    private static partial Regex RxTitoloNumerato();

    /// <summary>Una riga tutta in maiuscolo (le lettere che ha).</summary>
    private static bool Maiuscolo(string riga) => riga.Any(char.IsLetter) && !riga.Any(char.IsLower);

    /// <summary>Tutto il file è testo (<c>LI-ENR 5.3.4</c>, le introduzioni <c>LI-ENR 5.1</c>, <c>5.2.1</c>, <c>5.6.1</c>).</summary>
    public static ImportResult Read(IReadOnlyList<PdfPageText> pages, string section)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var pagine = pages.Select(Pagina).ToList();
        var testo = Testo(pages.Select(p => (p, p.Words.Where(w => DelCorpo(p, w)).ToList())).ToList(), section, pagine);
        return new ImportResult([], pagine, []) { Texts = testo is null ? [] : [testo] };
    }

    /// <summary>
    /// Il testo prima della prima tabella di <paramref name="colonne"/> colonne (ENR 5.5.1: le regole dei lanci, tre pagine
    /// e mezza prima delle zone): le pagine intere fino a quella della tabella, e lì quel che sta sopra. Null se non c'è.
    /// </summary>
    public static AipText? BeforeTable(IReadOnlyList<PdfPageText> pages, string section, int colonne)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var parti = new List<(PdfPageText, List<PdfWord>)>();
        foreach (var p in pages)
        {
            var griglia = Table.Find(p, rules: colonne + 1, minSpan: 400);
            // Sopra la tabella, e sopra la sua testata: la riga «Limite sup.» sta sopra il primo bordo.
            var cima = griglia is null ? double.MaxValue : griglia.Top - 2;
            parti.Add((p, p.Words.Where(w => DelCorpo(p, w) && w.Baseline < cima).ToList()));
            if (griglia is not null) break;
        }
        return Testo(parti, section, [.. pages.Select(Pagina)]);
    }

    private static ImportPage Pagina(PdfPageText p)
    {
        var piane = Table.PlainLines(p);
        return new ImportPage(p.Number, PageInfo.Reference(piane), PageInfo.Date(piane), Table: false);
    }

    /// <summary>Fra la testata della pagina («ENR 5.3.4 - 1», la data) e il piè di pagina («ENAV - Roma», l'emendamento).</summary>
    private static bool DelCorpo(PdfPageText page, PdfWord w) => w.Baseline > 40 && w.Baseline < page.Height - 20;

    private static AipText? Testo(List<(PdfPageText Pagina, List<PdfWord> Parole)> parti, string section, List<ImportPage> pagine)
    {
        var italiano = new List<(double Y, string Riga)>();
        var inglese = new List<(double Y, string Riga)>();
        ImportPage? prima = null;
        var scarto = 0.0;
        foreach (var (pagina, parole) in parti)
        {
            if (parole.Count == 0) continue;
            prima ??= pagine.FirstOrDefault(p => p.Number == pagina.Number);
            var destra = ColonnaInglese(pagina, parole);
            // Le righe delle pagine dopo vengono dopo: la y cresce di pagina in pagina.
            // Il punto elenco dell'inglese sta qualche punto prima della colonna (ENR 5.5.1 p.2-3): è dell'inglese.
            bool Inglese(PdfWord w) => w.X0 >= destra - 1 || (w.Text is "•" or "▪" && w.X0 >= destra - 15);
            italiano.AddRange(Righe(parole.Where(w => !Inglese(w))).Select(r => (r.Y + scarto, r.Riga)));
            inglese.AddRange(Righe(parole.Where(Inglese)).Select(r => (r.Y + scarto, r.Riga)));
            scarto += pagina.Height;
        }
        var it = Paragrafi(italiano);
        if (it is null) return null;
        return new AipText
        {
            Section = section,
            Text = it,
            English = Paragrafi(inglese),
            Page = prima?.Reference is { } rif ? new AipPage(rif, prima.Effective) : null,
        };
    }

    /// <summary>La x dove comincia la colonna inglese: la più frequente fra le parole che stanno a metà pagina.</summary>
    private static double ColonnaInglese(PdfPageText pagina, List<PdfWord> parole)
    {
        var meta = parole.Where(w => w.X0 > pagina.Width * 0.42 && w.X0 < pagina.Width * 0.62)
            .GroupBy(w => Math.Round(w.X0)).OrderByDescending(g => g.Count()).FirstOrDefault();
        return meta?.Key ?? pagina.Width / 2;
    }

    private static List<(double Y, string Riga)> Righe(IEnumerable<PdfWord> parole)
    {
        var righe = new List<(double Y, string Riga)>();
        var riga = new List<PdfWord>();
        foreach (var w in parole.OrderBy(w => w.Baseline).ThenBy(w => w.X0))
        {
            if (riga.Count > 0 && w.Baseline - riga[0].Baseline > 3.0)
            {
                righe.Add((riga[0].Baseline, string.Join(' ', riga.OrderBy(x => x.X0).Select(x => x.Text))));
                riga = [];
            }
            riga.Add(w);
        }
        if (riga.Count > 0) righe.Add((riga[0].Baseline, string.Join(' ', riga.OrderBy(x => x.X0).Select(x => x.Text))));
        return righe;
    }

    /// <summary>Le righe in paragrafi, uno per riga del risultato. Null se non c'è testo.</summary>
    private static string? Paragrafi(List<(double Y, string Riga)> righe)
    {
        if (righe.Count == 0) return null;
        var passi = righe.Zip(righe.Skip(1), (a, b) => b.Y - a.Y).Where(d => d > 0 && d < 30).Order().ToList();
        var interlinea = passi.Count == 0 ? 12 : passi[passi.Count / 2];
        var paragrafi = new List<string>();
        double? prima = null;
        var titolo = false;
        foreach (var (y, riga) in righe)
        {
            // Dopo un titolo («1 Zona vietata», «3 Procedure per gli aeromobili non utilizzati per il lancio», «NOTA»: corto o
            // numerato, senza punto in fondo) il testo è un paragrafo suo — salvo il seguito del titolo, tutto maiuscolo
            // anche lui («8 SERVIZIO DI COORDINAMENTO E CONTROLLO» / «DELL'AERONAUTICA MILITARE (SCCAM)»).
            var seguitoDelTitolo = titolo && Maiuscolo(riga) && paragrafi.Count > 0 && Maiuscolo(paragrafi[^1]);
            var nuovo = paragrafi.Count == 0 || prima is not { } p || y - p > interlinea * 1.5 || (titolo && !seguitoDelTitolo)
                        || RxInizioDiParagrafo().IsMatch(riga);
            var fine = riga.TrimEnd();
            var senzaPunto = !fine.EndsWith('.') && !fine.EndsWith(';') && !fine.EndsWith(',') && !fine.EndsWith(':');
            titolo = (nuovo || seguitoDelTitolo) && senzaPunto && (riga.Trim().Length < 50 || RxTitoloNumerato().IsMatch(riga) || seguitoDelTitolo);
            if (nuovo) paragrafi.Add(riga.Trim());
            else paragrafi[^1] = paragrafi[^1].EndsWith('-') && char.IsLower(riga.FirstOrDefault())
                ? paragrafi[^1][..^1] + riga.Trim()
                : paragrafi[^1] + " " + riga.Trim();
            prima = y;
        }
        return string.Join("\n", paragrafi);
    }
}
