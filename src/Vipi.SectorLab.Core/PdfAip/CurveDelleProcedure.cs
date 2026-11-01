using System.Globalization;
using System.Text.RegularExpressions;
using Aip;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// Le virate delle procedure lette dalla prosa disegnate come curve (carta «Le curve delle procedure», R1, R2, R4; D1, D2,
/// D4). Solo il disegno: i punti dell'AIP restano dove sono, e le RNAV non si toccano.
/// <list type="bullet">
/// <item>Un punto <b>col nome</b> (radioassistenza, fix) si sorvola sempre: la linea ci passa, poi vira. Così nel sector la
/// riga del punto resta (Aurora ne scrive il nome e il confronto lo ritrova) e una convenzionale vira sulla stazione.</item>
/// <item>Un punto <b>calcolato</b> (un incrocio di radiali, una distanza DME) si arrotonda con l'arco tangente ai due tratti
/// (fly-by); oltre i 120° si sorvola (D2).</item>
/// <item>Gli archi dell'AIP (<c>AF</c>, <c>RF</c>) e i loro punti restano come sono.</item>
/// </list>
/// Il raggio dalla velocità (la IAS MAX della prosa, o 210 kt per le SID e 220 per il resto) a 25° di inclinazione, senza
/// vento (D1). Una curva è un punto ogni 5°, come gli archi DME.
/// </summary>
public static partial class CurveDelleProcedure
{
    /// <summary>L'inclinazione della virata, in gradi (D1).</summary>
    public const double Inclinazione = 25;

    /// <summary>Le velocità quando la prosa non le dice (D1): SID e salite iniziali, il resto.</summary>
    public const double VelocitaDelleSid = 210, VelocitaDelleAltre = 220;

    /// <summary>Oltre questa virata (gradi) un punto calcolato si sorvola invece di arrotondarlo (D2).</summary>
    public const double OltreSiSorvola = 120;

    /// <summary>Sotto questa virata (gradi) non si disegna una curva.</summary>
    public const double SottoNonSiVira = 5;

    /// <summary>Un punto della curva ogni tanti gradi, come gli archi DME.</summary>
    public const double Passo = 5;

    [GeneratedRegex(@"\d{2,3}", RegexOptions.CultureInvariant)]
    private static partial Regex RxVelocita();

    /// <summary>Il raggio di virata (NM) a <paramref name="nodi"/> con l'inclinazione di <see cref="Inclinazione"/>.</summary>
    public static double Raggio(double nodi)
    {
        var v = nodi * 1852.0 / 3600.0;
        return v * v / (9.80665 * Math.Tan(Inclinazione * Math.PI / 180)) / 1852.0;
    }

    /// <summary>
    /// La velocità di una virata: quella stampata sul tratto che arriva al punto («-210», «IAS MAX 200 KT during turn»), o
    /// su quello che ne parte; se no quella che l'AIP dice per tutta la procedura (<paramref name="dellaProcedura"/>, R6: la sola
    /// IAS MAX del testo o delle REMARKS, <see cref="Procedure.TurnSpeed"/>), o quella di base.
    /// </summary>
    public static double Velocita(ProcedureKind tipo, ProcedureLeg? arriva, ProcedureLeg? parte, double? dellaProcedura = null) =>
        Stampata(arriva) ?? Stampata(parte) ?? dellaProcedura
        ?? (tipo is ProcedureKind.Sid or ProcedureKind.InitialClimb ? VelocitaDelleSid : VelocitaDelleAltre);

