using System.Text.RegularExpressions;

namespace Aip.Pdf.Italia;

/// <summary>Una pagina letta: il suo riferimento, la data AIRAC, e se aveva la tabella.</summary>
public sealed record ImportPage(int Number, string? Reference, DateOnly? Effective, bool Table);

/// <summary>Quel che l'importer ha da dire, legato all'area (<c>Key</c>) e alla pagina. Un codice, non una frase.</summary>
public sealed record ImportIssue(ImportIssueKind Kind, string? Key, int? Page, string? Detail = null);

public enum ImportIssueKind
{
    /// <summary>Una pagina senza la tabella (né sua né ereditata): il suo testo non è stato letto.</summary>
    PageWithoutTable,

    /// <summary>Una pagina senza la data AIRAC.</summary>
    PageWithoutDate,

    /// <summary>
    /// 🔴 Una riga della prima colonna che comincia come un identificativo (<c>LI …</c>, <c>EUC …</c>) e non lo è nella
    /// forma nota: la guardia della F0 — un identificativo fuori standard saldava l'area alla precedente.
    /// </summary>
    UnknownIdentifier,

    /// <summary>Il lettore delle geometrie ha qualcosa da dire. <c>Detail</c> = il suo codice e il dettaglio.</summary>
    Geometry,

    /// <summary>L'area non ha una geometria leggibile: non entra nel modello.</summary>
    NoGeometry,

    /// <summary>Il testo dell'area dava più di una geometria: si tiene la prima. <c>Detail</c> = quante.</summary>
    MoreGeometries,

    /// <summary>Meno di due limiti verticali (superiore e inferiore). <c>Detail</c> = quel che c'era.</summary>
    LimitsMissing,

    /// <summary>Più di due limiti verticali: si tengono i primi due. <c>Detail</c> = gli altri.</summary>
    LimitsExtra,

    /// <summary>
    /// I limiti verticali non sono nella tabella ma nel testo della sezione (le FMC area di ENR 2.1.1): presi da lì, e si
    /// dice. <c>Detail</c> = superiore / inferiore.
    /// </summary>
    LimitsFromText,

    /// <summary>Un file che l'importer non sa ancora leggere. <c>Detail</c> = il nome.</summary>
    Unsupported,

    /// <summary>
    /// Una rotta di ENR 3 che non torna: un punto senza coordinate, un tratto senza dati fra due punti, testo prima del
    /// primo punto che non è l'identificativo. <c>Detail</c> = cosa e dove.
    /// </summary>
    Route,

    /// <summary>Una riga di ENR 3.4.1 con un'attesa che non si legge (quota, virata o rotta d'avvicinamento). <c>Detail</c> = la riga.</summary>
    Holding,

    /// <summary>Una riga di ENR 4.1 che non è una radioassistenza leggibile (sigla, tipo, coordinate). <c>Detail</c> = cosa.</summary>
    Navaid,

    /// <summary>Una riga di ENR 4.4 senza un designatore di cinque lettere o senza coordinate. <c>Detail</c> = la riga.</summary>
    SignificantPoint,

    /// <summary>Una riga di ENR 4.5 senza nome, o con coordinate che non si leggono. <c>Detail</c> = cosa.</summary>
    Light,

    /// <summary>Uno scalo di AD 2 senza un dato che deve avere (ARP, piste) o con un dato che non si legge. <c>Detail</c> = cosa.</summary>
    Aerodrome,

    /// <summary>Un luogo di ENR 5.3 con coordinate che non si leggono. <c>Detail</c> = il testo.</summary>
    Hazard,
}

public sealed record ImportResult(IReadOnlyList<Airspace> Airspaces, IReadOnlyList<ImportPage> Pages, IReadOnlyList<ImportIssue> Issues)
{
    /// <summary>ENR 3: le rotte lette (slice 2).</summary>
    public IReadOnlyList<Route> Routes { get; init; } = [];

