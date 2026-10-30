using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// Un punto della posa: dove sta sulla pagina (<see cref="X"/>, <see cref="Y"/> in punti PDF, 1/72", dall'angolo in alto a
/// sinistra della pagina com'è vista) e dove sta sulla Terra. <see cref="Nome"/>: il punto che l'AOD ha scelto (un fix
/// dell'AIP, la soglia di una pista), o null se l'ha preso dalla mappa fuori da ogni punto.
/// </summary>
public sealed record PuntoDellaPosa(double X, double Y, double Lat, double Lon, string? Nome = null);

/// <summary>
/// La posa di una carta (carta «Gli aeroporti nel Lab», S3): quale pagina (<see cref="Pagina"/>, «AD 2 LIRN 5-3»), il suo
/// titolo e la data della pagina quando è stata posata, e i punti. Con la data si sa se la carta è cambiata da allora.
/// </summary>
public sealed record PosaDellaCarta(string Pagina, string Titolo, DateOnly? Data, IReadOnlyList<PuntoDellaPosa> Punti);

/// <summary>
/// Come una carta posata sta sulla mappa: una trasformazione affine dalla pagina (in punti PDF) al piano di Mercatore, quello
/// della mappa. Una carta d'avvicinamento è conforme (Lambert, o simile): su qualche decina di miglia, rispetto a Mercatore,
/// differisce di una similitudine più un poco di deformazione — due punti bastano, il terzo corregge il resto.
/// <list type="bullet">
/// <item>Due punti: una similitudine (scala, rotazione, spostamento; la pagina ha y in giù, la Terra il nord in su).</item>
/// <item>Tre o più: affine ai minimi quadrati; dal quarto in poi c'è anche uno scarto, il più grande in miglia.</item>
/// </list>
/// </summary>
public sealed class PosaSullaMappa
{
    // u = A x + B y + C ; v = D x + E y + F, con u la longitudine in radianti e v l'ordinata di Mercatore.
    private readonly double _a, _b, _c, _d, _e, _f;

    private PosaSullaMappa(double a, double b, double c, double d, double e, double f, double? scarto)
    {
        (_a, _b, _c, _d, _e, _f) = (a, b, c, d, e, f);
        ScartoMassimoNm = scarto;
    }

    /// <summary>Lo scarto più grande fra un punto della posa e dove la posa lo mette, in miglia; null con due o tre punti (lì è zero).</summary>
    public double? ScartoMassimoNm { get; }

    /// <summary>La posa dai punti, o null se non bastano (meno di due, due nello stesso posto, tre in fila).</summary>
    public static PosaSullaMappa? Da(IReadOnlyList<PuntoDellaPosa> punti)
    {
        ArgumentNullException.ThrowIfNull(punti);
        if (punti.Count < 2)
            return null;
        var uv = punti.Select(p => Mercatore(p.Lat, p.Lon)).ToArray();
        if (punti.Count == 2)
        {
            // La similitudine con la y della pagina girata: (x, -y) → (u, v) come numeri complessi, w = m z + q.
            double x1 = punti[0].X, y1 = -punti[0].Y, x2 = punti[1].X, y2 = -punti[1].Y;
            double dx = x2 - x1, dy = y2 - y1, du = uv[1].U - uv[0].U, dv = uv[1].V - uv[0].V;
            var quadro = dx * dx + dy * dy;
            if (quadro < 1e-9 || du * du + dv * dv < 1e-18)
                return null;
            // m = (du + i dv) / (dx + i dy)
            double mr = (du * dx + dv * dy) / quadro, mi = (dv * dx - du * dy) / quadro;
            double qr = uv[0].U - (mr * x1 - mi * y1), qi = uv[0].V - (mr * y1 + mi * x1);
            // In x, y della pagina (y in giù): u = mr x + mi y + qr; v = mi x - mr y + qi.
            return new PosaSullaMappa(mr, mi, qr, mi, -mr, qi, null);
        }

        // Ai minimi quadrati: le equazioni normali, una per u e una per v, con la stessa matrice [x y 1].
        double sxx = 0, sxy = 0, syy = 0, sx = 0, sy = 0, n = punti.Count;
        double sxu = 0, syu = 0, su = 0, sxv = 0, syv = 0, sv = 0;
        for (int i = 0; i < punti.Count; i++)
        {
            double x = punti[i].X, y = punti[i].Y, u = uv[i].U, v = uv[i].V;
            sxx += x * x; sxy += x * y; syy += y * y; sx += x; sy += y;
            sxu += x * u; syu += y * u; su += u;
            sxv += x * v; syv += y * v; sv += v;
        }
        double[,] m = { { sxx, sxy, sx }, { sxy, syy, sy }, { sx, sy, n } };
        var det = Determinante(m);
        var scala = Math.Max(1, sxx + syy);
        if (Math.Abs(det) < 1e-9 * scala * scala)
            return null;
        var (a, b, c) = Risolvi(m, det, sxu, syu, su);
        var (d, e, f) = Risolvi(m, det, sxv, syv, sv);
        var senza = new PosaSullaMappa(a, b, c, d, e, f, null);
        if (punti.Count == 3)
            return senza;
        var scarto = punti.Max(p => DistanzaNm(senza.SullaTerra(p.X, p.Y), new Coordinate(p.Lat, p.Lon)));
        return new PosaSullaMappa(a, b, c, d, e, f, scarto);
    }