    /// <summary>La rotta vera di un tratto della prosa (<see cref="ProcedureLeg.TrueCourse"/>), da intercettare; solo per i CF.</summary>
    public static double? RottaVera(ProcedureLeg? t) =>
        t is { PathTerminator: "CF", TrueCourse: { } vera } && double.TryParse(vera, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static double? Stampata(ProcedureLeg? t)
    {
        if (t is null) return null;
        // La colonna della velocità («-210»), poi la nota della prosa («IAS MAX 200 KT during turn»).
        foreach (var testo in new[] { t.Speed, t.Notes?.Contains("IAS", StringComparison.OrdinalIgnoreCase) == true ? t.Notes : null })
            if (testo is not null && RxVelocita().Match(testo) is { Success: true } m
                && double.Parse(m.Value, CultureInfo.InvariantCulture) is >= 80 and <= 300 and var v)
                return v;
        return null;
    }

    /// <summary>Un punto del percorso, nell'ordine dei tratti.</summary>
    /// <param name="Punto">Dove sta.</param>
    /// <param name="ConNome">Un punto col nome: si sorvola.</param>
    /// <param name="Fisso">Non si vira qui: un punto di un arco dell'AIP, o di una procedura che non è della prosa.</param>
    /// <param name="Verso">Il verso della virata detto dalla prosa (<c>L</c>, <c>R</c>) sul tratto che parte dal punto.</param>
    /// <param name="RaggioNm">Il raggio della virata sul punto.</param>
    /// <param name="RottaDopo">La rotta vera del tratto che parte dal punto, se la prosa la dice («to join RDL 273 … bound to
    /// SUGEP»): se non va diritta al punto dopo, è una linea da intercettare.</param>
    public readonly record struct Vertice(Position Punto, bool ConNome, bool Fisso, string? Verso, double RaggioNm, double? RottaDopo = null);

    /// <summary>L'angolo con cui si intercetta una rotta (gradi), quello abituale.</summary>
    public const double AngoloDIntercettazione = 45;

    /// <summary>Sotto questo scarto (gradi) fra la rotta detta e il punto dopo, il tratto va diritto al punto.</summary>
    public const double SottoNonSIntercetta = 3;

    /// <summary>Un punto del disegno: <see cref="Vertice"/> è l'indice del vertice se è lui, null per un punto della curva.</summary>
    public readonly record struct Tappa(Position Punto, int? Vertice);

    /// <summary>
    /// Il disegno dei vertici: ognuno al suo posto, salvo i punti calcolati arrotondati (al posto loro l'arco), e dopo un
    /// punto sorvolato la curva fino alla rotta verso il punto dopo.
    /// </summary>
    public static IReadOnlyList<Tappa> Disegna(IReadOnlyList<Vertice> vertici)
    {
        ArgumentNullException.ThrowIfNull(vertici);
        var uscita = new List<Tappa>();
        for (var i = 0; i < vertici.Count; i++)
        {
            var b = vertici[i];
            // Il primo e l'ultimo, quelli fissi, e quelli dove non si arriva da un punto diverso: come sono.
            if (i == 0 || i == vertici.Count - 1 || b.Fisso || b.RaggioNm <= 0 || uscita.Count == 0)
            {
                uscita.Add(new Tappa(b.Punto, i));
                continue;
            }
            var da = uscita[^1].Punto;
            var c = vertici[i + 1].Punto;
            var piano = new Piano(b.Punto);
            var (ax, ay) = piano.Di(da);
            var (cx, cy) = piano.Di(c);
            double la = Math.Sqrt(ax * ax + ay * ay), lc = Math.Sqrt(cx * cx + cy * cy);
            if (la < 1e-6 || lc < 1e-6)
            {
                uscita.Add(new Tappa(b.Punto, i));
                continue;
            }
            // La rotta che arriva (da A verso B) e quella che parte (da B verso C), come versori.
            double ux = -ax / la, uy = -ay / la, wx = cx / lc, wy = cy / lc;
            // «turn right to join RDL 273 … bound to SUGEP»: da B si vira verso la linea, la si prende a 45° e la si segue.
            if (b.RottaDopo is { } rotta && Intercetta(piano, ux, uy, cx, cy, rotta, b.Verso, b.RaggioNm) is { } presa)
            {
                uscita.Add(new Tappa(b.Punto, i));
                uscita.AddRange(DopoIlVertice(presa, b.Punto).Select(p => new Tappa(p, null)));
                continue;
            }
            var corta = Math.Atan2(ux * wy - uy * wx, ux * wx + uy * wy) * 180 / Math.PI;   // >0 a sinistra
            var destra = b.Verso == "R" || (b.Verso != "L" && corta < 0);
            var virata = destra ? Normalizza(-corta) : Normalizza(corta);
            if (virata < SottoNonSiVira || virata > 360 - SottoNonSiVira)
            {
                uscita.Add(new Tappa(b.Punto, i));
                continue;
            }
            if (!b.ConNome && virata <= OltreSiSorvola && Raccordo(piano, ux, uy, wx, wy, virata, destra, b.RaggioNm, Math.Min(la, lc) / 2) is { } arco)
            {
                uscita.AddRange(arco.Select(p => new Tappa(p, null)));
                continue;
            }
            uscita.Add(new Tappa(b.Punto, i));
            uscita.AddRange(DopoIlVertice(Sorvolo(piano, ux, uy, cx, cy, destra, b.RaggioNm), b.Punto).Select(p => new Tappa(p, null)));
        }
        return uscita;
    }

    /// <summary>
    /// La curva che parte dal vertice senza il suo primo punto, che è il vertice stesso: nel sector sarebbe una riga doppia
    /// («LMT;LMT;» e subito la coordinata di LMT; banco, 1 novembre).
    /// </summary>
    private static IEnumerable<Position> DopoIlVertice(List<Position> curva, Position vertice) =>
        curva.Count > 0 && Math.Abs(curva[0].Lat - vertice.Lat) < 1e-9 && Math.Abs(curva[0].Lon - vertice.Lon) < 1e-9 ? curva.Skip(1) : curva;

    /// <summary>L'arco tangente ai due tratti (fly-by); il raggio si stringe se il tratto più corto non basta.</summary>
    private static List<Position>? Raccordo(Piano piano, double ux, double uy, double wx, double wy, double virata, bool destra, double raggio, double posto)
    {
        var tangente = Math.Tan(virata * Math.PI / 360);
        var l = raggio * tangente;
        if (l > posto)
        {
            l = posto;
            raggio = l / tangente;
        }
        if (raggio < 0.05) return null;
        // L'inizio dell'arco sul tratto che arriva, il centro sulla normale verso l'interno della virata.
        double t1x = -ux * l, t1y = -uy * l;
        var (nx, ny) = destra ? (uy, -ux) : (-uy, ux);
        double ox = t1x + nx * raggio, oy = t1y + ny * raggio;
        return Arco(piano, ox, oy, raggio, Math.Atan2(t1y - oy, t1x - ox), virata, destra, conLaFine: true);
    }

    /// <summary>
    /// Dopo un punto sorvolato: la virata dal punto, tangente alla rotta che arriva, finché la prua guarda il punto dopo. Se il
    /// punto dopo sta dentro il cerchio della virata non ci si arriva girando: niente curva, si va diritti.
    /// </summary>
    private static List<Position> Sorvolo(Piano piano, double ux, double uy, double cx, double cy, bool destra, double raggio)
    {
        var (nx, ny) = destra ? (uy, -ux) : (-uy, ux);
        double ox = nx * raggio, oy = ny * raggio;
        double dx = cx - ox, dy = cy - oy;
        var d = Math.Sqrt(dx * dx + dy * dy);
        if (d <= raggio * 1.01) return [];
        // I due punti di tangenza dal punto dopo; si prende quello dove il verso della virata guarda il punto dopo.
        var verso = Math.Atan2(dy, dx);
        var apertura = Math.Acos(raggio / d);
        var inizio = Math.Atan2(-oy, -ox);
        foreach (var t in new[] { verso + apertura, verso - apertura })
        {
            double px = ox + raggio * Math.Cos(t), py = oy + raggio * Math.Sin(t);
            // La velocità sull'arco: oraria (a destra) o antioraria.
            double vx = destra ? Math.Sin(t) : -Math.Sin(t), vy = destra ? -Math.Cos(t) : Math.Cos(t);
            if (vx * (cx - px) + vy * (cy - py) <= 0) continue;
            var ampiezza = destra ? Normalizza((inizio - t) * 180 / Math.PI) : Normalizza((t - inizio) * 180 / Math.PI);
            if (ampiezza < SottoNonSiVira) return [];
            return Arco(piano, ox, oy, raggio, inizio, ampiezza, destra, conLaFine: true);
        }
        return [];
    }

    /// <summary>
    /// L'intercettazione (R2): da B (l'origine del piano) si vira nel verso detto, col raggio, fino alla prua che prende la
    /// linea di <paramref name="rotta"/> per C a <see cref="AngoloDIntercettazione"/>°; diritti fino alla linea; il raccordo
    /// sulla linea. Null se la linea passa per B o va diritta a C (niente da intercettare), o se la si prenderebbe dopo C.
    /// </summary>
    private static List<Position>? Intercetta(Piano piano, double ux, double uy, double cx, double cy, double rotta, string? verso, double raggio)
    {
        var r = rotta * Math.PI / 180;
        double dx = Math.Sin(r), dy = Math.Cos(r);
        // La linea non ha un verso sicuro (una radiale): quello che porta a C.
        if (dx * cx + dy * cy < 0) (dx, dy) = (-dx, -dy);
        var lc = Math.Sqrt(cx * cx + cy * cy);
        var scarto = Math.Acos(Math.Clamp((dx * cx + dy * cy) / lc, -1, 1)) * 180 / Math.PI;
        // Quanto B sta a sinistra (>0) della linea, guardando verso C.
        var lato = dx * -cy - dy * -cx;
        if (scarto < SottoNonSIntercetta || Math.Abs(lato) < 0.1 || raggio <= 0) return null;
        // La prua di intercettazione: la linea girata di 45° verso di lei.
        var a = (lato > 0 ? -1 : 1) * AngoloDIntercettazione * Math.PI / 180;
        double hx = dx * Math.Cos(a) - dy * Math.Sin(a), hy = dx * Math.Sin(a) + dy * Math.Cos(a);
        // La virata da B fino a quella prua.
        var daU = Math.Atan2(uy, ux) * 180 / Math.PI;
        var aH = Math.Atan2(hy, hx) * 180 / Math.PI;
        var destra = verso == "R" || (verso != "L" && Normalizza(daU - aH) < 180);
        var ampiezza = destra ? Normalizza(daU - aH) : Normalizza(aH - daU);
        var (nx, ny) = destra ? (uy, -ux) : (-uy, ux);
        double ox = nx * raggio, oy = ny * raggio;
        List<Position> punti = ampiezza < SottoNonSiVira ? [] : Arco(piano, ox, oy, raggio, Math.Atan2(-oy, -ox), ampiezza, destra, conLaFine: true);
        var (ex, ey) = punti.Count > 0 ? piano.Di(punti[^1]) : (0.0, 0.0);
        // Dove la prua taglia la linea: E + t·h = C + k·d.
        var croce = hx * dy - hy * dx;
        if (Math.Abs(croce) < 1e-9) return null;
        var t = ((cx - ex) * dy - (cy - ey) * dx) / croce;
        double ix = ex + t * hx, iy = ey + t * hy;
        // Il raccordo di 45° sulla linea: comincia prima dell'incrocio e finisce prima di C.
        var l = raggio * Math.Tan(AngoloDIntercettazione * Math.PI / 360);
        if (t < l || (cx - ix) * dx + (cy - iy) * dy < l) return null;
        double t1x = ix - hx * l, t1y = iy - hy * l;
        var sulla = lato > 0;   // da h a d si gira a sinistra se la linea era a destra (h girata a destra di d)
        var (mx, my) = sulla ? (-hy, hx) : (hy, -hx);
        double qx = t1x + mx * raggio, qy = t1y + my * raggio;
        punti.AddRange(Arco(piano, qx, qy, raggio, Math.Atan2(t1y - qy, t1x - qx), AngoloDIntercettazione, !sulla, conLaFine: true));
        return punti;
    }

    /// <summary>I punti di un arco attorno a (<paramref name="ox"/>, <paramref name="oy"/>), uno ogni <see cref="Passo"/>°.</summary>
    private static List<Position> Arco(Piano piano, double ox, double oy, double raggio, double inizio, double ampiezza, bool destra, bool conLaFine)
    {
        // Un decimo di grado in più (la sfera) non è un passo in più.
        var passi = Math.Max(1, (int)Math.Ceiling(ampiezza / Passo - 0.02));
        var punti = new List<Position>();
        for (var k = 0; k <= passi; k++)
        {
            if (k == passi && !conLaFine) break;
            var a = inizio + (destra ? -1 : 1) * ampiezza * k / passi * Math.PI / 180;
            punti.Add(piano.Da(ox + raggio * Math.Cos(a), oy + raggio * Math.Sin(a)));
        }
        return punti;
    }

    private static double Normalizza(double gradi)
    {
        var r = gradi % 360;
        return r < 0 ? r + 360 : r;
    }

    /// <summary>Un piano in NM attorno a un punto (est, nord): per curve di qualche miglio basta.</summary>
    private readonly struct Piano(Position centro)
    {
        private readonly double _k = Math.Cos(centro.Lat * Math.PI / 180);

        public (double X, double Y) Di(Position p) => ((p.Lon - centro.Lon) * 60 * _k, (p.Lat - centro.Lat) * 60);

        public Position Da(double x, double y) => new(centro.Lat + y / 60, centro.Lon + x / 60 / _k);
    }
}
