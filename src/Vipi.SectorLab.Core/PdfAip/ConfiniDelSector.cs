using Aip;
using Aip.Aurora;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// I tratti di confine e di costa dell'AIP disegnati sul tracciato di <c>GEO/itgeo.geo</c> del sector (committente,
/// 19 ottobre: «usiamo itgeo.geo per i confini, un'unica eccezione lì» alla regola dei dati solo da fonte primaria —
/// l'AIP nomina il confine, «Italian northern geographical border», ma non lo disegna; <c>itgeo.geo</c> ha tutti i
/// confini italiani, anche quelli di terra).
/// <para>Le linee del file sono una <b>rete</b>: i punti di una linea collegati in fila, e i capi delle linee aperte
/// collegati a quelli delle altre entro <see cref="Giunta"/>. Il tratto è la strada più corta sulla rete fra i punti più
/// vicini ai suoi due capi (entro <see cref="Tolleranza"/>).</para>
/// <para>⚠️ Perché una rete e non «la linea più vicina»: in <c>itgeo.geo</c> il confine delle Alpi va da Ventimiglia a
/// Gorizia, e le coste ripartono 3 NM più in là (e a Ventimiglia comincia anche la costa francese). Cercando la linea che
/// ha tutti e due i capi, la FIR di Milano faceva il giro delle coste da Muggia a Ventimiglia (misura del 19 ottobre).</para>
/// </summary>
public sealed class ConfiniDelSector
{
    /// <summary>Quanto un capo del tratto può stare lontano dal tracciato del sector, in NM.</summary>
    public const double Tolleranza = 4.0;

    /// <summary>Quanto possono stare lontani i capi di due linee che continuano l'una nell'altra, in NM.</summary>
    public const double Giunta = 5.0;

    private readonly List<Position> _punti = [];
    private readonly List<List<(int Verso, double Peso)>> _vicini = [];

    /// <summary>I lati delle linee (non le giunte): dove un capo dell'AIP entra nella rete.</summary>
    private readonly List<(int A, int B)> _lati = [];

    public ConfiniDelSector(IEnumerable<IReadOnlyList<Coordinate>> linee)
    {
        ArgumentNullException.ThrowIfNull(linee);
        var capi = new List<int>();
        foreach (var linea in linee.Where(l => l.Count > 1))
        {
            var primo = _punti.Count;
            foreach (var c in linea)
            {
                _punti.Add(new Position(c.LatitudeDeg, c.LongitudeDeg));
                _vicini.Add([]);
            }
            for (var i = primo; i + 1 < _punti.Count; i++)
            {
                Collega(i, i + 1);
                _lati.Add((i, i + 1));
            }
            var ultimo = _punti.Count - 1;
            if (Sphere.DistanceNm(_punti[primo], _punti[ultimo]) < 0.01)
            {
                Collega(primo, ultimo);
                continue;
            }
            capi.Add(primo);
            capi.Add(ultimo);
        }
        for (var i = 0; i < capi.Count; i++)
            for (var j = i + 1; j < capi.Count; j++)
                if (Sphere.DistanceNm(_punti[capi[i]], _punti[capi[j]]) <= Giunta)
                    Collega(capi[i], capi[j]);
        Linee = capi.Count / 2;
    }

    /// <summary>Quanti punti ha la rete: zero vuol dire che il sector non ha dato il file.</summary>
    public int Punti => _punti.Count;

    /// <summary>Le linee aperte (quelle che si giuntano).</summary>
    public int Linee { get; }

    private void Collega(int a, int b)
    {
        var peso = Sphere.DistanceNm(_punti[a], _punti[b]);
        _vicini[a].Add((b, peso));
        _vicini[b].Add((a, peso));
    }

    /// <summary>Il tracciato come lo vuole <see cref="Outline"/>: i punti fra i due capi, senza i capi.</summary>
    public BorderTracer Tracciatore => (_, da, a) => Traccia(da, a);

