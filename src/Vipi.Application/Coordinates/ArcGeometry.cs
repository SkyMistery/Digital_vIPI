namespace Vipi.Application.Coordinates;

/// <summary>
/// Archi e cerchi dei testi AIP, ridotti a vertici. Puro e deterministico (carta
/// <c>docs/feature/2026-09-18-f1-archi-convertitore.md</c> §2 e §3.5).
///
/// <para><b>Sulla sfera, non sull'ellissoide</b>: su 64 archi veri dell'AIP lo scarto fra estremo e centro è al
/// massimo 91 m sull'ellissoide WGS84 e 97 m sulla sfera. Le coordinate AIP sono arrotondate al secondo (~30 m):
/// la differenza sta sotto la precisione del dato, e una libreria geodetica non comprerebbe niente.</para>
///
/// <para>⚠️ Qui, e non in <see cref="CoordinateGeometry"/>: quella misura con l'approssimazione equirettangolare,
/// che basta a giudicare un perimetro a colpo d'occhio ma non a DISEGNARE un arco di 25 NM.</para>
/// </summary>
public static class ArcGeometry
{
    /// <summary>Raggio medio della Terra (IUGG), in NM.</summary>
    public const double RaggioTerraNm = 6371008.8 / 1852.0;

    /// <summary>Punti per grado d'arco, di base (decisione 13 della carta madre).</summary>
    public const double DensitaBase = 1;

    /// <summary>
    /// Il tetto della densità. I punti passano dal circuito Blazor, come il testo: un cerchio a 10 pt/° sono
    /// già 3600 vertici (carta F1 §8).
    /// </summary>
    public const double DensitaMassima = 10;

    /// <summary>Il pavimento: un punto ogni 10°. Sotto, un arco di 90° diventerebbe una corda.</summary>
    public const double DensitaMinima = 0.1;

    /// <summary>
    /// Oltre questo scarto fra il raggio dichiarato e la distanza vera di un estremo, l'arco si segnala. 0,1 NM
    /// (185 m): passano tutti i 128 estremi veri misurati, mentre un centro sbagliato o un incolla tagliato
    /// sbagliano di miglia, non di metri (carta F1 §2.2).
    /// </summary>
    public const double SogliaIncoerenzaNm = 0.1;

    /// <summary>Quanto due estremi devono somigliarsi per dire «l'arco fa il giro intero»: 1e-6° ≈ 11 cm.</summary>
    private const double StessoPunto = 1e-6;

    /// <summary>
    /// Un arco da <paramref name="inizio"/> a <paramref name="fine"/> attorno a <paramref name="centro"/>.
    ///
    /// <para>🔴 <b>Il raggio si interpola</b> fra la distanza vera dell'inizio e quella della fine, lungo
    /// l'angolo: così l'arco passa <b>esattamente</b> per i due punti dichiarati, che sono i vertici che l'area
    /// condivide coi lati vicini. Il raggio dichiarato serve solo a controllare (<see
    /// cref="ArcoCalcolato.ScartoNm"/>): usarlo per disegnare lascerebbe uno scalino fino a 91 m agli estremi.</para>
    ///
    /// <para>I punti comprendono <b>entrambi</b> gli estremi, identici a quelli ricevuti. Se inizio e fine
    /// coincidono l'arco è il giro intero.</para>
    /// </summary>
    /// <param name="orario">Il verso dichiarato dal testo: <c>clockwise</c>/<c>orario</c>.</param>
    /// <param name="puntiPerGrado">Densità, riportata fra <see cref="DensitaMinima"/> e <see cref="DensitaMassima"/>.</param>
    public static ArcoCalcolato Arco(
        (double Lat, double Lon) inizio,
        (double Lat, double Lon) fine,
        (double Lat, double Lon) centro,
        bool orario,
        double raggioDichiaratoNm,
        double puntiPerGrado = DensitaBase)
    {
        var r0 = DistanzaNm(centro, inizio);
        var r1 = DistanzaNm(centro, fine);
        var scarto = Math.Max(Math.Abs(r0 - raggioDichiaratoNm), Math.Abs(r1 - raggioDichiaratoNm));

        var b0 = RottaGradi(centro, inizio);
        var b1 = RottaGradi(centro, fine);

        // In senso orario la rotta dal centro CRESCE; in antiorario cala. Il giro intero è il caso degli
        // estremi uguali, non un'ampiezza zero: «till point of origin» su un'area fatta del solo arco.
        var ampiezza = Stesso(inizio, fine)
            ? 360.0
            : Modulo360(orario ? b1 - b0 : b0 - b1);
        var verso = orario ? 1.0 : -1.0;

        var tratti = Math.Max(1, (int)Math.Ceiling(ampiezza * Densita(puntiPerGrado) - 1e-9));
        var punti = new List<(double Lat, double Lon)>(tratti + 1) { inizio };
        for (var i = 1; i < tratti; i++)
        {
            var t = (double)i / tratti;
            punti.Add(Destinazione(centro, b0 + verso * ampiezza * t, r0 + (r1 - r0) * t));
        }
        punti.Add(fine);

        return new ArcoCalcolato(punti, scarto);
    }

