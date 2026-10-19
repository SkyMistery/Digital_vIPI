using System.Globalization;
using System.Text.RegularExpressions;

namespace Aip.Pdf.Italia;

/// <summary>
/// Le aree militari di ENR 5.2.2 che hanno una forma (carta F6, slice 4c, committente 19 ottobre: «prendi anche AAR, AT e
/// INDIA, sono comunque aree»):
/// <list type="bullet">
/// <item><b>ENR 5.2.2.1</b>, i corridoi per il rifornimento in volo (AAR): 5 colonne — nome · punti significativi ·
/// limiti · ampiezza (NM) · note. I punti sono coordinate compatte (<c>385200N 0090000E</c>) o nomi di ENR 4 (<c>BULAR</c>,
/// <c>TRP TACAN</c>, <c>ELB VORTAC</c>), uniti da «-»; con un'ampiezza sono un corridoio, con «NIL» un poligono chiuso
/// («line joining the following points», il primo punto ripetuto in fondo). I due blocchi di livelli («FL 280 / FL 260 o/or
/// FL 230 / FL 210») sono due fasce.</item>
/// <item><b>ENR 5.2.2.2</b>, le zone INDIA: 3 colonne — nome · limiti laterali · livelli di volo.</item>
/// <item><b>ENR 5.2.2.3</b>, la zona INDIA 5 SUD (I5S): testo, «linea congiungente i punti» e «limiti verticali».</item>
/// <item><b>ENR 5.2.2.4</b>, i corridoi del traffico aereo operativo (OAT): testo a due colonne, «Corridoio "YANKEE"», la
/// sua ampiezza («2 NM di ampiezza per ogni lato», «6 NM di larghezza»), i punti, i limiti verticali (sopra e sotto una riga
/// di trattini; non tutti li hanno) e le note.</item>
/// <item><b>ENR 5.2.2.6</b>, le aree tattiche (AT) del volo a bassissima quota: testo a due colonne, «AREA PIEMONTE» con
/// le sue coordinate fino a «to point of origin»; i limiti non sono nella tabella ma nelle regole sopra (ENR 5.2.2.6.4: la
/// quota minima entro le aree tattiche è 500 ft AGL per l'ala fissa, 150 per gli elicotteri; un limite superiore l'AIP non
/// lo dà).</item>
/// </list>
/// </summary>
public static partial class ItalianEnr5Zone
{
    private const RegexOptions Opzioni = RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

    [GeneratedRegex(@"^(?:linea congiungente i punti/?|line joining the|following points:?)$", Opzioni | RegexOptions.IgnoreCase)]
    private static partial Regex RxFraseDeiPunti();

    [GeneratedRegex(@"FL\s*\d{2,3}", Opzioni)]
    private static partial Regex RxLivello();

    [GeneratedRegex(@"^ENR\s+5\.2\.2\.\d+\.\d+", Opzioni)]
    private static partial Regex RxFineDellaTabella();

    [GeneratedRegex(@"^AREA\s+(?<nome>[A-Z][A-Z ]+?)\s*(?=\d|$)", Opzioni)]
    private static partial Regex RxAreaTattica();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RxSpazi();

    [GeneratedRegex(@"Corridoio\s+[""“](?<nome>[^""”]+?)\s*[""”]?(?:\s*-\s*\((?<verso>[^)]+)\))?\s*$", Opzioni)]
    private static partial Regex RxCorridoio();

    [GeneratedRegex(@"(?<n>\d+(?:,\d+)?)\s*NM di (?<come>ampiezza per ogni lato|larghezza)", Opzioni | RegexOptions.IgnoreCase)]
    private static partial Regex RxAmpiezza();

    [GeneratedRegex(@"^(?:ENR\s+5\.2\.2\.4\s*-\s*\d+|AIP\s*-\s*Italia.*|\d{2}\s+[A-Z]{3}\s+\d{4}.*|ENAV\s*-\s*Roma.*|\(\d{2}/\d{2}\).*)$", Opzioni)]
    private static partial Regex RxTestaOPiede();

    [GeneratedRegex(@"\s(?=\d{1,2}\.\D)")]
    private static partial Regex RxNumeroDellaNota();

