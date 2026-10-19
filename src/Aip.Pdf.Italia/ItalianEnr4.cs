using System.Text.RegularExpressions;

namespace Aip.Pdf.Italia;

/// <summary>
/// ENR 4 dell'AIP italiano (carta F6, slice 3): le radioassistenze in rotta (<c>LI-ENR 4.1.1</c>) e quelle militari fuori
/// dalla parte AD (<c>LI-ENR 4.1.2</c>), i punti significativi (<c>LI-ENR 4.4</c>), le luci aeronautiche al suolo
/// (<c>LI-ENR 4.5</c>).
/// <para>Tre tabelle con la stessa forma: <b>una riga per oggetto</b>, chiusa da bordi orizzontali, e il testo delle celle
/// a più righe. In ENR 4.1 il testo sta in mezzo alla cella (il nome della radioassistenza più in basso della copertura,
/// che è più lunga): di chi è una riga di testo lo dicono i bordi (<see cref="Table.RowBands"/>), non la sua y.</para>
/// <para><b>ENR 4.1</b>, 9 colonne: nome, tipo e declinazione · sigla · frequenza e canale · orario per apparato ·
/// coordinate per apparato · elevazione del DME · copertura · interesse FRA · note. <b>ENR 4.4</b>, 6: designatore ·
/// latitudine · longitudine · rotte · procedure · interesse FRA. <b>ENR 4.5</b>, 6: nome e coordinate · tipo ·
/// caratteristiche · orario · intensità · note.</para>
/// </summary>
public static partial class ItalianEnr4
{
    private const RegexOptions Opzioni = RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

    // Il tipo stampato sotto il nome della stazione; anche il nome di un apparato nelle colonne dell'orario e delle coordinate.
    [GeneratedRegex(@"^(?:T?D?VOR(?:/DME|TAC)?|DVORTAC|TACAN|DME|NDB|L)$", Opzioni)]
    private static partial Regex RxTipo();

    [GeneratedRegex(@"^\((?<v>.+)\)$", Opzioni)]
    private static partial Regex RxDeclinazione();

    [GeneratedRegex(@"(?<f>\d+(?:[.,]\d+)?)\s*(?<u>MHZ|KHZ)", Opzioni | RegexOptions.IgnoreCase)]
    private static partial Regex RxFrequenza();

    [GeneratedRegex(@"\bCH\s*(?<c>\d{1,3}\s*[XY])\b", Opzioni)]
    private static partial Regex RxCanale();

    [GeneratedRegex(@"^[A-Z]{2,3}$", Opzioni)]
    private static partial Regex RxSigla();

    [GeneratedRegex(@"^[A-Z]{5}$", Opzioni)]
    private static partial Regex RxDesignatore();

    [GeneratedRegex(@"\s(?=\d{1,2}\)\s)", Opzioni)]
    private static partial Regex RxNumeroDellaNota();

    [GeneratedRegex(@"[\s,;]+", Opzioni)]
    private static partial Regex RxSeparatoriDelleRotte();

    // La legenda sotto le luci di ENR 4.5: «OR (AM) - ottica rotante con pannelli aeromarittimi».
    [GeneratedRegex(@"^[A-Z]{1,3}\s*\([A-Z]{1,3}\)\s*-", Opzioni)]
    private static partial Regex RxLegenda();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RxSpazi();

    /// <summary>Una riga della tabella: il testo di ogni colonna, una voce per riga di testo, e la pagina.</summary>
    internal sealed record Riga(List<string>[] Celle, ImportPage Pagina);