    /// <summary>I punti del sector fra <paramref name="da"/> e <paramref name="a"/>, senza i capi; null se non si trovano.</summary>
    public IReadOnlyList<Position>? Traccia(Position da, Position a)
    {
        // ⚠️ Non solo il punto più vicino: a Ventimiglia il più vicino sta sulla costa francese e a Muggia su quella
        // slovena, linee che non toccano la penisola. Si parte da tutti i punti entro la tolleranza (ognuno col suo scarto
        // dal capo) e si arriva al migliore fra quelli vicini all'altro capo.
        var partenze = Vicini(da);
        var arrivi = Vicini(a).ToDictionary(x => x.Indice, x => x.Distanza);
        if (partenze.Count == 0 || arrivi.Count == 0) return null;

        var distanza = new double[_punti.Count];
        var prima = new int[_punti.Count];
        Array.Fill(distanza, double.MaxValue);
        Array.Fill(prima, -1);
        var coda = new PriorityQueue<int, double>();
        foreach (var (i, d) in partenze)
        {
            distanza[i] = d;
            coda.Enqueue(i, d);
        }
        while (coda.TryDequeue(out var n, out var d))
        {
            if (d > distanza[n]) continue;
            foreach (var (v, peso) in _vicini[n])
            {
                var nuova = d + peso;
                if (nuova >= distanza[v]) continue;
                distanza[v] = nuova;
                prima[v] = n;
                coda.Enqueue(v, nuova);
            }
        }

        var fine = arrivi.Where(x => distanza[x.Key] < double.MaxValue)
            .Select(x => (Indice: x.Key, Totale: distanza[x.Key] + x.Value))
            .DefaultIfEmpty((Indice: -1, Totale: double.MaxValue)).MinBy(x => x.Totale).Indice;
        if (fine < 0) return null;

        var strada = new List<Position>();
        for (var n = fine; n >= 0; n = prima[n])
            strada.Add(_punti[n]);
        strada.Reverse();
        return strada;
    }

    /// <summary>
    /// Dove <paramref name="p"/> entra nella rete: i due capi di ogni lato a meno della tolleranza da lui, ognuno con quanto
    /// costa arrivarci (la distanza dal lato più il pezzo di lato fino al capo). ⚠️ Il lato, non il vertice: il confine di
    /// terra di <c>itgeo.geo</c> ha pochi vertici (le Alpi in 130 punti), e i punti dell'AIP gli stanno sopra a 3-4 NM dal
    /// vertice più vicino (misura del 19 ottobre: 25 tratti alpini restavano una retta).
    /// </summary>
    private List<(int Indice, double Distanza)> Vicini(Position p)
    {
        var migliori = new Dictionary<int, double>();
        var k = Math.Cos(p.Lat * Math.PI / 180);
        foreach (var (a, b) in _lati)
        {
            var (pa, pb) = (_punti[a], _punti[b]);
            // Un filtro in gradi prima del conto: il lato deve passare vicino.
            if (Math.Min(pa.Lat, pb.Lat) - p.Lat > 0.05 || p.Lat - Math.Max(pa.Lat, pb.Lat) > 0.05
                || Math.Min(pa.Lon, pb.Lon) - p.Lon > 0.07 || p.Lon - Math.Max(pa.Lon, pb.Lon) > 0.07) continue;
            // Sul piano locale, in NM: il piede della perpendicolare, e da lì ai due capi.
            double ax = (pa.Lon - p.Lon) * 60 * k, ay = (pa.Lat - p.Lat) * 60, bx = (pb.Lon - p.Lon) * 60 * k, by = (pb.Lat - p.Lat) * 60;
            double dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
            var t = l2 == 0 ? 0 : Math.Clamp(-(ax * dx + ay * dy) / l2, 0, 1);
            double fx = ax + t * dx, fy = ay + t * dy;
            var d = Math.Sqrt(fx * fx + fy * fy);
            if (d > Tolleranza) continue;
            var lato = Math.Sqrt(l2);
            foreach (var (n, costo) in new[] { (a, d + t * lato), (b, d + (1 - t) * lato) })
                if (!migliori.TryGetValue(n, out var c) || costo < c) migliori[n] = costo;
        }
        return [.. migliori.Select(x => (x.Key, x.Value))];
    }
}
