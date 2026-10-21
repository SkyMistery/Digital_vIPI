using System.Globalization;
using System.Text.RegularExpressions;

namespace Aip.Pdf.Italia;

/// <summary>
/// ENR 5.4 dell'AIP italiano (carta «L'AIP intero», passo 4): gli ostacoli — puntuali (5.4.1, un PDF per regione),
/// piattaforme (5.4.2), lineari (5.4.3, per regione, con una tabella di note in fondo).
/// <para>Le tabelle sono stampate di traverso: si leggono sulla pagina girata (<see cref="PdfPageText.Turned"/>). Le
/// colonne sono quelle dei bordi verticali, ognuna col suo numero nella riga «1 2 3 …» della testata; un ostacolo comincia
/// alla riga con la sua sigla (<c>T004043</c>) e va fino alla sigla dopo: sotto ci sono il tipo in inglese e i nomi che
/// vanno a capo.</para>
/// </summary>
public static partial class ItalianEnr54
{
    private const RegexOptions Opzioni = RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

    [GeneratedRegex(@"^[TPL]\d{6}$", Opzioni)]
    private static partial Regex RxSigla();

    [GeneratedRegex(@"^REGIONE\s*/\s*REGION\s*:\s*(?<r>.+)$", Opzioni)]
    private static partial Regex RxRegione();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RxSpazi();

    [GeneratedRegex(@"^(?<s>ENR 5\.4(?:\.\d+)+)-\d+$", Opzioni)]
    private static partial Regex RxSottosezione();

    /// <summary>Che colonna è che cosa, per genere: il numero stampato nella testata (da 1).</summary>
    private sealed record Colonne(int Quante, int Lat, int Lon, int? LatFine, int? LonFine, int? Lunghezza, int? AltezzaM, int? AltezzaFt,
        int? ElevazioneM, int? ElevazioneFt, int? BassoM, int? BassoFt, int? AltoM, int? AltoFt, int Giorno, int Notte, int? Nome);

    private static readonly Colonne Puntuali = new(13, 6, 7, null, null, null, 8, 9, 10, 11, null, null, null, null, 12, 13, null);
    private static readonly Colonne Piattaforme = new(12, 6, 7, null, null, null, null, null, 8, 9, null, null, null, null, 10, 11, 12);
    private static readonly Colonne Lineari = new(18, 6, 7, 8, 9, 10, 15, 16, null, null, 11, 12, 13, 14, 17, 18, null);

    /// <summary>Un PDF di ENR 5.4, nel modello.</summary>
    public static ImportResult Read(IReadOnlyList<PdfPageText> pages, ObstacleKind kind)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var (sezione, colonne) = kind switch
        {
            ObstacleKind.Point => ("ENR 5.4.1", Puntuali),
            ObstacleKind.Platform => ("ENR 5.4.2", Piattaforme),
            _ => ("ENR 5.4.3", Lineari),
        };
        var pagine = new List<ImportPage>();
        var problemi = new List<ImportIssue>();
        var ostacoli = new List<Obstacle>();
        var note = new List<(string Sigla, string Testo, int Pagina)>();
        string? regione = null;

        foreach (var page in pages)
        {
            var piane = Table.PlainLines(page);
            var data = PageInfo.Date(piane);
            var pagina = new ImportPage(page.Number, PageInfo.Reference(piane), data, page.Turned is not null);
            pagine.Add(pagina);
            if (page.Turned is not { } girata) continue;
            if (data is null) problemi.Add(new ImportIssue(ImportIssueKind.PageWithoutDate, null, page.Number));

            var righe = Table.PlainLinesWithY(girata);
            foreach (var (_, testo) in righe)
                if (RxRegione().Match(testo.Trim()) is { Success: true } r) regione = r.Groups["r"].Value.Trim();

            // La tabella degli ostacoli comincia sotto la riga dei numeri delle colonne; quella delle note («ID
            // NOTE/REMARKS») sotto la sua testata, anche a metà pagina, dopo l'ultimo ostacolo.
            var numeri = Numeri(girata, colonne.Quante);
            var testataDelleNote = righe.Where(r => r.Text.StartsWith("ID ", StringComparison.Ordinal) && r.Text.Contains("NOTE/REMARKS", StringComparison.Ordinal))
                .Select(r => (double?)r.Y).FirstOrDefault();
            if (numeri is null && testataDelleNote is null)
            {
                if (righe.Count > 0)
                    problemi.Add(new ImportIssue(ImportIssueKind.PageWithoutTable, null, page.Number, "senza la riga dei numeri delle colonne"));
                continue;
            }
            if (numeri is { } n)
            {
                foreach (var (sigla, celle) in Righe(girata, n, testataDelleNote ?? double.MaxValue, colonne.Quante, page.Number, problemi))
                {
                    if (Ostacolo(sigla, celle, colonne, sezione, kind, regione, Pagina(pagina), page.Number, problemi) is { } o)
                        ostacoli.Add(o);
                }
            }
            if (testataDelleNote is { } t)
                note.AddRange(Note(girata, t, page.Number, problemi));
        }

