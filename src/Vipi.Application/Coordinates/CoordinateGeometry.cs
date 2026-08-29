namespace Vipi.Application.Coordinates;

/// <summary>
/// Il righello del convertitore: quanto è lungo il contorno, quanto misura l'area, e quanto si è perso
/// facendo il giro completo. Puro e deterministico.
///
/// <para>Serve a una domanda sola, che i numeri da soli non sanno porre: <i>ho incollato la cosa giusta?</i>
/// Con un errore di taglia-e-incolla il perimetro diventa assurdo e si vede subito, mentre un elenco di
/// coordinate sbagliate sembra un elenco di coordinate.</para>
///
/// <para>Distanze con l'approssimazione equirettangolare, come <see cref="Vipi.Application.Aor.PolygonGeometry"/>:
/// alle latitudini in cui operano le divisioni europee l'errore è sotto il per mille, e qui si misura per
/// giudicare a colpo d'occhio, non per navigare.</para>
/// </summary>
public static class CoordinateGeometry
{
    private const double NmPerGradoLat = 60.0;
    private const double MetriPerNm = 1852.0;

    /// <summary>Lunghezza del contorno in NM. Se l'anello è chiuso, comprende il lato che torna al primo punto.</summary>
    public static double PerimetroNm(IReadOnlyList<(double Lat, double Lon)> punti, bool chiuso)
    {
        if (punti.Count < 2) return 0;

        var totale = 0.0;
        for (var i = 0; i + 1 < punti.Count; i++) totale += DistanzaNm(punti[i], punti[i + 1]);
        if (chiuso) totale += DistanzaNm(punti[^1], punti[0]);
        return totale;
    }

    /// <summary>
    /// Area in NM², col metodo del laccio (shoelace) sui punti proiettati. Zero se i punti non bastano a
    /// chiudere una figura. ⚠️ Il valore è <b>assoluto</b>: il verso dell'anello (orario o antiorario) è
    /// un'informazione, non un segno da mostrare.
    /// </summary>
    public static double AreaNm2(IReadOnlyList<(double Lat, double Lon)> punti)
    {
        if (punti.Count < 3) return 0;

        var latMedia = punti.Average(p => p.Lat);
        var k = Math.Cos(latMedia * Math.PI / 180.0);

        var somma = 0.0;
        for (var i = 0; i < punti.Count; i++)
        {
            var a = punti[i];
            var b = punti[(i + 1) % punti.Count];
            var ax = a.Lon * k * NmPerGradoLat;
            var ay = a.Lat * NmPerGradoLat;
            var bx = b.Lon * k * NmPerGradoLat;
            var by = b.Lat * NmPerGradoLat;
            somma += ax * by - bx * ay;
        }
        return Math.Abs(somma) / 2.0;
    }

    /// <summary>
    /// L'errore massimo, in metri, fra due elenchi di punti presi nell'ordine. <c>null</c> se i due elenchi non
    /// hanno la stessa lunghezza — che non è «un errore grande», è <b>un'altra cosa</b>: significa che il giro
    /// ha perso o inventato un vertice, e un numero lo nasconderebbe.
    /// </summary>
    public static double? ErroreMassimoMetri(
        IReadOnlyList<(double Lat, double Lon)> a, IReadOnlyList<(double Lat, double Lon)> b)
    {
        if (a.Count != b.Count) return null;
        if (a.Count == 0) return 0;

        var peggio = 0.0;
        for (var i = 0; i < a.Count; i++) peggio = Math.Max(peggio, DistanzaNm(a[i], b[i]) * MetriPerNm);
        return peggio;
    }

    /// <summary>
    /// Il verso si inverte: orario ↔ antiorario, che per certi consumatori conta.
    ///
    /// <para>⚠️ Su un <b>anello</b> il primo vertice resta il primo e si rovescia il resto. Rovesciare
    /// l'elenco intero cambierebbe <i>due</i> cose — il verso <b>e</b> il punto di partenza — e chi ha chiesto
    /// «inverti» non ha chiesto la seconda. Il punto di partenza ha già il suo gesto
    /// (<see cref="Ruota"/>), e i due devono restare indipendenti.</para>
    ///
    /// <para>Su una <b>linea aperta</b> (una costa) è l'opposto: lì il primo punto è un capo, e invertire
    /// significa proprio percorrerla dall'altro capo. Per questo il verso di lettura è un parametro, non
    /// un'assunzione.</para>
    /// </summary>
    public static IReadOnlyList<(double Lat, double Lon)> Inverti(
        IReadOnlyList<(double Lat, double Lon)> punti, bool anelloChiuso = true)
    {
        if (punti.Count < 3 || !anelloChiuso) return punti.Reverse().ToList();

        var fuori = new List<(double Lat, double Lon)>(punti.Count) { punti[0] };
        for (var i = punti.Count - 1; i >= 1; i--) fuori.Add(punti[i]);
        return fuori;
    }

    /// <summary>
    /// Il primo vertice diventa un altro, di <paramref name="passi"/> posizioni più avanti. La forma non
    /// cambia: cambia da dove si comincia a scriverla, che è ciò che serve quando un consumatore vuole
    /// l'anello che parte da un punto preciso.
    /// </summary>
    public static IReadOnlyList<(double Lat, double Lon)> Ruota(
        IReadOnlyList<(double Lat, double Lon)> punti, int passi)
    {
        if (punti.Count < 2) return punti;
        var n = ((passi % punti.Count) + punti.Count) % punti.Count;
        if (n == 0) return punti;
        return punti.Skip(n).Concat(punti.Take(n)).ToList();
    }

    private static double DistanzaNm((double Lat, double Lon) a, (double Lat, double Lon) b)
    {
        var dLat = (b.Lat - a.Lat) * NmPerGradoLat;
        var dLon = (b.Lon - a.Lon) * NmPerGradoLat * Math.Cos((a.Lat + b.Lat) / 2.0 * Math.PI / 180.0);
        return Math.Sqrt(dLat * dLat + dLon * dLon);
    }
}
