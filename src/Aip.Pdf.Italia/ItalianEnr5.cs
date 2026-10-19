using System.Globalization;
using System.Text.RegularExpressions;

namespace Aip.Pdf.Italia;

/// <summary>
/// ENR 5.2 dell'AIP italiano (carta F6, slice 4a): le aree militari che hanno una tabella con le geometrie.
/// <para><b>ENR 5.2.1</b> (i poligoni militari nelle FIR di Milano, Roma e Brindisi): l'impaginazione di ENR 5.1 — 4
/// colonne, nome e limiti laterali · limiti verticali · tipo di attività · note — ma senza identificativi <c>LI R…</c>:
/// la prima riga è il nome («ARPY», «CANDELO MASAZZA»), col riferimento geografico fra parentesi sotto o accanto.</para>
/// <para><b>ENR 5.2.2.5</b> (zone e corridoi per gli aeromobili a pilotaggio remoto): 5 colonne, nome e limiti laterali ·
/// limiti verticali · periodi di attività · ente ATS · note; i corridoi sono «Corridor 10 NM wide joining following
/// points» (<see cref="CorridorGeometry"/>).</para>
/// <para>Un'area comincia su una riga della prima colonna che è un nome — non coordinate, non una frase della geometria —
/// e ha, alla stessa altezza, l'inizio di un limite nella seconda: così il riferimento «( W Morgex )», che ha accanto
/// «AMSL», resta col nome sopra. Il resto va all'ultima area cominciata sopra, anche nella pagina dopo (come ENR 5.1).</para>
/// </summary>
public static partial class ItalianEnr5
{
    private const RegexOptions Opzioni = RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

    // Le frasi della geometria: dove ci sono, la riga non è un nome.
    [GeneratedRegex(@"[°º˚]|Circular area|arc of circle|geographical border|from coast|point of origin|till point|within a|radius|Corridor\s+\d+(?:[.,]\d+)?\s*(?:NM|KM)\s+wide|joining", Opzioni | RegexOptions.IgnoreCase)]
    private static partial Regex RxGeometria();

    // Il riferimento geografico fra parentesi, alla fine del nome: «ANDREA BAFILE ( N L'Aquila )».
    [GeneratedRegex(@"^(?<id>.*?)\s*(?<rif>\(.*\))\s*$", Opzioni)]
    private static partial Regex RxRiferimento();

    // Dove finisce la tabella: le note generali e i paragrafi che seguono (ENR 5.2.2.5 p.20: «NOTE GENERALI», poi
    // «ENR 5.2.2.5.2 Procedure speciali» con le sue tabelle dei tratti, che hanno righe verticali alle stesse x).
    [GeneratedRegex(@"^(?:NOTE GENERALI|GENERAL REMARKS|ENR\s+5\.\d+(?:\.\d+){2,}\s+\S)", Opzioni)]
    private static partial Regex RxFineDellaTabella();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RxSpazi();

    [GeneratedRegex(@"\s(?=\d{1,2}\)\s)")]
    private static partial Regex RxNumeroDellaNota();

    private const double Tolleranza = 3.0;

    /// <summary>I poligoni militari di un PDF di ENR 5.2.1.</summary>
    public static ImportResult ReadFiringAreas(IReadOnlyList<PdfPageText> pages) => Leggi(pages, 4);

    /// <summary>Le zone e i corridoi APR di ENR 5.2.2.5.</summary>
    public static ImportResult ReadUasAreas(IReadOnlyList<PdfPageText> pages) => Leggi(pages, 5);

    private sealed class AreaLetta
    {
        public required string Prima { get; init; }
        public required int Pagina { get; init; }
        public List<string>[] Colonne { get; init; } = [];
    }

    private static ImportResult Leggi(IReadOnlyList<PdfPageText> pages, int colonne)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var problemi = new List<ImportIssue>();
        var pagine = new List<ImportPage>();
        var righe = new List<(int Indice, int Pagina, double Y, int Col, string Testo)>();
        var ultima = new Dictionary<int, TableGrid>();
        var finita = false;

        for (var k = 0; k < pages.Count; k++)
        {
            var page = pages[k];
            var testo = Table.PlainLines(page);
            var data = PageInfo.Date(testo);
            // Dopo la fine della tabella le pagine si contano (con la loro data) e non si leggono.
            if (finita)
            {
                pagine.Add(new ImportPage(page.Number, PageInfo.Reference(testo), data, Table: false));
                continue;
            }
            var griglia = Table.Find(page, rules: colonne + 1, minSpan: 400);
            if (griglia is not null) ultima[page.Number % 2] = griglia;
            else griglia = Table.Inherit(page, ultima.GetValueOrDefault(page.Number % 2));
            pagine.Add(new ImportPage(page.Number, PageInfo.Reference(testo), data, griglia is not null));
            if (griglia is null) continue;
            if (data is null) problemi.Add(new ImportIssue(ImportIssueKind.PageWithoutDate, null, page.Number));
            foreach (var r in ItalianAip.SenzaTesta(Table.Lines(page, griglia)))
            {
                if (RxFineDellaTabella().IsMatch(r.Text.Trim()))
                {
                    finita = true;
                    break;
                }
                righe.Add((k, page.Number, r.Y, r.Column, r.Text.Trim()));
            }
        }
        righe.Sort((a, b) => (a.Indice, a.Y, a.Col).CompareTo((b.Indice, b.Y, b.Col)));

