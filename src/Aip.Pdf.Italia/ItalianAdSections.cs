using System.Globalization;
using System.Text.RegularExpressions;

namespace Aip.Pdf.Italia;

/// <summary>
/// Tutte le sottosezioni del testo di uno scalo (carta «L'AIP intero», passo 5): AD 2.1-2.25 degli aeroporti, AD 3.1-3.23
/// degli eliporti, com'è stampate.
/// <para>Ogni parola del corpo della pagina va alla sottosezione dell'ultimo titolo sopra di lei («LICA AD 2.3 …», due righe:
/// l'italiano e l'inglese), anche nella pagina prima. Dentro una tabella: se la tabella ha tre colonne e la prima, stretta,
/// è numerata («1», «2» …), ogni numero apre una voce con le sue etichette e il suo valore (AD 2.2-2.11; una sottotabella
/// dentro il valore resta testo a righe); le altre tabelle si danno riga per riga, una cella per colonna, la testata
/// compresa. Fuori dalle tabelle è prosa, nelle due colonne dell'AIP (<see cref="ItalianProse"/>).</para>
/// </summary>
public static partial class ItalianAdSections
{
    private const RegexOptions Opzioni = RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

    [GeneratedRegex(@"^(?<icao>[A-Z]{4})\s+AD\s+(?<p>[23])\.(?<n>\d+)\s*(?<t>.*)$", Opzioni)]
    private static partial Regex RxTitolo();

    [GeneratedRegex(@"^\d{1,2}$", Opzioni)]
    private static partial Regex RxNumeroDellaVoce();

    private sealed class SezioneLetta
    {
        public required int Numero { get; init; }
        public required string Prefisso { get; init; }
        public string? Titolo { get; set; }
        public string? TitoloInglese { get; set; }
        public ImportPage? Pagina { get; init; }
        public List<(string Numero, List<string> Etichette, List<string> Valore)> Voci { get; } = [];
        public List<IReadOnlyList<string>> Righe { get; } = [];
        public List<(PdfPageText Pagina, List<PdfWord> Parole)> Prosa { get; } = [];
    }

    /// <summary>Le sottosezioni del testo di uno scalo, nell'ordine dell'AIP.</summary>
    public static IReadOnlyList<AerodromeSection> Read(IReadOnlyList<PdfPageText> pages, string icao, IReadOnlyList<ImportPage> pagine)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(pagine);
        var sezioni = new List<SezioneLetta>();
        SezioneLetta? aperta = null;
        foreach (var page in pages)
        {
            // Il testo in cima alla pagina, prima del primo titolo, è della sottosezione aperta alla fine della pagina prima.
            var apertaAllInizio = aperta;
            // I titoli della pagina: la prima riga di un numero apre la sottosezione, la seconda è il titolo inglese.
            var titoli = new List<(double Y, SezioneLetta Sezione)>();
            var linee = Table.PlainLinesWithY(page).ToList();
            int? NumeroDelTitolo(int i) =>
                i >= 0 && i < linee.Count && RxTitolo().Match(linee[i].Text.Trim()) is { Success: true } t && t.Groups["icao"].Value == icao
                    ? int.Parse(t.Groups["n"].Value, CultureInfo.InvariantCulture) : null;
            for (var i = 0; i < linee.Count; i++)
            {
                var (y, testo) = linee[i];
                // Il titolo italiano che va a capo (AD 3.8 degli eliporti, «… DEI PUNTI DI» / «CONTROLLO»): la riga in
                // maiuscolo fra il titolo italiano e quello inglese dello stesso numero.
                if (NumeroDelTitolo(i) is null)
                {
                    if (aperta is { TitoloInglese: null } a && NumeroDelTitolo(i - 1) == a.Numero && NumeroDelTitolo(i + 1) == a.Numero
                        && testo.Any(char.IsLetter) && !testo.Any(char.IsLower))
                    {
                        a.Titolo = $"{a.Titolo} {testo.Trim()}".Trim();
                        titoli.Add((y, a));
                    }
                    continue;
                }
                var m = RxTitolo().Match(testo.Trim());
                var numero = int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture);
                var titolo = m.Groups["t"].Value.Trim();
                if (aperta is { } s && s.Numero == numero)
                    s.TitoloInglese ??= titolo.Length == 0 ? null : titolo;
                else
                {
                    aperta = new SezioneLetta
                    {
                        Numero = numero, Prefisso = $"AD {m.Groups["p"].Value}", Titolo = titolo.Length == 0 ? null : titolo,
                        Pagina = pagine.FirstOrDefault(p => p.Number == page.Number),
                    };
                    sezioni.Add(aperta);
                }
                titoli.Add((y, aperta));
            }
            SezioneLetta? Di(double y) => titoli.Where(t => t.Y < y - 1).Select(t => t.Sezione).LastOrDefault() ?? apertaAllInizio;