    /// <summary>ENR 3.4.1: le attese lette (slice 2).</summary>
    public IReadOnlyList<Holding> Holdings { get; init; } = [];

    /// <summary>ENR 4.1: le radioassistenze lette (slice 3).</summary>
    public IReadOnlyList<Navaid> Navaids { get; init; } = [];

    /// <summary>ENR 4.4: i punti significativi letti (slice 3).</summary>
    public IReadOnlyList<SignificantPoint> Points { get; init; } = [];

    /// <summary>ENR 4.5: le luci aeronautiche al suolo lette (slice 3).</summary>
    public IReadOnlyList<GroundLight> Lights { get; init; } = [];

    /// <summary>AD 2: gli scali letti (slice 5).</summary>
    public IReadOnlyList<Aerodrome> Aerodromes { get; init; } = [];

    /// <summary>ENR 5.3: i pericoli puntuali letti.</summary>
    public IReadOnlyList<Hazard> Hazards { get; init; } = [];

    /// <summary>AD 2: i settori della carta ATC SMAC letti.</summary>
    public IReadOnlyList<MinimumAltitudeArea> MinimumAltitudes { get; init; } = [];
}

/// <summary>
/// L'AIP italiano di ENAV. Oggi le tabelle di ENR 5.1 (P, R, D, TSA/TRA/CBA): 4 colonne — identificativo, nome e
/// limiti laterali · limite superiore e inferiore · tipo di attività · note — su pagine sfalsate fra pari e dispari
/// (carta madre §5; il prototipo della F0 è <c>sector-lab-f0\pdf-aip\enr2.py</c>). Le altre sezioni entrano con le loro
/// slice (carta F6 §6).
/// </summary>
public static partial class ItalianAip
{
    public const string State = "LI";

