using System.Globalization;
using System.Text.RegularExpressions;

namespace Aip.Pdf.Italia;

/// <summary>
/// ENR 5.3, gli altri pericoli (carta F6, «finiamo con l'importer»): luoghi, non aree con limiti — nel modello come
/// <see cref="Hazard"/>.
/// <list type="bullet">
/// <item><b>ENR 5.3.1</b>, i lanci di palloni sonda: 10 colonne — località · ora · peso · lunghezza · velocità · quota
/// interessata (m) · durata · notifica · esercente · valido fino a. Una località può avere più cerchi segnati «(A)», «(B)»
/// (AREA ETNEA): un pericolo per cerchio.</item>
/// <item><b>ENR 5.3.2</b>, le emissioni laser: 5 colonne — località · orario · limiti verticali (km) · note · valido fino
/// a.</item>
/// <item><b>ENR 5.3.3</b>, i vulcani attivi: 5 colonne — nome e numero Smithsonian · coordinate e altezza · misure di avviso
/// · ente · note.</item>
/// <item><b>ENR 5.3.4</b>, le operazioni coi droni in BVLOS: solo regole a testo, niente da mettere nel modello.</item>
/// </list>
/// </summary>
public static partial class ItalianEnr53
{
    private const RegexOptions Opzioni = RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

    [GeneratedRegex(@"^\((?<l>[A-Z])\)$", Opzioni)]
    private static partial Regex RxSegno();

    [GeneratedRegex(@"radius\s+(?<n>\d+(?:[.,]\d+)?)\s*(?<u>KM|NM|M)\b", Opzioni | RegexOptions.IgnoreCase)]
    private static partial Regex RxRaggio();

    [GeneratedRegex(@"^(?:Cerchio|Circle|centr|\d+(?:[.,]\d+)?\s*(?:KM|NM|M)\b)", Opzioni | RegexOptions.IgnoreCase)]
    private static partial Regex RxFraseDelCerchio();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RxSpazi();

    /// <summary>Una riga che comincia con dei gradi: la latitudine o la longitudine di una coppia stampata su due righe.</summary>
    [GeneratedRegex(@"^\d{2,3}\s*[°º˚]\s*\d{2}", Opzioni)]
    private static partial Regex RxRigaDiCoordinate();

    /// <summary>Una longitudine stampata a due cifre dopo la latitudine («41°54'06"N 12°30'57"E», Roma in ENR 5.3.2).</summary>
    [GeneratedRegex(@"(?<=[NS]\s+)(?<g>\d{2})(?=\s*[°º˚])", Opzioni)]
    private static partial Regex RxLongitudineCorta();

    [GeneratedRegex(@"\s(?=\d{1,2}[.)]\s?\D)")]
    private static partial Regex RxNumeroDellaNota();

    /// <summary>I lanci di palloni sonda di ENR 5.3.1.</summary>
    public static ImportResult ReadBalloons(IReadOnlyList<PdfPageText> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var (righe, pagine, problemi) = ItalianEnr4.Righe(pages, 10);
        var pericoli = new List<Hazard>();
        foreach (var r in righe)
        {
            var c = r.Celle;
            if (!ConCoordinate(c[0])) continue;
            var quota = ConUnita(SenzaNil(Unisci(c[5])), "m");
            var note = Note(c[8]);
            foreach (var (nome, testo) in Luoghi(c[0]))
                Aggiungi(pericoli, problemi, r, "ENR 5.3.1", HazardKind.Balloon, nome, testo, quota, Unisci(c[1]), note);
        }
        return new ImportResult([], pagine, problemi) { Hazards = pericoli };
    }

    /// <summary>Le emissioni laser di ENR 5.3.2.</summary>
    public static ImportResult ReadLasers(IReadOnlyList<PdfPageText> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var (righe, pagine, problemi) = ItalianEnr4.Righe(pages, 5);
        var pericoli = new List<Hazard>();
        foreach (var r in righe)
        {
            var c = r.Celle;
            if (!ConCoordinate(c[0])) continue;
            foreach (var (nome, testo) in Luoghi(c[0]))
                Aggiungi(pericoli, problemi, r, "ENR 5.3.2", HazardKind.Laser, nome, testo, ConUnita(SenzaNil(Unisci(c[2])), "km"), SenzaNil(Unisci(c[1])), Note(c[3]));
        }
        return new ImportResult([], pagine, problemi) { Hazards = pericoli };
    }