    /// <summary>
    /// Dove sta un punto con nome, fra quelli già letti di ENR 4: un designatore di cinque lettere (<c>BULAR</c>) fra i
    /// punti; una sigla col tipo (<c>TRP TACAN</c>, <c>ELB VORTAC</c>) fra le radioassistenze, l'apparato di quel tipo se c'è.
    /// </summary>
    public static Func<string, Position?> Risolutore(IEnumerable<Navaid> radioassistenze, IEnumerable<SignificantPoint> punti)
    {
        ArgumentNullException.ThrowIfNull(radioassistenze);
        ArgumentNullException.ThrowIfNull(punti);
        return nome =>
        {
            var parti = nome.Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parti.Length == 0) return null;
            if (parti.Length == 1 && parti[0].Length == 5)
                return punti.FirstOrDefault(p => p.Designator == parti[0])?.Position;
            var sue = radioassistenze.Where(n => n.Designator == parti[0]).ToList();
            if (parti.Length > 1)
            {
                var tipo = parti[1];
                foreach (var n in sue)
                {
                    if (n.Components.FirstOrDefault(c => c.Equipment == tipo) is { } apparato) return apparato.Position;
                    if (n.Type == tipo) return n.Position;
                }
            }
            return sue.FirstOrDefault(n => n.Kind is not (NavaidKind.Ndb or NavaidKind.Locator))?.Position ?? sue.FirstOrDefault()?.Position;
        };
    }

    // --- AAR -----------------------------------------------------------------------------------------------------------

    /// <summary>I corridoi AAR di ENR 5.2.2.1. <paramref name="punto"/>: dove sta un punto con nome (ENR 4); null = nessuno.</summary>
    public static ImportResult ReadAirRefuelling(IReadOnlyList<PdfPageText> pages, Func<string, Position?>? punto)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var (righe, pagine, problemi) = ItalianEnr4.Righe(pages, 5);
        var aree = new List<Airspace>();
        foreach (var r in FinoAllaFine(righe))
        {
            var c = r.Celle;
            var nome = Unisci(c[0]);
            if (nome is null) continue;
            var testo = string.Join(' ', c[1].Select(Ricuci).Where(l => !RxFraseDeiPunti().IsMatch(l.Trim())));
            var (posizioni, nonTrovati) = Punti(testo, punto);
            foreach (var n in nonTrovati)
                problemi.Add(new ImportIssue(ImportIssueKind.Geometry, nome, r.Pagina.Number, $"punto non trovato in ENR 4: {n}"));
            var ampiezza = Unisci(c[3]);
            Geometry? geometria = null;
            if (nonTrovati.Count == 0 && posizioni.Count >= 2)
            {
                if (ampiezza is not null && double.TryParse(ampiezza, NumberStyles.Float, CultureInfo.InvariantCulture, out var nm))
                    geometria = new CorridorGeometry(new Distance(nm, DistanceUnit.Nm), posizioni);
                else
                    geometria = Anello(posizioni);
            }
            if (geometria is null)
            {
                problemi.Add(new ImportIssue(ImportIssueKind.NoGeometry, nome, r.Pagina.Number));
                continue;
            }
            // «FL 280», anche da una riga di lettere spaziate («F L 2 6 0» → «FL260» → «FL 260»).
            var livelli = c[2].Select(Ricuci).SelectMany(l => RxLivello().Matches(l).Select(m => "FL " + m.Value[2..].Trim())).ToList();
            if (livelli.Count < 2)
                problemi.Add(new ImportIssue(ImportIssueKind.LimitsMissing, nome, r.Pagina.Number, string.Join(" | ", livelli)));
            aree.Add(new Airspace
            {
                Section = "ENR 5.2",
                Type = AirspaceType.Military,
                Designator = nome,
                Upper = LimitText.Parse(livelli.FirstOrDefault() ?? ""),
                Lower = LimitText.Parse(livelli.LastOrDefault() ?? ""),
                // Due blocchi di tre livelli (FL 280-260 o FL 230-210): due fasce.
                Bands = livelli.Count == 4
                    ? [new VerticalBand(LimitText.Parse(livelli[0]), LimitText.Parse(livelli[1]), null), new VerticalBand(LimitText.Parse(livelli[2]), LimitText.Parse(livelli[3]), null)]
                    : null,
                Remarks = Note(c[4]),
                Geometry = geometria,
                Page = Pagina(r.Pagina),
            });
        }
        return new ImportResult(aree, pagine, problemi);
    }

    // --- INDIA ---------------------------------------------------------------------------------------------------------

    /// <summary>Le zone INDIA di ENR 5.2.2.2.</summary>
    public static ImportResult ReadIndia(IReadOnlyList<PdfPageText> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var (righe, pagine, problemi) = ItalianEnr4.Righe(pages, 3);
        var aree = new List<Airspace>();
        foreach (var r in FinoAllaFine(righe))
        {
            var c = r.Celle;
            if (c[0].Count == 0) continue;
            var nome = RxSpazi().Replace(c[0][0], " ").Trim();
            var riferimento = c[0].Count > 1 ? Unisci(c[0].Skip(1).ToList()) : null;
            var (posizioni, nonTrovati) = Punti(string.Join(' ', c[1]), null);
            if (nonTrovati.Count > 0 || posizioni.Count < 3)
            {
                problemi.Add(new ImportIssue(ImportIssueKind.NoGeometry, nome, r.Pagina.Number, string.Join(" | ", nonTrovati)));
                continue;
            }
            aree.Add(Zona(nome, riferimento, Unisci(c[2]) ?? "", Anello(posizioni), r.Pagina));
        }
        return new ImportResult(aree, pagine, problemi);
    }

    /// <summary>
    /// Una zona coi livelli di volo come li stampa l'AIP («FL 310 o più/or above», «FL 310 o/or FL 290»): sono i livelli
    /// usati, non una fascia; vanno com'è stampato nei due limiti.
    /// </summary>
    private static Airspace Zona(string nome, string? riferimento, string livelli, Geometry geometria, ImportPage pagina) => new()
    {
        Section = "ENR 5.2",
        Type = AirspaceType.Military,
        Designator = nome,
        Name = riferimento,
        Upper = LimitText.Parse(livelli),
        Lower = LimitText.Parse(livelli),
        Geometry = geometria,
        Page = Pagina(pagina),
    };

    /// <summary>La zona INDIA 5 SUD di ENR 5.2.2.3, dal testo: i punti dopo «linea congiungente i punti», i livelli dopo «limiti verticali».</summary>
    public static ImportResult ReadIndia5Sud(IReadOnlyList<PdfPageText> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var problemi = new List<ImportIssue>();
        var pagine = new List<ImportPage>();
        var aree = new List<Airspace>();
        foreach (var page in pages)
        {
            var piane = Table.PlainLines(page);
            var pagina = new ImportPage(page.Number, PageInfo.Reference(piane), PageInfo.Date(piane), Table: false);
            pagine.Add(pagina);
            var testo = string.Join('\n', piane);
            var da = testo.IndexOf("linea congiungente i punti", StringComparison.OrdinalIgnoreCase);
            var a = testo.IndexOf("limiti verticali", StringComparison.OrdinalIgnoreCase);
            if (da < 0 || a < da) continue;
            var (posizioni, _) = Punti(testo[da..a].Replace("linea congiungente i punti:", "", StringComparison.OrdinalIgnoreCase), null, soloCoordinate: true);
            var dopo = testo[a..];
            var fine = dopo.IndexOf("NOTA", StringComparison.Ordinal);
            var livelli = RxLivello().Matches(fine > 0 ? dopo[..fine] : dopo).Select(m => RxSpazi().Replace(m.Value, " ")).ToList();
            if (posizioni.Count < 3 || livelli.Count < 2)
            {
                problemi.Add(new ImportIssue(ImportIssueKind.NoGeometry, "INDIA 5 SUD", page.Number, $"{posizioni.Count} punti, {livelli.Count} livelli"));
                continue;
            }
            aree.Add(new Airspace
            {
                Section = "ENR 5.2",
                Type = AirspaceType.Military,
                Designator = "INDIA 5 SUD",
                Name = "(I5S)",
                Upper = LimitText.Parse(livelli[0]),
                Lower = LimitText.Parse(livelli[^1]),
                Geometry = Anello(posizioni),
                Page = Pagina(pagina),
            });
        }
        return new ImportResult(aree, pagine, problemi);
    }

    // --- OAT -----------------------------------------------------------------------------------------------------------

    /// <summary>
    /// I corridoi OAT di ENR 5.2.2.4 (carta F6, «finiamo con l'importer»): il testo delle pagine di seguito, la struttura dalla
    /// colonna italiana (la metà sinistra della pagina), le coordinate dalla riga intera (la colonna inglese non ne ha).
    /// L'ampiezza è quella intera: «2 NM di ampiezza per ogni lato» è un corridoio di 4 NM.
    /// </summary>
    public static ImportResult ReadOatCorridors(IReadOnlyList<PdfPageText> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var problemi = new List<ImportIssue>();
        var pagine = new List<ImportPage>();
        var aree = new List<Airspace>();
        string? nome = null, verso = null;
        double? ampiezza = null;
        ImportPage? daPagina = null;
        var punti = new List<Position>();
        var limiti = new List<string>();
        var note = new List<string>();
        var parte = "";

        void Chiudi()
        {
            if (nome is null) return;
            if (ampiezza is not { } nm || punti.Count < 2)
                problemi.Add(new ImportIssue(ImportIssueKind.NoGeometry, nome, daPagina?.Number, $"{punti.Count} punti, ampiezza {ampiezza}"));
            else
                aree.Add(new Airspace
                {
                    Section = "ENR 5.2",
                    Type = AirspaceType.Military,
                    Designator = nome,
                    Name = verso,
                    Upper = limiti.Count > 0 ? LimitText.Parse(limiti[0]) : new VerticalLimit { Text = "NIL" },
                    Lower = limiti.Count > 1 ? LimitText.Parse(limiti[1]) : new VerticalLimit { Text = "NIL" },
                    Remarks = Note(note),
                    Geometry = new CorridorGeometry(new Distance(nm, DistanceUnit.Nm), [.. punti]),
                    Page = daPagina is null ? null : Pagina(daPagina),
                });
            nome = verso = null;
            ampiezza = null;
            punti.Clear();
            limiti.Clear();
            note.Clear();
            parte = "";
        }

        foreach (var page in pages)
        {
            var piane = Table.PlainLinesWithY(page);
            var pagina = new ImportPage(page.Number, PageInfo.Reference([.. piane.Select(l => l.Text)]), PageInfo.Date([.. piane.Select(l => l.Text)]), Table: false);
            pagine.Add(pagina);
            var sinistra = Table.PlainLinesWithY(page with { Words = [.. page.Words.Where(w => w.X0 < page.Width * 0.5)] });
            foreach (var (y, riga) in piane)
            {
                var italiana = sinistra.Where(l => Math.Abs(l.Y - y) < 1.5).Select(l => l.Text.Trim()).FirstOrDefault() ?? "";
                if (RxTestaOPiede().IsMatch(riga.Trim())) continue;
                if (RxCorridoio().Match(italiana) is { Success: true } m)
                {
                    Chiudi();
                    nome = "Corridoio " + RxSpazi().Replace(m.Groups["nome"].Value, " ").Trim();
                    verso = m.Groups["verso"].Success ? m.Groups["verso"].Value.Trim() : null;
                    daPagina = pagina;
                    continue;
                }
                if (nome is null) continue;
                if (italiana.StartsWith("ENR 5.2.2.4.", StringComparison.Ordinal))
                {
                    Chiudi();
                    continue;
                }
                if (italiana.Contains("limiti laterali", StringComparison.OrdinalIgnoreCase)) { parte = "punti"; continue; }
                if (italiana.Contains("limiti verticali", StringComparison.OrdinalIgnoreCase)) { parte = "limiti"; continue; }
                if (italiana is "NOTE" || italiana.StartsWith("NOTE ", StringComparison.Ordinal)) { parte = "note"; continue; }
                switch (parte)
                {
                    case "punti":
                        if (RxAmpiezza().Match(riga) is { Success: true } a)
                        {
                            var n = double.Parse(a.Groups["n"].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
                            ampiezza = a.Groups["come"].Value.StartsWith("ampiezza", StringComparison.OrdinalIgnoreCase) ? 2 * n : n;
                        }
                        else punti.AddRange(GeometryText.ReadPoints(riga));
                        break;
                    case "limiti":
                        var limite = riga.Trim();
                        if (limite.Length > 0 && limite.Trim('_').Length > 0) limiti.Add(RxSpazi().Replace(limite, " "));
                        break;
                    case "note":
                        if (italiana.Length > 0) note.Add(italiana);
                        break;
                }
            }
        }
        Chiudi();
        return new ImportResult(aree, pagine, problemi);
    }

    // --- AT ------------------------------------------------------------------------------------------------------------

    /// <summary>Le aree tattiche di ENR 5.2.2.6: la colonna italiana, «AREA X» e le coordinate fino a «to point of origin».</summary>
    public static ImportResult ReadTacticalAreas(IReadOnlyList<PdfPageText> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var problemi = new List<ImportIssue>();
        var pagine = new List<ImportPage>();
        var aree = new List<Airspace>();
        foreach (var page in pages)
        {
            var piane = Table.PlainLines(page);
            var pagina = new ImportPage(page.Number, PageInfo.Reference(piane), PageInfo.Date(piane), Table: false);
            pagine.Add(pagina);
            // La colonna di sinistra, a righe: le coordinate vanno oltre la metà della pagina (fino a x 354 su 595), il nome
            // inglese comincia a 439 — si taglia al 70% della larghezza.
            var sinistra = page with { Words = [.. page.Words.Where(w => w.X0 < page.Width * 0.7)] };
            string? nome = null;
            var geometria = new List<string>();
            foreach (var riga in Table.PlainLines(sinistra))
            {
                if (RxAreaTattica().Match(riga) is { Success: true } m)
                {
                    nome = "AT " + RxSpazi().Replace(m.Groups["nome"].Value, " ").Trim();
                    geometria = [riga[m.Length..]];
                    continue;
                }
                if (nome is null) continue;
                geometria.Add(riga);
                if (!riga.Contains("point of origin", StringComparison.OrdinalIgnoreCase)) continue;
                var letta = GeometryText.Read(string.Join('\n', geometria));
                foreach (var g in letta.Issues)
                    problemi.Add(new ImportIssue(ImportIssueKind.Geometry, nome, page.Number, g.Detail is null ? $"{g.Kind}" : $"{g.Kind}: {g.Detail}"));
                if (letta.Geometries.Count == 1)
                    aree.Add(new Airspace
                    {
                        Section = "ENR 5.2",
                        Type = AirspaceType.Military,
                        Designator = nome,
                        Upper = new VerticalLimit { Text = "NIL" },
                        Lower = LimitText.Parse("500ft AGL"),
                        Remarks = "Quota minima 500 ft AGL per l'ala fissa, 150 ft AGL per gli elicotteri (ENR 5.2.2.6.4); un limite superiore l'AIP non lo dà.",
                        Geometry = letta.Geometries[0],
                        Page = Pagina(pagina),
                    });
                else
                    problemi.Add(new ImportIssue(ImportIssueKind.NoGeometry, nome, page.Number));
                nome = null;
            }
        }
        return new ImportResult(aree, pagine, problemi);
    }

    // --- in comune -----------------------------------------------------------------------------------------------------

    /// <summary>Le righe della tabella fino al primo paragrafo dopo (<c>ENR 5.2.2.1.2 Procedure speciali</c>).</summary>
    private static IEnumerable<ItalianEnr4.Riga> FinoAllaFine(IEnumerable<ItalianEnr4.Riga> righe) =>
        righe.TakeWhile(r => !(r.Celle[0].Count > 0 && RxFineDellaTabella().IsMatch(r.Celle[0][0].Trim())));

    /// <summary>Una riga di lettere spaziate («3 6 3 6 0 0 N 0 1 9 …», «F L 2 3 0»), com'è nel PDF di AAR DELTA, ricucita.</summary>
    internal static string Ricuci(string riga)
    {
        var parole = riga.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parole.Length > 3 && parole.All(p => p.Length == 1) ? string.Concat(parole) : riga;
    }

    /// <summary>I punti di un testo «A - B - C»: coordinate (anche compatte) o nomi di ENR 4. Torna anche i nomi non trovati.</summary>
    private static (List<Position> Punti, List<string> NonTrovati) Punti(string testo, Func<string, Position?>? punto, bool soloCoordinate = false)
    {
        var punti = new List<Position>();
        var nonTrovati = new List<string>();
        foreach (var pezzo in testo.Split(['-', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var p = RxSpazi().Replace(pezzo, " ").Trim().TrimEnd(';', ',');
            if (p.Length == 0) continue;
            if (GeometryText.ReadPoints(p) is { Count: > 0 } coordinate) punti.AddRange(coordinate);
            else if (soloCoordinate) continue;
            else if (punto?.Invoke(p) is { } trovato) punti.Add(trovato);
            else nonTrovati.Add(p);
        }
        return (punti, nonTrovati);
    }

    /// <summary>Un anello dai punti: il primo, ripetuto in fondo, non si ripete (la regola della 0a).</summary>
    private static RingGeometry Anello(List<Position> punti)
    {
        var p = punti.Count > 1 && punti[0] == punti[^1] ? punti.Take(punti.Count - 1) : punti;
        return new RingGeometry([.. p.Select(x => (Segment)new LineSegment(x))]);
    }

    private static string? Note(List<string> righe) =>
        Unisci(righe) is { } n && n != "NIL" ? RxNumeroDellaNota().Replace(n, "\n") : null;

    private static AipPage? Pagina(ImportPage p) => p.Reference is null ? null : new AipPage(p.Reference, p.Effective);

    private static string? Unisci(List<string> righe)
    {
        var t = RxSpazi().Replace(string.Join(' ', righe), " ").Trim();
        return t.Length == 0 ? null : t;
    }
}
