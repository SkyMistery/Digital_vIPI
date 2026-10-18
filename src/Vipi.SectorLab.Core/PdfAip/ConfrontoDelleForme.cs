using System.Text.RegularExpressions;
using Aip;
using Aip.Aurora;
using Vipi.SectorLab.Core.Mappa;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>Com'è uno spazio dell'AIP rispetto al sector (carta F6 §3, «I gesti nel Lab»).</summary>
public enum EsitoDelConfronto
{
    Uguale,
    FormaDiversa,
    MancaNelSector,
    SoloNelSector,
}

/// <summary>
/// Una riga della lista delle differenze. <paramref name="ScartoAip"/>: quanto il contorno dell'AIP si stacca dalla linea
/// del sector, al massimo, in NM; <paramref name="ScartoSector"/>: quanto la linea del sector si stacca dai contorni
/// dell'AIP. <paramref name="Dove"/>: il punto dello scarto più grande. <paramref name="SenzaTracciato"/>: i pezzi di
/// sector lungo un confine o una costa che l'AIP nomina senza darne la forma, e che quindi non si giudicano.
/// </summary>
public sealed record DifferenzaDiForma(
    EsitoDelConfronto Esito,
    string? Aip,
    int Zone,
    string? Sector,
    string? File,
    int? Record,
    double? ScartoAip,
    double? ScartoSector,
    (double Lat, double Lon)? Dove,
    bool SenzaTracciato);

/// <summary>
/// Il confronto fra le forme dell'AIP e quelle del sector, in sola lettura (slice 1b). Il sector tiene un CTR come
/// <b>una linea</b> che passa per tutte le sue zone (la voce <c>LIPE CTR</c> del <c>MAPS</c> di <c>lipe.str</c>, coi
/// commenti «//zona 1»), l'AIP come un anello per zona: non si confrontano i vertici ma le forme, a distanza.
/// <list type="bullet">
/// <item>Le zone di un CTR dell'AIP vanno insieme: «Bologna CTR Zona/Zone '1'» … «'9'» sono «Bologna CTR».</item>
/// <item>Ogni CTR dell'AIP si abbina alla voce CTR del sector che gli passa più vicino (in media); una voce già
/// abbinata non si riusa.</item>
/// <item><b>Uguale</b> se ogni punto dei contorni dell'AIP sta entro la soglia (D4: 0,1 NM) dalla linea del sector, e
/// ogni punto della linea del sector entro la soglia da un contorno dell'AIP; altrimenti <b>forma diversa</b>, con i due
/// scarti e il punto peggiore.</item>
/// <item>I tratti di confine o di costa (l'AIP li nomina, non li disegna) non si giudicano: né i loro punti, né i pezzi
/// di sector che stanno più vicini a loro che ad altro.</item>
/// </list>
/// Distanze sul piano locale (equirettangolare attorno al CTR): a 50 NM lo scarto dalla sfera è sotto il metro.
/// </summary>
public static partial class ConfrontoDelleForme
{
    /// <summary>La soglia di base (D4, committente: 0,1 NM).</summary>
    public const double SogliaDiBase = 0.1;

    /// <summary>Oltre questa distanza media un CTR non è «quello» del sector, anche se è il più vicino.</summary>
    private const double AbbinamentoMassimo = 3.0;

    /// <summary>Ogni quanto si campiona un lato, in NM.</summary>
    private const double Passo = 0.05;