    /// <summary>Dove va sulla Terra un punto della pagina.</summary>
    public Coordinate SullaTerra(double x, double y)
    {
        var u = _a * x + _b * y + _c;
        var v = _d * x + _e * y + _f;
        return new Coordinate((2 * Math.Atan(Math.Exp(v)) - Math.PI / 2) * 180 / Math.PI, u * 180 / Math.PI);
    }

    /// <summary>
    /// Tre angoli della pagina sulla Terra — in alto a sinistra, in alto a destra, in basso a sinistra: la mappa ne fa la
    /// matrice dell'immagine (sul piano di Mercatore la posa è affine, e tre punti la dicono tutta).
    /// </summary>
    public IReadOnlyList<Coordinate> Angoli(double larghezza, double altezza) =>
        [SullaTerra(0, 0), SullaTerra(larghezza, 0), SullaTerra(0, altezza)];

    /// <summary>Quante miglia è largo un centimetro della pagina, al centro (per un colpo d'occhio sulla scala).</summary>
    public double MigliaPerCentimetro(double larghezza, double altezza)
    {
        const double PuntiPerCm = 72 / 2.54;
        double cx = larghezza / 2, cy = altezza / 2;
        return DistanzaNm(SullaTerra(cx, cy), SullaTerra(cx + PuntiPerCm, cy));
    }

    private static (double U, double V) Mercatore(double lat, double lon) =>
        (lon * Math.PI / 180, Math.Log(Math.Tan(Math.PI / 4 + lat * Math.PI / 360)));

    /// <summary>La distanza in miglia nautiche (sfera di 3 440,065 NM).</summary>
    public static double DistanzaNm(Coordinate da, Coordinate a)
    {
        double f1 = da.LatitudeDeg * Math.PI / 180, f2 = a.LatitudeDeg * Math.PI / 180;
        double df = f2 - f1, dl = (a.LongitudeDeg - da.LongitudeDeg) * Math.PI / 180;
        var h = Math.Sin(df / 2) * Math.Sin(df / 2) + Math.Cos(f1) * Math.Cos(f2) * Math.Sin(dl / 2) * Math.Sin(dl / 2);
        return 2 * 3440.065 * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    private static double Determinante(double[,] m) =>
        m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1])
        - m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0])
        + m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);

    // Cramer: tre incognite, la matrice è già quella delle equazioni normali.
    private static (double, double, double) Risolvi(double[,] m, double det, double r0, double r1, double r2)
    {
        double Con(int colonna)
        {
            var c = (double[,])m.Clone();
            c[0, colonna] = r0; c[1, colonna] = r1; c[2, colonna] = r2;
            return Determinante(c) / det;
        }
        return (Con(0), Con(1), Con(2));
    }
}