    /// <summary>I vulcani attivi di ENR 5.3.3: il nome col numero Smithsonian, l'altezza sotto le coordinate.</summary>
    public static ImportResult ReadVolcanoes(IReadOnlyList<PdfPageText> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var (righe, pagine, problemi) = ItalianEnr4.Righe(pages, 5);
        var pericoli = new List<Hazard>();
        foreach (var r in righe)
        {
            var c = r.Celle;
            if (c[0].Count == 0 || !ConCoordinate(c[1])) continue;
            var nome = Unisci(c[0])!;
            var coordinate = string.Join(' ', c[1].Where(l => RxRigaDiCoordinate().IsMatch(l.Trim())));
            var altezza = Unisci([.. c[1].Where(l => !RxRigaDiCoordinate().IsMatch(l.Trim()))]);
            var avviso = SenzaNil(Unisci(c[2]));
            var note = Note(c[4]);
            Aggiungi(pericoli, problemi, r, "ENR 5.3.3", HazardKind.Volcano, nome, coordinate, altezza, null,
                avviso is null ? note : $"{avviso}\n{note}");
        }
        return new ImportResult([], pagine, problemi) { Hazards = pericoli };
    }


    /// <summary>
    /// I luoghi di una cella della prima colonna: il nome (le righe prima delle coordinate e della frase del cerchio) e, per
    /// ogni segno «(A)», «(B)», un luogo col suo testo; senza segni, uno solo.
    /// </summary>
    private static List<(string Nome, string Testo)> Luoghi(List<string> cella)
    {
        var righe = cella.Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        var nome = Unisci([.. righe.TakeWhile(l => !RxRigaDiCoordinate().IsMatch(l) && !RxSegno().IsMatch(l) && !RxFraseDelCerchio().IsMatch(l))]) ?? "?";
        var luoghi = new List<(string, string)>();
        string? segno = null;
        var testo = new List<string>();
        foreach (var l in righe)
        {
            if (RxSegno().Match(l) is { Success: true } m)
            {
                if (segno is not null) luoghi.Add(($"{nome} ({segno})", string.Join(' ', testo)));
                segno = m.Groups["l"].Value;
                testo.Clear();
                continue;
            }
            testo.Add(l);
        }
        luoghi.Add((segno is null ? nome : $"{nome} ({segno})", string.Join(' ', testo)));
        return luoghi;
    }

    private static void Aggiungi(List<Hazard> pericoli, List<ImportIssue> problemi, ItalianEnr4.Riga r, string sezione, HazardKind genere,
        string nome, string testo, string? quota, string? orario, string? note)
    {
        if ((GeometryText.ReadPoint(testo) ?? GeometryText.ReadPoint(RxLongitudineCorta().Replace(testo, "0${g}"))) is not { } posizione)
        {
            problemi.Add(new ImportIssue(ImportIssueKind.Hazard, nome, r.Pagina.Number, $"coordinate: {testo}"));
            return;
        }
        Distance? raggio = null;
        if (RxRaggio().Match(testo) is { Success: true } m)
            raggio = new Distance(double.Parse(m.Groups["n"].Value.Replace(',', '.'), CultureInfo.InvariantCulture),
                m.Groups["u"].Value.ToUpperInvariant() switch { "KM" => DistanceUnit.Km, "NM" => DistanceUnit.Nm, _ => DistanceUnit.M });
        pericoli.Add(new Hazard
        {
            Section = sezione,
            Kind = genere,
            Name = nome,
            Position = posizione,
            Radius = raggio,
            Vertical = quota,
            Hours = orario,
            Remarks = note,
            Page = r.Pagina.Reference is null ? null : new AipPage(r.Pagina.Reference, r.Pagina.Effective),
        });
    }

    /// <summary>La cella ha una coppia di coordinate, anche su due righe e con la longitudine a due cifre.</summary>
    private static bool ConCoordinate(List<string> cella)
    {
        var testo = string.Join(' ', cella);
        return GeometryText.HasCoordinate(testo) || GeometryText.HasCoordinate(RxLongitudineCorta().Replace(testo, "0${g}"));
    }

    private static string? Note(List<string> righe) =>
        SenzaNil(Unisci(righe)) is { } n ? RxNumeroDellaNota().Replace(n, "\n") : null;

    private static string? Unisci(List<string> righe)
    {
        var t = RxSpazi().Replace(string.Join(' ', righe), " ").Trim();
        return t.Length == 0 ? null : t;
    }

    private static string? SenzaNil(string? t) => t is null or "NIL" ? null : t;

    /// <summary>Un numero da solo prende l'unità della colonna («20000» → «20000 m», «60» → «60 km»); il resto resta com'è («UNL», «GND/ 30 KM»).</summary>
    private static string? ConUnita(string? valore, string unita) =>
        valore is not null && double.TryParse(valore.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out _) ? $"{valore} {unita}" : valore;
}