    [GeneratedRegex(@"^(?<ctr>.*?\bCTR\b)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture)]
    private static partial Regex RxNomeDelCtr();

    [GeneratedRegex(@"\bCTR\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxCtr();

    private sealed record Lato(double Ax, double Ay, double Bx, double By, bool Giudicabile);

    /// <summary>
    /// I CTR dell'AIP contro le voci CTR dei <c>MAPS</c> del sector. <paramref name="sector"/>: le forme della mappa del
    /// Lab (i punti per nome già risolti); si guardano quelle degli <c>.str</c> col nome che dice CTR.
    /// </summary>
    public static IReadOnlyList<DifferenzaDiForma> Ctr(IEnumerable<Airspace> aip, IEnumerable<FormaDellaMappa> sector, double soglia = SogliaDiBase)
    {
        var gruppi = aip.Where(a => a.Type == AirspaceType.Ctr)
            .GroupBy(a => NomeDelCtr(a.Designator), StringComparer.OrdinalIgnoreCase)
            .Select(g => (Nome: g.Key, Zone: g.ToList()))
            .OrderBy(g => g.Nome, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var voci = sector
            .Where(f => f.File.EndsWith(".str", StringComparison.OrdinalIgnoreCase) && RxCtr().IsMatch(f.Etichetta) && f.Tratti.Any(t => t.Count > 1))
            .ToList();

        // Il centro di riferimento della proiezione: uno per CTR.
        var candidati = new List<(int Gruppo, int Voce, double Media)>();
        var contorni = gruppi.Select(g => Contorni(g.Zone)).ToList();
        for (var g = 0; g < gruppi.Count; g++)
        {
            var (lat0, lon0) = Centro(contorni[g]);
            var latiAip = Proietta(contorni[g], lat0, lon0);
            for (var v = 0; v < voci.Count; v++)
            {
                var latiSector = ProiettaLaVoce(voci[v], lat0, lon0);
                if (!Vicini(latiAip, latiSector)) continue;
                var media = Campioni(latiAip.Where(l => l.Giudicabile)).Select(p => Distanza(p, latiSector).Distanza).DefaultIfEmpty(double.MaxValue).Average();
                if (media <= AbbinamentoMassimo) candidati.Add((g, v, media));
            }
        }

        var esiti = new List<DifferenzaDiForma>();
        var gruppoPreso = new HashSet<int>();
        var vocePresa = new HashSet<int>();
        foreach (var (g, v, _) in candidati.OrderBy(c => c.Media))
        {
            if (gruppoPreso.Contains(g) || vocePresa.Contains(v)) continue;
            gruppoPreso.Add(g);
            vocePresa.Add(v);
            esiti.Add(Misura(gruppi[g].Nome, gruppi[g].Zone.Count, contorni[g], voci[v], soglia));
        }
        for (var g = 0; g < gruppi.Count; g++)
            if (!gruppoPreso.Contains(g))
                esiti.Add(new DifferenzaDiForma(EsitoDelConfronto.MancaNelSector, gruppi[g].Nome, gruppi[g].Zone.Count, null, null, null, null, null, null, false));
        for (var v = 0; v < voci.Count; v++)
            if (!vocePresa.Contains(v))
                esiti.Add(new DifferenzaDiForma(EsitoDelConfronto.SoloNelSector, null, 0, voci[v].Etichetta, voci[v].File, voci[v].Record, null, null, null, false));

        return esiti
            .OrderBy(e => e.Esito switch { EsitoDelConfronto.FormaDiversa => 0, EsitoDelConfronto.MancaNelSector => 1, EsitoDelConfronto.SoloNelSector => 2, _ => 3 })
            .ThenBy(e => e.Aip ?? e.Sector, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Un punto come lo scrive l'AIP (<c>45°35'43"N 008°55'55"E</c>), per dire all'AOD dove guardare.</summary>
    public static string Dms((double Lat, double Lon) p)
    {
        static string Parte(double v, int cifre, char pos, char neg)
        {
            var s = (long)Math.Round(Math.Abs(v) * 3600);
            return string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"{(s / 3600).ToString(new string('0', cifre), System.Globalization.CultureInfo.InvariantCulture)}°{s / 60 % 60:00}'{s % 60:00}\"{(v < 0 ? neg : pos)}");
        }
        return Parte(p.Lat, 2, 'N', 'S') + " " + Parte(p.Lon, 3, 'E', 'W');
    }

    /// <summary>«Bologna CTR Zona/Zone '1'» → «Bologna CTR»; un nome senza CTR resta com'è.</summary>
    public static string NomeDelCtr(string designatore) =>
        RxNomeDelCtr().Match(designatore) is { Success: true } m ? m.Groups["ctr"].Value.Trim() : designatore.Trim();

    private static DifferenzaDiForma Misura(string nome, int zone, List<(Position A, Position B, bool Giudicabile)> contorno, FormaDellaMappa voce, double soglia)
    {
        var (lat0, lon0) = Centro(contorno);
        var latiAip = Proietta(contorno, lat0, lon0);
        var latiSector = ProiettaLaVoce(voce, lat0, lon0);

        // L'AIP contro il sector: solo i lati che l'AIP disegna.
        double scartoAip = 0;
        (double X, double Y) peggioreAip = default;
        foreach (var p in Campioni(latiAip.Where(l => l.Giudicabile)))
        {
            var d = Distanza(p, latiSector).Distanza;
            if (d > scartoAip) (scartoAip, peggioreAip) = (d, p);
        }

        // Il sector contro l'AIP: un pezzo di sector più vicino a un confine non disegnato che ad altro non si giudica.
        double scartoSector = 0;
        (double X, double Y) peggioreSector = default;
        var senzaTracciato = false;
        foreach (var p in Campioni(latiSector))
        {
            var (d, lato) = Distanza(p, latiAip);
            if (lato is { Giudicabile: false })
            {
                senzaTracciato = true;
                continue;
            }
            if (d > scartoSector) (scartoSector, peggioreSector) = (d, p);
        }

        var uguale = scartoAip <= soglia && scartoSector <= soglia;
        var peggiore = scartoAip >= scartoSector ? peggioreAip : peggioreSector;
        return new DifferenzaDiForma(
            uguale ? EsitoDelConfronto.Uguale : EsitoDelConfronto.FormaDiversa,
            nome, zone, voce.Etichetta, voce.File, voce.Record,
            Math.Round(scartoAip, 3), Math.Round(scartoSector, 3),
            uguale ? null : Indietro(peggiore, lat0, lon0),
            senzaTracciato);
    }

    /// <summary>I lati dei contorni delle zone: archi e cerchi disegnati fitti (4 punti per grado), i confini come lati non giudicabili.</summary>
    private static List<(Position A, Position B, bool Giudicabile)> Contorni(IEnumerable<Airspace> zone)
    {
        var lati = new List<(Position, Position, bool)>();
        foreach (var z in zone)
        {
            switch (z.Geometry)
            {
                case CircleGeometry c:
                    var cerchio = Outline.Circle(c, 4);
                    for (var i = 0; i < cerchio.Count; i++) lati.Add((cerchio[i], cerchio[(i + 1) % cerchio.Count], true));
                    break;
                case RingGeometry r:
                    for (var i = 0; i < r.Segments.Count; i++)
                    {
                        var s = r.Segments[i];
                        var fine = r.Segments[(i + 1) % r.Segments.Count].From;
                        switch (s)
                        {
                            case ArcSegment arco:
                                var punti = Outline.Arc(arco.From, fine, arco.Center, arco.Direction, 4).Append(fine).ToList();
                                for (var k = 0; k + 1 < punti.Count; k++) lati.Add((punti[k], punti[k + 1], true));
                                break;
                            case BorderSegment:
                                lati.Add((s.From, fine, false));
                                break;
                            default:
                                lati.Add((s.From, fine, true));
                                break;
                        }
                    }
                    break;
            }
        }
        return lati;
    }

    private static (double Lat, double Lon) Centro(List<(Position A, Position B, bool G)> lati) =>
        lati.Count == 0 ? (0, 0) : (lati.Average(l => l.A.Lat), lati.Average(l => l.A.Lon));

    private static List<Lato> Proietta(List<(Position A, Position B, bool Giudicabile)> lati, double lat0, double lon0)
    {
        var k = Math.Cos(lat0 * Math.PI / 180);
        return lati.Select(l => new Lato((l.A.Lon - lon0) * 60 * k, (l.A.Lat - lat0) * 60, (l.B.Lon - lon0) * 60 * k, (l.B.Lat - lat0) * 60, l.Giudicabile)).ToList();
    }

    private static List<Lato> ProiettaLaVoce(FormaDellaMappa voce, double lat0, double lon0)
    {
        var k = Math.Cos(lat0 * Math.PI / 180);
        var lati = new List<Lato>();
        foreach (var tratto in voce.Tratti)
            for (var i = 0; i + 1 < tratto.Count; i++)
                lati.Add(new Lato((tratto[i].LongitudeDeg - lon0) * 60 * k, (tratto[i].LatitudeDeg - lat0) * 60,
                    (tratto[i + 1].LongitudeDeg - lon0) * 60 * k, (tratto[i + 1].LatitudeDeg - lat0) * 60, true));
        return lati;
    }

    private static (double Lat, double Lon) Indietro((double X, double Y) p, double lat0, double lon0) =>
        (lat0 + p.Y / 60, lon0 + p.X / (60 * Math.Cos(lat0 * Math.PI / 180)));

    private static bool Vicini(List<Lato> a, List<Lato> b)
    {
        if (a.Count == 0 || b.Count == 0) return false;
        static (double X0, double Y0, double X1, double Y1) Scatola(List<Lato> l) =>
            (l.Min(x => Math.Min(x.Ax, x.Bx)), l.Min(x => Math.Min(x.Ay, x.By)), l.Max(x => Math.Max(x.Ax, x.Bx)), l.Max(x => Math.Max(x.Ay, x.By)));
        var (a0, a1, a2, a3) = Scatola(a);
        var (b0, b1, b2, b3) = Scatola(b);
        return a0 <= b2 + AbbinamentoMassimo && b0 <= a2 + AbbinamentoMassimo && a1 <= b3 + AbbinamentoMassimo && b1 <= a3 + AbbinamentoMassimo;
    }

    private static IEnumerable<(double X, double Y)> Campioni(IEnumerable<Lato> lati)
    {
        foreach (var l in lati)
        {
            var lunghezza = Math.Sqrt((l.Bx - l.Ax) * (l.Bx - l.Ax) + (l.By - l.Ay) * (l.By - l.Ay));
            var n = Math.Max(1, (int)Math.Ceiling(lunghezza / Passo));
            for (var i = 0; i <= n; i++)
            {
                var t = (double)i / n;
                yield return (l.Ax + (l.Bx - l.Ax) * t, l.Ay + (l.By - l.Ay) * t);
            }
        }
    }

    private static (double Distanza, Lato? Lato) Distanza((double X, double Y) p, List<Lato> lati)
    {
        var migliore = double.MaxValue;
        Lato? quale = null;
        foreach (var l in lati)
        {
            var dx = l.Bx - l.Ax;
            var dy = l.By - l.Ay;
            var len2 = dx * dx + dy * dy;
            var t = len2 == 0 ? 0 : Math.Clamp(((p.X - l.Ax) * dx + (p.Y - l.Ay) * dy) / len2, 0, 1);
            var qx = l.Ax + t * dx - p.X;
            var qy = l.Ay + t * dy - p.Y;
            var d = Math.Sqrt(qx * qx + qy * qy);
            if (d < migliore)
            {
                migliore = d;
                quale = l;
            }
        }
        return (migliore, quale);
    }
}