        // Le note per sigla: ognuna sul suo ostacolo; una sigla che non c'è si dice.
        foreach (var gruppo in note.GroupBy(n => n.Sigla, StringComparer.Ordinal))
        {
            var i = ostacoli.FindIndex(o => o.Designator == gruppo.Key);
            var testo = string.Join("\n", gruppo.Select(n => n.Testo));
            if (i < 0) problemi.Add(new ImportIssue(ImportIssueKind.Obstacle, gruppo.Key, gruppo.First().Pagina, $"nota senza ostacolo: {testo}"));
            else ostacoli[i] = ostacoli[i] with { Remarks = testo };
        }
        return new ImportResult([], pagine, problemi) { Obstacles = ostacoli };
    }

    /// <summary>La riga «1 2 3 … n» della testata: la y e la x di ogni numero. Null se la pagina non l'ha.</summary>
    private static (double Y, double[] X)? Numeri(PdfPageText girata, int quante)
    {
        foreach (var riga in girata.Words.GroupBy(w => Math.Round(w.Baseline)).OrderBy(g => g.Key))
        {
            var parole = riga.OrderBy(w => w.X0).ToList();
            if (parole.Count == quante && parole.Select((w, i) => w.Text == (i + 1).ToString(CultureInfo.InvariantCulture)).All(b => b))
                return (parole[0].Baseline, parole.Select(w => w.XCenter).ToArray());
        }
        return null;
    }

    /// <summary>
    /// Le righe della tabella: per ogni sigla le celle (una lista di righe per colonna, da 0). Le colonne vengono dai bordi
    /// verticali sotto la testata: fra due bordi, la colonna del numero che ci sta in mezzo. Il testo fuori dalle colonne, o
    /// prima della prima sigla, si dice.
    /// </summary>
    private static List<(string Sigla, List<string>[] Celle)> Righe(PdfPageText girata, (double Y, double[] X) numeri, double fine, int quante,
        int pagina, List<ImportIssue> problemi)
    {
        var bordi = new List<double>();
        foreach (var x in girata.VerticalRules.Where(r => r.Bottom - r.Top >= 8 && r.Bottom > numeri.Y + 5 && r.Top < fine).Select(r => r.X).Order())
        {
            if (bordi.Count > 0 && x - bordi[^1] <= 2.5) continue;
            bordi.Add(x);
        }
        int? Colonna(double x)
        {
            var i = bordi.FindLastIndex(b => b <= x);
            if (i < 0 || i + 1 >= bordi.Count) return null;
            var (a, b) = (bordi[i], bordi[i + 1]);
            var n = Array.FindIndex(numeri.X, c => c >= a && c <= b);
            return n < 0 ? null : n;
        }

        var dati = girata.Words.Where(w => w.Baseline > numeri.Y + 3 && w.Baseline < fine - 3).Select(w => (Parola: w, Colonna: Colonna(w.XCenter))).ToList();
        var fuori = dati.Where(d => d.Colonna is null).ToList();
        if (fuori.Count > 0)
            problemi.Add(new ImportIssue(ImportIssueKind.Obstacle, null, pagina, $"fuori dalle colonne: {string.Join(" ", fuori.Select(f => f.Parola.Text))}"));

        var sigle = dati.Where(d => d.Colonna == 4 && RxSigla().IsMatch(d.Parola.Text)).Select(d => d.Parola).OrderBy(w => w.Baseline).ToList();
        if (sigle.Count == 0) return [];
        var prima = dati.Where(d => d.Colonna is not null && d.Parola.Baseline < sigle[0].Baseline - 4).ToList();
        if (prima.Count > 0)
            problemi.Add(new ImportIssue(ImportIssueKind.Obstacle, null, pagina, $"prima della prima sigla: {string.Join(" ", prima.Select(p => p.Parola.Text))}"));

        var righe = new List<(string, List<string>[])>();
        for (var i = 0; i < sigle.Count; i++)
        {
            var cima = sigle[i].Baseline - 4;
            var fondo = i + 1 < sigle.Count ? sigle[i + 1].Baseline - 4 : double.MaxValue;
            var celle = Enumerable.Range(0, quante).Select(_ => new List<string>()).ToArray();
            foreach (var colonna in dati.Where(d => d.Colonna is not null && d.Parola.Baseline >= cima && d.Parola.Baseline < fondo).GroupBy(d => d.Colonna!.Value))
            {
                var riga = new List<PdfWord>();
                foreach (var w in colonna.Select(c => c.Parola).OrderBy(w => w.Baseline).ThenBy(w => w.X0))
                {
                    if (riga.Count > 0 && w.Baseline - riga[0].Baseline > 3)
                    {
                        celle[colonna.Key].Add(string.Join(' ', riga.OrderBy(x => x.X0).Select(x => x.Text)));
                        riga = [];
                    }
                    riga.Add(w);
                }
                if (riga.Count > 0) celle[colonna.Key].Add(string.Join(' ', riga.OrderBy(x => x.X0).Select(x => x.Text)));
            }
            righe.Add((sigle[i].Text, celle));
        }
        return righe;
    }

    private static Obstacle? Ostacolo(string sigla, List<string>[] celle, Colonne c, string sezione, ObstacleKind kind, string? regione,
        AipPage? pagina, int numero, List<ImportIssue> problemi)
    {
        // «NIL» in una cella (la località, la segnalazione, un numero) vuol dire che il dato non c'è.
        string? Cella(int? n) => n is { } i && Unisci(celle[i - 1]) is { } t && t != "NIL" ? t : null;
        double? Numero(int? n)
        {
            var t = Cella(n);
            if (t is null or "-") return null;
            if (double.TryParse(t.Replace(" ", "", StringComparison.Ordinal), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v)) return v;
            problemi.Add(new ImportIssue(ImportIssueKind.Obstacle, sigla, numero, $"colonna {n}: «{t}»"));
            return null;
        }

        var posizione = GeometryText.ReadPoint($"{Cella(c.Lat)} {Cella(c.Lon)}");
        if (posizione is not { } dove)
        {
            problemi.Add(new ImportIssue(ImportIssueKind.Obstacle, sigla, numero, $"coordinate: {Cella(c.Lat)} {Cella(c.Lon)}"));
            return null;
        }
        Position? fine = null;
        if (c.LatFine is not null)
        {
            fine = GeometryText.ReadPoint($"{Cella(c.LatFine)} {Cella(c.LonFine)}");
            if (fine is null) problemi.Add(new ImportIssue(ImportIssueKind.Obstacle, sigla, numero, $"coordinate della fine: {Cella(c.LatFine)} {Cella(c.LonFine)}"));
        }
        // Il tipo: la prima riga in italiano, il resto in inglese.
        var tipo = celle[3];
        return new Obstacle
        {
            // La sottosezione della regione dalla testata della pagina («ENR 5.4.1.13-10»: la Puglia), dove c'è.
            Section = pagina?.Reference is { } rif && RxSottosezione().Match(rif) is { Success: true } m ? m.Groups["s"].Value : sezione,
            Kind = kind,
            Designator = sigla,
            Region = kind == ObstacleKind.Platform ? null : regione,
            Province = Cella(1),
            Town = Cella(2),
            Site = Cella(3),
            Type = tipo.Count > 0 ? tipo[0].Trim() : null,
            TypeEnglish = Unisci([.. tipo.Skip(1)]),
            Position = dove,
            End = fine,
            LengthM = Numero(c.Lunghezza),
            HeightM = Numero(c.AltezzaM),
            HeightFt = Numero(c.AltezzaFt),
            ElevationM = Numero(c.ElevazioneM),
            ElevationFt = Numero(c.ElevazioneFt),
            LowestM = Numero(c.BassoM),
            LowestFt = Numero(c.BassoFt),
            HighestM = Numero(c.AltoM),
            HighestFt = Numero(c.AltoFt),
            DayMarking = Cella(c.Giorno),
            NightMarking = Cella(c.Notte),
            Name = Cella(c.Nome),
            Page = pagina,
        };
    }

    /// <summary>
    /// La tabella delle note di una regione: una riga per sigla, «L003543 Ostacolo lineare sul fiume Alento/Linear obstacle
    /// on the Alento river»; una riga senza sigla continua la nota di prima.
    /// </summary>
    private static List<(string Sigla, string Testo, int Pagina)> Note(PdfPageText girata, double testata, int pagina, List<ImportIssue> problemi)
    {
        var note = new List<(string Sigla, string Testo, int Pagina)>();
        foreach (var (_, riga) in Table.PlainLinesWithY(girata).Where(r => r.Y > testata + 3))
        {
            var parti = riga.Trim().Split(' ', 2);
            // La tabella vuota: «NIL».
            if (note.Count == 0 && riga.Trim() is "NIL" or "NIL NIL") continue;
            if (RxSigla().IsMatch(parti[0]))
                note.Add((parti[0], parti.Length > 1 ? parti[1].Trim() : "", pagina));
            else if (note.Count > 0)
                note[^1] = note[^1] with { Testo = note[^1].Testo + " " + riga.Trim() };
            else
                problemi.Add(new ImportIssue(ImportIssueKind.Obstacle, null, pagina, $"nota senza sigla: {riga.Trim()}"));
        }
        return note;
    }

    private static AipPage? Pagina(ImportPage p) => p.Reference is null ? null : new AipPage(p.Reference, p.Effective);

    /// <summary>Le righe di una cella in una: una riga che finisce col trattino continua senza spazio («FARA FILIORUM-» «CASACANDITELLA»).</summary>
    private static string? Unisci(List<string> righe)
    {
        var t = "";
        foreach (var r in righe.Select(r => r.Trim()).Where(r => r.Length > 0))
            t = t.Length == 0 ? r : t.EndsWith('-') ? t + r : t + " " + r;
        t = RxSpazi().Replace(t, " ").Trim();
        return t.Length == 0 ? null : t;
    }
}