    /// <summary>
    /// Un cerchio attorno a <paramref name="centro"/>: un anello di <c>360 × densità</c> punti, da nord in
    /// senso orario, <b>senza</b> ripetere il primo in fondo (il vertice di chiusura è una proprietà
    /// dell'anello, come in <see cref="CoordinateArea"/>).
    /// </summary>
    public static IReadOnlyList<(double Lat, double Lon)> Cerchio(
        (double Lat, double Lon) centro, double raggioNm, double puntiPerGrado = DensitaBase)
    {
        var quanti = Math.Max(3, (int)Math.Ceiling(360.0 * Densita(puntiPerGrado) - 1e-9));
        var punti = new List<(double Lat, double Lon)>(quanti);
        for (var i = 0; i < quanti; i++)
            punti.Add(Destinazione(centro, 360.0 * i / quanti, raggioNm));
        return punti;
    }

    /// <summary>Distanza sul cerchio massimo, in NM (formula dell'emisenoverso).</summary>
    public static double DistanzaNm((double Lat, double Lon) a, (double Lat, double Lon) b)
    {
        var la1 = Rad(a.Lat);
        var la2 = Rad(b.Lat);
        var dLa = la2 - la1;
        var dLo = Rad(b.Lon - a.Lon);
        var h = Math.Sin(dLa / 2) * Math.Sin(dLa / 2) +
                Math.Cos(la1) * Math.Cos(la2) * Math.Sin(dLo / 2) * Math.Sin(dLo / 2);
        return 2 * RaggioTerraNm * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    /// <summary>Rotta iniziale da <paramref name="a"/> verso <paramref name="b"/>, in gradi veri [0, 360).</summary>
    public static double RottaGradi((double Lat, double Lon) a, (double Lat, double Lon) b)
    {
        var la1 = Rad(a.Lat);
        var la2 = Rad(b.Lat);
        var dLo = Rad(b.Lon - a.Lon);
        var y = Math.Sin(dLo) * Math.Cos(la2);
        var x = Math.Cos(la1) * Math.Sin(la2) - Math.Sin(la1) * Math.Cos(la2) * Math.Cos(dLo);
        return Modulo360(Math.Atan2(y, x) * 180.0 / Math.PI);
    }

    /// <summary>Il punto a <paramref name="distanzaNm"/> da <paramref name="origine"/> lungo la rotta data.</summary>
    public static (double Lat, double Lon) Destinazione((double Lat, double Lon) origine, double rottaGradi, double distanzaNm)
    {
        var d = distanzaNm / RaggioTerraNm;
        var th = Rad(rottaGradi);
        var la1 = Rad(origine.Lat);
        var lo1 = Rad(origine.Lon);
        var la2 = Math.Asin(Math.Sin(la1) * Math.Cos(d) + Math.Cos(la1) * Math.Sin(d) * Math.Cos(th));
        var lo2 = lo1 + Math.Atan2(Math.Sin(th) * Math.Sin(d) * Math.Cos(la1),
                                   Math.Cos(d) - Math.Sin(la1) * Math.Sin(la2));
        var lon = (lo2 * 180.0 / Math.PI + 540.0) % 360.0 - 180.0;
        return (la2 * 180.0 / Math.PI, lon);
    }

    private static double Densita(double puntiPerGrado) =>
        double.IsNaN(puntiPerGrado) ? DensitaBase : Math.Clamp(puntiPerGrado, DensitaMinima, DensitaMassima);

    private static double Modulo360(double gradi) => ((gradi % 360.0) + 360.0) % 360.0;

    private static double Rad(double gradi) => gradi * Math.PI / 180.0;

    private static bool Stesso((double Lat, double Lon) a, (double Lat, double Lon) b) =>
        Math.Abs(a.Lat - b.Lat) < StessoPunto && Math.Abs(a.Lon - b.Lon) < StessoPunto;
}

/// <summary>
/// Un arco ridotto a vertici, estremi compresi. <paramref name="ScartoNm"/> = di quanto l'estremo più lontano
/// dal raggio dichiarato se ne discosta.
/// </summary>
public sealed record ArcoCalcolato(IReadOnlyList<(double Lat, double Lon)> Punti, double ScartoNm)
{
    /// <summary>Lo scarto supera <see cref="ArcGeometry.SogliaIncoerenzaNm"/>: il centro o un estremo è sbagliato.</summary>
    public bool RaggioIncoerente => ScartoNm > ArcGeometry.SogliaIncoerenzaNm;
}
