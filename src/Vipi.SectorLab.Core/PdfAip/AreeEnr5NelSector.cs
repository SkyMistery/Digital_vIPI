using System.Globalization;
using System.Text.RegularExpressions;
using Aip;
using Aip.Aurora;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// Le aree di ENR 5 scritte come le tiene il sector (carta F6, slice 4c): in <c>GEO/italy.prohibit</c>, <c>.restrict</c>,
/// <c>.danger</c>, un blocco per area — il commento <c>//NOME</c> e una riga per lato,
/// <c>N041.37.29.000;E015.43.05.000;N041.37.23.236;E015.44.23.195;RESTRICT;R300AB;</c>, l'ultimo lato che torna sul primo
/// punto.
/// <list type="bullet">
/// <item>Il file dal tipo: P in <c>italy.prohibit</c>, D in <c>italy.danger</c>, il resto — R, TSA, TRA, CBA e le aree
/// militari di ENR 5.2 — in <c>italy.restrict</c> (committente, 19 ottobre: «italy.restrict»).</item>
/// <item>Il nome come li scrive il sector: senza <c>LI</c>, solo lettere e cifre per le aree di ENR 5.1 (<c>R107A</c>,
/// <c>TSA74A</c>); per quelle di ENR 5.2 il designatore in maiuscolo (<c>AAR BRAVO</c>, <c>CANDELO MASAZZA</c>), tranne
/// le INDIA, che il sector scrive attaccate (<c>INDIA2</c>).</item>
/// <item>🔴 I vertici dell'AIP escono identici (<see cref="Outline"/>); i cerchi a un punto ogni 3 gradi, come quelli che
/// il sector ha (P140, P154); i corridoi col loro contorno; i tratti di confine o di costa dal tracciato di riserva
/// (<c>itgeo.geo</c>), e senza non si scrivono.</item>
/// </list>
/// </summary>
public static partial class AreeEnr5NelSector
{
    /// <summary>I cerchi a un punto ogni 3 gradi (120 punti), come quelli del sector.</summary>
    public const double PuntiPerGrado = 1.0 / 3;

    [GeneratedRegex(@"\s+")]
    private static partial Regex RxSpazi();