        // Gli inizi: un nome nella prima colonna con l'inizio di un limite accanto.
        var aree = new List<AreaLetta>();
        var inizio = new Dictionary<int, int>();
        for (var i = 0; i < righe.Count; i++)
        {
            var r = righe[i];
            if (r.Col != 0 || RxGeometria().IsMatch(r.Testo) || !char.IsLetter(r.Testo.FirstOrDefault(c => c != '(' && c != ' '))) continue;
            if (r.Testo.StartsWith('(')) continue;
            var accanto = righe.Any(x => x.Indice == r.Indice && x.Col == 1 && Math.Abs(x.Y - r.Y) <= Tolleranza && ItalianAip.RxInizioLimite().IsMatch(x.Testo));
            if (!accanto) continue;
            inizio[i] = aree.Count;
            aree.Add(new AreaLetta { Prima = r.Testo, Pagina = r.Pagina, Colonne = [.. Enumerable.Range(0, colonne).Select(_ => new List<string>())] });
        }

        // Ogni riga all'ultima area cominciata sopra di lei (le altre colonne appena SOPRA il nome sono già della nuova).
        var starts = inizio.Keys.Order().ToList();
        for (var i = 0; i < righe.Count; i++)
        {
            if (inizio.ContainsKey(i)) continue;
            var r = righe[i];
            var proprietaria = -1;
            foreach (var s in starts)
            {
                var a = righe[s];
                if (r.Indice > a.Indice || (r.Indice == a.Indice && r.Y >= a.Y - Tolleranza)) proprietaria = inizio[s];
                else break;
            }
            if (proprietaria >= 0) aree[proprietaria].Colonne[r.Col].Add(r.Testo);
            else if (r.Col == 0) problemi.Add(new ImportIssue(ImportIssueKind.UnknownIdentifier, null, r.Pagina, r.Testo));
        }

        var lette = new List<Airspace>();
        foreach (var a in aree)
        {
            var pagina = pagine.First(p => p.Number == a.Pagina);
            if (Area(a, colonne, pagina, problemi) is { } area) lette.Add(area);
        }
        return new ImportResult(lette, pagine, problemi);
    }

    private static Airspace? Area(AreaLetta a, int colonne, ImportPage pagina, List<ImportIssue> problemi)
    {
        // Il nome e il suo seguito: le righe prima della geometria (il riferimento fra parentesi sotto il nome).
        var geometria = a.Colonne[0].ToList();
        var nome = a.Prima;
        while (geometria.Count > 0 && !RxGeometria().IsMatch(geometria[0]))
        {
            nome += " " + geometria[0];
            geometria.RemoveAt(0);
        }
        nome = RxSpazi().Replace(nome, " ").Trim();
        var (id, rif) = RxRiferimento().Match(nome) is { Success: true } m && m.Groups["id"].Value.Length > 0
            ? (m.Groups["id"].Value.Trim(), (string?)RxSpazi().Replace(m.Groups["rif"].Value, " "))
            : (nome, null);
        var chiave = rif is null ? id : $"{id} {rif}";

        var letta = GeometryText.Read(string.Join('\n', geometria));
        foreach (var g in letta.Issues)
            problemi.Add(new ImportIssue(ImportIssueKind.Geometry, chiave, pagina.Number, g.Detail is null ? $"{g.Kind}" : $"{g.Kind}: {g.Detail}"));
        if (letta.Geometries.Count == 0)
        {
            problemi.Add(new ImportIssue(ImportIssueKind.NoGeometry, chiave, pagina.Number));
            return null;
        }
        if (letta.Geometries.Count > 1)
            problemi.Add(new ImportIssue(ImportIssueKind.MoreGeometries, chiave, pagina.Number, letta.Geometries.Count.ToString(CultureInfo.InvariantCulture)));

        var limiti = ItalianAip.UnisciLimiti(a.Colonne[1]);
        if (limiti.Count < 2)
            problemi.Add(new ImportIssue(ImportIssueKind.LimitsMissing, chiave, pagina.Number, string.Join(" | ", limiti)));
        else if (limiti.Count > 2)
            problemi.Add(new ImportIssue(ImportIssueKind.LimitsExtra, chiave, pagina.Number, string.Join(" | ", limiti.Skip(2))));

        var note = SenzaNil(Unisci(a.Colonne[colonne - 1])) is { } n ? RxNumeroDellaNota().Replace(n, "\n") : null;
        return new Airspace
        {
            Section = "ENR 5.2",
            Type = AirspaceType.Military,
            Designator = id,
            Name = rif,
            Upper = LimitText.Parse(limiti.Count > 0 ? limiti[0] : ""),
            Lower = LimitText.Parse(limiti.Count > 1 ? limiti[1] : ""),
            Activity = colonne == 4 ? Unisci(a.Colonne[2]) : null,
            Hours = colonne == 5 ? Unisci(a.Colonne[2]) : null,
            Unit = colonne == 5 ? Unisci(a.Colonne[3]) : null,
            Remarks = note,
            Geometry = letta.Geometries[0],
            Page = pagina.Reference is null ? null : new AipPage(pagina.Reference, pagina.Effective),
        };
    }

    private static string? Unisci(List<string> righe)
    {
        var t = RxSpazi().Replace(string.Join(' ', righe), " ").Trim();
        return t.Length == 0 ? null : t;
    }

    private static string? SenzaNil(string? t) => t is null or "NIL" ? null : t;
}