/// <summary>
/// Le pose delle carte di uno scalo nel repo dell'AIP espanso (D4): <c>posa\&lt;ICAO&gt;.json</c>, accanto a
/// <c>correzioni\</c>, fuori dalle cartelle dei cicli — una carta che non cambia resta posata anche nel ciclo dopo. Le
/// immagini no: si rifanno dal PDF.
/// </summary>
public static class PoseDelleCarte
{
    public const string Cartella = "posa";

    private const int Versione = 1;

    private sealed record SulDisco(int Versione, string Scalo, IReadOnlyList<PosaDellaCarta> Carte);

    private static readonly JsonSerializerOptions Opzioni = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Un file del repo, non HTML: le lettere accentate dei titoli restano lettere, e il diff si legge.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string File(string espanso, string icao) =>
        Path.Combine(espanso, Cartella, icao.ToUpperInvariant() + ".json");

    /// <summary>Le pose di uno scalo; vuoto se il file non c'è.</summary>
    /// <exception cref="InvalidDataException">Il file c'è ma non si legge.</exception>
    public static IReadOnlyList<PosaDellaCarta> Leggi(string espanso, string icao)
    {
        var file = File(espanso, icao);
        if (!System.IO.File.Exists(file))
            return [];
        try
        {
            return JsonSerializer.Deserialize<SulDisco>(System.IO.File.ReadAllText(file), Opzioni)?.Carte ?? [];
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"«{Path.GetFileName(file)}» non si legge: {e.Message}", e);
        }
    }

    /// <summary>
    /// Scrive la posa di una carta (al posto di quella che c'era per la stessa pagina); senza punti la toglie, e senza pose il
    /// file se ne va. Le carte in ordine di pagina, i numeri a sei decimali: il file cambia solo dove cambia la posa.
    /// </summary>
    public static void Scrivi(string espanso, string icao, PosaDellaCarta posa)
    {
        ArgumentNullException.ThrowIfNull(posa);
        var carte = Leggi(espanso, icao).Where(c => c.Pagina != posa.Pagina).ToList();
        if (posa.Punti.Count > 0)
            carte.Add(posa with { Punti = [.. posa.Punti.Select(Arrotonda)] });
        var file = File(espanso, icao);
        if (carte.Count == 0)
        {
            if (System.IO.File.Exists(file))
                System.IO.File.Delete(file);
            return;
        }
        var ordinate = carte.OrderBy(c => c.Pagina, StringComparer.Ordinal).ToList();
        var testo = JsonSerializer.Serialize(new SulDisco(Versione, icao.ToUpperInvariant(), ordinate), Opzioni) + "\n";
        AipEspanso.ScriviAlPosto(file, flusso => flusso.Write(System.Text.Encoding.UTF8.GetBytes(testo)));
    }

    private static PuntoDellaPosa Arrotonda(PuntoDellaPosa p) =>
        p with { X = Math.Round(p.X, 2), Y = Math.Round(p.Y, 2), Lat = Math.Round(p.Lat, 6), Lon = Math.Round(p.Lon, 6) };

    /// <summary>«41°48'02"N 012°14'13"E», come la stampa l'AIP.</summary>
    public static string InGradi(double lat, double lon) =>
        $"{Gms(lat, 2, lat < 0 ? 'S' : 'N')} {Gms(lon, 3, lon < 0 ? 'W' : 'E')}";

    private static string Gms(double gradi, int cifre, char lato)
    {
        var secondi = (int)Math.Round(Math.Abs(gradi) * 3600);
        return $"{(secondi / 3600).ToString(new string('0', cifre), CultureInfo.InvariantCulture)}°{secondi / 60 % 60:00}'{secondi % 60:00}\"{lato}";
    }
}