    [GeneratedRegex(@"^INDIA\s+(?<n>\d+)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxIndia();

    /// <summary>Il file del sector dove va un'area: <c>italy.prohibit</c>, <c>italy.danger</c> o <c>italy.restrict</c>.</summary>
    public static string File(Airspace a)
    {
        ArgumentNullException.ThrowIfNull(a);
        return a.Type switch
        {
            AirspaceType.Prohibited => "italy.prohibit",
            AirspaceType.Danger => "italy.danger",
            _ => "italy.restrict",
        };
    }

    /// <summary>Il quinto campo delle righe di quel file: <c>PROHIBIT</c>, <c>DANGER</c>, <c>RESTRICT</c>.</summary>
    public static string Tipo(string file) => file switch
    {
        "italy.prohibit" => "PROHIBIT",
        "italy.danger" => "DANGER",
        _ => "RESTRICT",
    };

    /// <summary>Il nome dell'area come lo scrive il sector.</summary>
    public static string Nome(Airspace a)
    {
        ArgumentNullException.ThrowIfNull(a);
        if (a.Section.StartsWith("ENR 5.1", StringComparison.Ordinal) || a.Designator.StartsWith("LI", StringComparison.Ordinal))
            return ConfrontoDelleForme.NomeDellAreaEnr5(a);
        var nome = RxSpazi().Replace(a.Designator.Trim().ToUpperInvariant(), " ");
        return RxIndia().Match(nome) is { Success: true } m ? "INDIA" + m.Groups["n"].Value : nome;
    }

    /// <summary>
    /// Il blocco di un'area: <c>//NOME</c> e una riga per lato, l'ultimo che torna sul primo punto. Null se non si può
    /// scrivere (un tratto di confine senza tracciato), e <paramref name="perche"/> lo dice.
    /// </summary>
    public static IReadOnlyList<string>? Blocco(Airspace a, string file, BorderTracer? confini, out string? perche)
    {
        ArgumentNullException.ThrowIfNull(a);
        perche = null;
        var problemi = new List<AuroraIssue>();
        var punti = Outline.Points(a.Geometry, new OutlineOptions { PointsPerDegree = PuntiPerGrado, Border = confini }, problemi, a.Key);
        if (problemi.FirstOrDefault(p => p.Kind == AuroraIssueKind.BorderNotDrawn) is { } confine)
        {
            perche = $"{a.Key}: il tratto «{confine.Detail}» l'AIP non lo disegna, e il sector non ha la linea da cui prenderlo.";
            return null;
        }
        if (punti.Count < 2)
        {
            perche = $"{a.Key}: meno di due punti.";
            return null;
        }
        var nome = Nome(a);
        var tipo = Tipo(file);
        var righe = new List<string> { "//" + nome };
        for (var i = 0; i < punti.Count; i++)
            righe.Add($"{AuroraText.Coordinates(punti[i])};{AuroraText.Coordinates(punti[(i + 1) % punti.Count])};{tipo};{nome};");
        return righe;
    }

    /// <summary>Vero se la riga è un lato di quell'area: il sesto campo è il nome.</summary>
    public static bool EDellArea(string riga, string nome)
    {
        ArgumentNullException.ThrowIfNull(riga);
        var campi = riga.Split(';');
        return campi.Length >= 6 && string.Equals(campi[5].Trim(), nome, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Le righe dove il nome compare in un campo qualunque ma che non sono lati leggibili (nel 2610 R107A-D, e pezzi di
    /// P154 e P219: uno spazio al posto del «;» fra la longitudine e la latitudine dopo): l'area c'è, scritta male. Numeri da 1.
    /// </summary>
    public static IReadOnlyList<int> ScritteMale(IReadOnlyList<string> righe, string nome)
    {
        ArgumentNullException.ThrowIfNull(righe);
        var male = new List<int>();
        for (var i = 0; i < righe.Count; i++)
        {
            if (righe[i].TrimStart().StartsWith("//", StringComparison.Ordinal) || EDellArea(righe[i], nome)) continue;
            if (righe[i].Split(';').Any(c => string.Equals(c.Trim(), nome, StringComparison.OrdinalIgnoreCase))) male.Add(i + 1);
        }
        return male;
    }

    /// <summary>
    /// Le righe di una voce del sector da riscrivere: i lati con quel nome vicini all'area (entro <paramref name="raggioNm"/>
    /// dal suo centro: un'altra area con la stessa etichetta altrove resta) e il commento <c>//</c> subito sopra il primo.
    /// Indici da 0, in ordine.
    /// </summary>
    public static IReadOnlyList<int> RigheDellaVoce(IReadOnlyList<string> righe, string nome, Position centro, double raggioNm = 60)
    {
        ArgumentNullException.ThrowIfNull(righe);
        var sue = new List<int>();
        for (var i = 0; i < righe.Count; i++)
        {
            if (!EDellArea(righe[i], nome) || GeometryTextDelSector(righe[i]) is not { } p) continue;
            if (Sphere.DistanceNm(p, centro) <= raggioNm) sue.Add(i);
        }
        if (sue.Count > 0 && sue[0] > 0 && righe[sue[0] - 1].TrimStart().StartsWith("//", StringComparison.Ordinal))
            sue.Insert(0, sue[0] - 1);
        return sue;
    }

    /// <summary>Il primo punto di una riga di lato del sector (<c>N041.37.29.000;E015.43.05.000;…</c>), o null.</summary>
    private static Position? GeometryTextDelSector(string riga)
    {
        var campi = riga.Split(';');
        return campi.Length >= 2 && Gradi(campi[0]) is { } lat && Gradi(campi[1]) is { } lon ? new Position(lat, lon) : null;
    }

    private static double? Gradi(string campo)
    {
        var c = campo.Trim();
        if (c.Length < 2) return null;
        var parti = c[1..].Split('.');
        if (parti.Length != 4) return null;
        var n = parti.Select(p => double.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : double.NaN).ToArray();
        if (n.Any(double.IsNaN)) return null;
        var g = n[0] + n[1] / 60 + (n[2] + n[3] / 1000) / 3600;
        return c[0] is 'S' or 'W' ? -g : g;
    }

    /// <summary>Il centro di un gruppo di aree, per riconoscere le righe della loro voce.</summary>
    public static Position Centro(IEnumerable<Airspace> aree)
    {
        var punti = aree.SelectMany(a => Outline.Points(a.Geometry, new OutlineOptions { PointsPerDegree = PuntiPerGrado }, [], a.Key)).ToList();
        return punti.Count == 0 ? new Position(0, 0) : new Position(punti.Average(p => p.Lat), punti.Average(p => p.Lon));
    }
}