            var corpo = page.Words.Where(w => w.Baseline > 40 && w.Baseline < page.Height - 20
                                              && !titoli.Any(t => Math.Abs(t.Y - w.Baseline) <= 1.5)).ToList();
            // Le tabelle, dalla più grande: una sottotabella dentro un valore è del valore.
            var griglie = Table.FindAll(page, 300).OrderByDescending(g => (g.Columns[^1] - g.Columns[0]) * (g.Bottom - g.Top)).ToList();
            var prese = new HashSet<PdfWord>();
            foreach (var g in griglie)
            {
                var dentro = corpo.Where(w => !prese.Contains(w) && w.XCenter >= g.Columns[0] - 2 && w.XCenter <= g.Columns[^1] + 2
                                              && w.Baseline >= g.Top - 1 && w.Baseline <= g.Bottom + 1).ToList();
                if (dentro.Count == 0) continue;
                prese.UnionWith(dentro);
                if (Di(g.Top + 2) is not { } sezione) continue;
                if (Numerata(g, dentro)) Voci(sezione, page, g, dentro);
                else sezione.Righe.AddRange(Righe(page, g, dentro));
            }
            foreach (var gruppo in corpo.Where(w => !prese.Contains(w)).GroupBy(w => Di(w.Baseline)))
                gruppo.Key?.Prosa.Add((page, gruppo.ToList()));
        }

        return sezioni.Select(s =>
        {
            // Una riga sola (il nome sotto AD 2.1, «Vedi AOC in vigore - See AOC in force» in AD 2.10) non si divide.
            var (it, en) = s.Prosa.Sum(p => A_Righe(p.Parole).Count) == 1
                ? (A_Righe(s.Prosa.SelectMany(p => p.Parole))[0].Testo, (string?)null)
                : ItalianProse.DueColonne(s.Prosa);
            return new AerodromeSection
            {
                Number = $"{s.Prefisso}.{s.Numero}",
                Title = s.Titolo,
                TitleEnglish = s.TitoloInglese,
                Items = s.Voci.Count == 0 ? null : s.Voci.Select(v => new AerodromeItem(v.Numero, string.Join("\n", v.Etichette),
                    v.Valore.Count == 0 ? null : string.Join("\n", v.Valore))).ToList(),
                Rows = s.Righe.Count == 0 ? null : s.Righe,
                Text = it,
                English = en,
                Page = s.Pagina?.Reference is { } rif ? new AipPage(rif, s.Pagina.Effective) : null,
            };
        }).ToList();
    }

    /// <summary>Tre colonne, la prima stretta, e dentro la prima almeno un numero: le voci di AD 2.2-2.11.</summary>
    private static bool Numerata(TableGrid g, List<PdfWord> parole) =>
        g.ColumnCount == 3 && g.Columns[1] - g.Columns[0] < 45
        && parole.Where(w => w.XCenter < g.Columns[1]).All(w => RxNumeroDellaVoce().IsMatch(w.Text));

    /// <summary>
    /// Le voci di una tabella numerata: un numero nella prima colonna apre una voce, che va fino al numero dopo; le righe
    /// sopra il primo numero (la voce che continua dalla pagina prima) vanno all'ultima voce della sottosezione.
    /// </summary>
    private static void Voci(SezioneLetta sezione, PdfPageText page, TableGrid g, List<PdfWord> parole)
    {
        var numeri = parole.Where(w => w.XCenter < g.Columns[1] && RxNumeroDellaVoce().IsMatch(w.Text)).OrderBy(w => w.Baseline).ToList();
        var etichette = A_Righe(parole.Where(w => w.XCenter >= g.Columns[1] && w.XCenter < g.Columns[2]));
        var valori = A_Righe(parole.Where(w => w.XCenter >= g.Columns[2]));
        // Una voce sta fra due bordi orizzontali: il numero non è sempre sulla sua prima riga (LICA AD 2.2, «8 Note»).
        // Senza bordi, da un numero al numero dopo.
        var bande = Table.RowBands(page, g).ToList();
        int? DiChi(double y)
        {
            if (bande.Count > 1 && bande.FindIndex(b => y >= b.Top - 1 && y < b.Bottom) is var k and >= 0)
            {
                var i = numeri.FindIndex(n => n.Baseline >= bande[k].Top - 1 && n.Baseline < bande[k].Bottom);
                return i < 0 ? null : i;
            }
            var j = numeri.FindLastIndex(n => n.Baseline <= y + 2);
            return j < 0 ? null : j;
        }
        var nuove = numeri.Select(n => (n.Text, new List<string>(), new List<string>())).ToList();
        foreach (var (y, t) in etichette)
        {
            if (DiChi(y) is { } i) nuove[i].Item2.Add(t);
            else if (sezione.Voci.Count > 0) sezione.Voci[^1].Etichette.Add(t);
        }
        foreach (var (y, t) in valori)
        {
            if (DiChi(y) is { } i) nuove[i].Item3.Add(t);
            else if (sezione.Voci.Count > 0) sezione.Voci[^1].Valore.Add(t);
        }
        sezione.Voci.AddRange(nuove);
    }

    /// <summary>Le righe di una tabella non numerata: una per banda fra i bordi orizzontali (o per riga di testo, se non ci sono), una cella per colonna.</summary>
    private static List<IReadOnlyList<string>> Righe(PdfPageText page, TableGrid g, List<PdfWord> parole)
    {
        int Colonna(PdfWord w)
        {
            for (var i = 0; i < g.ColumnCount; i++)
                if (w.XCenter <= g.Columns[i + 1] + 2) return i;
            return g.ColumnCount - 1;
        }
        var bande = Table.RowBands(page, g).ToList();
        if (bande.Count == 0)
            bande = [.. A_Righe(parole).Select(r => (r.Y - 3, r.Y + 3))];
        var righe = new List<IReadOnlyList<string>>();
        foreach (var (cima, fondo) in bande)
        {
            var nella = parole.Where(w => w.Baseline >= cima && w.Baseline < fondo).ToList();
            if (nella.Count == 0) continue;
            righe.Add([.. Enumerable.Range(0, g.ColumnCount).Select(c => string.Join(" ", A_Righe(nella.Where(w => Colonna(w) == c)).Select(r => r.Testo)))]);
        }
        return righe;
    }

    /// <summary>Le parole a righe di testo, dall'alto.</summary>
    private static List<(double Y, string Testo)> A_Righe(IEnumerable<PdfWord> parole)
    {
        var righe = new List<(double Y, string Testo)>();
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
}