    /// <summary>Le radioassistenze di un PDF di ENR 4.1 (<paramref name="military"/>: ENR 4.1.2).</summary>
    public static ImportResult ReadNavaids(IReadOnlyList<PdfPageText> pages, bool military)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var (righe, pagine, problemi) = Righe(pages, 9);
        var radioassistenze = new List<Navaid>();
        foreach (var r in righe)
            if (Radioassistenza(r, military, problemi) is { } n) radioassistenze.Add(n);
        return new ImportResult([], pagine, problemi) { Navaids = radioassistenze };
    }

    /// <summary>I punti significativi di ENR 4.4.</summary>
    public static ImportResult ReadPoints(IReadOnlyList<PdfPageText> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var (righe, pagine, problemi) = Righe(pages, 6);
        var punti = new List<SignificantPoint>();
        foreach (var r in righe)
        {
            var c = r.Celle;
            // La riga sotto i numeri delle colonne dice solo «LAT» e «LONG».
            if (c[0].Count == 0 && Unisci(c[1]) == "LAT") continue;
            var designatore = Unisci(c[0]) ?? "";
            if (!RxDesignatore().IsMatch(designatore))
            {
                problemi.Add(new ImportIssue(ImportIssueKind.SignificantPoint, designatore, r.Pagina.Number, $"designatore: {Tutto(c)}"));
                continue;
            }
            if (GeometryText.ReadPoint($"{Unisci(c[1])} {Unisci(c[2])}") is not { } posizione)
            {
                problemi.Add(new ImportIssue(ImportIssueKind.SignificantPoint, designatore, r.Pagina.Number, $"senza coordinate: {Unisci(c[1])} {Unisci(c[2])}"));
                continue;
            }
            punti.Add(new SignificantPoint
            {
                Section = "ENR 4.4",
                Designator = designatore,
                Position = posizione,
                Routes = SenzaNil(Unisci(c[3])) is { } rotte ? RxSeparatoriDelleRotte().Split(rotte).Where(x => x.Length > 0).ToList() : [],
                Procedures = SenzaNil(Unisci(c[4])),
                Fra = SenzaNil(Unisci(c[5])),
                Page = Pagina(r.Pagina),
            });
        }
        return new ImportResult([], pagine, problemi) { Points = punti };
    }

    /// <summary>Le luci aeronautiche al suolo di ENR 4.5.</summary>
    public static ImportResult ReadLights(IReadOnlyList<PdfPageText> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var (righe, pagine, problemi) = Righe(pages, 6);
        var luci = new List<GroundLight>();
        foreach (var r in righe)
        {
            var c = r.Celle;
            if (c[0].Count > 0 && RxLegenda().IsMatch(c[0][0].Trim())) continue;
            // Il nome, poi le coordinate (latitudine e longitudine su due righe), quando ci sono.
            var nome = c[0].TakeWhile(t => !GeometryText.HasCoordinate(t) && !Latitudine(t)).ToList();
            var resto = string.Join(' ', c[0].Skip(nome.Count));
            var posizione = GeometryText.ReadPoint(resto);
            if (nome.Count == 0)
            {
                problemi.Add(new ImportIssue(ImportIssueKind.Light, null, r.Pagina.Number, $"senza nome: {Tutto(c)}"));
                continue;
            }
            var chiave = Unisci(nome)!;
            if (resto.Length > 0 && posizione is null)
                problemi.Add(new ImportIssue(ImportIssueKind.Light, chiave, r.Pagina.Number, $"coordinate: {resto}"));
            luci.Add(new GroundLight
            {
                Section = "ENR 4.5",
                Name = chiave,
                Position = posizione,
                Type = c[1].Count == 0 ? null : string.Join(" / ", c[1].Select(t => t.Trim())),
                Characteristics = Unisci(c[2]),
                Hours = Unisci(c[3]),
                Intensity = Unisci(c[4]),
                Remarks = SenzaNil(Unisci(c[5])),
                Page = Pagina(r.Pagina),
            });
        }
        return new ImportResult([], pagine, problemi) { Lights = luci };
    }

    /// <summary>Una latitudine da sola sulla sua riga (la longitudine sta sotto): <c>43°57’17’’N</c>.</summary>
    private static bool Latitudine(string t) => t.IndexOfAny(['°', 'º', '˚']) >= 0 && t.TrimEnd().EndsWith('N');

    private static Navaid? Radioassistenza(Riga r, bool militare, List<ImportIssue> problemi)
    {
        var c = r.Celle;
        var sigla = Unisci(c[1]) ?? "";
        if (!RxSigla().IsMatch(sigla))
        {
            problemi.Add(new ImportIssue(ImportIssueKind.Navaid, sigla, r.Pagina.Number, $"sigla: {Tutto(c)}"));
            return null;
        }

        // Colonna 1: il nome (anche su due righe), il tipo, la declinazione fra parentesi.
        var iTipo = c[0].FindIndex(t => RxTipo().IsMatch(t.Trim()));
        if (iTipo <= 0)
        {
            problemi.Add(new ImportIssue(ImportIssueKind.Navaid, sigla, r.Pagina.Number, $"nome o tipo: {Unisci(c[0])}"));
            return null;
        }
        var tipo = c[0][iTipo].Trim();
        var nome = Unisci(c[0].Take(iTipo).ToList())!;
        var dopo = c[0].Skip(iTipo + 1).Select(t => t.Trim()).ToList();
        string? declinazione = null;
        if (dopo.Count == 1 && RxDeclinazione().Match(dopo[0]) is { Success: true } d) declinazione = d.Groups["v"].Value.Trim();
        else if (dopo.Count > 0) problemi.Add(new ImportIssue(ImportIssueKind.Navaid, $"{sigla} {tipo}", r.Pagina.Number, $"dopo il tipo: {string.Join(" | ", dopo)}"));

        var chiave = $"{sigla} {tipo}";
        var frequenza = Unisci(c[2]) ?? "";
        var f = RxFrequenza().Match(frequenza);
        var canale = RxCanale().Match(frequenza);

        var orari = PerApparato(c[3]);
        var apparati = new List<NavaidComponent>();
        foreach (var (apparato, testo) in PerApparato(c[4]))
        {
            if (GeometryText.ReadPoint(testo) is not { } p)
            {
                problemi.Add(new ImportIssue(ImportIssueKind.Navaid, chiave, r.Pagina.Number, $"coordinate {apparato}: {testo}"));
                continue;
            }
            // L'orario dello stesso apparato; con un apparato solo, l'unico che c'è.
            var orario = orari.FirstOrDefault(o => o.Apparato == apparato).Testo
                ?? (orari.Count == 1 ? orari[0].Testo : null);
            apparati.Add(new NavaidComponent(apparato, p, orario));
        }
        if (apparati.Count == 0)
        {
            problemi.Add(new ImportIssue(ImportIssueKind.Navaid, chiave, r.Pagina.Number, "senza coordinate"));
            return null;
        }

        return new Navaid
        {
            Section = "ENR 4.1",
            Military = militare,
            Designator = sigla,
            Name = nome,
            Type = tipo,
            Kind = Tipo(tipo),
            MagneticVariation = declinazione,
            Frequency = f.Success ? $"{f.Groups["f"].Value} {f.Groups["u"].Value.ToUpperInvariant()}" : null,
            Channel = canale.Success ? RxSpazi().Replace(canale.Groups["c"].Value, "") : null,
            Position = apparati[0].Position,
            Components = apparati,
            Elevation = SenzaNil(Unisci(c[5])),
            Coverage = SenzaNil(Unisci(c[6])),
            Fra = SenzaNil(Unisci(c[7])),
            Remarks = SenzaNil(Unisci(c[8])) is { } note ? RxNumeroDellaNota().Replace(note, "\n") : null,
            Page = Pagina(r.Pagina),
        };
    }

    /// <summary>
    /// Una colonna divisa per apparato: ogni riga col nome di un apparato (<c>VOR</c>, <c>DME</c>) apre il suo testo; quel
    /// che viene prima di ogni nome è dell'apparato senza nome (la radioassistenza di un apparato solo).
    /// </summary>
    private static List<(string? Apparato, string Testo)> PerApparato(List<string> righe)
    {
        var pezzi = new List<(string? Apparato, List<string> Righe)>();
        foreach (var t in righe.Select(t => t.Trim()).Where(t => t.Length > 0))
        {
            if (RxTipo().IsMatch(t)) pezzi.Add((t, []));
            else if (pezzi.Count == 0) pezzi.Add((null, [t]));
            else pezzi[^1].Righe.Add(t);
        }
        return pezzi.Select(p => (p.Apparato, string.Join(' ', p.Righe))).ToList();
    }

    internal static NavaidKind Tipo(string tipo) => tipo switch
    {
        "VOR" => NavaidKind.Vor,
        "DVOR" => NavaidKind.Dvor,
        "DME" => NavaidKind.Dme,
        "TACAN" => NavaidKind.Tacan,
        "VOR/DME" => NavaidKind.VorDme,
        "DVOR/DME" => NavaidKind.DvorDme,
        "TVOR/DME" => NavaidKind.TvorDme,
        "VORTAC" => NavaidKind.Vortac,
        "DVORTAC" => NavaidKind.Dvortac,
        "NDB" => NavaidKind.Ndb,
        "L" => NavaidKind.Locator,
        _ => NavaidKind.Other,
    };

    /// <summary>
    /// Le righe delle tabelle di tutte le pagine, sotto la riga dei numeri delle colonne («1 2 3 …»). Una pagina senza la
    /// tabella (la bianca in fondo a ENR 4.1.2) si conta e basta; una con la tabella ma senza i bordi orizzontali si dice.
    /// </summary>
    internal static (List<Riga> Righe, List<ImportPage> Pagine, List<ImportIssue> Problemi) Righe(IReadOnlyList<PdfPageText> pages, int colonne)
    {
        var righe = new List<Riga>();
        var pagine = new List<ImportPage>();
        var problemi = new List<ImportIssue>();
        foreach (var page in pages)
        {
            var piane = Table.PlainLines(page);
            var data = PageInfo.Date(piane);
            var griglia = ItalianEnr3.Griglia(page, colonne);
            var pagina = new ImportPage(page.Number, PageInfo.Reference(piane), data, griglia is not null);
            pagine.Add(pagina);
            if (griglia is null) continue;
            if (data is null) problemi.Add(new ImportIssue(ImportIssueKind.PageWithoutDate, null, page.Number));

            var bande = Table.RowBands(page, griglia);
            if (bande.Count == 0)
            {
                problemi.Add(new ImportIssue(ImportIssueKind.PageWithoutTable, null, page.Number, "senza i bordi delle righe"));
                continue;
            }
            var testo = Table.Lines(page, griglia);
            var sottoINumeri = false;
            foreach (var (cima, fondo) in bande)
            {
                var celle = Enumerable.Range(0, colonne).Select(_ => new List<string>()).ToArray();
                foreach (var l in testo.Where(l => l.Y >= cima && l.Y < fondo).OrderBy(l => l.Y))
                    celle[l.Column].Add(l.Text);
                if (!sottoINumeri)
                {
                    sottoINumeri = celle.Count(x => x.Count == 1) >= colonne - 1 && Unisci(celle[0]) == "1";
                    continue;
                }
                if (celle.Any(x => x.Count > 0)) righe.Add(new Riga(celle, pagina));
            }
            if (!sottoINumeri) problemi.Add(new ImportIssue(ImportIssueKind.PageWithoutTable, null, page.Number, "senza la riga dei numeri delle colonne"));
        }
        return (righe, pagine, problemi);
    }

    private static AipPage? Pagina(ImportPage p) => p.Reference is null ? null : new AipPage(p.Reference, p.Effective);

    private static string? Unisci(List<string> righe)
    {
        var t = RxSpazi().Replace(string.Join(' ', righe), " ").Trim();
        return t.Length == 0 ? null : t;
    }

    private static string? SenzaNil(string? t) => t is null or "NIL" ? null : t;

    private static string Tutto(List<string>[] celle) => string.Join(" | ", celle.Select(c => string.Join(' ', c)));
}