    // Gli identificativi di ENR 5.1, con le varianti fuori standard viste nel 2609 (`LI R48 A`, `LI R300/A Amendola`,
    // `LI/LD D35/A`, `LI TSA73 bis`): la forma della F0.
    [GeneratedRegex(@"^(?<id>LI(?:/L[A-Z])?\s*(?:TSA|TRA|TFR|P|R|D|T)\s*[0-9]+(?:\s*/?\s*(?:bis|ter|[A-Z])(?![a-z]))?)\s*(?:[-–]\s*)?(?<nome>[A-ZÀ-Ý""‘’(].*)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxIdentificativo();

    // Le aree transfrontaliere senza il prefisso dello Stato.
    [GeneratedRegex(@"^(?<id>EUC\s*[0-9]+[A-Z]?)\s*(?:[-–]\s*)?(?<nome>.*)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxIdentificativoEuc();

    // Quel che COMINCIA come un identificativo: se non è nella forma nota, la guardia lo dice.
    [GeneratedRegex(@"^(?:LI|LD|EUC)(?:[\s/]|\d)", RegexOptions.CultureInvariant)]
    private static partial Regex RxSembraIdentificativo();

    [GeneratedRegex(@"^(?:LI(?:/L[A-Z])?\s*)?(?<t>TSA|TRA|TFR|EUC|P|R|D|T)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxTipo();

    [GeneratedRegex(@"^(\(A\d+/\d+\)|ENAV\s*-\s*Roma|AIP\s*-\s*Italia|ENR\s*[\d.]+\s*-\s*\d+|AIRAC effective date.*|Nome e riferimento geografico|Name and location|Limiti laterali|Lateral limits)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex RxSporco();

    // Un limite comincia solo su una riga che parte con una quota: il resto è il seguito di quello prima ("2000ft" / "AMSL").
    [GeneratedRegex(@"^(UNL|GND|SFC|NIL|FL\s*\d{2,3}|\d[\d.,]*\s*(?:ft|FT|m|M|NM)\b)", RegexOptions.CultureInvariant)]
    internal static partial Regex RxInizioLimite();

    // Il seguito del nome: una riga senza gradi e senza le frasi della geometria.
    [GeneratedRegex(@"^(?![^°º]*[°º])(?!.*(?:Circular area|arc of circle|geographical border|from coast|to point of origin|till point))\S.*$", RegexOptions.CultureInvariant)]
    private static partial Regex RxSeguitoDelNome();

    [GeneratedRegex(@"^LI-ENR (?<s>3\.[123]) [A-Z]{1,2}\d+$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxRotte();

    [GeneratedRegex(@"^LI-AD 2 (?<icao>[A-Z]{4}) - (?<nome>.+) 1$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxTestoDiScalo();

    [GeneratedRegex(@"^LI-AD 2 (?<icao>[A-Z]{4}) - .+ \d+$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxCartaDiScalo();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RxSpazi();

    [GeneratedRegex(@"\s(?=\d{1,2}\)\s)")]
    private static partial Regex RxPuntoDellaNota();

    private static readonly string[] ParoleDiTesta =
    [
        "Identificazione", "Identification", "limiti laterali", "lateral limits", "Limite sup", "Limite inf", "Upper limit",
        "Lower limit", "Tipo di attivit", "Type of activity", "Note", "Remarks", "AIP - Italia", "AIRAC effective date",
        "ENR 5.1", "ENR 2.1",
    ];

    private const double TolleranzaY = 1.5;

    /// <summary>
    /// Un file dell'AIP italiano, scelto dal nome (<c>LI-ENR 5.1.2.pdf</c>). <paramref name="punto"/>: dove sta un punto con
    /// nome di ENR 4 (<see cref="ItalianEnr5Zone.Risolutore"/>), per le aree che si danno per punti (le AAR di ENR 5.2.2.1);
    /// chi legge più file legge ENR 4 prima.
    /// </summary>
    public static ImportResult ReadFile(string path, Func<string, Position?>? punto = null)
    {
        var nome = Path.GetFileNameWithoutExtension(path);
        // AD 2 (slice 5a): il testo di uno scalo è il suo primo PDF; gli altri sono carte.
        if (RxTestoDiScalo().Match(nome) is { Success: true } scalo)
            return ItalianAd2.Read(PdfText.Read(path), scalo.Groups["icao"].Value, scalo.Groups["nome"].Value.Trim());
        // Le carte di uno scalo: si legge la ATC SMAC (il testo dei settori); le altre sono immagini o SID e STAR (F7).
        if (RxCartaDiScalo().Match(nome) is { Success: true } carta)
        {
            var pagine = PdfText.Read(path);
            if (ItalianAd2.ESmac(pagine))
                return ItalianAd2.ReadSmac(pagine, carta.Groups["icao"].Value);
        }
        if (nome.StartsWith("LI-ENR 5.1.", StringComparison.Ordinal))
            return ReadEnr51(PdfText.Read(path));
        // ENR 5.2 (slice 4a): i poligoni militari, e le zone e i corridoi APR.
        if (nome.StartsWith("LI-ENR 5.2.1.", StringComparison.Ordinal))
            return ItalianEnr5.ReadFiringAreas(PdfText.Read(path));
        // ENR 5.2.2 (slice 4c, committente: «prendi anche AAR, AT e INDIA»).
        if (nome == "LI-ENR 5.2.2.1")
            return ItalianEnr5Zone.ReadAirRefuelling(PdfText.Read(path), punto);
        if (nome == "LI-ENR 5.2.2.2")
            return ItalianEnr5Zone.ReadIndia(PdfText.Read(path));
        if (nome == "LI-ENR 5.2.2.3")
            return ItalianEnr5Zone.ReadIndia5Sud(PdfText.Read(path));
        // ENR 5.3: palloni sonda, laser, vulcani; 5.3.4 sono regole a testo.
        if (nome == "LI-ENR 5.3.1")
            return ItalianEnr53.ReadBalloons(PdfText.Read(path));
        if (nome == "LI-ENR 5.3.2")
            return ItalianEnr53.ReadLasers(PdfText.Read(path));
        if (nome == "LI-ENR 5.3.3")
            return ItalianEnr53.ReadVolcanoes(PdfText.Read(path));
        if (nome == "LI-ENR 5.3.4")
            return ItalianEnr53.ReadRulesOnly(PdfText.Read(path));
        if (nome == "LI-ENR 5.2.2.4")
            return ItalianEnr5Zone.ReadOatCorridors(PdfText.Read(path));
        if (nome == "LI-ENR 5.2.2.6")
            return ItalianEnr5Zone.ReadTacticalAreas(PdfText.Read(path));
        if (nome == "LI-ENR 5.2.2.5")
            return ItalianEnr5.ReadUasAreas(PdfText.Read(path));
        // ENR 2 (slice 1a): il tipo di base dal file — i CTR uno per file, le ATZ militari, il FRAIT; il resto dal nome.
        if (nome.StartsWith("LI-ENR 2.1.2 ", StringComparison.Ordinal))
            return ItalianEnr2.Read(PdfText.Read(path), "ENR 2.1", AirspaceType.Ctr);
        if (nome == "LI-ENR 2.1.3")
            return ItalianEnr2.Read(PdfText.Read(path), "ENR 2.1", AirspaceType.Atz);
        if (nome.StartsWith("LI-ENR 2.1.1.", StringComparison.Ordinal))
            return ItalianEnr2.Read(PdfText.Read(path), "ENR 2.1", null);
        if (nome == "LI-ENR 2.2.1")
            return ItalianEnr2.Read(PdfText.Read(path), "ENR 2.2", AirspaceType.Fra);
        if (nome == "LI-ENR 2.2")
            return ItalianEnr2.Read(PdfText.Read(path), "ENR 2.2", null);
        // ENR 3 (slice 2): una rotta per file in 3.1-3.3, le attese in rotta in 3.4.1.
        if (RxRotte().Match(nome) is { Success: true } rotte)
            return ItalianEnr3.ReadRoutes(PdfText.Read(path), "ENR " + rotte.Groups["s"].Value);
        if (nome == "LI-ENR 3.4.1")
            return ItalianEnr3.ReadHoldings(PdfText.Read(path));
        if (nome == "LI-ENR 3.4.2")
            return ItalianEnr3.ReadNavaidHoldings(PdfText.Read(path));
        // ENR 4 (slice 3): le radioassistenze (4.1.2 le militari fuori dalla parte AD), i punti, le luci.
        if (nome is "LI-ENR 4.1.1" or "LI-ENR 4.1.2")
            return ItalianEnr4.ReadNavaids(PdfText.Read(path), military: nome == "LI-ENR 4.1.2");
        if (nome == "LI-ENR 4.4")
            return ItalianEnr4.ReadPoints(PdfText.Read(path));
        if (nome == "LI-ENR 4.5")
            return ItalianEnr4.ReadLights(PdfText.Read(path));
        return new ImportResult([], [], [new ImportIssue(ImportIssueKind.Unsupported, null, null, Path.GetFileName(path))]);
    }

    /// <summary>Le pagine di un PDF di ENR 5.1, nel modello.</summary>
    public static ImportResult ReadEnr51(IReadOnlyList<PdfPageText> pages)
    {
        var problemi = new List<ImportIssue>();
        var pagine = new List<ImportPage>();
        var righe = new List<(int Indice, int Pagina, double Y, int Col, string Testo)>();
        var ultima = new Dictionary<int, TableGrid>();

        for (var k = 0; k < pages.Count; k++)
        {
            var page = pages[k];
            var testo = Table.PlainLines(page);
            var data = PageInfo.Date(testo);
            if (data is null) problemi.Add(new ImportIssue(ImportIssueKind.PageWithoutDate, null, page.Number));

            var griglia = Table.Find(page, rules: 5, minSpan: 400);
            if (griglia is not null) ultima[page.Number % 2] = griglia;
            else griglia = Table.Inherit(page, ultima.GetValueOrDefault(page.Number % 2));

            pagine.Add(new ImportPage(page.Number, PageInfo.Reference(testo), data, griglia is not null));
            if (griglia is null)
            {
                problemi.Add(new ImportIssue(ImportIssueKind.PageWithoutTable, null, page.Number));
                continue;
            }

            foreach (var r in SenzaTesta(Table.Lines(page, griglia)))
                righe.Add((k, page.Number, r.Y, r.Column, r.Text));
        }

        righe.Sort((a, b) => (a.Indice, a.Y, a.Col).CompareTo((b.Indice, b.Y, b.Col)));

        var inizi = new List<int>();
        for (var i = 0; i < righe.Count; i++)
        {
            if (righe[i].Col != 0) continue;
            if (Identificativo(righe[i].Testo) is not null) inizi.Add(i);
            else if (RxSembraIdentificativo().IsMatch(righe[i].Testo))
                problemi.Add(new ImportIssue(ImportIssueKind.UnknownIdentifier, null, righe[i].Pagina, righe[i].Testo));
        }

        // Ogni riga va all'ultima area cominciata sopra di lei: le righe delle altre colonne appena SOPRA
        // l'identificativo (entro la tolleranza, il limite superiore stampato un filo più in alto) sono già della nuova.
        var colonnePerArea = inizi.Select(_ => new List<string>[] { [], [], [], [] }).ToList();
        var daInizio = new HashSet<int>(inizi);
        for (var i = 0; i < righe.Count; i++)
        {
            if (daInizio.Contains(i)) continue;
            var r = righe[i];
            var proprietaria = -1;
            for (var n = 0; n < inizi.Count; n++)
            {
                var s = righe[inizi[n]];
                if (r.Indice > s.Indice || (r.Indice == s.Indice && r.Y >= s.Y - TolleranzaY)) proprietaria = n;
                else break;
            }
            if (proprietaria >= 0) colonnePerArea[proprietaria][r.Col].Add(r.Testo);
        }

        var aree = new List<Airspace>();
        for (var n = 0; n < inizi.Count; n++)
        {
            var s = righe[inizi[n]];
            var (id, nome) = Identificativo(s.Testo)!.Value;
            var colonne = colonnePerArea[n];
            var pagina = pagine.First(p => p.Number == s.Pagina);
            var area = Area(id, nome, colonne, pagina, problemi);
            if (area is not null) aree.Add(area);
        }

        return new ImportResult(aree, pagine, problemi);
    }

    private static Airspace? Area(string id, string nome, List<string>[] colonne, ImportPage pagina, List<ImportIssue> problemi)
    {
        var geometria = colonne[0].ToList();
        var seguito = new List<string>();
        while (geometria.Count > 0 && RxSeguitoDelNome().IsMatch(geometria[0]))
        {
            seguito.Add(geometria[0]);
            geometria.RemoveAt(0);
        }
        var nomeIntero = RxSpazi().Replace((nome + " " + string.Join(' ', seguito)).Trim(), " ");
        var chiave = nomeIntero.Length > 0 ? $"{id} {nomeIntero}" : id;

        var letta = GeometryText.Read(string.Join('\n', geometria));
        foreach (var g in letta.Issues)
            problemi.Add(new ImportIssue(ImportIssueKind.Geometry, chiave, pagina.Number, g.Detail is null ? $"{g.Kind}" : $"{g.Kind}: {g.Detail}"));
        if (letta.Geometries.Count == 0)
        {
            problemi.Add(new ImportIssue(ImportIssueKind.NoGeometry, chiave, pagina.Number));
            return null;
        }
        if (letta.Geometries.Count > 1)
            problemi.Add(new ImportIssue(ImportIssueKind.MoreGeometries, chiave, pagina.Number, letta.Geometries.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        var limiti = UnisciLimiti(colonne[1]);
        if (limiti.Count < 2)
            problemi.Add(new ImportIssue(ImportIssueKind.LimitsMissing, chiave, pagina.Number, string.Join(" | ", limiti)));
        else if (limiti.Count > 2)
            problemi.Add(new ImportIssue(ImportIssueKind.LimitsExtra, chiave, pagina.Number, string.Join(" | ", limiti.Skip(2))));

        return new Airspace
        {
            Section = "ENR 5.1",
            Type = Tipo(id),
            Designator = id,
            Name = nomeIntero.Length > 0 ? nomeIntero : null,
            Upper = LimitText.Parse(limiti.Count > 0 ? limiti[0] : ""),
            Lower = LimitText.Parse(limiti.Count > 1 ? limiti[1] : ""),
            Activity = Unisci(colonne[2]),
            Remarks = Unisci(colonne[3]) is { } note ? RxPuntoDellaNota().Replace(note, "\n") : null,
            Geometry = letta.Geometries[0],
            Page = pagina.Reference is null ? null : new AipPage(pagina.Reference, pagina.Effective),
        };
    }

    internal static (string Id, string Nome)? Identificativo(string riga)
    {
        var m = RxIdentificativo().Match(riga);
        if (!m.Success) m = RxIdentificativoEuc().Match(riga);
        if (!m.Success) return null;
        return (RxSpazi().Replace(m.Groups["id"].Value, " ").Trim(), m.Groups["nome"].Value.Trim());
    }

    internal static AirspaceType Tipo(string id) => RxTipo().Match(id).Groups["t"].Value switch
    {
        "P" => AirspaceType.Prohibited,
        "R" => AirspaceType.Restricted,
        "D" => AirspaceType.Danger,
        "TSA" => AirspaceType.Tsa,
        "TRA" => AirspaceType.Tra,
        "EUC" => AirspaceType.Cba,
        _ => AirspaceType.Other,
    };

    internal static List<string> UnisciLimiti(IEnumerable<string> righe)
    {
        var limiti = new List<string>();
        foreach (var r in righe.Select(r => r.Trim()).Where(r => r.Length > 0))
        {
            if (limiti.Count > 0 && !RxInizioLimite().IsMatch(r)) limiti[^1] += " " + r;
            else limiti.Add(r);
        }
        return limiti;
    }

    private static string? Unisci(List<string> righe)
    {
        var t = RxSpazi().Replace(string.Join(' ', righe), " ").Trim();
        return t.Length == 0 ? null : t;
    }

    /// <summary>Via la testata della tabella (fino alla riga dei numeri di colonna «1 2 3 4») e le righe di cornice.</summary>
    internal static IEnumerable<TextLine> SenzaTesta(IReadOnlyList<TextLine> righe)
    {
        // La riga «1 2 3 4» non è allineata (in ENR 2.1.1.4.1 le celle 3 e 4 stanno 2,6 pt più in alto): a gruppi.
        double? fine = null;
        var numeri = righe.Where(r => r.Text.Trim() == (r.Column + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))
            .OrderBy(r => r.Y).ToList();
        var banda = new List<TextLine>();
        foreach (var r in numeri)
        {
            if (banda.Count > 0 && r.Y - banda[0].Y > 6.0)
            {
                if (banda.Select(b => b.Column).Distinct().Count() >= 3) fine = Math.Max(fine ?? 0, banda.Max(b => b.Y));
                banda = [];
            }
            banda.Add(r);
        }
        if (banda.Select(b => b.Column).Distinct().Count() >= 3) fine = Math.Max(fine ?? 0, banda.Max(b => b.Y));

        foreach (var r in righe)
        {
            if (fine is { } f && r.Y <= f + 1) continue;
            var t = r.Text.Trim();
            if (RxSporco().IsMatch(t)) continue;
            if (r.Column == 0 && t.Length < 40 && ParoleDiTesta.Any(p => t.Contains(p, StringComparison.Ordinal))) continue;
            yield return r;
        }
    }
}
